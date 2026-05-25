using System;
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
    [Feature("rcs", "Recoil Control System (Pattern)", "Aim")]
    public class Rcs : ThreadedServiceBase
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

        public Rcs(GameProcess gameProcess, GameData gameData, ConfigManager config)
        {
            _gameProcess = gameProcess;
            _gameData = gameData;
            _config = config;
        }

        protected override string ThreadName => nameof(Rcs);

        protected override void FrameAction()
        {
            if (!_config.Rcs.Enabled) return;

            try
            {
                if (_gameProcess == null || !_gameProcess.IsValid || _gameData?.Player == null)
                    return;

                var player = _gameData.Player;
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
                        int shotIndex = player.ShotsFired - 1;
                        if (shotIndex < pattern.Count)
                        {
                            var point = pattern[shotIndex];

                            float currentScale = _config.Rcs.GlobalScale;
                            if (player.CurrentWeaponName != null &&
                                _config.Rcs.WeaponScales.TryGetValue(player.CurrentWeaponName, out var customScale))
                            {
                                currentScale = customScale;
                            }

                            // Artanis pattern dx, dy.
                            // We scale them by our config if needed, although user asked for 100% (Scale = 1.0 or similar)
                            // We'll keep the scale from config but default is usually 2.0 or 1.0 depending on units.
                            // In Artanis, they use multiple=6 by default for AK47.

                            float dx = point.Dx * (currentScale / 2.0f); // Adjusting because our scales are usually around 2.0
                            float dy = -point.Dy * (currentScale / 2.0f);

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
                        // Fallback to traditional RCS
                        var currentPunch = player.AimPunchAngle;
                        var punchDelta = currentPunch - _lastAimPunch;

                        if (punchDelta.X != 0 || punchDelta.Y != 0)
                        {
                            float currentScale = _config.Rcs.GlobalScale;
                            if (player.CurrentWeaponName != null &&
                                _config.Rcs.WeaponScales.TryGetValue(player.CurrentWeaponName, out var customScale))
                            {
                                currentScale = customScale;
                            }

                            var deltaYawRad = (float)(punchDelta.Y * (Math.PI / 180.0));
                            var deltaPitchRad = (float)(punchDelta.X * (Math.PI / 180.0));

                            Point rcsPixels;
                            AimingMath.GetAimPixels(new Vector2(deltaYawRad, deltaPitchRad), _anglePerPixelHorizontal, _anglePerPixelVertical, out rcsPixels);

                            double finalX = -rcsPixels.X * currentScale;
                            double finalY = -rcsPixels.Y * currentScale;

                            int moveX = (int)(Math.Abs(finalX) > 0.01 && Math.Abs(finalX) < 1.0 ? Math.Sign(finalX) : Math.Round(finalX));
                            int moveY = (int)(Math.Abs(finalY) > 0.01 && Math.Abs(finalY) < 1.0 ? Math.Sign(finalY) : Math.Round(finalY));

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
