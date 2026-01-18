using Kernel.GameState;
using Lonize;
using Lonize.Logging;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Kernel.World
{
    /// <summary>
    /// summary: Chunk 选中控制器：点击地面输出所在 Chunk 的信息。
    /// param: 无
    /// return: 无
    /// </summary>
    public class ChunkSelectionController : MonoBehaviour
    {
        [Header("Refs")]
        public Camera mainCamera;
        public WorldGrid worldGrid;
        public WorldChunkMeshGenerator worldChunkMeshGenerator;

        [Header("Raycast")]
        public LayerMask groundLayerMask;
        [Min(1f)] public float rayDistance = 5000f;

        private MapControls mapControls;

        /// <summary>
        /// summary: 初始化输入映射并启用。
        /// param: 无
        /// return: 无
        /// </summary>
        private void Awake()
        {
            mapControls = InputActionManager.Instance != null ? InputActionManager.Instance.Map : null;
            mapControls?.Enable();
        }

        /// <summary>
        /// summary: 每帧入口：监听点击并输出 Chunk 信息。
        /// param: 无
        /// return: 无
        /// </summary>
        private void Update()
        {
            if (!IsEnableSelection())
                return;

            CheckChunkSelected();
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
        /// summary: 判断是否按下选择输入（鼠标左键边沿触发）。
        /// param: 无
        /// return: true=按下
        /// </summary>
        private bool IsSelectionPressed()
        {
            return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        }

        /// <summary>
        /// summary: 点击地面后输出所在 Chunk 的信息。
        /// param: 无
        /// return: 无
        /// </summary>
        private void CheckChunkSelected()
        {
            if (mainCamera == null || worldGrid == null)
                return;

            if (!IsSelectionPressed())
                return;

            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            Vector2 pointer = mapControls != null
                ? mapControls.OverlayClick.Position.ReadValue<Vector2>()
                : (Vector2)Input.mousePosition;

            Ray ray = mainCamera.ScreenPointToRay(pointer);
            if (!Physics.Raycast(ray, out var hit, rayDistance, groundLayerMask, QueryTriggerInteraction.Ignore))
                return;

            if (!TryResolveChunkFromHit(hit.point, out var chunkCoord, out var cellCoord))
                return;

            var generator = worldChunkMeshGenerator != null ? worldChunkMeshGenerator : WorldChunkMeshGenerator.Instance;
            if (generator != null && generator.TryGetChunkMineralInfo(chunkCoord, out var mineralInfo))
            {
                GameDebug.Log($"[ChunkSelection] Click cell=({cellCoord.x},{cellCoord.y}) chunk=({chunkCoord.x},{chunkCoord.y}) mineralCount={mineralInfo.MineralComposition?.Count ?? 0}");
                Log.Info($"[ChunkSelectionController] Click cell=({cellCoord.x},{cellCoord.y}) chunk=({chunkCoord.x},{chunkCoord.y}) mineralCount={mineralInfo.MineralComposition?.Count ?? 0}");
                foreach (var kvp in mineralInfo.MineralComposition)
                {
                    GameDebug.Log($"    Mineral '{kvp.Key}': {kvp.Value}");
                    // Log.Info($"    Mineral '{kvp.Key}': {kvp.Value}");
                }
                return;
            }

            GameDebug.Log($"[ChunkSelection] Click cell=({cellCoord.x},{cellCoord.y}) chunk=({chunkCoord.x},{chunkCoord.y})");
            Log.Info($"[ChunkSelectionController] Click cell=({cellCoord.x},{cellCoord.y}) chunk=({chunkCoord.x},{chunkCoord.y})");
        }

        /// <summary>
        /// summary: 根据世界坐标解析 Chunk 坐标与格子坐标。
        /// param: worldPos 世界坐标
        /// param: chunkCoord 输出 Chunk 坐标
        /// param: cellCoord 输出格子坐标
        /// return: true=解析成功
        /// </summary>
        private bool TryResolveChunkFromHit(Vector3 worldPos, out Vector2Int chunkCoord, out Vector3Int cellCoord)
        {
            cellCoord = worldGrid.WorldToCellXZ(worldPos);

            if (worldGrid.chunkWidthCells <= 0 || worldGrid.chunkHeightCells <= 0)
            {
                chunkCoord = Vector2Int.zero;
                return false;
            }

            chunkCoord = new Vector2Int(
                Mathf.FloorToInt((float)cellCoord.x / worldGrid.chunkWidthCells),
                Mathf.FloorToInt((float)cellCoord.y / worldGrid.chunkHeightCells)
            );

            return true;
        }
    }
}
