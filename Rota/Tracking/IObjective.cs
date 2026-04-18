using System.Collections.Generic;
using Rota.Automation;

namespace Rota.Tracking;

public enum ObjectiveCadence
{
    Daily,
    Weekly,
    OncePerReset,
}

public enum ObjectiveState
{
    Unknown,      // reader has not evaluated yet, or data unavailable
    NotLoggedIn,  // cannot evaluate without a character context
    Pending,      // can be done / has uncompleted work
    InProgress,   // partial progress (e.g. 1/3 beast tribe quests done)
    Completed,    // fully done for this cycle
    Unavailable,  // locked (content not unlocked, wrong level, etc.)
    Blocked,      // prerequisite plugin missing, or pre-flight check failed
}

public readonly record struct ObjectiveStatus(
    ObjectiveState State,
    string? Detail = null,
    int Current = 0,
    int Max = 0);

public interface IObjective
{
    string Id { get; }
    string DisplayName { get; }
    string Category { get; }          // e.g. "Roulette", "Beast Tribe", "Weekly"
    ObjectiveCadence Cadence { get; }

    ObjectiveStatus Evaluate();

    /// <summary>
    /// IPC keys (see <see cref="Services.IpcRegistry.KnownPlugins"/>) this objective
    /// needs in order to be runnable. Empty = read-only objective with no Run action.
    /// </summary>
    IReadOnlyList<string> RequiredPlugins { get; }

    /// <summary>
    /// Build an orchestrator workflow that completes this objective.
    /// Return null if no workflow is wired — the UI will disable the Run button.
    /// Default implementation returns null so readers don't have to opt out.
    /// </summary>
    Workflow? BuildWorkflow(WorkflowContext ctx) => null;
}
