using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

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

        var ps = PlayerState.Instance();
        if (ps is null) return new ObjectiveStatus(ObjectiveState.Unknown);

        var buf = ps->ContentRouletteCompletion;
        if (_completionIndex >= buf.Length) return new ObjectiveStatus(ObjectiveState.Unknown);

        var done = buf[_completionIndex] != 0;
        return new ObjectiveStatus(done ? ObjectiveState.Completed : ObjectiveState.Pending);
    }
}
