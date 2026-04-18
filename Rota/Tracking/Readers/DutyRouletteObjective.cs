using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Rota.Automation;

namespace Rota.Tracking.Readers;

/// <summary>
/// One tracked daily roulette (Leveling, 50/60/70/80/90/100, Trials, Alliance Raids, etc.).
/// Completion is read from PlayerState.ContentRouletteCompletion, which is a byte array
/// indexed by ContentRoulette.CompletionArrayIndex. A non-zero byte at that index means
/// the daily bonus has been claimed for this reset.
/// </summary>
public sealed class DutyRouletteObjective : IObjective
{
    private readonly IClientState _clientState;
    private readonly byte _completionIndex;
    private readonly string _name;

    public string Id { get; }
    public string DisplayName => _name;
    public string Category => "Roulette";
    public ObjectiveCadence Cadence => ObjectiveCadence.Daily;

    public IReadOnlyList<string> RequiredPlugins { get; } = new[] { "AutoDuty" };

    public DutyRouletteObjective(uint rouletteRowId, byte completionIndex, string name, IClientState clientState)
    {
        Id = $"roulette:{rouletteRowId}";
        _completionIndex = completionIndex;
        _name = name;
        _clientState = clientState;
    }

    public unsafe ObjectiveStatus Evaluate()
    {
        if (!_clientState.IsLoggedIn) return new ObjectiveStatus(ObjectiveState.NotLoggedIn);

        // Per CS's own docstring on PlayerState._contentRouletteCompletion:
        // "Use InstanceContent.IsRouletteComplete(byte)." That method reads the
        // underlying memory directly, bypassing the fixed-size public accessor
        // (which lags behind when new roulettes are added).
        var ic = FFXIVClientStructs.FFXIV.Client.Game.UI.InstanceContent.Instance();
        if (ic is null) return new ObjectiveStatus(ObjectiveState.Unknown);

        var done = ic->IsRouletteComplete(_completionIndex);
        return new ObjectiveStatus(done ? ObjectiveState.Completed : ObjectiveState.Pending);
    }

    public Workflow? BuildWorkflow(WorkflowContext ctx) => null;
}
