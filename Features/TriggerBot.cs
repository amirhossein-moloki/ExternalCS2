using CS2GameHelper.Utils.Registry;
using System;
using System.Threading.Tasks;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Graphics;
using CS2GameHelper.Core.Data;
using System.Numerics;
using CS2GameHelper.Utils;
using Vector3 = System.Numerics.Vector3;
using Keys = CS2GameHelper.Utils.Keys;

namespace CS2GameHelper.Features;

[Feature("triggerbot", "Triggerbot", "Triggerbot")]
public sealed class TriggerBot : ThreadedServiceBase
{
    private const float MaxSpeedThreshold = 250f;      // ~макс. скорость бега в CS2
    private const float TriggerFovDegrees = 5f;        // FOV для триггера
    private const int TriggerDelayMs = 5;
    private static readonly TimeSpan MinShotInterval = TimeSpan.FromMilliseconds(100);

    private readonly GameProcess _gameProcess;
    private readonly GameData _gameData;
    private readonly UserInputHandler _inputHandler;
    private readonly Keys _triggerBotHotKey;

    private DateTime _lastShot = DateTime.MinValue;

    public TriggerBot(GameProcess gameProcess, GameData gameData, UserInputHandler inputHandler)
    {
        _gameProcess = gameProcess ?? throw new ArgumentNullException(nameof(gameProcess));
        _gameData = gameData ?? throw new ArgumentNullException(nameof(gameData));
        _inputHandler = inputHandler ?? throw new ArgumentNullException(nameof(inputHandler));
        _triggerBotHotKey = ConfigManager.Load().TriggerBotKey;
    }

    protected override string ThreadName => nameof(TriggerBot);

    protected override void FrameAction()
    {
        if (!ShouldExecuteTriggerBot())
            return;

        if (_gameProcess.Process == null || _gameData.Player == null)
            return;

        // Try Box-based detection first for better accuracy
        bool foundViaBox = false;
        var center = new Vector2(_gameProcess.WindowRectangleClient.Width / 2f, _gameProcess.WindowRectangleClient.Height / 2f);

        foreach (var entity in _gameData.Entities)
        {
            if (!entity.IsAlive() || entity.AddressBase == _gameData.Player.AddressBase) continue;
            if (entity.Team == _gameData.Player.Team) continue;

            var bbox = GetEntityBoundingBox(_gameData.Player, entity);
            if (bbox == null) continue;

            var (topLeft, bottomRight) = bbox.Value;
            if (center.X >= topLeft.X && center.X <= bottomRight.X &&
                center.Y >= topLeft.Y && center.Y <= bottomRight.Y)
            {
                foundViaBox = true;
                break;
            }
        }

        if (foundViaBox)
        {
            _lastShot = DateTime.Now;
            ExecuteTrigger();
            return;
        }

        // Fallback to m_iIDEntIndex
        var targetEntity = GetTargetEntity();
        if (targetEntity != IntPtr.Zero)
        {
            var entityTeam = _gameProcess.Read<int>(targetEntity + Offsets.m_iTeamNum);
            if (ShouldTriggerOnEntity(entityTeam))
            {
                _lastShot = DateTime.Now;
                ExecuteTrigger();
            }
        }
    }

    private static (Vector2, Vector2)? GetEntityBoundingBox(Player player, Entity entity)
    {
        var matrix = player.MatrixViewProjectionViewport;
        if (entity.BonePos == null || !entity.BonePos.TryGetValue("head", out var headPos))
            return null;

        var origin = entity.Position;
        var head = headPos + new Vector3(0, 0, 10);
        var bottom = origin - new Vector3(0, 0, 5);

        var headProj = matrix.Transform(head);
        var bottomProj = matrix.Transform(bottom);

        // headProj.Z and bottomProj.Z contain the W component (distance)
        if (headProj.Z < 0.001f || bottomProj.Z < 0.001f)
            return null;

        float height = Math.Abs(headProj.Y - bottomProj.Y);
        float width = height / 2f;

        var topLeft = new Vector2(headProj.X - width / 2f, headProj.Y);
        var bottomRight = new Vector2(headProj.X + width / 2f, bottomProj.Y);

        return (topLeft, bottomRight);
    }

    private bool ShouldExecuteTriggerBot()
    {
        return _gameProcess.IsValid && IsHotKeyDown() && (DateTime.Now - _lastShot) >= MinShotInterval;
    }

    private IntPtr GetTargetEntity()
    {
        if (_gameProcess.ModuleClient == null || _gameProcess.Process == null)
            return IntPtr.Zero;

        var localPlayerPawn = _gameProcess.ModuleClient.Read<IntPtr>(Offsets.dwLocalPlayerPawn);
        if (localPlayerPawn == IntPtr.Zero)
            return IntPtr.Zero;

        var entityId = _gameProcess.Read<int>(localPlayerPawn + Offsets.m_iIDEntIndex);
        if (entityId < 0)
            return IntPtr.Zero;

        var entityList = _gameProcess.ModuleClient.Read<IntPtr>(Offsets.dwEntityList);
        var entityEntry = _gameProcess.Read<IntPtr>(
            entityList + 8 * (entityId >> 9) + 0x10);

        return _gameProcess.Read<IntPtr>(
            entityEntry + 112 * (entityId & 0x1FF));
    }

    private bool ShouldTriggerOnEntity(int entityTeam)
    {
        if (_gameData.Player == null)
            return false;

        var playerTeam = _gameData.Player.Team;
        var targetTeam = entityTeam.ToTeam();

        if (playerTeam == targetTeam || targetTeam == Team.Spectator || targetTeam == Team.Unknown)
            return false;

        // Проверяем, стоит ли игрок на земле или в приседе (стабильное положение)
        var isStable = (_gameData.Player.FFlags & (int)EntityFlags.OnGround) != 0;

        // Проверяем общую скорость (не только по Z)
        var speed = _gameData.Player.Velocity.Length();
        var isWithinSpeedLimit = speed <= MaxSpeedThreshold;

        return isStable && isWithinSpeedLimit;
    }

    private bool IsAimingAtEntity(IntPtr entityPtr)
    {
        if (_gameData.Player == null)
            return false;

        if (_gameProcess.Process == null)
            return false;

        // Читаем позицию головы (или origin, если костей нет)
        var origin = _gameProcess.Read<Vector3>(entityPtr + Offsets.m_vecOrigin);
        var eyePos = _gameData.Player.EyePosition;
        var toTarget = origin - eyePos;
        var distance = toTarget.Length();

        if (distance < 1f)
            return false;

        var aimDir = toTarget / distance;
        var playerDir = _gameData.Player.AimDirection;

        var angle = aimDir.GetAngleTo(playerDir);
        return angle <= GraphicsMath.DegreeToRadian(TriggerFovDegrees);
    }

    private bool IsHotKeyDown() => _inputHandler.IsKeyDown(_triggerBotHotKey);

    private void ExecuteTrigger()
    {
        if (TriggerDelayMs > 0)
            Thread.Sleep(TriggerDelayMs);

        Utility.MouseLeftDown();
        Thread.Sleep(10);
        Utility.MouseLeftUp();
    }

    public override void Dispose()
    {
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}