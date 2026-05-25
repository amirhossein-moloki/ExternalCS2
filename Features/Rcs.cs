using System;
using System.Diagnostics;
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
        private readonly AimBot? _aimBot;

        private Stopwatch _fireStopwatch = new Stopwatch();
        private bool _isFiring = false;
        private float _rcsAppliedX = 0;
        private float _rcsAppliedY = 0;

        public Rcs(GameProcess gameProcess, GameData gameData, ConfigManager config, AimBot? aimBot = null)
        {
            _gameProcess = gameProcess;
            _gameData = gameData;
            _config = config;
            _aimBot = aimBot;
        }

        protected override string ThreadName => nameof(Rcs);

        protected override void FrameAction()
        {
            if (!_config.Rcs.Enabled)
            {
                _isFiring = false;
                _fireStopwatch.Reset();
                return;
            }

            // If AimBot is actively moving the mouse to a target, it already accounts for RCS.
            if (_aimBot != null && _aimBot.IsActivelyTargeting)
            {
                _isFiring = false;
                _fireStopwatch.Reset();
                _rcsAppliedX = 0;
                _rcsAppliedY = 0;
                return;
            }

            try
            {
                if (_gameProcess == null || !_gameProcess.IsValid || _gameData?.Player == null)
                    return;

                var player = _gameData.Player;
                if (!player.IsAlive())
                {
                    _isFiring = false;
                    _fireStopwatch.Reset();
                    return;
                }

                bool isCurrentlyFiring = player.ShotsFired > 0;
                if (isCurrentlyFiring && !_isFiring)
                {
                    _fireStopwatch.Restart();
                    _isFiring = true;
                    _rcsAppliedX = 0;
                    _rcsAppliedY = 0;
                }
                else if (!isCurrentlyFiring)
                {
                    _isFiring = false;
                    _fireStopwatch.Reset();
                    _rcsAppliedX = 0;
                    _rcsAppliedY = 0;
                    return;
                }

                var weaponInfo = PatternManager.GetWeaponInfo(player.CurrentWeaponName);
                if (weaponInfo != null)
                {
                    double elapsedMs = _fireStopwatch.Elapsed.TotalMilliseconds;
                    int currIdx = 0;
                    double accTime = 0;
                    var pattern = weaponInfo.Pattern;

                    for (int i = 0; i < pattern.Count; i++)
                    {
                        double delay = pattern[i].Delay / weaponInfo.SleepDivider - weaponInfo.SleepSuber;
                        accTime += delay;
                        if (accTime > elapsedMs) break;
                        currIdx = i;
                    }

                    float totalDx = 0;
                    float totalDy = 0;
                    float sensScale = 2.45f / _config.Rcs.Sensitivity;
                    float globalScale = _config.Rcs.GlobalScale / 2.0f;

                    for (int i = 0; i <= currIdx; i++)
                    {
                        totalDx += pattern[i].Dx * sensScale * globalScale;
                        totalDy += -pattern[i].Dy * sensScale * globalScale;
                    }

                    int moveX = (int)(totalDx - _rcsAppliedX);
                    int moveY = (int)(totalDy - _rcsAppliedY);

                    if (moveX != 0 || moveY != 0)
                    {
                        Utility.MouseMove(moveX, moveY);
                        _rcsAppliedX += moveX;
                        _rcsAppliedY += moveY;
                    }
                }
            }
            catch
            {
                // Ignore errors in background thread
            }
        }
    }
}
