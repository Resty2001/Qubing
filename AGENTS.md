# AGENTS.md - Qubing Project Guide

## Project Identity

Project name: Qubing  
Engine: Unity 6000.3.3f1  
Language: C#  
Primary target: Android/iOS mobile  
Secondary test target: Windows  
Genre: Turn-based puzzle / survival arena  
Current stage: Working prototype, improving systems, UI, and long-run depth.

Qubing is a 7x7 turn-based dice-cube puzzle survival game. The player rolls a cube, accumulates charge on physical dice faces, kills color-matched enemies, manages a turn gauge, and survives enemy waves.

## Highest-Priority Rule

Always preserve the existing core game rules unless the user explicitly asks to change them. Prefer small, safe, reviewable diffs over large rewrites.

## Codex Working Rules

- Always assume Unity version is `6000.3.3f1`.
- Do not upgrade Unity, packages, render pipelines, Input System packages, or project settings unless explicitly requested.
- Use C# compatible with Unity 6000.3.3f1.
- Do not introduce unnecessary third-party packages.
- Use the global namespace unless the existing project is explicitly migrated to namespaces.
- Keep `GameManager` as the only singleton unless explicitly approved.
- Preserve serialized field names where possible to avoid breaking Inspector references.
- New systems should usually be separate classes/components.
- Avoid broad architecture rewrites unless the user specifically requests them.
- Do not edit generated or transient Unity folders:
  - `Library/`
  - `Temp/`
  - `Obj/`
  - `Logs/`
  - `UserSettings/`
  - build output folders
- After changes, report:
  1. files changed
  2. gameplay behavior changed
  3. Inspector fields added/renamed
  4. tests or manual checks performed
  5. risks and follow-up work

## Core Files To Inspect First

For gameplay tasks, inspect these scripts before editing:

- `GameManager.cs`
  - singleton
  - board generation
  - turn gauge
  - score/combo
  - enemy spawning
  - gameover checks
- `DiceController.cs`
  - keyboard input
  - touch swipe input
  - movement validation
  - combat handling
  - roll animation
  - hardlock check
- `DiceLogic.cs`
  - `DiceColor`
  - `DiceFace`
  - six physical dice faces
  - orientation updates
  - charge gain/spend
  - bottom HUD
- `Enemy.cs`
  - HP
  - color
  - grid position
  - visuals

If the files are in subfolders, search under `Assets/`.

## Board Rules

- Board size is fixed at 7x7.
- Valid grid coordinates are `x: 0..6`, `y: 0..6`.
- Player starts at `(3, 3)`.
- Positions are represented as `Vector2Int`.
- Grid x/y currently maps to Unity world x/z.
- Enemy cells block movement unless the enemy can be killed by a combat move.

Recommended helpers:

- `GameManager.IsInsideBoard(Vector2Int pos)`
- `GameManager.GetBoardTile(Vector2Int pos)`
- A stored `GameObject[,] boardTiles = new GameObject[7, 7]` for future indicator UI.

## Dice Rules

The dice has six physical faces.

Initial color layout:

- Initial Top: Red
- Initial Bottom: Red
- Initial North: Green
- Initial South: Green
- Initial East: Blue
- Initial West: Blue

Important:

- Opposite physical faces share the same color.
- Each physical face has independent charge.
- Do not merge the two Red faces, two Green faces, or two Blue faces into shared charge pools unless the user explicitly changes the design.
- Orientation must be identified using physical face identity, not only color.

Expected current orientation slots in `DiceLogic`:

- `topFace`
- `bottomFace`
- `northFace`
- `southFace`
- `eastFace`
- `westFace`

These variables represent current orientation slots. They should swap `DiceFace` object references when the cube rolls.

Recommended physical face ID enum:

```csharp
public enum DiceFaceId
{
    InitialTop = 0,
    InitialBottom = 1,
    InitialNorth = 2,
    InitialSouth = 3,
    InitialEast = 4,
    InitialWest = 5
}
```

## Movement Rules

On a valid empty-tile move:

1. The cube rolls one tile.
2. Dice orientation changes.
3. Only the physical face that becomes bottom gains `+1` charge.
4. Turn gauge increases by `+1`.
5. Combo resets to `1` unless a later upgrade explicitly modifies this.

On an invalid move:

- No movement.
- No orientation change.
- No charge gain.
- No turn gauge gain.
- No combo change unless explicitly implemented.

