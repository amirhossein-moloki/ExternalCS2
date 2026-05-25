using System;
using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using System.Threading;
using CS2GameHelper.Core;
using CS2GameHelper.Core.Data;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Features.Aiming;
using CS2GameHelper.Graphics;
using CS2GameHelper.Utils;
using Point = System.Drawing.Point;
using Keys = CS2GameHelper.Utils.Keys;

using CS2GameHelper.Utils.Registry;

namespace CS2GameHelper.Features
{
    public enum AimBotState { Up, DownSuppressed, Down }

    public record AimTargetResult(bool Found, Vector3 TargetPosition, Vector2 AimAngles, float Distance, int TargetId, Vector3 TargetVelocity);

    [Feature("aimbot", "Legit Aimbot", "Aimbot")]
    public class AimBot : ThreadedServiceBase
    {
        public bool IsActivelyTargeting { get; private set; }

        private const int SuppressMs = 200;
        private const int UserMouseDeltaResetMs = 50;
        private const int AimEventWindowMs = 1000;

        private readonly ConfigManager _config;
        private readonly object _stateLock = new();
        private readonly Random _humanizationRandom;

        // Кэшированные параметры настройки (читаются через _config.AimBotTuning).
        private readonly double _humanReactThreshold;
        private readonly double _humanEaseDistancePixels;
        private readonly double _humanMinimumGain;
        private readonly int _lockJitterStartMs;
        private readonly int _lockJitterStrongMs;
        private readonly int _minShootIntervalMs;
        private readonly int _aimUpdateIntervalMs;
        private readonly double _aimBotSmoothing;

        private static double _anglePerPixelHorizontal = 0.0006;
        private static double _anglePerPixelVertical = 0.0006;

        private readonly CompositeAimProvider _correctionProvider;
        private readonly TargetSelector _targetSelector;
        private readonly UserInputHandler _inputHandler;

        private double _aiAggressiveness = 1.0;
        private int _aimSuccessCount, _aimTotalCount;
        private double _dynamicFov = GraphicsMath.DegreeToRadian(15f);
        private double _dynamicSmoothing;
        private DateTime _lastAimEvent = DateTime.MinValue;

        private Stopwatch _fireStopwatch = new Stopwatch();
        private float _rcsAppliedX = 0;
        private float _rcsAppliedY = 0;
        private float _sprayScaleX = 1.0f;
        private float _sprayScaleY = 1.0f;
        private bool _isFiring = false;
        private DateTime _lastAiUpdate = DateTime.MinValue;
        private DateTime _lastSuppressed = DateTime.MinValue;
        private int _activeTargetId = -1;
        private DateTime _lastTargetLockTime = DateTime.MinValue;
        private double _userMoveAvg, _userMoveSum;
        private int _userMoveCount;
        private DateTime _lastShotTime = DateTime.MinValue;
        private bool _isCalibrated;

        // Контекст прицеливания (для расчёта deltaTime/accel и ConfirmHit).
        private DateTime _lastFrameTime = DateTime.MinValue;
        private Vector3 _lastTargetVelocity = Vector3.Zero;
        private int _lastTargetIdForAccel = -1;
        private int _aimBotLastDamage;

        public AimBot(GameProcess gameProcess, GameData gameData, UserInputHandler inputHandler, ConfigManager config)
        {
            GameProcess = gameProcess;
            GameData = gameData;
            _inputHandler = inputHandler;
            _config = config;

            var tuning = _config.AimBotTuning ?? new ConfigManager.AimBotTuningConfig();
            _humanReactThreshold = tuning.HumanReactThreshold;
            _humanEaseDistancePixels = tuning.HumanEaseDistancePixels;
            _humanMinimumGain = tuning.HumanMinimumGain;
            _lockJitterStartMs = tuning.LockJitterStartMs;
            _lockJitterStrongMs = tuning.LockJitterStrongMs;
            _minShootIntervalMs = tuning.MinShootIntervalMs;
            _aimUpdateIntervalMs = tuning.AimUpdateIntervalMs;
            _aimBotSmoothing = tuning.AimSmoothing;
            _dynamicSmoothing = _aimBotSmoothing;
            _humanizationRandom = tuning.HumanizationSeed > 0
                ? new Random(tuning.HumanizationSeed)
                : new Random();

            _correctionProvider = new CompositeAimProvider();
            _targetSelector = new TargetSelector();
            Console.WriteLine("[AimBot] Initialized with composite correction and shared input handler.");
        }

