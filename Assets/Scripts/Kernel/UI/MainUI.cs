using System.Collections;
using System.Collections.Generic;
using Kernel;
using Kernel.Building;
using Kernel.GameState;
using Lonize.Logging;
using Lonize.UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    [UIPrefab("Prefabs/UI/Main UI")]
    public sealed class MainUI : UIScreen
    {
        public Button Btn1, Btn2;

        public override Status currentStatus { get; } = StatusList.PlayingStatus;

        public GameObject MiniMapContainer;
        // public List<string> CurrentStatus = new();
        
        protected override void OnInit()
        {
            // BuildingPlacementController buildingPlacementController = FindAnyObjectByType<BuildingPlacementController>();

            Btn1.onClick.AddListener(() => TrybuildingPlacementMode());
            Btn2.onClick.AddListener(() => TryBuildingRemoveMode());
        }
        private void TryBuildingRemoveMode()
        {
            BuildingDestroyingController buildingRemoveController = FindAnyObjectByType<BuildingDestroyingController>();
            if(StatusController.HasStatus(StatusList.BuildingDestroyingStatus))
            {
                buildingRemoveController.StopRemoveMode();
                return;
            }
            else
            {
                buildingRemoveController.StartRemoveMode();
                return;
            }
            // Status currentStatus = StatusController.CurrentStatus.Find(s => s.StatusName == "RemovingBuilding");
            // BuildingRemoveController buildingRemoveController = FindAnyObjectByType<BuildingRemoveController>();
        }

        private void TrybuildingPlacementMode()
        {
            BuildingPlacementController buildingPlacementController = FindAnyObjectByType<BuildingPlacementController>();
            if(StatusController.HasStatus(StatusList.BuildingPlacementStatus))
            {
                buildingPlacementController.CancelPlacement();
                return;
            }
            else
            {
                TryPlaceBuildingCoroutine();
                return;
            }
        }


        private async void TryPlaceBuildingCoroutine()
        {
            BuildingPlacementController buildingPlacementController = FindAnyObjectByType<BuildingPlacementController>();
            await buildingPlacementController.StartPlacementById("generator_small");

        }
    }
}