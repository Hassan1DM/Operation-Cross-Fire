# Development Log — Operation Cross-Fire

This is a chronological record of every task done on this prototype, in the order it
happened, with the reasoning behind each decision and — most importantly for the
interview — the full diagnostic trail for every bug that came up: what broke, how it
was noticed, what was tried, and why the fix works. `README.md` documents the
*finished* architecture; this document documents *how it got there*, so you can read
through it once and be able to reconstruct the reasoning live if asked.

Each section header names the git commit(s) it corresponds to, so you can cross-reference
`git log` and `git show <hash>` against this if you want to see the literal diff for
any step.

---

## 0. Reading the brief and catching a scope mismatch

Before writing any code, the actual exercise brief (the PDF, later cross-checked
against the .docx which contains the reference sprite sheet and mockups) was read in
full. The very first version of the request described a multi-level shooter with a
main menu and level-select screen. That directly contradicts the brief's own
"Out of scope" list: *"Networking, accounts, multiple levels, ... production menus,
tutorials, and save data."* Building what was originally asked for would have
violated the graded evaluation criteria and wasted timebox hours on exactly the things
the brief says not to spend time on.

**This was flagged explicitly before writing anything**, and the decision to follow the
literal brief (single 60-second round, no menus, no levels) was confirmed before
proceeding. **Interview point**: if asked "why doesn't this have a menu / levels?" —
that's not a missed requirement, it's the brief's explicit "Out of scope" list, and
the acceptance criteria never mention either.

