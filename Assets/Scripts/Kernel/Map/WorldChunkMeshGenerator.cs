using System;
using System.Collections.Generic;
using Kernel.Item;
using Lonize;
using Lonize.EventSystem;
using Lonize.Logging;
using Lonize.Math;
using Lonize.Scribe;
using UnityEngine;

namespace Kernel.World
{
    /// <summary>
    /// 世界生成器：创建多个 Chunk，并用“世界空间采样”保证 chunk 间无缝拼接。
    /// </summary>
    public class WorldChunkMeshGenerator : MonoBehaviour
    {
        public static WorldChunkMeshGenerator Instance { get; private set; }
        // private string DebugTag
        // => $"[WorldChunkMeshGenerator name={name} id={GetInstanceID()} scene={gameObject.scene.name}]";

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
        [SerializeField] public int worldSeed = 123456;
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

        private static MapControls mapControls;

        private bool _mainSceneInitialized;
        private static bool _pendingRegenerate;

        private static MapInfo _pendingMapInfo;
        private readonly Dictionary<Vector2Int, ChunkMineralInfo> _chunkMineralInfos = new();
        private void OnEnable()
        {
            // 订阅一次主场景初始化事件
            Lonize.EventSystem.EventManager.eventBus.Subscribe<EventList.MainSceneInitialized>(OnMainSceneInitialized);
        }
        private void OnDisable()
        {
            // 记得取消订阅，避免泄漏/重复回调
            Lonize.EventSystem.EventManager.eventBus.Unsubscribe<EventList.MainSceneInitialized>(OnMainSceneInitialized);
        }
        private void LogState(string stage)
        {
            GameDebug.Log($"[WorldChunkGenerator] {stage} | mainInit={_mainSceneInitialized} pending={_pendingRegenerate}");
        }
        /// <summary>
        /// summary: 接收主场景初始化事件，并在需要时触发挂起的世界重建。
        /// param: e 主场景初始化事件（通常携带是否初始化完成的布尔值）。
        /// return: 无
        /// </summary>
        private void OnMainSceneInitialized(EventList.MainSceneInitialized e)
        {
            // if (e == null) return;

            _mainSceneInitialized = e.isInitialized; // 这里按你事件字段名调整：可能叫 IsInitialized / Value / Success 等
            // GameDebug.Log($"[WorldChunkGenerator] 收到主场景初始化事件，状态：{_mainSceneInitialized}");
            if (!_mainSceneInitialized)
            {
                // GameDebug.Log($"[WorldChunkGenerator] 主场景未初始化，跳过挂起的世界重建。");
                return;
            }
            if (_pendingRegenerate)
            {
                // GameDebug.Log($"[WorldChunkGenerator] 主场景已初始化，开始执行挂起的世界重建...");
                _pendingRegenerate = false;

                // 如果你担心 mapInfo 为空，这里也可以加判断
                GenerateWorld();
            }
            // GameDebug.Log($"[WorldChunkGenerator] 处理主场景初始化事件完成。");
        }

    public void RequestRegenerateAfterMainSceneInit(MapInfo mapInfo)
    {
        // GameDebug.Log($"[WorldChunkGenerator] 挂起世界重建，等待主场景初始化完成...");
        _pendingMapInfo = mapInfo;
        // if (_mainSceneInitialized)
        // {
        //     GenerateWorld();
        //     return;
        // }

        _pendingRegenerate = true;
    }
        

        // public class SaveMapInfo : ISaveItem
        // {
        //     public int worldseed;
        //     public int worldcols;
        //     public int worldrows;
        //     public int chunkwidth;
        //     public int chunkheight;
        //     public float cellsize;
            // public Vector2Int chunkspacing;
        // public string TypeId => "WorldChunkInfo";

        // public void ExposeData()
        // {
        //     SaveMapInfo mapInfo = new SaveMapInfo();
        //     mapInfo.worldseed = worldSeed;
        //     mapInfo.worldcols = worldCols;
        //     mapInfo.worldrows = worldRows;
        //     mapInfo.chunkwidth = chunkWidth;
        //     mapInfo.chunkheight = chunkHeight;
        //     mapInfo.cellsize = cellSize;
        //     Scribe_Values.Look("worldseed",ref worldSeed,114514);
        //     Scribe_Values.Look("worldcols", ref worldCols, 3);  
        //     Scribe_Values.Look("worldrows", ref worldRows, 3);
        //     Scribe_Values.Look("chunkwidth", ref chunkWidth, 100);
        //     Scribe_Values.Look("chunkheight", ref chunkHeight, 100);
        //     Scribe_Values.Look("cellsize", ref cellSize, 1f);


