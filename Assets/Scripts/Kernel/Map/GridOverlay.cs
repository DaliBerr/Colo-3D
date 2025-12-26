using System.Collections.Generic;
using UnityEngine;
using Kernel.World;
using Lonize;
using Kernel.GameState;

namespace Kernel.DebugView
{
    /// <summary>
    /// summary: 网格叠加显示模式。
    /// </summary>
    public enum GridOverlayMode
    {
        Placement,
        Removal
    }

    /// <summary>
    /// summary: 3D 地表网格线叠加层（贴合地形高度），用于放置/拆除时对齐。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GridOverlayRenderer3D : MonoBehaviour
    {
        private const int MaxOverlaySizeCells = 201;   // 最大显示范围：201x201 cells
        private const int MinOverlaySizeCells = 1;     // 最小显示范围：1x1 cells（必须为奇数）
        private const int MaxHalfExtentCells = 100;    // (201-1)/2 = 100

        [Header("Refs")]
        public WorldGrid worldGrid;
        public Camera targetCamera;

        [Header("Raycast")]
        public LayerMask groundMask;
        [Min(1f)] public float rayDistance = 5000f;

        [Header("Range (cells, odd, max 201x201)")]
        public Vector2Int overlaySizeCells = new Vector2Int(41, 41); // (宽, 高)，会自动修正为奇数并Clamp到1..201
        [Min(0f)] public float yOffset = 0.03f;

        [Header("Performance")]
        [Tooltip("网格重建的最小间隔(秒)，用于降频减轻CPU开销。")]
        [Min(0f)] public float rebuildMinInterval = 0.05f;

        [Tooltip("是否启用高度采样缓存（范围越大越推荐）。")]
        public bool enableHeightCache = true;

        [Tooltip("高度缓存容量上限（条目数），超出会按先进先出淘汰。")]
        [Min(0)] public int heightCacheCapacity = 200000;

        [Header("Colors (minor depends on mode)")]
        public Color minorLineColorPlace = new Color(0.2f, 1f, 0.2f, 0.35f);    // 绿色
        public Color minorLineColorRemove = new Color(1f, 0.25f, 0.25f, 0.35f); // 红色
        public Color majorLineColor = new Color(1f, 0.9f, 0.3f, 0.85f);         // 高亮

        [Header("Major Line Rules")]
        public bool highlightHoveredCell = true;
        public bool highlightChunkBorders = true;

        [Header("Map Bounds (clip outside)")]
        public bool useMapBounds = false;
        public Vector2Int mapMinCell = Vector2Int.zero;
        public Vector2Int mapSizeCells = new Vector2Int(256, 256); // 地图总格数（宽/高）

        [Header("Render")]
        public Material lineMaterial;

        private MeshFilter _mf;
        private MeshRenderer _mr;
        private Mesh _mesh;

        private bool _visible;
        private bool _dirty;
        private float _nextAllowedRebuildTime;

        private GridOverlayMode _mode = GridOverlayMode.Placement;

        private Vector3Int _lastHoverCell = new Vector3Int(int.MinValue, int.MinValue, 0);
        private Vector2Int _lastOverlaySizeCells = new Vector2Int(int.MinValue, int.MinValue);

        // 采样点缓存：(pointsX * pointsZ)
        private readonly List<Vector3> _gridPoints = new();
        private readonly List<byte> _gridPointValid = new(); // 1=valid,0=invalid

        // Mesh数据
        private readonly List<Vector3> _verts = new();
        private readonly List<Color> _colors = new();
        private readonly List<int> _indices = new();

        // 高度缓存：key=(gx,gz) -> height；height=NaN 表示采样失败（无效点）
        private readonly Dictionary<long, float> _heightCache = new();
        private readonly Queue<long> _heightCacheOrder = new();

        private MapControls mapControls;

        private void Awake()
        {
            mapControls = InputActionManager.Instance != null ? InputActionManager.Instance.Map : null;
            // mapControls.Enable();

            SanitizeOverlaySizeInPlace(ref overlaySizeCells);
            _lastOverlaySizeCells = overlaySizeCells;

            InitRefs();
            InitMesh();
            ApplyMaterial();
            SetVisible(false);
        }

        private void OnValidate()
        {
            var old = overlaySizeCells;
            SanitizeOverlaySizeInPlace(ref overlaySizeCells);
            if (old != overlaySizeCells)
            {
                _dirty = true;
            }

            heightCacheCapacity = Mathf.Max(0, heightCacheCapacity);
        }

        private void Update()
        {
            bool placing = StatusController.HasStatus(StatusList.BuildingPlacementStatus);
            bool removing = StatusController.HasStatus(StatusList.BuildingDestroyingStatus);

            bool desiredVisible = placing || removing;
            if (desiredVisible != _visible)
            {
                SetVisible(desiredVisible);
            }

            if (!_visible)
                return;

            SetMode(removing ? GridOverlayMode.Removal : GridOverlayMode.Placement);

            // 运行时如果在Inspector或代码修改了范围，标脏
            if (_lastOverlaySizeCells != overlaySizeCells)
            {
                SanitizeOverlaySizeInPlace(ref overlaySizeCells);
                _lastOverlaySizeCells = overlaySizeCells;
                _dirty = true;
            }

            if (!TryGetHoverCell(out var hoverCell))
            {
                if (_mr != null) _mr.enabled = false;
                return;
            }

            if (_mr != null && !_mr.enabled)
                _mr.enabled = true;

            bool hoverChanged = (hoverCell.x != _lastHoverCell.x || hoverCell.y != _lastHoverCell.y);
            if (hoverChanged)
            {
                _lastHoverCell = hoverCell;
                _dirty = true;
            }

            // 降频：到点再重建
            if (_dirty && Time.unscaledTime >= _nextAllowedRebuildTime)
            {
                _nextAllowedRebuildTime = Time.unscaledTime + rebuildMinInterval;
                _dirty = false;
                RebuildMesh(_lastHoverCell);
            }
        }

        /// <summary>
        /// summary: 外部设置显示/隐藏（接你的 StatusController 判定结果）。
        /// param: visible 是否显示
        /// return: 无
        /// </summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;

            if (_mr != null)
                _mr.enabled = visible;

            if (visible)
            {
                // 刚显示时强制下一次尽快重建
                _dirty = true;
                _nextAllowedRebuildTime = 0f;
            }
            else
            {
                _lastHoverCell = new Vector3Int(int.MinValue, int.MinValue, 0);
            }
        }

