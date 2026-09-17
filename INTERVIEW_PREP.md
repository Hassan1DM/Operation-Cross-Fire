# Interview Preparation Guide — Operation Cross-Fire

You're ready to defend this prototype. This guide tells you exactly what to prepare and how to present it.

---

## Quick Stat Sheet (Memorize This)

- **Lines of Code**: ~2,500 (all gameplay, architecture, no menus)
- **Key Commits**: 24 total (all documented in git history)
- **Architecture**: 6 singletons (GameManager, RoleManager, ObjectPooler, MobileInputController, ShipController, ShieldSystem)
- **Physics Bugs Fixed**: 2 (autoSyncTransforms, Kinematic-vs-Kinematic)
- **Input Bugs Fixed**: 3 (Device Simulator priority, stale aim, Quantum Flux touch re-registration)
- **Enhancements**: 2 (default aim tracking, Boost visual feedback)
- **Design Decisions Made**: 9+ with explicit reasoning in code comments and DEVELOPMENT_LOG.md

---

## Pre-Interview (48 hours before)

### 1. **Build for Real Device** (Critical)
```bash
# Open Unity, go to File > Build Settings
# Select Android or iOS (whichever platform the hiring company uses)
# Configure project settings (bundle ID, etc.)
# Build to an APK or IPA
# Test on a real device (not Device Simulator)
```

**Why**: Simulators mask real-world touch input quirks. A real build proves it works on actual hardware.

### 2. **Capture a 60-Second Playthrough Video**
- Play one full round (60 seconds) on the real device
- Show both players' controls working
- Show at least one role swap (Quantum Flux at 20s or 40s)
- Show both Pilot movement and Gunner aiming/firing working
- Show Shield activation
- Show Boost (watch for the gold tint)
- Show the win screen (reach 60s with hull > 0)
- **Export as MP4 or MOV** (not screen recording artifacts)

**Where to save**: `INTERVIEW_PREP_VIDEO.mp4` in project root (or your cloud storage, ready to show live).

### 3. **Prepare Your Talking Points** (Read these aloud 3× each)

#### Point 1: The Quantum Flux Architecture (1 min)
> "The game's entire progression is driven by one round timer and one phase index. At 20 seconds and 40 seconds, a single method — `ExecuteFluxTransition` — runs five steps in order: cancel input, swap roles, apply difficulty multipliers, notify the UI, and show a banner. This is the **single most complex decision** I made, because the brief specifically said 'one state machine,' not separate timers, and I kept all five steps atomic in one place so they can't get out of sync."

**Where to point**: `GameManager.cs:111-134` (`ExecuteFluxTransition` method)

---

#### Point 2: Input Ownership & Touch Priority (1.5 min)
> "Input has a strict hierarchy. `MobileInputController` is the single source of truth — it polls keyboard/mouse in the Editor, and real touch input on device. Every other system reads *published state*, not raw input, so there's one clear owner. But here's the tricky part: the Device Simulator runs *inside* the Editor, so both `Keyboard.current` and `Touch.activeTouches` are populated at the same time, fighting over the same aim value every frame. I fixed this with an explicit per-side priority rule: if a real touch is driving that side's control, the editor input steps aside. This taught me that 'which system owns this value' has to be decided deliberately, not assumed from your build target."

**Where to point**: `MobileInputController.cs:95-144` (the per-side priority logic)

---

#### Point 3: The Quantum Flux Touch Bug (2 min)
> "The hardest-to-find bug: when roles swapped at 20/40 seconds, shooting direction would instantly go wrong — but only sometimes, randomly. Took me a while to realize: when `CancelAllInputs()` clears the touch dictionary, the player's *finger is still on screen*. Next frame, that same touch gets re-processed, but roles have swapped, so the same screen position is now in a *different* zone. A finger that was on 'Left' (Pilot movement) is now re-registered as 'AimArea' (Gunner aim). Wrong zone classification, corrupted aim. The fix: mark touches that were active during the swap, ignore them until they end, so they can't be misinterpreted in the new role context. This is why it was 'random' — it depended on which player had which finger down at the moment of transition."

**Where to point**: `MobileInputController.cs:107-118` (the ignore logic) and `DEVELOPMENT_LOG.md:§12`

---

#### Point 4: Physics Debugging (1 min)
> "I hit two physics bugs. First: `Physics2D.autoSyncTransforms` defaulting to false. Every moving object — ship, hazards, projectiles, lasers — moves via direct `transform.position` assignment. Without autoSyncTransforms enabled, those changes never reach the physics engine's internal state, silently breaking every trigger contact. One line fixed it. Second: Kinematic-vs-Kinematic rigidbodies don't generate trigger contacts in Box2D. Switched all five pooled object types from Kinematic to Dynamic (gravity scale 0), so every pair now has at least one Dynamic side. These aren't sophisticated fixes, but they're the kind of thing that makes you look *really* carefully at how the physics engine actually works under the hood."

**Where to point**: `GameManager.cs:71` (autoSyncTransforms) and commit `a1478f7`

---

#### Point 5: Performance & Pooling (1 min)
> "Zero-allocation object pooling with a cached interface reference dictionary. Every pooled object is fetched once during prewarm, stored in a `Dictionary<GameObject, IPoolableObject>`, so spawn/return never calls `GetComponent`. HUD text updates are change-gated — only write `Text.text` if the value actually changed, avoiding recurring string allocations in a hot loop. No LINQ in update loops. These aren't novel techniques, but they're the disciplined habits that keep a mobile game responsive even with 50+ active objects on screen."

