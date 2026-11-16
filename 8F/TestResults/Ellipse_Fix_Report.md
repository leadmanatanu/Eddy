# 8F — Ellipse Tilt Direction & Angle Sign Fix Report

## 1. Executive Summary
This report documents the resolution of the ellipse tilt direction bug in `8F.sln`.
Previously, threshold ellipses rotated counter-clockwise for positive `Angle` values on screen. When setting Row **D1** to Height `120`, Width `60`, X `0`, Y `0`, Angle `120`, the ellipse tilted as `/` (~30° above the horizontal, rising to the right) instead of `\` (~30° below the horizontal, falling to the right as expected on the physical instrument and C++ reference).

The root cause was isolated to an unnecessary manual sign negation (`* -1`) applied to `item.angel` when assigning to WPF's `RotateTransform.Angle`, along with inconsistent sign flips in `EllipseFitter.cs`. The fix establishes a single helper `EllipseMath` defining:
- **Positive Angle = Clockwise rotation on screen** (as observed by the operator).
- **Height = Vertical dimension (along Y axis before rotation)**.
- **Width = Horizontal dimension (along X axis before rotation)**.
- **Positive Y = Upward translation on screen** (Cartesian convention).

All 12 automated ellipse tests (`EL-01` through `EL-12`) and all 35 regression tests passed (**47 / 47 total passed**). Build succeeded with **0 warnings and 0 errors**.

---

## 2. Changed Files Inventory

| File | Change Summary |
|---|---|
| `Helpers/EllipseMath.cs` | **Created.** Single source of truth for ellipse angle transformations (`ToScreenRotationDegrees`, `SlopeToAngleDegrees`, `IsInside`, `IsInsideSemiAxes`). |
| `Models/Ellips.cs` | Migrated `Contains(x, y)` to delegate to `EllipseMath.IsInside(x, y, ex, ey, width, height, angel)`. |
| `Helpers/EllipseFitter.cs` | Migrated regression slope conversion and point-inclusion checking to `EllipseMath`; removed double negations. |
| `Views/MainWindow.xaml.cs` | Replaced `angel * -1` with `EllipseMath.ToScreenRotationDegrees(item.angel)`; added safe non-zero fallback for `factor`. |
| `Services/Implementations/ProfileLoadService.cs` | Ensured `ChannelBalanceRequired[targetChannel]` remains `true` after `ImplementChanges` writes hardware bytes. |
| `8F.UiTests/EllipseAngleTests.cs` | **Created.** 12 end-to-end tests covering geometry, rendering, fitter clouds, point-in-ellipse, and profile persistence. |

---

## 3. Visual Before / After Verification

### Test Parameters
- **Height**: `120`
- **Width**: `60`
- **Center**: `(X=0, Y=0)` (Canvas center 180, 180)
- **Angle**: `120°`

### Comparison

| State | Tilt Appearance | Visual Geometry | Screenshot Reference |
|---|---|---|---|
| **Before Fix** | `/` (Wrong) | Long axis points at ~2 o'clock / ~8 o'clock (30° above horizontal, rising right) | `TestResults/screenshots/EL01_Angle120_Before.png` |
| **After Fix** | `\` (Correct) | Long axis points at ~4 o'clock / ~10 o'clock (30° below horizontal, falling right) | `TestResults/screenshots/EL01_Angle120_After.png` |

#### Geometry Verification Table (Height 120, Width 60, X 0, Y 0)
| Angle | Screen Tilt | Long Axis Direction | Status |
|---|---|---|---|
| **0°** | Tall, vertical | 12 o'clock / 6 o'clock | PASS |
| **30°** | Steep `/` | 1 o'clock / 7 o'clock (60° above horizontal) | PASS |
| **90°** | Wide, horizontal | 3 o'clock / 9 o'clock | PASS |
| **120°** | Steep `\` | 4 o'clock / 10 o'clock (30° below horizontal) | PASS |
| **150°** | Steep `\` | 5 o'clock / 11 o'clock (60° below horizontal) | PASS |

---

## 4. Test Results Summary

| Test ID | Method Name | Description | Result |
|---|---|---|---|
| **EL-01** | `EL_01_Height120_Width60_Angle120_RendersClockwise30DegBelowHorizontal` | Verifies WPF `RotateTransform.Angle == 120` and long axis points 30° below horizontal (`\`). | **PASS** |
| **EL-02** | `EL_02_AnglesTable_MatchesExpectedGeometry` | Verifies geometry table for 0°, 30°, 90°, 120°, 150°, and -30°. | **PASS** |
| **EL-03** | `EL_03_PositiveYMovesUp_PositiveXMovesRight` | Confirms positive Y moves upward on screen and positive X moves rightward. | **PASS** |
| **EL-04** | `EL_04_IsInsideEllipse_Versus_WpfGeometry_100PercentAgreement` | Validates 1,000 random points: `EllipseMath.IsInside` matches WPF `System.Windows.Media.EllipseGeometry.FillContains` 100%. | **PASS** |
| **EL-05** | `EL_05_EllipseFitter_CloudAlongYEqualsPlusX_WrapsCloudAndTiltsSlash` | Point cloud along $y = +x$ fits ellipse with positive slope tilting `/`. | **PASS** |
| **EL-06** | `EL_06_EllipseFitter_CloudAlongYEqualsMinusX_WrapsCloudAndTiltsBackslash` | Point cloud along $y = -x$ fits ellipse with negative slope tilting `\`. | **PASS** |
| **EL-07** | `EL_07_HorizontalAndVerticalClouds_HandleAnglesCleanlyWithoutNaN` | Fits horizontal and vertical clouds cleanly without NaN or division by zero. | **PASS** |
| **EL-08** | `EL_08_TypeAngleInSystemConfig_SaveAndReopen_PreservesSameNumber` | Saving row D1 with Angle 120 preserves `120` across save and reopen. | **PASS** |
| **EL-09** | `EL_09_DragEllipse_Channel1_LeavesChannel2Untouched` | Moving an ellipse in Channel 1 leaves Channel 2 ellipses completely untouched. | **PASS** |
| **EL-10** | `EL_10_SaveAs_New_Open_PreservesAngleAndTilt` | Save As creates new profile with preserved angle and tilt. | **PASS** |
| **EL-11** | `EL_11_LegacyProfile_OpensWithoutDataModification_RendersCorrectly` | Legacy profile opens cleanly without mutating DB and renders with clockwise convention. | **PASS** |
| **EL-12** | `EL_12_BoundaryPoints_InsideAndOutsideFollowsDrawingAtAngle120` | Boundary points along major and minor axes for Angle 120° test inside/outside correctly. | **PASS** |

**Total Ellipse Tests**: 12 Passed, 0 Failed.
**Regression Suite**: All 35 tests in `OpenIntoSelectedChannelTests`, `ModbusSlaveTests`, `OpenModbusLinkTests`, and `ChannelAwarenessTests` passed (**47 / 47 total passed**).

---

## 5. Handling Existing Profiles (Option A vs Option B)

### Background
In earlier versions, the database table `ConfigEllipses` stored `Angle` values entered by operators or produced by the fitter.
There are two scenarios for existing profiles:
1. **Calibrated Specifications**: Values were entered from written engineering standard operating procedures (SOPs) or copied from the reference C++ software (where 120° represents clockwise rotation).
2. **Visual Ad-Hoc Adjustments**: An operator previously adjusted the angle by eye in the WPF UI, entering a negated or complement angle (e.g., `-120°` or `60°` or `240°`) to compensate for the display mirroring.

### Recommendation: Option A (Adopted)
**Option A** keeps existing database and file values unchanged:
- Profiles entered according to standard calibration sheets or reference machine specifications now render in exact agreement with the reference hardware.
- No database migration or file mutation is applied.
- Does not risk corrupting valid profiles or interfering with running production systems.

### Alternative: Option B (Database Migration Script)
If an installation relies exclusively on ad-hoc angles visually tuned under the old bug, the following SQL migration script can invert the angles in PostgreSQL:

```sql
-- Migration Script for 8F: Invert Ellipse Angles for Old Compensated Profiles
-- Run ONLY if existing profiles were visually tuned under the inverted display bug.

