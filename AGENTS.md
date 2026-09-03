Include ..\AGENTS.md

# Automation Tweaks — Mod-Specific Agent Instructions

## Identity
- **Assembly:** `autotweaks`
- **Namespace:** `Calloatti.AutoTweaks`
- **Framework:** Harmony, Bindito DI
- **ModId:** `Calloatti.AutoTweaks`
- **Min Game Version:** 1.0.12.5 — uses `timberborn-decompiled-1.0.*`

## What This Mod Does
Tweaks automation UI and behavior: patches automate button, auto-rename, relay/memory colors, and relay behavior across automation buildings.

## Source Architecture (`Version-1.0/Source/`)

| File | Role |
|---|---|
| `PatchConfigurator.cs` | DI configurator |
| `PatchAutomateButton.cs` | Harmony patch on automate button behavior |
| `PatchAutoRename.cs` | Auto-rename patch for automation buildings |
| `PatchColor.cs` | Color persistence patches for all automation buildings with `CustomizableIlluminator` |
| `RelayPatches.cs` | Relay behavior patches (UI toggle for color replication) |
| `RelayColorReplicator.cs` | Color replication logic for relays (activation history, mode-specific) |
| `RelayColorReplicatorConfigurator.cs` | DI configurator for relay replicator |
| `MemoryPatches.cs` | Memory behavior patches (UI toggle for color replication) |
| `MemoryColorReplicator.cs` | Color replication logic for memory (activation history, mode-aware) |
| `MemoryColorReplicatorConfigurator.cs` | DI configurator for memory replicator |

## Version Folders
- `Version-1.0` — targets game 1.0.x.x
- `Version-1.1` — targets game 1.1.x.x

## Key Features Implemented

### Color Persistence (`PatchColor.cs`)
- **Force `IsCustomized = true`** always — prevents game from clearing color on UI close
- **`SetCustomColor(null)` → `_defaultColor`** — Reset button reverts to building's default color
- **`Apply` / `EffectiveColor` patches** — bypass game's `IsCustomized` check; always use `_customColor`
- **Per-building panel visibility** — `ConditionalWeakTable<CustomizableIlluminator, BoolWrapper>`
- **Per-fragment UI elements** — `ConditionalWeakTable<CustomizableIlluminatorFragment, UIPair>` for `ColorNameLabel` + `ResetButton`
- **Reset button** — always visible; calls `SetCustomColor(null)` → `_defaultColor`
- **Applies to all 15 automation buildings** with `CustomizableIlluminator` (Lever, Relay, Memory, WeatherStation, Chronometer, ScienceCounter, ResourceCounter, PopulationCounter, PowerMeter, Timer, Gate, Indicator, Speaker, DepthSensor, ContaminationSensor, FlowSensor, HttpAdapter, HttpLever)

### Relay Color Replicator (`RelayColorReplicator.cs`)
- **Activation history** (max 8 for v1.1, max 2 for v1.0) tracks input turn-on order
- **Mode-specific logic**: AND (last to complete), OR (most recent ON), XOR (first active)
- **Toggle behavior**: OFF = unlock + manual color works; ON = lock + replicate input color
- **No re-check needed** when toggling — handles subscribe/unsubscribe cleanly

### Memory Color Replicator (`MemoryColorReplicator.cs`)
- **Activation history** (max 8 for v1.1, max 2 for v1.0) tracks input turn-on order
- **Inputs tracked**: InputA + InputB only (ResetInput excluded as control signal)
- **Mode-aware**: InputB only subscribed when `_memory.UsesInputB` is true (Latch/FlipFlop modes)
- **Toggle behavior**: OFF = unlock + manual color works; ON = lock + replicate input color
- **Input change detection**: re-subscribes when wiring changes (InputA/InputB reconnected)
- **Evaluated on `Memory.CommitTick`** postfix — runs after state is committed

### Publicizer Fixes
- `DoNotPublicize` for `CustomColorChanged` and `AppliedColorChanged` events in both `.csproj` files (fixes CS0229 ambiguity)

## Decompiled Game Resources
- Base path: `C:\Users\calloatti\source\repos\timberborn-decompiled-*`
- Version mapping: `Version-1.0` → `timberborn-decompiled-1.0.*`, `Version-1.1` → `timberborn-decompiled-1.1.*`
- Always use highest game version folder when multiple exist

## Hard Rule
DO NOT EVER TOUCH THE DEPLOY FOLDER.

BUILD DOES EVERYTHING, NEVER EVER MESS WITH THE DEPLOY PROCESS.