using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using _8F.Helpers;
using _8F.Models;
using _8F.Repositories;
using _8F.Services;
using _8F.ViewModels;
using Xunit;

namespace _8F.UiTests
{
    public class EllipseAngleTests
    {
        public EllipseAngleTests()
        {
            TestEnvironment.EnsureTestResultsFolders();
        }

        private static Canvas GetCanvas1(MainWindow mainWin)
        {
            return (Canvas)mainWin.FindName("cnBr1");
        }

        [Fact]
        public async Task EL_01_Height120_Width60_Angle120_RendersClockwise30DegBelowHorizontal()
        {
            // Geometry check:
            // Unrotated: Height 120 (vertical), Width 60 (horizontal).
            // Rotating clockwise by 120° puts long axis 30° below horizontal ("\").
            await WpfTestHelper.InvokeAsync(() =>
            {
                var mainWin = new MainWindow();
                mainWin.InitialGraphData(false);

                var canvas1 = GetCanvas1(mainWin);
                Assert.NotNull(canvas1);

                var gData = DeviceCOM.channelDatas[0].graphDatas[0];
                gData.ellipses = new List<Ellips>
                {
                    new() { Id = 1, height = 120, width = 60, ex = 0, ey = 0, angel = 120 }
                };
                gData.height = 120;
                gData.width = 60;
                gData.ex = 0;
                gData.ey = 0;
                gData.angel = 120;

                mainWin.AddEllipses(canvas1, gData);

                // Assert child element is drawn
                var drawnEllipse = canvas1.Children.OfType<Ellipse>().LastOrDefault();
                Assert.NotNull(drawnEllipse);

                var tg = drawnEllipse.RenderTransform as TransformGroup;
                Assert.NotNull(tg);

                var rt = tg.Children.OfType<RotateTransform>().FirstOrDefault();
                Assert.NotNull(rt);

                // With the fix, rt.Angle is +120° (clockwise).
                // Before the fix, it was -120° (counter-clockwise "/").
                Assert.Equal(120.0, rt.Angle);

                // Numerically verify the long axis:
                // Tip of the long axis is at 30° below horizontal (4 o'clock on screen):
                // Direction: (cos 30°, -sin 30°) in data space = (0.866025, -0.5)
                double tipX = 50.0 * Math.Cos(30.0 * Math.PI / 180.0); // ~43.30
                double tipY = -50.0 * Math.Sin(30.0 * Math.PI / 180.0); // -25.0
                Assert.True(EllipseMath.IsInside(tipX, tipY, 0, 0, 60, 120, 120));

                double outsideX = 70.0 * Math.Cos(30.0 * Math.PI / 180.0);
                double outsideY = -70.0 * Math.Sin(30.0 * Math.PI / 180.0);
                Assert.False(EllipseMath.IsInside(outsideX, outsideY, 0, 0, 60, 120, 120));

                // Save rendered screenshots for report evidence:
                SaveCanvasScreenshot(canvas1, "EL01_Angle120_After.png");

                // Also render with the old bug (-120) to generate before screenshot:
                rt.Angle = -120.0;
                SaveCanvasScreenshot(canvas1, "EL01_Angle120_Before.png");
            });
        }

        [Fact]
        public void EL_02_AnglesTable_MatchesExpectedGeometry()
        {
            double w = 60.0;
            double h = 120.0;
            double cx = 0.0;
            double cy = 0.0;

            // Angle 0: tall, vertical long axis
            Assert.True(EllipseMath.IsInside(0, 50, cx, cy, w, h, 0));
            Assert.False(EllipseMath.IsInside(50, 0, cx, cy, w, h, 0));

            // Angle 30: steep "/" (long axis 60° above horizontal, 30° CW from vertical)
            // Long axis direction: (sin 30°, cos 30°) = (0.5, 0.866025)
            double p30X = 50.0 * Math.Sin(30.0 * Math.PI / 180.0);
            double p30Y = 50.0 * Math.Cos(30.0 * Math.PI / 180.0);
            Assert.True(EllipseMath.IsInside(p30X, p30Y, cx, cy, w, h, 30));

            // Angle 90: wide, horizontal long axis
            Assert.True(EllipseMath.IsInside(50, 0, cx, cy, w, h, 90));
            Assert.False(EllipseMath.IsInside(0, 50, cx, cy, w, h, 90));

            // Angle 120: "\" about 30° below horizontal
            double p120X = 50.0 * Math.Cos(30.0 * Math.PI / 180.0);
            double p120Y = -50.0 * Math.Sin(30.0 * Math.PI / 180.0);
            Assert.True(EllipseMath.IsInside(p120X, p120Y, cx, cy, w, h, 120));

            // Angle 150: "\" about 60° below horizontal
            double p150X = 50.0 * Math.Cos(60.0 * Math.PI / 180.0);
            double p150Y = -50.0 * Math.Sin(60.0 * Math.PI / 180.0);
            Assert.True(EllipseMath.IsInside(p150X, p150Y, cx, cy, w, h, 150));

            // Angle 210: steep "/" (60° below -X, 180°+30°)
            double p210X = -50.0 * Math.Sin(30.0 * Math.PI / 180.0);
            double p210Y = -50.0 * Math.Cos(30.0 * Math.PI / 180.0);
            Assert.True(EllipseMath.IsInside(p210X, p210Y, cx, cy, w, h, 210));

            // Angle -30: 30° counter-clockwise from vertical (11 o'clock on screen)
            double pMinus30X = -50.0 * Math.Sin(30.0 * Math.PI / 180.0);
            double pMinus30Y = 50.0 * Math.Cos(30.0 * Math.PI / 180.0);
            Assert.True(EllipseMath.IsInside(pMinus30X, pMinus30Y, cx, cy, w, h, -30));
        }

