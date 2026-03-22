# Building System Handoff

This file preserves the important project context from the current implementation and design discussion so work can continue on another computer.

## Core Vision

- The game centers around a grid-based decorating system similar to Happy Home Designer.
- Terrain exposes a `female grid` that receives objects.
- Each placeable object has exactly one `male grid` on its underside.
- Each placeable object may expose multiple `female grid` layers at different heights for stacking objects on top.
- NPCs should stop on the centers of `1 x 1` terrain tiles when stationary and should eventually interact with objects from specific sides.

## Terminology

- `female grid`: The world or support grid that receives placeable objects.
- `female tiles`: Individual receivable cells on terrain or object surfaces.
- `male grid`: The underside snap footprint of a placeable object.
- `male tiles`: The bottom cells of an object that snap into female tiles.

## Current Grid Rules

- World units map `1:1` with gameplay space.
- Full terrain tiles are `1 x 1` world units.
- Each full tile is subdivided into `0.5 x 0.5` subtiles for placement.
- The lowest female layer should start at a minimum height of `0.5` above the male base plane.
- Objects currently support irregular male footprints by painting a male mask inside the detected rectangular male bounds.

## Current Authoring Model

- `PlaceableGridAuthoring` is the main authoring component.
- Male footprint size is auto-derived from colliders when enabled.
- Male footprint cells can be painted on or off.
- Female layers are organized as a hierarchy of groups.
- Groups define spacing for their ordered children.
- Groups can contain layers or nested groups.
- Female layer heights are computed from the group spacing hierarchy, not manually entered per layer.

## Current Implemented Scripts

### Building and Decorating

- `.github/agents/decorating-system.agent.md`
  - Workspace custom agent definition for this decorating system.

- `Assets/Scripts/Building & Decorating/Data/PlaceableGridAuthoring.cs`
  - Runtime authoring data for male and female grids.
  - Stores the painted male mask.
  - Stores nested female groups and layers.
  - Draws male and female gizmos.
  - Uses `[SerializeReference]` for recursive hierarchy serialization.

- `Assets/Scripts/Building & Decorating/Editor/PlaceableGridAuthoringEditor.cs`
  - Compact inspector for the authoring component.

- `Assets/Scripts/Building & Decorating/Editor/PlaceableGridAuthoringWindow.cs`
  - Main authoring window.
  - Supports:
    - male grid painting
    - female layer painting
    - nested female groups
    - group spacing editing
    - basic hierarchy reordering via controls

## Current Player Scripts

- `Assets/Scripts/Player/PlayerMotor.cs`
  - Simple capsule motor.
  - WASD movement.
  - Spacebar jump.
  - CharacterController-based.
  - Can use Input System actions or keyboard fallback.

- `Assets/Scripts/Player/OrthographicFollowCamera.cs`
  - Orthographic follow camera.
  - Fixed world-facing base direction.
  - Follows player from above and behind.
  - Rotates with limited deviation toward midpoint between player and cursor.
  - Latest version uses screen-space midpoint logic.

- `Assets/Scripts/Player/PlayerInteractionController.cs`
  - Opens and closes a player menu.
  - Intended for a player-attached world-space or overlay canvas.
  - Can disable the player motor while the menu is open.
  - Exposes `RequestEnterBuildMode()` for menu buttons.

- `Assets/Scripts/Player/WorldCanvasFaceCamera.cs`
  - Billboard helper for world-space canvases so they face the camera.
  - User planned to move this to a more general/shared folder.

## Current Gizmo Rules

- Male gizmos are offset slightly below the underside to avoid z-fighting.
- Gizmo drawing can be disabled per object with a checkbox on `PlaceableGridAuthoring`.
- Female gizmos use alternating pink and orange tile families.
- Male gizmos use alternating blue and purple tile families.
- Each `1 x 1` tile family is subdivided visually by lighter and darker `0.5 x 0.5` subtiles.

## Known Recent Issues and Fixes

- Male grid size originally ignored transform scaling.
  - Fixed by measuring collider bounds in projected authoring axes with scale applied.

- Gizmo tiles originally scaled incorrectly and could extend outside the object.
  - Fixed by placing tiles from world-unit projected collider bounds.

- Female hierarchy hit Unity serialization depth-limit errors.
  - Fixed by switching recursive hierarchy fields to `[SerializeReference]`.

- UI image asset issue for `Player Menu BG.png`.
  - The image was set to `Sprite (2D and UI)` but `Sprite Mode` was `Multiple` with no sliced sprites.
  - For a normal menu background it should be `Sprite Mode = Single`.

## Current Camera Direction

- The desired behavior is Animal Crossing-like:
  - fixed world-facing camera direction
  - orthographic
  - soft follow
  - slight cursor-influenced rotation only
- Camera is not supposed to tether itself to player rotation.

## Suggested Next Steps

Recommended next implementation priorities:

1. Build runtime placement foundation
   - `TerrainFemaleGrid`
   - placement occupancy
   - world-to-grid conversions

2. Build placement validation
   - validate male footprint against terrain female tiles
   - validate stacking on female layers
   - validate clearance above female layers

3. Build build-mode state controller
   - enter from `PlayerInteractionController`
   - disable movement appropriately
   - show placement preview

4. Improve hierarchy UX later
   - true drag-and-drop hierarchy reordering
   - move entries out to parent group
   - better visual feedback for group structure

## Important Setup Notes For Another Computer

- The repo contains the code and this handoff document, so copying the repo preserves the implemented context.
- The live chat history itself will not automatically move to another computer.
- Open this file first on the new machine to recover the design and implementation context quickly.
- Also review:
  - `.github/agents/decorating-system.agent.md`
  - `Assets/Scripts/Building & Decorating/Data/PlaceableGridAuthoring.cs`
  - `Assets/Scripts/Building & Decorating/Editor/PlaceableGridAuthoringWindow.cs`
  - `Assets/Scripts/Player/PlayerMotor.cs`
  - `Assets/Scripts/Player/OrthographicFollowCamera.cs`
  - `Assets/Scripts/Player/PlayerInteractionController.cs`
