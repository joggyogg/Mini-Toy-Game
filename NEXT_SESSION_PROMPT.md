# Next Session Prompt

Use this in a new Copilot chat to restore working context quickly:

---

I am continuing work on my Unity project "Mini Toy Game". Please first read these files for project context:

- `BUILDING_SYSTEM_HANDOFF.md`
- `.github/agents/decorating-system.agent.md`
- `Assets/Scripts/Building & Decorating/Data/PlaceableGridAuthoring.cs`
- `Assets/Scripts/Building & Decorating/Editor/PlaceableGridAuthoringWindow.cs`
- `Assets/Scripts/Player/PlayerMotor.cs`
- `Assets/Scripts/Player/OrthographicFollowCamera.cs`
- `Assets/Scripts/Player/PlayerInteractionController.cs`

Important project concepts:

- The game centers around a grid-based decorating system.
- Terrain/support receivers are called the `female grid`.
- Object underside snap footprints are called the `male grid`.
- Each object has exactly one male grid and may expose multiple female grid layers.
- Female layers are grouped in nested spacing groups.
- Terrain tiles are `1 x 1` world units with `0.5 x 0.5` placement subtiles.
- NPCs should eventually stop on `1 x 1` tile centers and interact from specific sides of objects.

Current implemented systems:

- `PlaceableGridAuthoring` for male/female authoring data
- custom editor window for painting grids and managing female group hierarchy
- `PlayerMotor`
- `OrthographicFollowCamera`
- `PlayerInteractionController`

Current likely next priority:

- build runtime placement foundation (`TerrainFemaleGrid`, placement validation, and build mode flow)

Please summarize the current state back to me first before making changes.

---