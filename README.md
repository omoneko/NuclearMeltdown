# Nuclear Meltdown

A Cities: Skylines (2015 / base game) mod. When a nuclear power plant is destroyed by fire or a
disaster, it does not just quietly collapse — it can go into meltdown: a large explosion that
craters the ground and flattens the district around it, followed by long-lasting radioactive
ground contamination.

> Base game only. If the **Natural Disasters** DLC is present, the vanilla meteor impact effect
> is used for the explosion; without it the crater, the destruction and the contamination all
> still happen.
>
> **Harmony (Mod Dependency) is required** — subscribe to CitiesHarmony on the Steam Workshop or
> this mod will not work.

## Features

- **Only fire and disasters trigger it.** Bulldozing a plant yourself does nothing.
- **Three ways to decide how big it is**, chosen in the options:
  - *Random* — the original probability table: 5% huge, 15% large, 45% normal, 30% fallout only,
    5% collapse only
  - *Based on plant output* — the scale is directly proportional to the plant's power output,
    with a vanilla nuclear plant (640 MW) as 1.0. There is no ceiling, so a modded monster
    reactor really can take out the map.
  - *Fixed* — always the multiplier you set
- **Explosion and fallout can be switched on and off independently** — fallout with no blast,
  a blast with no fallout, or neither, in which case the plant simply collapses.
- **The blast** forms a crater and destroys the buildings, roads and trees around it, scaled by
  severity, and can chain-react between nuclear plants built close together.
- **Radioactive fallout** reuses the game's own ground-pollution system, and the mod keeps
  reasserting it so it does not quietly decay away.
- **Save and load are supported** — the contamination survives a reload.

## Clearing the contamination

- It lifts on its own after **50 in-game years**, or
- build a **decontamination facility** near the contaminated area — any building asset whose name
  contains `Decontamination` — and it clears at about **5% per in-game month**.

**A water treatment plant does not decontaminate.** You need a dedicated decontamination
facility.

## Companion mod

Works alongside the **Missile Disaster** mod: the same "Decontamination facility" asset cleans up
nuclear fallout from both mods.

## Building from source

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

This builds with MSBuild (targeting .NET Framework 3.5, for the game's Unity 5.6) and deploys the
DLL plus `CitiesHarmony.API.dll` to
`%LOCALAPPDATA%\Colossal Order\Cities_Skylines\Addons\Mods\NuclearMeltdown`.

The build needs the game's managed DLLs. It looks for them in the usual Steam location; if yours
is elsewhere, set `CITIES_SKYLINES_MANAGED` to your `Cities_Data\Managed` folder.

Pure logic lives in `src/NuclearMeltdown/Core` (UnityEngine-free — the pollution grid maths, the
50-year clock, the outcome table, the output scaling, the zone serialiser) and is covered by
xUnit tests in `tests/`:

```powershell
dotnet test
```

`src/NuclearMeltdown/README.md` has the step-by-step in-game verification procedure, and
`docs/` holds the design document and the implementation plan.

Logs are written to `Cities_Data\output_log.txt`; search it for `[NuclearMeltdown]`.

## License

MIT — see [LICENSE](LICENSE).
