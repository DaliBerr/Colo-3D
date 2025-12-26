using Lonize.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using Kernel.Pool;
using Kernel.GameState;
using Kernel.Nav;
using Kernel.World;
using Lonize;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 建筑拆除控制器（3D 版）：点击建筑回收/销毁，并释放 Occupancy 阻挡。
    /// </summary>
    public class BuildingDestroyingController : MonoBehaviour
    {
        [Header("基本引用")]
        public Camera mainCamera;
        public LayerMask buildingLayerMask;

        [Header("World/Grid")]
        public WorldGrid worldGrid;
        public OccupancyMap occupancyMap;

        [Header("输入")]
        private BuildingControls buildingControls;

        private bool _isRemoving = false;


        
        // 当前鼠标下方“被 ghost 预览”的建筑缓存
        private BuildingView _hoverGhostView;
        private BuildingRuntimeHost _hoverGhostHost;
        private long _hoverGhostId = -1;
        private BuildingViewBaseMode _hoverGhostPrevBaseMode = BuildingViewBaseMode.Normal;



        private void Awake()
        {
            buildingControls = InputActionManager.Instance.Building;


            if (worldGrid == null) worldGrid = WorldGrid.Instance;
            if (occupancyMap == null) occupancyMap = OccupancyMap.Instance;
        }

        /// <summary>
        /// summary: UI 调用：进入拆除模式。
        /// param: 无
        /// return: 无
        /// </summary>
        public void StartRemoveMode()
        {
            if (!StatusController.AddStatus(StatusList.BuildingDestroyingStatus))
            {
                GameDebug.LogWarning("[BuildingRemove] 无法进入拆除模式，已有其他状态阻塞。");
                return;
            }
            _isRemoving = true;
            GameDebug.Log("[BuildingRemove] 进入拆除模式。");
            Log.Info("[BuildingRemove] 进入拆除模式。");
        }

        /// <summary>
        /// summary: UI 调用：退出拆除模式。
        /// param: 无
        /// return: 无
        /// </summary>
        public void StopRemoveMode()
        {
            StatusController.RemoveStatus(StatusList.BuildingDestroyingStatus);
            ClearHoverGhost();
            _isRemoving = false;
            GameDebug.Log("[BuildingRemove] 退出拆除模式。");
            Log.Info("[BuildingRemove] 退出拆除模式。");
        }

        private void Update()
        {
            if (!_isRemoving) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (buildingControls.Removal.Cancel.IsPressed() || buildingControls.Placement.Cancel.IsPressed())
            {
                StopRemoveMode();
                return;
            }
            TryGhostBuildingUnderMouse();
            if (buildingControls.Removal.Confirm.IsPressed())
                TryRemoveBuildingUnderMouse();
        }
        private void TryGhostBuildingUnderMouse()
        {
            if (mainCamera == null)
                return;

            // 鼠标在 UI 上：不做建筑预览，并恢复之前的 ghost
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                ClearHoverGhost();
                return;
            }

            // 读取指针位置（与 SelectionController 一致的写法）
            Vector2 pointer = buildingControls.Removal.MousePos.ReadValue<Vector2>();
            Ray ray = mainCamera.ScreenPointToRay(pointer);

            if (!Physics.Raycast(ray, out var hit, 5000f, buildingLayerMask, QueryTriggerInteraction.Collide))
            {
                ClearHoverGhost();
                return;
            }
            // GameDebug.Log("collider:" + hit.collider.name);
            // collider 往上找更稳（常见 collider 在子节点）
            var host =
                hit.collider.GetComponentInParent<BuildingRuntimeHost>() ??
                hit.collider.GetComponentInChildren<BuildingRuntimeHost>();

            var view =
                hit.collider.GetComponentInParent<BuildingView>() ??
                hit.collider.GetComponentInChildren<BuildingView>();

            if (host == null || view == null || host.Runtime == null)
            {
                // GameDebug.LogWarning("[BuildingRemove] 鼠标下的对象不包含 BuildingRuntimeHost 或 BuildingView，无法预览。");
                ClearHoverGhost();
                return;
            }

            long id = host.Runtime.BuildingID;
            if (id < 0)
            {
                // GameDebug.LogWarning("[BuildingRemove] 鼠标下的建筑 ID 无效，无法预览。");
                ClearHoverGhost();
                return;
            }

            // 命中同一个建筑：不重复设置，避免每帧刷状态
            if (_hoverGhostId == id && _hoverGhostView == view)
                return;

            // 命中切换：先恢复旧的，再应用新的
            ClearHoverGhost();

            _hoverGhostId = id;
            _hoverGhostHost = host;
            _hoverGhostView = view;

            // 保存旧的 base mode，移开鼠标时能恢复（不覆盖 Disabled 等状态）
            _hoverGhostPrevBaseMode = _hoverGhostView.GetBaseMode();

            // 应用 ghost 显示（移除预览一般不需要 blocked；如果你想红色可改为 SetGhostBlocked(true)）
            _hoverGhostView.SetBaseMode(BuildingViewBaseMode.Ghost);
            _hoverGhostView.SetGhostBlocked(true);
        }
        /// <summary>
        /// summary: 在鼠标位置发射 3D 射线，找到建筑并移除。
        /// param: 无
        /// return: 无
        /// </summary>
        private void TryRemoveBuildingUnderMouse()
        {
            if (mainCamera == null)
            {
                GameDebug.LogError("[BuildingRemove] mainCamera 未设置。");
                return;
            }

            Vector2 pointer = buildingControls.Removal.MousePos.ReadValue<Vector2>();
            Ray ray = mainCamera.ScreenPointToRay(pointer);

            if (!Physics.Raycast(ray, out var hit, 5000f, buildingLayerMask, QueryTriggerInteraction.Ignore))
            {
                GameDebug.Log("[BuildingRemove] 点击处没有检测到建筑。");
                return;
            }
            GameDebug.Log("collider:" + hit.collider.name);
            var host = hit.collider.GetComponentInChildren<BuildingRuntimeHost>();
            if (host == null)
            {
                host = hit.collider.GetComponent<BuildingRuntimeHost>();
                if (host == null)
                {
                    host = hit.collider.GetComponentInChildren<BuildingRuntimeHost>();
                    if(host == null)
                    {
                        GameDebug.LogWarning("[BuildingRemove] 点击到的对象不包含 BuildingRuntimeHost，放弃拆除。");
                        return;
                    }
                }
            }

            RemoveBuilding(host);
        }

        /// <summary>
        /// summary: 移除建筑：释放 Occupancy + 尝试回收到对象池，否则销毁。
        /// param: host 建筑宿主
        /// return: 无
        /// </summary>
        private void RemoveBuilding(BuildingRuntimeHost host)
        {
            if (host == null) return;

            GameObject buildingGo = host.gameObject;

            TryReleaseOccupancyArea(host);

            var poolMember = buildingGo.GetComponent<BuildingPoolMember>();
            if (PoolManager.Instance != null && poolMember != null)
                PoolManager.Instance.ReturnToPool(buildingGo);
            else
                Destroy(buildingGo);
        }

        /// <summary>
        /// summary: 释放该建筑占用的阻挡区域（优先用 Host 记录的 AnchorCell/RotSteps）。
        /// param: host 建筑宿主
        /// return: 无
        /// </summary>
        private void TryReleaseOccupancyArea(BuildingRuntimeHost host)
        {
            var occ = occupancyMap != null ? occupancyMap : OccupancyMap.Instance;
            if (occ == null) return;

            var def = host.Runtime?.Def;
            int width = def?.Width ?? 1;
            int height = def?.Height ?? 1;

            Vector3Int anchorCell;
            byte rotSteps;

            if (!host.TryGetPlacement(out anchorCell, out rotSteps))
            {
                // 兜底：从 transform 反推
                if (worldGrid != null)
                    anchorCell = worldGrid.WorldToCellXZ(host.transform.position);
                else
                    anchorCell = new Vector3Int(Mathf.RoundToInt(host.transform.position.x), Mathf.RoundToInt(host.transform.position.z), 0);

                rotSteps = (byte)(Mathf.RoundToInt(host.transform.eulerAngles.y / 90f) & 3);
            }

            occ.UpdateAreaBlocked(anchorCell, width, height, rotSteps, false);
        }

                /// <summary>
        /// summary: 清理当前鼠标悬停 ghost 预览，并恢复建筑原本的 base mode。
        /// param: 无
        /// return: 无
        /// </summary>
        private void ClearHoverGhost()
        {
            if (_hoverGhostView != null)
            {
                // 恢复原 base mode（不动 Selected overlay）
                _hoverGhostView.SetBaseMode(_hoverGhostPrevBaseMode);
            }

            _hoverGhostId = -1;
            _hoverGhostHost = null;
            _hoverGhostView = null;
            _hoverGhostPrevBaseMode = BuildingViewBaseMode.Normal;
        }

    }
}
