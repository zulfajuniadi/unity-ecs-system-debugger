using System;
using Unity.Entities;
using UnityEngine;

namespace EntitySystemDebugger.Editor
{
    public static class DisableSystems
    {
        [RuntimeInitializeOnLoadMethod (RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void DisableSystemsOnLoad ()
        {
            if (World.AllWorlds == null) return;
            foreach (var world in World.AllWorlds)
            {
                var disabled = PlayerPrefs.GetString ("disabledsystems", "");
                var names = disabled.Split (':');
                foreach (var name in names)
                {
                    var type = Type.GetType (name);
                    if (type != null)
                    {
                        var manager = world.GetExistingManager (type) as ComponentSystemBase;
                        if (manager != null)
                        {
                            manager.Enabled = false;
                        }
                    }
                }
            }
        }
    }
}