Implementation caution:

- Make sure `DiceController.currentGridPos` is updated before `GameManager.CheckGameOverCondition()` reads the player position.
- Empty move and combat move should both update logical position before any gameover/lock check triggered by `GameManager.OnPlayerMove()`.

## Combat Rules

Combat occurs when the target tile contains an enemy.

A combat move is allowed only if:

1. The future bottom face color matches the enemy color.
2. The same future bottom physical face has charge greater than or equal to the enemy's `currentHP`.

On successful combat:

1. Charge is consumed from the specific physical face that will become bottom.
2. Consumed amount equals `enemy.currentHP`.
3. Charge must not go below `0`.
4. Enemy is removed.
5. Score is awarded.
6. Combo increases.
7. Cube moves into the enemy tile.
8. Dice orientation changes.
9. Combat movement does not increase the turn gauge.

Overkill/Rewind:

- `overkill = futureBottomFace.charge - enemy.currentHP`
- If `overkill > 0`, rewind the turn gauge by `overkill`.
- Gauge must clamp at `0`.
- Preserve the existing overkill behavior unless explicitly asked to change it.

## Turn Gauge Rules

- `maxTurnGauge` defaults to `6`.
- Empty movement increases the gauge.
- Combat does not increase the gauge.
- When the gauge reaches max:
  - spawn an enemy wave
  - reset gauge to `0`
- Rewind subtracts from the gauge and clamps at `0`.

## Score / Combo Rules

Current score formula:

```text
scoreGain = enemy.maxHP * 10 * currentCombo
```

Current combo behavior:

- Successful consecutive kills increase combo.
- Empty movement resets combo to `1` unless a future upgrade explicitly modifies this.

Do not change score or combo formulas unless the user explicitly asks.

## Enemy Spawn Rules

Initial enemies:

- Spawn one Red, one Green, and one Blue.
- Each has HP `2`.
- Initial enemies must be excluded from the balance budget using `addToBalance = false`.

Wave enemies:

- Triggered when turn gauge reaches max.
- Difficulty multiplier:

```csharp
Mathf.Lerp(0.7f, 1.3f, Mathf.Clamp01(totalTurns / 100f))
```

- Spawn budget:

```text
targetTotalHP - cumulativeEnemyHP
```

- Maximum wave spawn count:
  - turns 0-29: 2 enemies
  - turns 30-59: 3 enemies
  - turns 60+: 4 enemies
- Enemy colors are Red/Green/Blue random.

Spawn position rules:

- Do not spawn on the player.
- Do not spawn on existing enemies.
- Respect `avoidPos` where applicable.
- `GetRandomSpawnPos()` should not declare MAP FULL merely because random sampling failed. Use deterministic fallback over all 49 cells.

## Gameover Rules

Current gameover layers:

1. Hardlock / trapped
   - Four directions are all blocked by walls or unkillable enemies.
   - Handled by `DiceController.CheckIfTrapped()`.
2. Map full
   - No legal spawn tile exists when a wave tries to spawn.
3. Softlock
   - The player can move, but under the current static board state, no reachable path can eventually kill any current enemy.
   - Should be detected by `SoftLockDetector`.
   - Should warn first, not instantly end the run.

Softlock warning target behavior:

- Start warning when softlock is detected.
- If the player kills an enemy during warning, clear warning immediately.
- If the player fails to kill within the configured warning count, trigger gameover with `SOFT LOCK`.
- Softlock detection should run periodically or on relevant events, not every frame.

Recommended fields in `GameManager`:

```csharp
[Header("Soft Lock")]
public int softLockCheckInterval = 4;
public int softLockWarningMaxTurns = 3;
public GameObject softLockWarningPanel;
public TMPro.TextMeshProUGUI txtSoftLockWarning;

private int _softLockCheckTimer;
private int _softLockWarningTurns;
private bool _isSoftLockWarning;
```

## Orientation Rules

When implementing orientation logic, do not identify orientation by color pair. Colors are duplicated across opposite faces and cannot uniquely represent the 24 cube orientations.

Use physical face IDs.

`OrientationTable` should model 24 cube orientations. Each orientation stores which physical face ID is currently in each slot:

- Top
- Bottom
- North
- South
- East
- West

The transition table must mirror `DiceLogic.UpdateFaces()` exactly.

