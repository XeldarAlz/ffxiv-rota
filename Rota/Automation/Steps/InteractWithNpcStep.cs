using System;
using System.Linq;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace Rota.Automation.Steps;

/// <summary>
/// Find an NPC in the current ObjectTable and interact with it, triggering the
/// in-game conversation or vendor window as if the player pressed the interact
/// key. Identifies the NPC by DataId when provided, falling back to the nearest
/// EventNpc whose name contains <see cref="_nameContains"/> (case-insensitive)
/// within <see cref="_searchRadius"/> yalms.
///
/// This step does NOT move the player — combine with WalkToStep first so the
/// NPC is within interact range (~5 yalms).
/// </summary>
public sealed class InteractWithNpcStep : IStep
{
    private readonly IClientState _clientState;
    private readonly IObjectTable _objectTable;
    private readonly uint? _dataId;
    private readonly string? _nameContains;
    private readonly float _searchRadius;
    private readonly TimeSpan _timeout;

    private DateTime _deadline;
    private bool _invoked;
    private bool _invokedInteract;

    public string Name { get; }
    public StepState State { get; private set; } = StepState.Idle;
    public string? FailureReason { get; private set; }

    public InteractWithNpcStep(
        IClientState clientState,
        IObjectTable objectTable,
        uint? dataId,
        string? nameContains,
        string displayName,
        float searchRadius = 6f,
        TimeSpan? timeout = null)
    {
        _clientState = clientState;
        _objectTable = objectTable;
        _dataId = dataId;
        _nameContains = nameContains;
        _searchRadius = searchRadius;
        _timeout = timeout ?? TimeSpan.FromSeconds(10);
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
        _deadline = DateTime.UtcNow + _timeout;

        var target = FindNpc();
        if (target is null)
        {
            FailureReason = "target NPC not found within range";
            State = StepState.Failed;
            return;
        }

        var ts = TargetSystem.Instance();
        if (ts is null)
        {
            FailureReason = "TargetSystem.Instance() returned null";
            State = StepState.Failed;
            return;
        }

        var go = (FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject*)target.Address;
        ts->InteractWithObject(go);
        _invokedInteract = true;
        State = StepState.Running;
    }

    public void Tick()
    {
        if (State != StepState.Running) return;

        // We consider the step complete as soon as the interact call has been
        // made — the game drives the dialog state after that. If the player
        // wants post-dialog handling, chain another step.
        if (_invokedInteract)
        {
            State = StepState.Completed;
            return;
        }

        if (DateTime.UtcNow > _deadline)
        {
            FailureReason = "interact never dispatched";
            State = StepState.Failed;
        }
    }

    public void Cancel()
    {
        if (State == StepState.Running) State = StepState.Cancelled;
    }

    private Dalamud.Game.ClientState.Objects.Types.IGameObject? FindNpc()
    {
        var me = _objectTable.LocalPlayer;
        if (me is null) return null;

        // Prefer exact BaseId (formerly DataId) match.
        if (_dataId is { } id)
        {
            var byId = _objectTable.FirstOrDefault(o =>
                o is not null
                && o.BaseId == id
                && (o.Position - me.Position).LengthSquared() <= _searchRadius * _searchRadius);
            if (byId is not null) return byId;
        }

        // Fallback: nearest EventNpc whose name matches.
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