        // }
        public bool isNeedRegenerate(MapInfo mapInfo)
        {
            if(mapInfo.worldseed != worldSeed||
            mapInfo.worldcols != worldCols||
            mapInfo.worldrows != worldRows||
            mapInfo.chunkwidth != chunkWidth||
            mapInfo.chunkheight != chunkHeight||
            mapInfo.cellsize != cellSize)
            {
                return true;
            }
            else if (FindAnyObjectByType<ChunkMeshGenerator>() == null)
            {
                return true;
            }
            
            else
            {
                return false;
            }
        }


        private void Awake()
        {
            mapControls = InputActionManager.Instance.Map;
            if(Instance != this && Instance != null)
            {
                Destroy(gameObject);
            }
            Instance = this;

            DontDestroyOnLoad(gameObject);
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
            GameDebug.Log("[WorldChunkMeshGenerator] 开始生成世界...");
            if (!landMat || !rockMat)
            {
                GameDebug.LogError("[WorldChunkMeshGenerator] 请拖入 landMat / rockMat");
                return false;
            }

            if (randomizeSeedOnPlay && Application.isPlaying)
                worldSeed = System.Environment.TickCount;

            ClearWorld();
            _chunkMineralInfos.Clear();

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

                Vector2Int chunkCoord = new Vector2Int(cx, ry);
                Hash128 mineralSeed = MathUtils.GetChunkMineralSeed(worldSeed, cx, ry, 0, "Mineral");
                ChunkMineralInfo mineralInfo = BuildChunkMineralInfo(chunkCoord, mineralSeed);
                _chunkMineralInfos[chunkCoord] = mineralInfo;

                gen.GenerateChunk();
            }
            // var chunks = GetComponentsInChildren<GameObject>();
            // foreach (var c in chunks)
            // {
            //     c.gameObject.layer = LayerMask.NameToLayer("Ground");
            // }
            sw.Stop();
            GameDebug.Log($"[WorldChunkMeshGenerator] 世界生成完成：{worldCols}×{worldRows}，耗时 {sw.ElapsedMilliseconds} ms");
            Log.Info($"[WorldChunkMeshGenerator] 世界生成完成：{worldCols}×{worldRows}，耗时 {sw.ElapsedMilliseconds} ms");
            
            Lonize.EventSystem.EventManager.eventBus.Publish(new EventList.MapReady(true));
            return true;
        }

        /// <summary>
        /// 获取区块矿物信息。
        /// </summary>
        /// <param name="chunkCoord">区块坐标。</param>
        /// <param name="info">矿物信息。</param>
        /// <returns>是否存在。</returns>
        public bool TryGetChunkMineralInfo(Vector2Int chunkCoord, out ChunkMineralInfo info)
        {
            return _chunkMineralInfos.TryGetValue(chunkCoord, out info);
        }

        /// <summary>
        /// 构建区块矿物信息。
        /// </summary>
        /// <param name="chunkCoord">区块坐标。</param>
        /// <param name="seed">区块矿物种子。</param>
        /// <returns>矿物信息。</returns>
        private static ChunkMineralInfo BuildChunkMineralInfo(Vector2Int chunkCoord, Hash128 seed)
        {
            string mineralItemId = GetChunkMineralItemId(chunkCoord, "sulfide");
            var info = new ChunkMineralInfo
            {
                ChunkCoord = chunkCoord,
                MineralComposition = new Dictionary<string, float>(),
                ProcessingInfo = new MineralProcessingData()
            };

            if (!ItemDatabase.TryGet(mineralItemId, out var def) || def == null)
            {
                return info;
            }

            var rng = CreateDeterministicRandom(seed);
            info.MineralComposition = BuildMineralComposition(def.MineralComposition, rng);
            info.ProcessingInfo = BuildProcessingInfo(def.ProcessingInfo, rng);
            return info;
        }

        /// <summary>
        /// 获取区块对应矿物物品ID。
        /// </summary>
        /// <param name="chunkCoord">区块坐标。</param>
        /// <param name="mineralType">矿物类型。</param>
        /// <returns>矿物物品ID。</returns>
        private static string GetChunkMineralItemId(Vector2Int chunkCoord, string mineralType)
        {
            return "raw_ore";
        }