Up / North:

```csharp
temp = top;
top = south;
south = bottom;
bottom = north;
north = temp;
```

Down / South:

```csharp
temp = top;
top = north;
north = bottom;
bottom = south;
south = temp;
```

Right / East:

```csharp
temp = top;
top = west;
west = bottom;
bottom = east;
east = temp;
```

Left / West:

```csharp
temp = top;
top = east;
east = bottom;
bottom = west;
west = temp;
```

Recommended API:

```csharp
public static int GetIndex(DiceFaceId top, DiceFaceId north)
public static int GetNextOrientation(int currentOrientation, Vector2Int dir)
public static DiceFaceId GetBottomFaceId(int orientation)
public static DiceColor GetBottomColor(int orientation)
```

Generate the 24 legal orientations deterministically from identity using BFS or static initialization. Avoid reflection.

## SoftLockDetector Rules

Softlock detection must track charge by physical face ID, not by color.

Preferred state graph:

- position: 7x7 = 49
- orientation: 24
- total nodes: 1176
- for each node, best known charge for six physical faces: `int[6]`

Algorithm requirements:

- Active enemy cells are obstacles during path exploration.
- The player cannot pass through enemy cells.
- Empty moves virtually roll orientation and add `+1` to the physical face that becomes bottom.
- Track charges capped at max active enemy HP.
- Validate attacks separately from movement exploration.
- For each enemy, inspect adjacent attack-from cells.
- For each reachable orientation at attack-from, virtually roll toward the enemy.
- Check future bottom physical face color and charge against enemy color and HP.
- If any kill path exists, the state is not softlocked.
- If no kill path exists, the state is softlocked.

Performance requirements:

- Do not run softlock detection in `Update()`.
- Prefer interval-based checks such as every 4 player actions and after wave spawn if practical.
- Avoid LINQ in the detector.
- Avoid unnecessary allocations in hot loops.
- Prefer bounded arrays such as `Node[7,7,24]` over dictionaries when practical.
- Do not mutate actual game state inside `SoftLockDetector`.

Suggested public API:

```csharp
public static bool IsSoftLocked(
    Vector2Int playerPos,
    int playerOrientation,
    DiceFace[] currentFacesInSlots,
    List<Enemy> activeEnemies,
    bool[,] occupiedByEnemy)
```

A cleaner wrapper API may accept `DiceController`, `DiceLogic`, and enemies, but it must snapshot state and remain pure.

## Press-Down UI Direction

Planned goal:

- Normal mode: show color hints only and reduce number clutter.
- Press-down/hold mode: show charge values, attack possible/impossible hints, and expanded bottom HUD info.

Normal mode:

- Show color overlays for relevant directions.
- Show minimal enemy attack hints.
- Bottom HUD shows color identity without excessive numbers.

Press-down mode:

- Show directional face charge values.
- Show check/cross attack icons near enemies.
- Highlight cube border or selection state.
- Show expanded bottom HUD with color and charge.

Recommended architecture:

- Add `IndicatorController` as a separate component.
- `DiceController` should only report hold state and movement events.
- `DiceLogic` should provide future bottom face data.
- `GameManager` should provide board tile references and active enemy data.

## Cube Visual Direction

Planned simplification:

- Remove or disable 3D numeric TextMeshPro on cube faces because numbers are hard to read during rotation.
- Keep color + simple symbol identity:
  - Red: triangle
  - Green: square
  - Blue: circle
- Do not remove bottom HUD charge values unless implementing the Press-down UI design.

## Upgrade / Power-Up System Direction

Future planned system is inspired by Vampire Survivors-style level-up choices, but Qubing should remain a turn-based puzzle game.

Do not add unless explicitly requested:

- Real-time combat
- Bullet-hell action
- Enemy movement AI
- Automatic attacks that bypass the dice puzzle

Power-ups should modify:

- charge economy
- turn gauge
- combo rules
- score
- spawn pressure
- dice face behavior
- limited combat conditions

Preferred architecture:

- Add `UpgradeManager` as a separate component.
- Use shared rule hooks so `DiceController`, `GameManager`, and `SoftLockDetector` evaluate the same rules.

Recommended hooks:

```csharp
ModifyChargeGain(...)
CanAttack(...)
ModifyAttackCost(...)
ModifyRewind(...)
ModifyWaveBudget(...)
OnEnemyKilled(...)
OnEmptyMove(...)
```