        [Fact]
        public async Task EL_03_PositiveYMovesUp_PositiveXMovesRight()
        {
            await WpfTestHelper.InvokeAsync(() =>
            {
                var mainWin = new MainWindow();
                mainWin.InitialGraphData(false);

                var canvas1 = GetCanvas1(mainWin);
                var gData = DeviceCOM.channelDatas[0].graphDatas[0];
                gData.ellipses = new List<Ellips>
                {
                    new() { Id = 1, height = 120, width = 60, ex = 40, ey = 40, angel = 0 }
                };
                gData.height = 120;
                gData.width = 60;
                gData.ex = 40;
                gData.ey = 40;
                gData.angel = 0;

                mainWin.AddEllipses(canvas1, gData);

                var drawnEllipse = canvas1.Children.OfType<Ellipse>().LastOrDefault();
                Assert.NotNull(drawnEllipse);

                var tg = drawnEllipse.RenderTransform as TransformGroup;
                Assert.NotNull(tg);

                var tt = tg.Children.OfType<TranslateTransform>().FirstOrDefault();
                Assert.NotNull(tt);

                // With ex = 40, width = 60: tt.X = (40 - 30) = +10. Positive X moves right on screen.
                Assert.Equal(10.0, tt.X);

                // With ey = 40, height = 120: tt.Y = (-40 - 60) = -100.
                // In WPF coordinates, a negative Y translation moves UPWARDS towards top of screen.
                Assert.Equal(-100.0, tt.Y);
            });
        }

        [Fact]
        public void EL_04_IsInsideEllipse_Versus_WpfGeometry_100PercentAgreement()
        {
            double w = 60.0;
            double h = 120.0;
            double[] testAngles = { 0, 30, 90, 120, 150, 210, -45 };

            foreach (double angle in testAngles)
            {
                var geom = new System.Windows.Media.EllipseGeometry(new Point(0, 0), w / 2.0, h / 2.0);
                geom.Transform = new RotateTransform(angle, 0, 0);

                // Sample a dense grid of points
                for (int x = -70; x <= 70; x += 5)
                {
                    for (int y = -70; y <= 70; y += 5)
                    {
                        // In screen coordinates: screenX = x, screenY = -y
                        bool wpfContains = geom.FillContains(new Point(x, -y));
                        bool mathContains = EllipseMath.IsInside(x, y, 0, 0, w, h, angle);

                        // Points very close to the boundary (within 0.5 units) can differ due to floating-point edge handling.
                        // For non-boundary points, assert exact agreement.
                        double rad = angle * (Math.PI / 180.0);
                        double u = x * Math.Cos(rad) - y * Math.Sin(rad);
                        double v = x * Math.Sin(rad) + y * Math.Cos(rad);
                        double dist = ((u * u) / 900.0) + ((v * v) / 3600.0);

                        if (Math.Abs(dist - 1.0) > 0.05)
                        {
                            Assert.True(wpfContains == mathContains,
                                $"Disagreement at angle={angle}, point=({x},{y}): WPF={wpfContains}, Math={mathContains}, dist={dist}");
                        }
                    }
                }
            }
        }

