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
    public class ddd : MonoBehaviour
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
        public Color ghostColor = new Color(1f, 1f, 1f, 0.4f);
        public Color cannotPlaceColor = new Color(1f, 0.3f, 0.3f, 0.4f);

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
        [Min(0f)] public float buildingOverlapShrink = 0.02f;
        private BuildingControls buildingControls;
        private CameraControls cameraControls;

        private BuildingDef _currentDef;
        private GameObject _ghostInstance;
        private bool _isPlacing;
        private int _rotationSteps;

        private  List<Vector3Int> _tmpFootprint;
        private  MaterialPropertyBlock _mpb ;

        private void Awake()
        {
            buildingControls = InputActionManager.Instance.Building;
            cameraControls = InputActionManager.Instance.Camera;
            // buildingControls.Enable();
            // cameraControls.Enable();
            _mpb = new MaterialPropertyBlock();
            _tmpFootprint = new List<Vector3Int>();
            if (worldGrid == null) worldGrid = WorldGrid.Instance;
            if (occupancyMap == null) occupancyMap = OccupancyMap.Instance;
            if (navGrid == null) navGrid = NavGrid.Instance;

            if (groundMask.value == 0 && worldGrid != null)
                groundMask = worldGrid.groundMask;
        }

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

            GameDebug.Log($"[BuildingPlacement] 开始放置建筑：{_currentDef.Id} ({_currentDef.Name})");

            // 加载 prefab（用于 ghost 预览）
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

            // 初始颜色
            SetGhostColor(ghostColor);

            // ghost 不需要逻辑
            var host = _ghostInstance.GetComponent<BuildingRuntimeHost>();
            if (host != null) host.enabled = false;

            _isPlacing = true;
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

            bool canPlace = CheckCanPlace(anchorCell, out float avgHeight);

            // 更新 ghost pose
            Vector3 pos = worldGrid != null ? worldGrid.CellToWorldCenterXZ(anchorCell) : new Vector3(anchorCell.x, 0f, anchorCell.y);
            pos.y = (useAverageHeight ? avgHeight : hitPoint.y) + placementYOffset;

            Quaternion rot = Quaternion.Euler(0f, _rotationSteps * 90f, 0f);

            _ghostInstance.transform.SetPositionAndRotation(pos, rot);
            SetGhostColor(canPlace ? ghostColor : cannotPlaceColor);

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
            if (buildingControls.Rotation.Left.IsPressed())
                _rotationSteps = (_rotationSteps + 3) % 4;

            if (buildingControls.Rotation.Right.IsPressed())
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

                if (usePhysicsObstacleCheck && HasObstacleAtCell(c, h))
                    return false;
            }

            // 平坦度
            if (maxH - minH > maxFootprintHeightDelta)
                return false;

            avgHeight = sumH / _tmpFootprint.Count;
            if (useBuildingOverlapCheck && HasBuildingOverlap(anchorCell, avgHeight, _rotationSteps))
                return false;
            return true;
        }

        /// <summary>
        /// summary: 可选的物理障碍检测（用于非网格对象，如场景道具）。
        /// param: cell 格子坐标
        /// param: groundHeight 该格地面高度
        /// return: 是否存在障碍
        /// </summary>
        private bool HasObstacleAtCell(Vector3Int cell, float groundHeight)
        {
            if (worldGrid == null) return false;

            Vector3 center = worldGrid.CellToWorldCenterXZ(cell);
            center.y = groundHeight + obstacleCheckYOffset;

            float cs = worldGrid.cellSize;
            Vector3 halfExtents = new Vector3(cs * 0.45f, obstacleCheckHeight * 0.5f, cs * 0.45f);

            return Physics.CheckBox(center, halfExtents, Quaternion.identity, obstacleLayerMask, QueryTriggerInteraction.Ignore);
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

            var runtimeHost = go.GetComponent<BuildingRuntimeHost>();
            if (runtimeHost != null)
            {
                // 写入 placement（供拆除/存档使用）
                runtimeHost.SetPlacement(anchorCell, (byte)_rotationSteps);

                // 保留你原先的 DefId 写法
                if (runtimeHost.Runtime != null && runtimeHost.Runtime.Def != null)
                    runtimeHost.Runtime.Def.Id = _currentDef.Id;
            }

            // 更新 Occupancy（阻挡）
            if (occupancyMap != null)
                occupancyMap.UpdateAreaBlocked(anchorCell, _currentDef.Width, _currentDef.Height, _rotationSteps, true);
            else
                OccupancyMap.Instance?.UpdateAreaBlocked(anchorCell, _currentDef.Width, _currentDef.Height, _rotationSteps, true);

            //添加建筑的LayerMask
            go.layer = LayerMask.NameToLayer("Building");
            foreach (Transform child in go.transform)
            {
                child.gameObject.layer = LayerMask.NameToLayer("Building");
            }
            // 放置一次后退出放置（你也可以改成连续放置模式）
            CancelPlacement();
        }

        /// <summary>
        /// summary: 修改 ghost 颜色（兼容 Renderer 与 SpriteRenderer）。
        /// param: c 颜色
        /// return: 无
        /// </summary>
        private void SetGhostColor(Color c)
        {
            if (_ghostInstance == null) return;

            foreach (var sr in _ghostInstance.GetComponentsInChildren<SpriteRenderer>(true))
                sr.color = c;

            foreach (var r in _ghostInstance.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.sharedMaterial == null) continue;

                r.GetPropertyBlock(_mpb);

                if (r.sharedMaterial.HasProperty("_BaseColor"))
                    _mpb.SetColor("_BaseColor", c);

                if (r.sharedMaterial.HasProperty("_Color"))
                    _mpb.SetColor("_Color", c);

                r.SetPropertyBlock(_mpb);
            }
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
            _currentDef = null;
            _isPlacing = false;
        }
    }
}
