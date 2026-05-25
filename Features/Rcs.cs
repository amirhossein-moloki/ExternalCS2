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
    [Feature("rcs", "Recoil Control System", "Aim")]
    public class Rcs : ThreadedServiceBase
    {
        private readonly GameProcess _gameProcess;
        private readonly GameData _gameData;
        private readonly ConfigManager _config;
        private readonly Random _random = new();

        private Vector3 _lastAimPunch = Vector3.Zero;
        private static double _anglePerPixelHorizontal = 0.0006;
        private static double _anglePerPixelVertical = 0.0006;

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
                    return;
                }

                // Check if AimBot is active to avoid fighting for mouse control
                // In a real scenario, we might want a shared flag or state
                // For now, if AimBot has a target, we skip standalone RCS
                // Note: This requires access to AimBot state or a shared "IsAiming" flag.

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

                    // Convert degrees to radians
                    var deltaYawRad = (float)(punchDelta.Y * (Math.PI / 180.0));
                    var deltaPitchRad = (float)(punchDelta.X * (Math.PI / 180.0));

                    Point rcsPixels;
                    AimingMath.GetAimPixels(new Vector2(deltaYawRad, deltaPitchRad), _anglePerPixelHorizontal, _anglePerPixelVertical, out rcsPixels);

                    double finalX = -rcsPixels.X * currentScale;
                    double finalY = -rcsPixels.Y * currentScale;

                    // Humanization
                    double humanScale = 0.95 + (_random.NextDouble() * 0.1);
                    finalX *= humanScale;
                    finalY *= humanScale;

                    int moveX = (int)(Math.Abs(finalX) > 0.01 && Math.Abs(finalX) < 1.0 ? Math.Sign(finalX) : Math.Round(finalX));
                    int moveY = (int)(Math.Abs(finalY) > 0.01 && Math.Abs(finalY) < 1.0 ? Math.Sign(finalY) : Math.Round(finalY));

                    if (moveX != 0 || moveY != 0)
                    {
                        Utility.MouseMove(moveX, moveY);
                    }
                }

                _lastAimPunch = currentPunch;
            }
            catch
            {
                // Ignore errors in background thread
            }
        }
    }
}
