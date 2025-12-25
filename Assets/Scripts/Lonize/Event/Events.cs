using System.Collections.Generic;
using Kernel.Building;
using Kernel.Item;
using Lonize.Tick;
using UnityEngine;

namespace Lonize.Events
{
    public static class Events
    {
        public static readonly EventBus eventBus = new();
    }
    public readonly struct MapReady
    {
        public readonly bool value;
        public readonly Vector3 mapCenterPosition;
        public MapReady(bool value, Vector3 mapCenterPosition)
        {
            this.value = value;
            this.mapCenterPosition = mapCenterPosition;
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
}