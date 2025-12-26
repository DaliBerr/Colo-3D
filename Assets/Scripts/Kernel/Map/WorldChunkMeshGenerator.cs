using Lonize;
using Lonize.Logging;
using UnityEngine;

namespace Kernel.World
{
    /// <summary>
    /// 世界生成器：创建多个 Chunk，并用“世界空间采样”保证 chunk 间无缝拼接。
    /// </summary>
    public class WorldChunkMeshGenerator : MonoBehaviour
    {
        [Header("World Layout（世界网格尺寸）")]
        [Min(1)] public int worldCols = 3;
        [Min(1)] public int worldRows = 3;

        [Header("Chunk Size（小地图尺寸/格）")]
        [Min(4)] public int chunkWidth = 100;
        [Min(4)] public int chunkHeight = 100;

        [Header("Chunk Scale（单位/格）")]
        [Min(0.01f)] public float cellSize = 1f;

        [Header("Chunk Spacing（小地图间距/格）")]
        public Vector2Int chunkSpacing = new Vector2Int(0, 0); // 想无缝就必须 0

        [Header("Materials")]
        public Material landMat;
        public Material rockMat;

        [Header("Seed")]
        public int worldSeed = 123456;
        public bool randomizeSeedOnPlay = false;

        [Header("Biome Presets（可选：仍然按 chunk 选群系）")]
        public bool useChunkBiomeOverrides = false; // 先关掉，保证跨 chunk 参数一致最稳
        [Header("Biome Blend（群系渐变）")]
        [Range(0.01f, 0.35f)] public float biomeBlendWidth = 0.12f;
        [System.Serializable]
        public class BiomeOverrides
        {
            public bool enableWarp = true;
            public float heightAmplitude = 12f;
            public float heightPower = 1.6f;

            public float elevScale = 160f;
            public int elevOctaves = 5;
            public float elevLacunarity = 2f;
            public float elevPersistence = 0.5f;

            public float roughScale = 60f;
            public int roughOctaves = 3;
            public float roughLacunarity = 2f;
            public float roughPersistence = 0.5f;

            [Range(0f, 1f)] public float ridgedWeight = 0.45f;
            [Range(0f, 1f)] public float rockThreshold = 0.62f;
            [Range(0f, 1f)] public float slopeToRock01 = 0.55f;

            public float warpScale = 120f;
            public int warpOctaves = 3;
            public float warpAmplitude = 18f;
        }

        public BiomeOverrides plain = new BiomeOverrides();
        public BiomeOverrides mountain = new BiomeOverrides { heightAmplitude = 20f, ridgedWeight = 0.7f, rockThreshold = 0.58f };

        [Header("Biome Noise（只有在 useChunkBiomeOverrides=true 时才用）")]
        public float biomeNoiseScale = 0.35f;
        [Range(0f, 1f)] public float mountainThreshold = 0.55f;
        public float biomeWarpScale = 0.6f;
        public float biomeWarpAmp = 0.15f;

        private MapControls mapControls; 
        private void Awake()
        {
            mapControls = InputActionManager.Instance.Map;
            // mapControls.Enable();
        }

        private void Update()
        {
            if (mapControls.GenerateMap.Confirm.triggered)
            {
                GenerateWorld();
            }
        }
        /// <summary>
        /// 生成整个世界（无缝拼接版）。
        /// </summary>
        /// <returns>是否成功生成。</returns>
        [ContextMenu("Generate World (Seamless Chunk Mesh)")]
        public bool GenerateWorld()
        {
            if (!landMat || !rockMat)
            {
                GameDebug.LogError("[WorldChunkMeshGenerator] 请拖入 landMat / rockMat");
                return false;
            }

            if (randomizeSeedOnPlay && Application.isPlaying)
                worldSeed = System.Environment.TickCount;

            ClearWorld();

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 1) 用 worldSeed 生成一次全局 offset（所有 chunk 共用）
            var (offElev, offRough, offWarp) = BuildGlobalOffsets(worldSeed);

            // 2) 群系噪声 offset（可选，仅用于 per-chunk 覆写）
            Vector2 bioff = new Vector2(
                HashTo01(worldSeed * 911382323) * 1000f,
                HashTo01(worldSeed * 15485867) * 1000f
            );
            Vector2 biwarpOff = new Vector2(
                HashTo01(worldSeed * 32452843) * 1000f,
                HashTo01(worldSeed * 49979687) * 1000f
            );

