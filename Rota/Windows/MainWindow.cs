using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using Rota.Services;
using Rota.Tracking;

namespace Rota.Windows;

public sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin _plugin;

    public MainWindow(Plugin plugin)
        : base("Rota###rota-main")
    {
        _plugin = plugin;
        Size = new Vector2(520, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void Draw()
    {
        DrawRunnerBar();
        ImGui.Separator();

        if (ImGui.BeginTabBar("rota-tabs"))
        {
            if (ImGui.BeginTabItem("Dailies")) { DrawObjectives(ObjectiveCadence.Daily); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Weeklies")) { DrawObjectives(ObjectiveCadence.Weekly); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Dependencies")) { DrawDependencies(); ImGui.EndTabItem(); }
            if (ImGui.BeginTabItem("Settings")) { DrawSettings(); ImGui.EndTabItem(); }
            ImGui.EndTabBar();
        }
    }

    private void DrawRunnerBar()
    {
        var runner = _plugin.Runner;
        ImGui.TextUnformatted($"Runner: {runner.State}");
        if (runner.CurrentStepName is { } s)
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"— {s}");
        }
        if (runner.LastError is { } err)
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), err);
        }
        if (ImGui.Button("Cancel All")) runner.Cancel();
        ImGui.SameLine();
        if (ImGui.Button("Refresh Deps")) _plugin.Ipc.Refresh();
    }

    private void DrawObjectives(ObjectiveCadence cadence)
    {
        var list = _plugin.Objectives.ByCadence(cadence);
        var any = false;
        foreach (var obj in list)
        {
            any = true;
            DrawObjectiveRow(obj);
        }
        if (!any)
        {
            ImGui.TextDisabled("No objectives registered yet.");
            ImGui.TextDisabled("Readers are added per-commit; see Tracking/Readers/.");
        }
    }

    private void DrawObjectiveRow(IObjective obj)
    {
        var status = obj.Evaluate();

        ImGui.PushID(obj.Id);
        ImGui.BeginGroup();

        ImGui.TextUnformatted($"[{obj.Category}] {obj.DisplayName}");
        ImGui.SameLine();

        var (label, color) = status.State switch
        {
            ObjectiveState.Completed   => ("✓ done",         new Vector4(0.3f, 0.9f, 0.3f, 1f)),
            ObjectiveState.Pending     => ("● pending",       new Vector4(1.0f, 0.8f, 0.2f, 1f)),
            ObjectiveState.InProgress  => ($"● {status.Current}/{status.Max}", new Vector4(1.0f, 0.8f, 0.2f, 1f)),
            ObjectiveState.Unavailable => ("— unavailable",   new Vector4(0.6f, 0.6f, 0.6f, 1f)),
            ObjectiveState.Blocked     => ("⚠ blocked",       new Vector4(1.0f, 0.5f, 0.3f, 1f)),
            ObjectiveState.NotLoggedIn => ("— offline",       new Vector4(0.6f, 0.6f, 0.6f, 1f)),
            _                          => ("? unknown",       new Vector4(0.6f, 0.6f, 0.6f, 1f)),
        };
        ImGui.TextColored(color, label);
        if (status.Detail is { Length: > 0 })
        {
            ImGui.SameLine();
            ImGui.TextDisabled($"— {status.Detail}");
        }

        var canRun = obj.RequiredPlugins.Count > 0
                     && status.State is ObjectiveState.Pending or ObjectiveState.InProgress
                     && _plugin.Runner.State != Automation.RunnerState.Running;

        if (obj.RequiredPlugins.Count > 0)
        {
            var missing = new System.Collections.Generic.List<string>();
            foreach (var key in obj.RequiredPlugins)
                if (!_plugin.Ipc.IsAvailable(key)) missing.Add(key);

            ImGui.SameLine();
            if (missing.Count > 0)
            {
                ImGui.BeginDisabled();
                ImGui.Button("Run");
                ImGui.EndDisabled();
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip($"Missing: {string.Join(", ", missing)}");
            }
            else
            {
                var wf = obj.BuildWorkflow(_plugin.Workflows);
                var runnable = canRun && wf is not null;
                if (!runnable) ImGui.BeginDisabled();
                if (ImGui.Button("Run") && wf is not null)
                {
                    if (!_plugin.Runner.TryStart(wf, out var reason))
                        Plugin.Log.Warning("[Rota] Could not start workflow '{0}': {1}", wf.Name, reason ?? "?");
                }
                if (!runnable) ImGui.EndDisabled();
                if (wf is null && ImGui.IsItemHovered())
                    ImGui.SetTooltip("No workflow wired for this objective yet.");
            }
        }

        ImGui.EndGroup();
        ImGui.PopID();
        ImGui.Separator();
    }

    private void DrawDependencies()
    {
        ImGui.TextWrapped("Rota orchestrates other installed plugins over IPC. Dependencies are re-probed when plugins load/unload.");
        ImGui.Spacing();
        foreach (var probe in IpcRegistry.KnownPlugins)
        {
            var ok = _plugin.Ipc.IsAvailable(probe.Key);
            var color = ok ? new Vector4(0.3f, 0.9f, 0.3f, 1f) : new Vector4(0.8f, 0.4f, 0.4f, 1f);
            ImGui.TextColored(color, ok ? "●" : "○");
            ImGui.SameLine();
            ImGui.TextUnformatted(probe.DisplayName);
            ImGui.SameLine();
            ImGui.TextDisabled($"  ({probe.InstallHint})");
        }
    }

    private void DrawSettings()
    {
        var cfg = _plugin.Configuration;

        var dry = cfg.DryRun;
        if (ImGui.Checkbox("Dry run (IPC calls are no-ops)", ref dry)) { cfg.DryRun = dry; cfg.Save(); }

        var confirm = cfg.RequireConfirmationBeforeRun;
        if (ImGui.Checkbox("Confirm before running any workflow", ref confirm)) { cfg.RequireConfirmationBeforeRun = confirm; cfg.Save(); }

        var minutes = cfg.MaxSessionMinutes;
        if (ImGui.SliderInt("Session cap (minutes)", ref minutes, 15, 480)) { cfg.MaxSessionMinutes = minutes; cfg.Save(); }

        ImGui.Spacing();
        ImGui.TextDisabled("Global panic key: Ctrl+Shift+X (TODO).");
    }
}
