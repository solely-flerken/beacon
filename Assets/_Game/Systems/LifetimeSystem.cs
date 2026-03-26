using _Game.Components;
using Unity.Entities;

namespace _Game.Systems
{
    public partial struct LifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new(state.WorldUpdateAllocator);
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach ((RefRW<Lifetime> lifetime, Entity entity) in SystemAPI.Query<RefRW<Lifetime>>().WithEntityAccess())
            {
                lifetime.ValueRW.Remaining -= deltaTime;
                
                if (lifetime.ValueRO.Remaining > 0) continue;
                    
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}