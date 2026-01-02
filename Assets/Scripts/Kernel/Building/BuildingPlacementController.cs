using System.Collections.Generic;
using Lonize.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using Kernel.Nav;
using Kernel.Pool;
using Kernel.GameState;
using Lonize;
using Kernel.World;

namespace Kernel.Building
{
    /// <summary>
    /// summary: 3D 建筑放置控制器（基于 WorldGrid + OccupancyMap）。
    /// </summary>
    public class BuildingPlacementController : MonoBehaviour
    {
        [Header("基本引用")]
        public Camera mainCamera;
        public Transform buildingRoot;

        [Header("World/Grid")]
        public WorldGrid worldGrid;
        public OccupancyMap occupancyMap;
        public LayerMask groundMask;

        [Header("Physics Fallback（可选）")]
        public bool usePhysicsObstacleCheck = false;
        public LayerMask obstacleLayerMask;
        [Min(0.01f)] public float obstacleCheckHeight = 2f;
        [Min(0f)] public float obstacleCheckYOffset = 0.5f;

        [Header("导航网格（兼容旧代码）")]
        public NavGrid navGrid;

        [Header("BuildingDef 配置")]
        public string[] buildingIds;

        [Header("虚影参数")]
        public Color ghostOkColor = new Color(1f, 1f, 1f, 0.4f);
        public Color ghostBlockedColor = new Color(1f, 0.3f, 0.3f, 0.4f);

        [Header("放置规则")]
        [Min(0f)] public float maxFootprintHeightDelta = 1.2f; // footprint 最大高差（单位：米）
        public bool useAverageHeight = true;
        [Min(0f)] public float placementYOffset = 0f;

        [Header("旋转设置")]
        public bool enableRotate = true;

        [Header("Building Overlap Check（检测与其他建筑Collider重叠）")]
        public bool useBuildingOverlapCheck = true;
        public LayerMask buildingOverlapMask; // 设为 Building
        [Min(0.01f)] public float buildingOverlapHeight = 3f;
        [Min(-1f)] public float buildingOverlapShrink = 0.02f;

        private BuildingControls buildingControls;
        private CameraControls cameraControls;

        private BuildingDef _currentDef;
        private GameObject _ghostInstance;
        private BuildingView _ghostView;
        private bool _isPlacing;
        private int _rotationSteps;

        private List<Vector3Int> _tmpFootprint;

        /// <summary>
        /// summary: Unity 生命周期入口，初始化输入与依赖引用。
        /// param: 无
        /// return: 无
        /// </summary>
        private void Awake()
        {
            buildingControls = InputActionManager.Instance.Building;
            cameraControls = InputActionManager.Instance.Camera;
            // buildingControls.Enable();
            // cameraControls.Enable();

            _tmpFootprint = new List<Vector3Int>();

            if (worldGrid == null) worldGrid = WorldGrid.Instance;
            if (occupancyMap == null) occupancyMap = OccupancyMap.Instance;
            if (navGrid == null) navGrid = NavGrid.Instance;

            if (groundMask.value == 0 && worldGrid != null)
                groundMask = worldGrid.groundMask;
        }

        /// <summary>
        /// summary: Unity 每帧入口：若处于放置模式则更新 ghost 与输入。
        /// param: 无
        /// return: 无
        /// </summary>
        private void Update()
        {
            if (_isPlacing)
                HandlePlacementUpdate();
        }

        /// <summary>
        /// summary: UI 入口：按 index 启动放置。
        /// param: index buildingIds 下标
        /// return: 无
        /// </summary>
        public async void StartPlacementByIndex(int index)
        {
            if (buildingIds == null || index < 0 || index >= buildingIds.Length)
            {
                GameDebug.LogWarning("[BuildingPlacement] Building index out of range.");
                return;
            }

            await StartPlacementById(buildingIds[index]);
        }