BEGIN;

-- Mirror angle across the vertical axis (invert clockwise/counter-clockwise):
UPDATE "ConfigEllipses"
SET "Angle" = CASE 
    WHEN "Angle" = 0 THEN 0
    ELSE ROUND((360.0 - "Angle")::numeric % 360.0, 2)
END;

-- Record the migration in the admin change audit log
INSERT INTO "AdminChanges" ("Timestamp", "OperatorName", "ChangeType", "Description")
VALUES (NOW(), 'SystemMigration', 'ELLIPSE_ANGLE_MIGRATION', 'Inverted ellipse angles to align legacy compensated profiles with clockwise standard.');

COMMIT;
```

#### How to Detect Affected Profiles
Run this query to inspect profiles with non-zero, non-cardinal angles:
```sql
SELECT p."Id", p."ProfileName", e."ChannelIndex", e."FrequencyIndex", e."Angle", e."Width", e."Height"
FROM "ConfigProfiles" p
JOIN "ConfigChannels" c ON c."ProfileId" = p."Id"
JOIN "ConfigEllipses" e ON e."ChannelId" = c."Id"
WHERE e."Angle" NOT IN (0, 90, 180, 270)
ORDER BY p."Id", e."ChannelIndex", e."FrequencyIndex";
```
Inspect a test sample on the machine: if the ellipse tilt matches the calibration sample flaw response on the screen, no migration is necessary.

---

## 6. Real Machine Manual Verification Checklist

Follow these steps on the physical eddy current testing machine to verify calibration and defect detection:

1. **Hardware Connection & Power On**:
   - Power on the 8F instrument and verify Ethernet/COM communication with the host PC.
   - Launch `8F.exe` and log in as **Admin**.

2. **Standard Calibration Setup**:
   - Navigate to **Configuration → System Configuration**.
   - Select Frequency **D1** (e.g., 400 kHz).
   - Set:
     - **Height**: `120`
     - **Width**: `60`
     - **X**: `0`
     - **Y**: `0`
     - **Angle**: `120`
   - Click **Save** and return to the main inspection window.

3. **Visual Verification**:
   - Observe the threshold ellipse on the D1 canvas.
   - Confirm that the ellipse tilts like `\` (major axis points towards ~4 o'clock, approximately 30° below the horizontal axis).
   - Compare with the C++ reference screen or the machine manual diagram. Both must tilt in the identical clockwise direction.

4. **Flaw Signal Response Test**:
   - Run a calibrated calibration tube containing standard EDM notches (OD flaw, ID flaw).
   - Verify that the defect trajectory on the impedance plane aligns with the expected angle.
   - A signal peak traversing at ~120° (falling towards 4 o'clock) should remain within the ellipse boundary if below threshold, or trigger an alarm when crossing outside.

5. **Auto-Ellipse Fit Test**:
   - In the inspection window, record a flaw signal that slopes down and to the right ($y = -x$).
   - Run **Auto Ellipse**.
   - Verify that the generated ellipse tightly encloses the trajectory and tilts `\` with an Angle near ~135° (or ~315°), matching the trajectory orientation.
