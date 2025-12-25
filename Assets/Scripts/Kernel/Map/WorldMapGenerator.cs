// using System;
// using Lonize;
// using Lonize.Logging;
// using UnityEngine;
// using UnityEngine.EventSystems;
// using UnityEngine.Tilemaps;


// namespace Kernel
// {
//     public class WorldMapGenerator : MonoBehaviour
//     {

//         private event EventHandler<bool> _mapLoaded;

//         [Header("World Layout（世界网格尺寸）")]
//         [Min(1)] public int worldCols = 3;
//         [Min(1)] public int worldRows = 3;

//         [Header("Chunk Size（小地图尺寸/格）")]
//         [Min(4)] public int chunkWidth = 100;
//         [Min(4)] public int chunkHeight = 100;

//         [Header("Chunk Spacing（小地图之间的间距/格）")]
//         public Vector2Int chunkSpacing = new Vector2Int(8, 8);

//         [Header("Tiles（地块素材）")]
//         public TileBase landTile;
//         public TileBase rockTile;

//         [Header("Seed（世界种子）")]
//         public int worldSeed = 123456;
//         public bool randomizeSeedOnPlay = false;

//         [Header("Biome Noise（用噪声给每个格子选群系）")]
//         [Tooltip("越大越‘平滑’。建议 0.1~0.4。")]
//         public float biomeNoiseScale = 0.35f;
//         [Range(0f, 1f)] public float mountainThreshold = 0.55f; // >阈值为“山脉”，否则“平原”
//         public float biomeWarpScale = 0.6f;  // 群系域扭曲（轻度）
//         public float biomeWarpAmp = 0.15f;

//         [Header("默认小地图参数（在群系覆写前的基线）")]
//         public MapGenerator defaultChunkParams;

//         [System.Serializable]
//         public class BiomeOverrides
//         {
//             [Header("启用域扭曲")]
//             public bool enableWarp = true;

//             [Header("Elevation（低频地势）")]
//             public float elevScale = 200f;
//             public int elevOctaves = 5;
//             public float elevLacunarity = 2f;
//             public float elevPersistence = 0.5f;

//             [Header("Roughness（中高频粗糙）")]
//             public float roughScale = 70f;
//             public int roughOctaves = 3;
//             public float roughLacunarity = 2f;
//             public float roughPersistence = 0.5f;

//             [Header("Ridged & Threshold（山脊/阈值）")]
//             [Range(0f, 1f)] public float ridgedWeight = 0.45f;
//             [Range(0f, 1f)] public float rockThreshold = 0.62f;

//             [Header("Domain Warp（域扭曲）")]
//             public float warpScale = 120f;
//             public int warpOctaves = 3;
//             public float warpAmplitude = 16f;
//         }

//         [Header("Biome Presets（群系预设）")]
//         public BiomeOverrides plain = new BiomeOverrides
//         {
//             enableWarp = true,
//             elevScale = 160f,
//             elevOctaves = 5,
//             elevLacunarity = 2f,
//             elevPersistence = 0.5f,
//             roughScale = 60f,
//             roughOctaves = 3,
//             roughLacunarity = 2f,
//             roughPersistence = 0.5f,
//             ridgedWeight = 0.45f,
//             rockThreshold = 0.62f,
//             warpScale = 120f,
//             warpOctaves = 3,
//             warpAmplitude = 18f
//         };
//         public BiomeOverrides mountain = new BiomeOverrides
//         {
//             enableWarp = true,
//             elevScale = 140f,
//             elevOctaves = 5,
//             elevLacunarity = 2f,
//             elevPersistence = 0.5f,
//             roughScale = 50f,
//             roughOctaves = 4,
//             roughLacunarity = 2f,
//             roughPersistence = 0.5f,
//             ridgedWeight = 0.70f,
//             rockThreshold = 0.58f,
//             warpScale = 100f,
//             warpOctaves = 3,
//             warpAmplitude = 20f
//         };


//         private DevControls devControls;

//         private void Awake()
//         {
//             devControls = new DevControls();
//             if (Application.isEditor)
//             {
//                 devControls.Enable();
//             }
//         }