**Where to point**: `ObjectPooler.cs` (pooling) and `HUDController.cs` (change-gated updates)

---

### 4. **Prepare 2-3 Code Changes You Can Make Live**

The interviewer will probably ask "make a small change live." These are your ready-to-go options:

**Option A: Change Boost speed**
- File: `ShipController.cs`, line 21
- Change `boostMultiplier = 1.75f` to `2f` (or `1.5f`)
- Recompile, play, see the ship move faster/slower during Boost
- Takes 30 seconds

**Option B: Change ship spawn position**
- File: `ShipController.cs:58`, line `Hull = maxHull`
- Add after: `transform.position = new Vector3(-5f, -7f, 0f);` (move ship left)
- Recompile, play, ship starts on the left side
- Takes 30 seconds

**Option C: Add a debug log to damage**
- File: `HazardBase.cs`, inside `ApplyDamage()` method
- Add: `Debug.Log($"Hazard damaged! Health: {currentHealth}");`
- Recompile, play, shoot enemies, watch console for debug output
- Takes 30 seconds

Pick one that feels natural to you. **Don't overthink it** — the point is to show you understand the code, not to impress with cleverness.

---

### 5. **Mock Interview with Yourself** (Do this)

Sit down. Open the code. Speak aloud:
1. "Walk me through the architecture" (use Point 1 script)
2. "Show me a bug you found and how you fixed it" (use Point 3 or 4)
3. "Make a small change" (pick Option A/B/C and do it live)
4. "Why did you choose pooling instead of Instantiate/Destroy?" (Answer: predictable memory, no frame-time spikes, required by brief)
5. "What would you do differently?" (Answer: real device profiler capture, Rigidbody2D.MovePosition instead of direct position assignment for cleaner physics)

**Time yourself.** Aim for 2-3 minutes per question.

---

## During the Interview

### Posture & Tone
- **Confident but honest**: "I debugged this by checking…" not "I knew this would happen."
- **Technical specificity**: name the actual class, method, or property (not "the ship script," but "ShipController")
- **Own your decisions**: "I chose X over Y because…" not "X seemed easier"
- **Show your work**: explain your debugging process, not just the fix

### What to Emphasize
1. **One state machine for Quantum Flux** — this is your strongest architectural decision
2. **Input ownership hierarchy** — shows you think about data flow
3. **The Quantum Flux touch bug** — a hard-won debugging win
4. **Physics bugs** — shows you read documentation and understand engine internals

### What NOT to Oversell
- Don't claim AI-generated code was hand-written (you already acknowledged it in the README)
- Don't pretend you knew everything from day one (you debugged things)
- Don't make up test cases or profiler numbers you didn't actually capture
- Don't blame the brief for scope; own your decisions

### If You Get Stuck
- "Let me look at the code to show you exactly what I mean" (opens IDE)
- "That's a good question — the reason I did it this way is…" (pause, think, answer)
- "I didn't get to that because [time/scope], but if I had more time, I'd…" (honest answer)

---

## After the Interview (Tie-Up)

### Stuff to Have Ready
1. **GitHub link** to your repo (if you pushed it)
2. **Device build** (APK or IPA) to hand over or demo
3. **This guide** printed (shows preparation)
4. **DEVELOPMENT_LOG.md** open in browser (shows documentation discipline)

### What They're Actually Evaluating
- **Problem-solving**: Did you find real bugs and fix them systematically?
- **Architecture**: Is the code organized in a way that makes sense?
- **Communication**: Can you explain your choices clearly?
- **Ownership**: Do you own both successes and trade-offs?

---

## Git History (Your Proof)

Run this before the interview:
```bash
git log --oneline
```

Show them the commit history. **24 commits tell a story** — you didn't write this in one sitting. Each commit is a logical step:
1. Core architecture (pooling, managers, input)
2. Physics layer and collision matrix
3. Sprite generation
4. Scene assembly and first physics bugs fixed
5. Quantum Flux logic
6. Mobile testing → UI redesign
7. Device Simulator testing → input priority fix
8. Live mobile testing → aim + Boost fixes
9. Live mobile testing → Quantum Flux touch bug
10. Documentation

**This is your evidence that you debug systematically, not randomly.**

---

## Final Checklist (Day-Of)

- [ ] Real device build tested and works
- [ ] 60-second playthrough video captured
- [ ] Talking points rehearsed (read aloud 3× each)
- [ ] Code changes (Options A/B/C) ready to go
- [ ] Mock interview completed
- [ ] DEVELOPMENT_LOG.md bookmarked
- [ ] README.md reviewed
- [ ] Git history reviewed (`git log --oneline`)
- [ ] Know the brief requirements by heart (60s, co-op, roles swap, etc.)
- [ ] Calm and ready

---

## The Mindset

You're not here to be perfect. You're here to show:
1. **You can debug** — you found real bugs and fixed them
2. **You can think architecturally** — the code is organized, not a mess
3. **You can communicate** — you explain your choices clearly
4. **You can learn** — you tested on device and found issues the simulator hid
5. **You own your work** — you can defend every decision

You have all of this. Go in confident. 🎯

---

Good luck! Let me know if you want to rehearse any of the talking points or need help with anything else.
