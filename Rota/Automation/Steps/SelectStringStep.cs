using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Rota.Automation.Steps;

/// <summary>
/// Wait for a SelectString (dialog-list) addon to open, then fire its callback
/// to select the option at <paramref name="optionIndex"/>. Completes when the
/// callback is dispatched; does NOT wait for whatever window opens next (chain
/// another step for that).
///
/// Works against any addon that follows SelectString's conventional callback
/// shape (single-int AtkValue at index 0 = selected option). The common
/// instances are "SelectString" and "CutSceneSelectString".
/// </summary>
public sealed class SelectStringStep : IStep
{
    private readonly IGameGui _gameGui;
    private readonly IPluginLog _log;
    private readonly string _addonName;
    private readonly int _optionIndex;
    private readonly TimeSpan _waitForAddon;
    private readonly TimeSpan _settleDelay;

    private DateTime _deadline;
    private DateTime _firedAt;
    private bool _invoked;
    private bool _fired;

    public string Name { get; }
    public StepState State { get; private set; } = StepState.Idle;
    public string? FailureReason { get; private set; }

    public SelectStringStep(
        IGameGui gameGui,
        IPluginLog log,
        int optionIndex,
        string displayName,
        string addonName = "SelectString",
        TimeSpan? waitForAddon = null,
        TimeSpan? settleDelay = null)
    {
        _gameGui = gameGui;
        _log = log;
        _addonName = addonName;
        _optionIndex = optionIndex;
        _waitForAddon = waitForAddon ?? TimeSpan.FromSeconds(20);
        _settleDelay = settleDelay ?? TimeSpan.FromMilliseconds(250);
        Name = $"SelectString[{optionIndex}]: {displayName}";
    }

    public bool CanStart(out string? reason)
    {
        reason = null;
        return true;
    }

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
            // Short settle delay so we don't race-step to whatever opens next
            // before the game has finished routing the callback.
            if (now - _firedAt >= _settleDelay)
                State = StepState.Completed;
            return;
        }

        if (now > _deadline)
        {
            FailureReason = $"addon '{_addonName}' did not open within {_waitForAddon.TotalSeconds:0}s";
            State = StepState.Failed;
            return;
        }

        var addonWrapper = _gameGui.GetAddonByName(_addonName);
        var addon = (AtkUnitBase*)addonWrapper.Address;
        if (addon is null) return;
        if (!addon->IsVisible) return;

        var value = new AtkValue
        {
            Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int,
            Int = _optionIndex,
        };
        addon->FireCallback(1, &value, true);

        _fired = true;
        _firedAt = now;
        _log.Debug("[Rota] SelectString[{0}] dispatched on addon '{1}'", _optionIndex, _addonName);
    }

    public void Cancel()
    {
        if (State == StepState.Running) State = StepState.Cancelled;
    }
}