//         // ==== 生成与清空 ====
//         [ContextMenu("Generate World")]
//         public void GenerateWorld()
//         {
//             if (!landTile || !rockTile)
//             {
//                 GameDebug.LogError("[WorldMapGenerator] 请拖入 LandTile / RockTile ");
//                 // Debug.LogError("[WorldMapGenerator] 请拖入 LandTile / RockTile 喵！");
//                 return;
//             }
//             if (defaultChunkParams == null)
//             {
//                 // 创建一个临时 Scriptable-like 容器放默认参数（可选）
//                 defaultChunkParams = gameObject.AddComponent<MapGenerator>();
//                 defaultChunkParams.hideFlags = HideFlags.HideInInspector;
//             }
//             if (randomizeSeedOnPlay && Application.isPlaying)
//             {
//                 worldSeed = System.Environment.TickCount;
//             }

//             ClearWorld(); // 先清空旧的子对象

//             var sw = System.Diagnostics.Stopwatch.StartNew();

//             // 群系噪声偏移（来自世界种子，可复现）
//             Vector2 bioff = new Vector2(
//                 HashTo01(worldSeed * 911382323) * 1000f,
//                 HashTo01(worldSeed * 15485867) * 1000f
//             );
//             Vector2 biwarpOff = new Vector2(
//                 HashTo01(worldSeed * 32452843) * 1000f,
//                 HashTo01(worldSeed * 49979687) * 1000f
//             );

//             for (int ry = 0; ry < worldRows; ry++)
//                 for (int cx = 0; cx < worldCols; cx++)
//                 {
//                     // 1) 判定群系（平原/山脉）
//                     Vector2 u = new Vector2(
//                         (cx + bioff.x) * biomeNoiseScale,
//                         (ry + bioff.y) * biomeNoiseScale
//                     );

//                     // 轻度域扭曲：让群系边界更自然
//                     float warp = Mathf.PerlinNoise(
//                         (u.x + biwarpOff.x) * biomeWarpScale,
//                         (u.y + biwarpOff.y) * biomeWarpScale
//                     ) * 2f - 1f;
//                     Vector2 uw = u + new Vector2(warp * biomeWarpAmp, -warp * biomeWarpAmp);

//                     float n = Mathf.PerlinNoise(uw.x, uw.y); // 0..1
//                     bool isMountain = n > mountainThreshold;

//                     // 2) 为该格子创建一个 “(x,y)” 小地图对象（含 Grid+Tilemaps）
//                     var chunkRoot = new GameObject($"({cx},{ry})");
//                     chunkRoot.transform.SetParent(transform, false);

//                     // 让每个小地图在场景里分开展示
//                     var offset = new Vector3(
//                         cx * (chunkWidth + chunkSpacing.x),
//                         ry * (chunkHeight + chunkSpacing.y),
//                         0f
//                     );
//                     chunkRoot.transform.localPosition = offset;

//                     // Grid 容器
//                     var grid = chunkRoot.AddComponent<Grid>();
//                     grid.cellSize = Vector3.one;

//                     // 两张 Tilemap
//                     var landGO = new GameObject("Land");
//                     landGO.transform.SetParent(chunkRoot.transform, false);
//                     var landTM = landGO.AddComponent<Tilemap>();
//                     landGO.AddComponent<TilemapRenderer>();

//                     var rockGO = new GameObject("Rock");
//                     rockGO.transform.SetParent(chunkRoot.transform, false);
//                     var rockTM = rockGO.AddComponent<Tilemap>();
//                     rockGO.AddComponent<TilemapRenderer>();

//                     // 3) 挂上小地图生成器，并注入素材与尺寸
//                     var gen = chunkRoot.AddComponent<MapGenerator>();
//                     gen.width = chunkWidth;
//                     gen.height = chunkHeight;
//                     gen.landTilemap = landTM;
//                     gen.rockTilemap = rockTM;
//                     gen.landTile = landTile;
//                     gen.rockTile = rockTile;

