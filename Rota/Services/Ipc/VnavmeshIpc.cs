using System;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace Rota.Services.Ipc;

/// <summary>
/// Thin wrapper over vnavmesh's public IPC endpoints. Used for pathfinding
/// from the current player position to a target coord inside the current zone.
///
/// vnavmesh auto-loads the mesh for the current territory; PathfindAndMoveTo
/// returns immediately and runs asynchronously. IsRunning() reports whether
/// navigation is still in progress; Stop() cancels it cleanly.
/// </summary>
public sealed class VnavmeshIpc
{
    private readonly IDalamudPluginInterface _pi;
    private readonly IPluginLog _log;

    private ICallGateSubscriber<bool>? _isReady;
    private ICallGateSubscriber<Vector3, bool, object>? _pathfindAndMoveTo;
    private ICallGateSubscriber<bool>? _isRunning;
    private ICallGateSubscriber<object>? _stop;

    public VnavmeshIpc(IDalamudPluginInterface pi, IPluginLog log)
    {
        _pi = pi;
        _log = log;
    }

    public bool IsReady()
    {
        try
        {
            _isReady ??= _pi.GetIpcSubscriber<bool>("vnavmesh.Nav.IsReady");
            return _isReady.InvokeFunc();
        }
        catch (Exception ex)
        {
            _log.Warning("[Rota] vnavmesh.Nav.IsReady probe failed: {0}", ex.Message);
            return false;
        }
    }

    public bool PathfindAndMoveTo(Vector3 dest, bool fly = false)
    {
        try
        {
            _pathfindAndMoveTo ??= _pi.GetIpcSubscriber<Vector3, bool, object>("vnavmesh.Nav.PathfindAndMoveTo");
            _pathfindAndMoveTo.InvokeAction(dest, fly);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Rota] vnavmesh.Nav.PathfindAndMoveTo({0}) failed", dest);
            return false;
        }
    }

    public bool IsRunning()
    {
        try
        {
            _isRunning ??= _pi.GetIpcSubscriber<bool>("vnavmesh.Nav.IsRunning");
            return _isRunning.InvokeFunc();
        }
        catch (Exception ex)
        {
            _log.Warning("[Rota] vnavmesh.Nav.IsRunning probe failed: {0}", ex.Message);
            return false;
        }
    }

    public void Stop()
    {
        try
        {
            _stop ??= _pi.GetIpcSubscriber<object>("vnavmesh.Nav.Stop");
            _stop.InvokeAction();
        }
        catch (Exception ex)
        {
            _log.Warning("[Rota] vnavmesh.Nav.Stop failed: {0}", ex.Message);
        }
    }
}
