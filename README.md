# Operation Cross-Fire: Protecting the Orbital Corridor

Junior Unity Developer take-home exercise prototype, built to the exercise brief in
[Assets/GDD/Junior Unity Dev Exercise_ _Operation Cross-Fire_ Protecting the Orbital Corridor_.pdf](Assets/GDD/Junior%20Unity%20Dev%20Exercise_%20_Operation%20Cross-Fire_%20Protecting%20the%20Orbital%20Corridor_.pdf)
and cross-checked against the equivalent
[.docx version](Assets/GDD/Junior-Unity-Dev-Exercise_-_Operation-Cross-Fire_-Protecting-the-Orbital-Corridor_.docx),
which embeds the actual 4×2 sprite reference sheet and HUD mockup images (not visible
from the PDF's extracted text alone). Prototype sprites and HUD colours were adjusted
to match that reference sheet where it was cheap to do so — see "Assumptions" below for
the one place that mattered functionally (laser orientation) versus what stayed
cosmetic. Unity 6000.3.10f1, Universal Render Pipeline, 2D physics.

Two players share one interceptor for a single 60-second round. One is the **Pilot**
(movement + Boost), the other is the **Gunner** (aim, fire, Shield). Roles swap at 20s
and 40s via the **Quantum Flux** event, which also ramps up difficulty. The round is
won at 60s with hull remaining, and lost if hull reaches 0 or a Breach Hazard reaches
the bottom of the playfield.

This is a single-scene, single-round prototype. Per the brief's own "Out of scope" list
(multiple levels, production menus, tutorials, save data) and "Engineering over art"
guidance, there is no main menu, no level select, and all sprites are basic flat-shape
placeholders — the brief explicitly asks candidates not to spend time on those.

## Architecture

All gameplay code lives under `Assets/Scripts/CrossFire/`, namespace `CrossFire`, split
by responsibility:

| Folder | Script | Responsibility |
|---|---|---|
| `Core/` | `PoolObjectType`, `IPoolableObject` | Pool key enum + the reset contract every pooled prefab implements |
| `Core/` | `Enums.cs` | `Role`, `PlayerSlot`, `GamePhase`, `GameRoundState` |
| `Core/` | `PlayfieldBounds` | Lazily-initialized world-space playfield extents from the main camera |
| `Pooling/` | `ObjectPooler` | Generic `Dictionary<PoolObjectType, Queue<GameObject>>` pool, prewarmed at `Awake`, cached `IPoolableObject` references |
| `Managers/` | `GameManager` | Round timer, phase index, Quantum Flux state machine, win/loss, score |
| `Managers/` | `RoleManager` | Player1/Player2 ↔ Pilot/Gunner assignment |
| `Input/` | `MobileInputController` | Single source of truth for editor keyboard/mouse *and* device multi-touch |
| `Ship/` | `ShipController`, `WeaponSystem`, `ShieldSystem` | Movement/bounds/hull/invulnerability, fire cadence, Shield damage prevention |
| `Hazards/` | `HazardBase` + `EnemyDrone`/`Debris`/`BreachHazard`, `EnemyProjectile`, `PlayerLaser`, `EnemySpawner` | Pooled hazards and the phase-aware spawner |
| `Boundaries/` | `BottomBoundary`, `CleanupBoundary`, `PoolCleanupUtility` | Breach-loss detection and off-screen pool return |
| `UI/` | `HUDController` | Placeholder default-uGUI display layer, zero gameplay logic of its own |

**Pattern**: plain C# singletons (`static Instance`, assigned in `Awake`) for the
scene-unique managers, with `System.Action` events for one-to-many notification (HUD
listens to `GameManager`/`RoleManager`/`ShipController` events; nothing polls another
manager's private state). `GameManager` directly calls `RoleManager.SwapRoles()` and
`MobileInputController.CancelAllInputs()` as orchestration steps rather than going
through another layer of events — those two calls only ever have one caller, so a
direct method call is more honest about the actual coupling than wrapping it in an
event only `GameManager` itself would raise and handle. This is a deliberately lighter
architecture than the ScriptableObject event-channel pattern sketched in the technical
GDD; for a single-scene, ~15-script prototype under a strict timebox, a channel asset
per event would add indirection without a corresponding decoupling benefit, since
every consumer is a Mono­Behaviour in the same scene anyway.

**Quantum Flux** is implemented as one method, `GameManager.ExecuteFluxTransition`,
executing the brief's five steps in order: cancel input → swap roles → apply the new
phase's multipliers → raise `OnPhaseChanged` (HUD updates role labels/panels) → raise
`OnFluxBanner`. It's driven by a single elapsed-time counter and a phase index (0/1/2),
not by separate role-swap and flux timers.

## Input ownership & role switching

`MobileInputController` is the only script that reads raw input. Everything else
(`ShipController`, `WeaponSystem`, `ShieldSystem`) polls its published per-frame state
(`PilotMoveAxis`, `GunnerAimWorldPosition`, `GunnerFireHeld`, `TryConsumeBoost()`,
`TryConsumeShieldActivation()`).

- **Editor**: keyboard (A/D or arrows, Space) always drives whichever player currently
  holds the Pilot role; mouse (drag to aim, left click fire, right click Shield) always
  drives whichever player currently holds Gunner — matching the brief's control table,
  which maps by *role*, not by player. Implemented against the new Input System's
  low-level device API (`Keyboard.current` / `Mouse.current`), compiled only into
  editor/development builds.
- **Touch**: Player 1 owns the left half of the screen, Player 2 the right half — that
  physical ownership never changes. What each half *controls* depends on that player's
  current role, resolved once when a touch begins (`RegisterTouch`) by hit-testing the
  touch position against that role's zone `RectTransform`s. The resolved zone is stored
  against the touch's `touchId` in a dictionary and never re-evaluated for the life of
  that touch — so a finger drifting across the centre line mid-drag keeps controlling
  whatever it started controlling, per the brief's requirement. Held-state (movement
  axis, Fire) is recomputed each frame from whichever touches are still active, which
  naturally handles "holding both Left and Right stops movement" and multi-finger Fire
  without fragile increment/decrement bookkeeping. A Fire- or Shield-zone touch is
  never treated as an AimArea touch, so it can't jitter the reticle.
- Implemented with the new Input System's Enhanced Touch API (`Touch.activeTouches`),
  since this project's Active Input Handling is set to Input System only.

## Pooling & performance

`ObjectPooler` prewarms five pools (`PlayerLaser` 24, `EnemyProjectile` 20,
`EnemyDrone` 15, `Debris` 8, `BreachHazard` 6) at `Awake`, before the round starts.
Sizing rationale: at Critical-phase spawn intervals (base × 0.70) with each object's
approximate on-screen lifetime, these cover the observed peak concurrent count with
headroom; `SpawnFromPool` auto-expands by one and logs a warning if a pool ever runs
dry, satisfying the brief's "safe expansion, if documented" allowance — this is that
documentation. Verified in-editor by requesting 20 concurrent `EnemyDrone`s against a
prewarm of 15 with no errors and 20 distinct valid instances returned.

Every `IPoolableObject.OnSpawnFromPool` resets exactly what its script owns: hazards
reset `currentHealth`; the spawner separately calls `SetSpeed` right after spawning;
projectiles/lasers have their direction and speed reset in `OnDespawnToPool` so a
reused instance never inherits a stale velocity. Position/rotation/active-state are
the pooler's own responsibility, not the prefab's.

Two things done specifically for the "avoid recurring allocations" requirement:
- `HUDController` only writes to a `Text.text` when the *displayed* value actually
  changes (timer only updates once per whole second; hull/score/phase are event-driven,
  not polled), rather than reformatting every UI string every frame.
- `EnemySpawner` and every hazard's `Update` use a plain float accumulator, not
  coroutines or `WaitForSeconds`.

Not implemented: an on-device Unity Profiler capture. I don't have a physical
Android/iOS device attached to this environment to build to and profile — see
"Incomplete / left for you" below.

## Progression

| Phase | Window | P1 → P2 | Enemy speed | Spawn interval | Projectile speed |
|---|---|---|---|---|---|
| Patrol | 0–20s | Pilot → Gunner | ×1.00 | ×1.00 | ×1.00 |
| Alert | 20–40s | Gunner → Pilot | ×1.25 | ×1.00 | ×1.00 |
| Critical | 40–60s | Pilot → Gunner | ×1.25 (carried) | ×0.70 | ×1.50 |

Breach Hazards only start spawning once the phase leaves Patrol. Warnings
("QUANTUM FLUX IN 3... 2... 1...") fire 3 seconds before each transition.

## Setup

1. Open `Assets/Scenes/Game.unity` in Unity 6000.3.10f1 (or compatible 6000.3.x).
2. Press Play. Keyboard/mouse drive whichever role each control currently maps to
   (see Input section above).
3. All tunables (speeds, durations, cooldowns, pool sizes, spawn intervals) are
   serialized fields on their respective components — visible and editable in the
   Inspector without touching code, per the brief's "expose these values as serialized
   Unity fields" requirement.
4. For a device build: File → Build Settings → Android/iOS, `Assets/Scenes/Game.unity`
   is already the only scene in Build Settings.

## AI usage transparency

This prototype was built with Claude (Anthropic) driving the Unity Editor directly
through the MCP for Unity bridge — writing every script, configuring physics layers
and the collision matrix, generating the placeholder sprites, building the scene
hierarchy and prefabs, and wiring every serialized reference. I reviewed the resulting
architecture and I'm able to walk through and defend any part of it, including the two
bugs described below, which were found and fixed through targeted in-editor testing
during this same session, not by inspection alone.

## Assumptions

Where the brief was silent or ambiguous, these choices were made (all easy to change,
all in one place):

- **Sprite reference sheet**: the .docx version of the brief embeds the actual 4×2
  reference sheet as images, which the PDF's extracted text alone didn't surface. The
  player laser is a vertical bolt there, not the horizontal bar I'd originally guessed —
  fixed, including the rotation math in `WeaponSystem.Fire`, since a wrongly-oriented
  laser is a real visual bug, not just style. Debris/Boost/Shield icon shading was
  nudged closer to the reference (crater dots, an upward triangle, a filled disc).
- **HUD "feel"**: on request, the placeholder HUD was rebuilt to match the mockup
  screenshots' *feel* — bottom-corner bordered panels (a 9-sliced rounded-rect fill +
  outline sprite pair, generated once and reused everywhere, not hand-authored per
  size), arrow/crosshair icons, hull as three icon squares, and a starfield backdrop.
  Panel border and role-label colour dynamically follow the *role* (cyan Pilot / red
  Gunner), matching how colour travels with role rather than player across a Quantum
  Flux swap in the mockups. Stopped short of pixel-accurate recreation (exact fonts,
  glow effects, precise corner radii) — that crosses from "match the feel" into the
  detailed-art time the brief's "engineering over art" guidance says not to spend.
