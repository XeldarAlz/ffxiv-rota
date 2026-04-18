using Dalamud.Plugin.Services;
using Rota.Services.Ipc;

namespace Rota.Automation;

/// <summary>
/// Bundle of services a workflow / step may need. Built once in the plugin
/// ctor and passed to every objective when it constructs its workflow, so
/// workflows never reach into Plugin.cs statics.
/// </summary>
public sealed class WorkflowContext
{
    public required IClientState ClientState { get; init; }
    public required ICondition Condition { get; init; }
    public required IObjectTable ObjectTable { get; init; }
    public required IGameGui GameGui { get; init; }
    public required IPluginLog Log { get; init; }

    public required LifestreamIpc Lifestream { get; init; }
    public required VnavmeshIpc Vnavmesh { get; init; }
}
