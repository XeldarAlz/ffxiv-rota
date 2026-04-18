using System;
using System.Numerics;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace Rota.Services.Ipc;

/// <summary>
/// Thin wrapper over vnavmesh's public IPC endpoints.
///
/// vnavmesh splits its API across three subsystems:
///   - vnavmesh.Nav.*        mesh state (IsReady, BuildProgress)
///   - vnavmesh.SimpleMove.* high-level "path to a point and walk there"
///   - vnavmesh.Path.*       low-level running-path control (IsRunning, Stop)
///
/// PathfindAndMoveTo returns immediately; IsPathRunning() reports whether the
/// async path is still active; StopPath() cancels it cleanly.
/// </summary>
public sealed class VnavmeshIpc
{
    private readonly IDalamudPluginInterface _pi;
    private readonly IPluginLog _log;

    private ICallGateSubscriber<bool>? _isReady;
    private ICallGateSubscriber<Vector3, bool, bool>? _pathfindAndMoveTo;
    private ICallGateSubscriber<bool>? _pathIsRunning;
    private ICallGateSubscriber<object>? _pathStop;

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
            _pathfindAndMoveTo ??= _pi.GetIpcSubscriber<Vector3, bool, bool>("vnavmesh.SimpleMove.PathfindAndMoveTo");
            // Returns true if a path was queued. Ignore return value; we'll poll IsPathRunning.
            _pathfindAndMoveTo.InvokeFunc(dest, fly);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Rota] vnavmesh.SimpleMove.PathfindAndMoveTo({0}) failed", dest);
            return false;
        }
    }

    public bool IsPathRunning()
    {
        try
        {
            _pathIsRunning ??= _pi.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
            return _pathIsRunning.InvokeFunc();
        }
        catch (Exception ex)
        {
            _log.Warning("[Rota] vnavmesh.Path.IsRunning probe failed: {0}", ex.Message);
            return false;
        }
    }

    public void StopPath()
    {
        try
        {
            _pathStop ??= _pi.GetIpcSubscriber<object>("vnavmesh.Path.Stop");
            _pathStop.InvokeAction();
        }
        catch (Exception ex)
        {
            _log.Warning("[Rota] vnavmesh.Path.Stop failed: {0}", ex.Message);
        }
    }
}