Critical:

If an upgrade changes charge gain, movement legality, attack legality, color matching, attack cost, or enemy HP rules, `SoftLockDetector` must use the same rule source. Do not duplicate upgrade logic separately in gameplay and softlock code.

Suggested implementation phases:

1. Stabilize core movement/spawn bugs.
2. Implement `OrientationTable` and `SoftLockDetector`.
3. Implement Press-down UI.
4. Add `UpgradeManager`, XP, and level-up UI.
5. Add safe power-ups first: score, XP, gauge, warnings.
6. Add combat/charge-changing power-ups only after softlock integration supports them.

## UI Rules

Existing UI:

- Canvas Screen Space
- turn gauge
- score
- bottom HUD
- gameover panel
- settings panel

When adding UI fields:

- Use null guards around optional references.
- Report all new serialized fields in the final response.
- Do not rename existing serialized fields unless necessary.
- If a field rename is necessary, mention possible broken Inspector references.

## Performance Rules

Mobile-first constraints:

- Do not run heavy graph searches every frame.
- Do not allocate in `Update()` loops.
- Do not use LINQ in hot gameplay loops.
- Cache components in `Awake()` or `Start()`.
- Avoid repeated `GetComponent` calls in gameplay loops.
- Use `static readonly` direction arrays where practical.
- Keep graph searches bounded and event/interval-based.

## Logging Rules

Use tagged logs:

```csharp
Debug.Log("[Wave] ...");
Debug.Log("[SoftLock] ...");
Debug.LogWarning("[VFX] ...");
Debug.LogError("[Orientation] ...");
```

Avoid noisy logs in `Update()`.

## Unity Inspector Rules

- Existing scripts use public Inspector fields; preserving that style is acceptable.
- Prefer `[SerializeField] private` for new fields only when it does not conflict with current style.
- Do not rename serialized fields unless necessary.
- If adding fields, state where to assign them in the Unity Inspector.
- If making a field private, make sure existing Inspector references will not break.

## Known Safe Stabilization Tasks

These are acceptable small fixes when relevant:

1. Add `GameManager` duplicate singleton guard:

```csharp
if (Instance != null && Instance != this)
{
    Destroy(gameObject);
    return;
}
Instance = this;
```

2. Change keyboard movement from held input to single-press behavior if current code uses `isPressed` unintentionally.
3. Update player logical position before `GameManager.OnPlayerMove()` can trigger lock checks.
4. Add deterministic fallback to spawn position search.
5. Add null guards for optional UI/VFX references.
6. Use `sharedMaterial` for fixed enemy materials unless per-instance runtime material mutation is required.
7. Store generated board tiles for future indicator systems.

## Protected Behaviors

Do not break these unless the user explicitly asks:

- Initial enemies use `addToBalance = false`.
- Empty move charges only the new bottom physical face.
- Combat consumes only the future-bottom physical face.
- Combat does not increment turn gauge.
- Overkill rewind behavior.
- `DiceLogic.GetFutureBottomFace()` semantics.
- `DiceLogic.ConsumeCharge()` semantics.
- `DiceLogic.UpdateFaces()` rotation mapping.
- Score/combo formula.
- Board size and player start position.
- `GameManager` as the only singleton.

## Testing Expectations

If Unity CLI/test runner is available, use Unity 6000.3.3f1 to run relevant tests.

If automated Unity tests are not available, perform static validation and provide a manual test checklist.

Manual gameplay checks for core changes:

- Empty move updates position, orientation, charge, and gauge.
- Combat success consumes the correct physical face charge.
- Combat failure does not move and does not increase gauge.
- Overkill rewind still clamps gauge at 0.
- Combo increases on consecutive kills.
- Empty move resets combo.
- Initial enemies are excluded from `cumulativeEnemyHP`.
- Wave spawn avoids player position and `avoidPos`.
- Wave spawn does not report MAP FULL if a legal spawn tile exists.
- Hardlock still triggers `TRAPPED!`.
- Softlock warning appears only when appropriate.
- Killing during softlock warning clears warning.

## Expected Final Response Format From Codex

When completing a task, respond with:

1. Summary
2. Files changed
3. Inspector changes
4. Tests run or manual checks
5. Risks / follow-up

Keep the summary concise but specific. Mention any assumptions that were made.
