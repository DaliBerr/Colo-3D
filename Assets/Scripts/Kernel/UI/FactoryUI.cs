using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
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
using static Lonize.Events.EventList;

namespace Kernel.UI
{
    [UIPrefab("Prefabs/UI/Factory UI")]
    public sealed class FactoryUI : UIScreen
    {

        [SerializeField] private Button closeButton;
        [SerializeField] private List<Button> itemButtons = new List<Button>();
        // [SerializeField] private GameObject gridSelectedPanel;
        
        //Obsolete
        // [SerializeField] private Button AddInteriorBuildingButton;

        [SerializeField] private static readonly byte  _columns = 7;

        private int selectedGridIndex = 0;

        public override Status currentStatus { get; } = StatusList.PlayingStatus;

        private void OnEnable()
        {
            Lonize.Events.Event.eventBus.Subscribe<TryModifyInteriorBuildingEvent>(OnTryAddInteriorBuildingEvent);

            _ = InitInteriorShow();
        }
        private void OnDisable()
        {
            Lonize.Events.Event.eventBus.Unsubscribe<TryModifyInteriorBuildingEvent>(OnTryAddInteriorBuildingEvent);
        }

        private void OnTryAddInteriorBuildingEvent(TryModifyInteriorBuildingEvent evt)
        {
            if (TryAddInteriorBuilding(evt.buildingId, selectedGridIndex))
                {
                    _ = TryShowInteriorBuilding(selectedGridIndex);
                    BuildingFactory.InitializeInternalBehaviours(
                        BuildingFactoryController.Instance.GetCurrentFactoryRuntime()
                        .FactoryInterior.Children[selectedGridIndex]
                    );
                    GameDebug.Log("Successfully added interior building.");
                }
                
                GameDebug.Log("Add Interior Building Button Clicked - Obsolete");
        }

        protected override void OnInit()
        {
            closeButton.onClick.AddListener(TryCloseUI);
            for(int i = 0; i < itemButtons.Count; i++)
            {
                int index = i; // 捕获当前索引
                itemButtons[i].onClick.AddListener(() =>
                {
                    OnItemButtonClicked(index);
                    Lonize.Events.Event.eventBus.Publish(new FactoryGridSelected(index,CheckEmptyAtIndex(index)));
                });
                
            }
            // AddInteriorBuildingButton.onClick.AddListener(() =>
            // {


            // });
        }

        private async Task InitInteriorShow()
        {
            var runtime = BuildingFactoryController.Instance.GetCurrentFactoryRuntime();
            if (runtime == null)
            {
                GameDebug.LogError("当前没有选中任何工厂，无法初始化工厂界面！");
                UIManager.Instance.CloseTopModal();
                return;
            }
            // 初始化界面内容，比如显示工厂内部建筑等
            foreach (var child in runtime.FactoryInterior.Children)
            {
                GetIndexByCellPosition(child.CellPosition);
                await TryShowInteriorBuilding(GetIndexByCellPosition(child.CellPosition));
            }
        }

        private void OnDestroy()
        {
            
            closeButton.onClick.RemoveAllListeners();
            for (int i = 0; i < itemButtons.Count; i++)
            {
                ClearInteriorBuildingDisplay(i);
                itemButtons[i].onClick.RemoveAllListeners();
            }
        }

        private void TryCloseUI()
        {
            UIManager.Instance.CloseTopModal();
        }

        private void OnItemButtonClicked(int index)
        {
            GameDebug.Log($"Item button {index} clicked.");
            selectedGridIndex = index;
            // gridSelectedPanel.SetActive(true);
            UIManager.Instance.ShowModal<FactoryGridSelectionUI>();
            // 在这里添加处理按钮点击的逻辑
        }

        private async Task<GameObject> TryShowInteriorBuilding(int index,string defID = "factory_interior_default")
        {
            if(!BuildingDatabase.TryGet(defID, out var def))
            {
                GameDebug.LogError($"无法找到内部建筑定义，ID：{defID}");
                return null;
            }
            if(def.Category != BuildingCategory.Internal)
            {
                GameDebug.LogError($"建筑定义不是工厂内部建筑，ID：{defID}");
                return null;
            }
            if(string.IsNullOrEmpty(def.PrefabAddress))
            {
                GameDebug.LogError($"建筑定义没有指定预制体路径，ID：{defID}");
                return null;
            }
            
            var prefab = await AddressableRef.LoadAsync<GameObject>(def.PrefabAddress);
            var go = prefab ? Object.Instantiate(prefab)
                            : new GameObject($"InteriorBuilding_{defID}");
            
            var parent = itemButtons[index].transform;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        
        }




        public void ClearInteriorBuildingDisplay(int index)
        {
            var parent = itemButtons[index].transform;
            foreach(Transform child in parent)
            {
                Object.Destroy(child.gameObject);
            }
        }

        private bool CheckEmptyAtIndex(int index)
        {
            var factoryCtrl = BuildingFactoryController.Instance;
            var currentFactoryRuntime = factoryCtrl.GetCurrentFactoryRuntime();
            Vector2Int position = GetCellPositionByIndex(index);
            foreach (var child in currentFactoryRuntime.FactoryInterior.Children)
            {
                if (child.CellPosition == position)
                {
                    // GameDebug.LogError($"位置 {position} 已经有东西啦！添加失败。");
                    return false;
                }
            }
                return true;
        }

        private bool TryAddInteriorBuilding(string defID, int index)
        {
            var factoryCtrl = BuildingFactoryController.Instance;
            if (factoryCtrl == null) return false;

            var currentFactoryRuntime = factoryCtrl.GetCurrentFactoryRuntime();
            if (currentFactoryRuntime == null)
            {
                GameDebug.LogWarning("当前没有选中任何工厂，无法添加内部建筑哦！");
                return false;
            }
            
            CheckEmptyAtIndex(index);
            Vector2Int position = GetCellPositionByIndex(index);
            // // 2. 检查位置是否被占用了 (简单防呆)
            // // 遍历当前的所有子建筑，看看有没有人在这个格子上
            // if (currentFactoryRuntime.FactoryInterior != null)
            // {
            //     foreach (var child in currentFactoryRuntime.FactoryInterior.Children)
            //     {
            //         if (child.CellPosition == position)
            //         {
            //             GameDebug.LogError($"位置 {position} 已经有东西啦！添加失败。");
            //             return false;
            //         }
            //     }
            // }

            var newChild = BuildingFactory.CreateInternalRuntime(
            currentFactoryRuntime.BuildingID, // 父建筑 ID
                defID,                            // 内部建筑 Def ID
                position                          // 放置位置
            );

            if (newChild == null)
            {
                GameDebug.LogError($"创建失败，可能是 Def ID {defID} 不存在或者类型不对。");
                return false;
            }

            currentFactoryRuntime.EnsureFactoryInterior().Children.Add(newChild);
            GameDebug.Log($"✨ 成功向工厂 {currentFactoryRuntime.BuildingID} 添加了内部建筑 {defID  } @ {position}");
            return true;
        }

        private Vector2Int GetCellPositionByIndex(int index)
        {
            int columns = _columns;
            int x = index % columns;
            int y = index / columns;
            return new Vector2Int(x, y);
        }
        private int GetIndexByCellPosition(Vector2Int position)
        {
            int columns = _columns;
            return position.y * columns + position.x;
        }
    }
}