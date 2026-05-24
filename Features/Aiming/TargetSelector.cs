using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CS2GameHelper.Data.Entity;
using CS2GameHelper.Data.Game;
using CS2GameHelper.Graphics;
using CS2GameHelper.Features;

namespace CS2GameHelper.Features.Aiming
{
    public class TargetSelector
    {
        // Matching bone names from Offsets.cs
        private static readonly string[] AimBonePriority = { "head", "neck_0", "spine_1", "pelvis" };
        private const float StickinessMultiplier = 0.5f;

        public AimTargetResult FindBestTarget(GameData gameData, double customFov, int currentTargetId)
        {
            if (gameData?.Player == null || gameData.Entities == null)
                return new AimTargetResult(false, Vector3.Zero, Vector2.Zero, 0f, -1, Vector3.Zero);

            var minScore = float.MaxValue;
            Vector2 bestAimAngles = Vector2.Zero;
            Vector3 bestAimPosition = Vector3.Zero;
            float bestDistance = 0f;
            int bestTargetId = -1;
            Vector3 bestTargetVelocity = Vector3.Zero;
            bool targetFound = false;

            Vector3 playerPos = gameData.Player.EyePosition;
            Vector3 playerVel = gameData.Player.Velocity;
            Vector3 playerLookDir = gameData.Player.EyeDirection;

            foreach (var entity in gameData.Entities.Where(entity =>
                entity.IsAlive() &&
                entity.AddressBase != gameData.Player.AddressBase &&
                entity.Team != gameData.Player.Team))
            {
                Vector3 targetVelocity = entity.Velocity;
                float distanceToTarget = Vector3.Distance(playerPos, entity.Position);

                Vector3 relativeVelocity = targetVelocity - playerVel;
                // Prediction based on travel time + approx latency
                float predictionTime = 0.015f + (distanceToTarget / 20000f);

                foreach (var bone in AimBonePriority)
                {
                    if (!entity.BonePos.TryGetValue(bone, out var bonePos)) continue;

                    Vector3 predictedPos = bonePos + relativeVelocity * predictionTime;

                    var dirToTarget = (predictedPos - playerPos).GetNormalized();
                    float angleFromCrosshair = playerLookDir.GetAngleTo(dirToTarget);

                    float effectiveAngle = angleFromCrosshair;
                    if (entity.Id == currentTargetId)
                    {
                        effectiveAngle *= StickinessMultiplier;
                    }

                    if (effectiveAngle > customFov) continue;

                    // Score: smaller angle is much better than closer distance
                    float score = (float)(effectiveAngle * 100.0 + distanceToTarget / 1000.0);

                    if (score < minScore)
                    {
                        minScore = score;
                        AimingMath.GetAimAngles(gameData.Player, predictedPos, out _, out bestAimAngles);

                        bestAimPosition = predictedPos;
                        bestDistance = distanceToTarget;
                        bestTargetId = entity.Id;
                        bestTargetVelocity = targetVelocity;
                        targetFound = true;
                    }
                }
            }

            return new AimTargetResult(targetFound, bestAimPosition, bestAimAngles, bestDistance, bestTargetId, bestTargetVelocity);
        }
    }
}
