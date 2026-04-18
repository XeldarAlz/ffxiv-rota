namespace Rota.Automation.Workflows;

/// <summary>
/// Well-known identifiers around the Gold Saucer. Grouped here so Cactpot,
/// Fashion Report, Triple Triad tournaments, etc. can all reuse them.
/// </summary>
internal static class GoldSaucer
{
    public const uint AetheryteId = 62;
    public const uint TerritoryId = 144;

    /// <summary>
    /// In-game displayed name of the NPC that sells Jumbo Cactpot tickets.
    /// Substring match against IGameObject.Name.TextValue.
    /// </summary>
    public const string JumboCactpotBrokerName = "Jumbo Cactpot Broker";

    /// <summary>Lifestream aethernet shard that puts you next to the Jumbo Cactpot Broker.</summary>
    public const string CactpotBoardAethernet = "The Cactpot Board";
}
