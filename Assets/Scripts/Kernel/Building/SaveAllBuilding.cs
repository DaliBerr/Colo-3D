
using System.Collections.Generic;
using Lonize.Logging;
using Lonize.Scribe;
using UnityEngine;

namespace Kernel.Building
{
    public class SaveBuilding : MonoBehaviour
    {
        public static SaveBuilding Instance { get; private set; }
        public void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static List<SaveBuildingInstance> Buildings ;


        private void OnEnable()
        {
            Lonize.Events.Event.eventBus.Subscribe< Lonize.Events.EventList.MapReady>(OnMapReady);
            
        }
        private void OnDisable()
        {
            Lonize.Events.Event.eventBus.Unsubscribe< Lonize.Events.EventList.MapReady>(OnMapReady);
        }
        private void OnMapReady( Lonize.Events.EventList.MapReady evt)
        {
            if (evt.value)
            {
                BuildingSaveRuntime.RestoreBuildingsFromSave(Buildings);
            }
        }
    }

}