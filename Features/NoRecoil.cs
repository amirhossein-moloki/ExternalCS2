using System;
using System.Numerics;
using CS2GameHelper.Core;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Utils;
using CS2GameHelper.Utils.Registry;

namespace CS2GameHelper.Features
{
    [Feature("norecoil", "No Recoil (Memory)", "Aim")]
    public class NoRecoil : ThreadedServiceBase
    {
        private readonly GameProcess _gameProcess;
        private readonly GameData _gameData;
        private readonly ConfigManager _config;
        private DateTime _lastLogTime = DateTime.MinValue;
        private bool _wasActive = false;

        public NoRecoil(GameProcess gameProcess, GameData gameData, ConfigManager config)
        {
            _gameProcess = gameProcess;
            _gameData = gameData;
            _config = config;
        }

        protected override string ThreadName => nameof(NoRecoil);

        protected override void FrameAction()
        {
            bool isEnabled = _config.NoRecoil.PerfectNoRecoil || _config.NoRecoil.NoVisualRecoil;

            if (!isEnabled)
            {
                if (_wasActive)
                {
                    Console.WriteLine("[NoRecoil] Feature disabled in config.");
                    _wasActive = false;
                }
                return;
            }

            try
            {
                if (_gameProcess == null || !_gameProcess.IsValid)
                    return;

                if (_gameData?.Player == null)
                    return;

                var player = _gameData.Player;
                if (!player.IsAlive())
                {
                    if (_wasActive && (DateTime.Now - _lastLogTime).TotalSeconds > 5)
                    {
                        Console.WriteLine("[NoRecoil] Waiting for player to be alive...");
                        _lastLogTime = DateTime.Now;
                    }
                    return;
                }

                if (!_wasActive)
                {
                    Console.WriteLine("[NoRecoil] Service active and tracking player.");
                    _wasActive = true;
                }

                bool successPerfect = false;
                bool successVisual = false;

                if (_config.NoRecoil.PerfectNoRecoil)
                {
                    // Write zero to aimPunchAngle to remove bullet kick
                    successPerfect = _gameProcess.Write(player.AddressBase + Offsets.m_AimPunchAngle, Vector3.Zero);
                }

                if (_config.NoRecoil.NoVisualRecoil)
                {
                    // Write zero to viewPunchAngle to remove screen shake
                    IntPtr cameraServices = _gameProcess.Read<IntPtr>(player.AddressBase + Offsets.m_pCameraServices);
                    if (cameraServices != IntPtr.Zero)
                    {
                        successVisual = _gameProcess.Write(cameraServices + Offsets.m_vecCsViewPunchAngle, Vector3.Zero);
                    }
                }

                if ((DateTime.Now - _lastLogTime).TotalSeconds > 10)
                {
                    Console.WriteLine($"[NoRecoil] Status: Perfect={successPerfect}, Visual={successVisual} | Address: 0x{player.AddressBase:X}");
                    _lastLogTime = DateTime.Now;
                }
            }
            catch (Exception ex)
            {
                if ((DateTime.Now - _lastLogTime).TotalSeconds > 5)
                {
                    Console.WriteLine($"[NoRecoil] Error: {ex.Message}");
                    _lastLogTime = DateTime.Now;
                }
            }
        }
    }
}