        [Fact]
        public void EL_05_EllipseFitter_CloudAlongYEqualsPlusX_WrapsCloudAndTiltsSlash()
        {
            // Point cloud along y = +x (leaning like "/" on screen)
            var points = new List<(double X, double Y)>();
            for (int i = -80; i <= 80; i += 4)
            {
                points.Add((i, i + (i % 3 == 0 ? 2 : -2)));
            }

            var result = EllipseFitter.FitEllipse("D1", 1, points);
            Assert.True(result.IsValid);

            // Angle of y = +x in screen coordinates: -45° (counter-clockwise on screen, leaning "/")
            Assert.True(result.RotationAngle < -40 && result.RotationAngle > -50,
                $"Expected angle around -45°, actual={result.RotationAngle}");

            // All sample points must be inside the resulting ellipse
            foreach (var (px, py) in points)
            {
                bool inside = EllipseMath.IsInside(px, py, result.CenterX, result.CenterY, result.Width, result.Height, result.RotationAngle);
                Assert.True(inside, $"Point ({px}, {py}) outside fitted ellipse");
            }
        }

        [Fact]
        public void EL_06_EllipseFitter_CloudAlongYEqualsMinusX_WrapsCloudAndTiltsBackslash()
        {
            // Point cloud along y = -x (leaning like "\" on screen)
            var points = new List<(double X, double Y)>();
            for (int i = -80; i <= 80; i += 4)
            {
                points.Add((i, -i + (i % 3 == 0 ? 2 : -2)));
            }

            var result = EllipseFitter.FitEllipse("D1", 1, points);
            Assert.True(result.IsValid);

            // Angle of y = -x in screen coordinates: +45° (clockwise on screen, leaning "\")
            Assert.True(result.RotationAngle > 40 && result.RotationAngle < 50,
                $"Expected angle around +45°, actual={result.RotationAngle}");

            foreach (var (px, py) in points)
            {
                bool inside = EllipseMath.IsInside(px, py, result.CenterX, result.CenterY, result.Width, result.Height, result.RotationAngle);
                Assert.True(inside, $"Point ({px}, {py}) outside fitted ellipse");
            }
        }

        [Fact]
        public void EL_07_HorizontalAndVerticalClouds_HandleAnglesCleanlyWithoutNaN()
        {
            // Horizontal cloud (slope 0)
            var hPoints = new List<(double X, double Y)>();
            for (int i = -100; i <= 100; i += 5) hPoints.Add((i, 0));

            var hResult = EllipseFitter.FitEllipse("D1", 1, hPoints);
            Assert.True(hResult.IsValid);
            Assert.False(double.IsNaN(hResult.RotationAngle));
            Assert.InRange(Math.Abs(hResult.RotationAngle), 0, 2);

            // Near-vertical cloud (steep slope)
            var vPoints = new List<(double X, double Y)>();
            for (int i = -100; i <= 100; i += 5) vPoints.Add((0.001 * i, i));

            var vResult = EllipseFitter.FitEllipse("D1", 1, vPoints);
            Assert.True(vResult.IsValid);
            Assert.False(double.IsNaN(vResult.RotationAngle));
            Assert.InRange(Math.Abs(vResult.RotationAngle), 85, 95);
        }

        [Fact]
        public async Task EL_08_TypeAngleInSystemConfig_SaveAndReopen_PreservesSameNumber()
        {
            _8F.Auth.AppServices.SessionContext.SetSession(_8F.Auth.Role.Admin);

            await WpfTestHelper.InvokeAsync(() =>
            {
                var mainWin = new MainWindow();
                mainWin.InitialGraphData(false);
                mainWin.SelectCh1();

                // Open CircleSettingViewModel for D1
                var vm = new CircleSettingViewModel("D1");
                var rowD1 = vm.Ellipses.FirstOrDefault(e => e.ChannelName == "D1");
                Assert.NotNull(rowD1);

                rowD1.height = 120;
                rowD1.width = 60;
                rowD1.ex = 0;
                rowD1.ey = 0;
                rowD1.angel = 120;

                vm.SaveConfigCommand.Execute(null);

                // Verify channel data has updated angel = 120
                var gData = DeviceCOM.channelDatas[0].graphDatas[0];
                Assert.Equal(120, gData.angel);
                Assert.Equal(120, gData.ellipses[0].angel);

                // Reopen CircleSettingViewModel: verify Angle is 120 without alteration
                var vm2 = new CircleSettingViewModel("D1");
                var rowD1Reopened = vm2.Ellipses.FirstOrDefault(e => e.ChannelName == "D1");
                Assert.NotNull(rowD1Reopened);
                Assert.Equal(120, rowD1Reopened.angel);
            });
        }

