using System;

namespace _8F.Helpers
{
    
    public static class EllipseMath
    {
        public static double ToScreenRotationDegrees(double angleDegrees)
        {
            if (double.IsNaN(angleDegrees) || double.IsInfinity(angleDegrees)) return 0.0;
            return angleDegrees;
        }
        public static double SlopeToAngleDegrees(double slope)
        {
            if (double.IsNaN(slope) || double.IsInfinity(slope)) return 0.0;
            double thetaRad = Math.Atan2(slope, 1.0);
            return -(thetaRad * (180.0 / Math.PI));
        }
        public static bool IsInside(double x, double y, double centerX, double centerY, double width, double height, double angleDegrees)
        {
            if (width <= 1e-9 || height <= 1e-9) return false;

            double a = width / 2.0;
            double b = height / 2.0;

            double dx = x - centerX;
            double dy = y - centerY;
            double rad = angleDegrees * (Math.PI / 180.0);
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            double u = (dx * cos) - (dy * sin);
            double v = (dx * sin) + (dy * cos);

            double dist = ((u * u) / (a * a)) + ((v * v) / (b * b));
            return dist <= 1.0;
        }

        public static bool IsInsideSemiAxes(double x, double y, double a, double b, double centerX, double centerY, double angleDegrees)
        {
            return IsInside(x, y, centerX, centerY, a * 2.0, b * 2.0, angleDegrees);
        }
    }
}
