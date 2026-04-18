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
    private bool _invoked;

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
        _deadline = DateTime.UtcNow + _timeout;

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

        if (DateTime.UtcNow > _deadline)
        {
            _vnav.Stop();
            FailureReason = $"did not arrive at {_destination} within {_timeout.TotalSeconds:0}s";
            State = StepState.Failed;
            return;
        }

        if (IsWithinArrivalRadius())
        {
            _vnav.Stop();
            State = StepState.Completed;
            return;
        }

        // If vnav reports it has stopped but we haven't arrived, it couldn't path.
        if (!_vnav.IsRunning() && !IsWithinArrivalRadius())
        {
            FailureReason = "vnavmesh stopped before arrival — no path or blocked";
            State = StepState.Failed;
        }
    }

    public void Cancel()
    {
        _vnav.Stop();
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