        /// <summary>
        /// summary: 主入口：按 BuildingDef.Id 启动放置。
        /// param: buildingId 建筑 DefId
        /// return: Task
        /// </summary>
        public async System.Threading.Tasks.Task StartPlacementById(string buildingId)
        {
            if (!StatusController.AddStatus(StatusList.BuildingPlacementStatus))
            {
                GameDebug.LogWarning("[BuildingPlacement] 无法进入放置模式，已有其他状态阻塞。");
                return;
            }

            // 清理旧虚影
            if (_ghostInstance != null)
            {
                Destroy(_ghostInstance);
                _ghostInstance = null;
                _ghostView = null;
            }

            _rotationSteps = 0;
            _currentDef = null;
            _isPlacing = false;

            if (!BuildingDatabase.TryGet(buildingId, out _currentDef))
            {
                GameDebug.LogError($"[BuildingPlacement] 未找到 BuildingDef: {buildingId}");
                CancelPlacement();
                return;
            }

            if (_currentDef.Category == BuildingCategory.Internal)
            {
                GameDebug.LogWarning($"[BuildingPlacement] 内部建筑不可直接放置: {buildingId}");
                CancelPlacement();
                return;
            }

            GameDebug.Log($"[BuildingPlacement] 开始放置建筑：{_currentDef.Id} ({_currentDef.Name})");

            // 加载 prefab（用于 ghost 预览）
            if (string.IsNullOrWhiteSpace(_currentDef.PrefabAddress))
            {
                GameDebug.LogError($"[BuildingPlacement] BuildingDef 缺少 PrefabAddress: {_currentDef.Id}");
                CancelPlacement();
                return;
            }

            var prefab = await AddressableRef.LoadAsync<GameObject>(_currentDef.PrefabAddress);
            if (prefab == null)
            {
                GameDebug.LogError($"[BuildingPlacement] 无法加载 Prefab: {_currentDef.PrefabAddress}");
                CancelPlacement();
                return;
            }

            _ghostInstance = Instantiate(prefab, Vector3.zero, Quaternion.identity, buildingRoot);
            _ghostInstance.name = _currentDef.Id + "_Ghost";

            // 禁用 ghost 上的 3D 碰撞器
            foreach (var col in _ghostInstance.GetComponentsInChildren<Collider>(true))
                col.enabled = false;

            // 初始化 ghost 预览显示（统一接入 BuildingView）
            SetupGhostViewForPlacement();

            // ghost 不需要逻辑
            var host = _ghostInstance.GetComponent<BuildingRuntimeHost>();
            if (host != null) host.enabled = false;

            _isPlacing = true;
        }

        /// <summary>
        /// summary: 初始化放置用的 ghost BuildingView（设置为 Ghost 模式，并覆盖红/绿颜色）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void SetupGhostViewForPlacement()
        {
            _ghostView = null;

            if (_ghostInstance == null)
                return;

            // prefab 自带则直接复用；否则运行时补一个
            _ghostView = _ghostInstance.GetComponentInChildren<BuildingView>(true);
            if (_ghostView == null)
                _ghostView = _ghostInstance.AddComponent<BuildingView>();

            // 放置预览不应显示选中
            _ghostView.SetSelected(false);

            // 覆盖放置预览的 ghost 颜色（保持与 PlacementController 的配置一致）
            _ghostView.OverrideGhostColors(ghostOkColor, ghostBlockedColor);

            // 进入 ghost 模式：默认先按“可放置”显示
            _ghostView.SetBaseMode(BuildingViewBaseMode.Ghost);
            _ghostView.SetGhostBlocked(false);
        }

        /// <summary>
        /// summary: 每帧放置更新（raycast 地面 -> 计算 anchor -> 更新 ghost -> 点击放置）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void HandlePlacementUpdate()
        {
            if (_currentDef == null || _ghostInstance == null) return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            if (enableRotate)
                HandleRotateInput();

            if (!TryGetAnchorFromCursor(out var anchorCell, out var hitPoint))
                return;
                
            float previewAvgHeight = hitPoint.y;
            if (useAverageHeight)
                TryComputePreviewAverageHeight(anchorCell, hitPoint.y, out previewAvgHeight);

            // ✅ 再做规则判定
            bool canPlace = CheckCanPlace(anchorCell, out _);

            // 更新 ghost pose（用 previewAvgHeight，保证不被提前 false 影响）
            Vector3 pos = worldGrid != null ? worldGrid.CellToWorldCenterXZ(anchorCell) : new Vector3(anchorCell.x, 0f, anchorCell.y);
            pos.y = (useAverageHeight ? previewAvgHeight : hitPoint.y) + placementYOffset;
                        Quaternion rot = Quaternion.Euler(0f, _rotationSteps * 90f, 0f);

            _ghostInstance.transform.SetPositionAndRotation(pos, rot);

            // 统一走 BuildingView：blocked=true 表示不可放置（红）
            if (_ghostView != null)
                _ghostView.SetGhostBlocked(!canPlace);

            // 左键放置
            if (buildingControls.Placement.Confirm.IsPressed())
            {
                if (canPlace)
                    _ = PlaceBuildingAsync(anchorCell, pos, rot);
                else
                    GameDebug.Log("[BuildingPlacement] 当前位置不可放置。");
            }

            // 右键取消
            if (buildingControls.Placement.Cancel.IsPressed())
                CancelPlacement();
        }

