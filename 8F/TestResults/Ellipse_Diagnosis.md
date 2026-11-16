# 8F — Ellipse Tilt Direction & Angle Sign Diagnosis

## 1. Executive Summary
In the 8F solution (`8F.sln`), threshold ellipses drawn on each frequency canvas (D1 to D8) were rotating counter-clockwise for positive `Angle` inputs. When an operator configured row **D1** to Height `120`, Width `60`, X `0`, Y `0`, and Angle `120`, the ellipse tilted like `/` (rising to the right, long axis ~30° above the horizontal), whereas the physical instrument and C++ reference tilt like `\` (falling to the right, long axis ~30° below the horizontal).

Investigation reveals that:
1. **The Y-coordinate mapping is correct**: Setting $Y = +40$ translates the ellipse **upwards** on screen via `tt1.Y = (((item.ey * -1) - (item.height / 2)) / factor)`. Because WPF screen Y increases downwards, multiplying data Y by `-1` correctly maps positive Cartesian Y to upward screen movement.
2. **The Angle sign was explicitly negated in drawing**: In WPF, `RotateTransform.Angle` natively rotates clockwise on screen. However, `MainWindow.xaml.cs` explicitly negated the stored angle at lines 2033, 2052, and 2084 (`rtAngel1.Angle = item.angel * -1`). This turned the native clockwise rotation into a counter-clockwise rotation on screen.
3. **`EllipseFitter` and `IsInsideEllipse` contained contradictory sign negations**:
   - `EllipseFitter.cs` line 95 negated `thetaRad`: `thetaDeg = -(thetaRad * (180.0 / Math.PI))`.
   - Line 295 negated it again when returning: `RotationAngle = -(Math.Round(thetaDeg, 2))`.
   - Line 255 evaluated points using un-negated `thetaRad`.
   - `IsInsideEllipse` assumed counter-clockwise rotation with $a$ on the X axis, while drawing assumed $b$ on the Y axis with negated angle.

---

## 2. Inventory of Angle and Geometry References

### Group 1: Drawing (Canvas Rendering & UI Display)
| File | Line(s) | Description / Code | Sign Convention |
|---|---|---|---|
| `Views/MainWindow.xaml.cs` | 2031–2033 | `rtAngel11.Angle = graphData.angel_O * -1;` (Static overlay ellipse in box 1) | Negated (`* -1`), causing CCW rotation |
| `Views/MainWindow.xaml.cs` | 2049–2052 | `rtAngel1_1.Angle = graphData.angel_O * -1;` (Overlay ellipse in boxes 2–8) | Negated (`* -1`), causing CCW rotation |
| `Views/MainWindow.xaml.cs` | 2081–2084 | `rtAngel1.Angle = item.angel * -1;` (Primary threshold ellipse in all frequency boxes) | Negated (`* -1`), causing CCW rotation |
| `Views/MainWindow.xaml.cs` | 2185–2215 | `Ellipse_MouseMove` (Drag handler modifies `ex` and `ey`; preserves `angel`) | Neutral to angle; consistent with screen $(X, -Y)$ |
| `Views/MainWindow.xaml` | 262 | `<RotateTransform x:Name="rtAngel11" Angle="0"/>` | WPF native (Positive = CW) |
| `Views/CircleSetting.xaml` | 56 | `<DataGridTextColumn Header="Angle" Binding="{Binding angel}"/>` | Displays numeric degree value |
| `ViewModels/CircleSettingViewModel.cs` | 170, 181, 194 | Reads `item.angel`, stores to `graph.angel`, sends `t = el.angel` to hardware | Direct pass-through |
| `Views/Logs.xaml.cs` | 408 | `table.Cell()...Text(d.angel.ToString("0.##"))` | Tabular numeric export (QuestPDF) |
| `Views/RenewBatchWiseLog.xaml.cs` | 525 | `table.Cell()...Text(d.angel.ToString("0.##"))` | Tabular numeric export (QuestPDF) |

### Group 2: Decision Logic (Defect Detection & Point-in-Ellipse)
| File | Line(s) | Description / Code | Sign Convention |
|---|---|---|---|
| `Helpers/EllipseFitter.cs` | 305–318 | `IsInsideEllipse(x, y, a, b, Xc, Yc, thetaRad)` | Rotates $(dx, dy)$ by $\theta_{\text{rad}}$ into ellipse frame with semi-axis $a$ along X |
| `Models/Ellips.cs` | 38–55 | `Contains(double x, double y)` | $a = \text{width}/2$, $b = \text{height}/2$; rotated by $\text{angel} \times \pi / 180$ |
| `Views/MainWindow.xaml.cs` | 2717–2842 | `if (fd.R == 1) rResult1.Fill = Green; else Red;` | Defect evaluation evaluated on instrument hardware using transmitted $(a, b, t, x, y)$ |

### Group 3: Calculation (Curve Fitting & Line Slopes)
| File | Line(s) | Description / Code | Sign Convention |
|---|---|---|---|
| `Helpers/EllipseFitter.cs` | 94 | `double thetaRad = Math.Atan2(slope, 1.0);` | Cartesian line angle from fitted slope |
| `Helpers/EllipseFitter.cs` | 95 | `double thetaDeg = -(thetaRad * (180.0 / Math.PI));` | First negation (Cartesian CCW to screen CW) |
| `Helpers/EllipseFitter.cs` | 255 | `if (!IsInsideEllipse(px, py, a, b, Xc, Yc, thetaRad))` | Evaluates points using raw `thetaRad` |
| `Helpers/EllipseFitter.cs` | 295 | `RotationAngle = -(Math.Round(thetaDeg, 2));` | Second negation, flipping angle back to $+ \text{Cartesian}$ |
| `ViewModels/AutoEllipseViewModel.cs` | 711, 723 | `targetEll.angel = result.RotationAngle;` | Stores fitter output to channel data |
| `Views/MakeEllipseSelection.xaml.cs` | 196, 209 | `targetEll.angel = result.RotationAngle;` | Stores fitter output to channel data |

---

## 3. Disentangling Y-Flip from Angle-Flip

In 2D graphics, an inversion of the Y-axis and an inversion of rotation angle can produce visually identical reflections of symmetric shapes. To determine which axis was inverted:

1. **Test with $X = 0, Y = +40, \text{Height} = 120, \text{Width} = 60, \text{Angle} = 0$:**
   - In `MainWindow.xaml.cs` line 2079:
     ```csharp
     tt1.Y = (((item.ey * -1) - (item.height / 2)) / factor);
     ```
   - For $ey = +40$, `tt1.Y = (-40 - 60) / 1 = -100`.
   - In WPF Canvas coordinates, $Y = 0$ is at the center (180, 180). A translation of $-100$ moves the ellipse **upwards towards the top of the screen**.
   - **Conclusion:** Positive Y moves the ellipse **UP** on screen, matching the Cartesian instrument coordinate system and the real machine. The Y mapping is **NOT** mirrored.
2. **Angle Rotation Analysis:**
   - In WPF, `RotateTransform` operates in a left-handed screen coordinate system (X right, Y down). The rotation transform matrix is:
     $$\begin{pmatrix} X' \\ Y' \end{pmatrix} = \begin{pmatrix} \cos\theta & -\sin\theta \\ \sin\theta & \cos\theta \end{pmatrix} \begin{pmatrix} X \\ Y \end{pmatrix}$$
   - A positive angle $\theta > 0$ rotates vectors from $+X$ (right) towards $+Y$ (down), which is **clockwise on screen**.
   - By applying `Angle = item.angel * -1`, the code explicitly passed a negative angle to WPF when `item.angel = 120`, resulting in a $-120^\circ$ rotation (**counter-clockwise**).
   - Rotating a vertical ellipse (long axis at 12 o'clock) counter-clockwise by $120^\circ$ places the long axis at 8 o'clock (or 2 o'clock), leaning like `/` (30° above horizontal).
   - Rotating clockwise by $+120^\circ$ places the long axis at 4 o'clock, leaning like `\` (30° below horizontal).
   - **Conclusion:** The error is strictly an **angle sign negation** in the drawing code and fitter.

---

## 4. Semi-Axis Definitions: Width/Height vs $a/b$

In the 8F solution and instrument firmware:
- **`Width`:** The unrotated horizontal dimension ($X$-span). The semi-width is $a = \text{Width} / 2$.
- **`Height`:** The unrotated vertical dimension ($Y$-span). The semi-height is $b = \text{Height} / 2$.
- When $\text{Height} = 120$ and $\text{Width} = 60$, the unrotated ellipse ($\text{Angle} = 0$) is **tall and vertical** ($b > a$).
- In `EllipseFitter.cs`, $a$ represents the semi-axis along the fitted regression line, and $b$ represents the semi-axis perpendicular to it. `Width` is assigned $2a$ and `Height` is assigned $2b$. Thus, the fitter considers `Width` as the major axis along the line slope.

---

## 5. Single Helper Architecture (`EllipseGeometry`)

To eliminate scattered negations, a single helper `EllipseGeometry` is introduced:
1. `ToScreenRotationDegrees(double angle)`:
   - Returns `angle` directly (normalized), since WPF's positive `RotateTransform.Angle` is natively clockwise.
2. `IsInside(double x, double y, double centerX, double centerY, double width, double height, double angleDegrees)`:
   - Evaluates whether point $(x, y)$ lies inside the rotated ellipse using screen-consistent clockwise geometry.
3. `SlopeToAngleDegrees(double slope)`:
   - Converts linear regression slope $m = \Delta y / \Delta x$ into the unified screen clockwise angle:
     $$\theta = -\text{atan2}(m, 1.0) \times \frac{180.0}{\pi}$$
   - Guarantees that fitted ellipses tilt identically on screen to the input point cloud.
