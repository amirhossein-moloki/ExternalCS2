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

        public NoRecoil(GameProcess gameProcess, GameData gameData, ConfigManager config)
        {
            _gameProcess = gameProcess;
            _gameData = gameData;
            _config = config;
        }

        protected override string ThreadName => nameof(NoRecoil);

        protected override void FrameAction()
        {
            if (!_config.NoRecoil.PerfectNoRecoil && !_config.NoRecoil.NoVisualRecoil)
                return;

            try
            {
                if (_gameProcess == null || !_gameProcess.IsValid || _gameData?.Player == null)
                    return;

                var player = _gameData.Player;
                if (!player.IsAlive())
                    return;

                if (_config.NoRecoil.PerfectNoRecoil)
                {
                    // Write zero to aimPunchAngle to remove bullet kick
                    _gameProcess.Write(player.AddressBase + Offsets.m_AimPunchAngle, Vector3.Zero);
                }

                if (_config.NoRecoil.NoVisualRecoil)
                {
                    // Write zero to viewPunchAngle to remove screen shake
                    IntPtr cameraServices = _gameProcess.Read<IntPtr>(player.AddressBase + Offsets.m_pCameraServices);
                    if (cameraServices != IntPtr.Zero)
                    {
                        _gameProcess.Write(cameraServices + Offsets.m_vecCsViewPunchAngle, Vector3.Zero);
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