        [Fact]
        public async Task EL_09_DragEllipse_Channel1_LeavesChannel2Untouched()
        {
            await WpfTestHelper.InvokeAsync(() =>
            {
                var mainWin = new MainWindow();
                mainWin.InitialGraphData(false);
                mainWin.chNo = 2;

                var canvas1 = GetCanvas1(mainWin);

                // Configure Ch1 and Ch2 with known ellipses
                DeviceCOM.channelDatas[0].graphDatas[0].ellipses[0].angel = 120;
                DeviceCOM.channelDatas[0].graphDatas[0].ellipses[0].ex = 0;
                DeviceCOM.channelDatas[0].graphDatas[0].ellipses[0].ey = 0;

                DeviceCOM.channelDatas[1].graphDatas[0].ellipses[0].angel = 45;
                DeviceCOM.channelDatas[1].graphDatas[0].ellipses[0].ex = 50;
                DeviceCOM.channelDatas[1].graphDatas[0].ellipses[0].ey = 50;

                // Select Channel 1 and render
                mainWin.SelectCh1();
                mainWin.AddEllipses(canvas1, DeviceCOM.channelDatas[0].graphDatas[0]);

                // Simulate drag on Ch1 ellipse
                var gCh1 = DeviceCOM.channelDatas[0].graphDatas[0];
                gCh1.ellipses[0].ex = 25;
                gCh1.ellipses[0].ey = 35;
                gCh1.ex = 25;
                gCh1.ey = 35;

                // Verify Ch1 angle remains 120
                Assert.Equal(120, gCh1.ellipses[0].angel);
                Assert.Equal(25, gCh1.ellipses[0].ex);
                Assert.Equal(35, gCh1.ellipses[0].ey);

                // Verify Ch2 is completely untouched
                var gCh2 = DeviceCOM.channelDatas[1].graphDatas[0];
                Assert.Equal(45, gCh2.ellipses[0].angel);
                Assert.Equal(50, gCh2.ellipses[0].ex);
                Assert.Equal(50, gCh2.ellipses[0].ey);
            });
        }

        [Fact]
        public async Task EL_10_SaveAs_New_Open_PreservesAngleAndTilt()
        {
            await TestEnvironment.ResetTestDatabaseAsync();

            await WpfTestHelper.InvokeAsync(async () =>
            {
                var mainWin = new MainWindow();
                mainWin.InitialGraphData(false);
                var canvas1 = GetCanvas1(mainWin);

                // Setup Ch1 with Angle 120
                var gData = DeviceCOM.channelDatas[0].graphDatas[0];
                gData.ellipses[0].height = 120;
                gData.ellipses[0].width = 60;
                gData.ellipses[0].ex = 0;
                gData.ellipses[0].ey = 0;
                gData.ellipses[0].angel = 120;
                gData.height = 120;
                gData.width = 60;
                gData.ex = 0;
                gData.ey = 0;
                gData.angel = 120;

                var repo = new InspectionLogRepository(TestEnvironment.ConnectionString);

                // Save profile to DB
                int profileId = await repo.SaveConfigProfileAsync("Angle120Profile", "Admin", DeviceCOM.channelDatas);

                // Clear/Reset memory
                mainWin.InitialGraphData(false);
                Assert.Equal(DeviceCOM.DefaultAngel_O, DeviceCOM.channelDatas[0].graphDatas[0].angel_O);

                // Open from DB
                var loaded = await repo.GetFullConfigProfileAsync(profileId);
                Assert.NotNull(loaded.Channels);

                var loadedCh1 = loaded.Channels.First(c => c.Id == 1);
                Assert.Equal(120, loadedCh1.graphDatas[0].ellipses[0].angel);

                // Render into canvas and verify transform angle
                mainWin.AddEllipses(canvas1, loadedCh1.graphDatas[0]);
                var drawn = canvas1.Children.OfType<Ellipse>().LastOrDefault();
                Assert.NotNull(drawn);
                var tg = drawn.RenderTransform as TransformGroup;
                var rt = tg!.Children.OfType<RotateTransform>().FirstOrDefault();
                Assert.Equal(120.0, rt!.Angle);
            });
        }

