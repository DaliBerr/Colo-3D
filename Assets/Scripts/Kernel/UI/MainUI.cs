using System.Collections;
using System.Collections.Generic;
using Kernel;
using Kernel.Building;
using Kernel.GameState;
using Lonize;
using Lonize.Logging;
using Lonize.UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Kernel.UI
{
    [UIPrefab("Prefabs/UI/Main UI")]
    public sealed class MainUI : UIScreen
    {
        public Button Btn1, Btn2;
        public Button Btn3;

        public override Status currentStatus { get; } = StatusList.PlayingStatus;

        public GameObject MiniMapContainer;
        // public List<string> CurrentStatus = new();
        
        private DevControls devInputActions;
        protected override void OnInit()
        {
            // BuildingPlacementController buildingPlacementController = FindAnyObjectByType<BuildingPlacementController>();

            Btn1.onClick.AddListener(() => TrybuildingPlacementMode("generator_small"));
            Btn2.onClick.AddListener(() => TryBuildingRemoveMode());
            Btn3.onClick.AddListener(() => TrybuildingPlacementMode("storehouse"));

            
        }
        private void Start()
        {
            devInputActions = InputActionManager.Instance.Dev;
        }
        private void OnDestroy()
        {
            Btn1.onClick.RemoveAllListeners();
            Btn2.onClick.RemoveAllListeners();
            Btn3.onClick.RemoveAllListeners();
        }

        private bool isOpenLogConsole = false;
        private void OnEnable()
        {
            Lonize.Events.Event.eventBus.Subscribe<Lonize.Events.EventList.CloseModalRequest>(OnCloseModalRequest);
        }
        private void OnDisable()
        {
            Lonize.Events.Event.eventBus.Unsubscribe<Lonize.Events.EventList.CloseModalRequest>(OnCloseModalRequest);
        }
        private void OnCloseModalRequest(Lonize.Events.EventList.CloseModalRequest evt)
        {
            GameDebug.Log("MainUI received CloseModalRequest for " + evt.modalUI.name);
            if(evt.modalUI.name == "LogUI(Clone)")
            {
                isOpenLogConsole = !isOpenLogConsole;
            }
        }

        private void Update()
        {
            if(devInputActions.Console.Open.triggered)
            {
                isOpenLogConsole = !isOpenLogConsole;
                if(!isOpenLogConsole)
                    StartCoroutine(UIManager.Instance.PopModalAndWait());
                else
                    UIManager.Instance.ShowModal<LogConsoleUI_List>();
            }
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

        private void TrybuildingPlacementMode(string buildingId = "generator_small")
        {
            BuildingPlacementController buildingPlacementController = FindAnyObjectByType<BuildingPlacementController>();
            if(StatusController.HasStatus(StatusList.BuildingPlacementStatus))
            {
                buildingPlacementController.CancelPlacement();
                return;
            }
            else
            {
                TryPlaceBuildingCoroutine(buildingId);
                return;
            }
        }


        private async void TryPlaceBuildingCoroutine(string buildingId = "generator_small")
        {
            BuildingPlacementController buildingPlacementController = FindAnyObjectByType<BuildingPlacementController>();
            await buildingPlacementController.StartPlacementById(buildingId);

        }
    }
}