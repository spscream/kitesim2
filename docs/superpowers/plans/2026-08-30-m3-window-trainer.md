# M3: 2D Wind-Window Trainer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a 2D wind-window trainer where up to four gamepad pilots fly kites on a projection of the wind window, with walking, recording and playback.

**Architecture:** A reduced kite model (`WindowSim`) integrates the kite as a point on the sphere of radius `LineLength` around its pilot, carrying a unit direction vector and a unit tangent heading vector. Heading turns by a steering-law rate plus a derived gravity term; position advances along a great circle, which parallel-transports the heading. An azimuthal projection (`WindowChart`) maps the hemisphere onto a half-disc where iso-power contours are concentric circles. All physics lives in `Kitesim.Core` with no Unity dependency and is tested headlessly.

**Tech Stack:** C# (.NET 10 for headless tests, Unity 6000.0.27f1 for the scene), NUnit, Unity Input System.

**Spec:** `docs/superpowers/specs/2026-08-30-kite-simulation-design.md`
**Domain reference:** `docs/kite-anatomy.md`

## Global Constraints

- `Kitesim.Core` must not reference `UnityEngine`. Its asmdef has `"noEngineReferences": true`. Everything in `Assets/Scripts/Core/` obeys this.
- All physics uses `double`, never `float`.
- New core files go under `Assets/Scripts/Core/`. They are picked up automatically by the `Kitesim.Core` asmdef and by `.verify-core/Kitesim.Core.Verify.csproj` (which globs `..\Assets\Scripts\Core\**\*.cs`). No project file edits needed.
- New core tests go under `Assets/Tests/Editor/`. Same globbing applies.
- Headless test command, run from the repo root: `cd .verify-core && dotnet test`. Baseline before this plan: **10 tests, all passing, ~20 ms**.
- Existing `KiteSim`, `KiteState`, `KiteConfiguration` and their tests are **not touched in M3**. They are replaced in M4.
- Coordinate conventions (Unity left-handed): `+X` right, `+Y` up, `+Z` forward. `Vec3.Forward = (0,0,1)`, `Vec3.Up = (0,1,0)`. `WindField.Direction` is where the wind blows **to**; the default is `(0,0,1)`. The pilot stands at the origin facing downwind, so `+X` is the pilot's right.
- Heading angle ψ is measured from the tangent-plane "up" direction toward the tangent-plane "right" direction: ψ=0 is toward the zenith, ψ=90° is toward the pilot's right.
- Line length is 45.0 m.
- Field half-size is 45.0 m (the 90 × 90 m outer boundary for individual dual-line, ISKRB §IV.D.1).
- Commit message prefix for this milestone: `M3.<n>: <description>`. **No AI attribution in commit messages.**

---

## File Structure

**Core (no Unity):**

| File | Responsibility |
|---|---|
| `Assets/Scripts/Core/Math/MathD.cs` | *modify* — add `Tan`, `Atan`, `Asin` |
| `Assets/Scripts/Core/WindowChart.cs` | Azimuthal projection of the window hemisphere; `ChartPoint`, `ChartProjection` |
| `Assets/Scripts/Core/KiteStateView.cs` | Model-independent kite snapshot consumed by rendering, recording and cross-model comparison |
| `Assets/Scripts/Core/WindowConfiguration.cs` | Tunables for the reduced model |
| `Assets/Scripts/Core/WindowKiteState.cs` | Mutable state of one kite in the reduced model |
| `Assets/Scripts/Core/WindowSim.cs` | The reduced model: wind, walking, steering, gravity turn, great-circle motion, ground |
| `Assets/Scripts/Core/Track.cs` | In-memory recording and interpolated playback of `KiteStateView` |

**Core tests:**

| File | Covers |
|---|---|
| `Assets/Tests/Editor/WindowChartTests.cs` | Task 2 |
| `Assets/Tests/Editor/KiteStateViewTests.cs` | Task 3 |
| `Assets/Tests/Editor/WindowSimTests.cs` | Tasks 4–7 |
| `Assets/Tests/Editor/TrackTests.cs` | Task 8 |

**Unity layer:**

| File | Responsibility |
|---|---|
| `Assets/kitesim.inputactions` | *modify* — add `Walk`, split into a per-player map |
| `Assets/Scripts/Unity/WindowKiteBehaviour.cs` | Drives one `WindowSim` in `FixedUpdate`, exposes `KiteStateView` |
| `Assets/Scripts/Unity/WindowPilotInput.cs` | Per-player input: two hands plus walk |
| `Assets/Scripts/Unity/WindowChartRenderer.cs` | Draws the half-disc, power rings, ground line, boundary arc |
| `Assets/Scripts/Unity/WindowKiteIcon.cs` | Positions and orients one kite marker from a `KiteStateView` |
| `Assets/Scripts/Unity/WindowFieldStrip.cs` | Top-down field with pilot markers and the rulebook boundaries |
| `Assets/Scripts/Unity/WindowTrackRecorder.cs` | Record/playback wiring and file I/O |
| `Assets/Scripts/Unity/WindowSceneBootstrap.cs` | Assembles the 2D scene |

---

## Task 1: Extend MathD

**Files:**
- Modify: `Assets/Scripts/Core/Math/MathD.cs`
- Test: `Assets/Tests/Editor/WindowChartTests.cs` (created here, used from Task 2 on)

**Interfaces:**
- Consumes: nothing
- Produces: `MathD.Tan(double) -> double`, `MathD.Atan(double) -> double`, `MathD.Asin(double) -> double`

- [ ] **Step 1: Write the failing test**

Create `Assets/Tests/Editor/WindowChartTests.cs`:

```csharp
using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class WindowChartTests
    {
        [Test]
        public void MathD_HasInverseTrigonometry()
        {
            Assert.AreEqual(1.0, MathD.Tan(MathD.PI * 0.25), 1e-12);
            Assert.AreEqual(MathD.PI * 0.25, MathD.Atan(1.0), 1e-12);
            Assert.AreEqual(MathD.PI * 0.5, MathD.Asin(1.0), 1e-12);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd .verify-core && dotnet test --filter MathD_HasInverseTrigonometry`
Expected: build error, `'MathD' does not contain a definition for 'Tan'`

- [ ] **Step 3: Write minimal implementation**

In `Assets/Scripts/Core/Math/MathD.cs`, add these three lines directly after the existing `public static double Acos(double value) => System.Math.Acos(value);`:

```csharp
        public static double Asin(double value) => System.Math.Asin(value);
        public static double Tan(double radians) => System.Math.Tan(radians);
        public static double Atan(double value) => System.Math.Atan(value);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd .verify-core && dotnet test --filter MathD_HasInverseTrigonometry`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/Math/MathD.cs Assets/Tests/Editor/WindowChartTests.cs
