using System.IO;
using UnityEngine;
// using Lonize.Scribe;
// using Kernel.Save.Items;

using System.Collections.Generic;
using Lonize.Scribe;
using Lonize.Logging;
using Kernel.GameState;
using Kernel.Building;
using static Kernel.World.WorldChunkMeshGenerator;
using Kernel.World;
using Kernel.Storage;

/// <summary>
/// 放到场景里一个物体上；Awake 自动注册多态条目并加载；对外暴露 Save()/Load() 与 Items。
/// </summary>
namespace Kernel
{


    public sealed class ScribeSaveManager : MonoBehaviour
    {
        public static ScribeSaveManager Instance { get; private set; }

        [Header("Save Settings")]
        private string fileName = "save.save";
        [SerializeField] private int version = 1;
        [SerializeField] private bool autoSaveOnQuit = true;

        public PolySaveData Data { get; private set; } = new PolySaveData();
        public string FilePath { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // —— 注册可反序列化的条目类型（白名单）——
            RegisterSaveItems();

            FilePath = Path.Combine(Application.persistentDataPath, fileName);
            // LoadOrCreate();
            Data = new PolySaveData();
            // GameDebug.Log($"[ScribeSaveManager] Ready. Path = {FilePath}");
            
        }
        private void Start()
        {
            // Kernel.Building.BuildingIdGenerator.InitializeFromSave();
        }
        private static void RegisterSaveItems()
        {
            // 在这里注册你允许进存档的条目类型；新增类型时只需补一行
            PolymorphRegistry.Register<SaveBool>("Bool");
            PolymorphRegistry.Register<SaveInt>("Int");
            PolymorphRegistry.Register<SaveFloat>("Float");
            

            PolymorphRegistry.Register<SaveStatus>("StatusNames");
            PolymorphRegistry.Register<SaveAllBuildings>("AllBuildings");
            PolymorphRegistry.Register<SaveMapInfo>("WorldChunkInfo");
            // PolymorphRegistry.Register<SaveStorageInventory>("StorageInventory");

            CodecRegistry.Register(new BoolCodec());
            CodecRegistry.Register(new IntCodec());
            CodecRegistry.Register(new FloatCodec());
            CodecRegistry.Register(new StringCodec());
            CodecRegistry.Register(new LongCodec());
            CodecRegistry.Register(new DictStrEnumInt32Codec<KeyCode>());


            BuildingIDManager.RegisterSaveType();

            // PolymorphRegistry.Register<SaveControlCommand>("ControlCommandPref");
           

            // PolymorphRegistry.Register<SaveDictionaryStringKeyCode>("DictionaryStringKeyCode");
            // PolymorphRegistry.Register<SaveRefThing>("ref-thing");
            // ...
        }

        public void Save(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                FilePath = Path.Combine(Application.persistentDataPath, fileName);
            }
            // Log.Info("[ScribeSaveManager] Save called. Path: "+ FilePath);
            // GameDebug.Log("[ScribeSaveManager] Save called. Path: "+ FilePath);
        

            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var tmp = FilePath + ".tmp";
            try
            {
                using (var fs = File.Create(tmp))
                {
                    Scribe.InitSaving(fs, version);
                    ScribeRefs.Clear(); // 保存侧通常为空
                    var root = Data;
                    Scribe.Look(ref root);
                    Scribe.FinalizeWriting();
                }

                if (File.Exists(FilePath))
                    File.Replace(tmp, FilePath, FilePath + ".bak", ignoreMetadataErrors: true);
                else
                    File.Move(tmp, FilePath);
            }
            catch (System.Exception ex)
            {
                Log.Error($"[ScribeSaveManager] Save failed: {ex}");
                GameDebug.LogError($"[ScribeSaveManager] Save failed: {ex}");
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        public bool Load(string fileName)
        {
            if (!string.IsNullOrEmpty(fileName))
            {
                FilePath = Path.Combine(Application.persistentDataPath, fileName);
            }
        
            var pathToUse = FilePath;
            Log.Info("[ScribeSaveManager] Load called. Path: "+ pathToUse);
            GameDebug.Log("[ScribeSaveManager] Load called. Path: "+ pathToUse);
            bool loadingLegacy = false;
            if (!File.Exists(pathToUse))
            {
                var legacyPath = Path.ChangeExtension(FilePath, ".tlv");
                if (!File.Exists(legacyPath)) return false;
                GameDebug.LogWarning($"[ScribeSaveManager] JSON save missing; attempting legacy load from {legacyPath}.");
                Log.Warn($"[ScribeSaveManager] JSON save missing; attempting legacy load from {legacyPath}.");
                pathToUse = legacyPath;
                loadingLegacy = true;
            }
            try
            {
                using (var fs = File.OpenRead(pathToUse))
                {
                    Scribe.InitLoading(fs);
                    ScribeRefs.Clear();
                    PolySaveData loaded = null;
                    Scribe.Look(ref loaded);
                    ScribeRefs.ResolveAll(); // 条目里若用 Cross-Refs，这一步会生效
                    Scribe.FinalizeLoading();
                    Data = loaded ?? new PolySaveData();
                    return true;
                }
            }
            catch (System.Exception ex)
            {
                GameDebug.LogError($"[ScribeSaveManager] Load failed: {ex}");
                Log.Error($"[ScribeSaveManager] Load failed: {ex}");
                if (loadingLegacy || Path.GetExtension(pathToUse).Equals(".tlv", System.StringComparison.OrdinalIgnoreCase))
                    GameDebug.LogWarning("[ScribeSaveManager] Legacy format load failed. Consider resaving as JSON.");
                    Log.Warn("[ScribeSaveManager] Legacy format load failed. Consider resaving as JSON.");
                Data = new PolySaveData();
                return false;
            }
        }

        public void ResetSave()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
            Data = new PolySaveData();
        }

        private void LoadOrCreate()
        {
            var ok = Load(fileName);
            if (!ok) { Data = new PolySaveData(); Save(fileName); }
        }

        private void OnApplicationQuit()
        {
            if (autoSaveOnQuit) Save(fileName);
        }

        // —— 便捷静态入口 ——
        public static void SaveNow(string fileName) => Instance?.Save(fileName);
        public static bool LoadNow(string fileName) => Instance != null && Instance.Load(fileName);

        // —— 简易操作：增删查 ——（可选）
        public void AddItem(ISaveItem item) => Data.Items.Add(item);
        public IEnumerable<T> GetItems<T>() where T : class, ISaveItem
        {
            foreach (var it in Data.Items) if (it is T t) yield return t;
        }
        public void RemoveItems<T>(System.Predicate<T> pred = null) where T : class, ISaveItem
        {
            Data.Items.RemoveAll(it => it is T t && (pred == null || pred(t)));
        }
    }
}