namespace Rota.Automation.Workflows;

/// <summary>
/// Well-known identifiers around the Gold Saucer. Grouped here so Cactpot,
/// Fashion Report, Triple Triad tournaments, etc. can all reuse them.
/// </summary>
internal static class GoldSaucer
{
    public const uint AetheryteId = 62;
    public const uint TerritoryId = 144;

    /// <summary>Approximate in-world position of Lewena, the Jumbo Cactpot NPC.</summary>
    public static readonly System.Numerics.Vector3 LewenaPosition = new(-13.3f, 1.0f, 7.5f);

    /// <summary>Jumbo Cactpot NPC name — used as a fallback when BaseId isn't known.</summary>
    public const string LewenaName = "Lewena";
}
