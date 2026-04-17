# Rota

Consolidated daily & weekly content tracker for FFXIV, with optional orchestration of other Dalamud plugins.

## What it does

- Reads your current daily/weekly progress (roulettes, beast tribes, Wondrous Tails, Custom Deliveries, Fashion Report, Jumbo Cactpot, PvP series, hunt marks, etc.)
- Shows it all in one panel with live status
- For each item, exposes a **Run** button that orchestrates the plugins you already have installed — Lifestream, vnavmesh, AutoDuty, Questionable, AutoRetainer, RotationSolver, BossModReborn, PandorasBox, YesAlready, TextAdvance — to actually complete the content

Rota is an **orchestrator**. It never talks to the game directly for automation; it only calls other plugins' IPC endpoints. If a dependency isn't installed, the relevant button is disabled with a clear reason.

## Status

Early scaffold. Read-only status for a subset of items; workflow engine present but no shipped workflows yet.

## Install

Not on the main Dalamud repo, and will not be. Ships via a custom repo URL. Add it under `/xlsettings → Experimental → Custom Plugin Repositories` once published.

## Build

1. Install .NET SDK matching the current `Dalamud.NET.Sdk` version.
2. `XIVLauncher` must be installed and Dalamud must have been injected at least once (to populate `%AppData%\XIVLauncher\addon\Hooks\dev\`).
3. Open `Rota.sln` in Visual Studio 2022+ or Rider, or `dotnet build` in the `Rota/` folder.
4. DLL output: `Rota/bin/x64/Debug/Rota.dll`.
5. In-game: `/xlsettings → Experimental → Dev Plugin Locations`, add the DLL path, then `/xlplugins` to enable.

## Commands

- `/rota` — open the main panel
- `/rt` — alias for `/rota`

## License

AGPL-3.0-or-later, same as the Dalamud SDK.
