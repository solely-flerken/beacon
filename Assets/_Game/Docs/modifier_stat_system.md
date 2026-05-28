# Modifier & Stat Update System

## Overview

Stats are not computed every frame. Instead, entities carry a dirty flag (`*StatUpdateRequired` as an
`IEnableableComponent`). Stats are only re-resolved when that flag is set. Change detection on the shared modifier
buffers automatically marks all affected entities dirty.

---

## Data Model

### Modifier Buffer Types

| Buffer               | Entity               | Lifetime             | Filtered by         |
|----------------------|----------------------|----------------------|---------------------|
| `GlobalStatModifier` | `GlobalModSingleton` | Permanent            | Classification mask |
| `WaveStatModifier`   | `GlobalModSingleton` | Timed (`ExpiryTime`) | Classification mask |
| `StatModifier`       | Individual entity    | Permanent            | — (local only)      |

**Blob tier mods** (`TieredStatModifier`) live in `EnemyTypeBlob` asset data and are read-only at runtime — used for
enemy-type and enemy-weapon scaling.

### StatType Enum

```
Weapon: Damage, FireRate, TargetRange, ProjectileSpeed, Pierce
Beacon/Enemy: MaxHp, ArrivalRadius, Speed, ContactDamage
```

### ModifierType Enum

| Type             | Storage            | Applied as                      |
|------------------|--------------------|---------------------------------|
| `Additive`       | Raw value          | Flat bonus                      |
| `Multiplicative` | Percentage (0–100) | Percentage change of base value |

### Classification (Bitmask)

Every entity has a `Classification` component. Modifiers on the global/wave singletons carry their own `Classification`
mask. An entity only receives the modifier if all non-zero mask fields match at least one bit. 

| Field         | Options                  |
|---------------|--------------------------|
| `TargetMask`  | Character, Weapon        |
| `FactionMask` | Player, Enemy            |
| `RoleMask`    | Basic, Fast, Tank, Range |
| `TierMask`    | Normal, Elite, Boss      |

A zero field ("none"), applies to every entity.

---

## Resolution Formula

```
(Base + ΣAdditive) * max(0, 1 + ΣMultiplicative)
```

Both accumulators start at 0. All modifier sources are iterated and classified before the final formula is applied. The
minimum effective value is 0.

### Resolution Stack (in order)

1. `GlobalStatModifier` — filtered by Classification
2. `WaveStatModifier` — filtered by Classification
3. `StatModifier` — local, on entity
4. Blob `TieredStatModifier` — Normal-tier entries apply universally; non-Normal only when the entity's tier matches
   exactly

---

## System Pipeline

```
WaveStateSyncSystem                    (syncs MonoBehaviour → ECS singleton)
  └─ TimedModifierCleanupSystem         (removes expired WaveStatModifiers)
       └─ WaveModChangeDetectionSystem   (detects buffer changes → marks entities dirty)
            ├─ BeaconStatUpdateSystem
            ├─ EnemyStatUpdateSystem
            └─ WeaponStatUpdateSystem
```

---

## System Details

### WaveStateSyncSystem

Bridges the managed `WaveManager` MonoBehaviour and the unmanaged ECS world. Each frame writes `IsWaveActive` and
`WaveElapsed` into the `WaveStateSingleton`.

### TimedModifierCleanupSystem

- Runs only when the wave is active.
- Reads `WaveStateSingleton.WaveElapsed`.
- Scans `WaveStatModifier` buffer; removes any entry where `ExpiryTime <= WaveElapsed` (infinite entries use
  `float.MaxValue`).
- Removing entries marks the buffer as changed, which triggers the detection system downstream.

### WaveModChangeDetectionSystem

- Uses ECS **change version filters** on `WaveStatModifier` and `GlobalStatModifier`.
- Only fires if either buffer was written to since the last frame.
- On change: schedules three parallel jobs (`MarkBeaconDirtyJob`, `MarkEnemiesDirtyJob`, `MarkWeaponsDirtyJob`) that
  enable the `*StatUpdateRequired` flag on every entity of that type.

### BeaconStatUpdateSystem

- Queries entities with `BeaconStatUpdateRequired` **enabled**.
- **HP delta propagation**: `current += newMax - oldMax` so current HP scales with the change.
- Disables `BeaconStatUpdateRequired` after processing.

### EnemyStatUpdateSystem

Two jobs:

| Job              | Filter                                                        | Purpose                                                                                           |
|------------------|---------------------------------------------------------------|---------------------------------------------------------------------------------------------------|
| `SpawnInitJob`   | `WithAll<Uninitialized>`                                      | First-time stat init for newly spawned enemies; sets `current = max`; removes `Uninitialized` tag |
| `UpdateStatsJob` | `WithNone<Uninitialized>` + `EnemyStatUpdateRequired` enabled | Re-resolves Speed, MaxHp, ContactDamage; disables dirty flag                                      |

Both jobs use the blob `TieredStatModifier` from `EnemyTypeBlobRef`.

### WeaponStatUpdateSystem

- Queries entities with `WeaponStatUpdateRequired` **enabled**.
- **Identity sync**: inherits `Faction`, `Role`, `Tier` from owner's `Classification` (enemy-owned weapons scale
  correctly).
- Also reads owner's `StatModifier` buffer and blob ref for `ResolveWeapon`.
- Disables `WeaponStatUpdateRequired` after processing.

---

## Card Effect Application

| `CardTargetType` | Written to                                                         | Lifetime   |
|------------------|--------------------------------------------------------------------|------------|
| `Beacon`         | Beacon entity's `StatModifier` buffer                              | Permanent  |
| `BeaconWeapons`  | Beacon's entity's `StatModifier` buffer                            | Permanent  |
| `WeaponsOfType`  | Matching weapon entity's `StatModifier` buffer                     | Permanent  |
| `EnemiesOfType`  | `GlobalStatModifier` on singleton (Enemy faction, RoleMask filter) | Permanent  |
| `WaveEnemies`    | `WaveStatModifier` on singleton                                    | ExpiryTime |
| `WaveWeapons`    | `WaveStatModifier` on singleton                                    | ExpiryTime |

`Duration <= 0` on a wave-scoped effect means it lasts for the remainder of the current wave (`float.MaxValue`).

After writing, the applier immediately sets the relevant `*StatUpdateRequired` dirty flag so the stat systems pick up
the change this frame.

---

## Files

| File                                      | Role                                                              |
|-------------------------------------------|-------------------------------------------------------------------|
| `Components/ModifierComponents.cs`        | All modifier structs, StatType/ModifierType enums, Classification |
| `Util/StatResolver.cs`                    | Pure stat resolution logic                                        |
| `Systems/WaveStateSyncSystem.cs`          | MonoBehaviour → ECS bridge                                        |
| `Systems/TimedModifierCleanupSystem.cs`   | Expires WaveStatModifiers                                         |
| `Systems/WaveModChangeDetectionSystem.cs` | Dirty-flag propagation on buffer changes                          |
| `Systems/BeaconStatUpdateSystem.cs`       | Beacon stat recalculation                                         |
| `Systems/EnemyStatUpdateSystem.cs`        | Enemy stat init + recalculation                                   |
| `Systems/WeaponStatUpdateSystem.cs`       | Weapon stat recalculation                                         |
| `Cards/CardData.cs`                       | CardEffect data definition                                        |
| `Cards/CardModifierApplier.cs`            | Applies card effects to ECS buffers                               |
