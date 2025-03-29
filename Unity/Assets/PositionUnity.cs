using Friflo.Engine.ECS;
using Friflo.Json.Fliox;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Assets
{
    [ComponentKey("pos")]
    [StructLayout(LayoutKind.Explicit)]
    [ComponentSymbol("P", "0, 170, 0")]
    public struct PositionUnity : IComponent, IEquatable<PositionUnity>
    {
        [Ignore]
        [FieldOffset(0)] public Vector3 value;  // 12
                                                //
        [FieldOffset(0)] public float x;      // (4)
        [FieldOffset(4)] public float y;      // (4)
        [FieldOffset(8)] public float z;      // (4)

        public readonly override string ToString() => $"{x}, {y}, {z}";

        public PositionUnity(float x, float y, float z)
        {
            this.value = default;
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public bool Equals(PositionUnity other) => value == other.value;
        public static bool operator ==(in PositionUnity p1, in PositionUnity p2) => p1.value == p2.value;
        public static bool operator !=(in PositionUnity p1, in PositionUnity p2) => p1.value != p2.value;

        [ExcludeFromCodeCoverage] public override int GetHashCode() => throw new NotImplementedException("to avoid boxing");
        [ExcludeFromCodeCoverage] public override bool Equals(object obj) => throw new NotImplementedException("to avoid boxing");
    }
}