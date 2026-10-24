## TODO

### Gameplay

- [x] **FlowField: edge-cell stall** — enemies that reach the same cell as the player but land on the far edge stop moving. The flow field gives them zero or conflicting direction since they're already "at" the attractor cell. Need a fallback: if an enemy is in the attractor's cell, steer directly toward the attractor world position instead of sampling the flow field.

- [x] **Enemy movement smoothing** — enemies currently snap to the exact flow field direction each frame, causing jitter when the sampled direction changes. Interpolate or smooth the steering direction over a few frames (e.g. lerp toward the new direction with a small smoothing factor) so movement feels less mechanical.

### Tech debt

- [ ] **Migrate sprites off companion `SpriteRenderer`** — `EnemyTierColorSystem` and `ExplosionSystem` use `EntityManager.GetComponentObject<SpriteRenderer>`, deprecated since Entities 6.6 (CS0618) and slated for removal. Entities Graphics still bakes `SpriteRenderer` as a companion object with no unmanaged replacement, and `UnityObjectRef<T>` would only reference the prefab's renderer, not the per-instance companion. Move enemy/explosion visuals to Entities Graphics quads (mesh + sprite material) and drive tint/alpha via per-entity material properties (e.g. `URPMaterialPropertyBaseColor`), which also makes them Burst-compatible.
