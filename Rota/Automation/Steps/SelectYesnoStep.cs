using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Rota.Automation.Steps;

/// <summary>
/// Wait for the SelectYesno dialog to appear, then click Yes (0) or No (1).
/// Used anywhere a "Purchase for N gil?" / "Are you sure?" prompt follows an
/// action we dispatched earlier in the workflow.
/// </summary>
public sealed class SelectYesnoStep : IStep
{
    private readonly IGameGui _gameGui;
    private readonly IPluginLog _log;
    private readonly bool _clickYes;
    private readonly TimeSpan _waitForAddon;
    private readonly TimeSpan _settleDelay;

    private DateTime _deadline;
    private DateTime _firedAt;
    private bool _invoked;
    private bool _fired;

    public string Name { get; }
    public StepState State { get; private set; } = StepState.Idle;
    public string? FailureReason { get; private set; }

    public SelectYesnoStep(
        IGameGui gameGui,
        IPluginLog log,
        bool clickYes = true,
        TimeSpan? waitForAddon = null,
        TimeSpan? settleDelay = null)
    {
        _gameGui = gameGui;
        _log = log;
        _clickYes = clickYes;
        _waitForAddon = waitForAddon ?? TimeSpan.FromSeconds(10);
        _settleDelay = settleDelay ?? TimeSpan.FromMilliseconds(500);
        Name = $"SelectYesno: {(clickYes ? "Yes" : "No")}";
    }

    public bool CanStart(out string? reason) { reason = null; return true; }

    public void Start()
    {
        if (_invoked) return;
        _invoked = true;
        _deadline = DateTime.UtcNow + _waitForAddon;
        State = StepState.Running;
    }

    public unsafe void Tick()
    {
        if (State != StepState.Running) return;
        var now = DateTime.UtcNow;

        if (_fired)
        {
            if (now - _firedAt >= _settleDelay)
                State = StepState.Completed;
            return;
        }

        if (now > _deadline)
        {
            FailureReason = $"SelectYesno did not open within {_waitForAddon.TotalSeconds:0}s";
            State = StepState.Failed;
            return;
        }

        var addon = (AtkUnitBase*)_gameGui.GetAddonByName("SelectYesno").Address;
        if (addon is null) return;
        if (!addon->IsVisible) return;

        // SelectYesno callback convention: single int arg. 0 = Yes, 1 = No.
        var value = new AtkValue
        {
            Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int,
            Int = _clickYes ? 0 : 1,
        };
        addon->FireCallback(1, &value, true);

        _fired = true;
        _firedAt = now;
        _log.Debug("[Rota] SelectYesno dispatched: {0}", _clickYes ? "Yes" : "No");
    }

    public void Cancel()
    {
        if (State == StepState.Running) State = StepState.Cancelled;
    }
}
