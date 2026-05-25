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

                            // We use the pattern directly as requested.
                            // The scale from config is still applied as a global multiplier.
                            // In many cases, scale 2.0 is used for full compensation if the pattern is 1:1 with recoil.

                            float dx = point.Dx * (currentScale / 2.0f);
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
                    // Fallback to traditional RCS removed as per request to use patterns exclusively.

                    _lastShotsFired = player.ShotsFired;
                }
            }
            catch
            {
                // Ignore errors in background thread
            }
        }
    }
}
