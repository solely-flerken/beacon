using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace _Game.Shared
{
    /// <summary>
    /// Positions and entities of one unit group, copied once per frame.
    ///
    /// Do NOT index the arrays with your own query's EntityIndexInQuery,
    /// since the iteration order isn't guaranteed to match UnitDataSystem's.
    /// </summary>
    public struct UnitSnapshot
    {
        /// <summary>
        /// World positions of all active units, indexed 0...Count-1.
        /// </summary>
        public NativeArray<float2> Positions;

        /// <summary>
        /// ECS entities matching Positions by index - Positions[i] and Entities[i] are the same unit.
        /// </summary>
        public NativeArray<Entity> Entities;

        /// <summary>
        /// Number of active units this frame.
        /// </summary>
        public int Count;

        public bool IsValid => Positions.IsCreated && Count > 0;
    }

    public struct UnitData : IComponentData
    {
        public UnitSnapshot Enemies;
        public UnitSnapshot Allies;
    }
}
