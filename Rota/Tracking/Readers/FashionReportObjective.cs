using System;
using System.Collections.Generic;
using Dalamud.Plugin.Services;

namespace Rota.Tracking.Readers;

/// <summary>
/// Fashion Report — weekly glamour contest in the Gold Saucer.
///
/// The bonus window (where high scores pay out extra MGP) runs from Friday
/// 08:00 UTC to Tuesday 08:00 UTC each week. Outside that window the NPC
/// won't accept submissions. We compute window status locally because the
/// server doesn't expose a schedule via CS.
///
/// Passive *score* read is not available (would require scraping
/// AgentFashionCheck). State rules:
///   - Outside the bonus window         -> Unavailable (closed)
///   - Inside the bonus window          -> Pending    (open, go submit)
///
/// Upgrading to track "already submitted this week" requires new data
/// source — see task #12.
/// </summary>
public sealed class FashionReportObjective : IObjective
{
    private readonly IClientState _clientState;

    public string Id => "weekly:fashion-report";
    public string DisplayName => "Fashion Report";
    public string Category => "Weekly";
    public ObjectiveCadence Cadence => ObjectiveCadence.Weekly;

    public IReadOnlyList<string> RequiredPlugins { get; } = new[] { "Lifestream" };

    public FashionReportObjective(IClientState clientState)
    {
        _clientState = clientState;
    }

    public ObjectiveStatus Evaluate()
    {
        if (!_clientState.IsLoggedIn) return new ObjectiveStatus(ObjectiveState.NotLoggedIn);

        var now = DateTime.UtcNow;
        var (inWindow, nextChange) = FashionReportWindow.Evaluate(now);

        if (!inWindow)
            return new ObjectiveStatus(ObjectiveState.Unavailable,
                $"window opens in {FormatDelta(nextChange - now)}");

        return new ObjectiveStatus(ObjectiveState.Pending,
            Detail: $"window closes in {FormatDelta(nextChange - now)}");
    }

    private static string FormatDelta(TimeSpan span)
    {
        if (span.TotalDays >= 1) return $"{(int)span.TotalDays}d {span.Hours}h";
        if (span.TotalHours >= 1) return $"{(int)span.TotalHours}h {span.Minutes}m";
        return $"{(int)span.TotalMinutes}m";
    }
}

/// <summary>
/// Fashion Report bonus-window schedule.
///
/// Weekly window: Friday 08:00 UTC -> Tuesday 08:00 UTC (inclusive start,
/// exclusive end — matches the live-service daily-reset convention).
/// </summary>
internal static class FashionReportWindow
{
    public static (bool InWindow, DateTime NextChange) Evaluate(DateTime utcNow)
    {
        // Anchor on the start of this week's Friday 08:00 UTC.
        var todayMidnight = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc);
        int daysSinceFriday = ((int)utcNow.DayOfWeek - (int)DayOfWeek.Friday + 7) % 7;
        var fridayOpen = todayMidnight.AddDays(-daysSinceFriday).AddHours(8);
        var tuesdayClose = fridayOpen.AddDays(4);           // Fri -> Tue (4 days)

        if (utcNow >= fridayOpen && utcNow < tuesdayClose)
            return (true, tuesdayClose);

        // Otherwise we're between close and next open — next Friday 08:00 UTC.
        var nextOpen = utcNow < fridayOpen ? fridayOpen : fridayOpen.AddDays(7);
        return (false, nextOpen);
    }
}
