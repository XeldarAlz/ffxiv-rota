using System.Collections.Generic;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Rota.Automation;

namespace Rota.Tracking.Readers;

/// <summary>
/// PvP Series (Malmstones) progression. Unlike the other weekly items this is
/// not a per-reset chore — it accrues over the whole series (~12 weeks). We
/// surface it here so players can see current rank vs claimed-rank at a glance
/// (a gap between them means there are unclaimed Series rewards).
///
/// Status semantics:
///   - Current == Claimed                        -> Completed (all rewards claimed)
///   - Claimed &lt; Current                        -> Pending   (rewards to claim)
///   - Series not started (current rank == 0)    -> Unavailable
/// </summary>
public sealed class PvPSeriesObjective : IObjective
{
    private readonly IClientState _clientState;

    public string Id => "weekly:pvp-series";
    public string DisplayName => "PvP Series";
    public string Category => "Weekly";
    public ObjectiveCadence Cadence => ObjectiveCadence.Weekly;

    // No plugin orchestration for the progression itself — claiming rewards is a
    // manual UI interaction in the PvP profile window. We intentionally leave
    // RequiredPlugins empty so no Run button shows.
    public IReadOnlyList<string> RequiredPlugins { get; } = System.Array.Empty<string>();

    public PvPSeriesObjective(IClientState clientState)
    {
        _clientState = clientState;
    }

    public unsafe ObjectiveStatus Evaluate()
    {
        if (!_clientState.IsLoggedIn) return new ObjectiveStatus(ObjectiveState.NotLoggedIn);

        var agent = AgentPvpProfile.Instance();
        if (agent is null) return new ObjectiveStatus(ObjectiveState.Unknown);

        var profile = PvPProfile.Instance();
        if (profile is null) return new ObjectiveStatus(ObjectiveState.Unknown);

        var currentRank = profile->GetSeriesCurrentRank();
        var claimedRank = profile->GetSeriesClaimedRank();
        var exp = profile->GetSeriesExperience();

        if (currentRank == 0)
            return new ObjectiveStatus(ObjectiveState.Unavailable, "series not yet started");

        if (claimedRank < currentRank)
            return new ObjectiveStatus(ObjectiveState.Pending,
                Detail: $"{currentRank - claimedRank} rank reward(s) unclaimed — exp {exp}");

        return new ObjectiveStatus(ObjectiveState.Completed,
            Detail: $"rank {currentRank}, exp {exp}");
    }

    public Workflow? BuildWorkflow(WorkflowContext ctx) => null;
}
