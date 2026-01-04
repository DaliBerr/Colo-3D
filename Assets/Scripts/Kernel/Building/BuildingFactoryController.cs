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
            Lonize.Events.Event.eventBus.Subscribe< Lonize.Events.EventList.SelectedFactory>(OnFactorySelected);
        }
        private void OnDisable()
        {
            Lonize.Events.Event.eventBus.Unsubscribe< Lonize.Events.EventList.SelectedFactory>(OnFactorySelected);
        }

        private BuildingRuntime _currentFactoryRuntime;

        private void OnFactorySelected(Lonize.Events.EventList.SelectedFactory evt)
        {
            BuildingRuntimeHost[] host;
            host = FindObjectsByType<BuildingRuntimeHost>(FindObjectsSortMode.None);
            foreach (var h in host)
            {
                if(h.Runtime.BuildingID == evt.buildingId)
                {
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