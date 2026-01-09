using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kernel;
using Kernel.Building;
using Kernel.Factory.Connections;
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
        [SerializeField] private Button applyDesignButton;
        [SerializeField] private List<Button> itemButtons = new List<Button>();
        // [SerializeField] private GameObject gridSelectedPanel;
        
        //Obsolete
        // [SerializeField] private Button AddInteriorBuildingButton;

        [SerializeField] private static readonly byte  _columns = 7;
        [SerializeField] private List<FactoryUILinkData> uiLinks = new List<FactoryUILinkData>();

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
            // 防呆：grid index 合法性
            if (!IsValidGridIndex(selectedGridIndex))
            {
                GameDebug.LogWarning($"[FactoryUI] selectedGridIndex 越界：{selectedGridIndex} / itemButtons={itemButtons?.Count ?? 0}");
                return;
            }

            if (!evt.isAdd)
            {
                TryRemoveInteriorBuilding(selectedGridIndex);
                GameDebug.Log("[FactoryUI] Remove Interior Building");
                return;
            }

            // ✅ 关键：TryAdd 时把“新建的 child runtime”直接拿出来
            if (TryAddInteriorBuilding(evt.buildingId, selectedGridIndex, out var addedChild))
            {
                _ = TryShowInteriorBuilding(selectedGridIndex, evt.buildingId);

                if (addedChild != null)
                {
                    BuildingFactory.InitializeInternalBehaviours(addedChild);
                }
                else
                {
                    GameDebug.LogWarning("[FactoryUI] 添加成功但 addedChild 为 null，跳过 InitializeInternalBehaviours");
                }

                GameDebug.Log("[FactoryUI] Successfully added interior building.");
            }
            else
            {
                GameDebug.LogWarning("[FactoryUI] Failed to add interior building.");
            }
        }
        /// <summary>
        /// summary: 尝试在指定格子添加内部建筑，并返回新建的运行时数据。
        /// param: defID 内部建筑定义ID
        /// param: index 工厂内部格子索引
        /// param: addedChild 成功时返回新建的子建筑运行时
        /// return: 是否添加成功
        /// </summary>
        private bool TryAddInteriorBuilding(string defID, int index, out FactoryChildRuntime addedChild)
        {
            addedChild = null;

            var factoryCtrl = BuildingFactoryController.Instance;
            if (factoryCtrl == null) return false;

            var currentFactoryRuntime = factoryCtrl.GetCurrentFactoryRuntime();
            if (currentFactoryRuntime == null)
            {
                GameDebug.LogWarning("当前没有选中任何工厂，无法添加内部建筑哦！");
                return false;
            }

            if (!IsValidGridIndex(index))
            {
                GameDebug.LogWarning($"[FactoryUI] TryAddInteriorBuilding index 越界：{index}");
                return false;
            }

            // ✅ 关键：位置不为空就直接退出（你原来调用了但没用返回值）
            if (!CheckEmptyAtIndex(index))
            {
                GameDebug.LogWarning($"[FactoryUI] 格子 {index} 非空，添加取消。");
                return false;
            }

            Vector2Int position = GetCellPositionByIndex(index);

            var newChild = BuildingFactory.CreateInternalRuntime(
                currentFactoryRuntime.BuildingID,
                defID,
                position
            );

            if (newChild == null)
            {
                GameDebug.LogError($"创建失败，可能是 Def ID {defID} 不存在或者类型不对。");
                return false;
            }

            currentFactoryRuntime.EnsureFactoryInterior().Children.Add(newChild);
            addedChild = newChild;

            GameDebug.Log($"✨ 成功向工厂 {currentFactoryRuntime.BuildingID} 添加了内部建筑 {defID} @ {position}");
            return true;
        }

        /// <summary>
        /// summary: 检查工厂UI格子索引是否有效。
        /// param: index 格子索引
        /// return: 是否有效
        /// </summary>
        private bool IsValidGridIndex(int index)
        {
            return itemButtons != null && index >= 0 && index < itemButtons.Count;
        }
        protected override void OnInit()
        {
            closeButton.onClick.AddListener(TryCloseUI);
            if (applyDesignButton != null)
            {
                applyDesignButton.onClick.AddListener(OnApplyDesignButtonClicked);
            }
            for(int i = 0; i < itemButtons.Count; i++)
            {
                int index = i; // 捕获当前索引
                itemButtons[i].onClick.AddListener(() =>
                {
                    OnItemButtonClicked(index);
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
            if (applyDesignButton != null)
            {
                applyDesignButton.onClick.RemoveAllListeners();
            }
            for (int i = 0; i < itemButtons.Count; i++)
            {
                ClearInteriorBuildingDisplay(i);
                itemButtons[i].onClick.RemoveAllListeners();
            }
        }
        /// <summary>
        /// summary: 点击完成/应用设计按钮。
        /// param: 无
        /// return: 无
        /// </summary>
        private void OnApplyDesignButtonClicked()
        {
            ApplyFactoryDesign();
        }

        /// <summary>
        /// summary: 应用当前工厂内部设计并生成合成行为。
        /// param: 无
        /// return: 无
        /// </summary>
        private void ApplyFactoryDesign()
        {
            var factoryCtrl = BuildingFactoryController.Instance;
            if (factoryCtrl == null)
            {
                GameDebug.LogWarning("[FactoryUI] 未找到 BuildingFactoryController，无法应用设计。");
                return;
            }

            var runtime = factoryCtrl.GetCurrentFactoryRuntime();
            if (runtime == null)
            {
                GameDebug.LogWarning("[FactoryUI] 当前没有选中工厂，无法应用设计。");
                return;
            }

            var interior = runtime.EnsureFactoryInterior();
            var children = interior.Children;
            // GameDebug.Log($"[FactoryUI] 应用工厂设计，内部建筑数量：{children?.Count ?? 0}");
            interior.Connections ??= new FactoryInteriorConnectionsRuntime();
            interior.Connections.RebindAllPorts(children);
            BuildLinksFromUI(interior.Connections, runtime.BuildingID);
            interior.InteriorLinks = interior.Connections.ExportLinksForSave();

            BuildingFactory.BuildFactoryCompositeBehaviour(runtime);
            GameDebug.Log("[FactoryUI] 工厂内部设计已应用完成。");
        }

        /// <summary>
        /// summary: 根据 UI 连接数据创建连接图 Link。
        /// param: connections 连接运行时
        /// param: factoryId 默认工厂ID
        /// return: 成功创建的连接数量
        /// </summary>
        private int BuildLinksFromUI(FactoryInteriorConnectionsRuntime connections, long factoryId)
        {
            if (connections == null || uiLinks == null || uiLinks.Count == 0)
            {
                return 0;
            }

            int createdCount = 0;
            for (int i = 0; i < uiLinks.Count; i++)
            {
                var link = uiLinks[i];
                var aFactoryId = link.AFactoryId > 0 ? link.AFactoryId : factoryId;
                var bFactoryId = link.BFactoryId > 0 ? link.BFactoryId : factoryId;

                var a = new PortKey(aFactoryId, link.ALocalId, link.APortId);
                var b = new PortKey(bFactoryId, link.BLocalId, link.BPortId);

                if (connections.TryCreateLink(a, b, out _, out var error))
                {
                    createdCount++;
                }
                else
                {
                    GameDebug.LogWarning($"[FactoryUI] 连接创建失败: {error}");
                }
            }

            return createdCount;
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
            StartCoroutine(ShowFactoryGridSelectionUI());
            // 在这里添加处理按钮点击的逻辑
        }

        private IEnumerator ShowFactoryGridSelectionUI()
        {
            yield return UIManager.Instance.ShowModalAndWait<FactoryGridSelectionUI>();
            bool isEmpty = CheckEmptyAtIndex(selectedGridIndex);
            var evt = new FactoryGridSelected(selectedGridIndex,isEmpty);
            while(!(UIManager.Instance.GetTopModal() is FactoryGridSelectionUI))
            {
                yield return null;
            }

            Lonize.Events.Event.eventBus.Publish(evt);
        }

        private async Task<GameObject> TryShowInteriorBuilding(int index,string defID = "factory_interior_default")
        {
            if(!IsValidGridIndex(index))
            {
                GameDebug.LogError($"[FactoryUI] TryShowInteriorBuilding index 越界：{index}");
                return null;
            }

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
            GameDebug.Log($"位置 {position} 是空的，可以添加建筑。");
                return true;
        }

        private bool TryRemoveInteriorBuilding(int index)
        {
            var factoryCtrl = BuildingFactoryController.Instance;
            if (factoryCtrl == null) return false;

            var currentFactoryRuntime = factoryCtrl.GetCurrentFactoryRuntime();
            if (currentFactoryRuntime == null)
            {
                GameDebug.LogWarning("当前没有选中任何工厂，无法移除内部建筑哦！");
                return false;
            }
            
            Vector2Int position = GetCellPositionByIndex(index);
            for (int i = 0; i < currentFactoryRuntime.FactoryInterior.Children.Count; i++)
            {
                var child = currentFactoryRuntime.FactoryInterior.Children[i];
                if (child.CellPosition == position)
                {
                    currentFactoryRuntime.FactoryInterior.Children.RemoveAt(i);
                    ClearInteriorBuildingDisplay(index);
                    GameDebug.Log($"🗑️ 成功移除了工厂 {currentFactoryRuntime.BuildingID} 内部建筑 @ {position}");
                    return true;
                }
            }

            GameDebug.LogWarning($"位置 {position} 没有建筑，无法移除。");
            return false;
        }

        // private bool TryAddInteriorBuilding(string defID, int index)
        // {
        //     var factoryCtrl = BuildingFactoryController.Instance;
        //     if (factoryCtrl == null) return false;

        //     var currentFactoryRuntime = factoryCtrl.GetCurrentFactoryRuntime();
        //     if (currentFactoryRuntime == null)
        //     {
        //         GameDebug.LogWarning("当前没有选中任何工厂，无法添加内部建筑哦！");
        //         return false;
        //     }
            
        //     CheckEmptyAtIndex(index);
        //     Vector2Int position = GetCellPositionByIndex(index);
        //     // // 2. 检查位置是否被占用了 (简单防呆)
        //     // // 遍历当前的所有子建筑，看看有没有人在这个格子上
        //     // if (currentFactoryRuntime.FactoryInterior != null)
        //     // {
        //     //     foreach (var child in currentFactoryRuntime.FactoryInterior.Children)
        //     //     {
        //     //         if (child.CellPosition == position)
        //     //         {
        //     //             GameDebug.LogError($"位置 {position} 已经有东西啦！添加失败。");
        //     //             return false;
        //     //         }
        //     //     }
        //     // }

        //     var newChild = BuildingFactory.CreateInternalRuntime(
        //     currentFactoryRuntime.BuildingID, // 父建筑 ID
        //         defID,                            // 内部建筑 Def ID
        //         position                          // 放置位置
        //     );

        //     if (newChild == null)
        //     {
        //         GameDebug.LogError($"创建失败，可能是 Def ID {defID} 不存在或者类型不对。");
        //         return false;
        //     }

        //     currentFactoryRuntime.EnsureFactoryInterior().Children.Add(newChild);
        //     GameDebug.Log($"✨ 成功向工厂 {currentFactoryRuntime.BuildingID} 添加了内部建筑 {defID  } @ {position}");
        //     return true;
        // }

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

        [System.Serializable]
        private struct FactoryUILinkData
        {
            public long AFactoryId;
            public long ALocalId;
            public string APortId;
            public long BFactoryId;
            public long BLocalId;
            public string BPortId;
        }
    }
}