        /// <summary>
        /// 基于范围生成矿物成分。
        /// </summary>
        /// <param name="ranges">成分范围。</param>
        /// <param name="rng">随机源。</param>
        /// <returns>成分结果。</returns>
        private static Dictionary<string, float> BuildMineralComposition(
            Dictionary<string, FloatRange> ranges,
            System.Random rng)
        {
            var result = new Dictionary<string, float>();
            if (ranges == null || ranges.Count == 0)
            {
                result["Stone"] = 1f;
                return result;
            }

            float sum = 0f;
            foreach (var entry in ranges)
            {
                if (string.Equals(entry.Key, "Stone", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                float value = SampleRange(entry.Value, rng);
                result[entry.Key] = value;
                sum += value;
            }

            float stone = Mathf.Clamp01(1f - sum);
            result["Stone"] = stone;
            return result;
        }

        /// <summary>
        /// 基于范围生成加工属性。
        /// </summary>
        /// <param name="info">加工属性范围。</param>
        /// <param name="rng">随机源。</param>
        /// <returns>加工属性数据。</returns>
        private static MineralProcessingData BuildProcessingInfo(MineralProcessingInfo info, System.Random rng)
        {
            if (info == null)
            {
                return new MineralProcessingData();
            }

            return new MineralProcessingData
            {
                Magnetism = SampleRange(info.Magnetism, rng),
                MineralType = info.MineralType,
                ParticleSize = SampleRange(info.ParticleSize, rng),
                Floatability = SampleRange(info.Floatability, rng),
                Leachability = SampleRange(info.Leachability, rng),
                AssociatedMineralId = info.AssociatedMineralId
            };
        }

        /// <summary>
        /// 生成确定性随机源。
        /// </summary>
        /// <param name="seed">Hash128 种子。</param>
        /// <returns>随机源。</returns>
        private static System.Random CreateDeterministicRandom(Hash128 seed)
        {
            return new System.Random(seed.GetHashCode());
        }

        /// <summary>
        /// 根据范围采样随机值。
        /// </summary>
        /// <param name="range">范围数据。</param>
        /// <param name="rng">随机源。</param>
        /// <returns>采样值。</returns>
        private static float SampleRange(FloatRange range, System.Random rng)
        {
            double t = rng.NextDouble();
            return (float)(range.Min + (range.Max - range.Min) * t);
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
    public class MapInfo
    {
        public int worldseed;
        public int worldcols;
        public int worldrows;
        public int chunkwidth;
        public int chunkheight;
        public float cellsize;

    }
    public class SaveMapInfo:ISaveItem
    {
        public static MapInfo mapInfo = new MapInfo();

        public string TypeId => "WorldChunkInfo";

        public void ExposeData()
        {
            if(Scribe.mode == ScribeMode.Saving)
            {
                mapInfo.worldseed = WorldChunkMeshGenerator.Instance.worldSeed;
                mapInfo.worldcols = WorldChunkMeshGenerator.Instance.worldCols;
                mapInfo.worldrows = WorldChunkMeshGenerator.Instance.worldRows;
                mapInfo.chunkwidth = WorldChunkMeshGenerator.Instance.chunkWidth;
                mapInfo.chunkheight = WorldChunkMeshGenerator.Instance.chunkHeight;
                mapInfo.cellsize = WorldChunkMeshGenerator.Instance.cellSize;
            }
            Scribe_Values.Look("worldseed",ref mapInfo.worldseed,114514);
            Scribe_Values.Look("worldcols", ref mapInfo.worldcols, 3);  
            Scribe_Values.Look("worldrows", ref mapInfo.worldrows, 3);
            Scribe_Values.Look("chunkwidth", ref mapInfo.chunkwidth, 100);
            Scribe_Values.Look("chunkheight", ref mapInfo.chunkheight, 100);
            Scribe_Values.Look("cellsize", ref mapInfo.cellsize, 1f);

            if(Scribe.mode == ScribeMode.Loading)
            {
                if (WorldChunkMeshGenerator.Instance.isNeedRegenerate(mapInfo))
                {
                    WorldChunkMeshGenerator.Instance.RequestRegenerateAfterMainSceneInit(mapInfo);
                }
                else
                {
                    GameDebug.Log("[WorldChunkMeshGenerator] 存档参数与当前世界一致，无需重建世界。");
                    Lonize.EventSystem.EventManager.eventBus.Publish(new EventList.MapReady(true));
                }
            
                GameDebug.Log($"[SaveMapInfo] Loaded Map Info: seed={mapInfo.worldseed}, cols={mapInfo.worldcols}, rows={mapInfo.worldrows}, chunkW={mapInfo.chunkwidth}, chunkH={mapInfo.chunkheight}, cellSize={mapInfo.cellsize}");
                WorldChunkMeshGenerator.Instance.worldSeed = mapInfo.worldseed;
                WorldChunkMeshGenerator.Instance.worldCols = mapInfo.worldcols;
                WorldChunkMeshGenerator.Instance.worldRows = mapInfo.worldrows;
                WorldChunkMeshGenerator.Instance.chunkWidth = mapInfo.chunkwidth;
                WorldChunkMeshGenerator.Instance.chunkHeight = mapInfo.chunkheight;
                WorldChunkMeshGenerator.Instance.cellSize = mapInfo.cellsize;

            }
        }


    }
}
