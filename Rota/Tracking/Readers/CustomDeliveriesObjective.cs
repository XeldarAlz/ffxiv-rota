using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;

namespace Rota.Tracking.Readers;

/// <summary>
/// Custom Deliveries — weekly allowances across all unlocked Satisfaction NPCs
/// (Zhloe, M'naago, Kurenai, Adkiragh, Ehll Tou, Charlemend, Ameliance, Anden,
/// Margrat, Nitowikwe, and any future additions). Each unlocked NPC offers 6
/// deliveries per week; the weekly cap scales with how many you've unlocked.
///
/// Aggregated into one objective for UI density — per-NPC rows can be added
/// later if a user wants them.
/// </summary>
public sealed class CustomDeliveriesObjective : IObjective
{
    private readonly IClientState _clientState;

    public string Id => "weekly:custom-deliveries";
    public string DisplayName => "Custom Deliveries";
    public string Category => "Weekly";
    public ObjectiveCadence Cadence => ObjectiveCadence.Weekly;

    // Teleport only — no duty/combat involvement; deliveries are handed in at NPCs.
    // The actual hand-in requires the player to have HQ deliverables in inventory,
    // which we surface in the detail when relevant.
    public IReadOnlyList<string> RequiredPlugins { get; } = new[] { "Lifestream" };

    private const int AllowancesPerNpc = 6;

    public CustomDeliveriesObjective(IClientState clientState)
    {
        _clientState = clientState;
    }

    public unsafe ObjectiveStatus Evaluate()
    {
        if (!_clientState.IsLoggedIn) return new ObjectiveStatus(ObjectiveState.NotLoggedIn);

        var mgr = SatisfactionSupplyManager.Instance();
        if (mgr is null) return new ObjectiveStatus(ObjectiveState.Unknown);

        var ranks = mgr->SatisfactionRanks;
        var allowances = mgr->UsedAllowances;

        int unlocked = 0;
        int used = 0;
        int cap = System.Math.Min(ranks.Length, allowances.Length);
        for (int i = 0; i < cap; i++)
        {
            if (ranks[i] == 0) continue;
            unlocked++;
            used += allowances[i];
        }

        if (unlocked == 0)
            return new ObjectiveStatus(ObjectiveState.Unavailable, "no Satisfaction NPCs unlocked");

        int max = unlocked * AllowancesPerNpc;
        if (used >= max) return new ObjectiveStatus(ObjectiveState.Completed);

        return new ObjectiveStatus(
            ObjectiveState.InProgress,
            Detail: $"across {unlocked} NPC{(unlocked == 1 ? "" : "s")}",
            Current: used,
            Max: max);
    }
}
