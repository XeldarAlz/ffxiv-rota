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

            // Broker's first menu option is "I'd like to purchase a ticket."
            new SelectStringStep(
                ctx.GameGui,
                ctx.Log,
                optionIndex: 0,
                displayName: "Purchase a ticket"),

            // Ticket UI: press Random to roll 4 digits, press Purchase, confirm.
            // Purchase = case 0 (verified: ticket is issued with current digits).
            // Random case is being searched live — case 1 produced no effect,
            // trying case 2 next. Iterate in this file as we narrow it down.
            new AddonCallbackStep(
                ctx.GameGui,
                ctx.Log,
                addonName: "LotteryWeeklyInput",
                intArgs: [2],
                displayName: "Random numbers (case=2)"),

            new AddonCallbackStep(
                ctx.GameGui,
                ctx.Log,
                addonName: "LotteryWeeklyInput",
                intArgs: [0],
                displayName: "Purchase"),

            // Confirm "Purchase this ticket for N MGP?" — standard SelectYesno.
            new SelectYesnoStep(ctx.GameGui, ctx.Log, clickYes: true),
        ],
    };
}
