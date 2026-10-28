using _Game.Shared;
using _Game.Util;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace _Game.DebugUI
{
    public class FlowFieldDebugger : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying) return;

            World world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;

            EntityManager em = world.EntityManager;
            using EntityQuery query = em.CreateEntityQuery(typeof(FlowField));
            if (!query.TryGetSingleton(out FlowField flowField) || !flowField.IsValid) return;

            em.CompleteDependencyBeforeRO<FlowField>();

            float cellSize = flowField.CellSize;
            int gridWidth = flowField.GridWidth;
            int gridHeight = flowField.GridHeight;

            float arenaW = gridWidth * cellSize;
            float arenaH = gridHeight * cellSize;

            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(arenaW, arenaH, 0f));

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    float2 worldPos = FlowFieldUtil.CellToWorld(new int2(x, y), cellSize, gridWidth, gridHeight);
                    Vector3 center = new(worldPos.x, worldPos.y, 0f);
                    int idx = FlowFieldUtil.GetIndex(new int2(x, y), gridWidth);
                    float2 dir = flowField.Directions[idx];

                    if (math.lengthsq(dir) > 0.001f)
                    {
                        Gizmos.color = Color.cyan;
                        Gizmos.DrawRay(center, new Vector3(dir.x, dir.y, 0f) * (cellSize * 0.4f));
                    }
                    else
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawWireCube(center, Vector3.one * (cellSize * 0.2f));
                    }
                }
            }
        }
    }
}
