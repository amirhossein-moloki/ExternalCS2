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
                // Visibility check (IsSpotted)
                if (!entity.IsSpotted) continue;

                Vector3 targetVelocity = entity.Velocity;
                float distanceToTarget = Vector3.Distance(playerPos, entity.Position);

                Vector3 relativeVelocity = targetVelocity - playerVel;
                // Prediction based on travel time + approx latency
                float predictionTime = 0.015f + (distanceToTarget / 20000f);

                Vector3? bestBonePos = null;
                float bestBoneScore = float.MaxValue;
                Vector2 bestBoneAngles = Vector2.Zero;

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

                    // Priority weight: strict hierarchy. Head is much better than others.
                    float priorityWeight = bone switch
                    {
                        "head" => 0.1f,
                        "neck_0" => 0.5f,
                        "spine_1" => 0.9f,
                        _ => 1.0f
                    };

                    float boneScore = (float)(effectiveAngle * 100.0 * priorityWeight);

                    if (boneScore < bestBoneScore)
                    {
                        bestBoneScore = boneScore;
                        bestBonePos = predictedPos;
                        AimingMath.GetAimAngles(gameData.Player, predictedPos, 0f, out _, out bestBoneAngles);
                    }
                }

                if (bestBonePos.HasValue)
                {
                    // Overall entity score depends on distance and how good its best bone is
                    float entityScore = (float)(bestBoneScore + distanceToTarget / 500.0);

                    if (entityScore < minScore)
                    {
                        minScore = entityScore;
                        bestAimAngles = bestBoneAngles;
                        bestAimPosition = bestBonePos.Value;
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
