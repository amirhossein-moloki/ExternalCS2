using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using System.Threading;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;
using CS2GameHelper.Utils.Registry;
using Point = System.Drawing.Point;

namespace CS2GameHelper.Features
{
    [Feature("norecoil", "No Recoil (Pattern)", "Aim")]
    public class NoRecoil : ThreadedServiceBase
    {
        private readonly GameProcess _gameProcess;
        private readonly GameData _gameData;
        private readonly ConfigManager _config;

        private Vector3 _lastAimPunch = Vector3.Zero;
        private static double _anglePerPixelHorizontal = 0.0006;
        private static double _anglePerPixelVertical = 0.0006;

        private int _lastShotsFired = 0;
        private float _sumX = 0;
        private float _sumY = 0;

        public NoRecoil(GameProcess gameProcess, GameData gameData, ConfigManager config)
        {
            _gameProcess = gameProcess;
            _gameData = gameData;
            _config = config;
        }

        protected override string ThreadName => nameof(NoRecoil);

        protected override void FrameAction()
        {
            if (!_config.NoRecoil.Enabled) return;

            try
            {
                if (_gameProcess == null || !_gameProcess.IsValid || _gameData?.Player == null)
                    return;

                var player = _gameData.Player;

                // If not alive or not shooting, reset and exit
                if (!player.IsAlive() || player.ShotsFired == 0)
                {
                    _lastAimPunch = Vector3.Zero;
                    _lastShotsFired = 0;
                    _sumX = 0;
                    _sumY = 0;
                    return;
                }

                if (player.ShotsFired != _lastShotsFired)
                {
                    var pattern = PatternManager.GetPattern(player.CurrentWeaponName);
                    if (pattern != null)
                    {
                        // Use pattern-based recoil
                        int shotIndex = player.ShotsFired - 1;
                        if (shotIndex < pattern.Count)
                        {
                            var point = pattern[shotIndex];

                            // The pattern in Artanis-RCS uses dx, dy directly.
                            // Looking at its code:
                            // dx_float = jittered_dx (where jittered_dx = point.dx * scale)
                            // dy_float = -jittered_dy
                            // mouse_move(dx_int, dy_int)

                            float dx = point.Dx;
                            float dy = -point.Dy; // Negative because pattern usually stores recoil direction, and we want to compensate

                            _sumX += dx;
                            _sumY += dy;

                            int moveX = (int)_sumX;
                            int moveY = (int)_sumY;

                            _sumX -= moveX;
                            _sumY -= moveY;

                            if (moveX != 0 || moveY != 0)
                            {
                                Utility.MouseMove(moveX, moveY);
                            }
                        }
                    }
                    else
                    {
                        // Fallback to traditional NoRecoil (AimPunchAngle based)
                        var currentPunch = player.AimPunchAngle;
                        var punchDelta = currentPunch - _lastAimPunch;

                        if (punchDelta.X != 0 || punchDelta.Y != 0)
                        {
                            var deltaYawRad = (float)(punchDelta.Y * (Math.PI / 180.0));
                            var deltaPitchRad = (float)(punchDelta.X * (Math.PI / 180.0));

                            Point rcsPixels;
                            AimingMath.GetAimPixels(new Vector2(deltaYawRad, deltaPitchRad), _anglePerPixelHorizontal, _anglePerPixelVertical, out rcsPixels);

                            double finalX = -rcsPixels.X * _config.NoRecoil.HorizontalScale;
                            double finalY = -rcsPixels.Y * _config.NoRecoil.VerticalScale;

                            int moveX = (int)Math.Round(finalX);
                            int moveY = (int)Math.Round(finalY);

                            if (moveX != 0 || moveY != 0)
                            {
                                Utility.MouseMove(moveX, moveY);
                            }
                        }
                    }

                    _lastShotsFired = player.ShotsFired;
                }

                _lastAimPunch = player.AimPunchAngle;
            }
            catch
            {
                // Ignore errors in background thread
            }
        }
    }
}
