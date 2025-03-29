// Copyright (c) Ullrich Praetz - https://github.com/friflo. All rights reserved.
// See LICENSE file in the project root for full license information.

using Friflo.Engine.ECS;
using System;
using System.Numerics;
// Note: Avoid game engine dependencies to enable using code in various engines. E.g.
//  using UnityEngine;
//  using Godot;
//  using Microsoft.Xna.Framework;

// ReSharper disable once CheckNamespace
namespace Example.Systems
{

    public enum Shape
    {
        Plane,
        Cube,
        Ring,
        Rings
    }

    public struct Start : IComponent
    {
        public Vector3 value;
    }

    public struct Target : IComponent
    {
        public Vector3 value;
    }

    public class Drones
    {
        private readonly EntityStore store;
        public readonly int maxDroneCount = 64 * 1024;
        private float elapsed;
        private float duration = 1000; // ms

        private readonly ArchetypeQuery<Start, Position> startPositionQuery;
        private readonly ArchetypeQuery<Target> targetQuery;
        private readonly ArchetypeQuery<Transform, Position, Start, Target> transPosQuery;
        public readonly ArchetypeQuery<Transform> transQuery;
        private readonly ArchetypeQuery allQuery;
        private readonly CommandBuffer commandBuffer;


        internal Drones()
        {
            store = new EntityStore(PidType.UsePidAsId);
            startPositionQuery = store.Query<Start, Position>();
            targetQuery = store.Query<Target>();
            transPosQuery = store.Query<Transform, Position, Start, Target>();
            transQuery = store.Query<Transform>();
            allQuery = store.Query().WithDisabled();
            commandBuffer = store.GetCommandBuffer();
            commandBuffer.ReuseBuffer = true;
        }

        public void Initialize()
        {
            for (int n = 0; n < maxDroneCount; n++)
            {
                store.CreateEntity(new Position(), new Start(), new Target(), new Transform(), Tags.Get<Disabled>());
            }
        }

        public void SetEntityCount(int count)
        {
            int i = 0;
            foreach (var entity in allQuery.Entities)
            {
                if (i++ < count)
                {
                    commandBuffer.RemoveTag<Disabled>(entity.Id);
                }
                else
                {
                    commandBuffer.AddTag<Disabled>(entity.Id);
                    commandBuffer.AddComponent<Position>(entity.Id);
                }
            }
            commandBuffer.Playback();
        }

        internal void SetTargetPlane(int duration, float distance)
        {
            SetStart(duration);
            int rowCount = (int)Math.Sqrt(targetQuery.Count);
            var rowCountF = (float)rowCount;
            float offset = distance * rowCountF / 2;
            int x = 0;
            foreach (var (targets, _) in targetQuery.Chunks)
            {
                var targetSpan = targets.Span;
                for (int n = 0; n < targets.Length; n++)
                {
                    ref var target = ref targetSpan[n];
                    target.value.X = distance * x - offset;
                    target.value.Y = -distance;
                    target.value.Z = distance * (n / rowCount) - offset;
                    x = (x + 1) % rowCount;
                }
            }
        }

        internal void SetTargetCube(float duration, float distance)
        {
            SetStart(duration);
            int edgeCount = (int)Math.Pow(targetQuery.Count, (1.0 / 3.0));
            int edgeCount2 = edgeCount * edgeCount;
            var offset = distance * edgeCount / 2;
            int x = 0;
            foreach (var (targets, _) in targetQuery.Chunks)
            {
                var targetSpan = targets.Span;
                for (int n = 0; n < targets.Length; n++)
                {
                    ref var target = ref targetSpan[n];
                    target.value.X = distance * x - offset;
                    target.value.Y = distance * ((n / edgeCount2) % edgeCount2) - distance - offset;
                    target.value.Z = distance * ((n / edgeCount) % edgeCount) - offset;
                    x = (x + 1) % edgeCount;
                }
            }
        }

        internal void SetTargetRings(float duration, int radius, float distance, int count)
        {
            SetStart(duration);
            var entityCount = targetQuery.Count;
            int ringCount = Math.Max(1, entityCount / count);
            float ringCountF = ringCount;
            foreach (var (targets, _) in targetQuery.Chunks)
            {
                var targetSpan = targets.Span;
                for (int n = 0; n < targets.Length; n++)
                {
                    var pos = (n % ringCount) / ringCountF * Math.PI * 2;
                    var rot = Matrix4x4.CreateRotationY((float)pos);
                    var y = distance * (n / ringCount);
                    var v = new Vector3(radius, y, 0);
                    ref var target = ref targetSpan[n];
                    target.value = Vector3.Transform(v, rot);
                }
            }
        }

        private void SetStart(float duration)
        {
            elapsed = 0;
            this.duration = duration;
            foreach (var (starts, positions, _) in startPositionQuery.Chunks)
            {
                var startSpan = starts.Span;
                var positionSpan = positions.Span;
                for (int n = 0; n < positions.Length; n++)
                {
                    startSpan[n].value = positionSpan[n].value;
                }
            }
        }

