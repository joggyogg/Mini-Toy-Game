# Backlog

This file is the running backlog for project issues, technical debt, feature work, and polish tasks.

## How To Use This File

- Add new items as short action-oriented bullets.
- Keep status tags at the start of each line.
- Suggested tags: `[now]`, `[next]`, `[later]`, `[risk]`, `[done]`.
- When an item is completed, change its tag to `[done]` instead of deleting it immediately.
- If a task turns into multiple tasks, split it into separate bullets instead of making one bullet too broad.

## Current Risks

- `[risk]` UI input architecture is still split between the Input System UI module and a StandaloneInputModule fallback for the decorate catalog. This works, but could cause future inconsistencies across menus, controller navigation, or scene-to-scene UI behavior.
- `[risk]` SampleScene had stale EventSystem `.inputactions` references after the project move. Similar stale serialized references may still exist in other scenes or prefabs.
- `[risk]` Some build-mode furniture prefabs are authored with offset transforms or pivots. Runtime placement compensates for this now, but authoring inconsistencies can still make future placement/debugging harder.

## Decorating System Core

- `[next]` Add occupancy tracking so placed furniture reserves terrain female cells and future placements cannot overlap.
- `[next]` Add selection of already-placed furniture so the player can pick objects back up and reposition them.
- `[next]` Add drag/move workflow after initial placement instead of only direct spawn near the player.
- `[next]` Add rotation controls for placed or selected furniture during the move flow.
- `[later]` Expand placement validation beyond terrain-only support to object female layers and stacking.
- `[later]` Add clearance validation above female layers so stacked placement respects vertical space.
- `[later]` Add support for irregular and multi-tile furniture occupancy updates after placement.

## Build Mode And UI

- `[next]` Add visible build-mode feedback when placement fails, instead of relying on Console warnings only.
- `[next]` Add clear selected-state or hover-state styling for furniture catalog buttons.
- `[later]` Audit all project UI to decide on one long-term input module strategy and remove temporary module switching if possible.
- `[later]` Add proper controller navigation support across player menu and decorate catalog.
- `[later]` Add scroll support and category filtering if the furniture catalog grows.
- `[later]` Add icon fallback behavior for furniture entries with no assigned sprite.

## Data And Authoring

- `[next]` Review all furniture definition assets and prefabs for origin, collider, and footprint consistency.
- `[next]` Review TerrainFemaleGrid authoring on all playable surfaces to confirm enabled cells match intended placement areas.
- `[later]` Add authoring-time validation helpers or warnings for malformed furniture prefabs and invalid grid setups.
- `[later]` Standardize naming and folder spelling for building data assets, including the current `Defintions` folder typo if you want to clean that up safely.

## Player And Camera

- `[later]` Revisit player interaction/menu flow to ensure future menus do not need custom click fallbacks or menu-specific hacks.
- `[later]` Confirm OrthographicFollowCamera behavior still feels correct during build mode, movement, and UI-heavy interactions.

## Scene And Project Hygiene

- `[next]` Audit SampleScene for any remaining stale references caused by the project move to the new PC.
- `[later]` Check other scenes for EventSystem consistency, input asset references, and UI module setup.
- `[later]` Decide which project-context docs should stay current: `BUILDING_SYSTEM_HANDOFF.md`, `NEXT_SESSION_PROMPT.md`, and this backlog.

## Nice-To-Have Polish

- `[later]` Add placement sound effects and button feedback for catalog interactions.
- `[later]` Add simple placement success/failure VFX or highlights on target cells.
- `[later]` Add an in-game debug overlay for current build-mode state, selected furniture, and candidate cell info.