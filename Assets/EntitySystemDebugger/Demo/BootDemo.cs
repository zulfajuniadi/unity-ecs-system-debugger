using Demo.Systems;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Demo
{
    static class BootDemo
    {
        static void DomainUnloadShutdown ()
        {
            World.DisposeAllWorlds ();
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop ();
        }

        [RuntimeInitializeOnLoadMethod (RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BootWorld ()
        {
            var world = new World ("Demo World");
            world.CreateManager<SpawnBarrier> ();
            world.CreateManager<SpawnEntitySystem> ();
            world.CreateManager<OrbitSystem> ();
            world.CreateManager<TransformSystem> ();
            world.CreateManager<MeshInstanceRendererSystem> ();
            ScriptBehaviourUpdateOrder.UpdatePlayerLoop (world);
            PlayerLoopManager.RegisterDomainUnload (DomainUnloadShutdown, 10000);
        }
    }
}