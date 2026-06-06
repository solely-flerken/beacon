using System;
using _Game.Components;

namespace _Game.Util
{
    public struct Modifier<TStatType> where TStatType : struct, Enum
    {
        public TStatType Stat;
        public ModifierType Type;
        public float Value;
    }
}