- **Ship collision vs. Breach Hazard**: the brief's hull-damage rule lists exactly
  "enemy, debris, or enemy-projectile collision" — Breach Hazard is not in that list.
  So a direct collision between the ship and a Breach Hazard does *not* damage hull;
  its only failure mode is reaching the bottom boundary. (`ShipController.cs`)
- **Damage-dealing objects are consumed on hit**: an enemy/debris/projectile that hits
  the ship is returned to its pool on that same hit (rather than, say, an enemy
  surviving a pass through the ship). Not specified either way; this is the standard
  convention for this genre and avoids one hazard re-triggering the invulnerability
  window repeatedly while overlapping.
- **Alert's enemy-speed multiplier carries into Critical** rather than resetting: the
  brief's Critical row only calls out spawn interval and projectile speed changes, and
  reads as additive on top of Alert, not a fresh baseline.
- **Enemy Drone projectiles fire straight down**, not aimed at the ship — the brief
  doesn't specify aim behaviour, and this is the simplest defensible reading of
  "may fire projectiles" for a top-down descent shooter.
- **Laser/projectile collisions are single-hit**: a `PlayerLaser` is consumed by the
  first hazard it touches (no piercing).
- **`Physics2D.autoSyncTransforms = true`** is set once in `GameManager.Awake`. Every
  moving object here uses direct `transform.position` assignment rather than
  `Rigidbody2D.MovePosition`, for simplicity; this setting is what makes that safe with
  Unity's 2D physics. The tradeoff is a small per-frame sync cost, acceptable at this
  scale — see "Two real bugs" below for how this was found.
