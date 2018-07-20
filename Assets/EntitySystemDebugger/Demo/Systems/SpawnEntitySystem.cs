using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using Random = Demo.Utils.Random;

namespace Demo.Systems
{

    [AlwaysUpdateSystem]
    [DisableAutoCreation]
    public class SpawnEntitySystem : JobComponentSystem
    {

        public int Spawning = 1024;
        public int Spawned = 0;
        public int SpawnPerBatch = 64;
        public Mesh AgentMesh;
        public Material AgentMaterial;

        private MeshInstanceRenderer sharedMeshRenderer;

        private struct RandomizeDataJob : IJobParallelFor
        {
            [WriteOnly] public NativeArray<float3> positions;
            [WriteOnly] public NativeArray<quaternion> rotations;
            public void Execute (int index)
            {
                var position = Random.InUnitSphere () * 100f;
                position.y = 0;
                positions[index] = position;
                rotations[index] = Quaternion.identity;
            }
        }

        // [BurstCompile]
        private struct CreateAgentJob : IJobParallelFor
        {
            [WriteOnly] public EntityCommandBuffer.Concurrent buffer;
            [ReadOnly][DeallocateOnJobCompletion] public NativeArray<float3> positions;
            [ReadOnly][DeallocateOnJobCompletion] public NativeArray<quaternion> rotations;
            public void Execute (int index)
            {
                buffer.CreateEntity ();
                buffer.AddComponent<Position> (new Position { Value = positions[index] });
                buffer.AddComponent<Rotation> (new Rotation { Value = rotations[index] });
                buffer.AddComponent<MoveSpeed> (new MoveSpeed { speed = 10f });
                buffer.AddComponent<TransformMatrix> (new TransformMatrix { Value = new Matrix4x4 () });
            }
        }

        struct PendingMeshRenderer
        {
            public readonly int Length;
            public EntityArray entities;
            public ComponentDataArray<TransformMatrix> transforms;
            public SubtractiveComponent<MeshInstanceRenderer> sub;
        }

        struct ExistingEntities
        {
            public readonly int Length;
            public EntityArray entities;
            public ComponentDataArray<TransformMatrix> transforms;
        }

        [Inject] SpawnBarrier barrier;
        [Inject] PendingMeshRenderer pendingMeshRenderer;
        [Inject] ExistingEntities existingEntities;

        protected override JobHandle OnUpdate (JobHandle inputDeps)
        {
            if (sharedMeshRenderer.mesh != AgentMesh || sharedMeshRenderer.material != AgentMaterial)
            {
                var buffer = barrier.CreateCommandBuffer ();
                sharedMeshRenderer.mesh = AgentMesh;
                sharedMeshRenderer.material = AgentMaterial;
                for (int i = 0; i < existingEntities.Length; i++)
                {
                    buffer.SetSharedComponent<MeshInstanceRenderer> (existingEntities.entities[i], sharedMeshRenderer);
                }

            }
            if (Spawning > 0)
            {
                int batchCount = math.min (SpawnPerBatch, Spawning);
                Spawning -= batchCount;
                Spawned += batchCount;

                var positions = new NativeArray<float3> (batchCount, Allocator.TempJob);
                var rotations = new NativeArray<quaternion> (batchCount, Allocator.TempJob);

                var randomDataJob = new RandomizeDataJob
                {
                    positions = positions,
                        rotations = rotations
                }.Schedule (batchCount, 16, inputDeps);

                var createAgentJob = new CreateAgentJob
                {
                    buffer = barrier.CreateCommandBuffer (),
                        positions = positions,
                        rotations = rotations
                }.Schedule (batchCount, 16, randomDataJob);
                return createAgentJob;
            }

            if (pendingMeshRenderer.Length > 0)
            {
                var buffer = barrier.CreateCommandBuffer ();
                for (int i = 0; i < pendingMeshRenderer.Length; i++)
                {
                    buffer.AddSharedComponent<MeshInstanceRenderer> (pendingMeshRenderer.entities[i], sharedMeshRenderer);
                }
            }

            return inputDeps;
        }

        protected override void OnCreateManager (int capacity)
        {

            var go = GameObject.CreatePrimitive (PrimitiveType.Cube);

            AgentMesh = go.GetComponent<MeshFilter> ().sharedMesh;

            AgentMaterial = Resources.Load ("AgentMaterial") as Material;

            GameObject.DestroyImmediate (go);

            var entityManager = World.Active.GetOrCreateManager<EntityManager> ();

            sharedMeshRenderer = new MeshInstanceRenderer { mesh = AgentMesh, material = AgentMaterial };
        }

        protected override void OnDestroyManager () { }

        public void Spawn ()
        {
            Spawning += 1024;
        }
    }
}