        /// <summary>
        /// summary: 外部设置叠加层模式（放置/拆除），用于切换 minor 颜色。
        /// param: mode 模式
        /// return: 无
        /// </summary>
        public void SetMode(GridOverlayMode mode)
        {
            if (_mode == mode)
                return;

            _mode = mode;
            _dirty = true; // 颜色变化需要重建
        }

        /// <summary>
        /// summary: 设置叠加层显示范围（cell宽高，自动修正为奇数并Clamp到1..201）。
        /// param: widthCells 宽度（cell）
        /// param: heightCells 高度（cell）
        /// return: 是否发生变化
        /// </summary>
        public bool SetOverlaySizeCells(int widthCells, int heightCells)
        {
            var size = new Vector2Int(widthCells, heightCells);
            SanitizeOverlaySizeInPlace(ref size);

            if (overlaySizeCells == size)
                return false;

            overlaySizeCells = size;
            _lastOverlaySizeCells = overlaySizeCells;
            _dirty = true;
            return true;
        }

        /// <summary>
        /// summary: 清空高度采样缓存（当地形高度发生变化/Chunk重新生成后建议调用）。
        /// param: 无
        /// return: 无
        /// </summary>
        public void ClearHeightCache()
        {
            _heightCache.Clear();
            _heightCacheOrder.Clear();
        }

        /// <summary>
        /// summary: 获取当前 minor 颜色（随模式变化）。
        /// param: 无
        /// return: minor 颜色
        /// </summary>
        private Color GetMinorColor()
        {
            return _mode == GridOverlayMode.Removal ? minorLineColorRemove : minorLineColorPlace;
        }

