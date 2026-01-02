using System;
using System.Collections.Generic;
using Kernel.World;
using Lonize;
using Lonize.Events;
using Lonize.Logging;
using Lonize.Math;
using UnityEngine;
using static Lonize.Events.EventList;

namespace Kernel.World
{
    /// <summary>
    /// 单个 Chunk 的 3D 网格生成器（支持世界空间无缝采样与无缝法线）。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class ChunkMeshGenerator : MonoBehaviour
    {
        [Header("Chunk Size（格）")]
        [Min(1)] public int width = 100;
        [Min(1)] public int height = 100;

        [Header("World Scale（单位/格）")]
        [Min(0.01f)] public float cellSize = 1f;

        [Header("Height（高度映射）")]
        [Min(0f)] public float heightAmplitude = 12f;
        [Range(0.1f, 6f)] public float heightPower = 1.6f;

        [Header("Materials")]
        public Material landMat;
        public Material rockMat;

        [Header("Seed（仅在未配置世界采样时生效）")]
        public int seed = 12345;

        [Header("Noise - Elevation（低频地势）")]
        [Min(0.0001f)] public float elevScale = 160f;
        [Range(1, 8)] public int elevOctaves = 5;
        [Min(1.1f)] public float elevLacunarity = 2f;
        [Range(0.1f, 0.9f)] public float elevPersistence = 0.5f;

        [Header("Noise - Roughness（中高频粗糙）")]
        [Min(0.0001f)] public float roughScale = 60f;
        [Range(1, 6)] public int roughOctaves = 3;
        [Min(1.1f)] public float roughLacunarity = 2f;
        [Range(0.1f, 0.9f)] public float roughPersistence = 0.5f;

        [Header("Domain Warp（域扭曲）")]
        public bool enableWarp = true;
        [Min(0.0001f)] public float warpScale = 120f;
        [Range(1, 5)] public int warpOctaves = 3;
        [Min(0f)] public float warpAmplitude = 18f;

        [Header("Classifier（阈值与混合）")]
        [Range(0f, 1f)] public float rockThreshold = 0.62f;
        [Range(0f, 1f)] public float ridgedWeight = 0.45f;

        [Header("Slope -> Rock（坡度过大也裸岩）")]
        [Range(0f, 1f)] public float slopeToRock01 = 0.55f;

        [Header("Seamless（无缝选项）")]
        public bool seamlessNormals = true;
        public bool addMeshCollider = true;
        public bool logTime = true;

        [Header("Physics")]
        public bool addRigidbody = false;
        // ---- 世界空间采样配置（由 World 统一下发）----
        private bool _useWorldSampling;
        private Vector2 _worldOriginCells; // 该 Chunk 在“格子坐标系”下的原点（左下角顶点）
        private Vector2 _offElev;
        private Vector2 _offRough;
        private Vector2 _offWarp;

        private MeshFilter _mf;
        private MeshRenderer _mr;
        private MeshCollider _mc;
        /// <summary>
        /// 群系参数包：用于在平原/山地之间插值。
        /// </summary>
        public struct BiomeParam
        {
            public bool enableWarp;

            public float heightAmplitude;
            public float heightPower;

            public float elevScale;
            public int elevOctaves;
            public float elevLacunarity;
            public float elevPersistence;

            public float roughScale;
            public int roughOctaves;
            public float roughLacunarity;
            public float roughPersistence;

            public float ridgedWeight;
            public float rockThreshold;
            public float slopeToRock01;

            public float warpScale;
            public int warpOctaves;
            public float warpAmplitude;

            /// <summary>
            /// 在两个群系参数之间插值（整数项采用 max，避免因 round 造成跳变）。
            /// </summary>
            /// <param name="a">A 参数。</param>
            /// <param name="b">B 参数。</param>
            /// <param name="t">插值权重 0..1。</param>
            /// <returns>插值后的参数。</returns>
            public static BiomeParam Lerp(BiomeParam a, BiomeParam b, float t)
            {
                t = Mathf.Clamp01(t);
                return new BiomeParam
                {
                    enableWarp = a.enableWarp || b.enableWarp,

                    heightAmplitude = Mathf.Lerp(a.heightAmplitude, b.heightAmplitude, t),
                    heightPower = Mathf.Lerp(a.heightPower, b.heightPower, t),

                    elevScale = Mathf.Lerp(a.elevScale, b.elevScale, t),
                    elevOctaves = Mathf.Max(a.elevOctaves, b.elevOctaves),
                    elevLacunarity = Mathf.Lerp(a.elevLacunarity, b.elevLacunarity, t),
                    elevPersistence = Mathf.Lerp(a.elevPersistence, b.elevPersistence, t),

                    roughScale = Mathf.Lerp(a.roughScale, b.roughScale, t),
                    roughOctaves = Mathf.Max(a.roughOctaves, b.roughOctaves),
                    roughLacunarity = Mathf.Lerp(a.roughLacunarity, b.roughLacunarity, t),
                    roughPersistence = Mathf.Lerp(a.roughPersistence, b.roughPersistence, t),

                    ridgedWeight = Mathf.Lerp(a.ridgedWeight, b.ridgedWeight, t),
                    rockThreshold = Mathf.Lerp(a.rockThreshold, b.rockThreshold, t),
                    slopeToRock01 = Mathf.Lerp(a.slopeToRock01, b.slopeToRock01, t),

                    warpScale = Mathf.Lerp(a.warpScale, b.warpScale, t),
                    warpOctaves = Mathf.Max(a.warpOctaves, b.warpOctaves),
                    warpAmplitude = Mathf.Lerp(a.warpAmplitude, b.warpAmplitude, t),
                };
            }
        }

        private struct BiomeBlendSettings
        {
            public bool enabled;

            public BiomeParam plain;
            public BiomeParam mountain;

            // 用“chunk 坐标系”计算群系噪声（和你旧版 WorldMapGenerator 的思路一致）
            public int chunkWidth;
            public int chunkHeight;

            public float biomeNoiseScale;
            public float mountainThreshold;
            public float biomeWarpScale;
            public float biomeWarpAmp;

            public float blendWidth; // 越大过渡越宽（建议 0.08~0.18）

            public Vector2 biomeOff;
            public Vector2 biomeWarpOff;
        }

        private BiomeBlendSettings _biomeBlend;

        /// <summary>
        /// 配置群系渐变：在 plain/mountain 两套参数之间按 biome noise 平滑插值。
        /// </summary>
        /// <param name="plain">平原参数。</param>
        /// <param name="mountain">山地参数。</param>
        /// <param name="chunkWidth">chunk 宽（格）。用于把 world cell 坐标归一到 chunk 坐标。</param>
        /// <param name="chunkHeight">chunk 高（格）。</param>
        /// <param name="biomeNoiseScale">群系噪声缩放。</param>
        /// <param name="mountainThreshold">山地阈值。</param>
        /// <param name="blendWidth">渐变宽度（0..1，越大越柔）。</param>
        /// <param name="biomeWarpScale">群系域扭曲缩放。</param>
        /// <param name="biomeWarpAmp">群系域扭曲幅度。</param>
        /// <param name="biomeOff">群系噪声偏移。</param>
        /// <param name="biomeWarpOff">群系扭曲噪声偏移。</param>
        /// <returns>无。</returns>
        public void ConfigureBiomeBlend(
            BiomeParam plain,
            BiomeParam mountain,
            int chunkWidth,
            int chunkHeight,
            float biomeNoiseScale,
            float mountainThreshold,
            float blendWidth,
            float biomeWarpScale,
            float biomeWarpAmp,
            Vector2 biomeOff,
            Vector2 biomeWarpOff)
        {
            _biomeBlend.enabled = true;
            _biomeBlend.plain = plain;
            _biomeBlend.mountain = mountain;
            _biomeBlend.chunkWidth = Mathf.Max(1, chunkWidth);
            _biomeBlend.chunkHeight = Mathf.Max(1, chunkHeight);

            _biomeBlend.biomeNoiseScale = biomeNoiseScale;
            _biomeBlend.mountainThreshold = mountainThreshold;
            _biomeBlend.blendWidth = Mathf.Max(0.0001f, blendWidth);

            _biomeBlend.biomeWarpScale = biomeWarpScale;
            _biomeBlend.biomeWarpAmp = biomeWarpAmp;

            _biomeBlend.biomeOff = biomeOff;
            _biomeBlend.biomeWarpOff = biomeWarpOff;
        }
        private void Awake()
        {
            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
            if (addMeshCollider) _mc = GetComponent<MeshCollider>() ?? gameObject.AddComponent<MeshCollider>();
        }

        /// <summary>
        /// 配置为“世界空间无缝采样”：所有 chunk 共用同一套 offset，并用 worldOriginCells 对齐坐标。
        /// </summary>
        /// <param name="worldOriginCells">该 chunk 的世界原点（单位：格）。</param>
        /// <param name="offElev">全局 Elev 偏移。</param>
        /// <param name="offRough">全局 Rough 偏移。</param>
        /// <param name="offWarp">全局 Warp 偏移。</param>
        /// <returns>无。</returns>
        public void ConfigureWorldSampling(Vector2 worldOriginCells, Vector2 offElev, Vector2 offRough, Vector2 offWarp)
        {
            _useWorldSampling = true;
            _worldOriginCells = worldOriginCells;
            _offElev = offElev;
            _offRough = offRough;
            _offWarp = offWarp;
        }
        /// <summary>
        /// 确保 MeshFilter / MeshRenderer 已缓存且存在。
        /// </summary>
        /// <returns>是否准备成功。</returns>
        private bool EnsureMeshComponents()
        {
            if (_mf == null) _mf = GetComponent<MeshFilter>();
            if (_mr == null) _mr = GetComponent<MeshRenderer>();

            if (_mf == null || _mr == null)
            {
                GameDebug.LogError("[ChunkMeshGenerator] 缺少 MeshFilter 或 MeshRenderer");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 确保 Rigidbody 存在（用于让其他物体碰撞事件更稳定）。
        /// 注意：地形 MeshCollider 通常必须搭配 Kinematic Rigidbody，避免 convex 限制。
        /// </summary>
        /// <param name="forceCreate">是否强制创建。</param>
        /// <returns>Rigidbody（可能为 null）。</returns>
        private Rigidbody EnsureRigidbody(bool forceCreate = true)
        {
            if (!addRigidbody) return null;

            var rb = GetComponent<Rigidbody>();
            if (rb == null && forceCreate) rb = gameObject.AddComponent<Rigidbody>();
            if (rb == null) return null;

            // 地形/Chunk 不参与动力学，只作为碰撞体基座
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeAll;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;

            return rb;
        }
        private MeshCollider EnsureMeshCollider(bool forceCreate = true)
        {
            if (!addMeshCollider) return null;

            if (_mc == null) _mc = GetComponent<MeshCollider>();
            if (_mc == null && forceCreate) _mc = gameObject.AddComponent<MeshCollider>();

            if (_mc != null)
            {
                // Raycast 默认可能忽略 Trigger，所以这里明确设为 false
                _mc.convex = false;      // 地形一般是凹形，必须 false（静态使用没问题）
                _mc.isTrigger = false;

                // 这几个 cooking 选项能更稳一点（尤其是大网格）
                _mc.cookingOptions =
                    MeshColliderCookingOptions.CookForFasterSimulation |
                    MeshColliderCookingOptions.EnableMeshCleaning |
                    MeshColliderCookingOptions.WeldColocatedVertices;
            }

            return _mc;
        }
        /// <summary>
        /// 生成该 Chunk：准备偏移 -> 生成数据 -> 构建 Mesh。
        /// </summary>
        /// <param name="publishEvent">是否发布 MapReady 事件。</param>
        /// <returns>是否成功生成。</returns>
        public bool GenerateChunk()
        {
            if (!CheckRefs()) return false;
            if (!EnsureMeshComponents()) return false;
            if (addMeshCollider) EnsureMeshCollider();
            if (addRigidbody) EnsureRigidbody();
            var sw = System.Diagnostics.Stopwatch.StartNew();

            if (!_useWorldSampling)
                PrepareOffsetsFromLocalSeed();

            var data = GenerateData();
            var mesh = BuildMesh(data);

            // 将生成的高度场注册到全局 WorldGrid，供建造/查询使用
            TryRegisterHeightField(data);

            sw.Stop();
            if (logTime)
            {
                GameDebug.Log($"[ChunkMeshGenerator] 生成完成：{width}×{height}，耗时 {sw.ElapsedMilliseconds} ms");
                Log.Info($"[ChunkMeshGenerator] 生成完成：{width}×{height}，耗时 {sw.ElapsedMilliseconds} ms");
            }

            // if (publishEvent)
            //     Lonize.Events.Event.eventBus.Publish(new MapReady(true, transform.position));

            

            gameObject.layer = LayerMask.NameToLayer("Ground");
            return mesh != null;
        }

        private void TryRegisterHeightField(ChunkData data)
        {
            if (!_useWorldSampling) return;
            if (WorldGrid.Instance == null) return;

            int chunkX = Mathf.RoundToInt(_worldOriginCells.x / Mathf.Max(1, data.Width));
            int chunkZ = Mathf.RoundToInt(_worldOriginCells.y / Mathf.Max(1, data.Height));

            var originCell = new Vector2Int(
                Mathf.RoundToInt(_worldOriginCells.x),
                Mathf.RoundToInt(_worldOriginCells.y)
            );

            bool ok = WorldGrid.Instance.RegisterHeightFieldChunk(
                new Vector2Int(chunkX, chunkZ),
                originCell,
                data.Width,
                data.Height,
                data.VertexHeights
            );

            if (!ok)
                GameDebug.LogError("[ChunkMeshGenerator] 注册高度场失败");
        }
        /// <summary>
        /// 使用指定群系参数采样高度（单位：米）。
        /// </summary>
        /// <param name="worldX">世界格坐标 X。</param>
        /// <param name="worldZ">世界格坐标 Z。</param>
        /// <param name="p">群系参数。</param>
        /// <returns>高度（米）。</returns>
        private float SampleHeightWithParam(float worldX, float worldZ, BiomeParam p)
        {
            float wx = worldX;
            float wz = worldZ;

            if (p.enableWarp && p.warpAmplitude > 0f)
            {
                float w = MathUtils.FBM((wx + _offWarp.x) / Mathf.Max(0.0001f, p.warpScale),
                                (wz + _offWarp.y) / Mathf.Max(0.0001f, p.warpScale),
                                p.warpOctaves, 2f, 0.5f);
                w = w * 2f - 1f;
                wx += w * p.warpAmplitude;
                wz -= w * p.warpAmplitude;
            }

            float elevRaw = MathUtils.FBM((wx + _offElev.x) / Mathf.Max(0.0001f, p.elevScale),
                                    (wz + _offElev.y) / Mathf.Max(0.0001f, p.elevScale),
                                    p.elevOctaves, p.elevLacunarity, p.elevPersistence);
            float elev = Normalize01Safe(elevRaw);

            float ridged = 1f - Mathf.Abs(2f * elev - 1f);

            float roughRaw = MathUtils.FBM((wx + _offRough.x) / Mathf.Max(0.0001f, p.roughScale),
                                    (wz + _offRough.y) / Mathf.Max(0.0001f, p.roughScale),
                                    p.roughOctaves, p.roughLacunarity, p.roughPersistence);
            float rough = Normalize01Safe(roughRaw);

            float base01 = Mathf.Lerp(elev, ridged, p.ridgedWeight) * 0.85f + rough * 0.15f;
            float h01 = Mathf.Pow(Mathf.Clamp01(base01), p.heightPower);
            return h01 * p.heightAmplitude;
        }

        /// <summary>
        /// 使用指定群系参数采样 rockiness（0..1）。
        /// </summary>
        /// <param name="worldX">世界格坐标 X。</param>
        /// <param name="worldZ">世界格坐标 Z。</param>
        /// <param name="p">群系参数。</param>
        /// <returns>rockiness（0..1）。</returns>
        private float SampleRockinessWithParam(float worldX, float worldZ, BiomeParam p)
        {
            float elevRaw = MathUtils.FBM((worldX + _offElev.x) / Mathf.Max(0.0001f, p.elevScale),
                                    (worldZ + _offElev.y) / Mathf.Max(0.0001f, p.elevScale),
                                    p.elevOctaves, p.elevLacunarity, p.elevPersistence);
            float elev = Normalize01Safe(elevRaw);

            float ridged = 1f - Mathf.Abs(2f * elev - 1f);

            float roughRaw = MathUtils.FBM((worldX + _offRough.x) / Mathf.Max(0.0001f, p.roughScale),
                                    (worldZ + _offRough.y) / Mathf.Max(0.0001f, p.roughScale),
                                    p.roughOctaves, p.roughLacunarity, p.roughPersistence);
            float rough = Normalize01Safe(roughRaw);

            return Mathf.Lerp(elev, ridged, p.ridgedWeight) * 0.7f + rough * 0.3f;
        }

        /// <summary>
        /// 生成 ChunkData（高度+岩石遮罩）。高度采样在世界空间可无缝对齐。
        /// </summary>
        /// <returns>ChunkData。</returns>
        public ChunkData GenerateData()
        {
            int vw = width + 1;
            int vh = height + 1;

            var heights = new float[vw * vh];
            var rockMask = new byte[width * height];

            // Pass 1：顶点高度（使用世界坐标）
            for (int z = 0; z < vh; z++)
            for (int x = 0; x < vw; x++)
            {
                float worldX = GetWorldX(x);
                float worldZ = GetWorldZ(z);
                heights[z * vw + x] = SampleHeight(worldX, worldZ);
            }

            // Pass 2：按格分类 Rock/Land（rockiness + 坡度）
            for (int z = 0; z < height; z++)
            for (int x = 0; x < width; x++)
            {
                float h00 = heights[(z + 0) * vw + (x + 0)];
                float h10 = heights[(z + 0) * vw + (x + 1)];
                float h01 = heights[(z + 1) * vw + (x + 0)];
                float h11 = heights[(z + 1) * vw + (x + 1)];

                float dhx = Mathf.Max(Mathf.Abs(h10 - h00), Mathf.Abs(h11 - h01));
                float dhz = Mathf.Max(Mathf.Abs(h01 - h00), Mathf.Abs(h11 - h10));
                float slope = Mathf.Max(dhx, dhz) / Mathf.Max(0.0001f, cellSize);

                float worldCx = GetWorldX(x + 0.5f);
                float worldCz = GetWorldZ(z + 0.5f);
                
                float wBiome = _biomeBlend.enabled ? SampleBiomeWeight01(worldCx, worldCz) : 0f;
                float r0 = _biomeBlend.enabled ? SampleRockinessWithParam(worldCx, worldCz, _biomeBlend.plain) : SampleRockinessWithParam(worldCx, worldCz, GetBiomeParamAt(worldCx, worldCz));
                float r1 = _biomeBlend.enabled ? SampleRockinessWithParam(worldCx, worldCz, _biomeBlend.mountain) : r0;
                float rockiness = _biomeBlend.enabled ? Mathf.Lerp(r0, r1, wBiome) : r0;
                
                float rockTh = _biomeBlend.enabled ? Mathf.Lerp(_biomeBlend.plain.rockThreshold, _biomeBlend.mountain.rockThreshold, wBiome) : rockThreshold;
                float slopeTh = _biomeBlend.enabled ? Mathf.Lerp(_biomeBlend.plain.slopeToRock01, _biomeBlend.mountain.slopeToRock01, wBiome) : slopeToRock01;

                float amp = _biomeBlend.enabled ? Mathf.Lerp(_biomeBlend.plain.heightAmplitude, _biomeBlend.mountain.heightAmplitude, wBiome) : heightAmplitude;
                float slope01 = Mathf.Clamp01(slope / Mathf.Max(0.0001f, amp * 0.35f));
                                
                // var p = GetBiomeParamAt(worldCx, worldCz);
                // float slope01 = Mathf.Clamp01(slope / Mathf.Max(0.0001f, p.heightAmplitude * 0.35f));

                // float rockiness = SampleRockiness01(worldCx, worldCz, p);

                bool isRock = rockiness >= rockTh || slope01 >= slopeTh;
                rockMask[z * width + x] = (byte)(isRock ? 1 : 0);
            }

            return new ChunkData(width, height, cellSize, heights, rockMask);
        }

        /// <summary>
        /// 用 ChunkData 构建 Mesh（submesh0=Land, submesh1=Rock），并可生成无缝法线。
        /// </summary>
        /// <param name="data">Chunk 数据。</param>
        /// <returns>生成的 Mesh。</returns>
        public Mesh BuildMesh(ChunkData data)
        {
            int vw = data.Width + 1;
            int vh = data.Height + 1;

            var verts = new Vector3[vw * vh];
            var uvs = new Vector2[vw * vh];
            var norms = new Vector3[vw * vh];

            for (int z = 0; z < vh; z++)
            for (int x = 0; x < vw; x++)
            {
                float y = data.VertexHeights[z * vw + x];
                verts[z * vw + x] = new Vector3(x * data.CellSize, y, z * data.CellSize);
                uvs[z * vw + x] = new Vector2((float)x / data.Width, (float)z / data.Height);
            }

            if (seamlessNormals)
                BuildSeamlessNormals(data, norms);
            else
                Array.Fill(norms, Vector3.up);

            var landTris = new List<int>(data.Width * data.Height * 6);
            var rockTris = new List<int>(data.Width * data.Height * 6);

            for (int z = 0; z < data.Height; z++)
            for (int x = 0; x < data.Width; x++)
            {
                int v00 = (z + 0) * vw + (x + 0);
                int v10 = (z + 0) * vw + (x + 1);
                int v01 = (z + 1) * vw + (x + 0);
                int v11 = (z + 1) * vw + (x + 1);

                bool isRock = data.RockMask[z * data.Width + x] != 0;
                var tris = isRock ? rockTris : landTris;

                tris.Add(v00); tris.Add(v01); tris.Add(v11);
                tris.Add(v00); tris.Add(v11); tris.Add(v10);
            }

            var mesh = new Mesh();
            mesh.name = $"ChunkMesh_{data.Width}x{data.Height}";
            if (verts.Length > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.normals = norms;

            mesh.subMeshCount = 2;
            mesh.SetTriangles(landTris, 0);
            mesh.SetTriangles(rockTris, 1);

            mesh.RecalculateBounds();

            _mf.sharedMesh = mesh;
            _mr.sharedMaterials = new[] { landMat, rockMat };

            if (_mc != null)
            {
                _mc.sharedMesh = null;
                _mc.sharedMesh = mesh;
            }

            return mesh;
        }

        /// <summary>
        /// 计算无缝法线：内部用已生成高度，边界向外采样 1 格，避免 chunk 间光照接缝。
        /// </summary>
        /// <param name="data">Chunk 数据。</param>
        /// <param name="norms">输出法线数组。</param>
        /// <returns>无。</returns>
        private void BuildSeamlessNormals(ChunkData data, Vector3[] norms)
        {
            int vw = data.Width + 1;
            int vh = data.Height + 1;

            for (int z = 0; z < vh; z++)
            for (int x = 0; x < vw; x++)
            {
                float worldX = GetWorldX(x);
                float worldZ = GetWorldZ(z);

                float hL = (x > 0)      ? data.VertexHeights[z * vw + (x - 1)] : SampleHeight(worldX - 1f, worldZ);
                float hR = (x < vw - 1) ? data.VertexHeights[z * vw + (x + 1)] : SampleHeight(worldX + 1f, worldZ);
                float hD = (z > 0)      ? data.VertexHeights[(z - 1) * vw + x] : SampleHeight(worldX, worldZ - 1f);
                float hU = (z < vh - 1) ? data.VertexHeights[(z + 1) * vw + x] : SampleHeight(worldX, worldZ + 1f);

                // 梯度法线： (hL-hR, 2*cellSize, hD-hU)
                Vector3 n = new Vector3(hL - hR, 2f * cellSize, hD - hU).normalized;
                norms[z * vw + x] = n;
            }
        }

        /// <summary>
        /// 采样该顶点的世界 X 坐标（单位：格坐标，不是世界米）。
        /// </summary>
        /// <param name="localX">chunk 内部 x（可为小数）。</param>
        /// <returns>世界格坐标 X。</returns>
        private float GetWorldX(float localX) => _useWorldSampling ? (_worldOriginCells.x + localX) : localX;

        /// <summary>
        /// 采样该顶点的世界 Z 坐标（单位：格坐标，不是世界米）。
        /// </summary>
        /// <param name="localZ">chunk 内部 z（可为小数）。</param>
        /// <returns>世界格坐标 Z。</returns>
        private float GetWorldZ(float localZ) => _useWorldSampling ? (_worldOriginCells.y + localZ) : localZ;

        /// <summary>
        /// 采样世界高度（单位：米）：域扭曲 -> elev/rough -> 映射到高度。
        /// </summary>
        /// <param name="worldX">世界格坐标 X。</param>
        /// <param name="worldZ">世界格坐标 Z。</param>
        /// <returns>高度（米）。</returns>
        /// <summary>
        /// 获取某个世界坐标处的群系插值参数（若未启用渐变，则返回当前生成器参数）。
        /// </summary>
        /// <param name="worldX">世界格坐标 X。</param>
        /// <param name="worldZ">世界格坐标 Z。</param>
        /// <returns>插值后的群系参数。</returns>
        private BiomeParam GetBiomeParamAt(float worldX, float worldZ)
        {
            if (!_biomeBlend.enabled)
            {
                return new BiomeParam
                {
                    enableWarp = enableWarp,

                    heightAmplitude = heightAmplitude,
                    heightPower = heightPower,

                    elevScale = elevScale,
                    elevOctaves = elevOctaves,
                    elevLacunarity = elevLacunarity,
                    elevPersistence = elevPersistence,

                    roughScale = roughScale,
                    roughOctaves = roughOctaves,
                    roughLacunarity = roughLacunarity,
                    roughPersistence = roughPersistence,

                    ridgedWeight = ridgedWeight,
                    rockThreshold = rockThreshold,
                    slopeToRock01 = slopeToRock01,

                    warpScale = warpScale,
                    warpOctaves = warpOctaves,
                    warpAmplitude = warpAmplitude
                };
            }

            float w = SampleBiomeWeight01(worldX, worldZ);
            return BiomeParam.Lerp(_biomeBlend.plain, _biomeBlend.mountain, w);
        }

        /// <summary>
        /// 采样群系权重：0=平原，1=山地（使用 smoothstep 做过渡）。
        /// 采用 chunk 坐标系：worldCell / chunkSize，保证和旧版“按 chunk 分布”相近但可渐变。
        /// </summary>
        /// <param name="worldX">世界格坐标 X。</param>
        /// <param name="worldZ">世界格坐标 Z。</param>
        /// <returns>权重 0..1。</returns>
        private float SampleBiomeWeight01(float worldX, float worldZ)
        {
            // 归一到 chunk 坐标（例如 cx=0..1..2，且在 chunk 内连续变化）
            float bx = worldX / _biomeBlend.chunkWidth;
            float bz = worldZ / _biomeBlend.chunkHeight;

            Vector2 u = new Vector2(
                (bx + _biomeBlend.biomeOff.x) * _biomeBlend.biomeNoiseScale,
                (bz + _biomeBlend.biomeOff.y) * _biomeBlend.biomeNoiseScale
            );

            float warp = Mathf.PerlinNoise(
                (u.x + _biomeBlend.biomeWarpOff.x) * _biomeBlend.biomeWarpScale,
                (u.y + _biomeBlend.biomeWarpOff.y) * _biomeBlend.biomeWarpScale
            ) * 2f - 1f;

            Vector2 uw = u + new Vector2(warp * _biomeBlend.biomeWarpAmp, -warp * _biomeBlend.biomeWarpAmp);
            float n = Mathf.PerlinNoise(uw.x, uw.y); // 0..1

            float a = _biomeBlend.mountainThreshold - _biomeBlend.blendWidth;
            float b = _biomeBlend.mountainThreshold + _biomeBlend.blendWidth;

            float t = Mathf.InverseLerp(a, b, n);
            return Mathf.SmoothStep(0f, 1f, t);
        }

        /// <summary>
        /// 采样高度：若启用群系渐变，则混合平原/山地“结果”，避免参数渐变导致的尖峰。
        /// </summary>
        /// <param name="worldX">世界格坐标 X。</param>
        /// <param name="worldZ">世界格坐标 Z。</param>
        /// <returns>高度（米）。</returns>
        private float SampleHeight(float worldX, float worldZ)
        {
            if (!_biomeBlend.enabled)
            {
                var p0 = GetBiomeParamAt(worldX, worldZ); // 你原来的 fallback（或直接用当前字段组装）
                return SampleHeightWithParam(worldX, worldZ, p0);
            }

            float w = SampleBiomeWeight01(worldX, worldZ);
            var pPlain = _biomeBlend.plain;
            var pMtn = _biomeBlend.mountain;

            float h0 = SampleHeightWithParam(worldX, worldZ, pPlain);
            float h1 = SampleHeightWithParam(worldX, worldZ, pMtn);
            return Mathf.Lerp(h0, h1, w);
        }

        /// <summary>
        /// 采样 rockiness（0..1）：按群系渐变参数采样。
        /// </summary>
        /// <param name="worldX">世界格坐标 X。</param>
        /// <param name="worldZ">世界格坐标 Z。</param>
        /// <param name="p">该点群系参数。</param>
        /// <returns>rockiness 0..1。</returns>
        private float SampleRockiness01(float worldX, float worldZ, BiomeParam p)
        {
            float elevRaw = MathUtils.FBM((worldX + _offElev.x) / Mathf.Max(0.0001f, p.elevScale),
                                     (worldZ + _offElev.y) / Mathf.Max(0.0001f, p.elevScale),
                                     p.elevOctaves, p.elevLacunarity, p.elevPersistence);
            float elev = Normalize01(elevRaw);

            float ridged = 1f - Mathf.Abs(2f * elev - 1f);

            float roughRaw = MathUtils.FBM((worldX + _offRough.x) / Mathf.Max(0.0001f, p.roughScale),
                                      (worldZ + _offRough.y) / Mathf.Max(0.0001f, p.roughScale),
                                      p.roughOctaves, p.roughLacunarity, p.roughPersistence);
            float rough = Normalize01(roughRaw);

            return Mathf.Lerp(elev, ridged, p.ridgedWeight) * 0.7f + rough * 0.3f;
        }

        /// <summary>
        /// 采样 rockiness（0..1），用于材料分类。
        /// </summary>
        /// <param name="worldX">世界格坐标 X。</param>
        /// <param name="worldZ">世界格坐标 Z。</param>
        /// <returns>rockiness 0..1。</returns>
        private float SampleRockiness01(float worldX, float worldZ)
        {
            float elevRaw = MathUtils.FBM((worldX + _offElev.x) / Mathf.Max(0.0001f, elevScale),
                                     (worldZ + _offElev.y) / Mathf.Max(0.0001f, elevScale),
                                     elevOctaves, elevLacunarity, elevPersistence);
            float elev = Normalize01(elevRaw);

            float ridged = 1f - Mathf.Abs(2f * elev - 1f);

            float roughRaw = MathUtils.FBM((worldX + _offRough.x) / Mathf.Max(0.0001f, roughScale),
                                      (worldZ + _offRough.y) / Mathf.Max(0.0001f, roughScale),
                                      roughOctaves, roughLacunarity, roughPersistence);
            float rough = Normalize01(roughRaw);

            return Mathf.Lerp(elev, ridged, ridgedWeight) * 0.7f + rough * 0.3f;
        }

        /// <summary>
        /// 将噪声值稳健映射到 0..1（兼容 [0,1] 或 [-1,1]）。
        /// </summary>
        /// <param name="v">噪声值。</param>
        /// <returns>0..1。</returns>
        private static float Normalize01(float v)
        {
            if (v < 0f || v > 1f) v = v * 0.5f + 0.5f;
            return Mathf.Clamp01(v);
        }
        /// <summary>
        /// 将噪声值稳健映射到 0..1，并过滤 NaN/Infinity。
        /// </summary>
        /// <param name="v">噪声值。</param>
        /// <returns>0..1。</returns>
        private static float Normalize01Safe(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return 0f;
            if (v < 0f || v > 1f) v = v * 0.5f + 0.5f; // 兼容 [-1,1]
            return Mathf.Clamp01(v);
        }
        /// <summary>
        /// 检查必要引用与参数合法性。
        /// </summary>
        /// <returns>是否可生成。</returns>
        private bool CheckRefs()
        {
            if (!landMat || !rockMat)
            {
                GameDebug.LogError("[ChunkMeshGenerator] 请绑定 landMat / rockMat");
                return false;
            }
            if (width <= 0 || height <= 0)
            {
                GameDebug.LogError("[ChunkMeshGenerator] width/height 必须为正数");
                return false;
            }
            if (cellSize <= 0f)
            {
                GameDebug.LogError("[ChunkMeshGenerator] cellSize 必须 > 0");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 未配置世界采样时：由本地 seed 派生偏移（注意：这会导致 chunk 间不连续）。
        /// </summary>
        /// <returns>无。</returns>
        private void PrepareOffsetsFromLocalSeed()
        {
            var rng = new System.Random(seed);
            _offElev = new Vector2(RandLarge(rng), RandLarge(rng));
            _offRough = new Vector2(RandLarge(rng), RandLarge(rng));
            _offWarp = new Vector2(RandLarge(rng), RandLarge(rng));
        }

        /// <summary>
        /// 生成较大范围随机偏移，避免噪声伪周期。
        /// </summary>
        /// <param name="rng">随机源。</param>
        /// <returns>偏移值。</returns>
        private static float RandLarge(System.Random rng)
        {
            return (float)(rng.NextDouble() * 100000.0);
        }
    }

    /// <summary>
    /// Chunk 生成数据包：顶点高度 + 岩石遮罩。
    /// </summary>
    public readonly struct ChunkData
    {
        public readonly int Width;
        public readonly int Height;
        public readonly float CellSize;
        public readonly float[] VertexHeights; // (Width+1)*(Height+1)
        public readonly byte[] RockMask;       // Width*Height

        /// <summary>
        /// 创建 ChunkData。
        /// </summary>
        /// <param name="width">格子宽。</param>
        /// <param name="height">格子高。</param>
        /// <param name="cellSize">单位/格。</param>
        /// <param name="vertexHeights">顶点高度。</param>
        /// <param name="rockMask">岩石遮罩。</param>
        public ChunkData(int width, int height, float cellSize, float[] vertexHeights, byte[] rockMask)
        {
            Width = width;
            Height = height;
            CellSize = cellSize;
            VertexHeights = vertexHeights;
            RockMask = rockMask;
        }
    }
}
