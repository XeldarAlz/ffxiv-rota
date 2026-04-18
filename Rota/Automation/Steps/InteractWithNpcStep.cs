using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Rota.Services.Ipc;

namespace Rota.Automation.Steps;

/// <summary>
/// Find an NPC in the current ObjectTable, walk to it if necessary, and then
/// interact with it. Preferred over composing WalkToStep + InteractWithNpcStep
/// because the player's exact arrival coord from an aethernet hop drifts a
/// few yalms between runs — we never want to walk to a stale hard-coded Vec3
/// when the runtime position is right there in the ObjectTable.
///
/// Flow:
///   Start()
///     find NPC by BaseId (if provided) or by name within searchRadius
///     if already within interactDistance -> interact, Completed
///     else if vnav is provided -> pathfind to target, poll until close, interact
///     else -> Failed (no vnav, too far)
/// </summary>
public sealed class InteractWithNpcStep : IStep
{
    private readonly IClientState _clientState;
    private readonly IObjectTable _objectTable;
    private readonly VnavmeshIpc? _vnav;
    private readonly uint? _baseId;
    private readonly string? _nameContains;
    private readonly float _searchRadius;
    private readonly float _interactDistance;
    private readonly TimeSpan _timeout;

    private Dalamud.Game.ClientState.Objects.Types.IGameObject? _target;
    private DateTime _deadline;
    private DateTime _graceUntil;
    private bool _invoked;
    private bool _observedRunning;

    private static readonly TimeSpan StartGrace = TimeSpan.FromSeconds(4);

    public string Name { get; }
    public StepState State { get; private set; } = StepState.Idle;
    public string? FailureReason { get; private set; }

    public InteractWithNpcStep(
        IClientState clientState,
        IObjectTable objectTable,
        VnavmeshIpc? vnav,
        uint? baseId,
        string? nameContains,
        string displayName,
        float searchRadius = 30f,
        float interactDistance = 4.0f,
        TimeSpan? timeout = null)
    {
        _clientState = clientState;
        _objectTable = objectTable;
        _vnav = vnav;
        _baseId = baseId;
        _nameContains = nameContains;
        _searchRadius = searchRadius;
        _interactDistance = interactDistance;
        _timeout = timeout ?? TimeSpan.FromSeconds(60);
        Name = $"Interact with: {displayName}";
    }

    public bool CanStart(out string? reason)
    {
        if (!_clientState.IsLoggedIn) { reason = "not logged in"; return false; }
        if (_objectTable.LocalPlayer is null) { reason = "local player not available"; return false; }
        reason = null;
        return true;
    }

    public unsafe void Start()
    {
        if (_invoked) return;
        _invoked = true;
        var now = DateTime.UtcNow;
        _deadline = now + _timeout;
        _graceUntil = now + StartGrace;

        _target = FindNpc();
        if (_target is null)
        {
            FailureReason = $"target NPC not found within {_searchRadius:0} yalms";
            State = StepState.Failed;
            return;
        }

        if (IsWithinInteract())
        {
            DispatchInteract();
            return;
        }

        if (_vnav is null)
        {
            FailureReason = $"target is {Distance():0.0} yalms away and no vnav was supplied to close the gap";
            State = StepState.Failed;
            return;
        }

        if (!_vnav.PathfindAndMoveTo(_target.Position, fly: false))
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
            _vnav?.StopPath();
            FailureReason = "timed out approaching NPC";
            State = StepState.Failed;
            return;
        }

        // We re-resolve the target here because the ObjectTable occasionally
        // respawns the object when crossing invisible subzone boundaries; the
        // original reference becomes stale.
        _target ??= FindNpc();
        if (_target is null) return;

        if (IsWithinInteract())
        {
            _vnav?.StopPath();
            DispatchInteract();
            return;
        }

        // Standard "vnav stopped before arrival" diagnostic, same pattern as
        // WalkToStep (grace window + observedRunning latch).
        if (_vnav is null) return;
        var running = _vnav.IsPathRunning();
        if (running) _observedRunning = true;
        if (!running && _observedRunning && now > _graceUntil)
        {
            FailureReason = "vnavmesh stopped before reaching NPC — no path or blocked";
            State = StepState.Failed;
        }
    }

    public void Cancel()
    {
        _vnav?.StopPath();
        if (State == StepState.Running) State = StepState.Cancelled;
    }

    private unsafe void DispatchInteract()
    {
        var ts = TargetSystem.Instance();
        if (ts is null)
        {
            FailureReason = "TargetSystem.Instance() returned null";
            State = StepState.Failed;
            return;
        }
        var go = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)_target!.Address;
        ts->InteractWithObject(go);
        State = StepState.Completed;
    }

    private float Distance()
    {
        var me = _objectTable.LocalPlayer;
        if (me is null || _target is null) return float.MaxValue;
        return Vector3.Distance(me.Position, _target.Position);
    }

    private bool IsWithinInteract() =>
        Distance() <= _interactDistance;

    private Dalamud.Game.ClientState.Objects.Types.IGameObject? FindNpc()
    {
        var me = _objectTable.LocalPlayer;
        if (me is null) return null;

        if (_baseId is { } id)
        {
            var byId = _objectTable.FirstOrDefault(o =>
                o is not null
                && o.BaseId == id
                && (o.Position - me.Position).LengthSquared() <= _searchRadius * _searchRadius);
            if (byId is not null) return byId;
        }

        if (_nameContains is { Length: > 0 })
        {
            return _objectTable
                .Where(o => o.ObjectKind == ObjectKind.EventNpc)
                .Where(o => o.Name.TextValue.Contains(_nameContains!, StringComparison.OrdinalIgnoreCase))
                .Where(o => (o.Position - me.Position).LengthSquared() <= _searchRadius * _searchRadius)
                .OrderBy(o => (o.Position - me.Position).LengthSquared())
                .FirstOrDefault();
        }

        return null;
    }
}