        /// <summary>
        /// summary: 旋转输入（Q/E）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void HandleRotateInput()
        {
            if (buildingControls.Rotation.Left.WasPressedThisFrame())
                _rotationSteps = (_rotationSteps + 3) % 4;

            if (buildingControls.Rotation.Right.WasPressedThisFrame())
                _rotationSteps = (_rotationSteps + 1) % 4;
        }

        /// <summary>
        /// summary: 从鼠标位置 raycast 地面，得到 anchor cell。
        /// param: anchorCell 输出锚点格
        /// param: hitPoint 输出命中点
        /// return: 是否成功
        /// </summary>
        private bool TryGetAnchorFromCursor(out Vector3Int anchorCell, out Vector3 hitPoint)
        {
            anchorCell = default;
            hitPoint = default;

            if (mainCamera == null || worldGrid == null)
                return false;

            Vector2 pointer = cameraControls.Camera.PointerPosition.ReadValue<Vector2>();
            Ray ray = mainCamera.ScreenPointToRay(pointer);

            if (!Physics.Raycast(ray, out var hit, 5000f, groundMask, QueryTriggerInteraction.Ignore))
                return false;

            hitPoint = hit.point;
            anchorCell = worldGrid.WorldToCellXZ(hit.point);
            return true;
        }

