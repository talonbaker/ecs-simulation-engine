# Unity Visualizer — Setup Guide

> **Engine version:** Unity 6 (6000.0.x / 6000.4.x)
> **Target runtime:** .NET 8 (matches APIFramework)

---

## 1. Build the DLL

From the **repo root**, run:

```bat
# Windows
build-unity-dll.bat

# macOS / Linux
./build-unity-dll.sh
```

This compiles `APIFramework` in Release mode and copies `APIFramework.dll`
(and its `.pdb` for debugging) into `UnityVisualizer/Assets/Plugins/`.

---

## 2. Open the project in Unity

1. Open **Unity Hub**
2. Click **Add → Add project from disk**
3. Point it at `<repo root>/UnityVisualizer`
4. Open with **Unity 6 (6000.0.x or later)**

Unity will import the project and compile the scripts against the DLL.
There should be **zero compiler errors** if the DLL was built first.

---

## 3. Set up the scene

Create a new scene (or open the default `SampleScene`) and add three
empty GameObjects to it:

| Name               | Component to add         | Notes                          |
|--------------------|--------------------------|--------------------------------|
| `SimulationManager`| `SimulationManager`      | Singleton — one per scene      |
| `WorldSceneBuilder`| `WorldSceneBuilder`      | Reads from SimulationManager   |
| *(optional)*       | —                        | Camera at (10, 12, -10), look down-forward |

### Wiring

- **SimulationManager** requires no additional wiring.  It auto-locates
  `SimConfig.json` by walking up from the Unity `Assets/` folder.
  The repo-root `SimConfig.json` will be found automatically.

- **WorldSceneBuilder** also requires no wiring.  Optionally assign
  `World Object Root` and `Entity Root` transforms in the Inspector to
  keep the hierarchy tidy.

---

## 4. Inspector settings

### SimulationManager

| Field             | Default | Effect                                             |
|-------------------|---------|----------------------------------------------------|
| Config Path       | *(blank)*| Override to use a specific `SimConfig.json`       |
| Speed Multiplier  | 1       | 0 = pause, 1 = real-time, 5 = max fast-forward    |

### WorldSceneBuilder

| Field             | Default | Effect                                             |
|-------------------|---------|----------------------------------------------------|
| World Scale       | 1       | ECS units → Unity metres (increase to spread out)  |
| World Object Root | *(null)*| Optional parent transform for world-object cubes   |
| Entity Root       | *(null)*| Optional parent transform for entity cubes         |

---

## 5. Press Play

You should see:

- A **dark grey floor** plane (50 × 50 units)
- **World-object cubes** (fridge, sink, toilet, bed) at their configured positions
- **Entity cubes** (Billy, Cat) sliding smoothly around the floor
- Floating **text labels** above each object showing name, destination, and drive
- An **organ strip** extending to the right of each entity cube, with cubes that
  grow/shrink and change colour as fills change
- A small **orange bolus cube** sliding down the esophagus whenever an entity eats

---

## 6. What each script does

| Script                  | Role                                              |
|-------------------------|---------------------------------------------------|
| `SimulationManager.cs`  | Owns the ECS engine, ticks it, exposes Snapshot   |
| `WorldSceneBuilder.cs`  | Creates/syncs world-object and entity GameObjects |
| `EntityCubeView.cs`     | Moves, colours, and labels a living entity cube   |
| `OrganCluster.cs`       | Renders the GI tract strip + bolus transit        |
| `WorldObjectCubeView.cs`| Colours and labels a world-object cube            |
| `EcsColors.cs`          | Central colour palette — edit to restyle globally |

---

## 7. Rebuilding after code changes

Any time you change `APIFramework` C# code, re-run the build script and
then switch back to Unity and press **Ctrl + R** (Windows) or **Cmd + R**
(macOS) to force a DLL reimport.

You do **not** need to rebuild the DLL when changing Unity scripts —
Unity recompiles those automatically on save.