        protected override string ThreadName => nameof(AimBot);
        public GameProcess? GameProcess { get; set; }
        public GameData? GameData { get; set; }

        public override void Dispose()
        {
            _correctionProvider.Save();
            _correctionProvider.Dispose();
            // NOTE: _inputHandler is shared (owned by Program). Do NOT dispose it here,
            // otherwise hooks are unhooked while other features still need them and
            // Program.Dispose triggers a redundant unhook/finalizer pass.
            base.Dispose();
        }

        // Используем UserInputHandler для проверки хоткея (читаем из _config напрямую —
        // чтобы изменение AimBotKey через меню применялось без перезапуска).
        private bool IsHotKeyDown() => _inputHandler.IsKeyDown(_config.AimBotKey);

        private static bool TryMouseMoveNew(Point aimPixels)
        {
            if (aimPixels.X == 0 && aimPixels.Y == 0) return false;
            if (Math.Abs(aimPixels.X) > 100 || Math.Abs(aimPixels.Y) > 100) return false;
            Utility.WindMouseMove(aimPixels.X, aimPixels.Y, G_0: 9.0, W_0: 3.0, M_0: 15.0, D_0: 12.0);
            return true;
        }

        protected override void FrameAction()
        {
            if (!_config.AimBot)
            {
                IsActivelyTargeting = false;
                return;
            }

            bool isManualMode = IsHotKeyDown();
            bool isAutoMode = _config.AimBotAutoShoot;

            if (!isManualMode && !isAutoMode)
            {
                IsActivelyTargeting = false;
                return;
            }

            try
            {
                if (GameProcess == null || !GameProcess.IsValid || GameData?.Player == null)
                {
                    IsActivelyTargeting = false;
                    return;
                }

                var player = GameData.Player;
                if (!player.IsAlive())
                {
                    IsActivelyTargeting = false;
                    return;
                }

                var userMoveLen = Math.Sqrt(_inputHandler.LastMouseDelta.X * (double)_inputHandler.LastMouseDelta.X + _inputHandler.LastMouseDelta.Y * (double)_inputHandler.LastMouseDelta.Y);
                if (userMoveLen > _humanReactThreshold) _lastSuppressed = DateTime.Now;
                if ((DateTime.Now - _lastSuppressed).TotalMilliseconds < SuppressMs)
                {
                    IsActivelyTargeting = false;
                    _isFiring = false;
                    _fireStopwatch.Reset();
                    return;
                }

                // Professional RCS fire timing
                bool isCurrentlyFiring = _inputHandler.IsKeyDown(Keys.LButton) || player.ShotsFired > 0;
                if (isCurrentlyFiring && !_isFiring)
                {
                    _fireStopwatch.Restart();
                    _isFiring = true;
                    _rcsAppliedX = 0;
                    _rcsAppliedY = 0;

                    var weaponInfo = PatternManager.GetWeaponInfo(player.CurrentWeaponName);
                    if (weaponInfo != null && weaponInfo.JitterMovement > 0)
                    {
                        float deviation = weaponInfo.JitterMovement / 100.0f;
                        float sigma = deviation / 2.0f;
                        // Approximate Gaussian using sum of uniforms
                        float randGauss() => (float)((_humanizationRandom.NextDouble() + _humanizationRandom.NextDouble() + _humanizationRandom.NextDouble() - 1.5) * 2.0 * sigma);
                        _sprayScaleX = 1.0f + randGauss();
                        _sprayScaleY = 1.0f + randGauss();
                    }
                    else
                    {
                        _sprayScaleX = 1.0f;
                        _sprayScaleY = 1.0f;
                    }
                }
                else if (!isCurrentlyFiring)
                {
                    _isFiring = false;
                    _fireStopwatch.Reset();
                    _rcsAppliedX = 0;
                    _rcsAppliedY = 0;
                }

                if (!_isCalibrated)
                {
                    Calibrate();
                    _isCalibrated = true;
                }

                if ((DateTime.Now - _lastAiUpdate).TotalMilliseconds > _aimUpdateIntervalMs && _userMoveCount > 0)
                {
                    _userMoveAvg = _userMoveSum / _userMoveCount;
                    _aiAggressiveness = 1.0 - Math.Min(_userMoveAvg / 20.0, 0.7);
                    _userMoveSum = 0; _userMoveCount = 0; _lastAiUpdate = DateTime.Now;
                }

                if (_aimTotalCount > 0 && (DateTime.Now - _lastAimEvent).TotalMilliseconds > AimEventWindowMs)
                {
                    var successRate = _aimSuccessCount / (double)_aimTotalCount;
                    if (successRate < 0.5)
                    {
                        _dynamicFov = Math.Max(GraphicsMath.DegreeToRadian(5f), _dynamicFov - GraphicsMath.DegreeToRadian(0.5f));
                        _dynamicSmoothing = Math.Min(_dynamicSmoothing + 0.5, 10.0);
                    }
                    else if (successRate > 0.8)
                    {
                        _dynamicFov = Math.Min(GraphicsMath.DegreeToRadian(30f), _dynamicFov + GraphicsMath.DegreeToRadian(0.5f));
                        _dynamicSmoothing = Math.Max(_dynamicSmoothing - 0.5, 1.0);
                    }
                    _aimSuccessCount = 0; _aimTotalCount = 0; _lastAimEvent = DateTime.Now;
                }

                var aimResult = _targetSelector.FindBestTarget(GameData, _dynamicFov, _activeTargetId);
                if (aimResult.Found)
                {
                    if (aimResult.TargetId != _activeTargetId)
                    {
                        _activeTargetId = aimResult.TargetId;
                        _lastTargetLockTime = DateTime.Now;
                    }
                    IsActivelyTargeting = isManualMode || isAutoMode;
                }
                else
                {
                    _activeTargetId = -1;
                    _lastTargetLockTime = DateTime.MinValue;
                    IsActivelyTargeting = false;
                }
                Point aimPixels = Point.Empty;

                // === Расчёт фич для AimContext ===
                var nowFrame = DateTime.UtcNow;
                float deltaTimeMs = _lastFrameTime == DateTime.MinValue ? 16f
                    : (float)Math.Clamp((nowFrame - _lastFrameTime).TotalMilliseconds, 1.0, 100.0);
                _lastFrameTime = nowFrame;
                float aimSpeed = (float)Math.Sqrt(
                    _inputHandler.LastMouseDelta.X * (double)_inputHandler.LastMouseDelta.X +
                    _inputHandler.LastMouseDelta.Y * (double)_inputHandler.LastMouseDelta.Y);

                float targetAccelMag = 0f;
                if (aimResult.Found)
                {
                    if (aimResult.TargetId == _lastTargetIdForAccel)
                    {
                        var dv = aimResult.TargetVelocity - _lastTargetVelocity;
                        targetAccelMag = dv.Length() / Math.Max(deltaTimeMs, 1f) * 1000f; // в unit/s^2
                    }
                    _lastTargetVelocity = aimResult.TargetVelocity;
                    _lastTargetIdForAccel = aimResult.TargetId;
                }
                else
                {
                    _lastTargetIdForAccel = -1;
                    _lastTargetVelocity = Vector3.Zero;
                }

                float currentRecoilScale = _config.Rcs.GlobalScale;

                Point rcsDeltaPixels = Point.Empty;
                if (_config.Rcs.Enabled && _isFiring)
                {
                    var weaponInfo = PatternManager.GetWeaponInfo(player.CurrentWeaponName);
                    if (weaponInfo != null && player.ShotsFired > 0)
                    {
                        double elapsedMs = _fireStopwatch.Elapsed.TotalMilliseconds;
                        var pattern = weaponInfo.Pattern;
                        int currIdx = 0;
                        double accTime = 0;
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
                        for (int i = 0; i <= currIdx; i++)
                        {
                            totalDx += pattern[i].Dx * sensScale * _sprayScaleX;
                            totalDy += -pattern[i].Dy * sensScale * _sprayScaleY;
                        }

                        rcsDeltaPixels.X = (int)Math.Round(totalDx - _rcsAppliedX);
                        rcsDeltaPixels.Y = (int)Math.Round(totalDy - _rcsAppliedY);
                    }
                }

                if (aimResult.Found)
                {
                    // Calculate aimbot correction accounting for TOTAL intended recoil
                    float sensScaleInner = 2.45f / _config.Rcs.Sensitivity;
                    float totalRcsX = 0, totalRcsY = 0;
                    var weaponInfo = PatternManager.GetWeaponInfo(player.CurrentWeaponName);
                    if (weaponInfo != null && player.ShotsFired > 0)
                    {
                        double elapsedMs = _fireStopwatch.Elapsed.TotalMilliseconds;
                        double accTime = 0;
                        for (int i = 0; i < weaponInfo.Pattern.Count; i++)
                        {
                            double delay = weaponInfo.Pattern[i].Delay / weaponInfo.SleepDivider - weaponInfo.SleepSuber;
                            accTime += delay;
                            if (accTime > elapsedMs) break;
                            totalRcsX += weaponInfo.Pattern[i].Dx * sensScaleInner * _sprayScaleX;
                            totalRcsY += -weaponInfo.Pattern[i].Dy * sensScaleInner * _sprayScaleY;
                        }
                    }

                    Vector2 patternAngles = new Vector2(
                        (float)(totalRcsX * _anglePerPixelHorizontal),
                        (float)(-totalRcsY * _anglePerPixelVertical)
                    );

                    AimingMath.GetAimAngles(player, aimResult.TargetPosition, 0f, out _, out var angles, patternAngles);
                    AimingMath.GetAimPixels(angles, _anglePerPixelHorizontal, _anglePerPixelVertical, out aimPixels);

                    var ctx = new AimContext(
                        aimResult.Distance,
                        aimResult.TargetPosition,
                        player.EyePosition,
                        aimResult.TargetVelocity,
                        deltaTimeMs,
                        aimSpeed,
                        targetAccelMag);
                    var correction = _correctionProvider.GetCorrection(in ctx);

                    aimPixels.X = (int)Math.Round(aimPixels.X + correction.X);
                    aimPixels.Y = (int)Math.Round(aimPixels.Y + correction.Y);
                }

                ApplyHumanizedAimAdjustments(ref aimPixels, aimResult);

                aimPixels.X = Math.Max(Math.Min(aimPixels.X, 50), -50);
                aimPixels.Y = Math.Max(Math.Min(aimPixels.Y, 50), -50);

                var adapt = _aiAggressiveness;
                if ((DateTime.Now - _inputHandler.LastMouseMoveTime).TotalMilliseconds < UserMouseDeltaResetMs)
                    adapt *= 0.5;

                double smoothing = Math.Max(1.0, _dynamicSmoothing);

                // Professional Integration: Smoothed AimBot + Direct RCS Delta
                double finalX = (aimPixels.X * adapt / smoothing) + rcsDeltaPixels.X;
                double finalY = (aimPixels.Y * adapt / smoothing) + rcsDeltaPixels.Y;

                // Update applied tracking
                _rcsAppliedX += rcsDeltaPixels.X;
                _rcsAppliedY += rcsDeltaPixels.Y;

                // Humanization/Rounding fix: If the value is very small but not zero,
                // we should round it instead of truncating to zero,
                // to avoid "mushy" feel when small adjustments are needed.
                aimPixels.X = (int)(Math.Abs(finalX) > 0.01 && Math.Abs(finalX) < 1.0 ? Math.Sign(finalX) : Math.Round(finalX));
                aimPixels.Y = (int)(Math.Abs(finalY) > 0.01 && Math.Abs(finalY) < 1.0 ? Math.Sign(finalY) : Math.Round(finalY));

                var shouldWait = false;

                if (aimResult.Found && (isManualMode || isAutoMode))
                {
                    if (isAutoMode && (DateTime.Now - _lastShotTime).TotalMilliseconds > _minShootIntervalMs)
                    {
                        Utility.MouseLeftDown();
                        Thread.Sleep(10);
                        // Only release if the user isn't manually holding the fire button
                        if (!_inputHandler.IsKeyDown(Keys.LButton))
                        {
                            Utility.MouseLeftUp();
                        }
                        _lastShotTime = DateTime.Now;
                        shouldWait = true;
                    }
                }

                if ((isManualMode || isAutoMode) && (aimPixels.X != 0 || aimPixels.Y != 0))
                {
                    if (Math.Abs(aimPixels.X) > 3 || Math.Abs(aimPixels.Y) > 3)
                    {
                        Utility.WindMouseMove(aimPixels.X, aimPixels.Y, G_0: 8.0, W_0: 2.5, M_0: 12.0, D_0: 10.0);
                    }
                    else
                    {
                        Utility.MouseMove(aimPixels.X, aimPixels.Y);
                    }

                    shouldWait = true;
                }

                if (shouldWait) Thread.Sleep(1);
                if (aimResult.Found) _aimSuccessCount++;

                // === СБОР ОСТАТКОВ БЕЗ SLEEP ===
                if (aimResult.Found)
                {
                    // 1. Сохраняем НАПРАВЛЕНИЕ ДО движения мыши
                    var aimDirectionBefore = player.AimDirection;

                    // 2. Вычисляем ЖЕЛАЕМЫЙ вектор взгляда
                    var desiredDirection = (aimResult.TargetPosition - player.EyePosition).GetNormalized();

                    // 3. Вычисляем УГЛОВУЮ ошибку (в радианах)
                    var horizontalError = desiredDirection.GetSignedAngleTo(aimDirectionBefore, new Vector3(0, 0, 1));
                    var verticalError = desiredDirection.GetSignedAngleTo(aimDirectionBefore,
                        Vector3.Cross(desiredDirection, new Vector3(0, 0, 1)).GetNormalized());

                    // 4. Конвертируем ошибку в ПИКСЕЛИ (как если бы мы её компенсировали)
                    var errorPixelsX = horizontalError / _anglePerPixelHorizontal;
                    var errorPixelsY = verticalError / _anglePerPixelVertical;

                    // 5. Но мы уже ВЫПОЛНИЛИ коррекцию через aimPixels!
                    //    Поэтому реальный остаток = ошибка ДО коррекции - то, что мы применили
                    var appliedCorrectionX = aimPixels.X;
                    var appliedCorrectionY = aimPixels.Y;

                    var residualX = (float)(errorPixelsX - appliedCorrectionX);
                    var residualY = (float)(errorPixelsY - appliedCorrectionY);

                    // 6. Добавляем наблюдение (пойдёт в PENDING; промоверится при ConfirmHit)
                    var obsCtx = new AimContext(
                        aimResult.Distance,
                        aimResult.TargetPosition,
                        player.EyePosition,
                        aimResult.TargetVelocity,
                        deltaTimeMs,
                        aimSpeed,
                        targetAccelMag);
                    _correctionProvider.AddObservation(in obsCtx, residualX, residualY);
                }

                // === Детект попадания по дельте урона → ConfirmHit() для обучения ===
                TryConfirmHitFromDamage();

                _aimTotalCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AimBot ERROR] {ex.Message}\n{ex.StackTrace}");
            }
        }