//                     // 为保证每格子既相关又不同：用世界种子 + 坐标派生子种子
//                     gen.seed = (int)Hash32((uint)worldSeed, (uint)cx, (uint)ry);

//                     // 4) 应用群系参数
//                     ApplyBiomeOverrides(gen, isMountain ? mountain : plain);

//                     // 5) 生成
//                     gen.GenerateMap();
//                     _mapLoaded?.Invoke(this, true);
//                 }

//             sw.Stop();
//             GameDebug.Log($"[WorldMapGenerator] 世界生成完成：{worldCols}×{worldRows} 个小地图，耗时 {sw.ElapsedMilliseconds} ms");
//             // Debug.Log($"[WorldMapGenerator] 世界生成完成：{worldCols}×{worldRows} 个小地图，耗时 {sw.ElapsedMilliseconds} ms");
//         }

//         [ContextMenu("Clear World")]
//         public void ClearWorld()
//         {
//             // 删掉本物体下的所有子对象（每个子对象就是一个小地图）
//             var list = new System.Collections.Generic.List<Transform>();
//             foreach (Transform child in transform) list.Add(child);

//             foreach (var t in list)
//             {
// #if UNITY_EDITOR
//                 if (!Application.isPlaying) DestroyImmediate(t.gameObject);
//                 else Destroy(t.gameObject);
//                 GameDebug.Log($"[WorldMapGenerator] 删除小地图对象 {t.gameObject.name}");
//                 // Debug.Log($"[WorldMapGenerator] 删除小地图对象 {t.gameObject.name}");
// #else
//             Destroy(t.gameObject);
//             GameDebug.Log($"[WorldMapGenerator] 删除小地图对象 {t.gameObject.name}");
//             // Debug.Log($"[WorldMapGenerator] 删除小地图对象 {t.gameObject.name}");
// #endif
//             }
//         }

//         private void ApplyBiomeOverrides(MapGenerator gen, BiomeOverrides o)
//         {
//             gen.enableWarp = o.enableWarp;

//             gen.elevScale = o.elevScale;
//             gen.elevOctaves = o.elevOctaves;
//             gen.elevLacunarity = o.elevLacunarity;
//             gen.elevPersistence = o.elevPersistence;

//             gen.roughScale = o.roughScale;
//             gen.roughOctaves = o.roughOctaves;
//             gen.roughLacunarity = o.roughLacunarity;
//             gen.roughPersistence = o.roughPersistence;

//             gen.ridgedWeight = o.ridgedWeight;
//             gen.rockThreshold = o.rockThreshold;

//             gen.warpScale = o.warpScale;
//             gen.warpOctaves = o.warpOctaves;
//             gen.warpAmplitude = o.warpAmplitude;
//         }

//         // ====== 杂项：热键 & 哈希 ======
//         private void Update()
//         {
//             if (devControls.Map.RegenerateWorldMap.IsPressed())
//             {
//                 if (randomizeSeedOnPlay) worldSeed = System.Environment.TickCount;
//                 GenerateWorld();
//             }
//             if (devControls.Map.ClearWorldMap.IsPressed())
//             {
//                 ClearWorld();
//             }
//         }

//         private static uint Hash32(uint a, uint b, uint c)
//         {
//             // 3-元组混合哈希（deterministic），改自 wyhash/murmur 风格
//             uint x = a * 0x9E3779B1u ^ b * 0x85EBCA77u ^ c * 0xC2B2AE3Du;
//             x ^= x >> 16; x *= 0x7FEB352Du;
//             x ^= x >> 15; x *= 0x846CA68Bu;
//             x ^= x >> 16;
//             return x;
//         }
//         private static float HashTo01(int v)
//         {
//             uint x = (uint)v;
//             x ^= x >> 17; x *= 0xED5AD4BBu;
//             x ^= x >> 11; x *= 0xAC4C1B51u;
//             x ^= x >> 15; x *= 0x31848BABu;
//             x ^= x >> 14;
//             return (x & 0x00FFFFFFu) / 16777215f; // 0..1
//         }
//     }
// }