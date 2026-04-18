using System;
using System.Linq;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;

namespace Rota.Services.Ipc;

/// <summary>
/// Wrapper over Lifestream. Prefers Lifestream's own /li chat command (through
/// Dalamud's command registry) over IPC for driving actions — Lifestream's
/// EzIPC registrations for void-returning actions have a known gotcha where
/// some endpoints register but the client-side subscriber reports NotReady.
/// The /li command handler is unambiguous: Lifestream registers it itself,
/// and invoking through <see cref="ICommandManager.Commands"/> is exactly
/// what happens when a player types the command.
/// </summary>
public sealed class LifestreamIpc
{
    private readonly IDalamudPluginInterface _pi;
    private readonly ICommandManager _cmd;
    private readonly IPluginLog _log;

    private ICallGateSubscriber<bool>? _isBusy;

    public LifestreamIpc(IDalamudPluginInterface pi, ICommandManager cmd, IPluginLog log)
    {
        _pi = pi;
        _cmd = cmd;
        _log = log;
    }

    /// <summary>
    /// Read-only probe: is Lifestream currently driving a teleport/travel task?
    /// Uses the Lifestream.IsBusy IPC (Func endpoint — these register reliably,
    /// unlike Action-style endpoints). Returns false on any failure so workflow
    /// logic degrades gracefully.
    /// </summary>
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

    /// <summary>
    /// Aethernet-teleport to a named shard within the current zone, e.g.
    /// "The Cactpot Board", "Wonder Square". Dispatched through Lifestream's
    /// own /li chat command.
    /// </summary>
    public bool AethernetTeleport(string placeName) => InvokeLiCommand(placeName);

    /// <summary>
    /// Pass an arbitrary argument string to /li. Useful for aliases.
    /// </summary>
    public bool RunLiAlias(string alias) => InvokeLiCommand(alias);

    private bool InvokeLiCommand(string args)
    {
        var entry = _cmd.Commands.FirstOrDefault(kv =>
            string.Equals(kv.Key, "/li", StringComparison.OrdinalIgnoreCase));

        if (entry.Key is null || entry.Value is null)
        {
            _log.Error("[Rota] /li command is not registered — is Lifestream enabled?");
            return false;
        }

        try
        {
            entry.Value.Handler.Invoke("/li", args);
            return true;
        }
        catch (Exception ex)
        {
            _log.Error(ex, "[Rota] /li {0} failed", args);
            return false;
        }
    }
}
