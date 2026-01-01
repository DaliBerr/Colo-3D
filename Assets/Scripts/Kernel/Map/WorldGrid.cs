using System;
using System.Collections.Generic;
using Kernel.UI;
using Lonize.Logging;
using UnityEngine;

namespace Kernel.World
{
    /// <summary>
    /// summary: 世界网格服务（XZ 平面格子），提供坐标转换、地面高度/法线/Tag 查询。
    /// </summary>
    public class WorldGrid : MonoBehaviour
    {
        /// <summary>
        /// summary: 单例引用。
        /// </summary>
        public static WorldGrid Instance { get; private set; }

        [Header("Grid")]
        [Min(0.001f)] public float cellSize = 1f;
        public Vector3 worldOrigin = Vector3.zero;

        [Header("Chunk Layout（用于快速定位高度场 chunk）")]
        [Min(1)] public int chunkWidthCells = 50;
        [Min(1)] public int chunkHeightCells = 50;

        [Header("Raycast Fallback（没注册高度场时兜底）")]
        public bool enableRaycastFallback = true;
        public LayerMask groundMask;
        [Min(0.1f)] public float raycastStartHeight = 500f;
        [Min(0.1f)] public float raycastDistance = 2000f;

        [Header("Buildable")]
        [Range(0f, 89f)] public float maxSlopeDegrees = 35f;

        /// <summary>
        /// summary: 地块 Tag 提供器（可选），用于支持 BuildingDef 的 required/forbidden tags。
        /// </summary>
        public ITerrainTagProvider tagProvider;

        private readonly Dictionary<Vector2Int, HeightFieldChunk> _heightChunks = new();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }
        private void Start()
        {
            chunkWidthCells = GetComponentInChildren<WorldChunkMeshGenerator>()?.chunkWidth ?? chunkWidthCells;
            chunkHeightCells = GetComponentInChildren<WorldChunkMeshGenerator>()?.chunkHeight ?? chunkHeightCells;
        }
        #region 坐标转换

        /// <summary>
        /// summary: 世界坐标 -> 格子坐标（XZ），返回 Vector3Int(x,z,0) 形式（y 用作 Z）。
        /// param: worldPos 世界坐标
        /// return: 格子坐标（x=cellX, y=cellZ, z=0）
        /// </summary>
        public Vector3Int WorldToCellXZ(Vector3 worldPos)
        {
            float cx = (worldPos.x - worldOrigin.x) / cellSize;
            float cz = (worldPos.z - worldOrigin.z) / cellSize;

            int x = Mathf.FloorToInt(cx);
            int z = Mathf.FloorToInt(cz);
            return new Vector3Int(x, z, 0);
        }

        /// <summary>
        /// summary: 格子坐标 -> 世界坐标中心点（XZ），Y 不填高度（需要再用 TryGetGroundAtCell）。
        /// param: cell 格子坐标（x=cellX, y=cellZ）
        /// return: 世界坐标中心点（y=0）
        /// </summary>
        public Vector3 CellToWorldCenterXZ(Vector3Int cell)
        {
            float wx = worldOrigin.x + (cell.x + 0.5f) * cellSize;
            float wz = worldOrigin.z + (cell.y + 0.5f) * cellSize;
            return new Vector3(wx, 0f, wz);
        }

        #endregion

        #region 高度场注册与采样

        /// <summary>
        /// summary: 注册/更新一个 chunk 的高度场（顶点高度数组为 (w+1)*(h+1)，单位=世界单位）。
        /// param: chunkCoord chunk 坐标（cx,cz）
        /// param: originCell chunk 的左下角顶点所在格坐标（cellX,cellZ）
        /// param: widthCells chunk 宽（格）
        /// param: heightCells chunk 高（格）
        /// param: vertexHeights 顶点高度数组（(w+1)*(h+1)）
        /// return: 是否成功
        /// </summary>
        public bool RegisterHeightFieldChunk(Vector2Int chunkCoord, Vector2Int originCell, int widthCells, int heightCells, float[] vertexHeights)
        {
            if (widthCells <= 0 || heightCells <= 0 || vertexHeights == null)
                return false;

            int vw = widthCells + 1;
            int vh = heightCells + 1;
            if (vertexHeights.Length != vw * vh)
            {
                GameDebug.LogError($"[WorldGrid] RegisterHeightFieldChunk 失败：数组长度不匹配，期望 {vw * vh}，实际 {vertexHeights.Length}");
                Log.Error($"[WorldGrid] RegisterHeightFieldChunk 失败：数组长度不匹配，期望 {vw * vh}，实际 {vertexHeights.Length}");
                return false;
            }

            _heightChunks[chunkCoord] = new HeightFieldChunk
            {
                chunkCoord = chunkCoord,
                originCell = originCell,
                widthCells = widthCells,
                heightCells = heightCells,
                vertexHeights = vertexHeights
            };
            return true;
        }
        #endregion
        /// <summary>
        /// summary: 清空所有高度场（用于重生成世界）。
        /// param: 无
        /// return: 无
        /// </summary>
        public void ClearHeightFields()
        {
            _heightChunks.Clear();
        }

