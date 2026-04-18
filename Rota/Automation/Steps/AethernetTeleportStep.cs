using System;
using Dalamud.Plugin.Services;
using Rota.Services.Ipc;

namespace Rota.Automation.Steps;

/// <summary>
/// Aethernet-teleport to a named shard within the current zone via Lifestream.
/// Used for short hops inside Gold Saucer, city states, etc., where walking
/// from the main aetheryte would be slow and error-prone.
///
/// The step completes once Lifestream reports IsBusy() == false and we have
/// stopped moving (BetweenAreas51 clears). Fails on IPC error or timeout.
/// </summary>
public sealed class AethernetTeleportStep : IStep
{
    private readonly LifestreamIpc _lifestream;
    private readonly IClientState _clientState;
    private readonly ICondition _condition;
    private readonly string _placeName;
    private readonly TimeSpan _timeout;

    private DateTime _deadline;
    private DateTime _graceUntil;
    private bool _invoked;
    private bool _observedBusy;

    private static readonly TimeSpan StartGrace = TimeSpan.FromSeconds(2);

    public string Name { get; }
    public StepState State { get; private set; } = StepState.Idle;
    public string? FailureReason { get; private set; }

    public AethernetTeleportStep(
        LifestreamIpc lifestream,
        IClientState clientState,
        ICondition condition,
        string placeName,
        TimeSpan? timeout = null)
    {
        _lifestream = lifestream;
        _clientState = clientState;
        _condition = condition;
        _placeName = placeName;
        _timeout = timeout ?? TimeSpan.FromSeconds(30);
        Name = $"Aethernet: {placeName}";
    }

    public bool CanStart(out string? reason)
    {
        if (!_clientState.IsLoggedIn) { reason = "not logged in"; return false; }
        if (_condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat])
        { reason = "in combat"; return false; }
        if (_condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Casting])
        { reason = "already casting"; return false; }
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

        if (!_lifestream.AethernetTeleport(_placeName))
        {
            FailureReason = "Lifestream.AethernetTeleport IPC failed";
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
            FailureReason = $"aethernet hop to '{_placeName}' did not complete within {_timeout.TotalSeconds:0}s";
            State = StepState.Failed;
            return;
        }

        var busy = _lifestream.IsBusy();
        if (busy) _observedBusy = true;

        // Busy means Lifestream is still driving (queued teleport + move).
        // Only decide "done" once we are past grace, have seen busy at least
        // once, and are now idle.
        if (!busy && _observedBusy && now > _graceUntil && !IsTransitioning())
        {
            State = StepState.Completed;
        }
    }

    public void Cancel()
    {
        if (State == StepState.Running) State = StepState.Cancelled;
    }

    private bool IsTransitioning()
    {
        if (_condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas]) return true;
        if (_condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.BetweenAreas51]) return true;
        if (_condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.Casting]) return true;
        return false;
    }
}
