using System.Collections.Generic;
using Dalamud.Plugin.Services;
using Rota.Automation;
using Rota.Automation.Workflows;

namespace Rota.Tracking.Readers;

/// <summary>
/// Jumbo Cactpot — weekly lottery, up to 3 tickets per week, drawn Saturday.
///
/// Passive state read is not currently available: CS does not expose a
/// weekly-ticket-count field, and scraping AgentLotteryWeekly only works while
/// the Cactpot UI is open. For now we register the objective as always-Pending
/// during the week and let the Run workflow handle actual ticket purchase.
///
/// When a sig or a CS accessor becomes available we can upgrade this to a
/// real per-week read. See task #11.
/// </summary>
public sealed class JumboCactpotObjective : IObjective
{
    private readonly IClientState _clientState;

    public string Id => "weekly:jumbo-cactpot";
    public string DisplayName => "Jumbo Cactpot";
    public string Category => "Weekly";
    public ObjectiveCadence Cadence => ObjectiveCadence.Weekly;

    // Intra-world aetheryte teleport uses the game's native Telepo (no IPC),
    // so the only orchestrator plugin this workflow needs is vnavmesh to walk
    // from the Gold Saucer entrance to Lewena.
    public IReadOnlyList<string> RequiredPlugins { get; } = new[] { "vnavmesh" };

    public JumboCactpotObjective(IClientState clientState)
    {
        _clientState = clientState;
    }

    public ObjectiveStatus Evaluate()
    {
        if (!_clientState.IsLoggedIn) return new ObjectiveStatus(ObjectiveState.NotLoggedIn);

        // No passive read available yet — always treat as pending so the Run
        // button stays actionable. See Tracking/Readers/JumboCactpotObjective.cs
        // for notes.
        return new ObjectiveStatus(ObjectiveState.Pending,
            "passive read unavailable — open Cactpot board to confirm 0/3 tickets");
    }

    public Workflow BuildWorkflow(WorkflowContext ctx) => JumboCactpotWorkflow.Build(ctx);
}
