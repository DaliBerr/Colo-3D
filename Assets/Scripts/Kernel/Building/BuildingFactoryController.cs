using System.Collections.Generic;
using Lonize.Logging;
using UnityEngine;

namespace Kernel.Building
{
    public class BuildingFactoryController : MonoBehaviour
    {
        public static BuildingFactoryController Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            Lonize.Events.Event.eventBus.Subscribe<Lonize.Events.EventList.BuildingSelected>(OnFactorySelected);
        }
        private void OnDisable()
        {
            Lonize.Events.Event.eventBus.Unsubscribe<Lonize.Events.EventList.BuildingSelected>(OnFactorySelected);
        }

        private static BuildingRuntime _currentFactoryRuntime;

        private void OnFactorySelected(Lonize.Events.EventList.BuildingSelected evt)
        {
            BuildingRuntimeHost[] host;
            host = FindObjectsByType<BuildingRuntimeHost>(FindObjectsSortMode.None);
            if (host == null || host.Length == 0)
            {
                // GameDebug.LogError("No BuildingRuntimeHost found in the scene.");
                return;
            }
            foreach (var h in host)
            {
                if(!evt.isSelected) continue;
                if(h.Runtime == null) continue;
                if(h.Runtime.BuildingID == evt.buildingId)
                {
                    if(h.Runtime.Def.Category != BuildingCategory.Factory)
                    {
                        GameDebug.LogError($"Selected building is not a factory. ID={evt.buildingId}");
                        Log.Error($"Selected building is not a factory. ID={evt.buildingId}");
                        return;
                    }
                    _currentFactoryRuntime = h.Runtime;
                    GameDebug.Log($"Factory selected: ID={evt.buildingId}");
                    break;
                }
            }
        }

        public BuildingRuntime GetCurrentFactoryRuntime()
        {
            return _currentFactoryRuntime;
        }
    }
}