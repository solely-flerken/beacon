# Adding a new enemy type

This guide walks through every step needed to add a new enemy type (e.g. `Sniper`) to the game.

---

## 1. Register the type - `EnemyComponents.cs`

Add the new value at the end of the `EnemyType` enum:

```csharp
public enum EnemyType : byte
{
    Basic,
    Fast,
    Tank,
    Range,
    Sniper,   // <-- add here
}
```

---

## 2. Add its tag - `ClassificationComponents.cs`

Modifiers (cards, wave modifiers, global modifiers) pick enemies by their tags.

a) Add a unit type tag to `Tags`:

```csharp
EnemySniper = 1 << 9,
```

b) Add it to `TagGroups.UnitType`.

c) Add a case to `ToTag(this EnemyType type)`:

```csharp
EnemyType.Sniper => Tags.EnemySniper,
```

---

## 3. Create the prefab

1. Duplicate an existing enemy prefab (e.g. `Enemy.prefab` → `EnemySniper.prefab`).
2. On the `EnemyAuthoring` component set:
    - `type` → `Sniper`
    - `speed`, `maxHealth`, `contactDamage`, `exp`, etc. to the desired base values
3. Optionally add `TieredModifierEntry` rows for stat overrides based on different tiers.

---

## 4. Add the prefab to the scene catalog

Search for the GameObject with `EnemyTypeCatalogAuthoring` in the main scene.

Add the new prefab (`EnemySniper`) to its `prefabs` list. The catalog is looked up by `EnemyType` at runtime.

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
    └─ EnemyType.ToTag()         (type tag in Classification, for modifier filters)
    └─ EnemyAuthoring.Baker      (bakes Classification, UnitFaction, Hostility onto the prefab entity)
    └─ EnemyTypeCatalogAuthoring (scene catalog, looked up by EnemySpawnSystem)
    └─ WaveManager definitions   (budget cost / weight / start wave - per tier)
```

All other systems (movement, stat resolution, damage, exp drops, etc) work automatically via the shared `EnemyTag` and
component data. No changes needed there.
