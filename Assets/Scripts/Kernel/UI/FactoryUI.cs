using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Kernel;
using Kernel.Building;
using Kernel.Factory.Connections;
using Kernel.GameState;
using Lonize;
using Lonize.EventSystem;
using Lonize.Logging;
using Lonize.UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static Lonize.EventSystem.EventList;

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
        private RectTransform pendingPortRect;

        [SerializeField] private Button applyDesignButton;
        [SerializeField] private List<Button> itemButtons = new List<Button>();
        [SerializeField] private RopeLinkView ropePreview;
        [SerializeField] private RectTransform ropeLinksContainer;


        [SerializeField] private static readonly byte  _columns = 7;
        [SerializeField] private List<FactoryUILinkData> uiLinks = new List<FactoryUILinkData>();

        private int selectedGridIndex = 0;
        private readonly Dictionary<long, RopeLinkBinding> ropeLinks = new Dictionary<long, RopeLinkBinding>();

        public override Status currentStatus { get; } = StatusList.PlayingStatus;

        private void OnEnable()
        {
            EventManager.eventBus.Subscribe<TryModifyInteriorBuildingEvent>(OnTryAddInteriorBuildingEvent);
            connectionsBound = false;
            ClearPendingConnection();

            _ = InitInteriorShow();
        }
        private void OnDisable()
        {
            EventManager.eventBus.Unsubscribe<TryModifyInteriorBuildingEvent>(OnTryAddInteriorBuildingEvent);
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
                RefreshButtons();
                if (addedChild != null)
                {
                    BuildingFactory.InitializeInternalBehaviours(addedChild);
                    if (TryGetConnectionsRuntime(out var connections))
                    {
                        connections.BindChildPorts(addedChild);
                        GameDebug.Log($"[FactoryUI] 已自动绑定新建筑的端口: {evt.buildingId}");
                    }
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
            // RefreshInteriorButtonState();
            
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
                BuildingLayerActiveButton.onClick.AddListener(() => OnBuildingLayerActiveButtonClicked());
            }
            if (ConnectionLayerActiveButton != null)
            {
                ConnectionLayerActiveButton.onClick.AddListener(() => OnConnectionLayerActiveButtonClicked());
            }
            for(int i = 0; i < itemButtons.Count; i++)
            {
                int index = i; // 捕获当前索引
                itemButtons[i].onClick.AddListener(() =>
                {
                    OnItemButtonClicked(index);
                });
                
            }
            // RefreshInteriorButtonState();
            RefreshButtons();
            // AddInteriorBuildingButton.onClick.AddListener(() =>
            // {
            SetLayerButtonColors(Color.green, Color.white);

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

            EnsureConnectionsBound();
            RefreshRopeLinks();
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
        /// summary: 更新绳索预览状态。
        /// param: 无
        /// return: 无
        /// </summary>
        private void Update()
        {
            UpdateRopePreview();
        }
        /// <summary>
        /// summary: 切换当前激活的界面层级。
        /// param: layer 目标激活层级
        /// return: 无
        /// </summary>
        // private void SetActiveLayer(ActivateLayer layer)
        // {
        //     currentActiveLayer = layer;
        //     RefreshInteriorButtonState();
        // }


        private void RefreshFactoryUIButtons()
        {
            // 根据 currentActiveLayer 刷新按钮状态
            switch (currentActiveLayer)
            {
                case ActivateLayer.Building:
                    // 设置建筑层按钮为激活状态
                    foreach (var button in itemButtons)
                    {
                        button.interactable = true;
                    }
                    break;
                case ActivateLayer.Connection:
                    // 设置连接层按钮为激活状态
                    foreach (var button in itemButtons)
                    {
                        button.interactable = false;
                    }
                    break;
            }
        }

        private void RefreshButtons()
        {
            RefreshInteriorButtonState();
            RefreshFactoryUIButtons();
            RefreshRopeLinks();
        }

        /// <summary>
        /// summary: 刷新内部建筑按钮的交互状态。
        /// param: 无
        /// return: 无
        /// </summary>
        private void RefreshInteriorButtonState()
        {

            if (itemButtons == null)
            {
                return;
            }

            for (int i = 0; i < itemButtons.Count; i++)
            {
                var button = itemButtons[i];
                if (button == null)
                {
                    continue;
                }

                var interiorUis = button.GetComponentsInChildren<IInteriorBuildingUI>(true);
                if (interiorUis == null || interiorUis.Length == 0)
                {
                    continue;
                }

                for (int j = 0; j < interiorUis.Length; j++)
                {
                    var interiorUi = interiorUis[j];
                    if (interiorUi == null)
                    {
                        continue;
                    }

                    switch (currentActiveLayer)
                    {
                        case ActivateLayer.Building:
                            interiorUi.SetAllButtonsInteractable(false);
                            
                            break;
                        case ActivateLayer.Connection:
                            interiorUi.SetAllButtonsInteractable(false);
                            interiorUi.SetPortButtonsInteractable(true);
                            break;
                    }
                }
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
            // interior.Connections ??= new FactoryInteriorConnectionsRuntime();
            // interior.Connections.RebindAllPorts(children);
            // var linkErrors = new List<string>();
            // BuildLinksFromUI(interior.Connections, runtime.BuildingID, linkErrors);
            if (interior.Connections == null)
            {
                GameDebug.LogWarning("[FactoryUI] 连接数据为空，没有什么可保存的。");
                return; 
            }

            // 1. 校验 Graph (此时 Graph 里已经是你刚才连好的线了)
            if (!interior.Connections.ValidateGraph(out var graphErrors))
            {
                // 如果有之前的 linkErrors 逻辑，这里只需要展示 graphErrors
                ShowValidationErrors(graphErrors);
                return;
            }
            // if (!interior.Connections.ValidateGraph(out var graphErrors))
            // {
            //     graphErrors.InsertRange(0, linkErrors);
            //     ShowValidationErrors(graphErrors);
            //     return;
            // }

            // if (linkErrors.Count > 0)
            // {
            //     ShowValidationErrors(linkErrors);
            //     return;
            // }

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

            Lonize.EventSystem.EventManager.eventBus.Publish(evt);
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
                    GameDebug.LogError($"位置 {position} 已经有东西啦！");
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
                    if (TryGetConnectionsRuntime(out var connections))
                    {
                        int removedPorts = connections.UnbindChildPorts(child);
                        GameDebug.Log($"[FactoryUI] 移除建筑同时清理了 {removedPorts} 个端口绑定。");
                    }
                    RemoveRopeLinksForBuilding(child);
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
            SetLayerButtonColors(Color.green, Color.white);
            ClearPendingConnection();
            // RefreshInteriorButtonState();
            RefreshButtons();

            if (layer == ActivateLayer.Connection)
            {
                EnsureConnectionsBound();
            }
        }

        private void SetLayerButtonColors(Color activeColor, Color inactiveColor)
        {

            if(currentActiveLayer == ActivateLayer.Building)
            {
                BuildingLayerActiveButton.GetComponent<Image>().color = activeColor;
                ConnectionLayerActiveButton.GetComponent<Image>().color = inactiveColor;
                // 根据 currentActiveLayer 设置按钮颜色
                // 这里可以根据需要自定义颜色
            }
            else if(currentActiveLayer == ActivateLayer.Connection)
            {
                BuildingLayerActiveButton.GetComponent<Image>().color = inactiveColor;
                ConnectionLayerActiveButton.GetComponent<Image>().color = activeColor;
                // 根据 currentActiveLayer 设置按钮颜色
                // 这里可以根据需要自定义颜色
            }
            // 根据 currentActiveLayer 设置按钮颜色
            // 这里可以根据需要自定义颜色
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
            pendingPortRect = null;
            if (ropePreview != null)
            {
                ropePreview.gameObject.SetActive(false);
            }
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
        /// return: 是否设置成功
        /// </summary>
        private bool TrySetPendingPort(PortKey key)
        {
            if (!TryGetConnectionsRuntime(out var connections))
            {
                return false;
            }

            if (connections.Graph == null || !connections.Graph.TryGetPort(key, out var port))
            {
                GameDebug.LogWarning($"[FactoryUI] 端口不存在，无法设置待连接端口：{key}");
                return false;
            }

            if (port.Direction != PortDirection.Output && port.Direction != PortDirection.Bidirectional)
            {
                GameDebug.LogWarning($"[FactoryUI] 端口方向不匹配，无法作为输出端口：{port.Direction}");
                return false;
            }

            pendingPort = key;
            pendingDirection = port.Direction;
            pendingPortRect = FindPortButtonRect(key);
            if (ropePreview != null)
            {
                ropePreview.gameObject.SetActive(pendingPortRect != null);
            }
            GameDebug.Log($"[FactoryUI] 已记录待连接端口：{key}");
            return true;
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
            GameDebug.Log($"[FactoryUI] pendingPort :{pendingPort}");
            var outputRect = pendingPortRect ?? FindPortButtonRect(pendingPort.Value);
            var inputRect = FindPortButtonRect(inputKey);
            if (ropePreview != null && outputRect != null && inputRect != null)
            {
                ropePreview.SetEndpoints(outputRect, inputRect);
            }
            if (connections.TryCreateLink(pendingPort.Value, inputKey, out var linkId, out var error))
                {
                    GameDebug.Log($"[FactoryUI] 创建连接成功，Link ID：{linkId}");
                    
                    // 我们需要把这次成功的连接记录到 uiLinks 里，防止 Apply 时被丢失
                    var newLinkData = new FactoryUILinkData
                    {
                        // A 端点 (Output)
                        AFactoryId = pendingPort.Value.FactoryId,
                        ALocalId = pendingPort.Value.LocalBuildingId,
                        APortId = pendingPort.Value.PortId,

                        // B 端点 (Input)
                        BFactoryId = inputKey.FactoryId,
                        BLocalId = inputKey.LocalBuildingId,
                        BPortId = inputKey.PortId,
                        
                        // 注意：这里可能需要获取 Channel，虽然 uiLinks 定义里好像没用到 Channel 做匹配，
                        // 但如果结构体里有 Channel 字段最好也填上。
                        // 暂时假设 uiLinks 只是为了重建连接关系。
                    };
                    
                    uiLinks.Add(newLinkData);

                    GameDebug.Log($"[FactoryUI] 连接创建成功并已记录：{pendingPort.Value} -> {inputKey}");
                    CreateRopeLinkView(pendingPort.Value, inputKey, linkId, outputRect, inputRect);

                    ClearPendingConnection();
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
            // connections.RebindAllPorts(interior.Children);
            connections.SyncPorts(interior.Children);
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

        /// <summary>
        /// summary: 更新预览绳索的终点位置。
        /// param: 无
        /// return: 无
        /// </summary>
        private void UpdateRopePreview()
        {
            if (ropePreview == null || !pendingPort.HasValue || currentActiveLayer != ActivateLayer.Connection)
            {
                return;
            }

            if (pendingPortRect == null)
            {
                pendingPortRect = FindPortButtonRect(pendingPort.Value);
            }

            if (pendingPortRect == null)
            {
                ropePreview.gameObject.SetActive(false);
                return;
            }

            ropePreview.gameObject.SetActive(true);

            if (TryGetHoveredInputRect(out var hoveredRect))
            {
                ropePreview.SetEndpoints(pendingPortRect, hoveredRect);
                return;
            }

            var mousePosition = GetMouseScreenPosition();
            ropePreview.SetEndpoints(pendingPortRect, mousePosition);
        }

        /// <summary>
        /// summary: 尝试获取鼠标悬停的输入端口按钮。
        /// param: rect 返回按钮 RectTransform
        /// return: 是否找到
        /// </summary>
        private bool TryGetHoveredInputRect(out RectTransform rect)
        {
            rect = null;

            if (itemButtons == null || itemButtons.Count == 0)
            {
                return false;
            }

            var mousePosition = GetMouseScreenPosition();
            var camera = GetUICamera();

            for (int i = 0; i < itemButtons.Count; i++)
            {
                var button = itemButtons[i];
                if (button == null)
                {
                    continue;
                }

                var interiorUis = button.GetComponentsInChildren<IInteriorBuildingUI>(true);
                if (interiorUis == null || interiorUis.Length == 0)
                {
                    continue;
                }

                for (int j = 0; j < interiorUis.Length; j++)
                {
                    var interiorUi = interiorUis[j];
                    if (interiorUi == null || interiorUi.InputButtons == null)
                    {
                        continue;
                    }

                    for (int k = 0; k < interiorUi.InputButtons.Count; k++)
                    {
                        var inputButton = interiorUi.InputButtons[k];
                        if (inputButton == null)
                        {
                            continue;
                        }

                        var inputRect = inputButton.GetComponent<RectTransform>();
                        if (inputRect == null)
                        {
                            continue;
                        }

                        if (RectTransformUtility.RectangleContainsScreenPoint(inputRect, mousePosition, camera))
                        {
                            rect = inputRect;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// summary: 获取鼠标屏幕坐标。
        /// param: 无
        /// return: 鼠标屏幕坐标
        /// </summary>
        private Vector2 GetMouseScreenPosition()
        {
            if (Mouse.current != null)
            {
                return Mouse.current.position.ReadValue();
            }

            return Input.mousePosition;
        }

        /// <summary>
        /// summary: 获取 UI 相机。
        /// param: 无
        /// return: UI 相机
        /// </summary>
        private Camera GetUICamera()
        {
            if (ropePreview == null || ropePreview.container == null)
            {
                return null;
            }

            var canvas = ropePreview.container.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.worldCamera : null;
        }

        /// <summary>
        /// summary: 根据端口键查找按钮 RectTransform。
        /// param: key 端口键
        /// return: 按钮 RectTransform
        /// </summary>
        private RectTransform FindPortButtonRect(PortKey key)
        {
            if (itemButtons == null || itemButtons.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < itemButtons.Count; i++)
            {
                var button = itemButtons[i];
                if (button == null)
                {
                    continue;
                }

                var interiorUis = button.GetComponentsInChildren<IInteriorBuildingUI>(true);
                if (interiorUis == null || interiorUis.Length == 0)
                {
                    continue;
                }

                for (int j = 0; j < interiorUis.Length; j++)
                {
                    var interiorUi = interiorUis[j];
                    if (interiorUi == null)
                    {
                        continue;
                    }

                    if (interiorUi.TryGetPortButtonRect(key, out var rect))
                    {
                        return rect;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// summary: 创建连接绳索视图。
        /// param: outputKey 输出端口键
        /// param: inputKey 输入端口键
        /// param: linkId 连接ID
        /// param: outputRect 输出端口 RectTransform
        /// param: inputRect 输入端口 RectTransform
        /// return: 无
        /// </summary>
        private void CreateRopeLinkView(PortKey outputKey, PortKey inputKey, long linkId, RectTransform outputRect, RectTransform inputRect)
        {
            if (ropePreview == null || ropeLinksContainer == null)
            {
                return;
            }

            outputRect ??= FindPortButtonRect(outputKey);
            inputRect ??= FindPortButtonRect(inputKey);

            var linkView = Instantiate(ropePreview, ropeLinksContainer);
            linkView.gameObject.SetActive(true);
            linkView.container = ropeLinksContainer;

            if (outputRect != null && inputRect != null)
            {
                linkView.SetEndpoints(outputRect, inputRect);
            }

            ropeLinks[linkId] = new RopeLinkBinding
            {
                View = linkView,
                OutputKey = outputKey,
                InputKey = inputKey
            };
        }

        /// <summary>
        /// summary: 移除指定连接的绳索视图。
        /// param: linkId 连接ID
        /// return: 无
        /// </summary>
        private void RemoveRopeLinkView(long linkId)
        {
            if (!ropeLinks.TryGetValue(linkId, out var binding))
            {
                return;
            }

            if (binding.View != null)
            {
                Destroy(binding.View.gameObject);
            }

            ropeLinks.Remove(linkId);
        }

        /// <summary>
        /// summary: 移除与指定建筑相关的绳索视图。
        /// param: child 内部建筑运行时
        /// return: 无
        /// </summary>
        private void RemoveRopeLinksForBuilding(FactoryChildRuntime child)
        {
            if (child == null || ropeLinks.Count == 0)
            {
                return;
            }

            var toRemove = new List<long>();
            foreach (var pair in ropeLinks)
            {
                if (IsPortFromBuilding(pair.Value.OutputKey, child) || IsPortFromBuilding(pair.Value.InputKey, child))
                {
                    toRemove.Add(pair.Key);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                RemoveRopeLinkView(toRemove[i]);
            }
        }

        /// <summary>
        /// summary: 刷新并重建所有连接绳索视图。
        /// param: 无
        /// return: 无
        /// </summary>
        private void RefreshRopeLinks()
        {
            if (!TryGetConnectionsRuntime(out var connections))
            {
                return;
            }

            if (connections.Graph == null)
            {
                return;
            }

            ClearAllRopeLinks();

            var links = connections.Graph.GetAllLinks();
            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i];
                if (!TryResolveLinkEndpoints(connections, link, out var outputKey, out var inputKey))
                {
                    outputKey = link.A;
                    inputKey = link.B;
                }

                var outputRect = FindPortButtonRect(outputKey);
                var inputRect = FindPortButtonRect(inputKey);
                if (outputRect == null || inputRect == null)
                {
                    continue;
                }

                CreateRopeLinkView(outputKey, inputKey, link.LinkId, outputRect, inputRect);
            }
        }

        /// <summary>
        /// summary: 清除所有连接绳索视图。
        /// param: 无
        /// return: 无
        /// </summary>
        private void ClearAllRopeLinks()
        {
            if (ropeLinks.Count == 0)
            {
                return;
            }

            var toRemove = new List<long>(ropeLinks.Keys);
            for (int i = 0; i < toRemove.Count; i++)
            {
                RemoveRopeLinkView(toRemove[i]);
            }
        }

        /// <summary>
        /// summary: 根据连接信息解析输出与输入端口。
        /// param: connections 连接运行时
        /// param: link 连接信息
        /// param: outputKey 输出端口键
        /// param: inputKey 输入端口键
        /// return: 是否成功解析
        /// </summary>
        private bool TryResolveLinkEndpoints(FactoryInteriorConnectionsRuntime connections, LinkInfo link, out PortKey outputKey, out PortKey inputKey)
        {
            outputKey = default;
            inputKey = default;

            if (connections == null || connections.Graph == null || link == null)
            {
                return false;
            }

            if (!connections.Graph.TryGetPort(link.A, out var portA) || !connections.Graph.TryGetPort(link.B, out var portB))
            {
                return false;
            }

            bool aIsOutput = portA.Direction == PortDirection.Output || portA.Direction == PortDirection.Bidirectional;
            bool aIsInput = portA.Direction == PortDirection.Input || portA.Direction == PortDirection.Bidirectional;
            bool bIsOutput = portB.Direction == PortDirection.Output || portB.Direction == PortDirection.Bidirectional;
            bool bIsInput = portB.Direction == PortDirection.Input || portB.Direction == PortDirection.Bidirectional;

            if (aIsOutput && bIsInput)
            {
                outputKey = link.A;
                inputKey = link.B;
                return true;
            }

            if (bIsOutput && aIsInput)
            {
                outputKey = link.B;
                inputKey = link.A;
                return true;
            }

            return false;
        }

        /// <summary>
        /// summary: 判断端口是否属于指定建筑。
        /// param: key 端口键
        /// param: child 内部建筑运行时
        /// return: 是否属于
        /// </summary>
        private bool IsPortFromBuilding(PortKey key, FactoryChildRuntime child)
        {
            if (child == null)
            {
                return false;
            }

            return key.FactoryId == child.BuildingParentID && key.LocalBuildingId == child.BuildingLocalID;
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

        private struct RopeLinkBinding
        {
            public RopeLinkView View;
            public PortKey OutputKey;
            public PortKey InputKey;
        }
    }
}
