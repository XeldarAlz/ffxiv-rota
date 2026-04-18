using System;
using System.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Rota.Services;

/// <summary>
/// Diagnostic helpers for inspecting native addons live. Used to reverse-engineer
/// callback shapes when CS doesn't ship a typed AddonXxx struct.
/// </summary>
public static class AddonDiagnostics
{
    /// <summary>
    /// Dump the AtkValues array of the named addon to the provided log. Each
    /// slot is logged with its index, type, and value so we can diff before/after
    /// a UI action to discover what the action writes.
    /// </summary>
    public static unsafe void DumpAtkValues(IGameGui gui, IPluginLog log, string addonName)
    {
        var addon = (AtkUnitBase*)gui.GetAddonByName(addonName).Address;
        if (addon is null)
        {
            log.Info("[Rota][diag] addon '{0}' is not loaded", addonName);
            return;
        }

        var count = addon->AtkValuesCount;
        var values = addon->AtkValues;

        var sb = new StringBuilder();
        sb.AppendLine($"[Rota][diag] {addonName}: Visible={addon->IsVisible}, AtkValuesCount={count}");
        for (int i = 0; i < count; i++)
        {
            var v = values[i];
            sb.Append($"  [{i,3}] {v.Type,-8} = ");
            switch (v.Type)
            {
                case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int:
                    sb.AppendLine(v.Int.ToString()); break;
                case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.UInt:
                    sb.AppendLine(v.UInt.ToString()); break;
                case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Bool:
                    sb.AppendLine(v.Byte != 0 ? "true" : "false"); break;
                case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Float:
                    sb.AppendLine(v.Float.ToString("0.###")); break;
                case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String:
                case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.String8:
                case FFXIVClientStructs.FFXIV.Component.GUI.ValueType.ManagedString:
                    try { sb.AppendLine('"' + System.Runtime.InteropServices.Marshal.PtrToStringUTF8((nint)v.String.Value) + '"'); }
                    catch { sb.AppendLine("<string>"); }
                    break;
                default:
                    sb.AppendLine($"<type={(int)v.Type}>"); break;
            }
        }
        log.Info(sb.ToString());
    }
}
