using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace Demo.Systems
{
    [AlwaysUpdateSystem]
    [DisableAutoCreation]
    public class OrbitSystem : JobComponentSystem
    {

        [BurstCompile]
        private struct OrbitSystemJob : IJobProcessComponentData<Rotation, MoveSpeed, Position>
        {
            public float dt;
            public float3 zero;
            public Quaternion tangent;
            public void Execute (ref Rotation rotation, [ReadOnly] ref MoveSpeed speed, ref Position position)
            {
                var tangentVector = tangent * (position.Value - zero);
                rotation.Value = Quaternion.LookRotation (tangentVector);
                position.Value = position.Value + math.forward (rotation.Value) * speed.speed * dt;
            }
        }

        protected override JobHandle OnUpdate (JobHandle inputDeps)
        {
            return new OrbitSystemJob { dt = Time.deltaTime, zero = math.float3 (0), tangent = Quaternion.Euler (0, 90, 0) }.Schedule (this, 128, inputDeps);
        }

        protected override void OnCreateManager (int capacity) { }

        protected override void OnDestroyManager () { }
    }
}