            float chunkWorldW = chunkWidth * cellSize;
            float chunkWorldH = chunkHeight * cellSize;
            float spacingW = chunkSpacing.x * cellSize;
            float spacingH = chunkSpacing.y * cellSize;

            for (int ry = 0; ry < worldRows; ry++)
            for (int cx = 0; cx < worldCols; cx++)
            {
                var chunkRoot = new GameObject($"({cx},{ry})");
                chunkRoot.transform.SetParent(transform, false);

                // 3D：紧贴放在 XZ 平面（spacing=0 才能无缝“看起来连起来”）
                chunkRoot.transform.localPosition = new Vector3(
                    cx * (chunkWorldW + spacingW),
                    0f,
                    ry * (chunkWorldH + spacingH)
                );

                var gen = chunkRoot.AddComponent<ChunkMeshGenerator>();
                gen.width = chunkWidth;
                gen.height = chunkHeight;
                gen.cellSize = cellSize;
                gen.landMat = landMat;
                gen.rockMat = rockMat;

                // 2) 关键：配置世界空间采样，让所有 chunk 在同一噪声域上取值
                Vector2 worldOriginCells = new Vector2(cx * chunkWidth, ry * chunkHeight);
                gen.ConfigureWorldSampling(worldOriginCells, offElev, offRough, offWarp);

                // 3) 可选：你想继续“按 chunk 选群系参数”，也行（但群系交界仍可能出现宏观差异）
                if (useChunkBiomeOverrides)
                {
                    gen.ConfigureBiomeBlend(
                        ToParam(plain),
                        ToParam(mountain),
                        chunkWidth,
                        chunkHeight,
                        biomeNoiseScale,
                        mountainThreshold,
                        biomeBlendWidth,
                        biomeWarpScale,
                        biomeWarpAmp,
                        bioff,
                        biwarpOff
                    );
                    bool isMountain = IsMountainChunk(cx, ry, bioff, biwarpOff);
                    ApplyBiomeOverrides(gen, isMountain ? mountain : plain);
                }

                gen.GenerateChunk(publishEvent: true);
            }
            // var chunks = GetComponentsInChildren<GameObject>();
            // foreach (var c in chunks)
            // {
            //     c.gameObject.layer = LayerMask.NameToLayer("Ground");
            // }
            sw.Stop();
            GameDebug.Log($"[WorldChunkMeshGenerator] 世界生成完成：{worldCols}×{worldRows}，耗时 {sw.ElapsedMilliseconds} ms");
            Log.Info($"[WorldChunkMeshGenerator] 世界生成完成：{worldCols}×{worldRows}，耗时 {sw.ElapsedMilliseconds} ms");
            return true;
        }
        /// <summary>
        /// 将 World 的 BiomeOverrides 转换为 ChunkMeshGenerator.BiomeParam。
        /// </summary>
        /// <param name="o">覆写对象。</param>
        /// <returns>BiomeParam。</returns>
        private static ChunkMeshGenerator.BiomeParam ToParam(BiomeOverrides o)
        {
            return new ChunkMeshGenerator.BiomeParam
            {
                enableWarp = o.enableWarp,

                heightAmplitude = o.heightAmplitude,
                heightPower = o.heightPower,

                elevScale = o.elevScale,
                elevOctaves = o.elevOctaves,
                elevLacunarity = o.elevLacunarity,
                elevPersistence = o.elevPersistence,

                roughScale = o.roughScale,
                roughOctaves = o.roughOctaves,
                roughLacunarity = o.roughLacunarity,
                roughPersistence = o.roughPersistence,

                ridgedWeight = o.ridgedWeight,
                rockThreshold = o.rockThreshold,
                slopeToRock01 = o.slopeToRock01,

                warpScale = o.warpScale,
                warpOctaves = o.warpOctaves,
                warpAmplitude = o.warpAmplitude
            };
        }
        /// <summary>
        /// 清空世界：删除该物体下所有 chunk 子对象。
        /// </summary>
        /// <returns>是否成功清理。</returns>
        [ContextMenu("Clear World")]
        public bool ClearWorld()
        {
            var list = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in transform) list.Add(child);

