using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace Rota.Tracking.Readers;

/// <summary>
/// Beast tribe daily quests, aggregated.
///
/// QuestManager.DailyQuests is the fixed-size array of currently-accepted daily
/// (repeatable) quests. Cross-referencing each accepted quest's RowId against
/// Lumina's Quest sheet tells us which are tribal quests. We count those and
/// surface a simple "N accepted" summary.
///
/// Limitations: this counts quests CURRENTLY ACCEPTED, not daily allowances
/// used. If the player already turned in three today, those no longer occupy
/// DailyQuests slots — so a "0 accepted" state could mean either "haven't
/// started" or "all 12 allowances used and turned in". The Run workflow (once
/// wired) handles both cases correctly.
/// </summary>
public sealed class BeastTribesObjective : IObjective
{
    private readonly IClientState _clientState;
    private readonly IDataManager _dataManager;

    public string Id => "daily:beast-tribes";
    public string DisplayName => "Beast Tribes";
    public string Category => "Daily";
    public ObjectiveCadence Cadence => ObjectiveCadence.Daily;

    public IReadOnlyList<string> RequiredPlugins { get; } =
        new[] { "Lifestream", "Questionable" };

    public BeastTribesObjective(IClientState clientState, IDataManager dataManager)
    {
        _clientState = clientState;
        _dataManager = dataManager;
    }

    public unsafe ObjectiveStatus Evaluate()
    {
        if (!_clientState.IsLoggedIn) return new ObjectiveStatus(ObjectiveState.NotLoggedIn);

        var qm = QuestManager.Instance();
        if (qm is null) return new ObjectiveStatus(ObjectiveState.Unknown);

        var questSheet = _dataManager.GetExcelSheet<Quest>();

        int tribalAccepted = 0;
        var dailyQuests = qm->DailyQuests;
        for (int i = 0; i < dailyQuests.Length; i++)
        {
            var slot = dailyQuests[i];
            if (slot.QuestId == 0) continue;

            // DailyQuest slot stores a quest ID; looking it up and checking whether
            // its BeastTribe row is non-zero identifies it as a tribal quest.
            if (questSheet.TryGetRow(slot.QuestId, out var row) && row.BeastTribe.RowId > 0)
                tribalAccepted++;
        }

        if (tribalAccepted == 0)
            return new ObjectiveStatus(ObjectiveState.Pending,
                "no tribal quests accepted — pick up from a tribe NPC");

        return new ObjectiveStatus(ObjectiveState.InProgress,
            Detail: $"{tribalAccepted} tribal quest{(tribalAccepted == 1 ? "" : "s")} accepted",
            Current: tribalAccepted,
            Max: 12);
    }
}