A second, unrelated mismatch was also caught here: the GDD files had been placed in
`E:\Usman_Workspace\Operation Cross Fire\Assets\GDD`, which is actually a *different*,
much larger Unity project (a multi-project workspace folder containing many unrelated
games) than the actual target project at
`E:\Usman_Workspace\Operation Cross Fire\Operation Cross Fire\`. MCP for Unity was
also initially connected to that wrong outer project. Both were caught before any
code was written, by checking `Application.dataPath` against the expected path rather
than trusting the Editor's displayed session name.

---

## 1. `chore: initial Unity 6 project scaffold and GDD` (`b1453aa`)

Baseline commit. Git wasn't initialized in the project yet (submission checklist
requires a repo with commit history), so:
- `git init`, plus a Unity-standard `.gitignore` (`Library/`, `Temp/`, `obj/`, build
  artifacts, IDE files — none of that belongs in source control for a Unity project).
- Copied the GDD PDF and the "Atomic GDD" prompting document into the *correct*
  project's `Assets/GDD/` so the submission repo is self-contained.
- Local (repo-scoped, not global) git identity set, since none existed.

**Why this matters for defense**: the git history itself is graded ("regular
descriptive commits showing meaningful progress"), so each commit from here on was
scoped to one coherent unit of work with a message explaining *why*, not just *what*.

---

## 2. `feat: core gameplay architecture` (`63f0719`)

This is the biggest single step: all 22 C# scripts, written before touching the Unity
Editor at all, so the architecture could be reasoned about as a whole first.

### Design decisions and why

**Singleton pattern for managers** (`GameManager`, `RoleManager`, `ObjectPooler`,
`MobileInputController`, `ShipController`, `WeaponSystem`'s dependents): each of these
is scene-unique by construction (there's exactly one round, one set of roles, one
pool, one ship). A `static Instance` assigned in `Awake()` is the simplest correct
tool for "give every other script a way to reach this one object" at this scale. A
heavier ServiceLocator or dependency-injection framework would be over-engineering for
a single-scene, ~15-script prototype under a strict timebox — **this is a deliberate
scope call, worth stating outright if asked "why not DI?"**.

**Quantum Flux as one method, not scattered event handlers**: the brief is explicit —
*"This should be driven by one round timer and one phase transition — not separate
role-swap and Quantum Flux timers."* `GameManager.ExecuteFluxTransition(GamePhase)`
executes the brief's five steps in the brief's own order, in one place:
```
1. Cancel active input       -> MobileInputController.Instance.CancelAllInputs()
2. Swap roles                -> RoleManager.Instance.SwapRoles()
3. Apply new phase values    -> ApplyPhaseMultipliers(newPhase)
4. Notify listeners          -> OnPhaseChanged event (HUD updates labels/panels)
5. Banner                    -> OnFluxBanner event
```
This is the single most likely thing to be asked about live ("walk me through what
happens at the 20-second mark") — the answer is literally this one method, in order.

**Input ownership**: `MobileInputController` is the *only* script that reads raw
input (`Keyboard`/`Mouse` in editor, `Touch.activeTouches` on device). Every other
script (`ShipController`, `WeaponSystem`, `ShieldSystem`) only ever reads *published
state* from it (`PilotMoveAxis`, `GunnerAimWorldPosition`, `TryConsumeBoost()`, etc.).
This is a one-way data flow, not an event mesh — deliberate, because it makes "who
owns this input" a one-sentence answer instead of a tracing exercise.

The touch-ownership model specifically:
- Player 1 = left half of the screen, Player 2 = right half, **permanently** — this
  never changes.
- What each half *controls* (Pilot buttons vs Gunner aim/fire/shield) depends on that
  player's *current role*, looked up from `RoleManager` **once, at the moment a touch
  begins** (`RegisterTouch`), and cached against that touch's `touchId` for its entire
  lifetime. This is why a finger crossing the centre line mid-drag doesn't reassign
  ownership — the brief requires this explicitly, and it falls out naturally from
  resolving ownership once at touch-down rather than re-checking every frame.
- Held-state (movement axis, Fire) is *recomputed* every frame from whichever touches
  are still in the dictionary (`RecomputeHeldStateFromTouches`), not incremented/
  decremented on press/release events. This is why "holding both Left and Right stops
  movement" and multi-finger Fire both just fall out of the logic for free, instead of
  needing special-cased bookkeeping.

**Pooling**: `ObjectPooler` is a `Dictionary<PoolObjectType, Queue<GameObject>>`,
built once at `Awake`/prewarm. The one performance detail worth stating explicitly if
asked "how do you avoid GetComponent calls at runtime": every pooled instance's
`IPoolableObject` reference is fetched **once**, during prewarm, and cached in a
second dictionary keyed by the `GameObject` itself. `SpawnFromPool`/`ReturnToPool`
never call `GetComponent` — they're dictionary lookups by object reference.

**Pooling lifecycle** (this is explicitly named in the brief's interview-defense
note, so know it cold):
1. `Awake()` → `PrewarmAll()` instantiates `prewarmSize` inactive copies per type,
   parented under a `Pool_<Type>` container in the hierarchy (so the whole pool is
   inspectable by hand, not invisible).
2. `SpawnFromPool(type, pos, rot)` → dequeue (or expand-by-one + warn if empty) →
   position/rotate → `SetActive(true)` → call `OnSpawnFromPool()` on the object,
   which is where *that object's own script* resets its own state (health, timers).
   The pooler itself only ever touches transform + active state; it never knows what
   "health" or "velocity" mean for a given object type — that's each script's job via
   the `IPoolableObject` contract.
3. `ReturnToPool(type, obj)` → `OnDespawnToPool()` (reset velocity, direction) →
   `SetActive(false)` → re-parent under its pool container → enqueue.

### The reasoning that ended up mattering most: enemy/debris/breach hazard shared code

`HazardBase` is an abstract class implementing `IPoolableObject`, holding health/score/
movement/laser-hit logic shared by `EnemyDrone`, `Debris`, `BreachHazard`. Each
concrete subclass only supplies `PoolType` and, for `EnemyDrone`, the extra
projectile-firing behaviour. This is a straightforward "put the shared 90% in a base
class" call — worth mentioning as the reason there isn't near-duplicate collision/
health code three times over.

---

## 3. `feat: physics layers, 2D collision matrix, placeholder sprites` (`f3091dc`)

### Layers

Six custom layers were added (`manage_editor`'s `add_layer`, not hand-edited YAML):
`PlayerShip`, `PlayerLaser`, `EnemyHazard`, `EnemyLaser`, `BottomBoundary`,
`CleanupBoundary`. Note this is **six**, not the five sketched in the "Atomic GDD"
prompt document — a `CleanupBoundary` layer was added beyond that sketch because the
five-layer version had no way to catch a laser that flies off the top of the screen
without hitting anything (see the collision matrix reasoning below).

### Collision matrix — the actual reasoning, not just the table

The brief says: *"configure the physics collision matrix to exclude impossible
interactions."* The matrix was derived pair-by-pair from **what needs to detect
what**, not copied blind:

| Pair | Collides? | Why |
|---|---|---|
| PlayerShip ↔ EnemyHazard | Yes | ship takes damage from enemy/debris/breach bodies |
| PlayerShip ↔ EnemyLaser | Yes | ship takes damage from enemy projectiles |
| PlayerLaser ↔ EnemyHazard | Yes | laser damages enemies/debris/breach |
| PlayerLaser ↔ CleanupBoundary | Yes | laser flying off-screen gets pooled back |
| EnemyHazard ↔ BottomBoundary | Yes | breach-loss check / escaped-hazard cleanup |
| EnemyLaser ↔ CleanupBoundary | Yes | stray projectile gets pooled back |
| everything else among these six | No | never a meaningful interaction |

**If asked "why six layers and not five"**: the projectile/laser layers need *some*
boundary to hit when they miss and fly off-screen, and giving that job to
`BottomBoundary` would mean it also needs to react correctly to lasers (which it
shouldn't — `BottomBoundary`'s only job is the breach-loss check + returning escaped
*hazards*, not projectiles). Splitting that into a dedicated `CleanupBoundary` layer
keeps each boundary's script doing exactly one thing.

### Placeholder sprites (first pass)

Nine sprites (ship, enemy, debris, breach hazard, player laser, enemy projectile,
Boost icon, Shield icon, shield ring) were generated as **baked PNG files at edit
time** — a small one-off Editor script drew flat shapes pixel-by-pixel with
`Texture2D.SetPixel`/`EncodeToPNG`, wrote them to `Assets/Sprites/`, and they were
imported as ordinary Sprite assets. This matters for one specific requirement: *no
sprite is created at runtime* — these are static assets sitting in the Project window
like any hand-drawn ones, satisfying "everything should be in the hierarchy/visible
for hand-editing" as much as is possible while still meeting the brief's own
"basic shapes... sufficient" guidance.

(This first pass got some shapes wrong relative to the *actual* reference sheet — see
section 7. Worth knowing the sequence if asked "did you look at the reference sprites
first" — honestly, not fully; that was caught and fixed later.)

---

## 4. `feat: scene assembly...; fix two physics bugs` (`a1478f7`)

This is where the scene got built (camera, ship + children, five prefabs, managers,
boundaries, full HUD canvas) and — more importantly for the interview — **where two
real, non-obvious Unity physics bugs were found and fixed**. This is the strongest
material for "walk me through a bug you hit and how you debugged it."

### Bug 1: `Physics2D.autoSyncTransforms` defaults to `false`

**Symptom**: spawned a laser directly on top of an enemy (same position, guaranteed
overlap by geometry) and nothing happened — no `OnTriggerEnter2D`, no damage, no score.

**Diagnosis process**:
1. First suspected the reactivated-collider-needs-a-frame theory (a real thing in
   Unity, but not the actual cause here) — ruled out by testing with `Physics2D.
   Simulate()` stepped manually across several iterations; still nothing.
2. Checked `Collider2D.bounds` on both objects — they correctly reported the new
   overlapping position. This was the misleading part: `.bounds` is a **managed-side**
   geometric computation from the Transform, so it looked right regardless of whether
   the physics engine's own internal state had actually been updated.
3. Checked `Collider2D.IsTouching()` (a call that *does* query the native physics
   engine, not the managed Transform) — it returned `false`, despite `.bounds`
   overlapping. That mismatch was the actual signal: the *physics engine's* copy of
   the object's position was stale.
4. Checked `Physics2D.autoSyncTransforms` directly: `false`. That's the setting that
   controls whether changing `transform.position` in script automatically pushes into
   the physics engine's internal body state each frame. Every moving object in this
   project (`HazardBase.Update`, `PlayerLaser.Update`, `EnemyProjectile.Update`,
   `ShipController`'s movement) moves via **direct `transform.position` assignment**,
   not `Rigidbody2D.MovePosition()` — so with this flag off, none of those position
   changes were ever reaching the physics engine at all. Everything was colliding
   against stale positions from whenever each object was last spawned.

**Fix**: one line, `Physics2D.autoSyncTransforms = true;` in `GameManager.Awake()`,
with a comment explaining exactly this tradeoff (a small per-frame sync cost, traded
for the simplicity of not rewriting every movement script to use `MovePosition`).

**If asked "why not just use `Rigidbody2D.MovePosition` everywhere instead"**: that's
the more idiomatic fix and the one worth naming as the alternative — it was not done
here purely because of the time already invested in the simpler Transform-based
movement code, and because the one-line global fix works correctly at this project's
scale. Good, honest answer if pushed on it.

### Bug 2: Kinematic-vs-Kinematic `Rigidbody2D` pairs don't generate 2D trigger contacts

**Symptom**: after fixing Bug 1, *still* no collision. Score stayed 0.

**Diagnosis process**:
1. With autoSync now on, re-checked `IsTouching()` between a laser and an enemy at the
   same position, after several manually-stepped physics simulations. Still `false`.
2. Isolated variables one at a time: confirmed layers weren't ignoring each other
   (`Physics2D.GetIgnoreLayerCollision` → `false`, i.e. they *should* collide),
   confirmed both colliders were triggers, confirmed both had a `Rigidbody2D`.
3. Tested a controlled experiment: manually changed *one* of the two objects'
   `Rigidbody2D.bodyType` from `Kinematic` to `Dynamic` (gravity scale 0) at runtime,
   left the other as `Kinematic`, and re-ran the same overlap test. It worked
   immediately.

**Root cause**: Unity's 2D physics (Box2D under the hood) only generates contacts —
including trigger contacts — when **at least one** of the two colliding bodies is
`Dynamic`. Two `Kinematic` bodies never generate a contact with each other, and
neither does a `Kinematic` body against a collider with no `Rigidbody2D` at all
(effectively `Static`). Every object in this project (ship, five pooled prefab types)
had originally been built `Kinematic` — a reasonable first instinct, since nothing
here needs real physics forces — but that instinct is what broke it.

**Fix**: `EnemyDrone`, `Debris`, `BreachHazard`, `EnemyProjectile`, `PlayerLaser`
prefabs switched to `Dynamic` bodies (gravity scale 0, rotation frozen). The ship
stays `Kinematic`, since every hazard/projectile it can touch is now `Dynamic`,
satisfying the "at least one side Dynamic" rule for every pair without needing the
ship itself to change.

**If asked "why is the ship still Kinematic and not Dynamic like everything else"**:
because it doesn't need to be — the rule only needs *one* side of a pair to be
Dynamic, and making the hazards Dynamic already covers every pair the ship
participates in. Keeping the ship Kinematic is also more semantically correct for a
player-controlled body that's moved by script rather than by physics forces.

### Verification performed after both fixes (worth naming specifically if asked "how did you verify this")

Rather than trusting the fix by inspection, each of these was exercised directly at
runtime via Unity's `execute_code` scripting bridge, stepping physics manually with
`Physics2D.Simulate()` so results didn't depend on real-time frame delivery:
- Laser destroys a 1-HP enemy, awards +10 score.
- Debris survives exactly one hit (2 HP), dies on the second.
- Enemy projectile damages ship hull; a second immediate hit is correctly blocked by
  the invulnerability window (hull unchanged, but the second projectile is still
  consumed/pooled).
- Breach Hazard reaching `BottomBoundary` ends the round in a loss.
- Laser crossing `CleanupBoundary` is returned to its pool.
- `RoleManager.SwapRoles()` correctly propagates into the HUD's labels and
  control-panel visibility.
- Both Quantum Flux transitions invoked directly via reflection on
  `GameManager.ExecuteFluxTransition` (since waiting 20 real seconds in this
  automated environment wasn't reliable — see the note on editor throttling below):
  Patrol→Alert swaps roles and sets enemy-speed ×1.25; Alert→Critical swaps roles back
  and sets spawn-interval ×0.70 / projectile-speed ×1.50 while enemy-speed *stays*
  ×1.25 (confirms the "Alert's multiplier carries into Critical" assumption is what
  the code actually does, not just what the README claims).
- Pool safe-expansion: requested 20 concurrent `EnemyDrone`s against a prewarm size of
  15 — all 20 spawned as distinct, valid, active instances, no errors.

A note on *why* physics was stepped manually instead of just pressing Play and
waiting: this specific automated session runs the Unity Editor fully unfocused, which
throttles its internal frame rate severely (observed roughly 10-15× slower than
wall-clock, and in the worst case a `ScreenCapture.CaptureScreenshot` call didn't
actually flush to disk until *play mode was stopped*, forcing a delayed frame). None
of this is a property of the game — a normal, focused Editor session runs at full
speed. It just meant the fastest reliable way to test gameplay logic here was to
manipulate state directly and force physics steps, rather than trust wall-clock
waiting.

---

## 5. `docs: submission README` (`17723f8`)

First pass at `README.md` — architecture table, input ownership, pooling strategy,
progression table, setup instructions, AI-usage transparency, and an explicit
Assumptions section for every place the brief was ambiguous (Breach Hazard not
damaging the ship directly, damage-dealing objects being consumed on hit, Alert's
speed multiplier carrying into Critical, enemy projectiles firing straight down,
lasers being single-hit). Also honestly lists what can't be done from this
environment: a physical device build/test, the Profiler capture, and the submission
video.

---

## 6. `fix: align sprites/UI to the official reference sheet...` (`8331e26`, `78a1f40`)

A second GDD document — the `.docx` version of the same brief — was pointed out as
already present in the project (it had a `.meta` file, meaning it had been added at
some point but never opened during the first GDD review). Unlike the PDF, this file's
raw XML could be unzipped to pull out its **embedded images directly** — the actual
4×2 sprite reference sheet and several HUD mockup screenshots that the PDF's
text-only extraction never surfaced.

Comparing against that sheet caught one **real** bug (not cosmetic): the reference
shows the player laser as a **vertical** bolt; the first-pass sprite (section 3) had
been built as a **horizontal** bar, based on a guess rather than the actual asset.
Since `WeaponSystem.Fire()` computes rotation from `Mathf.Atan2(dir.y, dir.x)`, which
assumes the sprite's *default* art points along +X (right), a sprite that actually
points +Y (up) needs a **-90° offset** applied to that angle, or every laser would
visually point 90° off from its true travel direction.

**Fix verified numerically**, not just by eye: for five test directions (up, down,
left, right, diagonal), computed `Quaternion.Euler(0,0, atan2(dir)*Rad2Deg - 90) *
Vector3.up` and confirmed it equals the intended direction to ~1e-7 floating-point
error in every case.

Also updated from the same reference: debris got a couple of darker "crater" dots,
the Boost icon changed from a plain circle to an upward triangle, the Shield icon
from a ring to a filled disc — all cheap (a few lines each, reusing existing pixel-draw
helpers), explicitly stopping short of pixel-accurate art since the brief's
"engineering over art" guidance cuts both ways: it justifies *not* over-polishing just
as much as it justifies fixing an actually-wrong sprite orientation.

The mockup images also confirmed the HUD architecture was already conceptually
correct — Player 1's panel stays on the left, Player 2's stays on the right, and only
the *contents* (and, per the mockup, the *colour*) swap with role. That prompted
changing the zone background tints from being fixed-per-player to being
fixed-per-role (Pilot zones always cyan-tinted, the Gunner aim zone always
red-tinted, regardless of which physical player currently occupies that role), plus
making the role-label *text colour* swap dynamically in `HandleRolesSwapped` —
verified live by calling `RoleManager.SwapRoles()` and reading the resulting
`Text.color` back.

**If asked "did the mockup match the written spec exactly"**: no, and that's worth
knowing — one mockup frame shows a banner reading "ROLES SWAPPED", but both the PDF
and the .docx's *written* text consistently say *"Briefly display QUANTUM FLUX —
ROLES REVERSED."* The written spec was trusted over the mockup image since it's the
more authoritative, consistent source across both documents — a good example of
resolving conflicting reference material by weighting the literal requirement text
over illustrative concept art.

---

## 7. `feat: match HUD feel to the reference mockup...` (`7fe0b23`, `d550300`)

Explicit follow-up request: make the placeholder HUD's *feel* match the mockup
screenshots more closely (not just the individual sprites, the overall layout/style).

New assets generated (all baked sprites, same "draw once at edit time" approach as
section 3): a 9-sliced rounded-rectangle **fill** sprite and a matching **outline**
sprite (one shape, reused everywhere via `Image.type = Sliced` and tinted per-use via
`Image.color`, rather than hand-drawing a separate bordered sprite for every button
size), left/right arrow icons, a crosshair reticle icon, and a starfield background
texture.

The zone `RectTransform`s were **reparented and resized in place** rather than
rebuilt from scratch — deliberately, so `MobileInputController`'s existing serialized
references (which point to these objects directly, not by path) would keep working
without needing to be rewired. Object references in Unity survive reparenting; that
fact was relied on explicitly here.

Hull display changed from text-only to three tintable icon squares alongside the
existing text (kept both, for redundancy). Panel border colour and role-label colour
both now dynamically follow role via two new `HUDController` fields
(`p1FrameOutline`/`p2FrameOutline`).

### Two more real bugs, both caught by testing the result rather than trusting the script

**Bug 3 — orphaned GameObjects from an incomplete `Transform.Find` path.** An earlier
step used `canvas.Find("P1_PanelFrame")` — a single path segment with no `/`. `Transform.
Find` only searches **direct children** for a bare name; `P1_PanelFrame` is actually
nested two levels down (`P1_ControlHalf/P1_PanelFrame`), so the call silently returned
`null`. The next line, `zone.SetParent(null, false)`, does not throw on a null parent
— it just detaches the object to become a **scene root**. Six objects (`P1`/`P2`'s
`AimArea`, `FireZone`, `ShieldZone`, plus `P1`/`P2`'s `BoostFill`) ended up sitting as
stray root-level GameObjects instead of inside the HUD hierarchy, discovered by
listing `scene.GetRootGameObjects()` and finding six names that shouldn't be there.
**Fixed** by locating each by `GameObject.Find` (safe here since their names are
unique in the scene) and re-parenting them into the correct group with **explicit,
full paths** this time — and by then verifying every one of `MobileInputController`'s
twelve zone references was still non-null and pointed at the right object, since the
whole point of reparenting-in-place was to *not* need to touch that wiring.

**Bug 4 — `foreach` + `DestroyImmediate` skips items.** A later screenshot showed what
looked like a duplicate "SHIELD" label. Root cause:
```csharp
foreach (Transform child in zone) UnityEngine.Object.DestroyImmediate(child.gameObject);
```
`Transform`'s enumerator walks the hierarchy **live** by index. Destroying a child
mid-iteration shifts every subsequent sibling's index down by one, so the enumerator's
internal cursor skips whatever child now occupies the index it's about to visit next.
Two of the six re-skinned zones (`BoostZone`, `ShieldZone`) originally had one extra
child compared to the other four (their old text label sat alongside other content),
which is exactly the shape of hierarchy where this bug bites — one leftover child
survived the cull in each. **Confirmed** by listing each zone's actual children and
finding `"Label"` appearing twice in exactly those two zones; **fixed** by removing
only the correct stray instance (the original, first-added one — the correctly
re-positioned replacement is always the last child added) and re-verifying all twelve
zones show a clean, non-duplicated child list.

**If asked "what's the general lesson from bugs 3 and 4"**: both come from the same
category of mistake — trusting that a Unity API call succeeded because it didn't
throw, when Unity's convention is often to silently no-op or return null rather than
raise an exception (`Find` returning null, `SetParent(null)` succeeding quietly,
`DestroyImmediate` not caring that you're mutating the collection you're iterating).
The fix in both cases was the same habit: after any bulk hierarchy operation, list the
actual resulting state and check it against what was intended, rather than trusting
the operation's return value (or lack of an exception) alone.

---

## 8. `fix: remove stray leftover Label children...` (`ed76519`)

The direct fix for Bug 4 above — see that section for the full diagnosis. Verified via
`scene.isDirty`/root-object-count checks that no other stray objects existed anywhere
else in the hierarchy before committing.

---

## Anticipated interview questions and where to point

- **"Walk me through the architecture."** Start from `GameManager` as the orchestrator
  (round timer, phase index, win/loss), name the other four manager singletons and
  what each owns, then name the one-way data flow for input
  (`MobileInputController` → everyone else reads, nothing writes back). See §2.
- **"Explain input ownership."** Player-to-screen-half mapping is fixed; role-to-
  control-scheme mapping is dynamic and resolved once per touch at touch-down. See §2.
- **"Explain role switching."** One method, `GameManager.ExecuteFluxTransition`,
  five ordered steps taken straight from the brief's own text. See §2.
- **"Explain the pooling lifecycle."** Prewarm → Spawn (dequeue/expand, reposition,
  `OnSpawnFromPool`) → Return (`OnDespawnToPool`, deactivate, re-parent, enqueue).
  Cached `IPoolableObject` references avoid `GetComponent` at runtime. See §2.
- **"Show me a bug you hit and how you fixed it."** Either physics bug in §4 is strong
  material — both have a full symptom → hypothesis → test → root cause → fix →
  verification trail, not just "it didn't work, then it did."
- **"Make a small live change."** Good candidates, all cheap and self-contained:
  changing a serialized tunable (`ShipController.moveSpeed`, `WeaponSystem.
  fireCooldown`, any pool's `prewarmSize`) in the Inspector; changing
  `GameManager.alertEnemySpeedMultiplier`; swapping which zone colour constant
  `HUDController.PilotColor`/`GunnerColor` uses; adding a `Debug.Log` inside
  `HazardBase.ApplyDamage` to show live health values. All are one-line, all are in
  scripts you now know the reasoning behind.
- **"What would you do differently with more time?"** Honest answer, consistent with
  what's *not* claimed as done anywhere else in this repo: switch movement from direct
  `transform.position` assignment to `Rigidbody2D.MovePosition` (the more idiomatic
  fix behind Bug 1, not taken here for time reasons); a real device build and Profiler
  capture, which this environment cannot produce.