        /// <summary>
        /// summary: 查询某格中心点的地面信息（高度/法线/Tag）。
        /// param: cell 格子坐标（x=cellX, y=cellZ）
        /// param: height 输出高度
        /// param: normal 输出法线
        /// param: tags 输出 tags（可为空集合）
        /// return: 是否查询成功
        /// </summary>
        public bool TryGetGroundAtCell(Vector3Int cell, out float height, out Vector3 normal, out IReadOnlyCollection<string> tags)
        {
            Vector3 centerXZ = CellToWorldCenterXZ(cell);

            bool ok = TrySampleHeightAndNormalAtWorld(centerXZ, out height, out normal);
            tags = tagProvider != null ? tagProvider.GetTags(cell) : Array.Empty<string>();
            return ok;
        }

        /// <summary>
        /// summary: 判断格子是否“存在”（即有高度数据或可 raycast 命中）。
        /// param: cell 格子坐标
        /// return: 是否存在
        /// </summary>
        public bool IsCellValid(Vector3Int cell)
        {
            Vector3 world = CellToWorldCenterXZ(cell);
            if (TrySampleHeightAndNormalAtWorld(world, out _, out _))
                return true;
            return false;
        }

        /// <summary>
        /// summary: 判断格子是否可建（存在 + 坡度不超过阈值 + Tag 规则可由上层再叠加）。
        /// param: cell 格子坐标
        /// return: 是否可建
        /// </summary>
        public bool IsCellBuildable(Vector3Int cell)
        {
            if (!TryGetGroundAtCell(cell, out _, out var n, out _))
                return false;

            float slope = Vector3.Angle(n, Vector3.up);
            return slope <= maxSlopeDegrees;
        }

