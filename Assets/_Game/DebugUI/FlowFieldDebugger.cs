using _Game.Config;
using _Game.Shared;
using _Game.Util;
using Unity.Mathematics;
using UnityEngine;

namespace _Game.DebugUI
{
    public class FlowFieldDebugger : MonoBehaviour
    {
        private void OnDrawGizmos()
        {
            if (!Application.isPlaying || !FlowFieldShared.IsValid) return;

            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(ArenaConfig.Width, ArenaConfig.Height, 0f));

            float cellSize = FlowFieldShared.CellSize;
            int gridWidth = FlowFieldShared.GridWidth;
            int gridHeight = FlowFieldShared.GridHeight;

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    float2 worldPos = FlowFieldUtil.CellToWorld(new int2(x, y), cellSize, gridWidth, gridHeight);
                    Vector3 center = new(worldPos.x, worldPos.y, 0f);
                    int idx = FlowFieldUtil.GetIndex(new int2(x, y), gridWidth);
                    float2 dir = FlowFieldShared.Directions[idx];

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