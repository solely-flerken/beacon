# Modifier & Stat Update System

## Overview

Stats are not computed every frame. Instead, entities carry a dirty flag (`*StatUpdateRequired` as an
`IEnableableComponent`). Stats are only re-resolved when that flag is set. Change detection on the shared modifier
buffers automatically marks all affected entities dirty.

---

## Data Model

### Modifier Buffer Types

| Buffer               | Entity               | Lifetime             | Filtered by    |
|----------------------|----------------------|----------------------|----------------|
| `GlobalStatModifier` | `GlobalModSingleton` | Permanent            | `TagFilter`    |
| `WaveStatModifier`   | `GlobalModSingleton` | Timed (`ExpiryTime`) | `TagFilter`    |
| `StatModifier`       | Individual entity    | Permanent            | — (local only) |

**Blob tier mods** (`TieredStatModifier`) live in `EnemyTypeBlob` asset data and are read-only at runtime — used for
enemy-type and enemy-weapon scaling.

### StatType Enum

```
Weapon: Damage, FireRate, TargetRange, ProjectileSpeed, Pierce, ArmorPenetration, CritChance, CritDamage
Beacon/Enemy: MaxHp, Armor, ArrivalRadius, Speed, ContactDamage
Beam: BeamRotationSpeed, BeamWidth
Enemy reward: Exp
Beacon exp: ExpGain
Projectile ricochet: BounceCount, BounceRange, BounceFalloff, BounceDamageFloor
Chain lightning: ChainCount, ChainRange, ChainFalloff, ChainDamageFloor
Tier bonus: BonusDamageElite, BonusDamageBoss
```

### ModifierType Enum

| Type             | Storage            | Applied as                      |
|------------------|--------------------|---------------------------------|
| `Additive`       | Raw value          | Flat bonus                      |
| `Multiplicative` | Percentage (0–100) | Percentage change of base value |
| `Overwrite`      | Raw value          | Replaces the result (last wins) |

### Classification (Tags)

Every unit and weapon has a `Classification` component holding a `Tags` flag set (`ClassificationComponents.cs`).
An entity carries only the tags that apply to it:

| Group       | Tags                                                 |
|-------------|------------------------------------------------------|
| Kind        | Unit, Weapon                                         |
| Side        | Player, Enemy                                        |
| Unit type   | Beacon, EnemyBasic, EnemyFast, EnemyTank, EnemyRange |
| Tier        | Normal, Elite, Boss                                  |
| Weapon type | WeaponBasic, Beam, Scatter, Mortar                   |

The side is not stored in `Classification`: `Classification.ForUnit` adds it from `UnitFaction` when stats are resolved.
A weapon stores only `Weapon` and its type; `Classification.ForWeapon` adds the owner's side, unit type and tier.

Modifiers on the global/wave singletons carry a `TagFilter`:

| Mask       | The entity must have…                   |
|------------|-----------------------------------------|
| `required` | all of these                            |
| `any`      | at least one of these (ignored if None) |
| `excluded` | none of these                           |

An empty filter applies to every entity.

---

## Resolution Formula

```
(Base + ΣAdditive) * max(0, 1 + ΣMultiplicative)
```

Both accumulators start at 0. All modifier sources are iterated and classified before the final formula is applied.
The minimum effective value is 0. If an `Overwrite` modifier applies, its value is used instead.

### Resolution Stack (in order)

1. `GlobalStatModifier` — filtered by its `TagFilter`
2. `WaveStatModifier` — filtered by its `TagFilter`
3. `StatModifier` — local, on entity (weapons: also the owner's)
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
- On change: diffs both buffers against a copy from the last change and collects the filters of added and removed
  entries.
  Three parallel jobs (`MarkBeaconDirtyJob`, `MarkEnemiesDirtyJob`, `MarkWeaponsDirtyJob`) then enable the
  `*StatUpdateRequired` flag on every entity whose tags match one of those filters.

### BeaconStatUpdateSystem

- Queries entities with `BeaconStatUpdateRequired` **enabled**.
- **HP delta propagation**: `current += newMax - oldMax` so current HP scales with the change.
- Disables `BeaconStatUpdateRequired` after processing.

### EnemyStatUpdateSystem

Two jobs:

| Job              | Filter                                                        | Purpose                                                                                           |
|------------------|---------------------------------------------------------------|---------------------------------------------------------------------------------------------------|
| `SpawnInitJob`   | `WithAll<Uninitialized>`                                      | First-time stat init for newly spawned enemies; sets `current = max`; removes `Uninitialized` tag |
| `UpdateStatsJob` | `WithNone<Uninitialized>` + `EnemyStatUpdateRequired` enabled | Re-resolves all enemy stats; disables dirty flag                                                  |

Both jobs use the blob `TieredStatModifier` from `EnemyTypeBlobRef`.

### WeaponStatUpdateSystem

- Queries entities with `WeaponStatUpdateRequired` **enabled**.
- **Owner tags**: resolves with the weapon's own tags plus the owner's, so enemy-owned weapons scale correctly. The
  result is written back into the weapon's `Classification`.
- **Attack rules**: `AttackRules.TargetFactions` comes from the owner's `Hostility`.
- Also reads owner's `StatModifier` buffer and blob ref for `ResolveWeapon`.
- Disables `WeaponStatUpdateRequired` after processing.

---

## Card Effect Application

Each `CardEffect` carries its own `TagFilter` and a `Lifetime`:

| `Lifetime` | Written to                                      | Lifetime   |
|------------|-------------------------------------------------|------------|
| `Run`      | `GlobalStatModifier` on singleton (`RunScoped`) | Permanent  |
| `Wave`     | `WaveStatModifier` on singleton                 | ExpiryTime |

`Duration <= 0` on a wave-scoped effect means it lasts for the remainder of the current wave (`float.MaxValue`).

The change detection then marks the matching entities dirty.

---

## Files

| File                                      | Role                                               |
|-------------------------------------------|----------------------------------------------------|
| `Components/ModifierComponents.cs`        | All modifier structs, StatType/ModifierType enums  |
| `Components/ClassificationComponents.cs`  | `Tags`, `TagGroups`, `Classification`, `TagFilter` |
| `Util/StatResolver.cs`                    | Pure stat resolution logic                         |
| `Systems/WaveStateSyncSystem.cs`          | MonoBehaviour → ECS bridge                         |
| `Systems/TimedModifierCleanupSystem.cs`   | Expires WaveStatModifiers                          |
| `Systems/WaveModChangeDetectionSystem.cs` | Dirty-flag propagation on buffer changes           |
| `Systems/BeaconStatUpdateSystem.cs`       | Beacon stat recalculation                          |
| `Systems/EnemyStatUpdateSystem.cs`        | Enemy stat init + recalculation                    |
| `Systems/WeaponStatUpdateSystem.cs`       | Weapon stat recalculation                          |
| `Cards/CardData.cs`                       | CardEffect data definition                         |
| `Cards/CardModifierApplier.cs`            | Applies card effects to ECS buffers                |
