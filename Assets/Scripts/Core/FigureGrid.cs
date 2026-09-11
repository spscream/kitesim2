namespace Kitesim.Core
{
    /// <summary>
    /// The 45-degree ballet grid on the wind window: meridians of constant azimuth
    /// and one elevation parallel at 45 degrees. This is the same grid TurnAssist
    /// finishes onto, drawn so a routine can be read against it.
    /// </summary>
    public static class FigureGrid
    {
        public static readonly double[] AzimuthStepsDeg = { -90.0, -45.0, 0.0, 45.0, 90.0 };
        public static readonly double[] ElevationStepsDeg = { 45.0 };

        public static Vec3[] Meridian(WindField wind, double azimuthRad, double radius, int segments)
        {
            if (segments < 1)
                segments = 1;

            var points = new Vec3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double elevation = 0.5 * MathD.PI * i / segments;
                points[i] = Direction(wind, azimuthRad, elevation) * radius;
            }

            return points;
        }

        public static Vec3[] Elevation(WindField wind, double elevationRad, double radius, int segments)
        {
            if (segments < 1)
                segments = 1;

            var points = new Vec3[segments + 1];
            for (int i = 0; i <= segments; i++)
            {
                double azimuth = MathD.PI * (i / (double)segments - 0.5);
                points[i] = Direction(wind, azimuth, elevationRad) * radius;
            }

            return points;
        }

        public static double AzimuthDeg(WindField wind, Vec3 point)
        {
            Vec3 direction = point.Normalized();
            Vec3 centre = WindowChart.Center(wind);
            Vec3 right = WindowChart.Right(wind);
            double along = Vec3.Dot(direction, centre);
            double across = Vec3.Dot(direction, right);
            return MathD.Atan2(across, along) * MathD.Rad2Deg;
        }

        public static double ElevationDeg(Vec3 point)
        {
            return MathD.Asin(MathD.Clamp(point.Normalized().Y, -1.0, 1.0)) * MathD.Rad2Deg;
        }

        private static Vec3 Direction(WindField wind, double azimuthRad, double elevationRad)
        {
            Vec3 centre = WindowChart.Center(wind);
            Vec3 right = WindowChart.Right(wind);
            Vec3 up = Vec3.Cross(centre, right).Normalized();
            double cosElevation = MathD.Cos(elevationRad);
            return (right * (MathD.Sin(azimuthRad) * cosElevation)
                    + up * MathD.Sin(elevationRad)
                    + centre * (MathD.Cos(azimuthRad) * cosElevation)).Normalized();
        }
    }
}