        /// <summary>
        /// summary: 初始化引用（WorldGrid/Camera/组件）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void InitRefs()
        {
            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();

            if (worldGrid == null)
                worldGrid = WorldGrid.Instance;

            if (targetCamera == null)
                targetCamera = Camera.main;

            if (groundMask.value == 0 && worldGrid != null)
                groundMask = worldGrid.groundMask;
        }

        /// <summary>
        /// summary: 初始化网格线 Mesh（Topology=Lines）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void InitMesh()
        {
            _mesh = new Mesh
            {
                name = "GridOverlayLines"
            };
            _mesh.MarkDynamic();
            _mf.sharedMesh = _mesh;
        }

        /// <summary>
        /// summary: 应用材质（必须支持顶点色，否则颜色不生效）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void ApplyMaterial()
        {
            if (_mr == null)
                return;

            if (lineMaterial != null)
            {
                _mr.sharedMaterial = lineMaterial;
            }
            else
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                    _mr.sharedMaterial = new Material(shader);
            }

            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;
        }

        /// <summary>
        /// summary: 从鼠标位置 raycast 地面，得到悬停格子坐标（XZ）。
        /// param: hoverCell 输出悬停格
        /// return: 是否命中地面
        /// </summary>
        private bool TryGetHoverCell(out Vector3Int hoverCell)
        {
            hoverCell = default;

            if (targetCamera == null || worldGrid == null)
                return false;

            Vector2 mousePos = (mapControls != null)
                ? mapControls.OverlayClick.Position.ReadValue<Vector2>()
                : (Vector2)Input.mousePosition;

            Ray ray = targetCamera.ScreenPointToRay(mousePos);
            if (!Physics.Raycast(ray, out var hit, rayDistance, groundMask, QueryTriggerInteraction.Ignore))
                return false;

            hoverCell = worldGrid.WorldToCellXZ(hit.point);
            return true;
        }

        /// <summary>
        /// summary: 重建鼠标附近网格线（贴合地形高度），并裁剪到地图边界。
        /// param: hoverCell 悬停格
        /// return: 无
        /// </summary>
        private void RebuildMesh(Vector3Int hoverCell)
        {
            if (worldGrid == null || _mesh == null)
                return;

            // overlaySizeCells 是奇数：half = (size-1)/2
            int halfX = Mathf.Clamp((overlaySizeCells.x - 1) / 2, 0, MaxHalfExtentCells);
            int halfZ = Mathf.Clamp((overlaySizeCells.y - 1) / 2, 0, MaxHalfExtentCells);

            int minX = hoverCell.x - halfX;
            int maxX = hoverCell.x + halfX;
            int minZ = hoverCell.y - halfZ;
            int maxZ = hoverCell.y + halfZ;

            // 裁剪到地图边界（cell 边界）
            if (useMapBounds)
            {
                ClampCellsToMapBounds(ref minX, ref maxX, ref minZ, ref maxZ);

                // 完全在边界外：不画
                if (minX > maxX || minZ > maxZ)
                {
                    _mesh.Clear();
                    return;
                }
            }

            int cellsX = maxX - minX + 1;
            int cellsZ = maxZ - minZ + 1;

            int pointsX = cellsX + 1;
            int pointsZ = cellsZ + 1;

            EnsureMeshCapacity(pointsX, pointsZ);
            BuildGridPoints(minX, minZ, pointsX, pointsZ);
            BuildLineSegments(minX, minZ, pointsX, pointsZ, hoverCell);
            UploadMesh();
        }

        /// <summary>
        /// summary: 预估线段/顶点数量并为List预留容量，减少扩容与GC。
        /// param: pointsX X方向格点数
        /// param: pointsZ Z方向格点数
        /// return: 无
        /// </summary>
        private void EnsureMeshCapacity(int pointsX, int pointsZ)
        {
            int segCount = pointsX * (pointsZ - 1) + (pointsX - 1) * pointsZ;
            int vCount = segCount * 2;

            if (_verts.Capacity < vCount) _verts.Capacity = vCount;
            if (_colors.Capacity < vCount) _colors.Capacity = vCount;
            if (_indices.Capacity < vCount) _indices.Capacity = vCount;

            int pCount = pointsX * pointsZ;
            if (_gridPoints.Capacity < pCount) _gridPoints.Capacity = pCount;
            if (_gridPointValid.Capacity < pCount) _gridPointValid.Capacity = pCount;
        }

        /// <summary>
        /// summary: 将请求范围裁剪到地图 cell 边界内。
        /// param: minX 最小cellX（引用）
        /// param: maxX 最大cellX（引用）
        /// param: minZ 最小cellZ（引用）
        /// param: maxZ 最大cellZ（引用）
        /// return: 无
        /// </summary>
        private void ClampCellsToMapBounds(ref int minX, ref int maxX, ref int minZ, ref int maxZ)
        {
            int mapMinX = mapMinCell.x;
            int mapMinZ = mapMinCell.y;

            int mapMaxX = mapMinCell.x + mapSizeCells.x - 1;
            int mapMaxZ = mapMinCell.y + mapSizeCells.y - 1;

            minX = Mathf.Max(minX, mapMinX);
            maxX = Mathf.Min(maxX, mapMaxX);

            minZ = Mathf.Max(minZ, mapMinZ);
            maxZ = Mathf.Min(maxZ, mapMaxZ);
        }

        /// <summary>
        /// summary: 判断某个“格点坐标”(整数交点)是否在地图点边界内。
        /// param: gx 格点X（整数）
        /// param: gz 格点Z（整数）
        /// return: 是否在边界内
        /// </summary>
        private bool IsPointInsideMap(int gx, int gz)
        {
            if (!useMapBounds) return true;

            // 点范围：x ∈ [minCellX, minCellX + sizeX]（注意点比cell多1）
            int minX = mapMinCell.x;
            int minZ = mapMinCell.y;
            int maxX = mapMinCell.x + mapSizeCells.x;
            int maxZ = mapMinCell.y + mapSizeCells.y;

            return gx >= minX && gx <= maxX && gz >= minZ && gz <= maxZ;
        }

        /// <summary>
        /// summary: 构建网格交点世界坐标并采样高度（贴地），并标记无效点（边界外/无高度）。
        /// param: startCellX 区域起始格X
        /// param: startCellZ 区域起始格Z
        /// param: pointsX 交点数量X
        /// param: pointsZ 交点数量Z
        /// return: 无
        /// </summary>
        private void BuildGridPoints(int startCellX, int startCellZ, int pointsX, int pointsZ)
        {
            _gridPoints.Clear();
            _gridPointValid.Clear();

            float cs = worldGrid.cellSize;
            Vector3 origin = worldGrid.worldOrigin;

            for (int z = 0; z < pointsZ; z++)
            {
                int gz = startCellZ + z;
                for (int x = 0; x < pointsX; x++)
                {
                    int gx = startCellX + x;

                    bool inside = IsPointInsideMap(gx, gz);
                    if (!inside)
                    {
                        _gridPoints.Add(Vector3.zero);
                        _gridPointValid.Add(0);
                        continue;
                    }

                    float sampledHeight;
                    bool hasHeight = TryGetPointHeight(gx, gz, origin, cs, out sampledHeight);
                    if (hasHeight)
                    {
                        _gridPoints.Add(new Vector3(origin.x + gx * cs, sampledHeight + yOffset, origin.z + gz * cs));
                        _gridPointValid.Add(1);
                    }
                    else
                    {
                        _gridPoints.Add(Vector3.zero);
                        _gridPointValid.Add(0);
                    }
                }
            }
        }

        /// <summary>
        /// summary: 获取格点高度（可走缓存），失败则返回false。
        /// param: gx 格点X
        /// param: gz 格点Z
        /// param: origin WorldGrid原点
        /// param: cs cellSize
        /// param: height 输出高度（不含yOffset）
        /// return: 是否成功获取高度
        /// </summary>
        private bool TryGetPointHeight(int gx, int gz, Vector3 origin, float cs, out float height)
        {
            height = 0f;

            if (enableHeightCache)
            {
                long key = MakeKey(gx, gz);
                if (_heightCache.TryGetValue(key, out float cached))
                {
                    if (float.IsNaN(cached))
                        return false;

                    height = cached;
                    return true;
                }

                bool ok = SampleHeightAtPoint(gx, gz, origin, cs, out float h);
                PutHeightCache(key, ok ? h : float.NaN);
                if (!ok) return false;

                height = h;
                return true;
            }
            else
            {
                return SampleHeightAtPoint(gx, gz, origin, cs, out height);
            }
        }

        /// <summary>
        /// summary: 对指定格点进行真实高度采样。
        /// param: gx 格点X
        /// param: gz 格点Z
        /// param: origin WorldGrid原点
        /// param: cs cellSize
        /// param: height 输出高度
        /// return: 是否采样成功
        /// </summary>
        private bool SampleHeightAtPoint(int gx, int gz, Vector3 origin, float cs, out float height)
        {
            height = 0f;

            float wx = origin.x + gx * cs;
            float wz = origin.z + gz * cs;
            var p = new Vector3(wx, 0f, wz);

            return worldGrid.TrySampleHeightAndNormalAtWorld(p, out height, out _);
        }

        /// <summary>
        /// summary: 构建线段（每个格点之间连线），无效点/边界外不渲染。
        /// param: startCellX 区域起始格X
        /// param: startCellZ 区域起始格Z
        /// param: pointsX 交点数量X
        /// param: pointsZ 交点数量Z
        /// param: hoverCell 悬停格
        /// return: 无
        /// </summary>
        private void BuildLineSegments(int startCellX, int startCellZ, int pointsX, int pointsZ, Vector3Int hoverCell)
        {
            _verts.Clear();
            _colors.Clear();
            _indices.Clear();

            Color minor = GetMinorColor();

            // 竖线段：连接 (x,z)->(x,z+1)
            for (int z = 0; z < pointsZ - 1; z++)
            {
                int segZ = startCellZ + z;
                for (int x = 0; x < pointsX; x++)
                {
                    int lineX = startCellX + x;

                    int i0 = z * pointsX + x;
                    int i1 = (z + 1) * pointsX + x;

                    if (_gridPointValid[i0] == 0 || _gridPointValid[i1] == 0)
                        continue;

                    Color c = ChooseSegmentColor_Vertical(lineX, segZ, hoverCell, minor);
                    AddSegment(_gridPoints[i0], _gridPoints[i1], c);
                }
            }

            // 横线段：连接 (x,z)->(x+1,z)
            for (int z = 0; z < pointsZ; z++)
            {
                int lineZ = startCellZ + z;
                for (int x = 0; x < pointsX - 1; x++)
                {
                    int segX = startCellX + x;

                    int i0 = z * pointsX + x;
                    int i1 = z * pointsX + (x + 1);

                    if (_gridPointValid[i0] == 0 || _gridPointValid[i1] == 0)
                        continue;

                    Color c = ChooseSegmentColor_Horizontal(segX, lineZ, hoverCell, minor);
                    AddSegment(_gridPoints[i0], _gridPoints[i1], c);
                }
            }
        }

        /// <summary>
        /// summary: 选择竖线段颜色（两色制）：hover格边框/Chunk边界用major，其余用minor。
        /// param: lineX 竖线所在整数X
        /// param: segZ 线段起点整数Z（从 segZ 到 segZ+1）
        /// param: hoverCell 悬停格
        /// param: minor minor颜色（随模式）
        /// return: 颜色
        /// </summary>
        private Color ChooseSegmentColor_Vertical(int lineX, int segZ, Vector3Int hoverCell, Color minor)
        {
            if (highlightHoveredCell)
            {
                int hx = hoverCell.x;
                int hz = hoverCell.y;

                bool onHoverX = (lineX == hx || lineX == hx + 1);
                bool onHoverSeg = (segZ == hz);
                if (onHoverX && onHoverSeg)
                    return majorLineColor;
            }

            if (highlightChunkBorders && worldGrid != null && worldGrid.chunkWidthCells > 0)
            {
                if (Mod(lineX, worldGrid.chunkWidthCells) == 0)
                    return majorLineColor;
            }

            return minor;
        }

        /// <summary>
        /// summary: 选择横线段颜色（两色制）：hover格边框/Chunk边界用major，其余用minor。
        /// param: segX 线段起点整数X（从 segX 到 segX+1）
        /// param: lineZ 横线所在整数Z
        /// param: hoverCell 悬停格
        /// param: minor minor颜色（随模式）
        /// return: 颜色
        /// </summary>
        private Color ChooseSegmentColor_Horizontal(int segX, int lineZ, Vector3Int hoverCell, Color minor)
        {
            if (highlightHoveredCell)
            {
                int hx = hoverCell.x;
                int hz = hoverCell.y;

                bool onHoverZ = (lineZ == hz || lineZ == hz + 1);
                bool onHoverSeg = (segX == hx);
                if (onHoverZ && onHoverSeg)
                    return majorLineColor;
            }

            if (highlightChunkBorders && worldGrid != null && worldGrid.chunkHeightCells > 0)
            {
                if (Mod(lineZ, worldGrid.chunkHeightCells) == 0)
                    return majorLineColor;
            }

            return minor;
        }

        /// <summary>
        /// summary: 添加一条线段到 Mesh 顶点/索引列表。
        /// param: a 端点A
        /// param: b 端点B
        /// param: c 线段颜色
        /// return: 无
        /// </summary>
        private void AddSegment(Vector3 a, Vector3 b, Color c)
        {
            int baseIndex = _verts.Count;
            _verts.Add(a);
            _verts.Add(b);

            _colors.Add(c);
            _colors.Add(c);

            _indices.Add(baseIndex);
            _indices.Add(baseIndex + 1);
        }

        /// <summary>
        /// summary: 上传数据到 Mesh（Lines）。
        /// param: 无
        /// return: 无
        /// </summary>
        private void UploadMesh()
        {
            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
            _mesh.SetIndices(_indices, MeshTopology.Lines, 0, true);
            _mesh.RecalculateBounds();
        }

        /// <summary>
        /// summary: 生成缓存Key（gx,gz -> long）。
        /// param: gx 格点X
        /// param: gz 格点Z
        /// return: Key
        /// </summary>
        private static long MakeKey(int gx, int gz)
        {
            return ((long)gx << 32) ^ (uint)gz;
        }

        /// <summary>
        /// summary: 写入高度缓存并按容量上限淘汰旧数据。
        /// param: key 缓存Key
        /// param: value 高度（NaN表示无效）
        /// return: 无
        /// </summary>
        private void PutHeightCache(long key, float value)
        {
            if (_heightCache.ContainsKey(key))
                return;

            _heightCache[key] = value;
            _heightCacheOrder.Enqueue(key);

            if (heightCacheCapacity <= 0)
                return;

            while (_heightCache.Count > heightCacheCapacity && _heightCacheOrder.Count > 0)
            {
                long oldKey = _heightCacheOrder.Dequeue();
                _heightCache.Remove(oldKey);
            }
        }

        /// <summary>
        /// summary: 修正范围参数为奇数，并Clamp到1..201。
        /// param: sizeCells 输入输出范围（宽高）
        /// return: 无
        /// </summary>
        private static void SanitizeOverlaySizeInPlace(ref Vector2Int sizeCells)
        {
            int w = Mathf.Clamp(sizeCells.x, MinOverlaySizeCells, MaxOverlaySizeCells);
            int h = Mathf.Clamp(sizeCells.y, MinOverlaySizeCells, MaxOverlaySizeCells);

            // 强制奇数：若为偶数则向下取最近奇数（至少为1）
            if ((w & 1) == 0) w = Mathf.Max(1, w - 1);
            if ((h & 1) == 0) h = Mathf.Max(1, h - 1);

            sizeCells = new Vector2Int(w, h);
        }

        /// <summary>
        /// summary: 取模（兼容负数）。
        /// param: x 输入
        /// param: m 模
        /// return: 0..m-1
        /// </summary>
        private static int Mod(int x, int m)
        {
            if (m <= 0) return 0;
            int r = x % m;
            return r < 0 ? r + m : r;
        }
    }
}
