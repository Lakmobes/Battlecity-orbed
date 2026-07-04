using System.Numerics;

using Arch.Core;

using BattleCity.Core.Audio;
using BattleCity.Core.Ecs.Components;
using BattleCity.Core.Ecs.Systems;
using BattleCity.Shared.Constants;
using BattleCity.Shared.Data;
using BattleCity.Shared.Network.Packets;

namespace BattleCity.Core.Gameplay;

/// <summary>Shared weapon fire rules for offline sim, online client prediction, and authoritative server.</summary>
public static class WeaponActions
{
    public static void AdvanceCooldowns(ref WeaponState weapons, float deltaSeconds)
    {
        weapons.LaserCooldownSeconds = Math.Max(0f, weapons.LaserCooldownSeconds - deltaSeconds);
        weapons.RocketCooldownSeconds = Math.Max(0f, weapons.RocketCooldownSeconds - deltaSeconds);
        weapons.FlareCooldownSeconds = Math.Max(0f, weapons.FlareCooldownSeconds - deltaSeconds);
    }

    public static bool TryFireFromInput(
        World world,
        Entity owner,
        ref InputCommand input,
        ref WeaponState weapons,
        ref PlayerInventory inventory,
        ref TankFacing facing,
        ref TankLifeState life,
        ref TankStatus status,
        Vector2 tankTopLeft,
        SimulationAudioBuffer? audio,
        out ClientShotPacket? networkShot)
    {
        networkShot = null;

        if (life.IsDead || status.IsFrozen)
        {
            return false;
        }

        if (input.FireFlareHeld && weapons.FlareCooldownSeconds <= 0f && inventory.Flare > 0)
        {
            var centerDirection = (facing.Direction + 16) % TankFacing.DirectionCount;
            FireFlare(world, owner, tankTopLeft, facing.Direction, audio);
            inventory.Flare--;
            weapons.FlareCooldownSeconds = GameConstants.TimerShootFlare / 1000f;
            networkShot = CreateShotPacket(tankTopLeft, facing.Direction, bulletType: 3, centerDirection);
            return true;
        }

        if (!input.FireHeld)
        {
            return false;
        }

        var isStopped = input.Move == 0;
        if (isStopped &&
            inventory.Rocket > 0 &&
            weapons.RocketCooldownSeconds <= 0f &&
            weapons.LaserCooldownSeconds <= 0f)
        {
            FireSingle(world, owner, tankTopLeft, facing.Direction, BulletKind.Rocket, audio);
            weapons.RocketCooldownSeconds = GameConstants.TimerShootRocket / 1000f;
            weapons.LaserCooldownSeconds = GameConstants.TimerShootRocket / 1000f;
            networkShot = CreateShotPacket(tankTopLeft, facing.Direction, bulletType: 1, packetDirection: facing.Direction);
            return true;
        }

        if (weapons.LaserCooldownSeconds <= 0f)
        {
            FireSingle(world, owner, tankTopLeft, facing.Direction, BulletKind.Laser, audio);
            weapons.LaserCooldownSeconds = GameConstants.TimerShootLaser / 1000f;
            networkShot = CreateShotPacket(tankTopLeft, facing.Direction, bulletType: 0, packetDirection: facing.Direction);
            return true;
        }

        return false;
    }

    public static bool TryFireFromNetworkRequest(
        World world,
        Entity owner,
        ref WeaponState weapons,
        ref PlayerInventory inventory,
        ref TankLifeState life,
        ref TankStatus status,
        in ClientShotPacket request,
        SimulationAudioBuffer? audio)
    {
        if (life.IsDead || status.IsFrozen)
        {
            return false;
        }

        if (request.Type == 3)
        {
            if (weapons.FlareCooldownSeconds > 0f || inventory.Flare <= 0)
            {
                return false;
            }

            ApplyLegacyShot(world, owner, request, audio);
            inventory.Flare--;
            weapons.FlareCooldownSeconds = GameConstants.TimerShootFlare / 1000f;
            return true;
        }

        if (request.Type == 1)
        {
            if (weapons.RocketCooldownSeconds > 0f || inventory.Rocket <= 0)
            {
                return false;
            }

            ApplyLegacyShot(world, owner, request, audio);
            inventory.Rocket--;
            weapons.RocketCooldownSeconds = GameConstants.TimerShootRocket / 1000f;
            weapons.LaserCooldownSeconds = GameConstants.TimerShootRocket / 1000f;
            return true;
        }

        if (weapons.LaserCooldownSeconds > 0f)
        {
            return false;
        }

        ApplyLegacyShot(world, owner, request, audio);
        weapons.LaserCooldownSeconds = GameConstants.TimerShootLaser / 1000f;
        return true;
    }

