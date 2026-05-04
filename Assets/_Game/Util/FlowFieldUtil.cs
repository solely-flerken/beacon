using Unity.Collections;
using Unity.Mathematics;

namespace _Game.Util
{
    /// <summary>
    /// Stateless helpers for sampling a flow field NativeArray.
    /// Burst-compatible
    /// </summary>
    public static class FlowFieldUtil
    {
        /// <summary>
        /// Returns the flow direction at a world-space position.
        /// </summary>
        public static float2 Sample(in NativeArray<float2> directions, float2 worldPos, float cellSize, int gridWidth, int gridHeight)
        {
            float halfWidth = gridWidth * cellSize * 0.5f;
            float halfHeight = gridHeight * cellSize * 0.5f;

            int x = (int)math.floor((worldPos.x + halfWidth) / cellSize);
            int y = (int)math.floor((worldPos.y + halfHeight) / cellSize);

            x = math.clamp(x, 0, gridWidth - 1);
            y = math.clamp(y, 0, gridHeight - 1);

            return directions[y * gridWidth + x];
        }

        public static int2 WorldToCell(float2 worldPos, float cellSize, int gridWidth, int gridHeight)
        {
            float halfWidth = gridWidth * cellSize * 0.5f;
            float halfHeight = gridHeight * cellSize * 0.5f;

            int x = (int)math.floor((worldPos.x + halfWidth) / cellSize);
            int y = (int)math.floor((worldPos.y + halfHeight) / cellSize);

            return new int2(
                math.clamp(x, 0, gridWidth - 1),
                math.clamp(y, 0, gridHeight - 1)
            );
        }

        public static float2 CellToWorld(int2 cell, float cellSize, int gridWidth, int gridHeight)
        {
            float halfWidth = gridWidth * cellSize * 0.5f;
            float halfHeight = gridHeight * cellSize * 0.5f;

            return new float2(
                cell.x * cellSize - halfWidth + cellSize * 0.5f,
                cell.y * cellSize - halfHeight + cellSize * 0.5f
            );
        }

        public static int GetIndex(int2 cell, int gridWidth)
        {
            return cell.y * gridWidth + cell.x;
        }

        public static bool IsInsideGrid(int2 cell, int gridWidth, int gridHeight)
        {
            return cell.x >= 0 && cell.x < gridWidth &&
                   cell.y >= 0 && cell.y < gridHeight;
        }
    }
}