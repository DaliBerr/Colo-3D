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
        private CameraControls cameraControls;

        private bool _isRemoving = false;

        private void Awake()
        {
            buildingControls = new BuildingControls();
            cameraControls = new CameraControls();
            buildingControls.Enable();
            cameraControls.Enable();

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

            if (buildingControls.Removal.Confirm.IsPressed())
                TryRemoveBuildingUnderMouse();
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

            Vector2 pointer = cameraControls.Camera.PointerPosition.ReadValue<Vector2>();
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
    }
}
