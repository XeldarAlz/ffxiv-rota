using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Rota.Automation;

namespace Rota.Tracking.Readers;

/// <summary>
/// Wondrous Tails — weekly journal from Khloe Aliapoh in Idyllshire.
/// Nine stickers per book maximum. The book lives for a week (calendar-weekly reset)
/// and can be turned in early for second-chance points.
///
/// States tracked:
///   - No journal at all          -> Pending (go pick one up)
///   - Journal held but expired   -> Pending (turn in + grab a new one)
///   - Held, &lt; 9 stickers placed -> InProgress (current / 9)
///   - Held, 9 stickers placed    -> Completed
/// </summary>
public sealed class WondrousTailsObjective : IObjective
{
    private readonly IClientState _clientState;

    public string Id => "weekly:wondrous-tails";
    public string DisplayName => "Wondrous Tails";
    public string Category => "Weekly";
    public ObjectiveCadence Cadence => ObjectiveCadence.Weekly;

    // Full automation requires a teleport to Idyllshire (Lifestream) and the ability
    // to actually clear duties (AutoDuty + RotationSolver/WrathCombo downstream).
    // For now we list only the teleport + duty-runner deps; rotation is assumed.
    public IReadOnlyList<string> RequiredPlugins { get; } = new[] { "Lifestream", "AutoDuty" };

    public WondrousTailsObjective(IClientState clientState)
    {
        _clientState = clientState;
    }

    public unsafe ObjectiveStatus Evaluate()
    {
        if (!_clientState.IsLoggedIn) return new ObjectiveStatus(ObjectiveState.NotLoggedIn);

        var ps = PlayerState.Instance();
        if (ps is null) return new ObjectiveStatus(ObjectiveState.Unknown);

        if (!ps->HasWeeklyBingoJournal)
            return new ObjectiveStatus(ObjectiveState.Pending,
                "no journal — pick one up from Khloe in Idyllshire");

        if (ps->IsWeeklyBingoExpired())
            return new ObjectiveStatus(ObjectiveState.Pending,
                "journal expired — turn in + grab a new one");

        var placed = ps->WeeklyBingoNumPlacedStickers;
        const int max = 9;

        if (placed >= max)
            return new ObjectiveStatus(ObjectiveState.Completed,
                $"{ps->WeeklyBingoNumSecondChancePoints} second-chance pts");

        return new ObjectiveStatus(ObjectiveState.InProgress,
            Detail: $"{max - placed} to go",
            Current: placed,
            Max: max);
    }

    public Workflow? BuildWorkflow(WorkflowContext ctx) => null;
}
