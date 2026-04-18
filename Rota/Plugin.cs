using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using Rota.Automation;
using Rota.Services;
using Rota.Tracking;
using Rota.Tracking.Readers;
using Rota.Windows;

namespace Rota;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IKeyState KeyState { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string MainCommand = "/rota";
    private const string ShortCommand = "/rt";

    public Configuration Configuration { get; }
    public IpcRegistry Ipc { get; }
    public ObjectiveRegistry Objectives { get; }
    public WorkflowRunner Runner { get; }

    public readonly WindowSystem WindowSystem = new("Rota");
    private MainWindow MainWindow { get; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        Ipc = new IpcRegistry(PluginInterface, Log);
        Objectives = new ObjectiveRegistry();
        Runner = new WorkflowRunner(Framework, Log);

        Roulettes.RegisterAll(Objectives, DataManager, ClientState, Log);
        Objectives.Register(new WondrousTailsObjective(ClientState));
        Objectives.Register(new CustomDeliveriesObjective(ClientState));
        Objectives.Register(new PvPSeriesObjective(ClientState));

        MainWindow = new MainWindow(this);
        WindowSystem.AddWindow(MainWindow);

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleMainUi;

        CommandManager.AddHandler(MainCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Rota panel.",
            ShowInHelp = true,
        });
        CommandManager.AddHandler(ShortCommand, new CommandInfo(OnCommand)
        {
            HelpMessage = "Alias for /rota.",
            ShowInHelp = false,
        });

        Log.Information("Rota loaded.");
    }

    public void Dispose()
    {
        CommandManager.RemoveHandler(MainCommand);
        CommandManager.RemoveHandler(ShortCommand);

        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleMainUi;

        WindowSystem.RemoveAllWindows();
        MainWindow.Dispose();

        Runner.Dispose();
        Ipc.Dispose();
    }

    private void OnCommand(string command, string args) => ToggleMainUi();

    public void ToggleMainUi() => MainWindow.Toggle();
}
