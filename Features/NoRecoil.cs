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
    [Feature("norecoil", "No Recoil (Precise)", "Aim")]
    public class NoRecoil : ThreadedServiceBase
    {
        private readonly GameProcess _gameProcess;
        private readonly GameData _gameData;
        private readonly ConfigManager _config;

        private Vector3 _lastAimPunch = Vector3.Zero;
        private static double _anglePerPixelHorizontal = 0.0006;
        private static double _anglePerPixelVertical = 0.0006;

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
                    return;
                }

                var currentPunch = player.AimPunchAngle;
                var punchDelta = currentPunch - _lastAimPunch;

                if (punchDelta.X != 0 || punchDelta.Y != 0)
                {
                    // Convert degrees to radians (Source engine aim punch is in degrees)
                    var deltaYawRad = (float)(punchDelta.Y * (Math.PI / 180.0));
                    var deltaPitchRad = (float)(punchDelta.X * (Math.PI / 180.0));

                    Point rcsPixels;
                    // Get needed pixels to compensate for the change in recoil
                    AimingMath.GetAimPixels(new Vector2(deltaYawRad, deltaPitchRad), _anglePerPixelHorizontal, _anglePerPixelVertical, out rcsPixels);

                    // Apply scales
                    double finalX = -rcsPixels.X * _config.NoRecoil.HorizontalScale;
                    double finalY = -rcsPixels.Y * _config.NoRecoil.VerticalScale;

                    // "Dry" (خشک) movement: no humanization, pure rounding
                    int moveX = (int)Math.Round(finalX);
                    int moveY = (int)Math.Round(finalY);

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
