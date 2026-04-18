using System;
using System.Numerics;
using Dalamud.Plugin.Services;
using Rota.Services.Ipc;

namespace Rota.Automation.Steps;

/// <summary>
/// Walk to a target Vector3 in the current zone via vnavmesh. Completes when
/// player position is within <see cref="ArrivalRadius"/> of the target, or
/// when vnavmesh reports its run has ended.
///
/// Does NOT cross zones — use TeleportStep first for that. If the player is
/// already within arrival radius at Start(), the step completes immediately.
/// </summary>
public sealed class WalkToStep : IStep
{
    private readonly VnavmeshIpc _vnav;
    private readonly IClientState _clientState;
    private readonly IObjectTable _objectTable;
    private readonly Vector3 _destination;
    private readonly float _arrivalRadius;
    private readonly TimeSpan _timeout;

    private DateTime _deadline;
    private DateTime _graceUntil;
    private bool _invoked;
    private bool _observedRunning;

    // vnavmesh.SimpleMove.PathfindAndMoveTo returns immediately but the path
    // subsystem takes a frame or two to mark itself running. We grant a short
    // grace window after Start() during which "not running" is not interpreted
    // as failure.
    private static readonly TimeSpan StartGrace = TimeSpan.FromSeconds(4);

    public string Name { get; }
    public StepState State { get; private set; } = StepState.Idle;
    public string? FailureReason { get; private set; }

    public WalkToStep(
        VnavmeshIpc vnav,
        IClientState clientState,
        IObjectTable objectTable,
        Vector3 destination,
        string displayName,
        float arrivalRadius = 3.0f,
        TimeSpan? timeout = null)
    {
        _vnav = vnav;
        _clientState = clientState;
        _objectTable = objectTable;
        _destination = destination;
        _arrivalRadius = arrivalRadius;
        _timeout = timeout ?? TimeSpan.FromSeconds(120);
        Name = $"Walk to: {displayName}";
    }

    public bool CanStart(out string? reason)
    {
        if (!_clientState.IsLoggedIn) { reason = "not logged in"; return false; }
        if (_objectTable.LocalPlayer is null) { reason = "local player not available"; return false; }
        if (!_vnav.IsReady()) { reason = "vnavmesh not ready (mesh still loading?)"; return false; }
        reason = null;
        return true;
    }

    public void Start()
    {
        if (_invoked) return;
        _invoked = true;
        var now = DateTime.UtcNow;
        _deadline = now + _timeout;
        _graceUntil = now + StartGrace;

        if (IsWithinArrivalRadius())
        {
            State = StepState.Completed;
            return;
        }

        if (!_vnav.PathfindAndMoveTo(_destination, fly: false))
        {
            FailureReason = "vnavmesh.PathfindAndMoveTo IPC failed";
            State = StepState.Failed;
            return;
        }
        State = StepState.Running;
    }

    public void Tick()
    {
        if (State != StepState.Running) return;
        var now = DateTime.UtcNow;

        if (now > _deadline)
        {
            _vnav.StopPath();
            FailureReason = $"did not arrive at {_destination} within {_timeout.TotalSeconds:0}s";
            State = StepState.Failed;
            return;
        }

        if (IsWithinArrivalRadius())
        {
            _vnav.StopPath();
            State = StepState.Completed;
            return;
        }

        var running = _vnav.IsPathRunning();
        if (running) _observedRunning = true;

        // Only treat "not running" as failure if:
        //  (a) we're past the grace window, AND
        //  (b) we observed running at least once (so the path was actually underway).
        // Before either condition, the path is just warming up — keep polling.
        if (!running && _observedRunning && now > _graceUntil)
        {
            FailureReason = "vnavmesh stopped before arrival — no path or blocked";
            State = StepState.Failed;
        }
    }

    public void Cancel()
    {
        _vnav.StopPath();
        if (State == StepState.Running) State = StepState.Cancelled;
    }

    private bool IsWithinArrivalRadius()
    {
        var me = _objectTable.LocalPlayer;
        if (me is null) return false;
        var delta = me.Position - _destination;
        return delta.LengthSquared() <= _arrivalRadius * _arrivalRadius;
    }
}
