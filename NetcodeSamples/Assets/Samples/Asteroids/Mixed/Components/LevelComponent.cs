using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

/// <summary>Serializable 属性确保 Inspector 可以公开字段，因为该结构是 ServerSettings.</summary> 内的字段
[Serializable]
public struct LevelComponent : IComponentData
{
    public int levelWidth;
    public int levelHeight;

    public float shipForwardForce;
    public float shipRotationRate;
    public float shipCollisionRadius;

    public float bulletVelocity;
    public float bulletCollisionRadius;
    /// <summary>Value 为 0 意味着每个模拟刻度有一颗子弹。ROF 不能高于 that.</summary>
    public uint bulletRofCooldownTicks;

    public float asteroidVelocity;
    public float asteroidCollisionRadius;
    public int numAsteroids;

    public bool asteroidsDamageShips;
    /// <summary>Can 船只互相摧毁？</summary>
    public bool shipPvP;
    public bool asteroidsDestroyedOnShipContact;
    public bool bulletsDestroyedOnContact;

    /// <summary>When > 0，通知 <see cref="Unity.NetCode.GhostRelevancyMode"/>。Optimization.</summary>
    /// <remarks>
    /// Note: 如果选中 <see cref="enableGhostImportanceScaling"/>，则 package 使用中定义的 const
    /// <see cref="GhostDistanceImportance.BatchScaleWithRelevancyFunctionPointer"/> 代替该字段。
    /// </remarks>
    public int relevancyRadius;
    public bool staticAsteroidOptimization;

    public bool enableGhostImportanceScaling;
    public GhostDistanceData distanceImportanceTileConfig;

    /// <summary>Distributes CollisionSystem 工作超过 N 个刻度 (1 = OFF).</summary>
    [Min(1)]
    public uint collisionSystemRoundRobinSegments;

    public static LevelComponent Default = new LevelComponent
    {
        levelWidth = 2048,
        levelHeight = 2048,

        shipForwardForce = 50,
        shipRotationRate = 140,
        shipCollisionRadius = 10,

        bulletVelocity = 500,
        bulletCollisionRadius = 5,
        bulletRofCooldownTicks = 3,

        asteroidVelocity = 10,
        asteroidCollisionRadius = 15,
        numAsteroids = 800,

        asteroidsDamageShips = true,
        shipPvP = true,
        asteroidsDestroyedOnShipContact = true,
        bulletsDestroyedOnContact = true,

        enableGhostImportanceScaling = true,
        distanceImportanceTileConfig = new GhostDistanceData
        {
            TileSize = new int3(512, 512, 10240),
            TileCenter = new int3(0, 0, 0),
            TileBorderWidth = new float3(1f),
        },
        relevancyRadius = 1400,
        staticAsteroidOptimization = false,
        collisionSystemRoundRobinSegments = 1,
    };
}