git commit -m "M3.1: add Asin, Tan and Atan to MathD"
```

---

## Task 2: WindowChart projection

The projection centre is the horizontal downwind direction `C`. For any unit direction `p` from pilot to kite, `Dot(p, C) = cos β · cos φ = cos θ`, so the effective wind is `W · cos θ` and depends only on the angular distance from the centre. Any azimuthal projection therefore has concentric iso-power circles. See `docs/kite-anatomy.md` §3 for the derivation.

**Files:**
- Create: `Assets/Scripts/Core/WindowChart.cs`
- Test: `Assets/Tests/Editor/WindowChartTests.cs` (modify)

**Interfaces:**
- Consumes: `Vec3`, `MathD`, `WindField` from `Kitesim.Core`
- Produces:
  - `struct ChartPoint { double X; double Y; ChartPoint(double x, double y); }`
  - `enum ChartProjection { Equidistant, Stereographic }`
  - `WindowChart.Center(WindField) -> Vec3`
  - `WindowChart.Right(WindField) -> Vec3`
  - `WindowChart.RadiusFromTheta(double, ChartProjection) -> double`
  - `WindowChart.ThetaFromRadius(double, ChartProjection) -> double`
  - `WindowChart.EdgeRadius(ChartProjection) -> double`
  - `WindowChart.PowerAtRadius(double, ChartProjection) -> double`
  - `WindowChart.Project(Vec3, WindField, ChartProjection) -> ChartPoint`
  - `WindowChart.Unproject(ChartPoint, WindField, ChartProjection) -> Vec3`

- [ ] **Step 1: Write the failing tests**

Replace the body of `Assets/Tests/Editor/WindowChartTests.cs` with:

```csharp
using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class WindowChartTests
    {
        private static WindField DefaultWind()
        {
            return new WindField { Speed = 5.0, Direction = Vec3.Forward };
        }

        [Test]
        public void MathD_HasInverseTrigonometry()
        {
            Assert.AreEqual(1.0, MathD.Tan(MathD.PI * 0.25), 1e-12);
            Assert.AreEqual(MathD.PI * 0.25, MathD.Atan(1.0), 1e-12);
            Assert.AreEqual(MathD.PI * 0.5, MathD.Asin(1.0), 1e-12);
        }

        [Test]
        public void Center_IsHorizontalDownwind()
        {
            Vec3 c = WindowChart.Center(DefaultWind());
            Assert.AreEqual(0.0, c.X, 1e-12);
            Assert.AreEqual(0.0, c.Y, 1e-12);
            Assert.AreEqual(1.0, c.Z, 1e-12);
        }

        [Test]
        public void Right_IsPilotsRight()
        {
            Vec3 r = WindowChart.Right(DefaultWind());
            Assert.AreEqual(1.0, r.X, 1e-12);
            Assert.AreEqual(0.0, r.Y, 1e-12);
            Assert.AreEqual(0.0, r.Z, 1e-12);
        }

        [Test]
        public void WindowCentre_ProjectsToOrigin()
        {
            ChartPoint p = WindowChart.Project(Vec3.Forward, DefaultWind(), ChartProjection.Equidistant);
            Assert.AreEqual(0.0, p.X, 1e-12);
            Assert.AreEqual(0.0, p.Y, 1e-12);
        }

        [Test]
        public void Zenith_ProjectsToTopOfArc()
        {
            ChartPoint p = WindowChart.Project(Vec3.Up, DefaultWind(), ChartProjection.Equidistant);
            Assert.AreEqual(0.0, p.X, 1e-12);
            Assert.AreEqual(MathD.PI * 0.5, p.Y, 1e-12);
        }

        [Test]
        public void GroundIsTheHorizontalDiameter()
        {
            // Any direction with zero elevation must land on the chart x-axis.
            for (int degrees = -80; degrees <= 80; degrees += 20)
            {
                double phi = degrees * MathD.Deg2Rad;
                Vec3 direction = new Vec3(MathD.Sin(phi), 0.0, MathD.Cos(phi));
                ChartPoint p = WindowChart.Project(direction, DefaultWind(), ChartProjection.Equidistant);
                Assert.AreEqual(0.0, p.Y, 1e-12, $"phi={degrees} deg");
            }
        }

        [Test]
        public void PowerDependsOnlyOnRadius()
        {
            // Two directions at the same angular distance from the centre must have
            // the same effective wind, whatever their bearing.
            var wind = DefaultWind();
            double theta = 50.0 * MathD.Deg2Rad;

            Vec3 up = (Vec3.Forward * MathD.Cos(theta) + Vec3.Up * MathD.Sin(theta)).Normalized();
            Vec3 side = (Vec3.Forward * MathD.Cos(theta) + new Vec3(1, 0, 0) * MathD.Sin(theta)).Normalized();

            double windUp = WindWindow.EffectiveWindSpeed(wind, up);
            double windSide = WindWindow.EffectiveWindSpeed(wind, side);
            Assert.AreEqual(windUp, windSide, 1e-9);

            double radius = WindowChart.Project(up, wind, ChartProjection.Equidistant).Radius;
            double predicted = wind.Speed * WindowChart.PowerAtRadius(radius, ChartProjection.Equidistant);
            Assert.AreEqual(windUp, predicted, 1e-9);
        }

        [Test]
        public void EdgeRadius_HasZeroPower()
        {
            double edge = WindowChart.EdgeRadius(ChartProjection.Equidistant);
            Assert.AreEqual(MathD.PI * 0.5, edge, 1e-12);
            Assert.AreEqual(0.0, WindowChart.PowerAtRadius(edge, ChartProjection.Equidistant), 1e-12);
        }

        [Test]
        public void ProjectUnproject_RoundTrips_BothProjections()
        {
            var wind = DefaultWind();
            var projections = new[] { ChartProjection.Equidistant, ChartProjection.Stereographic };

            foreach (var projection in projections)
            {
                for (int beta = 0; beta <= 80; beta += 20)
                {
                    for (int phi = -80; phi <= 80; phi += 40)
                    {
                        double b = beta * MathD.Deg2Rad;
                        double f = phi * MathD.Deg2Rad;
                        Vec3 original = new Vec3(
                            MathD.Cos(b) * MathD.Sin(f),
                            MathD.Sin(b),
                            MathD.Cos(b) * MathD.Cos(f));

                        ChartPoint point = WindowChart.Project(original, wind, projection);
                        Vec3 restored = WindowChart.Unproject(point, wind, projection);

                        Assert.AreEqual(original.X, restored.X, 1e-9, $"{projection} beta={beta} phi={phi} X");
                        Assert.AreEqual(original.Y, restored.Y, 1e-9, $"{projection} beta={beta} phi={phi} Y");
                        Assert.AreEqual(original.Z, restored.Z, 1e-9, $"{projection} beta={beta} phi={phi} Z");
                    }
                }
            }
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd .verify-core && dotnet test --filter WindowChartTests`
Expected: build error, `The name 'WindowChart' does not exist in the current context`

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Core/WindowChart.cs`:

```csharp
namespace Kitesim.Core
{
    public enum ChartProjection
    {
        Equidistant,
        Stereographic
    }

    public readonly struct ChartPoint
    {
        public readonly double X;
        public readonly double Y;

        public ChartPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double Radius => MathD.Sqrt(X * X + Y * Y);

        public override string ToString() => $"({X:F3}, {Y:F3})";
    }

    /// <summary>
    /// Azimuthal projection of the wind-window hemisphere onto a plane, centred on
    /// the horizontal downwind direction.
    ///
    /// For a unit direction p from pilot to kite and that centre C,
    /// Dot(p, C) = cos(beta) * cos(phi) = cos(theta), so the effective wind is
    /// W * cos(theta) and depends only on the angular distance from the centre.
    /// Consequences: iso-power contours are concentric circles, the ground is the
    /// horizontal diameter, the zero-power edge of the window is the circle at
    /// theta = 90 degrees, and the flyable window is the upper half-disc.
    /// See docs/kite-anatomy.md section 3.
    /// </summary>
    public static class WindowChart
    {
        /// <summary>Chart centre in world space: the horizontal part of the wind direction.</summary>
        public static Vec3 Center(WindField wind)
        {
            Vec3 horizontal = new Vec3(wind.Direction.X, 0.0, wind.Direction.Z);
            if (horizontal.SqrLength < 1e-12)
                return Vec3.Forward;
            return horizontal.Normalized();
        }

        /// <summary>Chart +x axis in world space: the pilot's right.</summary>
        public static Vec3 Right(WindField wind)
        {
            return Vec3.Cross(Vec3.Up, Center(wind)).Normalized();
        }

        public static double RadiusFromTheta(double thetaRad, ChartProjection projection)
        {
            if (projection == ChartProjection.Stereographic)
                return 2.0 * MathD.Tan(thetaRad * 0.5);
            return thetaRad;
        }

        public static double ThetaFromRadius(double radius, ChartProjection projection)
        {
            if (projection == ChartProjection.Stereographic)
                return 2.0 * MathD.Atan(radius * 0.5);
            return radius;
        }

        /// <summary>Chart radius at which the effective wind reaches zero.</summary>
        public static double EdgeRadius(ChartProjection projection)
        {
            return RadiusFromTheta(MathD.PI * 0.5, projection);
        }

        /// <summary>Normalised power, cos(theta), at a chart radius. Clamped to [0, 1].</summary>
        public static double PowerAtRadius(double radius, ChartProjection projection)
        {
            return MathD.Clamp01(MathD.Cos(ThetaFromRadius(radius, projection)));
        }

        public static ChartPoint Project(Vec3 direction, WindField wind, ChartProjection projection)
        {
            Vec3 p = direction.Normalized();
            Vec3 center = Center(wind);
            Vec3 right = Vec3.Cross(Vec3.Up, center).Normalized();

            double cosTheta = MathD.Clamp(Vec3.Dot(p, center), -1.0, 1.0);
            double radius = RadiusFromTheta(MathD.Acos(cosTheta), projection);

            // Bearing inside the plane perpendicular to the centre. That plane is
            // spanned by world up and the chart right axis, because the centre is
            // horizontal.
            double bx = Vec3.Dot(p, right);
            double by = Vec3.Dot(p, Vec3.Up);
            double bearing = MathD.Sqrt(bx * bx + by * by);
            if (bearing < 1e-12)
                return new ChartPoint(0.0, 0.0);

            return new ChartPoint(radius * bx / bearing, radius * by / bearing);
        }

        public static Vec3 Unproject(ChartPoint point, WindField wind, ChartProjection projection)
        {
            Vec3 center = Center(wind);
            Vec3 right = Vec3.Cross(Vec3.Up, center).Normalized();

            double radius = point.Radius;
            if (radius < 1e-12)
                return center;

            double theta = ThetaFromRadius(radius, projection);
            Vec3 bearing = right * (point.X / radius) + Vec3.Up * (point.Y / radius);
            return (center * MathD.Cos(theta) + bearing * MathD.Sin(theta)).Normalized();
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd .verify-core && dotnet test --filter WindowChartTests`
Expected: `Passed! - Failed: 0, Passed: 9`

- [ ] **Step 5: Run the whole suite**

Run: `cd .verify-core && dotnet test`
Expected: `Passed! - Failed: 0, Passed: 19` (10 pre-existing plus 9 new)

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Core/WindowChart.cs Assets/Tests/Editor/WindowChartTests.cs
git commit -m "M3.1: azimuthal wind-window chart projection"
```

---

## Task 3: KiteStateView

The model-independent snapshot both simulators produce. Direction and heading are stored as unit vectors rather than `(phi, beta, psi)` angles because the angle form is singular at the zenith; the angles are derived properties for UI and diagnostics.

**Files:**
- Create: `Assets/Scripts/Core/KiteStateView.cs`
- Test: `Assets/Tests/Editor/KiteStateViewTests.cs`

**Interfaces:**
- Consumes: `Vec3`, `MathD`
- Produces:
  - `struct KiteStateView` with readonly fields `PilotPosition`, `Direction`, `Heading`, `LineLength`, `Speed`, `Tension`, `LineWraps`, `Landed`
  - constructor `KiteStateView(Vec3 pilotPosition, Vec3 direction, Vec3 heading, double lineLength, double speed, double tension, int lineWraps, bool landed)`
  - properties `WorldPosition -> Vec3`, `ElevationRad -> double`, `AzimuthRad -> double`, `HeadingRad -> double`
  - `KiteStateView.TangentUp(Vec3 direction) -> Vec3`
  - `KiteStateView.TangentRight(Vec3 direction) -> Vec3`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/KiteStateViewTests.cs`:

```csharp
using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class KiteStateViewTests
    {
        private static KiteStateView Make(Vec3 direction, Vec3 heading)
        {
            return new KiteStateView(Vec3.Zero, direction, heading, 45.0, 20.0, 60.0, 0, false);
        }

        [Test]
        public void TangentUp_AtWindowCentre_IsWorldUp()
        {
            Vec3 up = KiteStateView.TangentUp(Vec3.Forward);
            Assert.AreEqual(0.0, up.X, 1e-12);
            Assert.AreEqual(1.0, up.Y, 1e-12);
            Assert.AreEqual(0.0, up.Z, 1e-12);
        }

        [Test]
        public void TangentRight_AtWindowCentre_IsPositiveX()
        {
            Vec3 right = KiteStateView.TangentRight(Vec3.Forward);
            Assert.AreEqual(1.0, right.X, 1e-12);
            Assert.AreEqual(0.0, right.Y, 1e-12);
            Assert.AreEqual(0.0, right.Z, 1e-12);
        }

        [Test]
        public void HeadingRad_TowardZenith_IsZero()
        {
            var view = Make(Vec3.Forward, Vec3.Up);
            Assert.AreEqual(0.0, view.HeadingRad, 1e-12);
        }

        [Test]
        public void HeadingRad_TowardPilotsRight_IsNinetyDegrees()
        {
            var view = Make(Vec3.Forward, new Vec3(1, 0, 0));
            Assert.AreEqual(MathD.PI * 0.5, view.HeadingRad, 1e-12);
        }

        [Test]
        public void HeadingRad_TowardGround_IsOneEightyDegrees()
        {
            var view = Make(Vec3.Forward, new Vec3(0, -1, 0));
            Assert.AreEqual(MathD.PI, MathD.Abs(view.HeadingRad), 1e-12);
        }

        [Test]
        public void ElevationAndAzimuth_MatchTheConstructedDirection()
        {
            double beta = 35.0 * MathD.Deg2Rad;
            double phi = -20.0 * MathD.Deg2Rad;
            Vec3 direction = new Vec3(
                MathD.Cos(beta) * MathD.Sin(phi),
                MathD.Sin(beta),
                MathD.Cos(beta) * MathD.Cos(phi));

            var view = Make(direction, KiteStateView.TangentUp(direction));

            Assert.AreEqual(beta, view.ElevationRad, 1e-12);
            Assert.AreEqual(phi, view.AzimuthRad, 1e-12);
        }

        [Test]
        public void WorldPosition_IsPilotPlusDirectionTimesLineLength()
        {
            var view = new KiteStateView(
                new Vec3(3.0, 0.0, 0.0), Vec3.Forward, Vec3.Up, 45.0, 20.0, 60.0, 0, false);

            Vec3 world = view.WorldPosition;
            Assert.AreEqual(3.0, world.X, 1e-12);
            Assert.AreEqual(0.0, world.Y, 1e-12);
            Assert.AreEqual(45.0, world.Z, 1e-12);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd .verify-core && dotnet test --filter KiteStateViewTests`
Expected: build error, `The name 'KiteStateView' does not exist in the current context`

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Core/KiteStateView.cs`:

```csharp
namespace Kitesim.Core
{
    /// <summary>
    /// Model-independent snapshot of one flying kite. Both WindowSim (the reduced
    /// 2D model) and the future RigidKiteSim produce this; rendering, track
    /// recording and cross-model comparison all consume it.
    ///
    /// Direction and Heading are unit vectors, not (phi, beta, psi) angles: the
    /// angle form is singular at the zenith. Angles are derived on demand.
    /// </summary>
    public readonly struct KiteStateView
    {
        /// <summary>Pilot's hands in world space. The pilot walks, so this varies.</summary>
        public readonly Vec3 PilotPosition;

        /// <summary>Unit vector from pilot to kite.</summary>
        public readonly Vec3 Direction;

        /// <summary>Unit direction of travel, perpendicular to Direction.</summary>
        public readonly Vec3 Heading;

        public readonly double LineLength;
        public readonly double Speed;
        public readonly double Tension;
        public readonly int LineWraps;
        public readonly bool Landed;

        public KiteStateView(
            Vec3 pilotPosition,
            Vec3 direction,
            Vec3 heading,
            double lineLength,
            double speed,
            double tension,
            int lineWraps,
            bool landed)
        {
            PilotPosition = pilotPosition;
            Direction = direction;
            Heading = heading;
            LineLength = lineLength;
            Speed = speed;
            Tension = tension;
            LineWraps = lineWraps;
            Landed = landed;
        }

        public Vec3 WorldPosition => PilotPosition + Direction * LineLength;

        /// <summary>Elevation above the horizon, radians.</summary>
        public double ElevationRad => MathD.Asin(MathD.Clamp(Direction.Y, -1.0, 1.0));

        /// <summary>Azimuth from world +Z toward +X, radians.</summary>
        public double AzimuthRad => MathD.Atan2(Direction.X, Direction.Z);

        /// <summary>
        /// Heading in the tangent plane: 0 toward the zenith, +90 degrees toward
        /// the pilot's right.
        /// </summary>
        public double HeadingRad
        {
            get
            {
                Vec3 up = TangentUp(Direction);
                Vec3 right = TangentRight(Direction);
                return MathD.Atan2(Vec3.Dot(Heading, right), Vec3.Dot(Heading, up));
            }
        }

        /// <summary>
        /// World up projected into the tangent plane at the given direction: the
        /// "toward zenith" axis of the local frame. Falls back to world forward
        /// exactly at the zenith, where the frame is undefined.
        /// </summary>
        public static Vec3 TangentUp(Vec3 direction)
        {
            Vec3 candidate = Vec3.Up - direction * Vec3.Dot(Vec3.Up, direction);
            if (candidate.SqrLength < 1e-12)
                return Vec3.Cross(direction, Vec3.Forward).Normalized();
            return candidate.Normalized();
        }

        /// <summary>
        /// The "toward the pilot's right" axis of the local frame. Chosen so that a
        /// positive turn rate rotates the heading from TangentUp toward TangentRight.
        /// </summary>
        public static Vec3 TangentRight(Vec3 direction)
        {
            return Vec3.Cross(TangentUp(direction), direction).Normalized();
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd .verify-core && dotnet test --filter KiteStateViewTests`
Expected: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/KiteStateView.cs Assets/Tests/Editor/KiteStateViewTests.cs
git commit -m "M3.1: model-independent KiteStateView snapshot"
```

---

## Task 4: WindowSim — configuration, state and steady flight

No steering and no walking yet. The kite advances along a great circle at the quasi-steady crosswind speed.

The glide ratio is **derived from the existing polar** rather than being a free number: `E = cl / (cd_polar + ParasiticDrag)`, where `ParasiticDrag` covers lines, bridle and frame. With the default 8° trim and `ParasiticDrag = 0.09`, `E ≈ 6.04`, so in 5 m/s wind the kite reaches ≈30 m/s at the window centre. That matches the commonly quoted 50–80 km/h for sport kites and is the calibration target.

**Files:**
- Create: `Assets/Scripts/Core/WindowConfiguration.cs`
- Create: `Assets/Scripts/Core/WindowKiteState.cs`
- Create: `Assets/Scripts/Core/WindowSim.cs`
- Test: `Assets/Tests/Editor/WindowSimTests.cs`

**Interfaces:**
- Consumes: `Vec3`, `MathD`, `WindField`, `AeroPolar`, `KiteStateView`
- Produces:
  - `class WindowConfiguration` with public fields `LineLength`, `SailArea`, `AirDensity`, `TrimAngleOfAttack`, `ParasiticDrag`, `SteeringGain`, `SpeedLag`, `PowerFloor`, `MinAirspeed`, `HandTravel`, `WalkSpeedBack`, `WalkSpeedForward`, `FieldHalfSize`; property `GlideRatio -> double`
  - `class WindowKiteState` with public fields `Direction`, `Heading`, `Speed`, `AccumulatedTurn`, `Landed`; static `WindowKiteState.OnGround(double azimuthRad) -> WindowKiteState`
  - `class WindowSim` with public fields `Config`, `Wind`, `PilotPosition`, `PilotVelocity`; methods `Step(WindowKiteState state, double dt, double handLeft, double handRight, double walkAxis)`, `Launch(WindowKiteState state)`, `View(WindowKiteState state) -> KiteStateView`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/WindowSimTests.cs`:

```csharp
using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class WindowSimTests
    {
        private const double Dt = 0.02;

        private static WindowSim MakeSim(double windSpeed = 5.0)
        {
            return new WindowSim(new WindowConfiguration())
            {
                Wind = new WindField { Speed = windSpeed, Direction = Vec3.Forward }
            };
        }

        private static WindowKiteState Flying(Vec3 direction, Vec3 heading, double speed)
        {
            Vec3 d = direction.Normalized();
            Vec3 h = heading - d * Vec3.Dot(heading, d);
            return new WindowKiteState
            {
                Direction = d,
                Heading = h.Normalized(),
                Speed = speed,
                Landed = false
            };
        }

        private static void Run(WindowSim sim, WindowKiteState state, double seconds,
            double handLeft = 0.0, double handRight = 0.0, double walk = 0.0)
        {
            int steps = (int)(seconds / Dt);
            for (int i = 0; i < steps; i++)
                sim.Step(state, Dt, handLeft, handRight, walk);
        }

        [Test]
        public void GlideRatio_ComesFromThePolarPlusParasiticDrag()
        {
            var config = new WindowConfiguration();
            var (cl, cd) = AeroPolar.Evaluate(config.TrimAngleOfAttack);
            Assert.AreEqual(cl / (cd + config.ParasiticDrag), config.GlideRatio, 1e-12);
            Assert.AreEqual(6.04, config.GlideRatio, 0.05);
        }

        [Test]
        public void AtWindowCentre_SpeedApproachesGlideRatioTimesWind()
        {
            var sim = MakeSim(5.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 0.0);

            // Freeze the kite in place so only the speed relaxation is exercised.
            sim.Config.LineLength = 1e9;
            Run(sim, state, 5.0);

            Assert.AreEqual(5.0 * sim.Config.GlideRatio, state.Speed, 0.5);
            Assert.That(state.Speed, Is.InRange(25.0, 35.0));
        }

        [Test]
        public void AtWindowEdge_SpeedFallsToThePowerFloor()
        {
            var sim = MakeSim(5.0);
            sim.Config.LineLength = 1e9;
            // Directly crosswind at ground level: theta = 90 degrees.
            var state = Flying(new Vec3(1, 0, 0), Vec3.Up, 30.0);

            Run(sim, state, 5.0);

            double expected = sim.Config.GlideRatio * sim.Config.PowerFloor * 5.0;
            Assert.AreEqual(expected, state.Speed, 0.2);
            Assert.Less(state.Speed, 3.0);
        }

        [Test]
        public void HeadingTowardZenith_FliesUpTheMeridian()
        {
            var sim = MakeSim(6.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 30.0);

            Run(sim, state, 3.0);

            // Gravity cannot turn a kite that is flying straight up, so it climbs
            // along the meridian and the azimuth stays at zero.
            Assert.Greater(sim.View(state).ElevationRad, 60.0 * MathD.Deg2Rad);
            Assert.AreEqual(0.0, sim.View(state).AzimuthRad, 1e-9);
        }

        [Test]
        public void DirectionAndHeadingStayUnitAndOrthogonal()
        {
            var sim = MakeSim(6.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 25.0);

            Run(sim, state, 20.0, handLeft: -0.3, handRight: 0.5);

            Assert.AreEqual(1.0, state.Direction.Length(), 1e-9);
            Assert.AreEqual(1.0, state.Heading.Length(), 1e-9);
            Assert.AreEqual(0.0, Vec3.Dot(state.Direction, state.Heading), 1e-9);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd .verify-core && dotnet test --filter WindowSimTests`
Expected: build error, `The name 'WindowSim' does not exist in the current context`

- [ ] **Step 3: Write WindowConfiguration**

Create `Assets/Scripts/Core/WindowConfiguration.cs`:

```csharp
namespace Kitesim.Core
{
    /// <summary>
    /// Tunables for the reduced wind-window model. Every value that can be derived
    /// is derived; only genuinely free parameters live here.
    /// </summary>
    public class WindowConfiguration
    {
        public double LineLength = 45.0;
        public double SailArea = 0.75;         // m^2
        public double AirDensity = 1.225;      // kg/m^3

        /// <summary>Trim angle of attack set by the bridle. 8 degrees.</summary>
        public double TrimAngleOfAttack = 8.0 * MathD.Deg2Rad;

        /// <summary>
        /// Drag of lines, bridle and frame, which the sail polar does not include.
        /// Calibrated so the kite reaches 25-35 m/s at the window centre in 5 m/s
        /// wind, matching the 50-80 km/h commonly quoted for sport kites.
        /// </summary>
        public double ParasiticDrag = 0.09;

        /// <summary>Turn rate per unit steering input per unit apparent wind, 1/m.</summary>
        public double SteeringGain = 0.5;

        /// <summary>Time constant of the speed relaxation, seconds.</summary>
        public double SpeedLag = 0.4;

        /// <summary>
        /// Fraction of the wind speed that remains available at the zenith and at
        /// the edge of the window. The cos(theta) law gives exactly zero there,
        /// while a real kite still flies a little; without this floor every visit
        /// to the zenith ends in a fall. See docs/kite-anatomy.md section 3.
        /// </summary>
        public double PowerFloor = 0.05;

        /// <summary>
        /// Airspeed floor for the gravity turn term, m/s. Below this the "kite
        /// flies along its heading" assumption has broken down anyway.
        /// </summary>
        public double MinAirspeed = 2.0;

        /// <summary>Half the travel of one hand, metres.</summary>
        public double HandTravel = 0.7;

        public double WalkSpeedBack = 1.7;     // m/s
        public double WalkSpeedForward = 1.2;  // m/s

        /// <summary>Half the side of the outer competition boundary, metres.</summary>
        public double FieldHalfSize = 45.0;

        /// <summary>
        /// Lift-to-drag ratio, derived from the sail polar plus parasitic drag.
        /// This is what sets the kite's crosswind speed.
        /// </summary>
        public double GlideRatio
        {
            get
            {
                var (cl, cd) = AeroPolar.Evaluate(TrimAngleOfAttack);
                return cl / (cd + ParasiticDrag);
            }
        }

        /// <summary>Resultant force coefficient, used for the line tension readout.</summary>
        public double ForceCoefficient
        {
            get
            {
                var (cl, cd) = AeroPolar.Evaluate(TrimAngleOfAttack);
                double total = cd + ParasiticDrag;
                return MathD.Sqrt(cl * cl + total * total);
            }
        }
    }
}
```

- [ ] **Step 4: Write WindowKiteState**

Create `Assets/Scripts/Core/WindowKiteState.cs`:

```csharp
namespace Kitesim.Core
{
    /// <summary>
    /// Mutable state of one kite in the reduced model: a point on the sphere of
    /// radius LineLength around its pilot, plus the direction it is travelling.
    /// </summary>
    public class WindowKiteState
    {
        /// <summary>Unit vector from pilot to kite.</summary>
        public Vec3 Direction = Vec3.Forward;

        /// <summary>Unit direction of travel, perpendicular to Direction.</summary>
        public Vec3 Heading = Vec3.Up;

        public double Speed;

        /// <summary>
        /// Integral of the turn rate, radians. Every 2*pi of it is one twist of the
        /// two lines around each other.
        /// </summary>
        public double AccumulatedTurn;

        public bool Landed = true;

        public int LineWraps => (int)(AccumulatedTurn / (2.0 * MathD.PI));

        /// <summary>
        /// A kite lying on the ground at the given azimuth, nose up, ready to be
        /// launched.
        /// </summary>
        public static WindowKiteState OnGround(double azimuthRad)
        {
            return new WindowKiteState
            {
                Direction = new Vec3(MathD.Sin(azimuthRad), 0.0, MathD.Cos(azimuthRad)).Normalized(),
                Heading = Vec3.Up,
                Speed = 0.0,
                AccumulatedTurn = 0.0,
                Landed = true
            };
        }
    }
}
```

- [ ] **Step 5: Write WindowSim (steady flight only)**

Create `Assets/Scripts/Core/WindowSim.cs`:

```csharp
namespace Kitesim.Core
{
    /// <summary>
    /// Reduced wind-window model. The kite is a point constrained to the sphere of
    /// radius LineLength around the pilot's hands, carrying a heading in the local
    /// tangent plane. Speed relaxes toward the quasi-steady crosswind speed;
    /// heading turns at a rate set by the steering law plus gravity; position
    /// advances along a great circle, which parallel-transports the heading.
    ///
    /// This model assumes the kite always flies nose-first like a wing. It does not
    /// reproduce stalls, slack lines, hovering, launching or landing dynamics. See
    /// the spec, section 4.
    /// </summary>
    public class WindowSim
    {
        private const double Gravity = 9.80665;

        public WindowConfiguration Config;
        public WindField Wind;
        public Vec3 PilotPosition;
        public Vec3 PilotVelocity;

        public WindowSim(WindowConfiguration config)
        {
            Config = config;
            Wind = new WindField { Speed = 5.0, Direction = Vec3.Forward };
            PilotPosition = Vec3.Zero;
            PilotVelocity = Vec3.Zero;
        }

        public void Step(WindowKiteState state, double dt, double handLeft, double handRight, double walkAxis)
        {
            if (state.Landed)
                return;

            Vec3 p = state.Direction;
            Vec3 t = state.Heading;

            double effectiveWind = EffectiveWind(p);

            double targetSpeed = Config.GlideRatio * effectiveWind;
            state.Speed += (targetSpeed - state.Speed) * MathD.Clamp01(dt / Config.SpeedLag);

            // Advance along the great circle in the heading direction. This form is
            // exact and parallel-transports the heading, so it has no singularity at
            // the zenith the way integrating (phi, beta) would.
            double sigma = state.Speed * dt / Config.LineLength;
            double cos = MathD.Cos(sigma);
            double sin = MathD.Sin(sigma);
            Vec3 nextDirection = p * cos + t * sin;
            Vec3 nextHeading = t * cos - p * sin;

            state.Direction = nextDirection.Normalized();
            state.Heading = Orthonormalize(nextHeading, state.Direction);
        }

        /// <summary>
        /// Component of the wind relative to the pilot along the line. With a still
        /// pilot this is W * cos(theta); walking adds the pilot's own motion, which
        /// is how a flier keeps a kite up in light wind.
        /// </summary>
        public double EffectiveWind(Vec3 direction)
        {
            Vec3 relativeWind = Wind.GetWindVector() - PilotVelocity;
            double along = Vec3.Dot(relativeWind, direction);
            double floor = Config.PowerFloor * Wind.Speed;
            return MathD.Max(along, floor);
        }

        public void Launch(WindowKiteState state)
        {
            if (!state.Landed)
                return;

            state.Landed = false;
            state.Heading = KiteStateView.TangentUp(state.Direction);
            state.Speed = 0.0;
        }

        public KiteStateView View(WindowKiteState state)
        {
            double airspeed = MathD.Max(state.Speed, Config.MinAirspeed);
            double tension = 0.5 * Config.AirDensity * airspeed * airspeed
                             * Config.SailArea * Config.ForceCoefficient;

            return new KiteStateView(
                PilotPosition,
                state.Direction,
                state.Heading,
                Config.LineLength,
                state.Speed,
                tension,
                state.LineWraps,
                state.Landed);
        }

        private static Vec3 Orthonormalize(Vec3 heading, Vec3 direction)
        {
            Vec3 corrected = heading - direction * Vec3.Dot(heading, direction);
            if (corrected.SqrLength < 1e-12)
                return KiteStateView.TangentUp(direction);
            return corrected.Normalized();
        }
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

Run: `cd .verify-core && dotnet test --filter WindowSimTests`
Expected: `Passed! - Failed: 0, Passed: 5`

- [ ] **Step 7: Run the whole suite**

Run: `cd .verify-core && dotnet test`
Expected: `Passed! - Failed: 0, Passed: 31`

- [ ] **Step 8: Commit**

```bash
git add Assets/Scripts/Core/WindowConfiguration.cs Assets/Scripts/Core/WindowKiteState.cs Assets/Scripts/Core/WindowSim.cs Assets/Tests/Editor/WindowSimTests.cs
git commit -m "M3.2: reduced window model with steady crosswind flight"
```

---

## Task 5: Steering and the gravity turn

The turn rate has two terms:

```
psiDot = SteeringGain * airspeed * u   +   Dot(gravityVector, n) / airspeed
```

where `u` is the normalised hand difference and `n = Cross(Heading, Direction)` is the unit tangent 90° from the heading toward increasing ψ.

The second term is **derived, not fitted**. Working it out in angles gives `g * cos(beta) * sin(psi) / v`, which is exactly the gravity term of the published Erhard–Strauch turn-rate law with the constant equal to `g`. Computing it as a dot product avoids angles entirely and is singularity-free. See `docs/kite-anatomy.md` §5.

**Files:**
- Modify: `Assets/Scripts/Core/WindowSim.cs`
- Test: `Assets/Tests/Editor/WindowSimTests.cs` (modify)

**Interfaces:**
- Consumes: everything from Task 4
- Produces: `WindowSim.TurnRate(WindowKiteState state, double handLeft, double handRight) -> double`

- [ ] **Step 1: Write the failing tests**

Append these tests inside the `WindowSimTests` class, before the closing brace:

```csharp
        // Direction of turn is asserted on AccumulatedTurn, which is the unwrapped
        // integral of the turn rate, and over a short window. HeadingRad comes from
        // Atan2 and so folds at +/-180 degrees, and this kite turns far too fast for
        // that to be safe: at full input in 6 m/s wind the rate is around 7.5 rad/s,
        // which crosses the branch cut in under half a second. Asserting the
        // unwrapped quantity keeps these tests valid if SteeringGain is ever retuned.

        [Test]
        public void PullingTheRightHand_TurnsTheKiteToThePilotsRight()
        {
            var sim = MakeSim(6.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 25.0);

            Run(sim, state, 0.1, handLeft: 0.0, handRight: 0.7);

            // Positive turn is from "toward zenith" toward "toward the pilot's right".
            Assert.Greater(state.AccumulatedTurn, 0.2);
            Assert.Greater(sim.View(state).HeadingRad, 0.2);
        }

        [Test]
        public void PullingTheLeftHand_TurnsTheKiteToThePilotsLeft()
        {
            var sim = MakeSim(6.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 25.0);

            Run(sim, state, 0.1, handLeft: 0.7, handRight: 0.0);

            Assert.Less(state.AccumulatedTurn, -0.2);
            Assert.Less(sim.View(state).HeadingRad, -0.2);
        }

        [Test]
        public void TurnRate_IsLinearInSteeringInput()
        {
            var sim = MakeSim(6.0);
            var half = Flying(Vec3.Forward, Vec3.Up, 25.0);
            var full = Flying(Vec3.Forward, Vec3.Up, 25.0);

            double rateHalf = sim.TurnRate(half, 0.0, 0.35);
            double rateFull = sim.TurnRate(full, 0.0, 0.7);

            Assert.AreEqual(2.0, rateFull / rateHalf, 1e-9);
        }

        [Test]
        public void TurnRate_IsLinearInAirspeed()
        {
            var sim = MakeSim(6.0);
            var slow = Flying(Vec3.Forward, Vec3.Up, 10.0);
            var fast = Flying(Vec3.Forward, Vec3.Up, 20.0);

            // Straight up the meridian the gravity term vanishes, leaving only the
            // steering term, which is what the oracle law says is linear in v_app.
            double rateSlow = sim.TurnRate(slow, 0.0, 0.7);
            double rateFast = sim.TurnRate(fast, 0.0, 0.7);

            Assert.AreEqual(2.0, rateFast / rateSlow, 1e-9);
        }

        [Test]
        public void GravityDoesNotTurnAKiteFlyingStraightUp()
        {
            var sim = MakeSim(6.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 25.0);

            Assert.AreEqual(0.0, sim.TurnRate(state, 0.0, 0.0), 1e-12);
        }

        [Test]
        public void GravityDroopsAKiteFlyingAcrossTheWindow()
        {
            var sim = MakeSim(6.0);
            // Flying toward the pilot's right at the window centre.
            var state = Flying(Vec3.Forward, new Vec3(1, 0, 0), 25.0);

            double rate = sim.TurnRate(state, 0.0, 0.0);

            // Positive rate turns the heading from "right" on toward "down".
            Assert.Greater(rate, 0.0);
            Assert.AreEqual(9.80665 / 25.0, rate, 1e-9);
        }

        [Test]
        public void GravityTermMatchesTheClosedFormOfTheOracleLaw()
        {
            var sim = MakeSim(6.0);
            double beta = 40.0 * MathD.Deg2Rad;
            double psi = 120.0 * MathD.Deg2Rad;
            double speed = 18.0;

            Vec3 direction = new Vec3(0.0, MathD.Sin(beta), MathD.Cos(beta)).Normalized();
            Vec3 heading = KiteStateView.TangentUp(direction) * MathD.Cos(psi)
                           + KiteStateView.TangentRight(direction) * MathD.Sin(psi);
            var state = Flying(direction, heading, speed);

            double expected = 9.80665 * MathD.Cos(beta) * MathD.Sin(psi) / speed;
            Assert.AreEqual(expected, sim.TurnRate(state, 0.0, 0.0), 1e-9);
        }

        [Test]
        public void LineWraps_CountFullTurns()
        {
            var sim = MakeSim(6.0);

            // Start well above the horizon. Under full steering the heading turns
            // about eleven times faster than the kite travels across the sphere, so
            // it spins almost in place on a circle of roughly five degrees. Started
            // at ground level it would dip below the horizon and land (Task 7) long
            // before completing a full turn.
            double beta = 45.0 * MathD.Deg2Rad;
            var state = Flying(new Vec3(0.0, MathD.Sin(beta), MathD.Cos(beta)), Vec3.Up, 25.0);
            Assert.AreEqual(0, state.LineWraps);

            Run(sim, state, 6.0, handLeft: 0.0, handRight: 0.7);

            Assert.IsFalse(state.Landed, "the kite must stay airborne to wrap its lines");
            Assert.Greater(state.LineWraps, 0);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd .verify-core && dotnet test --filter WindowSimTests`
Expected: build error, `'WindowSim' does not contain a definition for 'TurnRate'`

- [ ] **Step 3: Add TurnRate and wire it into Step**

In `Assets/Scripts/Core/WindowSim.cs`, add this method after `EffectiveWind`:

```csharp
        /// <summary>
        /// Turn rate about the tether axis, radians per second.
        ///
        /// The steering term is the Erhard-Strauch law: linear in the steering input
        /// and in the apparent wind. The gravity term is derived rather than fitted.
        /// The component of gravity perpendicular to the flight direction inside the
        /// tangent plane, divided by speed, is the turn rate it produces; written in
        /// angles that is g * cos(beta) * sin(psi) / v, exactly the published form.
        /// </summary>
        public double TurnRate(WindowKiteState state, double handLeft, double handRight)
        {
            double airspeed = MathD.Max(state.Speed, Config.MinAirspeed);

            double steering = MathD.Clamp(
                (handRight - handLeft) / (2.0 * Config.HandTravel), -1.0, 1.0);

            // Unit tangent 90 degrees from the heading toward increasing psi.
            Vec3 turnNormal = Vec3.Cross(state.Heading, state.Direction);
            Vec3 gravity = new Vec3(0.0, -Gravity, 0.0);

            return Config.SteeringGain * airspeed * steering
                   + Vec3.Dot(gravity, turnNormal) / airspeed;
        }
```

Then in `Step`, replace the block that begins `// Advance along the great circle` with:

```csharp
            // Rotate the heading inside the tangent plane.
            double turnRate = TurnRate(state, handLeft, handRight);
            double dPsi = turnRate * dt;
            Vec3 turnNormal = Vec3.Cross(t, p);
            t = t * MathD.Cos(dPsi) + turnNormal * MathD.Sin(dPsi);
            state.AccumulatedTurn += dPsi;

            // Advance along the great circle in the heading direction. This form is
            // exact and parallel-transports the heading, so it has no singularity at
            // the zenith the way integrating (phi, beta) would.
            double sigma = state.Speed * dt / Config.LineLength;
            double cos = MathD.Cos(sigma);
            double sin = MathD.Sin(sigma);
            Vec3 nextDirection = p * cos + t * sin;
            Vec3 nextHeading = t * cos - p * sin;
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd .verify-core && dotnet test --filter WindowSimTests`
Expected: `Passed! - Failed: 0, Passed: 13`

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/WindowSim.cs Assets/Tests/Editor/WindowSimTests.cs
git commit -m "M3.2: steering law and derived gravity turn"
```

---

## Task 6: Walking

The pilot walks along the downwind axis. Walking already enters the physics through `EffectiveWind`, which uses the wind **relative to the pilot**; this task adds the pilot integration and the field bound.

**Files:**
- Modify: `Assets/Scripts/Core/WindowSim.cs`
- Test: `Assets/Tests/Editor/WindowSimTests.cs` (modify)

**Interfaces:**
- Consumes: everything from Task 5
- Produces: `WindowSim.UpdatePilot(double dt, double walkAxis)`

- [ ] **Step 1: Write the failing tests**

Append these tests inside the `WindowSimTests` class:

```csharp
        [Test]
        public void WalkingBack_RaisesTheEffectiveWind()
        {
            var sim = MakeSim(5.0);
            double still = sim.EffectiveWind(Vec3.Forward);

            sim.PilotVelocity = new Vec3(0.0, 0.0, -1.7);
            double walking = sim.EffectiveWind(Vec3.Forward);

            Assert.AreEqual(5.0, still, 1e-12);
            Assert.AreEqual(6.7, walking, 1e-12);
        }

        [Test]
        public void WalkingForward_LowersTheEffectiveWind()
        {
            var sim = MakeSim(5.0);
            sim.PilotVelocity = new Vec3(0.0, 0.0, 1.2);

            Assert.AreEqual(3.8, sim.EffectiveWind(Vec3.Forward), 1e-12);
        }

        [Test]
        public void WalkAxis_MovesThePilotAtTheConfiguredSpeeds()
        {
            var sim = MakeSim(5.0);

            sim.UpdatePilot(1.0, -1.0);
            Assert.AreEqual(-sim.Config.WalkSpeedBack, sim.PilotPosition.Z, 1e-12);
            Assert.AreEqual(-sim.Config.WalkSpeedBack, sim.PilotVelocity.Z, 1e-12);

            sim.PilotPosition = Vec3.Zero;
            sim.UpdatePilot(1.0, 1.0);
            Assert.AreEqual(sim.Config.WalkSpeedForward, sim.PilotPosition.Z, 1e-12);
        }

        [Test]
        public void PilotCannotWalkOutOfTheField()
        {
            var sim = MakeSim(5.0);

            for (int i = 0; i < 5000; i++)
                sim.UpdatePilot(Dt, -1.0);

            Assert.AreEqual(-sim.Config.FieldHalfSize, sim.PilotPosition.Z, 1e-9);
            Assert.AreEqual(0.0, sim.PilotVelocity.Z, 1e-9,
                "velocity must be zeroed at the boundary or the kite keeps gaining airspeed");
        }

        [Test]
        public void TheGravityTurnDominatesWhenTheKiteIsSlow()
        {
            // This is the mechanism by which a kite drops out of the sky in light
            // wind: the gravity turn goes as 1/v, so a slow kite flying across the
            // window is whipped toward the ground within a second, while a fast one
            // barely droops. Asserted on the rate itself, which is exact, rather
            // than on an integrated trajectory.
            var sim = MakeSim(5.0);
            var slow = Flying(Vec3.Forward, new Vec3(1, 0, 0), 6.0);
            var fast = Flying(Vec3.Forward, new Vec3(1, 0, 0), 30.0);

            double slowRate = sim.TurnRate(slow, 0.0, 0.0);
            double fastRate = sim.TurnRate(fast, 0.0, 0.0);

            Assert.AreEqual(9.80665 / 6.0, slowRate, 1e-9);
            Assert.Greater(slowRate, fastRate * 4.0);
            Assert.Greater(slowRate, 1.5,
                "over a radian per second: the kite droops to the ground within a second");
        }

        [Test]
        public void InLightWind_WalkingBackKeepsTheKiteFaster()
        {
            var passive = MakeSim(1.1);
            var active = MakeSim(1.1);
            var passiveKite = Flying(Vec3.Forward, Vec3.Up, 6.0);
            var activeKite = Flying(Vec3.Forward, Vec3.Up, 6.0);

            passive.Config.LineLength = 1e9;
            active.Config.LineLength = 1e9;

            Run(passive, passiveKite, 5.0);
            Run(active, activeKite, 5.0, walk: -1.0);

            Assert.Greater(activeKite.Speed, passiveKite.Speed * 1.5,
                "walking back must be the dominant control in light wind");
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd .verify-core && dotnet test --filter WindowSimTests`
Expected: build error, `'WindowSim' does not contain a definition for 'UpdatePilot'`

- [ ] **Step 3: Add UpdatePilot and call it from Step**

In `Assets/Scripts/Core/WindowSim.cs`, add after `TurnRate`:

```csharp
        /// <summary>
        /// Move the pilot along the downwind axis. walkAxis is +1 for a full stride
        /// downwind (toward the kite) and -1 for a full stride upwind (away from
        /// it). Walking back is the flier's main tool for keeping a kite up in light
        /// wind: it raises the wind seen relative to the pilot, which EffectiveWind
        /// already accounts for.
        /// </summary>
        public void UpdatePilot(double dt, double walkAxis)
        {
            double axis = MathD.Clamp(walkAxis, -1.0, 1.0);
            double speed = axis >= 0.0
                ? axis * Config.WalkSpeedForward
                : axis * Config.WalkSpeedBack;

            Vec3 downwind = WindowChart.Center(Wind);
            Vec3 velocity = downwind * speed;
            Vec3 next = PilotPosition + velocity * dt;

            double half = Config.FieldHalfSize;
            double clampedX = MathD.Clamp(next.X, -half, half);
            double clampedZ = MathD.Clamp(next.Z, -half, half);
            Vec3 clamped = new Vec3(clampedX, next.Y, clampedZ);

            // Recover the velocity that was actually realised, so a pilot pressed
            // against the boundary stops feeding airspeed to the kite.
            PilotVelocity = dt > 1e-12 ? (clamped - PilotPosition) / dt : Vec3.Zero;
            PilotPosition = clamped;
        }
```

In `Step`, insert as the very first statement, **before** the `if (state.Landed)` guard:

```csharp
            UpdatePilot(dt, walkAxis);
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd .verify-core && dotnet test --filter WindowSimTests`
Expected: `Passed! - Failed: 0, Passed: 19`

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Core/WindowSim.cs Assets/Tests/Editor/WindowSimTests.cs
git commit -m "M3.2: pilot walking bounded by the competition field"
```

---

## Task 7: Ground contact and launch

**Files:**
- Modify: `Assets/Scripts/Core/WindowSim.cs`
- Test: `Assets/Tests/Editor/WindowSimTests.cs` (modify)

**Interfaces:**
- Consumes: everything from Task 6
- Produces: no new public members; `Step` now lands the kite and `Launch` unlands it

- [ ] **Step 1: Write the failing tests**

Append inside `WindowSimTests`:

```csharp
        [Test]
        public void FlyingIntoTheGround_Lands()
        {
            var sim = MakeSim(6.0);
            var state = Flying(
                new Vec3(0.0, MathD.Sin(0.2), MathD.Cos(0.2)),
                new Vec3(0.0, -1.0, 0.0),
                25.0);

            Run(sim, state, 3.0);

            Assert.IsTrue(state.Landed);
            Assert.AreEqual(0.0, state.Direction.Y, 1e-9);
            Assert.AreEqual(0.0, state.Speed, 1e-12);
        }

        [Test]
        public void ALandedKiteDoesNotMove()
        {
            var sim = MakeSim(6.0);
            var state = WindowKiteState.OnGround(0.0);
            Vec3 before = state.Direction;

            Run(sim, state, 3.0, handRight: 0.7);

            Assert.AreEqual(before.X, state.Direction.X, 1e-12);
            Assert.AreEqual(before.Y, state.Direction.Y, 1e-12);
            Assert.AreEqual(before.Z, state.Direction.Z, 1e-12);
        }

        [Test]
        public void LaunchingFromTheGround_Climbs()
        {
            var sim = MakeSim(6.0);
            var state = WindowKiteState.OnGround(0.0);

            sim.Launch(state);
            Run(sim, state, 4.0);

            Assert.IsFalse(state.Landed);
            Assert.Greater(sim.View(state).ElevationRad, 20.0 * MathD.Deg2Rad);
        }

        [Test]
        public void StaysStableOverTenMinutesOfSteering()
        {
            var sim = MakeSim(8.0);
            var state = Flying(Vec3.Forward, Vec3.Up, 25.0);

            int steps = (int)(600.0 / Dt);
            for (int i = 0; i < steps; i++)
            {
                double phase = MathD.Sin(i * Dt * 0.7) * 0.7;
                sim.Step(state, Dt, -phase, phase, 0.0);

                if (state.Landed)
                    sim.Launch(state);
            }

            Assert.AreEqual(1.0, state.Direction.Length(), 1e-6);
            Assert.IsFalse(double.IsNaN(state.Speed));
            Assert.That(state.Speed, Is.InRange(0.0, 200.0));
        }

        [Test]
        public void IsDeterministic()
        {
            var first = MakeSim(7.0);
            var second = MakeSim(7.0);
            var a = Flying(Vec3.Forward, Vec3.Up, 20.0);
            var b = Flying(Vec3.Forward, Vec3.Up, 20.0);

            for (int i = 0; i < 3000; i++)
            {
                double u = MathD.Sin(i * 0.013) * 0.7;
                double walk = MathD.Cos(i * 0.007);
                first.Step(a, Dt, -u, u, walk);
                second.Step(b, Dt, -u, u, walk);
            }

            Assert.AreEqual(a.Direction.X, b.Direction.X);
            Assert.AreEqual(a.Direction.Y, b.Direction.Y);
            Assert.AreEqual(a.Direction.Z, b.Direction.Z);
            Assert.AreEqual(a.Speed, b.Speed);
        }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd .verify-core && dotnet test --filter WindowSimTests`
Expected: `FlyingIntoTheGround_Lands` fails with `Expected: True But was: False`

- [ ] **Step 3: Add ground contact**

In `Assets/Scripts/Core/WindowSim.cs`, at the end of `Step`, after the two `state.Direction` / `state.Heading` assignments, add:

```csharp
            if (state.Direction.Y < 0.0)
                Land(state);
```

And add this private method at the end of the class:

```csharp
        private static void Land(WindowKiteState state)
        {
            Vec3 horizontal = new Vec3(state.Direction.X, 0.0, state.Direction.Z);
            state.Direction = horizontal.SqrLength < 1e-12
                ? Vec3.Forward
                : horizontal.Normalized();
            state.Heading = KiteStateView.TangentUp(state.Direction);
            state.Speed = 0.0;
            state.Landed = true;
        }
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd .verify-core && dotnet test --filter WindowSimTests`
Expected: `Passed! - Failed: 0, Passed: 24`

- [ ] **Step 5: Run the whole suite**

Run: `cd .verify-core && dotnet test`
Expected: `Passed! - Failed: 0, Passed: 50`

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Core/WindowSim.cs Assets/Tests/Editor/WindowSimTests.cs
git commit -m "M3.2: ground contact, launch, stability and determinism"
```

---

## Task 8: Track recording and playback

Pure in-memory recording of `KiteStateView` per pilot. File I/O lives in the Unity layer (Task 12) so that `Kitesim.Core` stays dependency-free.

Frames store the direction and heading **vectors**, which are the singularity-free encoding of `(phi, beta, psi)` named in the spec, plus the pilot's position at that instant, because the pilot walks.

**Files:**
- Create: `Assets/Scripts/Core/Track.cs`
- Test: `Assets/Tests/Editor/TrackTests.cs`

**Interfaces:**
- Consumes: `Vec3`, `MathD`, `KiteStateView`
- Produces:
  - `struct TrackFrame` with readonly fields `Time`, `PilotIndex`, `PilotPosition`, `Direction`, `Heading`, `Speed`, `Tension`, `Landed`
  - `class Track` with `Frames -> IReadOnlyList<TrackFrame>`, `Duration -> double`, `PilotCount -> int`, `Record(double time, int pilotIndex, KiteStateView view)`, `Sample(double time, int pilotIndex, double lineLength) -> KiteStateView`, `Clear()`

- [ ] **Step 1: Write the failing tests**

Create `Assets/Tests/Editor/TrackTests.cs`:

```csharp
using NUnit.Framework;

namespace Kitesim.Core.Tests
{
    public class TrackTests
    {
        private static KiteStateView View(Vec3 direction, Vec3 heading, double speed)
        {
            return new KiteStateView(Vec3.Zero, direction.Normalized(), heading.Normalized(),
                45.0, speed, 60.0, 0, false);
        }

        [Test]
        public void RecordsFramesPerPilot()
        {
            var track = new Track();
            track.Record(0.0, 0, View(Vec3.Forward, Vec3.Up, 20.0));
            track.Record(0.0, 1, View(Vec3.Forward, Vec3.Up, 25.0));
            track.Record(0.02, 0, View(Vec3.Forward, Vec3.Up, 21.0));

            Assert.AreEqual(3, track.Frames.Count);
            Assert.AreEqual(2, track.PilotCount);
            Assert.AreEqual(0.02, track.Duration, 1e-12);
        }

        [Test]
        public void SamplingAtARecordedTimeReturnsThatFrame()
        {
            var track = new Track();
            track.Record(0.0, 0, View(Vec3.Forward, Vec3.Up, 20.0));
            track.Record(0.02, 0, View(Vec3.Up, new Vec3(1, 0, 0), 30.0));

            var sample = track.Sample(0.02, 0, 45.0);

            Assert.AreEqual(0.0, sample.Direction.X, 1e-9);
            Assert.AreEqual(1.0, sample.Direction.Y, 1e-9);
            Assert.AreEqual(30.0, sample.Speed, 1e-9);
        }

        [Test]
        public void SamplingBetweenFramesInterpolates()
        {
            var track = new Track();
            track.Record(0.0, 0, View(Vec3.Forward, Vec3.Up, 10.0));
            track.Record(1.0, 0, View(Vec3.Forward, Vec3.Up, 30.0));

            var sample = track.Sample(0.5, 0, 45.0);

            Assert.AreEqual(20.0, sample.Speed, 1e-9);
            Assert.AreEqual(1.0, sample.Direction.Length(), 1e-9);
            Assert.AreEqual(1.0, sample.Heading.Length(), 1e-9);
        }

        [Test]
        public void SamplingOutsideTheRangeClampsToTheEnds()
        {
            var track = new Track();
            track.Record(1.0, 0, View(Vec3.Forward, Vec3.Up, 10.0));
            track.Record(2.0, 0, View(Vec3.Forward, Vec3.Up, 30.0));

            Assert.AreEqual(10.0, track.Sample(-5.0, 0, 45.0).Speed, 1e-9);
            Assert.AreEqual(30.0, track.Sample(99.0, 0, 45.0).Speed, 1e-9);
        }

        [Test]
        public void InterpolatedDirectionsStayUnitLength()
        {
            var track = new Track();
            track.Record(0.0, 0, View(Vec3.Forward, Vec3.Up, 20.0));
            track.Record(1.0, 0, View(Vec3.Up, new Vec3(1, 0, 0), 20.0));

            for (double t = 0.0; t <= 1.0; t += 0.1)
            {
                var sample = track.Sample(t, 0, 45.0);
                Assert.AreEqual(1.0, sample.Direction.Length(), 1e-9, $"t={t}");
                Assert.AreEqual(1.0, sample.Heading.Length(), 1e-9, $"t={t}");
            }
        }

        [Test]
        public void RecordAndReplayReproduceTheFlight()
        {
            var sim = new WindowSim(new WindowConfiguration())
            {
                Wind = new WindField { Speed = 6.0, Direction = Vec3.Forward }
            };
            // Start well above the horizon and keep the run short. From ground level
            // this steering lands the kite within about a third of a second, and the
            // round trip would then be matching a stationary point rather than a
            // flight. Even from 45 degrees the kite sags and lands at about 9.5 s —
            // correct behaviour, since gravity drags an unattended kite down — so the
            // run stops at 5 s, with roughly four and a half seconds of margin.
            double beta = 45.0 * MathD.Deg2Rad;
            Vec3 start = new Vec3(0.0, MathD.Sin(beta), MathD.Cos(beta));
            var state = new WindowKiteState
            {
                Direction = start,
                Heading = KiteStateView.TangentUp(start),
                Speed = 25.0,
                Landed = false
            };

            Vec3 firstDirection = state.Direction;
            var track = new Track();
            double time = 0.0;
            for (int i = 0; i < 250; i++)
            {
                double u = MathD.Sin(i * 0.02) * 0.7;
                sim.Step(state, 0.02, -u, u, 0.0);
                time += 0.02;
                track.Record(time, 0, sim.View(state));
            }

            Assert.IsFalse(state.Landed, "the round trip must be recorded from a kite that actually flew");
            Assert.Greater(
                MathD.Acos(MathD.Clamp(Vec3.Dot(firstDirection, state.Direction), -1.0, 1.0)),
                10.0 * MathD.Deg2Rad,
                "the kite must have crossed the window, not hovered in place");

            var last = track.Sample(time, 0, 45.0);
            Assert.AreEqual(state.Direction.X, last.Direction.X, 1e-9);
            Assert.AreEqual(state.Direction.Y, last.Direction.Y, 1e-9);
            Assert.AreEqual(state.Direction.Z, last.Direction.Z, 1e-9);
        }

        [Test]
        public void ClearEmptiesTheTrack()
        {
            var track = new Track();
            track.Record(0.0, 0, View(Vec3.Forward, Vec3.Up, 20.0));
            track.Clear();

            Assert.AreEqual(0, track.Frames.Count);
            Assert.AreEqual(0.0, track.Duration, 1e-12);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd .verify-core && dotnet test --filter TrackTests`
Expected: build error, `The name 'Track' does not exist in the current context`

- [ ] **Step 3: Write the implementation**

Create `Assets/Scripts/Core/Track.cs`:

```csharp
using System.Collections.Generic;

namespace Kitesim.Core
{
    /// <summary>
    /// One recorded sample of one kite.
    ///
    /// Direction and Heading are stored as unit vectors, which is the
    /// singularity-free encoding of the (phi, beta, psi) triple. PilotPosition is
    /// recorded per frame because the pilot walks, and it is recorded in world
    /// space so that a track made in the 2D chart can be replayed in 3D with the
    /// pilots at their real spacing.
    /// </summary>
    public readonly struct TrackFrame
    {
        public readonly double Time;
        public readonly int PilotIndex;
        public readonly Vec3 PilotPosition;
        public readonly Vec3 Direction;
        public readonly Vec3 Heading;
        public readonly double Speed;
        public readonly double Tension;
        public readonly bool Landed;

        public TrackFrame(double time, int pilotIndex, Vec3 pilotPosition, Vec3 direction,
            Vec3 heading, double speed, double tension, bool landed)
        {
            Time = time;
            PilotIndex = pilotIndex;
            PilotPosition = pilotPosition;
            Direction = direction;
            Heading = heading;
            Speed = speed;
            Tension = tension;
            Landed = landed;
        }
    }

    /// <summary>
    /// In-memory recording of a flight, sampled per pilot. Playback interpolates
    /// between frames; unit vectors are lerped and renormalised, which at the 50 Hz
    /// sample rate is well inside the error of the model itself.
    ///
    /// File I/O deliberately lives outside Kitesim.Core.
    /// </summary>
    public class Track
    {
        private readonly List<TrackFrame> frames = new List<TrackFrame>();

        public IReadOnlyList<TrackFrame> Frames => frames;

        public double Duration => frames.Count == 0 ? 0.0 : frames[frames.Count - 1].Time;

        public int PilotCount
        {
            get
            {
                int highest = -1;
                for (int i = 0; i < frames.Count; i++)
                {
                    if (frames[i].PilotIndex > highest)
                        highest = frames[i].PilotIndex;
                }
                return highest + 1;
            }
        }

        public void Record(double time, int pilotIndex, KiteStateView view)
        {
            frames.Add(new TrackFrame(
                time,
                pilotIndex,
                view.PilotPosition,
                view.Direction,
                view.Heading,
                view.Speed,
                view.Tension,
                view.Landed));
        }

        public void Clear()
        {
            frames.Clear();
        }

        public KiteStateView Sample(double time, int pilotIndex, double lineLength)
        {
            TrackFrame? previous = null;
            TrackFrame? next = null;

            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i].PilotIndex != pilotIndex)
                    continue;

                if (frames[i].Time <= time)
                    previous = frames[i];

                if (frames[i].Time >= time)
                {
                    next = frames[i];
                    break;
                }
            }

            if (previous == null && next == null)
                return new KiteStateView(Vec3.Zero, Vec3.Forward, Vec3.Up, lineLength, 0.0, 0.0, 0, true);

            if (previous == null)
                return ToView(next.Value, lineLength);

            if (next == null)
                return ToView(previous.Value, lineLength);

            TrackFrame a = previous.Value;
            TrackFrame b = next.Value;
            double span = b.Time - a.Time;
            double blend = span < 1e-12 ? 0.0 : MathD.Clamp01((time - a.Time) / span);

            Vec3 direction = Lerp(a.Direction, b.Direction, blend).Normalized();
            Vec3 heading = Lerp(a.Heading, b.Heading, blend);
            heading = heading - direction * Vec3.Dot(heading, direction);
            heading = heading.SqrLength < 1e-12
                ? KiteStateView.TangentUp(direction)
                : heading.Normalized();

            return new KiteStateView(
                Lerp(a.PilotPosition, b.PilotPosition, blend),
                direction,
                heading,
                lineLength,
                MathD.Lerp(a.Speed, b.Speed, blend),
                MathD.Lerp(a.Tension, b.Tension, blend),
                0,
                blend < 0.5 ? a.Landed : b.Landed);
        }

        private static KiteStateView ToView(TrackFrame frame, double lineLength)
        {
            return new KiteStateView(
                frame.PilotPosition,
                frame.Direction,
                frame.Heading,
                lineLength,
                frame.Speed,
                frame.Tension,
                0,
                frame.Landed);
        }

        private static Vec3 Lerp(Vec3 a, Vec3 b, double t)
        {
            return a + (b - a) * t;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd .verify-core && dotnet test --filter TrackTests`
Expected: `Passed! - Failed: 0, Passed: 7`

- [ ] **Step 5: Run the whole suite**

Run: `cd .verify-core && dotnet test`
Expected: `Passed! - Failed: 0, Passed: 57`

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Core/Track.cs Assets/Tests/Editor/TrackTests.cs
git commit -m "M3.4: in-memory track recording and interpolated playback"
```

---

## Task 9: Input actions for hands and walking

Hands take only the **Y** axes of the two sticks. The X axes and both analog triggers were free, so walking goes on the triggers as a single bidirectional axis. Hands are position control, feet are rate control.

**Files:**
- Modify: `Assets/kitesim.inputactions`

**Interfaces:**
- Consumes: nothing
- Produces: an action map named `Player` containing `PullLeft` (Axis), `PullRight` (Axis), `Walk` (Axis), `Launch` (Button), with both Gamepad and Keyboard control schemes

- [ ] **Step 1: Add the Walk action**

In `Assets/kitesim.inputactions`, inside `"actions"`, after the `PullRight` entry and before `Launch`, insert:

```json
                {
                    "name": "Walk",
                    "type": "Value",
                    "id": "1a4a3c6c-0f2e-4d7a-9d1b-4a1b6f9c2e01",
                    "expectedControlType": "Axis",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": true
                },
```

- [ ] **Step 2: Add the trigger and keyboard bindings**

In the same file, inside `"bindings"`, append these four entries at the end of the array (after the existing `space` binding, adding a comma to that entry):

```json
                {
                    "name": "GamepadWalk",
                    "id": "2b5b4d7d-1a3f-4e8b-8e2c-5b2c7a0d3f12",
                    "path": "1DAxis",
                    "interactions": "",
                    "processors": "",
                    "groups": "",
                    "action": "Walk",
                    "isComposite": true,
                    "isPartOfComposite": false
                },
                {
                    "name": "negative",
                    "id": "3c6c5e8e-2b40-4f9c-9f3d-6c3d8b1e4023",
                    "path": "<Gamepad>/leftTrigger",
                    "interactions": "",
                    "processors": "",
                    "groups": "Gamepad",
                    "action": "Walk",
                    "isComposite": false,
                    "isPartOfComposite": true
                },
                {
                    "name": "positive",
                    "id": "4d7d6f9f-3c51-40ad-a04e-7d4e9c2f5134",
                    "path": "<Gamepad>/rightTrigger",
                    "interactions": "",
                    "processors": "",
                    "groups": "Gamepad",
                    "action": "Walk",
                    "isComposite": false,
                    "isPartOfComposite": true
                },
                {
                    "name": "KeyboardWalk",
                    "id": "5e8e701a-4d62-41be-b15f-8e5f0d3a6245",
                    "path": "1DAxis",
                    "interactions": "",
                    "processors": "",
                    "groups": "",
                    "action": "Walk",
                    "isComposite": true,
                    "isPartOfComposite": false
                },
                {
                    "name": "negative",
                    "id": "6f9f812b-5e73-42cf-c260-9f601e4b7356",
                    "path": "<Keyboard>/s",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Walk",
                    "isComposite": false,
                    "isPartOfComposite": true
                },
                {
                    "name": "positive",
                    "id": "70a0923c-6f84-43d0-d371-a0712f5c8467",
                    "path": "<Keyboard>/w",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard",
                    "action": "Walk",
                    "isComposite": false,
                    "isPartOfComposite": true
                }
```

- [ ] **Step 3: Verify the file is valid JSON**

Run: `python3 -c "import json;d=json.load(open('Assets/kitesim.inputactions'));m=d['maps'][0];print([a['name'] for a in m['actions']]);print(len(m['bindings']))"`
Expected: `['PullLeft', 'PullRight', 'Walk', 'Launch']` and `17` (11 existing plus 6 new)

- [ ] **Step 4: Commit**

```bash
git add Assets/kitesim.inputactions
git commit -m "M3.3: add a walk axis on the gamepad triggers and W/S"
```

---

## Task 10: Unity behaviour and per-pilot input

**Files:**
- Create: `Assets/Scripts/Unity/WindowKiteBehaviour.cs`
- Create: `Assets/Scripts/Unity/WindowPilotInput.cs`

**Interfaces:**
- Consumes: `WindowSim`, `WindowKiteState`, `WindowConfiguration`, `KiteStateView`, `WindField`, `Vec3` from `Kitesim.Core`
- Produces:
  - `WindowKiteBehaviour` with `Configure(int pilotIndex, Vec3 startPilotPosition, WindField wind)`, `SetInputs(float handLeft, float handRight, float walkAxis)`, `Launch()`, `PilotIndex -> int`, `View -> KiteStateView`, `Sim -> WindowSim`
  - `WindowPilotInput` — a `MonoBehaviour` used alongside `PlayerInput`, with `Bind(WindowKiteBehaviour kite)`

- [ ] **Step 1: Write WindowKiteBehaviour**

Create `Assets/Scripts/Unity/WindowKiteBehaviour.cs`:

```csharp
using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Drives one kite of the reduced wind-window model. Holds no rendering logic;
    /// the chart renderer reads View each frame.
    /// </summary>
    public class WindowKiteBehaviour : MonoBehaviour
    {
        [SerializeField] private int pilotIndex;
        [SerializeField] private float handLeft;
        [SerializeField] private float handRight;
        [SerializeField] private float walkAxis;

        private WindowSim sim;
        private WindowKiteState state;

        public int PilotIndex => pilotIndex;
        public WindowSim Sim => sim;
        public bool Landed => state != null && state.Landed;
        public KiteStateView View => sim.View(state);

        public void Configure(int index, Vec3 startPilotPosition, WindField wind)
        {
            pilotIndex = index;
            sim = new WindowSim(new WindowConfiguration())
            {
                Wind = wind,
                PilotPosition = startPilotPosition,
                PilotVelocity = Vec3.Zero
            };
            state = WindowKiteState.OnGround(0.0);
        }

        public void SetInputs(float left, float right, float walk)
        {
            handLeft = left;
            handRight = right;
            walkAxis = walk;
        }

        public void Launch()
        {
            sim.Launch(state);
        }

        public void SetWind(WindField wind)
        {
            sim.Wind = wind;
        }

        void FixedUpdate()
        {
            if (sim == null)
                return;

            // Hand axes are positions in metres; the axis is scaled by the
            // configured half-travel. Walk is a rate and passes through raw.
            double travel = sim.Config.HandTravel;
            sim.Step(state, Time.fixedDeltaTime, handLeft * travel, handRight * travel, walkAxis);
        }
    }
}
```

- [ ] **Step 2: Write WindowPilotInput**

Create `Assets/Scripts/Unity/WindowPilotInput.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kitesim.Unity
{
    /// <summary>
    /// Reads one pilot's controls and feeds them to that pilot's kite. Sits on the
    /// same GameObject as a PlayerInput component, so PlayerInputManager pairs one
    /// gamepad per pilot automatically.
    ///
    /// Hands are position control: the stick axis is the hand's fore-aft position,
    /// rate-limited to a human hand speed so that a keyboard key does not teleport
    /// the hand across its whole travel in a single frame. Feet are rate control on
    /// the triggers.
    /// </summary>
    [RequireComponent(typeof(PlayerInput))]
    public class WindowPilotInput : MonoBehaviour
    {
        /// <summary>Maximum hand speed as a fraction of full travel per second.</summary>
        [SerializeField] private float handRatePerSecond = 2.5f / 0.7f;

        [SerializeField] private WindowKiteBehaviour kite;

        private PlayerInput playerInput;
        private InputAction pullLeft;
        private InputAction pullRight;
        private InputAction walk;
        private InputAction launch;

        private float smoothedLeft;
        private float smoothedRight;

        public void Bind(WindowKiteBehaviour target)
        {
            kite = target;
        }

        void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            pullLeft = playerInput.actions.FindAction("PullLeft", true);
            pullRight = playerInput.actions.FindAction("PullRight", true);
            walk = playerInput.actions.FindAction("Walk", true);
            launch = playerInput.actions.FindAction("Launch", true);
        }

        void Update()
        {
            if (kite == null)
                return;

            float step = handRatePerSecond * Time.deltaTime;
            smoothedLeft = Mathf.MoveTowards(smoothedLeft, pullLeft.ReadValue<float>(), step);
            smoothedRight = Mathf.MoveTowards(smoothedRight, pullRight.ReadValue<float>(), step);

            kite.SetInputs(smoothedLeft, smoothedRight, walk.ReadValue<float>());

            if (launch.WasPressedThisFrame())
                kite.Launch();
        }
    }
}
```

- [ ] **Step 3: Verify the core still builds headlessly**

Run: `cd .verify-core && dotnet test`
Expected: `Passed! - Failed: 0, Passed: 57` — the Unity layer is not part of this project, so this only confirms nothing in Core regressed.

- [ ] **Step 4: Verify in Unity**

Open the project in Unity 6000.0.27f1. Check the Console has no compile errors after the scripts import.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Unity/WindowKiteBehaviour.cs Assets/Scripts/Unity/WindowPilotInput.cs
git commit -m "M3.3: window kite behaviour and per-pilot input with hand rate limiting"
```

---

## Task 11: Chart rendering and the 2D scene

The chart is drawn in world space on the XY plane and viewed with an orthographic camera, so one chart unit equals one radian and the window edge sits at `pi/2 ≈ 1.5708`.

**Files:**
- Create: `Assets/Scripts/Unity/WindowChartRenderer.cs`
- Create: `Assets/Scripts/Unity/WindowKiteIcon.cs`
- Create: `Assets/Scripts/Unity/WindowSceneBootstrap.cs`
- Create: `Assets/Scenes/WindowScene.unity` (via the Unity editor)

**Interfaces:**
- Consumes: `WindowChart`, `ChartPoint`, `ChartProjection`, `KiteStateView`, `WindField`; `WindowKiteBehaviour`, `WindowPilotInput`
- Produces:
  - `WindowChartRenderer` with `Configure(WindField wind, ChartProjection projection)`
  - `WindowKiteIcon` with `Bind(WindowKiteBehaviour kite, WindField wind, ChartProjection projection, Color color)`
  - `WindowSceneBootstrap` — a `MonoBehaviour` with no public API, assembles the scene in `Awake`

- [ ] **Step 1: Write WindowChartRenderer**

Create `Assets/Scripts/Unity/WindowChartRenderer.cs`:

```csharp
using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Draws the wind window as a half-disc: the flat bottom is the ground, the arc
    /// is the zero-power edge, and the concentric rings are iso-power contours. The
    /// power at any point depends only on its radius, so the rings are exact rather
    /// than decorative. See docs/kite-anatomy.md section 3.
    /// </summary>
    public class WindowChartRenderer : MonoBehaviour
    {
        [SerializeField] private int ringCount = 5;
        [SerializeField] private int segmentsPerRing = 128;
        [SerializeField] private float lineWidth = 0.006f;

        private WindField wind;
        private ChartProjection projection;

        public void Configure(WindField windField, ChartProjection chartProjection)
        {
            wind = windField;
            projection = chartProjection;
            Rebuild();
        }

        private void Rebuild()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            float edge = (float)WindowChart.EdgeRadius(projection);

            // Iso-power rings at power = i/ringCount. The loop stops before
            // i == ringCount, which would be a degenerate ring of radius zero at the
            // window centre.
            for (int i = 1; i < ringCount; i++)
            {
                double power = (double)i / ringCount;
                double theta = MathD.Acos(MathD.Clamp01(power));
                float radius = (float)WindowChart.RadiusFromTheta(theta, projection);
                float intensity = 0.20f + 0.45f * (float)power;
                CreateArc($"Ring_{i}", radius, new Color(intensity, intensity * 0.75f, 0.15f, 1f));
            }

            CreateArc("Edge", edge, new Color(0.85f, 0.35f, 0.25f, 1f));
            CreateGroundLine(edge);
        }

        /// <summary>Upper half of a circle: the sky part of an iso-power contour.</summary>
        private void CreateArc(string label, float radius, Color color)
        {
            var line = CreateLine(label, color);
            int points = segmentsPerRing / 2 + 1;
            line.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float angle = Mathf.PI * i / (points - 1);
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }

        private void CreateGroundLine(float edge)
        {
            var line = CreateLine("Ground", new Color(0.45f, 0.35f, 0.2f, 1f));
            line.positionCount = 2;
            line.SetPosition(0, new Vector3(-edge, 0f, 0f));
            line.SetPosition(1, new Vector3(edge, 0f, 0f));
        }

        private LineRenderer CreateLine(string label, Color color)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            return line;
        }
    }
}
```

- [ ] **Step 2: Write WindowKiteIcon**

Create `Assets/Scripts/Unity/WindowKiteIcon.cs`:

```csharp
using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// One kite marker on the chart: a triangle pointing along the kite's heading,
    /// tinted by line tension, trailing its recent path.
    /// </summary>
    public class WindowKiteIcon : MonoBehaviour
    {
        [SerializeField] private float size = 0.045f;

        private WindowKiteBehaviour kite;
        private WindField wind;
        private ChartProjection projection;
        private Color baseColor;
        private MeshRenderer meshRenderer;

        public void Bind(WindowKiteBehaviour target, WindField windField,
            ChartProjection chartProjection, Color color)
        {
            kite = target;
            wind = windField;
            projection = chartProjection;
            baseColor = color;
            BuildTriangle();
        }

        private void BuildTriangle()
        {
            var mesh = new Mesh
            {
                vertices = new[]
                {
                    new Vector3(0f, size, 0f),
                    new Vector3(-size * 0.62f, -size * 0.7f, 0f),
                    new Vector3(size * 0.62f, -size * 0.7f, 0f)
                },
                triangles = new[] { 0, 2, 1 }
            };
            mesh.RecalculateNormals();

            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
            meshRenderer.material.color = baseColor;

            var trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 3f;
            trail.startWidth = 0.008f;
            trail.endWidth = 0f;
            trail.material = new Material(Shader.Find("Sprites/Default"));
            trail.startColor = baseColor;
            trail.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        }

        void LateUpdate()
        {
            if (kite == null || kite.Sim == null)
                return;

            KiteStateView view = kite.View;
            ChartPoint point = WindowChart.Project(view.Direction, wind, projection);
            transform.localPosition = new Vector3((float)point.X, (float)point.Y, 0f);

            // Heading measured in the chart plane: psi = 0 points to the top of the
            // chart, +90 degrees to the right, which is the same convention the
            // chart itself uses.
            float headingDegrees = (float)(view.HeadingRad * MathD.Rad2Deg);
            transform.localRotation = Quaternion.Euler(0f, 0f, -headingDegrees);

            // Tension shading: pale when the kite is loafing at the edge, saturated
            // in the power zone.
            float load = Mathf.Clamp01((float)(view.Tension / 120.0));
            meshRenderer.material.color = Color.Lerp(
                new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f), baseColor, load);
        }
    }
}
```

- [ ] **Step 3: Write WindowSceneBootstrap**

Create `Assets/Scripts/Unity/WindowSceneBootstrap.cs`:

```csharp
using UnityEngine;
using UnityEngine.InputSystem;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Assembles the 2D wind-window trainer: an orthographic camera on the chart
    /// plane, the chart itself, and one kite plus one PlayerInput per pilot.
    /// PlayerInputManager pairs a gamepad to each pilot as it joins.
    /// </summary>
    public class WindowSceneBootstrap : MonoBehaviour
    {
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private int pilotCount = 4;
        [SerializeField] private double windSpeed = 5.0;
        [SerializeField] private ChartProjection projection = ChartProjection.Equidistant;

        /// <summary>Real spacing between pilots on the ground, metres.</summary>
        [SerializeField] private float pilotSpacing = 4f;

        private static readonly Color[] PilotColors =
        {
            new Color(0.90f, 0.25f, 0.25f),
            new Color(0.25f, 0.55f, 0.95f),
            new Color(0.30f, 0.80f, 0.40f),
            new Color(0.95f, 0.75f, 0.20f)
        };

        void Awake()
        {
            var wind = new WindField { Speed = windSpeed, Direction = Vec3.Forward };

            CreateCamera(projection);
            CreateChart(wind, projection);

            var iconRoot = new GameObject("Kites").transform;

            for (int i = 0; i < pilotCount; i++)
            {
                Vec3 start = new Vec3((i - (pilotCount - 1) * 0.5) * pilotSpacing, 0.0, 0.0);
                var kite = CreateKite(i, start, wind);
                CreateIcon(i, kite, wind, iconRoot);
                CreateInput(i, kite);
            }
        }

        private void CreateCamera(ChartProjection chartProjection)
        {
            var go = new GameObject("ChartCamera");
            var cam = go.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.orthographic = true;
            cam.orthographicSize = (float)WindowChart.EdgeRadius(chartProjection) * 1.12f;
            cam.transform.position = new Vector3(0f, (float)WindowChart.EdgeRadius(chartProjection) * 0.5f, -10f);
            cam.backgroundColor = new Color(0.07f, 0.09f, 0.12f);
            cam.clearFlags = CameraClearFlags.SolidColor;
        }

        private void CreateChart(WindField wind, ChartProjection chartProjection)
        {
            var go = new GameObject("WindowChart");
            var renderer = go.AddComponent<WindowChartRenderer>();
            renderer.Configure(wind, chartProjection);
        }

        private WindowKiteBehaviour CreateKite(int index, Vec3 start, WindField wind)
        {
            var go = new GameObject($"Kite_{index}");
            var kite = go.AddComponent<WindowKiteBehaviour>();
            kite.Configure(index, start, wind);
            return kite;
        }

        private void CreateIcon(int index, WindowKiteBehaviour kite, WindField wind, Transform parent)
        {
            var go = new GameObject($"Icon_{index}");
            go.transform.SetParent(parent, false);
            var icon = go.AddComponent<WindowKiteIcon>();
            icon.Bind(kite, wind, projection, PilotColors[index % PilotColors.Length]);
        }

        private void CreateInput(int index, WindowKiteBehaviour kite)
        {
            var go = new GameObject($"Pilot_{index}");
            var playerInput = go.AddComponent<PlayerInput>();
            playerInput.actions = inputActions;
            playerInput.defaultActionMap = "Player";

            var pilot = go.AddComponent<WindowPilotInput>();
            pilot.Bind(kite);
        }
    }
}
```

- [ ] **Step 4: Create the scene**

In the Unity editor:
1. `File > New Scene > Basic (Built-in)`, save as `Assets/Scenes/WindowScene.unity`.
2. Delete the default `Main Camera` and `Directional Light`.
3. Create an empty GameObject named `Bootstrap`, add the `WindowSceneBootstrap` component.
4. Assign `Assets/kitesim.inputactions` to its `Input Actions` field.
5. Add `WindowScene` to `File > Build Profiles > Scene List`.

- [ ] **Step 5: Verify by running**

Press Play. Expected:
- a half-disc chart with four iso-power rings, a red edge arc and a ground line;
- four coloured triangles sitting at the bottom centre of the chart, on the ground line;
- pressing the launch button (`Space` or gamepad South) makes a kite climb;
- pulling the right stick back turns that kite to the right of the chart;
- holding the right trigger (walk downwind) makes the kite slow down; holding the left trigger (walk upwind) speeds it up;
- flying to the arc slows the kite almost to a stop.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Unity/WindowChartRenderer.cs Assets/Scripts/Unity/WindowKiteIcon.cs Assets/Scripts/Unity/WindowSceneBootstrap.cs Assets/Scenes/WindowScene.unity Assets/Scenes/WindowScene.unity.meta
git commit -m "M3.3: 2D wind-window scene with four gamepad pilots"
```

---

## Task 12: Track recording UI and file I/O

**Files:**
- Create: `Assets/Scripts/Unity/WindowTrackRecorder.cs`
- Modify: `Assets/Scripts/Unity/WindowSceneBootstrap.cs`

**Interfaces:**
- Consumes: `Track`, `TrackFrame`, `KiteStateView` from `Kitesim.Core`; `WindowKiteBehaviour`
- Produces: `WindowTrackRecorder` with `Register(WindowKiteBehaviour kite)`, `StartRecording()`, `StopRecording()`, `StartPlayback()`, `Stop()`, `Save(string path)`, `Load(string path)`, `IsRecording -> bool`, `IsPlaying -> bool`, `PlaybackViews -> IReadOnlyList<KiteStateView>`

- [ ] **Step 1: Write WindowTrackRecorder**

Create `Assets/Scripts/Unity/WindowTrackRecorder.cs`:

```csharp
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Records every pilot's KiteStateView at a fixed rate and replays it. The track
    /// is model-independent, which is what lets a routine flown on the 2D chart be
    /// replayed later in the 3D scene.
    ///
    /// File format is CSV so a track can be inspected and diffed by hand.
    /// </summary>
    public class WindowTrackRecorder : MonoBehaviour
    {
        [SerializeField] private double sampleRate = 50.0;

        private readonly List<WindowKiteBehaviour> kites = new List<WindowKiteBehaviour>();
        private readonly List<KiteStateView> playbackViews = new List<KiteStateView>();
        private readonly Track track = new Track();

        private double time;
        private double nextSample;

        public bool IsRecording { get; private set; }
        public bool IsPlaying { get; private set; }
        public Track Track => track;
        public IReadOnlyList<KiteStateView> PlaybackViews => playbackViews;

        public void Register(WindowKiteBehaviour kite)
        {
            kites.Add(kite);
        }

        public void StartRecording()
        {
            track.Clear();
            time = 0.0;
            nextSample = 0.0;
            IsRecording = true;
            IsPlaying = false;
        }

        public void StopRecording()
        {
            IsRecording = false;
        }

        public void StartPlayback()
        {
            if (track.Frames.Count == 0)
                return;

            time = 0.0;
            IsRecording = false;
            IsPlaying = true;

            playbackViews.Clear();
            for (int i = 0; i < track.PilotCount; i++)
                playbackViews.Add(default);
        }

        public void Stop()
        {
            IsRecording = false;
            IsPlaying = false;
        }

        void Update()
        {
            if (IsRecording)
            {
                time += Time.deltaTime;
                if (time >= nextSample)
                {
                    for (int i = 0; i < kites.Count; i++)
                        track.Record(time, kites[i].PilotIndex, kites[i].View);

                    nextSample += 1.0 / sampleRate;
                }
                return;
            }

            if (IsPlaying)
            {
                time += Time.deltaTime;
                if (time > track.Duration)
                {
                    time = 0.0;
                }

                double lineLength = kites.Count > 0 ? kites[0].Sim.Config.LineLength : 45.0;
                for (int i = 0; i < playbackViews.Count; i++)
                    playbackViews[i] = track.Sample(time, i, lineLength);
            }
        }

        public void Save(string path)
        {
            var culture = CultureInfo.InvariantCulture;
            using (var writer = new StreamWriter(path))
            {
                writer.WriteLine("time,pilot,px,py,pz,dx,dy,dz,hx,hy,hz,speed,tension,landed");
                foreach (TrackFrame f in track.Frames)
                {
                    writer.WriteLine(string.Join(",",
                        f.Time.ToString("R", culture),
                        f.PilotIndex.ToString(culture),
                        f.PilotPosition.X.ToString("R", culture),
                        f.PilotPosition.Y.ToString("R", culture),
                        f.PilotPosition.Z.ToString("R", culture),
                        f.Direction.X.ToString("R", culture),
                        f.Direction.Y.ToString("R", culture),
                        f.Direction.Z.ToString("R", culture),
                        f.Heading.X.ToString("R", culture),
                        f.Heading.Y.ToString("R", culture),
                        f.Heading.Z.ToString("R", culture),
                        f.Speed.ToString("R", culture),
                        f.Tension.ToString("R", culture),
                        f.Landed ? "1" : "0"));
                }
            }
            Debug.Log($"[Track] saved {track.Frames.Count} frames to {path}");
        }

        public void Load(string path)
        {
            var culture = CultureInfo.InvariantCulture;
            track.Clear();

            string[] lines = File.ReadAllLines(path);
            for (int i = 1; i < lines.Length; i++)
            {
                string[] c = lines[i].Split(',');
                if (c.Length < 14)
                    continue;

                var view = new KiteStateView(
                    new Vec3(double.Parse(c[2], culture), double.Parse(c[3], culture), double.Parse(c[4], culture)),
                    new Vec3(double.Parse(c[5], culture), double.Parse(c[6], culture), double.Parse(c[7], culture)),
                    new Vec3(double.Parse(c[8], culture), double.Parse(c[9], culture), double.Parse(c[10], culture)),
                    45.0,
                    double.Parse(c[11], culture),
                    double.Parse(c[12], culture),
                    0,
                    c[13] == "1");

                track.Record(double.Parse(c[0], culture), int.Parse(c[1], culture), view);
            }
            Debug.Log($"[Track] loaded {track.Frames.Count} frames from {path}");
        }

        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 200, 130), GUI.skin.box);
            GUILayout.Label(IsRecording ? "REC" : IsPlaying ? "PLAY" : "IDLE");
            GUILayout.Label($"frames: {track.Frames.Count}");

            if (!IsRecording && GUILayout.Button("Record"))
                StartRecording();
            if (IsRecording && GUILayout.Button("Stop"))
                StopRecording();
            if (!IsRecording && track.Frames.Count > 0 && GUILayout.Button("Play"))
                StartPlayback();
            if (!IsRecording && track.Frames.Count > 0 && GUILayout.Button("Save"))
                Save(Path.Combine(Application.persistentDataPath, "track.csv"));

            GUILayout.EndArea();
        }
    }
}
```

- [ ] **Step 2: Wire the recorder into the scene**

In `Assets/Scripts/Unity/WindowSceneBootstrap.cs`, inside `Awake`, replace the pilot loop with:

```csharp
            var recorderObject = new GameObject("TrackRecorder");
            var recorder = recorderObject.AddComponent<WindowTrackRecorder>();

            for (int i = 0; i < pilotCount; i++)
            {
                Vec3 start = new Vec3((i - (pilotCount - 1) * 0.5) * pilotSpacing, 0.0, 0.0);
                var kite = CreateKite(i, start, wind);
                CreateIcon(i, kite, wind, iconRoot);
                CreateInput(i, kite);
                recorder.Register(kite);
            }
```

- [ ] **Step 3: Verify by running**

Press Play. Expected:
- an `IDLE` panel top-left with a `Record` button;
- pressing `Record` shows `REC` and the frame count climbing at roughly 50 per second;
- pressing `Stop` then `Play` shows `PLAY`;
- pressing `Save` logs a path to the Console, and that file opens as CSV with a `time,pilot,...` header.

- [ ] **Step 4: Run the whole core suite one more time**

Run: `cd .verify-core && dotnet test`
Expected: `Passed! - Failed: 0, Passed: 57`

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Unity/WindowTrackRecorder.cs Assets/Scripts/Unity/WindowSceneBootstrap.cs
git commit -m "M3.4: track recording, playback and CSV export"
```

---

## Task 13: Field strip, pilot markers and tension rumble

The chart shows each kite's angular position **relative to its own pilot**, so a walking pilot does not move on it. The spec requires the pilot's ground displacement to be shown separately, otherwise the geometry of the field lies. This task adds a top-down field strip below the chart, plus the boundary marking from the rulebook and the tension rumble.

**Files:**
- Create: `Assets/Scripts/Unity/WindowFieldStrip.cs`
- Modify: `Assets/Scripts/Unity/WindowSceneBootstrap.cs`
- Modify: `Assets/Scripts/Unity/WindowPilotInput.cs`

**Interfaces:**
- Consumes: `WindowKiteBehaviour`, `WindowChart`, `ChartProjection`, `Vec3`
- Produces: `WindowFieldStrip` with `Configure(ChartProjection projection, double fieldHalfSize)` and `Register(WindowKiteBehaviour kite, Color color)`

- [ ] **Step 1: Write WindowFieldStrip**

Create `Assets/Scripts/Unity/WindowFieldStrip.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Top-down view of the competition field drawn under the chart. The chart
    /// itself is relative to each pilot, so walking does not move a kite on it; this
    /// strip is where the pilot's own displacement becomes visible, together with
    /// the red outer and yellow inner boundaries from ISKRB section IV.D.1.
    /// </summary>
    public class WindowFieldStrip : MonoBehaviour
    {
        private const double InnerMargin = 3.0;   // metres, ISKRB minimum

        [SerializeField] private float stripHeight = 0.55f;
        [SerializeField] private float lineWidth = 0.006f;

        private readonly List<WindowKiteBehaviour> kites = new List<WindowKiteBehaviour>();
        private readonly List<Transform> markers = new List<Transform>();

        private double fieldHalfSize = 45.0;
        private float scale;
        private float centreY;

        public void Configure(ChartProjection projection, double halfSize)
        {
            fieldHalfSize = halfSize;
            scale = stripHeight / (float)(2.0 * halfSize);
            centreY = -(float)WindowChart.EdgeRadius(projection) * 0.18f - stripHeight * 0.5f;

            DrawRectangle("OuterBoundary", halfSize, new Color(0.85f, 0.2f, 0.2f));
            DrawRectangle("InnerBoundary", halfSize - InnerMargin, new Color(0.85f, 0.8f, 0.2f));
        }

        public void Register(WindowKiteBehaviour kite, Color color)
        {
            kites.Add(kite);

            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = $"PilotMarker_{kite.PilotIndex}";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.022f;
            go.GetComponent<MeshRenderer>().material =
                new Material(Shader.Find("Sprites/Default")) { color = color };

            markers.Add(go.transform);
        }

        private void DrawRectangle(string label, double halfSize, Color color)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.startWidth = lineWidth;
            line.endWidth = lineWidth;
            line.material = new Material(Shader.Find("Sprites/Default"));
            line.startColor = color;
            line.endColor = color;
            line.positionCount = 4;

            float h = (float)halfSize * scale;
            line.SetPosition(0, new Vector3(-h, centreY - h, 0f));
            line.SetPosition(1, new Vector3(h, centreY - h, 0f));
            line.SetPosition(2, new Vector3(h, centreY + h, 0f));
            line.SetPosition(3, new Vector3(-h, centreY + h, 0f));
        }

        void LateUpdate()
        {
            for (int i = 0; i < kites.Count && i < markers.Count; i++)
            {
                if (kites[i].Sim == null)
                    continue;

                Vec3 pilot = kites[i].Sim.PilotPosition;
                markers[i].localPosition = new Vector3(
                    (float)pilot.X * scale,
                    centreY + (float)pilot.Z * scale,
                    -0.01f);
            }
        }

        /// <summary>True when the pilot has reached the inner, yellow boundary.</summary>
        public bool IsNearBoundary(WindowKiteBehaviour kite)
        {
            Vec3 pilot = kite.Sim.PilotPosition;
            double limit = fieldHalfSize - InnerMargin;
            return MathD.Abs(pilot.X) >= limit || MathD.Abs(pilot.Z) >= limit;
        }
    }
}
```

- [ ] **Step 2: Add tension rumble to WindowPilotInput**

In `Assets/Scripts/Unity/WindowPilotInput.cs`, add this field next to the others:

```csharp
        /// <summary>Line tension in newtons that corresponds to full rumble.</summary>
        [SerializeField] private float rumbleFullScaleNewtons = 120f;
```

And add this method, calling it from the end of `Update`:

```csharp
        private void ApplyRumble()
        {
            var pad = playerInput.devices.Count > 0 ? playerInput.devices[0] as Gamepad : null;
            if (pad == null)
                return;

            float load = kite.Landed
                ? 0f
                : Mathf.Clamp01((float)kite.View.Tension / rumbleFullScaleNewtons);

            pad.SetMotorSpeeds(load * 0.25f, load * 0.12f);
        }
```

Add `ApplyRumble();` as the last statement of `Update`, and add this to stop the motors when play ends:

```csharp
        void OnDisable()
        {
            var pad = playerInput != null && playerInput.devices.Count > 0
                ? playerInput.devices[0] as Gamepad
                : null;
            pad?.ResetHaptics();
        }
```

- [ ] **Step 3: Wire the strip into the scene and widen the camera**

In `Assets/Scripts/Unity/WindowSceneBootstrap.cs`:

Replace the two camera lines in `CreateCamera` with:

```csharp
            float edge = (float)WindowChart.EdgeRadius(chartProjection);
            cam.orthographicSize = edge * 1.32f;
            cam.transform.position = new Vector3(0f, edge * 0.36f, -10f);
```

In `Awake`, after `CreateChart(...)`, add:

```csharp
            var stripObject = new GameObject("FieldStrip");
            var strip = stripObject.AddComponent<WindowFieldStrip>();
            strip.Configure(projection, new WindowConfiguration().FieldHalfSize);
```

And inside the pilot loop, after `recorder.Register(kite);`, add:

```csharp
                strip.Register(kite, PilotColors[i % PilotColors.Length]);
```

- [ ] **Step 4: Verify by running**

Press Play. Expected:
- a red square with a yellow square inside it, below the chart;
- one coloured dot per pilot in the middle of that square;
- holding a trigger moves that pilot's dot up or down the square and the kite's speed changes;
- the dot stops at the red square and the kite stops gaining speed.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Unity/WindowFieldStrip.cs Assets/Scripts/Unity/WindowPilotInput.cs Assets/Scripts/Unity/WindowSceneBootstrap.cs
git commit -m "M3.3: field strip with pilot markers, boundaries and tension rumble"
```

---

## Acceptance for M3

All of these must hold before M4 starts:

1. `cd .verify-core && dotnet test` reports 57 passing tests.
2. In `WindowScene`, four pilots can fly simultaneously on four gamepads.
3. Kite speed at the window centre in 5 m/s wind settles between 25 and 35 m/s.
4. Kite speed at the edge arc falls below 3 m/s.
5. A slow kite flying across the window is turned toward the ground at over a radian per second, and walking upwind raises its speed by more than half. Together these are what makes light wind hard.
6. A recorded routine replays with the kites following the same paths.
7. The pilot markers move on the field strip when the triggers are held and stop at the red boundary.

## Known limitations to state in the UI

- The reduced model assumes the kite always flies nose-first. No stalls, no slack lines, no hovering, no trick elements.
- The zenith and the window edge carry a 5 % power floor because the `cos(theta)` law gives exactly zero there while a real kite still flies a little.
- Line wraps are counted and displayed but have no effect on the physics.
- Gravity affects the heading but not the speed along the flight path, so a kite pointed straight at the zenith climbs however light the wind. Crosswind flight, which is what a routine is made of, does behave correctly, because there the gravity turn dominates. Adding an along-track gravity term is an M4 decision: in the 6-DOF model it comes for free.
- The same inputs fed to the 2D model and to the future 3D model will **not** produce the same trajectory. View switching is provided by replaying a track, not by re-simulating.
