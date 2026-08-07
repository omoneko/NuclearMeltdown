# NuclearMeltdown mod - design

- Date: 2026-07-08
- Target: Cities: Skylines (2015 / Unity 5 era / .NET Framework 3.5)
- Status: approved, pending verification of the real APIs before implementation

## 1. Overview

A system mod that, when a nuclear power plant - a plant with a `PowerPlantAI` - either **burns
down in a fire** or **collapses** through a disaster, produces **an explosion effect equivalent
to a meteor strike** at its position and applies **wide-area ground pollution** standing in for
radioactive contamination.

The contamination lifts in either of two ways:

1. **Time**: **50 in-game years** after it appeared.
2. **Decontamination**: an existing building standing in for a decontamination facility - by
   default the Water Treatment Plant - operates inside or near the zone and gradually removes
   the contamination in range.

The mod **holds the contamination in place** against Cities: Skylines' own natural decay, so it
does not fade until one of those conditions is met.

## 2. Technical requirements

- Language and framework: C# on .NET Framework 3.5
- References: `ICities.dll`, `Assembly-CSharp.dll`, `UnityEngine.dll` and `ColossalManaged.dll`,
  from the game's `Cities_Data\Managed\` directory
- Harmony: the `CitiesHarmony.API` NuGet package, applied through
  `HarmonyHelper.DoOnHarmonyReady`
- Build: MSBuild (VS2022), an old-style csproj with
  `<TargetFrameworkVersion>v3.5</TargetFrameworkVersion>`. The dotnet SDK is not used, since it
  does not support net35.
- Deployed to `%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\NuclearMeltdown\`
- Interface: `ICities.IUserMod`, whose Name and Description appear in the mod manager

## 3. Architecture

```
NuclearMeltdown/
├─ NuclearMeltdown.csproj            # net35, old-style MSBuild, NuGet: CitiesHarmony.API
├─ Properties/AssemblyInfo.cs
├─ Source/
│  ├─ Mod.cs                         # IUserMod; patches and unpatches Harmony in OnEnabled/OnDisabled
│  ├─ NuclearDetector.cs             # identifies a nuclear plant (PowerPlantAI plus the prefab name)
│  ├─ MeltdownEffect.cs              # creates the explosion effect and writes the initial contamination
│  ├─ ContaminationManager.cs        # the ledger of zones (centre, radius, start time) - the core
│  ├─ Patches/
│  │   └─ BuildingCollapsePatch.cs   # Postfix detecting destruction (the real API to be confirmed by decompiling)
│  ├─ Simulation/
│  │   └─ MeltdownThreading.cs       # IThreadingExtension: expiry, upkeep and decontamination each tick
│  └─ Serialization/
│      └─ ContaminationSerializer.cs # saves and restores the ledger through ISerializableData
├─ docs/specs/2026-07-08-nuclear-meltdown-mod-design.md
└─ README.md
```

## 4. Component responsibilities

### Mod.cs
- Implements `IUserMod` (`Name`, `Description`).
- Applies and removes the patches in `OnEnabled` and `OnDisabled` through
  `HarmonyHelper.DoOnHarmonyReady`.
- If needed, offers a settings UI in `OnSettingsUI` for options such as which building
  decontaminates and the contamination radius.

### NuclearDetector.cs
- `IsNuclearPlant(ushort buildingID)`, a pure predicate.
- The test: `BuildingManager.instance.m_buildings.m_buffer[id].Info.m_buildingAI is PowerPlantAI`
  and the prefab name containing "Nuclear" or similar. The real names to be confirmed by
  decompiling and against the actual data.

### BuildingCollapsePatch.cs
- A Harmony `Postfix` detecting the destruction trigger.
- Candidate hooks, to be verified by decompiling before implementing:
  - collapse: `CommonBuildingAI.CollapseBuilding`
  - burning down: whichever method in `BuildingAI`/`CommonBuildingAI` removes a building lost to
    fire
- On detecting one it checks `NuclearDetector.IsNuclearPlant` and, for a nuclear plant, does
  nothing but call `MeltdownEffect.Trigger(position)`. The patch stays thin.

### MeltdownEffect.cs
- `Trigger(Vector3 position)`:
  - (a) creates the meteor explosion effect at the position. How the effect prefab or
    `EffectInfo` is obtained is to be confirmed by decompiling.
  - (b) writes the initial ground pollution, falling off from the centre out to about 700 m,
    into `NaturalResourceManager`'s pollution cells.
  - (c) registers the zone through
    `ContaminationManager.RegisterZone(center, radius, startTime)`.

### ContaminationManager.cs (the core)
- Holds the list of contamination zones as
  `{ Vector3 center, float radius, DateTime startGameTime }`.
- `RegisterZone(...)`, `RemoveZone(...)`, `GetZones()`.
- Utilities for writing a zone into the pollution grid cells and clearing it again, with bounds
  checking.
- Immutable by preference: updating the zones produces a new list.

### MeltdownThreading.cs
- In `IThreadingExtension.OnAfterSimulationTick`, at a reduced frequency if needed:
  1. **Reasserts** each zone's pollution cells, holding the level against the natural decay.
  2. Releases and clears any zone **50 in-game years** past its start, measured as the difference
     in years against `SimulationManager.instance.m_currentGameTime`.
  3. Decontamination: where an operating decontamination building - the water treatment plant -
     stands inside or near a zone, gradually reduces the contamination in range, releasing the
     zone once it is all gone.

### ContaminationSerializer.cs
- Saves and restores the ledger into the save game through `ISerializableData`, via
  `SerializableDataExtensionBase`.
- Stored as binary under a unique key such as `"NuclearMeltdown.Contamination.v1"`.
- On a failed deserialisation it continues with an empty ledger, so it cannot corrupt the save.

## 5. Data flow

```
the game destroys a nuclear plant (fire or collapse)
  └→ BuildingCollapsePatch.Postfix
        └→ NuclearDetector.IsNuclearPlant? ── no → do nothing
              └ yes → MeltdownEffect.Trigger(pos)
                        ├→ create the explosion effect
                        ├→ write the initial pollution, falling off out to 700 m
                        └→ ContaminationManager.RegisterZone

