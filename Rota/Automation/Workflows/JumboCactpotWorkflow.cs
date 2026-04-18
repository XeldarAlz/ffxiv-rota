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

            // The aethernet shard is a few yalms from Lewena; a short walk
            // covers the residual distance if vnavmesh has a mesh here.
            new WalkToStep(
                ctx.Vnavmesh,
                ctx.ClientState,
                ctx.ObjectTable,
                destination: GoldSaucer.LewenaPosition,
                displayName: GoldSaucer.LewenaName,
                arrivalRadius: 4.5f),

            new InteractWithNpcStep(
                ctx.ClientState,
                ctx.ObjectTable,
                dataId: null,
                nameContains: GoldSaucer.LewenaName,
                displayName: GoldSaucer.LewenaName),
        ],
    };
}