            foreach (var t in list)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) Object.DestroyImmediate(t.gameObject);
                else Object.Destroy(t.gameObject);
#else
                Object.Destroy(t.gameObject);
#endif
            }

            return true;
        }

        /// <summary>
        /// 构造全局 offset（保证同 worldSeed 可复现）。
        /// </summary>
        /// <param name="seed">世界种子。</param>
        /// <returns>三个 offset。</returns>
        private static (Vector2 offElev, Vector2 offRough, Vector2 offWarp) BuildGlobalOffsets(int seed)
        {
            var rng = new System.Random(seed);
            Vector2 offElev = new Vector2(RandLarge(rng), RandLarge(rng));
            Vector2 offRough = new Vector2(RandLarge(rng), RandLarge(rng));
            Vector2 offWarp = new Vector2(RandLarge(rng), RandLarge(rng));
            return (offElev, offRough, offWarp);
        }

        /// <summary>
        /// 生成较大范围随机偏移，避免噪声伪周期。
        /// </summary>
        /// <param name="rng">随机源。</param>
        /// <returns>偏移值。</returns>
        private static float RandLarge(System.Random rng) => (float)(rng.NextDouble() * 100000.0);

        /// <summary>
        /// 判断该 chunk 是否山地（仅用于 per-chunk 覆写）。
        /// </summary>
        /// <param name="cx">chunk x。</param>
        /// <param name="ry">chunk y。</param>
        /// <param name="bioff">群系偏移。</param>
        /// <param name="biwarpOff">群系扭曲偏移。</param>
        /// <returns>是否山地。</returns>
        private bool IsMountainChunk(int cx, int ry, Vector2 bioff, Vector2 biwarpOff)
        {
            Vector2 u = new Vector2(
                (cx + bioff.x) * biomeNoiseScale,
                (ry + bioff.y) * biomeNoiseScale
            );

            float warp = Mathf.PerlinNoise(
                (u.x + biwarpOff.x) * biomeWarpScale,
                (u.y + biwarpOff.y) * biomeWarpScale
            ) * 2f - 1f;

            Vector2 uw = u + new Vector2(warp * biomeWarpAmp, -warp * biomeWarpAmp);
            float n = Mathf.PerlinNoise(uw.x, uw.y);
            return n > mountainThreshold;
        }

        /// <summary>
        /// 应用群系参数覆写到 chunk 生成器。
        /// </summary>
        /// <param name="gen">chunk 生成器。</param>
        /// <param name="o">覆写参数。</param>
        /// <returns>无。</returns>
        private void ApplyBiomeOverrides(ChunkMeshGenerator gen, BiomeOverrides o)
        {
            gen.enableWarp = o.enableWarp;

            gen.heightAmplitude = o.heightAmplitude;
            gen.heightPower = o.heightPower;

            gen.elevScale = o.elevScale;
            gen.elevOctaves = o.elevOctaves;
            gen.elevLacunarity = o.elevLacunarity;
            gen.elevPersistence = o.elevPersistence;

            gen.roughScale = o.roughScale;
            gen.roughOctaves = o.roughOctaves;
            gen.roughLacunarity = o.roughLacunarity;
            gen.roughPersistence = o.roughPersistence;

            gen.ridgedWeight = o.ridgedWeight;
            gen.rockThreshold = o.rockThreshold;
            gen.slopeToRock01 = o.slopeToRock01;

            gen.warpScale = o.warpScale;
            gen.warpOctaves = o.warpOctaves;
            gen.warpAmplitude = o.warpAmplitude;
        }

        /// <summary>
        /// 将整数映射到 0..1 的确定性随机数。
        /// </summary>
        /// <param name="v">输入整数。</param>
        /// <returns>0..1。</returns>
        private static float HashTo01(int v)
        {
            uint x = (uint)v;
            x ^= x >> 17; x *= 0xED5AD4BBu;
            x ^= x >> 11; x *= 0xAC4C1B51u;
            x ^= x >> 15; x *= 0x31848BABu;
            x ^= x >> 14;
            return (x & 0x00FFFFFFu) / 16777215f;
        }
    }
}
