Include ..\AGENTS.md

# Automation Tweaks — Mod-Specific Agent Instructions

## Identity
- **Assembly:** `autotweaks`
- **Namespace:** `Calloatti.AutoTweaks`
- **Framework:** Harmony, Bindito DI
- **ModId:** `Calloatti.AutoTweaks`
- **Min Game Version:** 1.0.12.5 — uses `timberborn-decompiled-1.0.*`

## What This Mod Does
Tweaks automation UI and behavior: patches automate button, auto-rename, relay colors, and relay behavior across automation buildings.

## Source Architecture (`Version-1.0/Source/`)

| File | Role |
|---|---|
| `PatchConfigurator.cs` | DI configurator |
| `PatchAutomateButton.cs` | Harmony patch on automate button behavior |
| `PatchAutoRename.cs` | Auto-rename patch for automation buildings |
| `PatchColor.cs` | Color-related patches |
| `RelayPatches.cs` | Relay behavior patches |
| `RelayColorReplicator.cs` | Color replication for relays |
| `RelayColorReplicatorConfigurator.cs` | DI configurator for relay replicator |

## Version Folders
- `Version-1.0` — targets game 1.0.x.x
- `Version-1.1` — targets game 1.1.x.x
