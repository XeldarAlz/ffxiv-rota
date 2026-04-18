using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Rota.Tracking.Readers;

public static class Roulettes
{
    /// <summary>
    /// Enumerates Lumina's ContentRoulette sheet and registers one objective per
    /// row that has a non-empty name and a valid completion index.
    /// </summary>
    public static void RegisterAll(
        ObjectiveRegistry registry,
        IDataManager data,
        IClientState clientState,
        IPluginLog log)
    {
        var sheet = data.GetExcelSheet<ContentRoulette>();
        int added = 0;

        foreach (var row in sheet)
        {
            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (row.CompletionArrayIndex < 0) continue;

            var obj = new DutyRouletteObjective(
                rouletteRowId: row.RowId,
                completionIndex: (byte)row.CompletionArrayIndex,
                name: name,
                clientState: clientState);

            registry.Register(obj);
            added++;

            log.Debug("[Rota] roulette row={0} name='{1}' completionIndex={2}",
                row.RowId, name, (int)row.CompletionArrayIndex);
        }

        log.Information("[Rota] Registered {0} duty roulette objectives.", added);
    }
}
