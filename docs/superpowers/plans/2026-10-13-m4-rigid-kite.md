# M4.2 Rigid kite on the 100Sixteen donor

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A 6-DOF rigid 100Sixteen that trims and hangs for 60 s on 45 m lines, with inertia and six sail panels taken from the donor layout.

**Architecture:** `Kitesim.Core` only. `HundredSixteenLayout` is the frame. `FrameInertia` builds mass, CoM and a diagonal inertia tensor from rods (line mass) plus sail lamina plus tail weight. `RigidKiteState` + `RigidKiteSim` integrate at 500 Hz. `PanelAero` turns six panel attitudes into force and moment. Lines stay rigid (distance constraint) until M4.3 XPBD. Gate: hang 60 s before any steering.

**Tech Stack:** C# / NUnit / `.verify-core` (`mise exec -- dotnet test .verify-core/Kitesim.Core.Verify.csproj`). Unity 6000.0.27f1 is not required until a later scene hook.

## Global Constraints

- `Kitesim.Core` has `"noEngineReferences": true`. Doubles only.
- Do not edit `WindowSim`. It stays the product trainer.
- Do not replace `KiteSim` until hang is proven.
- Donor numbers live in `HundredSixteenLayout`; do not invent rod masses beyond `CarbonMassPerMetre` from Nitro Strong 23 g / 0.81 m.
- T-piece = Center Cross = 0.597 m from nose sewing. Inhaul anchor = inner 0.637 m.
- Assembled spine = `Height` (1.015 m). Standoffs set panel dihedral; first hang may still use a flat sail.
- Commit prefix `M4.2:` when the owner asks to commit. No AI attribution.

---

## File map

| File | Role |
|---|---|
| `Assets/Scripts/Core/HundredSixteenLayout.cs` | Donor numbers and anchors |
| `Assets/Scripts/Core/FrameInertia.cs` | Mass, CoM, Ixx/Iyy/Izz from rods + sail + tail |
| `Assets/Scripts/Core/RigidKiteState.cs` | Position, velocity, quaternion, ω |
| `Assets/Scripts/Core/PanelAero.cs` | Six panels → force and moment about CoM |
| `Assets/Scripts/Core/RigidKiteSim.cs` | 2 ms substeps, gravity + aero + two rigid lines |
| `Assets/Tests/Editor/FrameInertiaTests.cs` | Tensor tests |
| `Assets/Tests/Editor/RigidKiteSimTests.cs` | Hang / trim tests |
| `docs/donors/100sixteen.md` | Locked numbers |

---

### Task 1: Lock donor numbers in layout tests

- [x] Extend `HundredSixteenLayoutTests` so `P200Stub` is 0.190 m, `TPiece` equals `CenterCross`, assembled spine equals `Height`, and the three-point bridle still closes on the inner anchor.
- [x] Run `mise exec -- dotnet test .verify-core/Kitesim.Core.Verify.csproj --filter HundredSixteenLayout`.
- [x] Confirm it fails if constants drift, then keep the layout as already filled in.

### Task 2: Inertia from the rod layout

- [x] Write `FrameInertiaTests` first: total mass 0.350 kg; CoM in the sail plane Y = 0 and between tail and nose; Ixx (pitch) much smaller than Iyy/Izz; tensor computed, not typed in.
- [x] Run the test, see it fail (type missing).
- [x] Add `FrameInertia` that treats each published rod as a thin line at `CarbonMassPerMetre`, the p200 stub the same way, standoffs at that density, tail 0.020 kg at the tail, remaining mass as a lamina on the four sail corners. Shift I to CoM. Diagonal in kite axes.
- [x] Run `.verify-core` until green.

### Task 3: RigidKiteState

- [x] Test: identity quaternion, zero ω, copy-in / copy-out of position.
- [x] Add the readonly/mutable state struct matching `WindowKiteState` style (fields the integrator needs: `Position`, `Velocity`, `Orientation`, `Omega`).
- [x] Green.

### Task 4: Hang on two rigid 45 m lines, gravity only

- [x] Test: kite 45 m downwind, nose up, no wind, two lines from pilot origin to the two towpoints; after 10 s it has not fallen through the ground and line lengths stay 45 m ± 1 cm.
- [x] Minimal `RigidKiteSim`: substep 2 ms, gravity, project two distance constraints, no aero yet.
- [x] Green.

### Task 5: PanelAero + wind hang 60 s

- [x] Six panels from layout (inner/outer wing split at standoff station, two keel). Dihedral from standoff lengths; first version may keep keel in the spine plane.
- [x] Test: 6 m/s wind, bridle as published, hangs 60 s above the ground with bounded pitch.
- [x] Do not start M4.3 (XPBD / walking / steering) until this passes.

### Task 6: Docs

- [x] Point `STATUS.md` at this plan and at the locked donor.
- [x] Do not switch the Window trainer over to `RigidKiteSim` in this plan.
