using System;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Rota.Automation.Steps;

/// <summary>
/// Wait for a named addon to become visible, then fire an AtkUnitBase callback
/// with the supplied integer AtkValues. This is the generic primitive behind
/// "click a button programmatically" for most simple addons — each visible
/// widget in a native window routes through a numbered "case" in the addon's
/// ReceiveEvent, and firing the matching AtkValue sequence triggers that case
/// as if the user clicked the element.
///
/// Use for well-understood addons where the case -> action map is known.
/// For unknown addons, ship a prototype with a best-guess case value and
/// iterate based on live testing.
/// </summary>
public sealed class AddonCallbackStep : IStep
{
    private readonly IGameGui _gameGui;
    private readonly IPluginLog _log;
    private readonly string _addonName;
    private readonly int[] _intArgs;
    private readonly TimeSpan _waitForAddon;
    private readonly TimeSpan _settleDelay;

    private DateTime _deadline;
    private DateTime _firedAt;
    private bool _invoked;
    private bool _fired;

    public string Name { get; }
    public StepState State { get; private set; } = StepState.Idle;
    public string? FailureReason { get; private set; }

    public AddonCallbackStep(
        IGameGui gameGui,
        IPluginLog log,
        string addonName,
        int[] intArgs,
        string displayName,
        TimeSpan? waitForAddon = null,
        TimeSpan? settleDelay = null)
    {
        _gameGui = gameGui;
        _log = log;
        _addonName = addonName;
        _intArgs = intArgs;
        _waitForAddon = waitForAddon ?? TimeSpan.FromSeconds(20);
        _settleDelay = settleDelay ?? TimeSpan.FromMilliseconds(500);
        Name = $"Addon[{addonName}] callback({string.Join(",", intArgs)}): {displayName}";
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
            FailureReason = $"addon '{_addonName}' did not open within {_waitForAddon.TotalSeconds:0}s";
            State = StepState.Failed;
            return;
        }

        var addon = (AtkUnitBase*)_gameGui.GetAddonByName(_addonName).Address;
        if (addon is null) return;
        if (!addon->IsVisible) return;

        var values = stackalloc AtkValue[_intArgs.Length];
        for (int i = 0; i < _intArgs.Length; i++)
        {
            values[i].Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int;
            values[i].Int = _intArgs[i];
        }
        addon->FireCallback((uint)_intArgs.Length, values, true);

        _fired = true;
        _firedAt = now;
        _log.Debug("[Rota] {0} dispatched", Name);
    }

    public void Cancel()
    {
        if (State == StepState.Running) State = StepState.Cancelled;
    }
}
