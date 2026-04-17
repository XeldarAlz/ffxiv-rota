using System.Collections.Generic;

namespace Rota.Automation;

public sealed class Workflow
{
    public required string Name { get; init; }
    public required IReadOnlyList<IStep> Steps { get; init; }

    /// <summary>Tags used by UI for grouping/filter; also surfaced in logs.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Estimated runtime for UI display. Best-effort.</summary>
    public int EstimatedMinutes { get; init; } = 0;
}
