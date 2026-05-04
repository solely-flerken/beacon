using _Game.Components;
using _Game.Core;
using Unity.Entities;
using UnityEngine;

namespace _Game.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct BeaconDeathCleanupSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            EntityCommandBuffer ecb = new(state.WorldUpdateAllocator);
            bool signalFound = false;

            foreach ((BeaconStats _, Entity entity) in SystemAPI.Query<BeaconStats>().WithAll<IsDead>().WithEntityAccess())
            {
                signalFound = true;
                ecb.DestroyEntity(entity);
            }

            if (!signalFound) return;

            Debug.Log("[Beacon] Death logic triggered. Cleaning up...");

            // Destroy all enemies
            foreach ((EnemyTag _, Entity entity) in SystemAPI.Query<EnemyTag>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            // Destroy all projectiles
            foreach ((ProjectileTag _, Entity entity) in SystemAPI.Query<ProjectileTag>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }
            
            // Destroy all flares
            foreach ((FlareTag _, Entity entity) in SystemAPI.Query<FlareTag>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }
            
            // Destroy all exp orbs
            foreach ((ExpOrbTag _, Entity entity) in SystemAPI.Query<ExpOrbTag>().WithEntityAccess())
            {
                ecb.DestroyEntity(entity);
            }

            EventBus.FireBeaconDestroyed();

            ecb.Playback(state.EntityManager);
        }
    }
}