using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace Rota.Automation.Steps;

/// <summary>
/// Teleport to a world-map aetheryte using the game's native Telepo struct —
/// the exact same code path as pressing "Teleport" in the Aetheryte menu.
///
/// Does NOT use Lifestream because Lifestream's public IPC only covers aethernet
/// shards and cross-world visits, not simple intra-world aetheryte teleports.
/// For cross-DC work we will layer Lifestream on top in a separate step.
///
/// Flow:
///   Start() -> Telepo.Teleport(aetheryteId, subIndex)
///   Tick() polls until:
///     territory == expected && not Casting && not BetweenAreas -> Completed
///     deadline exceeded                                        -> Failed
/// </summary>
public sealed class TeleportStep : IStep
{
    private readonly IClientState _clientState;
    private readonly ICondition _condition;
    private readonly uint _aetheryteId;
    private readonly byte _subIndex;
    private readonly uint _expectedTerritoryId;
    private readonly TimeSpan _timeout;

    private DateTime _deadline;
    private bool _invoked;

    public string Name { get; }
    public StepState State { get; private set; } = StepState.Idle;
    public string? FailureReason { get; private set; }

    public TeleportStep(
        IClientState clientState,
        ICondition condition,
        uint aetheryteId,
        uint expectedTerritoryId,
        string displayName,
        byte subIndex = 0,
        TimeSpan? timeout = null)
    {
        _clientState = clientState;
        _condition = condition;
        _aetheryteId = aetheryteId;
        _subIndex = subIndex;
        _expectedTerritoryId = expectedTerritoryId;
        _timeout = timeout ?? TimeSpan.FromSeconds(60);
        Name = $"Teleport: {displayName}";
    }

    public bool CanStart(out string? reason)
    {
        if (!_clientState.IsLoggedIn) { reason = "not logged in"; return false; }
        if (_condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat])
        { reason = "in combat"; return false; }
        if (_condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Casting])
        { reason = "already casting"; return false; }
        if (_condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BoundByDuty])
        { reason = "bound by duty"; return false; }
        reason = null;
        return true;
    }

    public unsafe void Start()
    {
        if (_invoked) return;
        _invoked = true;
        _deadline = DateTime.UtcNow + _timeout;

        // Short-circuit: already at target zone and idle.
        if (_clientState.TerritoryType == _expectedTerritoryId && IsIdle())
        {
            State = StepState.Completed;
            return;
        }

        var telepo = Telepo.Instance();
        if (telepo is null)
        {
            FailureReason = "Telepo.Instance() returned null";
            State = StepState.Failed;
            return;
        }

        var ok = telepo->Teleport(_aetheryteId, _subIndex);
        if (!ok)
        {
            FailureReason = $"Telepo.Teleport({_aetheryteId}, {_subIndex}) returned false — aetheryte unlocked? enough gil?";
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
            FailureReason = $"did not arrive at territory {_expectedTerritoryId} within {_timeout.TotalSeconds:0}s";
            State = StepState.Failed;
            return;
        }

        if (_clientState.TerritoryType != _expectedTerritoryId) return;
        if (!IsIdle()) return;

        State = StepState.Completed;
    }

    public void Cancel()
    {
        if (State == StepState.Running) State = StepState.Cancelled;
    }

    private bool IsIdle()
    {
        if (_condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Casting]) return false;
        if (_condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]) return false;
        if (_condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51]) return false;
        return true;
    }
}