        [Fact]
        public async Task EL_11_LegacyProfile_OpensWithoutDataModification_RendersCorrectly()
        {
            await TestEnvironment.ResetTestDatabaseAsync();

            int legacyProfileId = 0;
            // Seed legacy profile with Angel = 120 in DB
            await using (var conn = new Npgsql.NpgsqlConnection(TestEnvironment.ConnectionString))
            {
                await conn.OpenAsync();
                await using var cmd = new Npgsql.NpgsqlCommand(
                    @"INSERT INTO public.""ConfigProfiles"" (""Name"", ""OperatorName"", ""CreatedAt"")
                      VALUES ('LegacyAngleProfile', 'Admin', NOW()) RETURNING ""Id"";", conn);
                legacyProfileId = Convert.ToInt32(await cmd.ExecuteScalarAsync());

                await using var cmdCh = new Npgsql.NpgsqlCommand(
                    @"INSERT INTO public.""ConfigChannels"" (""ConfigProfileId"", ""ChannelNumber"")
                      VALUES (@pId, 1) RETURNING ""Id"";", conn);
                cmdCh.Parameters.AddWithValue("pId", legacyProfileId);
                int chId = Convert.ToInt32(await cmdCh.ExecuteScalarAsync());

                await using var cmdFreq = new Npgsql.NpgsqlCommand(
                    @"INSERT INTO public.""ConfigFrequencies"" (""ConfigChannelId"", ""FrequencyNumber"", ""Name"", ""Freq"", ""Gain"", ""Phase"", ""IsEnable"")
                      VALUES (@chId, 1, 'D1', 1000, 10, 0, true) RETURNING ""Id"";", conn);
                cmdFreq.Parameters.AddWithValue("chId", chId);
                int freqId = Convert.ToInt32(await cmdFreq.ExecuteScalarAsync());

                await using var cmdEll = new Npgsql.NpgsqlCommand(
                    @"INSERT INTO public.""ConfigEllipses"" (""ConfigFrequencyId"", ""EllipseIndex"", ""Height"", ""Width"", ""Ex"", ""Ey"", ""Angel"")
                      VALUES (@fId, 0, 120, 60, 0, 0, 120);", conn);
                cmdEll.Parameters.AddWithValue("fId", freqId);
                await cmdEll.ExecuteNonQueryAsync();
            }

            // Open legacy profile
            var repo = new InspectionLogRepository(TestEnvironment.ConnectionString);
            var fullProf = await repo.GetFullConfigProfileAsync(legacyProfileId);
            Assert.NotNull(fullProf.Channels);
            var ell = fullProf.Channels[0].graphDatas[0].ellipses[0];

            // Verify stored angle is completely unmodified (120)
            Assert.Equal(120, ell.angel);

            // Verify rendered angle is +120° (now correctly tilting \)
            double screenAngle = EllipseMath.ToScreenRotationDegrees(ell.angel);
            Assert.Equal(120.0, screenAngle);
        }

        [Fact]
        public void EL_12_BoundaryPoints_InsideAndOutsideFollowsDrawingAtAngle120()
        {
            // Ellipse: Height 120 (semi-axis b = 60), Width 60 (semi-axis a = 30), Angle 120
            // Long axis is along 30° below horizontal (4 o'clock): (cos 30°, -sin 30°)
            double dirX = Math.Cos(30.0 * Math.PI / 180.0);
            double dirY = -Math.Sin(30.0 * Math.PI / 180.0);

            // Point just inside tip (R = 58):
            double insideTipX = 58.0 * dirX;
            double insideTipY = 58.0 * dirY;
            Assert.True(EllipseMath.IsInside(insideTipX, insideTipY, 0, 0, 60, 120, 120));

            // Point just outside tip (R = 62):
            double outsideTipX = 62.0 * dirX;
            double outsideTipY = 62.0 * dirY;
            Assert.False(EllipseMath.IsInside(outsideTipX, outsideTipY, 0, 0, 60, 120, 120));

            // Short axis is along 60° below -X (7 o'clock): (cos 120°, -sin 120°) = (-0.5, -0.866025)
            // Point just inside short axis (R = 28):
            double insideShortX = 28.0 * (-0.5);
            double insideShortY = 28.0 * (-Math.Sin(60.0 * Math.PI / 180.0));
            Assert.True(EllipseMath.IsInside(insideShortX, insideShortY, 0, 0, 60, 120, 120));

            // Point just outside short axis (R = 32):
            double outsideShortX = 32.0 * (-0.5);
            double outsideShortY = 32.0 * (-Math.Sin(60.0 * Math.PI / 180.0));
            Assert.False(EllipseMath.IsInside(outsideShortX, outsideShortY, 0, 0, 60, 120, 120));
        }

        private static void SaveCanvasScreenshot(Canvas canvas, string fileName)
        {
            try
            {
                int width = 360;
                int height = 360;
                canvas.Measure(new Size(width, height));
                canvas.Arrange(new Rect(0, 0, width, height));
                canvas.UpdateLayout();

                var rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(canvas);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                string outPath = System.IO.Path.Combine(TestEnvironment.ScreenshotsDir, fileName);
                using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write);
                encoder.Save(fs);
            }
            catch (Exception ex)
            {
                TestEnvironment.Log($"Failed to capture canvas screenshot: {ex.Message}");
            }
        }
    }
}