        private void ApplyHumanizedAimAdjustments(ref Point aimPixels, AimTargetResult aimResult)
        {
            if (!aimResult.Found) return;

            var lockDuration = (DateTime.Now - _lastTargetLockTime).TotalMilliseconds;
            var pixelDistance = Math.Sqrt(aimPixels.X * (double)aimPixels.X + aimPixels.Y * (double)aimPixels.Y);

            if (pixelDistance > 0)
            {
                var gain = Math.Clamp(pixelDistance / _humanEaseDistancePixels, _humanMinimumGain, 1.0);
                aimPixels.X = (int)Math.Round(aimPixels.X * gain);
                aimPixels.Y = (int)Math.Round(aimPixels.Y * gain);
            }

            if (lockDuration > _lockJitterStartMs && pixelDistance < 8)
            {
                var jitterRange = lockDuration > _lockJitterStrongMs ? 2 : 1;
                aimPixels.X += _humanizationRandom.Next(-jitterRange, jitterRange + 1);
                aimPixels.Y += _humanizationRandom.Next(-jitterRange, jitterRange + 1);
            }
        }
        private void TryConfirmHitFromDamage()
        {
            try
            {
                if (GameProcess?.Process == null || GameProcess.ModuleClient == null) return;
                var localController = GameProcess.ModuleClient.Read<IntPtr>(Offsets.client_dll.dwLocalPlayerController);
                if (localController == IntPtr.Zero) return;
                var actionTracking = GameProcess.Read<IntPtr>(
                    IntPtr.Add(localController, Offsets.m_pActionTrackingServices));
                if (actionTracking == IntPtr.Zero) return;
                int currentDamage = GameProcess.Read<int>(
                    IntPtr.Add(actionTracking, Offsets.m_flTotalRoundDamageDealt));

                // Сначала калибруем (например, после перезахода в раунд damage может сброситься).
                if (currentDamage < _aimBotLastDamage)
                {
                    _aimBotLastDamage = currentDamage;
                    return;
                }

                if (currentDamage > _aimBotLastDamage)
                {
                    _correctionProvider.ConfirmHit();
                    _aimBotLastDamage = currentDamage;
                }
            }
            catch
            {
                // Чтение памяти может вернуть мусор — глушим, цикл аим-бота не должен падать.
            }
        }