every simulation tick: MeltdownThreading
  ├→ reassert the pollution cells (upkeep)
  ├→ a zone past 50 years → clear and release it
  └→ a decontamination facility in range → reduce it gradually → release once clear

save and load: ContaminationSerializer persists the ledger
```

## 6. Error handling and safety

- The patch, the tick and the serialisation are all wrapped in try/catch, so an exception in the
  mod never takes the game with it.
- A guard against firing twice for repeated destruction events on the same building.
- Bounds checking when converting a position to a grid cell.
- An empty ledger as the fallback when deserialisation fails.
- No console output is left behind; logging goes through CS's `DebugOutputPanel` or `Debug.Log`
  only.

## 7. Verification

- Confirm it compiles for net35 under MSBuild, references and all.
- **Check the following real signatures by decompiling (with ilspycmd or similar) before
  implementing**:
  - the destruction hooks (`CollapseBuilding` and whatever handles burning down)
  - writing the ground pollution cells (`NaturalResourceManager`)
  - the in-game clock (`SimulationManager.m_currentGameTime`)
  - obtaining and playing the meteor `EffectInfo`
  - the real `ISerializableData` and `IThreadingExtension` interfaces
- Verifying it in the running game - triggering it, waiting 50 years, decontaminating - is
  **left to the user**, since the game cannot be launched from here.

## 8. Open questions (to be resolved in the implementation plan)

- The exact signatures and arguments of the destruction hooks, and how collapse and burning down
  differ.
- How to obtain the meteor explosion effect: through `EffectCollection` or `DisasterManager`, or
  by referencing an `EffectInfo` directly.
- The exact resolution, coordinate conversion and write API of the pollution cells.
- How to identify the decontamination facility (the water treatment plant) - by AI type or by
  prefab name - and the parameters governing how fast it works.
- How far to space the tick work out, and what the upkeep costs with a large number of zones.

## 9. Out of scope (YAGNI)

- Creating a new building asset for the decontamination facility, since an existing building is
  reused.
- A bespoke radiation resource system, since the existing pollution is reused.
- A multilingual UI; the initial wording is kept minimal.
