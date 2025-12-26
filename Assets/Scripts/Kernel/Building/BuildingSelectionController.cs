using System.Collections.Generic;
using Kernel.GameState;
using Lonize;
using Lonize.Events;
using Lonize.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 建筑选中控制器（3D）：单选 + Shift 多选，通过 Raycast 命中建筑并驱动 BuildingView 的 Selected Overlay。
    /// param: 无
    /// return: 无
    /// </summary>
    public class BuildingSelectionController : MonoBehaviour
    {
        public Camera mainCamera;
        public LayerMask buildingLayerMask;

        private readonly Dictionary<long, BuildingRuntimeHost> _selectedHosts = new();
        private readonly Dictionary<long, BuildingView> _selectedViews = new();

        private BuildingControls buildingControls;
        private CameraControls cameraControls;

        /// <summary>
        /// summary: 初始化输入映射并启用。
        /// param: 无
        /// return: 无
        /// </summary>
        private void Awake()
        {
            buildingControls = InputActionManager.Instance.Building;
            cameraControls = InputActionManager.Instance.Camera;
            buildingControls.Enable();
            cameraControls.Enable();
        }

        /// <summary>
        /// summary: 每帧入口：在允许选中的状态下，Shift 按下走多选逻辑，否则走单选逻辑。
        /// param: 无
        /// return: 无
        /// </summary>
        private void Update()
        {
            if (!IsEnableSelection())
                return;

            if (IsMultiChoice())
                CheckMultiSelection();
            else
                CheckBuildingSelected();
        }

        /// <summary>
        /// summary: 判断当前是否允许选中（放置/拆除状态禁用）。
        /// param: 无
        /// return: true=允许选中，false=禁用
        /// </summary>
        private bool IsEnableSelection()
        {
            return !StatusController.HasStatus(StatusList.BuildingPlacementStatus) &&
                   !StatusController.HasStatus(StatusList.BuildingDestroyingStatus);
        }

        /// <summary>
        /// summary: 判断 Shift 是否按下（新 InputSystem）。
        /// param: 无
        /// return: true=Shift 按下
        /// </summary>
        private bool IsMultiChoice()
        {
            return buildingControls.Selection.Multi.IsPressed();
            // if (Keyboard.current == null) return false;
            // return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
        }

        /// <summary>
        /// summary: 单选逻辑：点击建筑=选中且清空其他；再次点击同一建筑=清空全部；点击空地=清空全部。
        /// param: 无
        /// return: 无
        /// </summary>
        public void CheckBuildingSelected()
        {
            if (mainCamera == null)
            {
                GameDebug.LogWarning("BuildingSelectionController: Main camera is not assigned.");
                return;
            }

            // 边沿触发，避免按住抖动
            if (!buildingControls.Selection.Confirm.WasPressedThisFrame())
                return;

            // 点到 UI 上就不处理
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Vector2 pointer = cameraControls.Camera.PointerPosition.ReadValue<Vector2>();
            Ray ray = mainCamera.ScreenPointToRay(pointer);

            if (Physics.Raycast(ray, out var hit, 5000f, buildingLayerMask, QueryTriggerInteraction.Collide))
            {
                if (!TryResolveBuildingFromHit(hit, out var hitRuntimeHost, out var hitView, out var hitId))
                    return;

                GameDebug.Log($"[Selection] Click building id: {hitId}, selectedCount: {_selectedHosts.Count}");
                Log.Info($"[BuildingSelectionController] Click building id: {hitId}, selectedCount: {_selectedHosts.Count}");

                // 如果当前只选中一个且就是它：再次点击 => 清空
                if (_selectedHosts.Count == 1 && _selectedHosts.ContainsKey(hitId))
                {
                    ClearSelection();
                    return;
                }

                // 否则：清空全部，再选中当前
                ClearSelection();

                SelectBuilding(hitId, hitRuntimeHost, hitView, publishEvent: true);
            }
            else
            {
                // 点击空地：清空全部
                if (_selectedHosts.Count > 0)
                    ClearSelection();
            }
        }

        /// <summary>
        /// summary: 多选逻辑（Shift）：点击建筑=加入/移除该建筑；点击空地=不改变当前选择。
        /// param: 无
        /// return: 无
        /// </summary>
        public void CheckMultiSelection()
        {
            if (mainCamera == null)
            {
                GameDebug.LogWarning("BuildingSelectionController: Main camera is not assigned.");
                return;
            }

            // 边沿触发，避免按住抖动
            if (!buildingControls.Selection.Confirm.WasPressedThisFrame())
                return;

            // 点到 UI 上就不处理
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Vector2 pointer = cameraControls.Camera.PointerPosition.ReadValue<Vector2>();
            Ray ray = mainCamera.ScreenPointToRay(pointer);

            if (Physics.Raycast(ray, out var hit, 5000f, buildingLayerMask, QueryTriggerInteraction.Collide))
            {
                if (!TryResolveBuildingFromHit(hit, out var hitRuntimeHost, out var hitView, out var hitId))
                    return;

                GameDebug.Log($"[MultiSelection] Click building id: {hitId}, selectedCount: {_selectedHosts.Count}");
                Log.Info($"[BuildingSelectionController] Multi click building id: {hitId}, selectedCount: {_selectedHosts.Count}");

                // 已选中 => 取消选中（需要对外发事件）
                if (_selectedHosts.ContainsKey(hitId))
                {
                    DeselectBuilding(hitId, publishEvent: true);

                    // 如果删完了，维持历史语义：发一个 runtime=null 的“无选中”事件
                    if (_selectedHosts.Count == 0)
                    {
                        Events.eventBus.Publish(new BuildingSelected
                        {
                            buildingRuntime = null,
                            isSelected = false
                        });
                    }

                    return;
                }

                // 未选中 => 加入选中（需要对外发事件）
                SelectBuilding(hitId, hitRuntimeHost, hitView, publishEvent: true);
            }
            else
            {
                // Shift + 点击空地：通常不清空，保留多选集
                //（如果你希望 Shift+空地也清空，把这里改为 ClearSelection() 即可）
            }
        }

        /// <summary>
        /// summary: 从 RaycastHit 解析出 BuildingRuntimeHost/BuildingView/BuildingID（更鲁棒：先 Parent 再 Children）。
        /// param: hit 射线命中信息
        /// param: runtimeHost 输出 runtimeHost
        /// param: view 输出 view
        /// param: buildingId 输出 buildingId
        /// return: true=解析成功
        /// </summary>
        private bool TryResolveBuildingFromHit(RaycastHit hit, out BuildingRuntimeHost runtimeHost, out BuildingView view, out long buildingId)
        {
            runtimeHost =
                hit.collider.GetComponentInParent<BuildingRuntimeHost>() ??
                hit.collider.GetComponentInChildren<BuildingRuntimeHost>();

            view =
                hit.collider.GetComponentInParent<BuildingView>() ??
                hit.collider.GetComponentInChildren<BuildingView>();

            if (runtimeHost == null || view == null)
            {
                runtimeHost = null;
                view = null;
                buildingId = -1;
                // 单选模式下点到奇怪 collider 会清空；多选模式按需求不清空（这里保持“什么都不做”更安全）
                return false;
            }

            if (runtimeHost.Runtime == null)
            {
                runtimeHost = null;
                view = null;
                buildingId = -1;
                return false;
            }

            buildingId = runtimeHost.Runtime.BuildingID;
            if (buildingId < 0)
                return false;

            return true;
        }

        /// <summary>
        /// summary: 选中一个建筑（仅管理 Selected Overlay，不改 BaseMode）。
        /// param: id 建筑ID
        /// param: host runtime host
        /// param: view building view
        /// param: publishEvent 是否发布 BuildingSelected 事件
        /// return: 无
        /// </summary>
        private void SelectBuilding(long id, BuildingRuntimeHost host, BuildingView view, bool publishEvent)
        {
            if (_selectedHosts.ContainsKey(id))
                return;

            _selectedHosts[id] = host;
            _selectedViews[id] = view;

            view.SetSelected(true);

            if (publishEvent)
            {
                Events.eventBus.Publish(new BuildingSelected
                {
                    buildingRuntime = host.Runtime,
                    isSelected = true
                });
            }
        }

        /// <summary>
        /// summary: 取消选中一个建筑（仅管理 Selected Overlay，不改 BaseMode）。
        /// param: id 建筑ID
        /// param: publishEvent 是否发布 BuildingSelected 事件（多选取消时需要）
        /// return: 无
        /// </summary>
        private void DeselectBuilding(long id, bool publishEvent)
        {
            if (_selectedViews.TryGetValue(id, out var view) && view != null)
                view.SetSelected(false);

            if (publishEvent && _selectedHosts.TryGetValue(id, out var host) && host != null && host.Runtime != null)
            {
                // 注意：这是“单个建筑被取消选中”的事件（用于 Shift 多选非常关键）
                Events.eventBus.Publish(new BuildingSelected
                {
                    buildingRuntime = host.Runtime,
                    isSelected = false
                });
            }

            _selectedViews.Remove(id);
            _selectedHosts.Remove(id);
        }

        /// <summary>
        /// summary: 清空所有选中（不发布“每个建筑的取消事件”，只保留历史语义：runtime=null, isSelected=false）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void ClearSelection()
        {
            if (_selectedViews.Count > 0)
            {
                foreach (var kv in _selectedViews)
                {
                    if (kv.Value != null)
                        kv.Value.SetSelected(false);
                }
            }

            _selectedViews.Clear();
            _selectedHosts.Clear();

            Events.eventBus.Publish(new BuildingSelected
            {
                buildingRuntime = null,
                isSelected = false
            });

            Log.Info("[BuildingSelectionController] Clear selection (all).");
        }
    }
}