        private void Calibrate()
        {
            var horizontalSamples = new[]
            {
                CalibrationMeasureHorizontalAnglePerPixel(100),
                CalibrationMeasureHorizontalAnglePerPixel(-200),
                CalibrationMeasureHorizontalAnglePerPixel(300)
            }.Where(sample => sample > 0 && !double.IsInfinity(sample) && !double.IsNaN(sample)).ToList();

            if (horizontalSamples.Count > 0)
                _anglePerPixelHorizontal = horizontalSamples.Average();
            else
                _anglePerPixelHorizontal = 0.0006; // Fallback for 400-800 DPI typical range

            var verticalSamples = new[]
            {
                CalibrationMeasureVerticalAnglePerPixel(60),
                CalibrationMeasureVerticalAnglePerPixel(-120),
                CalibrationMeasureVerticalAnglePerPixel(180)
            }.Where(sample => sample > 0 && !double.IsInfinity(sample) && !double.IsNaN(sample)).ToList();

            if (verticalSamples.Count > 0)
                _anglePerPixelVertical = verticalSamples.Average();
            else
                _anglePerPixelVertical = _anglePerPixelHorizontal;

            Console.WriteLine($"[AimBot] Calibrated: H={_anglePerPixelHorizontal:F8}, V={_anglePerPixelVertical:F8}");
        }

        private double CalibrationMeasureHorizontalAnglePerPixel(int deltaPixels)
        {
            Thread.Sleep(100);
            if (GameData?.Player == null) return 0.0;
            var yawStart = AimingMath.GetYaw(GameData.Player.EyeDirection);
            Utility.MouseMove(deltaPixels, 0);
            Thread.Sleep(100);
            if (GameData?.Player == null) return 0.0;
            var yawEnd = AimingMath.GetYaw(GameData.Player.EyeDirection);
            return Math.Abs(AimingMath.NormalizeRadians(yawEnd - yawStart)) / Math.Abs(deltaPixels);
        }

        private double CalibrationMeasureVerticalAnglePerPixel(int deltaPixels)
        {
            Thread.Sleep(100);
            if (GameData?.Player == null) return 0.0;
            var pitchStart = AimingMath.GetPitch(GameData.Player.EyeDirection);
            Utility.MouseMove(0, deltaPixels);
            Thread.Sleep(100);
            if (GameData?.Player == null) return 0.0;
            var pitchEnd = AimingMath.GetPitch(GameData.Player.EyeDirection);
            return Math.Abs(pitchEnd - pitchStart) / Math.Abs(deltaPixels);
        }
    }
}