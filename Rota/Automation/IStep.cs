namespace Rota.Automation;

public enum StepState
{
    Idle,
    Running,
    Completed,
    Failed,
    Cancelled,
}

/// <summary>
/// One discrete unit of work in a workflow.
/// A step is started once, polled each frame via <see cref="Tick"/>, and is done
/// when <see cref="State"/> is Completed / Failed / Cancelled.
///
/// Implementations MUST be:
///   - non-blocking: Tick() returns immediately
///   - cancellable: Cancel() leaves the game in a safe state
///   - idempotent on Start(): double-starts are no-ops
/// </summary>
public interface IStep
{
    string Name { get; }
    StepState State { get; }
    string? FailureReason { get; }

    /// <summary>Preconditions for starting. Return false to skip/fail.</summary>
    bool CanStart(out string? reason);

    void Start();
    void Tick();
    void Cancel();
}
