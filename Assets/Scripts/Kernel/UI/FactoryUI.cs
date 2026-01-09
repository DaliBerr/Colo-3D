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
        
        private enum ActivateLayer
        {
            Building,
            Connection
        }

        [SerializeField] private Button closeButton;


        [SerializeField] private Button BuildingLayerActiveButton;
        [SerializeField] private Button ConnectionLayerActiveButton;
        
        private ActivateLayer currentActiveLayer = ActivateLayer.Building;
        private PortKey? pendingPort;
        private PortDirection pendingDirection = PortDirection.Output;
        private bool connectionsBound = false;

        [SerializeField] private Button applyDesignButton;
        [SerializeField] private List<Button> itemButtons = new List<Button>();


        [SerializeField] private static readonly byte  _columns = 7;
        [SerializeField] private List<FactoryUILinkData> uiLinks = new List<FactoryUILinkData>();

        private int selectedGridIndex = 0;

        public override Status currentStatus { get; } = StatusList.PlayingStatus;

        private void OnEnable()
        {
            Lonize.Events.Event.eventBus.Subscribe<TryModifyInteriorBuildingEvent>(OnTryAddInteriorBuildingEvent);
            connectionsBound = false;
            ClearPendingConnection();

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
                _ = TryShowInteriorBuilding(selectedGridIndex, evt.buildingId, addedChild);
                MarkConnectionsDirty();

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
            if (BuildingLayerActiveButton != null)
            {
                BuildingLayerActiveButton.onClick.AddListener(OnBuildingLayerActiveButtonClicked);
            }
            if (ConnectionLayerActiveButton != null)
            {
                ConnectionLayerActiveButton.onClick.AddListener(OnConnectionLayerActiveButtonClicked);
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
                await TryShowInteriorBuilding(GetIndexByCellPosition(child.CellPosition), child.Def.Id, child);
            }
        }

        private void OnDestroy()
        {
            
            closeButton.onClick.RemoveAllListeners();
            if (applyDesignButton != null)
            {
                applyDesignButton.onClick.RemoveAllListeners();
            }
            if (BuildingLayerActiveButton != null)
            {
                BuildingLayerActiveButton.onClick.RemoveAllListeners();
            }
            if (ConnectionLayerActiveButton != null)
            {
                ConnectionLayerActiveButton.onClick.RemoveAllListeners();
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
            var linkErrors = new List<string>();
            BuildLinksFromUI(interior.Connections, runtime.BuildingID, linkErrors);

            if (!interior.Connections.ValidateGraph(out var graphErrors))
            {
                graphErrors.InsertRange(0, linkErrors);
                ShowValidationErrors(graphErrors);
                return;
            }

            if (linkErrors.Count > 0)
            {
                ShowValidationErrors(linkErrors);
                return;
            }

            interior.InteriorLinks = interior.Connections.ExportLinksForSave();

            BuildingFactory.BuildFactoryCompositeBehaviour(runtime);
            GameDebug.Log("[FactoryUI] 工厂内部设计已应用完成。");
        }

        /// <summary>
        /// summary: 根据 UI 连接数据创建连接图 Link。
        /// param: connections 连接运行时
        /// param: factoryId 默认工厂ID
        /// param: errors 返回连接创建失败的错误列表
        /// return: 成功创建的连接数量
        /// </summary>
        private int BuildLinksFromUI(FactoryInteriorConnectionsRuntime connections, long factoryId, List<string> errors)
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
                    errors?.Add(error);
                    GameDebug.LogWarning($"[FactoryUI] 连接创建失败: {error}");
                }
            }

            return createdCount;
        }

        /// <summary>
        /// summary: 通过弹窗提示校验失败原因。
        /// param: errors 错误列表
        /// return: 无
        /// </summary>
        private void ShowValidationErrors(IReadOnlyList<string> errors)
        {
            if (errors == null || errors.Count == 0) return;
            StartCoroutine(ShowValidationPopup(string.Join("\n", errors)));
        }

        /// <summary>
        /// summary: 弹出校验失败提示框。
        /// param: message 提示内容
        /// return: 协程枚举器
        /// </summary>
        private IEnumerator ShowValidationPopup(string message)
        {
            yield return UIManager.Instance.ShowModalAndWait<PopupModal>();
            var modal = UIManager.Instance.GetTopModal(false) as PopupModal;
            if (modal == null) yield break;

            modal.SetMessage(message);
            modal.SetConfirmButtonActive(false);
            modal.SetCloseButtonActive(true);
            modal.SetCloseButtonText("确定");
        }


        private void TryCloseUI()
        {
            UIManager.Instance.CloseTopModal();
        }

        /// <summary>
        /// summary: 切换到建筑编辑层。
        /// param: 无
        /// return: 无
        /// </summary>
        private void OnBuildingLayerActiveButtonClicked()
        {
            SetActiveLayer(ActivateLayer.Building);
        }

        /// <summary>
        /// summary: 切换到连接编辑层。
        /// param: 无
        /// return: 无
        /// </summary>
        private void OnConnectionLayerActiveButtonClicked()
        {
            SetActiveLayer(ActivateLayer.Connection);
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

        private async Task<GameObject> TryShowInteriorBuilding(int index, string defID = "factory_interior_default", FactoryChildRuntime child = null)
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

            if (child == null)
            {
                var runtime = BuildingFactoryController.Instance?.GetCurrentFactoryRuntime();
                if (runtime != null)
                {
                    var cell = GetCellPositionByIndex(index);
                    child = runtime.FactoryInterior.Children.Find(target => target != null && target.CellPosition == cell);
                }
            }

            var interiorUI = go.GetComponent<IInteriorBuildingUI>();
            if (interiorUI != null)
            {
                interiorUI.InitializePortMeta(child);
                interiorUI.PortClicked += OnInteriorPortClicked;
            }

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
                    MarkConnectionsDirty();
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

        /// <summary>
        /// summary: 标记连接数据需要重新绑定。
        /// param: 无
        /// return: 无
        /// </summary>
        private void MarkConnectionsDirty()
        {
            connectionsBound = false;
            ClearPendingConnection();
        }

        /// <summary>
        /// summary: 设置当前激活层并处理连接状态。
        /// param: layer 目标层级
        /// return: 无
        /// </summary>
        private void SetActiveLayer(ActivateLayer layer)
        {
            if (currentActiveLayer == layer)
            {
                return;
            }

            currentActiveLayer = layer;
            ClearPendingConnection();

            if (layer == ActivateLayer.Connection)
            {
                EnsureConnectionsBound();
            }
        }

        /// <summary>
        /// summary: 清空待连接端口状态。
        /// param: 无
        /// return: 无
        /// </summary>
        private void ClearPendingConnection()
        {
            pendingPort = null;
            pendingDirection = PortDirection.Output;
        }

        /// <summary>
        /// summary: 处理内部建筑端口按钮点击。
        /// param: key 端口键
        /// param: direction 端口方向
        /// return: 无
        /// </summary>
        private void OnInteriorPortClicked(PortKey key, PortDirection direction)
        {
            if (currentActiveLayer != ActivateLayer.Connection)
            {
                return;
            }

            if (!EnsureConnectionsBound())
            {
                GameDebug.LogWarning("[FactoryUI] 连接运行时未准备好，无法处理端口点击。");
                return;
            }

            if (direction == PortDirection.Output)
            {
                TrySetPendingPort(key);
                return;
            }

            if (direction == PortDirection.Input)
            {
                TryCreateLinkFromPending(key);
                return;
            }

            GameDebug.LogWarning($"[FactoryUI] 不支持的端口方向：{direction}");
        }

        /// <summary>
        /// summary: 尝试设置待连接端口。
        /// param: key 端口键
        /// return: 无
        /// </summary>
        private void TrySetPendingPort(PortKey key)
        {
            if (!TryGetConnectionsRuntime(out var connections))
            {
                return;
            }

            if (connections.Graph == null || !connections.Graph.TryGetPort(key, out var port))
            {
                GameDebug.LogWarning($"[FactoryUI] 端口不存在，无法设置待连接端口：{key}");
                return;
            }

            if (port.Direction != PortDirection.Output && port.Direction != PortDirection.Bidirectional)
            {
                GameDebug.LogWarning($"[FactoryUI] 端口方向不匹配，无法作为输出端口：{port.Direction}");
                return;
            }

            pendingPort = key;
            pendingDirection = port.Direction;
            GameDebug.Log($"[FactoryUI] 已记录待连接端口：{key}");
        }

        /// <summary>
        /// summary: 使用待连接端口创建连接。
        /// param: inputKey 输入端口键
        /// return: 无
        /// </summary>
        private void TryCreateLinkFromPending(PortKey inputKey)
        {
            if (!pendingPort.HasValue)
            {
                GameDebug.LogWarning("[FactoryUI] 尚未选择输出端口，无法创建连接。");
                return;
            }

            if (pendingDirection != PortDirection.Output && pendingDirection != PortDirection.Bidirectional)
            {
                GameDebug.LogWarning($"[FactoryUI] 待连接端口方向不匹配：{pendingDirection}");
                ClearPendingConnection();
                return;
            }

            if (!TryGetConnectionsRuntime(out var connections))
            {
                return;
            }

            if (connections.Graph == null || !connections.Graph.TryGetPort(inputKey, out var port))
            {
                GameDebug.LogWarning($"[FactoryUI] 端口不存在，无法创建连接：{inputKey}");
                return;
            }

            if (port.Direction != PortDirection.Input && port.Direction != PortDirection.Bidirectional)
            {
                GameDebug.LogWarning($"[FactoryUI] 端口方向不匹配，无法作为输入端口：{port.Direction}");
                return;
            }

            if (connections.TryCreateLink(pendingPort.Value, inputKey, out _, out var error))
            {
                ClearPendingConnection();
                GameDebug.Log($"[FactoryUI] 连接创建成功：{pendingPort.Value} -> {inputKey}");
                return;
            }

            GameDebug.LogWarning($"[FactoryUI] 连接创建失败：{error}");
        }

        /// <summary>
        /// summary: 确保连接运行时已绑定端口。
        /// param: 无
        /// return: 是否绑定成功
        /// </summary>
        private bool EnsureConnectionsBound()
        {
            if (connectionsBound)
            {
                return true;
            }

            if (!TryGetConnectionsRuntime(out var connections))
            {
                return false;
            }

            var runtime = BuildingFactoryController.Instance?.GetCurrentFactoryRuntime();
            if (runtime == null)
            {
                GameDebug.LogWarning("[FactoryUI] 当前没有选中工厂，无法绑定端口。");
                return false;
            }

            var interior = runtime.EnsureFactoryInterior();
            connections.RebindAllPorts(interior.Children);
            connectionsBound = true;
            return true;
        }

        /// <summary>
        /// summary: 获取当前工厂的连接运行时。
        /// param: connections 返回连接运行时
        /// return: 是否成功获取
        /// </summary>
        private bool TryGetConnectionsRuntime(out FactoryInteriorConnectionsRuntime connections)
        {
            connections = null;

            var runtime = BuildingFactoryController.Instance?.GetCurrentFactoryRuntime();
            if (runtime == null)
            {
                GameDebug.LogWarning("[FactoryUI] 当前没有选中工厂，无法获取连接运行时。");
                return false;
            }

            var interior = runtime.EnsureFactoryInterior();
            interior.Connections ??= new FactoryInteriorConnectionsRuntime();
            connections = interior.Connections;
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
