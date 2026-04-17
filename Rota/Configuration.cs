using System;
using Dalamud.Configuration;

namespace Rota;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool ShowDailies { get; set; } = true;
    public bool ShowWeeklies { get; set; } = true;
    public bool DryRun { get; set; } = false;

    public bool RequireConfirmationBeforeRun { get; set; } = true;
    public int MaxSessionMinutes { get; set; } = 180;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);
}
