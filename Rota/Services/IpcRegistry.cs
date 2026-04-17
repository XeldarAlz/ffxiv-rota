using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Rota.Services;

/// <summary>
/// Discovers which orchestrator plugins are installed + loaded.
/// Detection is name-based via <see cref="IDalamudPluginInterface.InstalledPlugins"/>,
/// not IPC-probed — each plugin publishes a different set of endpoints and we do not want
/// detection to depend on knowing those names.
///
/// Actually CALLING another plugin's IPC happens in dedicated wrappers (see Services/Ipc/*).
/// Those wrappers should first check <see cref="IsAvailable"/>.
/// </summary>
public sealed class IpcRegistry : IDisposable
{
    public sealed record Probe(
        string Key,
        string DisplayName,
        string InternalName,
        string InstallHint);

    public static readonly IReadOnlyList<Probe> KnownPlugins = new Probe[]
    {
        new("Lifestream",     "Lifestream",      "Lifestream",     "NightmareXIV custom repo"),
        new("vnavmesh",       "vnavmesh",        "vnavmesh",       "awgil custom repo"),
        new("AutoDuty",       "AutoDuty",        "AutoDuty",       "erdelf custom repo"),
        new("Questionable",   "Questionable",    "Questionable",   "Questionable custom repo"),
        new("AutoRetainer",   "AutoRetainer",    "AutoRetainer",   "PunishXIV custom repo"),
        new("RotationSolver", "RotationSolver",  "RotationSolver", "FFXIV-CombatReborn custom repo"),
        new("BossModReborn",  "BossModReborn",   "BossModReborn",  "FFXIV-CombatReborn custom repo"),
        new("PandorasBox",    "PandorasBox",     "PandorasBox",    "PunishXIV custom repo"),
        new("TextAdvance",    "TextAdvance",     "TextAdvance",    "NightmareXIV custom repo"),
        new("YesAlready",     "YesAlready",      "YesAlready",     "PunishXIV custom repo"),
    };

    private readonly IDalamudPluginInterface _pi;
    private readonly IPluginLog _log;
    private readonly Dictionary<string, bool> _available = new();

    public IpcRegistry(IDalamudPluginInterface pi, IPluginLog log)
    {
        _pi = pi;
        _log = log;
        _pi.ActivePluginsChanged += OnActivePluginsChanged;
        Refresh();
    }

    public bool IsAvailable(string key) => _available.TryGetValue(key, out var v) && v;

    public IReadOnlyDictionary<string, bool> Snapshot() => _available;

    public void Refresh()
    {
        var loaded = _pi.InstalledPlugins
            .Where(p => p.IsLoaded)
            .Select(p => p.InternalName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        _available.Clear();
        foreach (var probe in KnownPlugins)
            _available[probe.Key] = loaded.Contains(probe.InternalName);

        var green = string.Join(", ", _available.Where(kv => kv.Value).Select(kv => kv.Key));
        _log.Debug("[Rota] IPC registry refreshed; available: {0}", string.IsNullOrEmpty(green) ? "<none>" : green);
    }

    private void OnActivePluginsChanged(IActivePluginsChangedEventArgs args) => Refresh();

    public void Dispose()
    {
        _pi.ActivePluginsChanged -= OnActivePluginsChanged;
    }
}
