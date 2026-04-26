# Adding a new enemy type

This guide walks through every step needed to add a new enemy type (e.g. `Ranged`) to the game.

---

## 1. Register the type and its tag - `EnemyComponents.cs`

a) Add the new value to the `EnemyType` enum:
```csharp
public enum EnemyType : byte
{
    Basic,
    Fast,
    Tank,
    Ranged,   // <-- add here
}
```

b) Add a marker tag struct (used for ECS archetype filtering):
```csharp
public struct RangedEnemyTag : IComponentData { }
```

c) Add a case to `EnemyTypeTagMap.Get()`:
```csharp
EnemyType.Ranged => ComponentType.ReadOnly<RangedEnemyTag>(),
```

---

## 2. Update the Baker - `EnemyAuthoring.cs`

Add a case to the `switch (authoring.typeId)` block:
```csharp
case EnemyType.Ranged:
    AddComponent<RangedEnemyTag>(entity);
    break;
```

---

## 3. Create the prefab

1. Duplicate an existing enemy prefab (e.g. `Enemy.prefab` → `EnemyRanged.prefab`).
2. On the `EnemyAuthoring` component set:
   - `typeId` → `Ranged`
   - `speed`, `maxHealth`, `contactDamage`, `exp` to the desired base values
3. Optionally add `TieredModifierEntry` rows for stat overrides based on different tiers.

---

## 4. Add the prefab to the scene catalog

Search for the GameObject with `EnemyTypeCatalogAuthoring` in the main scene.

Add the new prefab (`EnemyRanged`) to its `prefabs` list. The catalog is looked up by `EnemyType` at runtime.

---

## 5. Configure spawning - WaveManager inspector

Select the `WaveManager` GameObject in the scene. In the **Enemy Type Definitions** list:

1. Click `+`, a new entry is created automatically with the next unused type.
2. Add tier rows (`+` inside the definition) for Normal, Elite, etc.
3. For each tier set:
   - **Cost** - budget units this enemy costs to spawn (min 1)
   - **Weight** - relative spawn frequency (higher = more common)
   - **Start Wave** - the first wave this tier can appear on (min 1)

---

## How the pipeline fits together

```
EnemyType enum
    └─ EnemyTypeTagMap           (ECS archetype filtering, e.g. card effects)
    └─ EnemyAuthoring.Baker      (bakes tag onto prefab entity)
    └─ EnemyTypeCatalogAuthoring (scene catalog, looked up by EnemySpawner)
    └─ WaveManager definitions   (budget cost / weight / start wave - per tier)
```

All other systems (movement, stat resolution, damage, exp drops, etc) work automatically via the shared `EnemyTag` and
component data. No changes needed there.
