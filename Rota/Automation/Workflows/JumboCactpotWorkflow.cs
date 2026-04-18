using Rota.Automation.Steps;

namespace Rota.Automation.Workflows;

/// <summary>
/// Minimal end-to-end workflow for buying a Jumbo Cactpot ticket.
/// Proves the orchestrator loop: teleport → walk → interact.
///
/// The final ticket number selection is left to the player — the workflow
/// stops after opening Lewena's dialog, because picking numbers is the one
/// thing players genuinely want to do themselves (the whole point is the
/// gambling feeling of pressing the button). If we ever want full
/// automation we can chain a SelectYesNo / SelectString step here.
/// </summary>
public static class JumboCactpotWorkflow
{
    public static Workflow Build(WorkflowContext ctx) => new()
    {
        Name = "Jumbo Cactpot — buy ticket",
        Tags = ["weekly", "gold-saucer"],
        EstimatedMinutes = 1,
        Steps =
        [
            new TeleportStep(
                ctx.ClientState,
                ctx.Condition,
                aetheryteId: GoldSaucer.AetheryteId,
                expectedTerritoryId: GoldSaucer.TerritoryId,
                displayName: "Gold Saucer"),

            // No aethernet hop — the Jumbo Cactpot Broker is a short walk
            // (~20y) from the main Gold Saucer aetheryte, and the correct
            // aethernet shard name is not known yet. InteractWithNpcStep
            // searches a 60-yalm radius and vnav-pathfinds to the runtime
            // position, which comfortably covers the distance.
            new InteractWithNpcStep(
                ctx.ClientState,
                ctx.ObjectTable,
                ctx.Vnavmesh,
                baseId: null,
                nameContains: GoldSaucer.JumboCactpotBrokerName,
                displayName: GoldSaucer.JumboCactpotBrokerName,
                searchRadius: 60f),
        ],
    };
}
