using System;
using System.Numerics;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Utils;

namespace CS2GameHelper.Features
{
    public class Movement : ThreadedServiceBase
    {
        private readonly GameProcess _gameProcess;
        private readonly GameData _gameData;
        private readonly UserInputHandler _inputHandler;
        private readonly ConfigManager _config;

        private const int FL_ONGROUND = 1 << 0;

        public Movement(GameProcess gameProcess, GameData gameData, UserInputHandler inputHandler, ConfigManager config)
        {
            _gameProcess = gameProcess;
            _gameData = gameData;
            _inputHandler = inputHandler;
            _config = config;
        }

        protected override string ThreadName => nameof(Movement);

        protected override void FrameAction()
        {
            if (!_gameProcess.IsValid || _gameData.Player == null || !_gameData.Player.IsAlive())
                return;

            if (_config.Movement.Bhop)
                HandleBhop();

            if (_config.Movement.AutoStrafe)
                HandleAutoStrafe();
        }

        private void HandleBhop()
        {
            if (_inputHandler.IsKeyDown(Utils.Keys.Space))
            {
                int flags = _gameData.Player!.FFlags;
                if ((flags & FL_ONGROUND) != 0)
                {
                    Utility.PressSpace();
                }
            }
        }

        private void HandleAutoStrafe()
        {
            int flags = _gameData.Player!.FFlags;
            if ((flags & FL_ONGROUND) == 0) // In air
            {
                var delta = _inputHandler.LastMouseDelta;
                if (delta.X < 0) // Moving left
                {
                    Utility.ReleaseKey(Utils.Keys.D);
                    Utility.PressKey(Utils.Keys.A);
                }
                else if (delta.X > 0) // Moving right
                {
                    Utility.ReleaseKey(Utils.Keys.A);
                    Utility.PressKey(Utils.Keys.D);
                }
            }
        }
    }
}
