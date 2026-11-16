using System;

namespace _8F.Helpers
{
    /// <summary>
    /// Single authority for threshold ellipse geometry, angle conventions, and point-in-ellipse checks in 8F.
    ///
    /// CONVENTION:
    /// - Stored Angle: Stored in degrees. Positive Angle = Clockwise rotation on screen (as seen by the operator).
    /// - Unrotated Ellipse: Width is along the horizontal axis (X), Height is along the vertical axis (Y).
    ///   An ellipse with Height 120, Width 60 has its major axis vertical at Angle = 0.
    /// - Screen Coordinates: Positive X is Right, Positive Y is UP in data space (mapped to WPF Screen Y via -Y).
    /// </summary>
    public static class EllipseMath
    {
        /// <summary>
        /// Converts the stored angle (degrees) to WPF RotateTransform angle on screen.
        /// In standard WPF Canvas coordinates (X right, Y down), RotateTransform.Angle natively rotates clockwise.
        /// Therefore, the screen rotation angle directly matches the stored angle.
        /// </summary>
        public static double ToScreenRotationDegrees(double angleDegrees)
        {
            if (double.IsNaN(angleDegrees) || double.IsInfinity(angleDegrees)) return 0.0;
            return angleDegrees;
        }

        /// <summary>
        /// Converts a linear regression slope (dy/dx in Cartesian data space) to the screen clockwise rotation angle in degrees.
        /// Because data +Y is screen -Y (up on screen), a line with positive slope dy/dx rises to the right on screen (counter-clockwise from horizontal).
        /// To rotate an unrotated horizontal axis (Width) to align with this line on screen:
        /// Clockwise screen angle = -atan2(slope, 1.0) * (180.0 / PI).
        /// </summary>
        public static double SlopeToAngleDegrees(double slope)
        {
            if (double.IsNaN(slope) || double.IsInfinity(slope)) return 0.0;
            double thetaRad = Math.Atan2(slope, 1.0);
            return -(thetaRad * (180.0 / Math.PI));
        }

        /// <summary>
        /// Evaluates whether a 2D point (x, y) in data space lies inside or on the rotated ellipse.
        /// Center is (centerX, centerY). Dimensions are width (horizontal before rotation) and height (vertical before rotation).
        /// angleDegrees is clockwise rotation on screen.
        /// </summary>
        public static bool IsInside(double x, double y, double centerX, double centerY, double width, double height, double angleDegrees)
        {
            if (width <= 1e-9 || height <= 1e-9) return false;

            double a = width / 2.0;
            double b = height / 2.0;

            double dx = x - centerX;
            double dy = y - centerY;

            // In screen space (where screen Y is -dy), the ellipse is rotated clockwise by angleDegrees.
            // Rotating (dx, -dy) counter-clockwise by angleDegrees gives coordinates in the unrotated ellipse frame (u, v):
            // u = dx * cos(theta) + (-dy) * sin(theta) = dx * cos(theta) - dy * sin(theta)
            // v = -dx * sin(theta) + (-dy) * cos(theta) = -(dx * sin(theta) + dy * cos(theta))
            // Since (v / b)^2 is squared, (-v)^2 = v^2.
            double rad = angleDegrees * (Math.PI / 180.0);
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            double u = (dx * cos) - (dy * sin);
            double v = (dx * sin) + (dy * cos);

            double dist = ((u * u) / (a * a)) + ((v * v) / (b * b));
            return dist <= 1.0;
        }

        /// <summary>
        /// Compatibility overload taking semi-axes a and b.
        /// </summary>
        public static bool IsInsideSemiAxes(double x, double y, double a, double b, double centerX, double centerY, double angleDegrees)
        {
            return IsInside(x, y, centerX, centerY, a * 2.0, b * 2.0, angleDegrees);
        }
    }
}
