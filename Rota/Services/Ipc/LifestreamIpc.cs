using System;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace Rota.Services.Ipc;

/// <summary>
/// Thin wrapper over Lifestream's public IPC endpoints.
/// All calls are best-effort: failures are logged and surfaced as a false return,
/// so workflow steps can fail cleanly rather than propagating exceptions into
/// the framework tick.
///
/// Endpoint names mirror Lifestream's public contract; if Lifestream renames
/// one we catch the miss here and the dependent step reports a clean error.
/// </summary>
public sealed class LifestreamIpc
{
    private readonly IDalamudPluginInterface _pi;
    private readonly IPluginLog _log;

    private ICallGateSubscriber<uint, object>? _teleport;
    private ICallGateSubscriber<bool>? _isBusy;
    private ICallGateSubscriber<string, object>? _executeCommand;

    public LifestreamIpc(IDalamudPluginInterface pi, IPluginLog log)
    {
        _pi = pi;
        _log = log;
    }

    public bool Teleport(uint aetheryteId)
    {
        try
        {
            _teleport ??= _pi.GetIpcSubscriber<uint, object>("Lifestream.Teleport");
            _teleport.InvokeAction(aetheryteId);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Rota] Lifestream.Teleport({0}) failed", aetheryteId);
            return false;
        }
    }

    public bool IsBusy()
    {
        try
        {
            _isBusy ??= _pi.GetIpcSubscriber<bool>("Lifestream.IsBusy");
            return _isBusy.InvokeFunc();
        }
        catch (Exception ex)
        {
            _log.Warning("[Rota] Lifestream.IsBusy probe failed: {0}", ex.Message);
            return false;
        }
    }

    public bool ExecuteCommand(string command)
    {
        try
        {
            _executeCommand ??= _pi.GetIpcSubscriber<string, object>("Lifestream.ExecuteCommand");
            _executeCommand.InvokeAction(command);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Rota] Lifestream.ExecuteCommand('{0}') failed", command);
            return false;
        }
    }
}
