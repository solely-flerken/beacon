using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Util
{
    /// <summary>
    /// Spatial grid over the arena, rebuilt each frame by SpatialGridSystem.
    /// Units are stored sorted by cell, each with its position and entity (<see cref="Positions"/> / <see cref="Entities"/>).
    /// Cells are numbered row-major (cell = y * Width + x) and units are stored in cell order, so all rows form one continuous sequence.
    /// </summary>
    public struct SpatialGrid
    {
        /// <summary>
        /// Index of the first unit of each cell, length CellCount + 1. Cell c holds indices [CellFirstIndex[c], CellFirstIndex[c + 1]),
        /// since the next cell starts where this one ends. The extra last entry holds <see cref="Count"/>.
        /// </summary>
        [ReadOnly] public NativeArray<int> CellFirstIndex;

        /// <summary>
        /// Unit positions, sorted by cell.
        /// </summary>
        [ReadOnly] public NativeArray<float2> Positions;

        /// <summary>
        /// Unit entities, sorted by cell.
        /// </summary>
        [ReadOnly] public NativeArray<Entity> Entities;

        public float2 Origin;
        public float CellSize;
        public float InvCellSize;
        public int Width;
        public int Height;
        public int Count;

        public bool IsCreated => CellFirstIndex.IsCreated;

        public int2 WorldToCell(float2 worldPos)
        {
            int2 cell = (int2)math.floor((worldPos - Origin) * InvCellSize);
            return math.clamp(cell, int2.zero, new int2(Width - 1, Height - 1));
        }

        /// <summary>
        /// Finds the cells a world-space box overlaps, clamped into the grid.
        /// </summary>
        /// <param name="worldMin">Bottom-left corner of the box in world space.</param>
        /// <param name="worldMax">Top-right corner of the box in world space.</param>
        /// <param name="minCell">Bottom-left cell, inclusive.</param>
        /// <param name="maxCell">Top-right cell, inclusive.</param>
        public void GetCellBounds(float2 worldMin, float2 worldMax, out int2 minCell, out int2 maxCell)
        {
            ClampBounds((int2)math.floor((worldMin - Origin) * InvCellSize),
                (int2)math.floor((worldMax - Origin) * InvCellSize), out minCell, out maxCell);
        }

        /// <summary>
        /// All units in the cells within <paramref name="cellRadius"/> of the cell containing <paramref name="worldPos"/>.
        /// </summary>
        public GridQuery QueryNeighbors(float2 worldPos, int cellRadius)
        {
            int2 cell = (int2)math.floor((worldPos - Origin) * InvCellSize);
            ClampBounds(cell - cellRadius, cell + cellRadius, out int2 minCell, out int2 maxCell);
            return Query(minCell, maxCell);
        }

        /// <summary>
        /// All units in the cells overlapping the world-space box [<paramref name="worldMin"/>, <paramref name="worldMax"/>].
        /// </summary>
        public GridQuery Query(float2 worldMin, float2 worldMax)
        {
            GetCellBounds(worldMin, worldMax, out int2 minCell, out int2 maxCell);
            return Query(minCell, maxCell);
        }

        /// <summary>
        /// All units in the cell box [<paramref name="minCell"/>, <paramref name="maxCell"/>].
        /// </summary>
        public GridQuery Query(int2 minCell, int2 maxCell)
        {
            return new GridQuery(CellFirstIndex, Width, minCell, maxCell);
        }

        /// <summary>
        /// Number of units in the cell box [<paramref name="minCell"/>, <paramref name="maxCell"/>].
        /// </summary>
        public int CountInBox(int2 minCell, int2 maxCell)
        {
            int count = 0;
            for (int rowCell = minCell.y * Width; rowCell <= maxCell.y * Width; rowCell += Width)
            {
                count += CellFirstIndex[rowCell + maxCell.x + 1] - CellFirstIndex[rowCell + minCell.x];
            }

            return count;
        }

        /// <summary>
        /// Index of the <paramref name="n"/>-th unit in the cell box [<paramref name="minCell"/>, <paramref name="maxCell"/>], in the order
        /// <see cref="Query(int2, int2)"/> yields them, or -1 if the box holds fewer units.
        /// </summary>
        public int NthInBox(int2 minCell, int2 maxCell, int n)
        {
            for (int rowCell = minCell.y * Width; rowCell <= maxCell.y * Width; rowCell += Width)
            {
                int start = CellFirstIndex[rowCell + minCell.x];
                int rowCount = CellFirstIndex[rowCell + maxCell.x + 1] - start;
                if (n < rowCount) return start + n;
                n -= rowCount;
            }

            return -1;
        }

        /// <summary>
        /// All units in the cells exactly <paramref name="radius"/> cells from <paramref name="centerCell"/>, clipped to the grid.
        /// Radius 0 is the center cell alone.
        /// </summary>
        public RingQuery QueryRing(int2 centerCell, int radius)
        {
            int2 min = centerCell - radius;
            int2 max = centerCell + radius;

            if (radius == 0)
            {
                return new RingQuery(ClippedQuery(min, max), GridQuery.Range(0), GridQuery.Range(0), GridQuery.Range(0));
            }

            return new RingQuery(
                ClippedQuery(min, new int2(max.x, min.y)),
                ClippedQuery(new int2(min.x, min.y + 1), new int2(min.x, max.y - 1)),
                ClippedQuery(new int2(max.x, min.y + 1), new int2(max.x, max.y - 1)),
                ClippedQuery(new int2(min.x, max.y), max));
        }

        /// <summary>
        /// World distance from <paramref name="worldPos"/>, clamped into the grid, to the nearest edge of its cell.
        /// </summary>
        public float DistanceToCellEdge(float2 worldPos)
        {
            float2 clamped = math.clamp(worldPos, Origin, Origin + new float2(Width, Height) * CellSize);
            float2 inCell = (clamped - Origin) * InvCellSize - WorldToCell(clamped);
            return math.cmin(math.min(inCell, 1f - inCell)) * CellSize;
        }

        /// <summary>
        /// All units. Cheaper than a cell query when there are only a few units.
        /// </summary>
        public GridQuery All()
        {
            return GridQuery.Range(Count);
        }

        private void ClampBounds(int2 min, int2 max, out int2 minCell, out int2 maxCell)
        {
            int2 upper = new(Width - 1, Height - 1);
            minCell = math.clamp(min, int2.zero, upper);
            maxCell = math.clamp(max, int2.zero, upper);
        }

        /// <summary>
        /// Like <see cref="Query(int2, int2)"/>, but the box is cut to the grid and may end up empty.
        /// </summary>
        private GridQuery ClippedQuery(int2 min, int2 max)
        {
            min = math.max(min, int2.zero);
            max = math.min(max, new int2(Width - 1, Height - 1));
            return math.any(min > max) ? GridQuery.Range(0) : Query(min, max);
        }
    }

    /// <summary>
    /// Iterates the units in a ring of cells, created by <see cref="SpatialGrid.QueryRing"/>.
    /// The ring is read as four boxes: the bottom row, the left and right columns between them, and the top row.
    /// <code>
    /// radius 2:   T T T T T
    ///             L . . . R
    ///             L . c . R
    ///             L . . . R
    ///             B B B B B
    /// </code>
    /// </summary>
    public struct RingQuery
    {
        private GridQuery _bottom;
        private GridQuery _left;
        private GridQuery _right;
        private GridQuery _top;
        private int _part;

        internal RingQuery(GridQuery bottom, GridQuery left, GridQuery right, GridQuery top)
        {
            _bottom = bottom;
            _left = left;
            _right = right;
            _top = top;
            _part = 0;
            Current = -1;
        }

        public int Current { get; private set; }

        public RingQuery GetEnumerator() => this;

        public bool MoveNext()
        {
            while (true)
            {
                switch (_part)
                {
                    case 0: if (_bottom.MoveNext()) { Current = _bottom.Current; return true; } break;
                    case 1: if (_left.MoveNext()) { Current = _left.Current; return true; } break;
                    case 2: if (_right.MoveNext()) { Current = _right.Current; return true; } break;
                    case 3: if (_top.MoveNext()) { Current = _top.Current; return true; } break;
                    default: return false;
                }

                _part++;
            }
        }
    }

    /// <summary>
    /// Iterates the units in a box of cells, created by <see cref="SpatialGrid.Query(int2, int2)"/>.
    /// Yields an index per unit; use it to look up the unit in <see cref="SpatialGrid.Positions"/> and <see cref="SpatialGrid.Entities"/>.
    /// <para>
    /// The box covers part of each row, so it reads one index range per row.
    /// Example with a 3x3 grid:
    /// <code>
    /// input:    box from cell (1,0) bottom-left to cell (2,1) top-right
    ///
    /// cells:    y=2 | 6  7  8 |
    ///           y=1 | 3 [4  5]|
    ///           y=0 | 0 [1  2]|
    ///
    /// storage:  0 [1  2] 3 [4  5] 6  7  8
    ///
    /// enemies:  y=2 | -   G   -  |
    ///           y=1 | E  [-   F ]|
    ///           y=0 | A  [BC  D ]|
    ///
    /// index:    0  1  2  3  4  5  6
    /// units:    A  B  C  D  E  F  G
    /// cell:     0  1  1  2  3  5  7
    /// read:        ^  ^  ^     ^
    /// 
    /// output:   1, 2, 3, 5   (B, C, D, F)
    /// </code>
    /// </para>
    /// </summary>
    public struct GridQuery
    {
        private NativeArray<int> _cellFirstIndex;
        private readonly int _width;
        private readonly int _boxWidth;
        private int _lastRowCell;
        private int _rowCell;
        private int _end;

        internal GridQuery(NativeArray<int> cellFirstIndex, int width, int2 minCell, int2 maxCell)
        {
            _cellFirstIndex = cellFirstIndex;
            _width = width;
            _boxWidth = maxCell.x - minCell.x + 1;
            _lastRowCell = maxCell.y * width + minCell.x;

            _rowCell = minCell.y * width + minCell.x - width;
            _end = 0;
            Current = -1;
        }

        /// <summary>
        /// A single preloaded range [0, <paramref name="count"/>) with no rows left to load.
        /// </summary>
        internal static GridQuery Range(int count)
        {
            return new GridQuery { _rowCell = 0, _lastRowCell = -1, _end = count, Current = -1 };
        }

        public int Current { get; private set; }

        public GridQuery GetEnumerator() => this;

        public bool MoveNext()
        {
            Current++;

            while (Current >= _end)
            {
                _rowCell += _width;
                if (_rowCell > _lastRowCell) return false;

                Current = _cellFirstIndex[_rowCell];
                _end = _cellFirstIndex[_rowCell + _boxWidth];
            }

            return true;
        }
    }
}
