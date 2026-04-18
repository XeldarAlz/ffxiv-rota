using System;
using Dalamud.Plugin.Services;
using Rota.Services.Ipc;

namespace Rota.Automation.Steps;

/// <summary>
/// Teleport to a world-map aetheryte via Lifestream IPC, then wait until the
/// territory change completes and the player is controllable again.
///
/// Step states:
///   - Running while Lifestream.IsBusy() returns true or we haven't arrived
///   - Completed once the expected territory is loaded and the player is idle
///   - Fails if Lifestream isn't reachable, or after a hard timeout
/// </summary>
public sealed class TeleportStep : IStep
{
    private readonly LifestreamIpc _lifestream;
    private readonly IClientState _clientState;
    private readonly ICondition _condition;
    private readonly uint _aetheryteId;
    private readonly uint _expectedTerritoryId;
    private readonly TimeSpan _timeout;

    private DateTime _deadline;
    private bool _invoked;

    public string Name { get; }
    public StepState State { get; private set; } = StepState.Idle;
    public string? FailureReason { get; private set; }

    public TeleportStep(
        LifestreamIpc lifestream,
        IClientState clientState,
        ICondition condition,
        uint aetheryteId,
        uint expectedTerritoryId,
        string displayName,
        TimeSpan? timeout = null)
    {
        _lifestream = lifestream;
        _clientState = clientState;
        _condition = condition;
        _aetheryteId = aetheryteId;
        _expectedTerritoryId = expectedTerritoryId;
        _timeout = timeout ?? TimeSpan.FromSeconds(60);
        Name = $"Teleport: {displayName}";
    }

    public bool CanStart(out string? reason)
    {
        if (!_clientState.IsLoggedIn) { reason = "not logged in"; return false; }
        if (_condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat])
        { reason = "in combat"; return false; }
        reason = null;
        return true;
    }

    public void Start()
    {
        if (_invoked) return;
        _invoked = true;
        _deadline = DateTime.UtcNow + _timeout;

        // Short-circuit: we are already in the target zone and idle.
        if (_clientState.TerritoryType == _expectedTerritoryId && !_lifestream.IsBusy())
        {
            State = StepState.Completed;
            return;
        }

        if (!_lifestream.Teleport(_aetheryteId))
        {
            FailureReason = "Lifestream.Teleport IPC failed";
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
        if (_lifestream.IsBusy()) return;
        if (_condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]) return;
        if (_condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51]) return;

        State = StepState.Completed;
    }

    public void Cancel()
    {
        if (State == StepState.Running) State = StepState.Cancelled;
    }
}