        internal void UpdateTransforms(float deltaTime, Matrix4x4 world)
        {
            elapsed += deltaTime;
            var complete = Math.Min(elapsed / duration, 1);


            //transPosQuery.ForEachEntity((ref Transform transform, ref Position position, ref Start start, ref Target target, Entity e) =>
            ////transPosQuery.Chunks.ForEachComponentResult((ref Transform transform, ref Position position, ref Start start, ref Target target) =>
            //{
            //    //ref var pos = ref positionSpan[n];
            //    var pos = Vector3.Lerp(start.value, target.value, complete);
            //    position.value = pos;
            //    transform.value = world + Matrix4x4.CreateTranslation(pos);
            //    //transform.value = Matrix4x4.CreateTranslation(pos);
            //});


            foreach (var (transforms, positions, starts, targets, _) in transPosQuery.Chunks)
            {
                var transformSpan = transforms.Span;
                var positionSpan = positions.Span;
                var startSpan = starts.Span;
                var targetSpan = targets.Span;
                for (int n = 0; n < positions.Length; n++)
                {
                    //ref var pos = ref positionSpan[n];
                    var pos = Vector3.Lerp(startSpan[n].value, targetSpan[n].value, complete);
                    positionSpan[n].value = pos;
                    transformSpan[n].value = world + Matrix4x4.CreateTranslation(pos);
                }
            }

            //ParallelQueryJob(transPosQuery, world, complete);
        }

        public static void ParallelQueryJob(ArchetypeQuery<Transform, Position, Start, Target> query, Matrix4x4 world, float complete)
        {
            using var runner = new ParallelJobRunner(Environment.ProcessorCount);

            var queryJob = query.ForEach((transforms, positions, starts, targets, _) =>
            {
                var transformSpan = transforms.Span;
                var positionSpan = positions.Span;
                var startSpan = starts.Span;
                var targetSpan = targets.Span;
                for (int n = 0; n < positions.Length; n++)
                {
                    //ref var pos = ref positionSpan[n];
                    var pos =  Vector3.Lerp(startSpan[n].value, targetSpan[n].value, complete);
                    //ref var target = ref targets[n];
                    //var pos = target.value;
                    positionSpan[n].value = pos;
                    transformSpan[n].value = world + Matrix4x4.CreateTranslation(pos);
                }
            });
            queryJob.JobRunner = runner;
            queryJob.RunParallel();
        }
    }

}

public delegate void ForEachComponentResult<T1, T2, T3, T4>(ref T1 component1, ref T2 component2, ref T3 component3, ref T4 component4)
    where T1 : struct
    where T2 : struct
    where T3 : struct
    where T4 : struct;

public static class EEEE
{
    public static void ForEachComponentResult<T1, T2, T3, T4>(this QueryChunks<T1, T2, T3, T4> Chunks, ForEachComponentResult<T1, T2, T3, T4> lambda)
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        where T3 : struct, IComponent
        where T4 : struct, IComponent
    {
        foreach (var (chunk1, chunk2, chunk3, chunk4, entities) in Chunks)
        {
            var span1 = chunk1.Span;
            var span2 = chunk2.Span;
            var span3 = chunk3.Span;
            var span4 = chunk4.Span;
            var ids = entities.Ids;
            for (int n = 0; n < chunk1.Length; n++)
            {
                lambda(ref span1[n], ref span2[n], ref span3[n], ref span4[n]);
            }
        }
    }
}

//public readonly struct QueryChunks<T1, T2, T3, T4> : IEnumerable<Chunks<T1, T2, T3, T4>>, IEnumerable
//    where T1 : struct
//    where T2 : struct
//    where T3 : struct
//    where T4 : struct
//{
//    private readonly ArchetypeQuery<T1, T2, T3, T4> query;

//    public int Count => query.Count;

//    //
//    // Summary:
//    //     Obsolete. Renamed to Friflo.Engine.ECS.QueryChunks`4.Count.
//    [Obsolete("Renamed to Count")]
//    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
//    public int EntityCount => query.Count;

//    public override string ToString()
//    {
//        return query.GetQueryChunksString();
//    }

//    internal QueryChunks(ArchetypeQuery<T1, T2, T3, T4> query)
//    {
//        this.query = query;
//    }

//    [ExcludeFromCodeCoverage]
//    IEnumerator<Chunks<T1, T2, T3, T4>> IEnumerable<Chunks<T1, T2, T3, T4>>.GetEnumerator()
//    {
//        return new ChunkEnumerator<T1, T2, T3, T4>(query);
//    }

//    [ExcludeFromCodeCoverage]
//    IEnumerator IEnumerable.GetEnumerator()
//    {
//        return new ChunkEnumerator<T1, T2, T3, T4>(query);
//    }

//    public ChunkEnumerator<T1, T2, T3, T4> GetEnumerator()
//    {
//        return new ChunkEnumerator<T1, T2, T3, T4>(query);
//    }
//}