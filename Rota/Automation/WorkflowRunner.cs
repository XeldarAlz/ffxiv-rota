using System;
using Dalamud.Plugin.Services;

namespace Rota.Automation;

public enum RunnerState { Idle, Running, Succeeded, Failed, Cancelled }

public sealed class WorkflowRunner : IDisposable
{
    private readonly IFramework _framework;
    private readonly IPluginLog _log;

    private Workflow? _current;
    private int _cursor;
    private bool _tickSubscribed;

    public RunnerState State { get; private set; } = RunnerState.Idle;
    public string? CurrentStepName => _current?.Steps is { } s && _cursor < s.Count ? s[_cursor].Name : null;
    public string? LastError { get; private set; }

    public event Action<Workflow, IStep>? StepStarted;
    public event Action<Workflow, IStep>? StepCompleted;
    public event Action<Workflow>? Finished;
    public event Action<Workflow, string>? Failed;

    public WorkflowRunner(IFramework framework, IPluginLog log)
    {
        _framework = framework;
        _log = log;
    }

    public bool TryStart(Workflow wf, out string? reason)
    {
        if (State == RunnerState.Running)
        {
            reason = $"Another workflow is running: {_current?.Name}.";
            return false;
        }

        _current = wf;
        _cursor = 0;
        State = RunnerState.Running;
        LastError = null;

        _log.Information("[Rota] Starting workflow: {0} ({1} steps)", wf.Name, wf.Steps.Count);

        if (!_tickSubscribed)
        {
            _framework.Update += OnTick;
            _tickSubscribed = true;
        }

        BeginCurrentStep();
        reason = null;
        return true;
    }

    public void Cancel()
    {
        if (State != RunnerState.Running || _current is null) return;

        var step = _current.Steps[_cursor];
        try { step.Cancel(); } catch (Exception ex) { _log.Error(ex, "[Rota] Step.Cancel threw"); }

        State = RunnerState.Cancelled;
        _log.Warning("[Rota] Workflow cancelled at step '{0}'.", step.Name);
        Unsubscribe();
    }

    private void BeginCurrentStep()
    {
        if (_current is null) return;

        while (_cursor < _current.Steps.Count)
        {
            var step = _current.Steps[_cursor];
            if (!step.CanStart(out var reason))
            {
                Fail($"Step '{step.Name}' cannot start: {reason ?? "unspecified"}");
                return;
            }

            try
            {
                step.Start();
                StepStarted?.Invoke(_current, step);
                _log.Debug("[Rota] -> Step {0}/{1}: {2}", _cursor + 1, _current.Steps.Count, step.Name);
                return;
            }
            catch (Exception ex)
            {
                Fail($"Step '{step.Name}' threw on start: {ex.Message}");
                return;
            }
        }

        Complete();
    }

    private void OnTick(IFramework _)
    {
        if (State != RunnerState.Running || _current is null) return;

        var step = _current.Steps[_cursor];
        try { step.Tick(); }
        catch (Exception ex)
        {
            Fail($"Step '{step.Name}' threw in Tick: {ex.Message}");
            return;
        }

        switch (step.State)
        {
            case StepState.Completed:
                StepCompleted?.Invoke(_current, step);
                _cursor++;
                BeginCurrentStep();
                break;
            case StepState.Failed:
                Fail($"Step '{step.Name}' failed: {step.FailureReason ?? "unspecified"}");
                break;
            case StepState.Cancelled:
                State = RunnerState.Cancelled;
                Unsubscribe();
                break;
        }
    }

    private void Complete()
    {
        State = RunnerState.Succeeded;
        _log.Information("[Rota] Workflow '{0}' completed.", _current!.Name);
        Finished?.Invoke(_current);
        Unsubscribe();
    }

    private void Fail(string msg)
    {
        State = RunnerState.Failed;
        LastError = msg;
        _log.Error("[Rota] Workflow '{0}' failed: {1}", _current?.Name ?? "<none>", msg);
        if (_current is not null) Failed?.Invoke(_current, msg);
        Unsubscribe();
    }

    private void Unsubscribe()
    {
        if (_tickSubscribed)
        {
            _framework.Update -= OnTick;
            _tickSubscribed = false;
        }
    }

    public void Dispose()
    {
        Cancel();
        Unsubscribe();
    }
}
