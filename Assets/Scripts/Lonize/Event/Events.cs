using System.Collections.Generic;
using Kernel.Building;
using Kernel.Item;
using Lonize.Tick;
using Lonize.UI;
using UnityEngine;

namespace Lonize.Events
{
    public static class Event
    {
        public static readonly EventBus eventBus = new();
    }
    public static class EventList{
    public readonly struct MapReady
    {
        public readonly bool value;
        // public readonly Vector3 mapCenterPosition;
        public MapReady(bool value )
        {
            this.value = value;
            // this.mapCenterPosition = mapCenterPosition;
        }
    }
    public readonly struct MainSceneInitialized
    {
        public readonly bool isInitialized;
        public MainSceneInitialized(bool isInitialized)
        {
            this.isInitialized = isInitialized;
        }
    }
    public readonly struct SpeedChange
    {
        public readonly float speedMultiplier;
        public readonly GameSpeed currentGameSpeed;
        public SpeedChange(float speedMultiplier, GameSpeed currentGameSpeed)
        {
            this.speedMultiplier = speedMultiplier;
            this.currentGameSpeed = currentGameSpeed;
        }
    }

    public readonly struct ItemLoaded
    {
        public readonly int itemCount;
        public ItemLoaded(int itemCount)
        {
            this.itemCount = itemCount;

        }
    }

    public readonly struct BuildingLoaded
    {
        public readonly int buildingCount;
        public BuildingLoaded(int buildingCount)
        {
            this.buildingCount = buildingCount;
        }
    }

    public readonly struct BuildingPlaced
    {
        // public readonly string buildingId;
        // public readonly Vector3 position;
        public readonly bool isPlaced;
        public readonly BuildingRuntimeHost BuildingRuntimeHost;
        public BuildingPlaced(bool isPlaced, BuildingRuntimeHost buildingRuntimeHost)
        {
            // this.buildingId = buildingId;
            // this.position = position;
            this.isPlaced = isPlaced;
            this.BuildingRuntimeHost = buildingRuntimeHost;
        }
    }

    public  struct BuildingSelected
    {
        // public readonly string buildingId;
        public  BuildingRuntime buildingRuntime;
        public long buildingId => buildingRuntime.BuildingID;
        public  bool isSelected;
        public BuildingSelected(BuildingRuntime buildingRuntime, bool isSelected)
        {
            this.buildingRuntime = buildingRuntime;
            this.isSelected = isSelected;
        }
    }

    public struct SettingChanged
    {
        public bool needApply;
        public SettingChanged(bool needApply)
        {
            this.needApply = needApply;
        }
    }

    public struct CancelSettingChange
    {
        public List<string> undoSetting;
        public CancelSettingChange(List<string> undoSetting)
        {
            this.undoSetting = undoSetting;
        }
    }

    public struct SaveGameRequest
    {
        public string saveName;
        public SaveGameRequest(string saveName)
        {
            this.saveName = saveName;
        }
    }

    public struct LoadGameRequest
    {
        public string loadName;
        public LoadGameRequest(string loadName)
        {
            this.loadName = loadName;
        }
    }

    public struct CloseModalRequest
    {
        public UIScreen modalUI;
        public CloseModalRequest(UIScreen modalUI)
        {
            this.modalUI = modalUI;
        }

    }

    public struct SelectedFactory
    {
        public long buildingId;
        public SelectedFactory(long buildingId)
        {
        this.buildingId = buildingId;
        }
    }

    }
}