    public static void ApplyNetworkShot(
        World world,
        Entity shooter,
        in ServerShotPacket packet,
        SimulationAudioBuffer? audio)
    {
        var muzzle = new Vector2(packet.X, packet.Y);
        if (packet.Type == 3)
        {
            var left = (packet.Direction + 4) % TankFacing.DirectionCount;
            var center = packet.Direction % TankFacing.DirectionCount;
            var right = (packet.Direction + TankFacing.DirectionCount - 4) % TankFacing.DirectionCount;
            GameplayEntityFactory.CreateExplosion(world, ExplosionKind.MuzzleFlash, muzzle);
            GameplayEntityFactory.CreateBullet(world, BulletKind.Flare, muzzle, left, shooter);
            GameplayEntityFactory.CreateBullet(world, BulletKind.Flare, muzzle, center, shooter);
            GameplayEntityFactory.CreateBullet(world, BulletKind.Flare, muzzle, right, shooter);
            audio?.Play(SoundId.Flare, muzzle);
            return;
        }

        var kind = packet.Type switch
        {
            1 => BulletKind.Rocket,
            _ => BulletKind.Laser,
        };
        var travelDirection = InputSystem.ToTravelDirection(packet.Direction);
        GameplayEntityFactory.CreateBullet(world, kind, muzzle, travelDirection, shooter);
        GameplayEntityFactory.CreateExplosion(world, ExplosionKind.MuzzleFlash, muzzle);
        audio?.Play(GetSoundForBullet(kind), muzzle);
    }

    private static void ApplyLegacyShot(
        World world,
        Entity owner,
        in ClientShotPacket request,
        SimulationAudioBuffer? audio)
    {
        var muzzle = new Vector2(request.X, request.Y);
        if (request.Type == 3)
        {
            var left = (request.Direction + 4) % TankFacing.DirectionCount;
            var center = request.Direction % TankFacing.DirectionCount;
            var right = (request.Direction + TankFacing.DirectionCount - 4) % TankFacing.DirectionCount;
            GameplayEntityFactory.CreateExplosion(world, ExplosionKind.MuzzleFlash, muzzle);
            GameplayEntityFactory.CreateBullet(world, BulletKind.Flare, muzzle, left, owner);
            GameplayEntityFactory.CreateBullet(world, BulletKind.Flare, muzzle, center, owner);
            GameplayEntityFactory.CreateBullet(world, BulletKind.Flare, muzzle, right, owner);
            audio?.Play(SoundId.Flare, muzzle);
            return;
        }

        var kind = request.Type switch
        {
            1 => BulletKind.Rocket,
            _ => BulletKind.Laser,
        };
        var travelDirection = InputSystem.ToTravelDirection(request.Direction);
        GameplayEntityFactory.CreateBullet(world, kind, muzzle, travelDirection, owner);
        GameplayEntityFactory.CreateExplosion(world, ExplosionKind.MuzzleFlash, muzzle);
        audio?.Play(GetSoundForBullet(kind), muzzle);
    }

    private static ClientShotPacket CreateShotPacket(
        Vector2 tankTopLeft,
        int spriteFacing,
        byte bulletType,
        int packetDirection)
    {
        var travelDirection = InputSystem.ToTravelDirection(spriteFacing);
        var muzzle = WeaponGeometry.GetMuzzleWorldPosition(tankTopLeft, travelDirection);
        return new ClientShotPacket(
            (ushort)Math.Clamp((int)muzzle.X, 0, ushort.MaxValue),
            (ushort)Math.Clamp((int)muzzle.Y, 0, ushort.MaxValue),
            (byte)packetDirection,
            bulletType);
    }

    private static void FireSingle(
        World world,
        Entity owner,
        Vector2 tankTopLeft,
        int spriteFacing,
        BulletKind kind,
        SimulationAudioBuffer? audio)
    {
        var travelDirection = InputSystem.ToTravelDirection(spriteFacing);
        var muzzle = WeaponGeometry.GetMuzzleWorldPosition(tankTopLeft, travelDirection);
        GameplayEntityFactory.CreateBullet(world, kind, muzzle, travelDirection, owner);
        GameplayEntityFactory.CreateExplosion(world, ExplosionKind.MuzzleFlash, muzzle);
        audio?.Play(GetSoundForBullet(kind), muzzle);
    }

    private static void FireFlare(
        World world,
        Entity owner,
        Vector2 tankTopLeft,
        int spriteFacing,
        SimulationAudioBuffer? audio)
    {
        var left = (spriteFacing + 20) % TankFacing.DirectionCount;
        var center = (spriteFacing + 16) % TankFacing.DirectionCount;
        var right = (spriteFacing + 12) % TankFacing.DirectionCount;
        var travelDirection = InputSystem.ToTravelDirection(spriteFacing);
        var muzzle = WeaponGeometry.GetMuzzleWorldPosition(tankTopLeft, travelDirection);

        GameplayEntityFactory.CreateExplosion(world, ExplosionKind.MuzzleFlash, muzzle);
        GameplayEntityFactory.CreateBullet(world, BulletKind.Flare, muzzle, left, owner);
        GameplayEntityFactory.CreateBullet(world, BulletKind.Flare, muzzle, center, owner);
        GameplayEntityFactory.CreateBullet(world, BulletKind.Flare, muzzle, right, owner);
        audio?.Play(SoundId.Flare, muzzle);
    }

    private static SoundId GetSoundForBullet(BulletKind kind) =>
        kind switch
        {
            BulletKind.Rocket => SoundId.Fire,
            BulletKind.Flare => SoundId.Flare,
            _ => SoundId.Laser,
        };
}
