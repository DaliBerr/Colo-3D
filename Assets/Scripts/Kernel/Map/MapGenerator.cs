// using UnityEngine;
// using UnityEngine.Tilemaps;
// using Lonize.Events;
// using Lonize.Math;
// using Lonize.Logging;
// using Lonize;

// namespace Kernel
// {
//     public class MapGenerator : MonoBehaviour
//     {
//         [Header("Map Size")]
//         [Min(1)] public int width = 200;
//         [Min(1)] public int height = 200;

//         [Header("Tilemaps & Tiles")]
//         public Tilemap landTilemap;
//         public Tilemap rockTilemap;
//         public TileBase landTile;
//         public TileBase rockTile;

//         [Header("Seed")]
//         public int seed = 12345;
//         public bool randomizeSeedOnPlay = false;

//         [Header("Noise - Elevation (低频地势)")]
//         [Min(0.0001f)] public float elevScale = 160f;   // 数值越大，地形越“宽”（低频）
//         [Range(1, 8)] public int elevOctaves = 5;
//         [Min(1.1f)] public float elevLacunarity = 2f;
//         [Range(0.1f, 0.9f)] public float elevPersistence = 0.5f;

//         [Header("Noise - Roughness (中高频粗糙)")]
//         [Min(0.0001f)] public float roughScale = 60f;
//         [Range(1, 6)] public int roughOctaves = 3;
//         [Min(1.1f)] public float roughLacunarity = 2f;
//         [Range(0.1f, 0.9f)] public float roughPersistence = 0.5f;

//         [Header("Domain Warp (域扭曲让山脉更自然)")]
//         public bool enableWarp = true;
//         [Min(0.0001f)] public float warpScale = 120f;
//         [Range(1, 5)] public int warpOctaves = 3;
//         [Min(0f)] public float warpAmplitude = 18f; // 扭曲强度（单位：格）

//         [Header("Classifier (阈值与混合)")]
//         [Range(0f, 1f)] public float rockThreshold = 0.62f; // > 即判为岩石
//         [Range(0f, 1f)] public float ridgedWeight = 0.45f;  // 山脊感权重（0 只看平滑地势，1 强调山脊）

//         [Header("Extras")]
//         public bool compressBoundsOnDone = true;
//         public bool logTime = true;

//         // ---- 内部偏移（由种子派生，确保可复现）----
//         private Vector2 _offElev;
//         private Vector2 _offRough;
//         private Vector2 _offWarp;
//         // private Image test;

//         private DevControls devControls;

//         void Awake()
//         {
//             devControls = new DevControls();
//             if (Application.isEditor)
//             {
//                 devControls.Enable();
//             }
//         }

//         // Start is called once before the first execution of Update after the MonoBehaviour is created
//         void Start()
//         {
//             GenerateMap();
//         }

//         // Update is called once per frame
//         public void GenerateMap()
//         {
//             if (!CheckRefs()) return;
//             PrepareOffsets();
//             ClearAll();

//             var sw = System.Diagnostics.Stopwatch.StartNew();

//             // 逐格生成（简单清晰）；地图不大时足够快
//             for (int z = 0; z < height; z++)
//             {
//                 for (int x = 0; x < width; x++)
//                 {
//                     // 1) 可选：域扭曲坐标
//                     float wx = x, wz = z;
//                     if (enableWarp && warpAmplitude > 0f)
//                     {
//                         float w = MathUtils.FBM((x + _offWarp.x) / warpScale, (z + _offWarp.y) / warpScale,
//                         warpOctaves, 2f, 0.5f);
//                         // 映射到 [-1,1]
//                         w = w * 2f - 1f;
//                         wx += w * warpAmplitude;
//                         wz -= w * warpAmplitude;
//                     }

//                     // 2) 采样低频地势（0..1）
//                     float elev = MathUtils.FBM((wx + _offElev.x) / elevScale, (wz + _offElev.y) / elevScale,
//                     elevOctaves, elevLacunarity, elevPersistence);

//                     // 3) 派生“ridged”形态（凸显山脊/悬崖感）
//                     //    elev 在 0..1，上凸处理：1 - |2e-1| 依中心 0.5 为脊
//                     float ridged = 1f - Mathf.Abs(2f * elev - 1f);

//                     // 4) 中高频粗糙，提供断裂/裸岩的局部触发
//                     float rough = MathUtils.FBM((wx + _offRough.x) / roughScale, (wz + _offRough.y) / roughScale,
//                     roughOctaves, roughLacunarity, roughPersistence);

//                     // 5) 组合评分：越大越“像岩石”
//                     float rockiness = Mathf.Lerp(elev, ridged, ridgedWeight) * 0.7f + rough * 0.3f;

//                     bool isRock = rockiness >= rockThreshold;
//                     var cell = new Vector3Int(x, z, 0);

//                     if (isRock)
//                     {
//                         rockTilemap.SetTile(cell, rockTile);
//                         // 土地层不要重叠
//                     }
//                     else
//                     {
//                         landTilemap.SetTile(cell, landTile);
//                     }
//                 }
//             }

//             if (compressBoundsOnDone)
//             {
//                 landTilemap.CompressBounds();
//                 rockTilemap.CompressBounds();
//             }

//             sw.Stop();
//             if (logTime)
//                 GameDebug.Log($"[TilemapMapGenerator] 生成完成：{width}×{height}，耗时 {sw.ElapsedMilliseconds} ms");
//                 Log.Info($"[TilemapMapGenerator] 生成完成：{width}×{height}，耗时 {sw.ElapsedMilliseconds} ms");
//             Events.eventBus.Publish(new MapReady(true, new Vector3(this.transform.position.x, this.transform.position.y, 0)));
//             // Log.Info($"This is Central Position: {this.transform.position}");
//         }

//         [ContextMenu("Clear All")]
//         public void ClearAll()
//         {
//             if (landTilemap) landTilemap.ClearAllTiles();
//             if (rockTilemap) rockTilemap.ClearAllTiles();
//         }

//         // 支持运行时按 G 重新生成
//         private void Update()
//         {
//             if (Application.isPlaying && devControls.Map.RegenerateMiniMap.IsPressed())
//             {
//                 if (randomizeSeedOnPlay) seed = System.Environment.TickCount;
//                 GenerateMap();
//             }
//         }

//         // ====== 工具函数 ======

//         private bool CheckRefs()
//         {
//             if (!landTilemap || !rockTilemap || !landTile || !rockTile)
//             {
//                 GameDebug.LogError("[TilemapMapGenerator] 请在 Inspector 里绑定 Land/Rock Tilemap 与 LandTile/RockTile");
//                 return false;
//             }
//             if (width <= 0 || height <= 0)
//             {
//                 GameDebug.LogError("[TilemapMapGenerator] width/height 必须为正数");
//                 return false;
//             }
//             return true;
//         }

//         private void PrepareOffsets()
//         {
//             var rng = new Lonize.Random(seed);
//             _offElev = new Vector2(RandLarge(rng), RandLarge(rng));
//             _offRough = new Vector2(RandLarge(rng), RandLarge(rng));
//             _offWarp = new Vector2(RandLarge(rng), RandLarge(rng));
//         }

//         private static float RandLarge(Lonize.Random rng)
//         {
//             // 在较大的坐标偏移空间里采样，避免与原点相关的 Perlin 伪周期
//             return (float)(rng.NextDouble() * 100000.0);
//         }


//     }
// }