        /// <summary>
        /// summary: 检查某格是否满足 required/forbidden tags（用于 BuildingDef 放置规则）。
        /// param: cell 格子坐标
        /// param: required 必须具备的 tags（可为 null/空）
        /// param: forbidden 禁止出现的 tags（可为 null/空）
        /// return: 是否满足
        /// </summary>
        public bool CheckCellTags(Vector3Int cell, IList<string> required, IList<string> forbidden)
        {
            var tags = tagProvider != null ? tagProvider.GetTags(cell) : Array.Empty<string>();

            if (forbidden != null)
            {
                for (int i = 0; i < forbidden.Count; i++)
                {
                    if (ContainsTag(tags, forbidden[i]))
                        return false;
                }
            }

            if (required != null)
            {
                for (int i = 0; i < required.Count; i++)
                {
                    if (!ContainsTag(tags, required[i]))
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// summary: 尝试在世界坐标处采样高度+法线（优先高度场，否则 raycast）。
        /// param: worldPos 世界坐标（只取 xz）
        /// param: height 输出高度
        /// param: normal 输出法线
        /// return: 是否成功
        /// </summary>
        public bool TrySampleHeightAndNormalAtWorld(Vector3 worldPos, out float height, out Vector3 normal)
        {
            // 1) 高度场优先
            if (TrySampleFromHeightField(worldPos, out height, out normal))
                return true;

            // 2) raycast 兜底
            if (enableRaycastFallback && TryRaycastGround(worldPos, out height, out normal))
                return true;

            height = 0f;
            normal = Vector3.up;
            return false;
        }

        /// <summary>
        /// summary: 从高度场采样高度+法线（双线性插值 + 中心差分法线）。
        /// param: worldPos 世界坐标（只取 xz）
        /// param: height 输出高度
        /// param: normal 输出法线
        /// return: 是否成功
        /// </summary>
        private bool TrySampleFromHeightField(Vector3 worldPos, out float height, out Vector3 normal)
        {
            // 换算到“格坐标系（浮点）”
            float cellX = (worldPos.x - worldOrigin.x) / cellSize;
            float cellZ = (worldPos.z - worldOrigin.z) / cellSize;

            Vector2Int chunkCoord = new Vector2Int(
                Mathf.FloorToInt(cellX / chunkWidthCells),
                Mathf.FloorToInt(cellZ / chunkHeightCells)
            );

            if (!_heightChunks.TryGetValue(chunkCoord, out var chunk))
            {
                height = 0f;
                normal = Vector3.up;
                return false;
            }
            int expectOriginX = chunkCoord.x * chunkWidthCells;
            int expectOriginZ = chunkCoord.y * chunkHeightCells;
            if (chunk.originCell.x != expectOriginX || chunk.originCell.y != expectOriginZ)
            {
                GameDebug.LogWarning($"HeightFieldChunk originCell mismatch. key={chunkCoord} originCell={chunk.originCell} expected=({expectOriginX},{expectOriginZ})");
            }
            float localX = cellX - chunk.originCell.x;
            float localZ = cellZ - chunk.originCell.y;

            // 双线性插值高度（在顶点网格上插值）
            height = SampleHeightBilinear(chunk, localX, localZ);

            // 法线：用高度函数做中心差分（采样间隔=1格）
            float hL = SampleHeightBilinear(chunk, localX - 1f, localZ);
            float hR = SampleHeightBilinear(chunk, localX + 1f, localZ);
            float hD = SampleHeightBilinear(chunk, localX, localZ - 1f);
            float hU = SampleHeightBilinear(chunk, localX, localZ + 1f);

            // 横向距离单位=cellSize（世界单位）
            normal = new Vector3(hL - hR, 2f * cellSize, hD - hU).normalized;
            return true;
        }

        /// <summary>
        /// summary: 高度场双线性插值（输入 localX/localZ 以“格”为单位）。
        /// param: chunk 高度场 chunk
        /// param: localX 本 chunk 内浮点格坐标 x
        /// param: localZ 本 chunk 内浮点格坐标 z
        /// return: 高度（世界单位）
        /// </summary>
        private static float SampleHeightBilinear(HeightFieldChunk chunk, float localX, float localZ)
        {
            // Clamp 到 [0, width] / [0, height] 的顶点范围
            localX = Mathf.Clamp(localX, 0f, chunk.widthCells);
            localZ = Mathf.Clamp(localZ, 0f, chunk.heightCells);

            int x0 = Mathf.Clamp(Mathf.FloorToInt(localX), 0, chunk.widthCells - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt(localZ), 0, chunk.heightCells - 1);
            int x1 = x0 + 1;
            int z1 = z0 + 1;

            float tx = localX - x0;
            float tz = localZ - z0;

            int vw = chunk.widthCells + 1;

            float h00 = chunk.vertexHeights[z0 * vw + x0];
            float h10 = chunk.vertexHeights[z0 * vw + x1];
            float h01 = chunk.vertexHeights[z1 * vw + x0];
            float h11 = chunk.vertexHeights[z1 * vw + x1];

            float hx0 = Mathf.Lerp(h00, h10, tx);
            float hx1 = Mathf.Lerp(h01, h11, tx);
            return Mathf.Lerp(hx0, hx1, tz);
        }

        /// <summary>
        /// summary: 通过 Physics.Raycast 查询地面（兜底方案）。
        /// param: worldPos 世界坐标（只取 xz）
        /// param: height 输出高度
        /// param: normal 输出法线
        /// return: 是否命中
        /// </summary>
        private bool TryRaycastGround(Vector3 worldPos, out float height, out Vector3 normal)
        {
            Vector3 origin = new Vector3(worldPos.x, worldPos.y + raycastStartHeight, worldPos.z);
            Ray ray = new Ray(origin, Vector3.down);

            if (Physics.Raycast(ray, out var hit, raycastDistance, groundMask, QueryTriggerInteraction.Ignore))
            {
                height = hit.point.y;
                normal = hit.normal;
                return true;
            }

            height = 0f;
            normal = Vector3.up;
            return false;
        }

        /// <summary>
        /// summary: 判断 tag 集合是否包含某 tag。
        /// param: tags tag 集合
        /// param: tag 目标 tag
        /// return: 是否包含
        /// </summary>
        private static bool ContainsTag(IReadOnlyCollection<string> tags, string tag)
        {
            if (tags == null || string.IsNullOrEmpty(tag))
                return false;

            foreach (var t in tags)
            {
                if (t == tag)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// summary: 高度场 chunk 数据。
        /// </summary>
        private struct HeightFieldChunk
        {
            public Vector2Int chunkCoord;
            public Vector2Int originCell;
            public int widthCells;
            public int heightCells;
            public float[] vertexHeights;
        }
    }

    /// <summary>
    /// summary: 地形 Tag 提供器接口（可由你的群系/地表系统实现）。
    /// </summary>
    public interface ITerrainTagProvider
    {
        /// <summary>
        /// summary: 获取某格的 tags。
        /// param: cell 格子坐标
        /// return: tags（可返回空集合）
        /// </summary>
        IReadOnlyCollection<string> GetTags(Vector3Int cell);
    }
}