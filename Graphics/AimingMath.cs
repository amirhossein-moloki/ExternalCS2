using System;
using System.Drawing;
using System.Numerics;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Data.Game;

namespace CS2GameHelper.Graphics
{
    public static class AimingMath
    {
        /// <summary>
        /// Calculates the required aim angle deltas to reach pointWorld from the current player's state.
        /// </summary>
        public static void GetAimAngles(Player player, Vector3 pointWorld, float recoilScale, out float angleSize, out Vector2 aimAngles, Vector2? patternAngles = null)
        {
            aimAngles = Vector2.Zero;
            angleSize = 0f;

            var eyePos = player.EyePosition;
            var dirToTarget = (pointWorld - eyePos).GetNormalized();

            // Current view angles (X = Pitch, Y = Yaw)
            var currentAngles = player.ViewAngles;

            // Target angles
            double targetYaw = Math.Atan2(dirToTarget.Y, dirToTarget.X) * (180.0 / Math.PI);
            double targetPitch = Math.Asin(-dirToTarget.Z) * (180.0 / Math.PI);

            // Compensation angles
            double compYaw = player.AimPunchAngle.Y * recoilScale;
            double compPitch = player.AimPunchAngle.X * recoilScale;

            if (patternAngles.HasValue)
            {
                compYaw = patternAngles.Value.X * (180.0 / Math.PI);
                compPitch = patternAngles.Value.Y * (180.0 / Math.PI);
            }

            // Deltas with proper recoil compensation for both axes
            double deltaYaw = targetYaw - (currentAngles.Y + compYaw);
            double deltaPitch = targetPitch - (currentAngles.X + compPitch);

            // Normalize Yaw
            while (deltaYaw > 180) deltaYaw -= 360;
            while (deltaYaw < -180) deltaYaw += 360;

            aimAngles = new Vector2((float)(deltaYaw * (Math.PI / 180.0)), (float)(deltaPitch * (Math.PI / 180.0)));
            angleSize = player.EyeDirection.GetAngleTo(dirToTarget);
        }

        /// <summary>
        /// Converts angular deltas to pixel offsets.
        /// </summary>
        public static void GetAimPixels(Vector2 aimAngles, double anglePerPixelHorizontal, double anglePerPixelVertical, out Point aimPixels)
        {
            // Note: In Source, mouse movement translates directly to angle changes.
            // Horizontal: +deltaX move = -deltaYaw (looking right)
            // Vertical: +deltaY move = +deltaPitch (looking down)

            // aimAngles.X is deltaYaw in radians
            // aimAngles.Y is deltaPitch in radians

            aimPixels = new Point(
                (int)Math.Round(-aimAngles.X / anglePerPixelHorizontal),
                (int)Math.Round(aimAngles.Y / anglePerPixelVertical)
            );
        }

        public static double GetYaw(Vector3 direction) => Math.Atan2(direction.Y, direction.X);

        public static double GetPitch(Vector3 direction)
        {
            var clampedZ = Math.Clamp(direction.Z, -1f, 1f);
            return Math.Asin(-clampedZ);
        }

        public static double NormalizeRadians(double value)
        {
            const double twoPi = Math.PI * 2;
            value %= twoPi;
            if (value <= -Math.PI) value += twoPi;
            else if (value > Math.PI) value -= twoPi;
            return value;
        }

        /// <summary>
        /// Calculates a point on a Cubic Bezier curve.
        /// P0: Start, P1: Control 1, P2: Control 2, P3: End
        /// t: [0, 1]
        /// </summary>
        public static Vector2 GetBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            float u = 1 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;

            Vector2 p = uuu * p0; // (1-t)^3 * P0
            p += 3 * uu * t * p1; // 3 * (1-t)^2 * t * P1
            p += 3 * u * tt * p2; // 3 * (1-t) * t^2 * P2
            p += ttt * p3;        // t^3 * P3

            return p;
        }
    }
}
