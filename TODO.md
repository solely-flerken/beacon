## TODO

### Gameplay

- [x] **FlowField: edge-cell stall** — enemies that reach the same cell as the player but land on the far edge stop moving. The flow field gives them zero or conflicting direction since they're already "at" the attractor cell. Need a fallback: if an enemy is in the attractor's cell, steer directly toward the attractor world position instead of sampling the flow field.

- [x] **Enemy movement smoothing** — enemies currently snap to the exact flow field direction each frame, causing jitter when the sampled direction changes. Interpolate or smooth the steering direction over a few frames (e.g. lerp toward the new direction with a small smoothing factor) so movement feels less mechanical.

- [x] **Spawned entities flash at the origin for one frame.** Projectiles render at the world origin for their first frame, then jump to the player. The cause is that `ShootProjectileSystem` and `ShootScatterSystem` only set `LocalTransform` through the `EndSimulationEntityCommandBufferSystem` ECB. That ECB plays back after `TransformSystemGroup` has already run that frame, so the new entity keeps the prefab's baked `LocalToWorld` (the origin) when it is rendered. Its `LocalToWorld` is only corrected on the next frame. The same pattern appears in `MortarLaunchSystem` (shell and shadow), `MortarSystem` (explosion) and `ExpOrbSpawnSystem`.

- [x] *(Optional)* **`ExpGain` beacon stat** — a receiver-side "+X% exp gained" multiplier, separate from the enemy `StatType.Exp` drop value. Add `StatType.ExpGain` (append to the enum), resolve it in `BeaconStatUpdateSystem`, and apply it in `ExpManager.AddExp` so every exp source is scaled. Revisit when a non-enemy exp source is added (wave-clear bonus, chests, …), or if scaling drops makes orbs saturate and fly in too early (pickup-side scaling leaves orb pacing untouched).

### Tech debt

- [x] **Migrate sprites off companion `SpriteRenderer`** — `EnemyTierColorSystem` and `ExplosionSystem` use `EntityManager.GetComponentObject<SpriteRenderer>`, deprecated since Entities 6.6 (CS0618) and slated for removal. Entities Graphics still bakes `SpriteRenderer` as a companion object with no unmanaged replacement, and `UnityObjectRef<T>` would only reference the prefab's renderer, not the per-instance companion. Move enemy/explosion visuals to Entities Graphics quads (mesh + sprite material) and drive tint/alpha via per-entity material properties (e.g. `URPMaterialPropertyBaseColor`), which also makes them Burst-compatible.