        /// <summary>
        /// summary: 检查 anchorCell 是否可放置（buildable + tag + occupancy + 平坦度 + 可选物理障碍）。
        /// param: anchorCell 锚点格
        /// param: avgHeight 输出 footprint 平均高度
        /// return: 是否可放置
        /// </summary>
        private bool CheckCanPlace(Vector3Int anchorCell, out float avgHeight)
        {
            avgHeight = 0f;

            if (_currentDef == null || worldGrid == null)
                return false;

            var occ = occupancyMap != null ? occupancyMap : OccupancyMap.Instance;
            if (occ == null)
                return false;

            _tmpFootprint.Clear();
            _tmpFootprint.AddRange(occ.GetFootprintCells(anchorCell, _currentDef.Width, _currentDef.Height, _rotationSteps));
            if (_tmpFootprint.Count == 0)
                return false;

            float minH = float.MaxValue;
            float maxH = float.MinValue;
            float sumH = 0f;

            for (int i = 0; i < _tmpFootprint.Count; i++)
            {
                var c = _tmpFootprint[i];

                if (!worldGrid.IsCellValid(c))
                    return false;

                if (!worldGrid.IsCellBuildable(c))
                    return false;

                if (!worldGrid.CheckCellTags(c, _currentDef.PlacementRequiredTags, _currentDef.PlacementForbiddenTags))
                    return false;

                if (occ.IsCellBlocked(c))
                    return false;

                if (!worldGrid.TryGetGroundAtCell(c, out float h, out _, out _))
                    return false;

                minH = Mathf.Min(minH, h);
                maxH = Mathf.Max(maxH, h);
                sumH += h;
            }

            // ✅ 关键：先把 avgHeight 算出来（即使失败也能用于 ghost Y）
            avgHeight = sumH / _tmpFootprint.Count;

            // ✅ 再做高度差判定（失败则返回 false，但 avgHeight 已经有值了）
            if (maxH - minH > maxFootprintHeightDelta)
                return false;

            avgHeight = sumH / _tmpFootprint.Count;

            // 可选：物理障碍检测（例如树/岩石等）
            if (usePhysicsObstacleCheck)
            {
                if (HasPhysicsObstacle(anchorCell, avgHeight, _rotationSteps))
                    return false;
            }

            // 可选：检测与其他建筑 Collider 发生重叠
            if (useBuildingOverlapCheck)
            {
                if (HasBuildingOverlap(anchorCell, avgHeight, _rotationSteps))
                    return false;
            }

            return true;
        }
        /// <summary>
        /// summary: 计算放置预览用的平均高度（只做地形采样，不做可放置规则判定）；采样失败则回退到 hitY。
        /// param: anchorCell 锚点格
        /// param: hitY 光标射线命中点的 y（作为回退值）
        /// param: avgHeight 输出：预览高度
        /// return: 是否成功从 footprint 采样到至少一个有效高度
        /// </summary>
        private bool TryComputePreviewAverageHeight(Vector3Int anchorCell, float hitY, out float avgHeight)
        {
            avgHeight = hitY;

            if (_currentDef == null || worldGrid == null)
                return false;

            var occ = occupancyMap != null ? occupancyMap : OccupancyMap.Instance;
            if (occ == null)
                return false;

            _tmpFootprint.Clear();
            _tmpFootprint.AddRange(occ.GetFootprintCells(anchorCell, _currentDef.Width, _currentDef.Height, _rotationSteps));
            if (_tmpFootprint.Count == 0)
                return false;

            float sum = 0f;
            int count = 0;

            for (int i = 0; i < _tmpFootprint.Count; i++)
            {
                var c = _tmpFootprint[i];

                // 预览高度：无论能不能放，都尽量采到地形高度
                if (!worldGrid.IsCellValid(c))
                    continue;

                if (!worldGrid.TryGetGroundAtCell(c, out float h, out _, out _))
                    continue;

                sum += h;
                count++;
            }

            if (count <= 0)
                return false;

            avgHeight = sum / count;
            return true;
        }
        /// <summary>
        /// summary: 检测 footprint 区域是否存在物理障碍物（OverlapBox）。
        /// param: anchorCell 锚点格
        /// param: avgHeight footprint 平均高度
        /// param: rotationSteps 旋转步数
        /// return: true=有障碍（不可放置），false=无
        /// </summary>
        private bool HasPhysicsObstacle(Vector3Int anchorCell, float avgHeight, int rotationSteps)
        {
            if (worldGrid == null || _currentDef == null) return false;

            int rot = ((rotationSteps % 4) + 4) % 4;
            int realW = (rot % 2 == 1) ? _currentDef.Height : _currentDef.Width;
            int realH = (rot % 2 == 1) ? _currentDef.Width : _currentDef.Height;

            float cs = worldGrid.cellSize;

            Vector3Int start = anchorCell - new Vector3Int(realW / 2, realH / 2, 0);

            Vector3 startWorld = worldGrid.CellToWorldCenterXZ(start);
            Vector3 center = startWorld + new Vector3((realW - 1) * cs * 0.5f, 0f, (realH - 1) * cs * 0.5f);
            center.y = avgHeight + obstacleCheckYOffset + obstacleCheckHeight * 0.5f;

            Vector3 halfExtents = new Vector3(realW * cs * 0.5f,
                                            obstacleCheckHeight * 0.5f,
                                            realH * cs * 0.5f);

            Quaternion rotQ = Quaternion.Euler(0f, rot * 90f, 0f);

            var hits = Physics.OverlapBox(center, halfExtents, rotQ, obstacleLayerMask, QueryTriggerInteraction.Ignore);
            return hits != null && hits.Length > 0;
        }