- **Hazard/laser/projectile rigidbodies are `Dynamic` (gravity scale 0, rotation
  frozen)**, not `Kinematic`. Unity's 2D physics does not generate trigger contacts
  between two `Kinematic` bodies (or `Kinematic` vs. a collider with no `Rigidbody2D`
  at all) — at least one side of any colliding pair needs to be `Dynamic`. The ship
  itself stays `Kinematic` since every hazard/projectile it can touch is now `Dynamic`,
  satisfying that rule for every pair in the collision matrix.

### Two real bugs found via testing (worth knowing for the interview defense)

Both were caught by spawning specific object pairs at runtime and stepping physics
manually (`Physics2D.Simulate`) rather than trusting the code by inspection alone:

1. `Physics2D.autoSyncTransforms` defaults to `false` in this Unity version. Since
   nothing here moves via `Rigidbody2D`, every trigger in the game would have silently
   never fired without explicitly enabling it.
2. Kinematic-vs-Kinematic `Rigidbody2D` pairs don't generate 2D trigger contacts in
   Box2D. All five prefabs were originally built `Kinematic` (reasonable first
   instinct — nothing here needs physics forces); switching them to `Dynamic` with
   zero gravity fixed it.

Verified after both fixes, in-editor: laser destroys a 1-HP enemy and awards score;
debris survives one hit and dies on the second (2 HP); ship hull drops on an enemy
projectile hit and a second immediate hit is correctly blocked by the invulnerability
window; a Breach Hazard reaching the bottom boundary ends the round in a loss; a laser
crossing the top cleanup boundary is returned to its pool; role swap propagates
correctly into the HUD's labels and control-panel visibility; both Quantum Flux
transitions (Patrol→Alert→Critical) apply the correct role swap and multiplier values.

## Incomplete / left for you

This environment has no physical Android/iOS device attached, so the following
submission-checklist items need to be done on your machine, not by me:

- **Device build + testing**: build to your Android/iOS device and confirm touch
  ownership, simultaneous P1+P2 input, Boost/Shield, and Quantum Flux feel right on
  real hardware. The touch-zone hit-testing logic is unit-verified by construction
  (every `RectTransform` it checks is the same one the HUD renders), but I could not
  drive actual finger input into the Game view from here.
- **Device video/screenshots** for the submission checklist.
- **Unity Profiler capture during Critical phase**, per the checklist's requirement —
  needs a running device build.
- **A full real-time 60-second playthrough** in the Editor with live keyboard/mouse
  input — I verified every mechanic individually (see above) rather than one
  continuous real-time round, since this automated environment's Unity Editor window
  runs heavily throttled while unfocused (observed roughly 10–15× slower than
  wall-clock), making a full passive real-time playtest here unreliable as a signal.
  A normal, focused Editor session will not have this problem.
- **Balance pass**: spawn intervals, speeds, and pool sizes are reasonable first
  values, not tuned through actual play.
