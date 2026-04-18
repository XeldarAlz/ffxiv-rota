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

            new AethernetTeleportStep(
                ctx.Lifestream,
                ctx.ClientState,
                ctx.Condition,
                placeName: GoldSaucer.CactpotBoardAethernet),

            // Composite: find Lewena by name in the ObjectTable, vnav to her
            // runtime position, then interact. Replaces the old static walk +
            // fixed-coord interact which was fragile to aethernet drop drift.
            new InteractWithNpcStep(
                ctx.ClientState,
                ctx.ObjectTable,
                ctx.Vnavmesh,
                baseId: null,
                nameContains: GoldSaucer.JumboCactpotBrokerName,
                displayName: GoldSaucer.JumboCactpotBrokerName),
        ],
    };
}