        /// <summary>
        /// summary: 真正放置建筑（从对象池获取并写入 placement + 更新 Occupancy）。
        /// param: anchorCell 锚点格
        /// param: worldPos 放置世界坐标
        /// param: rot 放置旋转
        /// return: Task
        /// </summary>
        private async System.Threading.Tasks.Task PlaceBuildingAsync(Vector3Int anchorCell, Vector3 worldPos, Quaternion rot)
        {
            if (_currentDef == null) return;

            if (!CheckCanPlace(anchorCell, out _))
                return;

            if (PoolManager.Instance == null)
            {
                GameDebug.LogError("[BuildingPlacement] PoolManager.Instance 为空，无法生成建筑。");
                return;
            }

            var go = await PoolManager.Instance.GetAsync(_currentDef.Id, worldPos, rot);
            if (go == null)
            {
                GameDebug.LogError("[BuildingPlacement] PoolManager.GetAsync 失败。");
                return;
            }

            if (buildingRoot != null)
                go.transform.SetParent(buildingRoot, true);

            go.transform.SetPositionAndRotation(worldPos, rot);

            var runtimeHost = go.GetComponentInChildren<BuildingRuntimeHost>();
            if (runtimeHost != null)
            {
                GameDebug.Log($"[BuildingPlacement] 放置建筑实例：{_currentDef.Id} at {anchorCell}");
                // 写入 placement（供拆除/存档使用）
                runtimeHost.SetPlacement(anchorCell, (byte)_rotationSteps);

                // 保留你原先的 DefId 写法
                if (runtimeHost.Runtime != null && runtimeHost.Runtime.Def != null)
                    runtimeHost.Runtime.Def.Id = _currentDef.Id;
            }
            else
            {
                GameDebug.LogWarning("[BuildingPlacement] 放置的建筑缺少 BuildingRuntimeHost 组件。");
            }

            // 更新 Occupancy（阻挡）
            if (occupancyMap != null)
                occupancyMap.UpdateAreaBlocked(anchorCell, _currentDef.Width, _currentDef.Height, _rotationSteps, true);
            else
                OccupancyMap.Instance?.UpdateAreaBlocked(anchorCell, _currentDef.Width, _currentDef.Height, _rotationSteps, true);

            // 添加建筑的 LayerMask
            go.layer = LayerMask.NameToLayer("Building");
            foreach (Transform child in go.transform)
            {
                child.gameObject.layer = LayerMask.NameToLayer("Building");
            }

            // 放置一次后退出放置（你也可以改成连续放置模式）
            // CancelPlacement();
        }

        /// <summary>
        /// summary: 检测当前建筑在该 anchorCell/rot 下的整体体积是否与已存在建筑 Collider 重叠。
        /// param: anchorCell 锚点格
        /// param: avgHeight footprint 平均高度
        /// param: rotationSteps 旋转步数（0..3）
        /// return: true=发生重叠（不可放置），false=无重叠
        /// </summary>
        private bool HasBuildingOverlap(Vector3Int anchorCell, float avgHeight, int rotationSteps)
        {
            if (worldGrid == null || _currentDef == null) return false;

            int rot = ((rotationSteps % 4) + 4) % 4;
            int realW = (rot % 2 == 1) ? _currentDef.Height : _currentDef.Width;
            int realH = (rot % 2 == 1) ? _currentDef.Width : _currentDef.Height;

            float cs = worldGrid.cellSize;

            // 计算 footprint 左下角起点（与 OccupancyMap 的中心锚逻辑一致）
            Vector3Int start = anchorCell - new Vector3Int(realW / 2, realH / 2, 0);

            // 用“起点格中心 + (w-1)/2 格偏移”得到矩形中心（兼容偶数宽高）
            Vector3 startWorld = worldGrid.CellToWorldCenterXZ(start);
            Vector3 center = startWorld + new Vector3((realW - 1) * cs * 0.5f, 0f, (realH - 1) * cs * 0.5f);
            center.y = avgHeight + buildingOverlapHeight * 0.5f;

            Vector3 halfExtents = new Vector3(realW * cs * 0.5f - buildingOverlapShrink,
                                            buildingOverlapHeight * 0.5f,
                                            realH * cs * 0.5f - buildingOverlapShrink);

            Quaternion rotQ = Quaternion.Euler(0f, rot * 90f, 0f);

            var hits = Physics.OverlapBox(center, halfExtents, rotQ, buildingOverlapMask, QueryTriggerInteraction.Collide);
            return hits != null && hits.Length > 0;
        }

        /// <summary>
        /// summary: 取消放置并清理 ghost。
        /// param: 无
        /// return: 无
        /// </summary>
        public void CancelPlacement()
        {
            StatusController.RemoveStatus(StatusList.BuildingPlacementStatus);
            if (_ghostInstance != null)
                Destroy(_ghostInstance);

            _ghostInstance = null;
            _ghostView = null;
            _currentDef = null;
            _isPlacing = false;
        }
    }
}
