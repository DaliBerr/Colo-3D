
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Kernel.GameState;
using Kernel.UI;
using Lonize.Events;
using Lonize.Logging;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Kernel.Item
{
    
        public static class ItemDatabase
    {
        public static readonly Dictionary<string, ItemDef> Defs = new();
        static readonly JsonSerializerSettings _jsonSettings = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// 异步加载所有物品定义。
        /// </summary>
        /// <param name="labelOrGroup">Addressables 的标签或分组名。</param>
        /// <returns>异步任务。</returns>
        public static async Task LoadAllAsync(string labelOrGroup = "ItemDef")
        {
            Defs.Clear();

            // 1) 查找所有 TextAsset 资源位置
            AsyncOperationHandle<IList<IResourceLocation>> locHandle =
                Addressables.LoadResourceLocationsAsync(labelOrGroup, typeof(TextAsset));

            IList<IResourceLocation> locations = null;
            try
            {
                locations = await locHandle.Task;
            }
            catch (System.Exception ex)
            {
                GameDebug.LogError($"[Items] 查询 Addressables 失败（Group: {labelOrGroup}）：\n{ex}");
                if (locHandle.IsValid()) Addressables.Release(locHandle);
                GlobalLoadingProgress.ReportItem(1, 1);
                return;
            }

            if (locations == null || locations.Count == 0)
            {
                GameDebug.LogWarning($"[Items] 未在 Addressables 中找到任何 TextAsset（Group: {labelOrGroup}）。");
                if (locHandle.IsValid()) Addressables.Release(locHandle);
                GlobalLoadingProgress.ReportItem(1, 1);
                return;
            }

            // 2) 批量加载这些 TextAsset
            AsyncOperationHandle<IList<TextAsset>> loadHandle =
                Addressables.LoadAssetsAsync<TextAsset>(locations, null, true);

            IList<TextAsset> assets = null;
            try
            {
                assets = await loadHandle.Task;
            }
            catch (System.Exception ex)
            {
                Log.Error($"[Items] 批量加载 TextAsset 失败：\n{ex}");
                GameDebug.LogError($"[Items] 批量加载 TextAsset 失败：\n{ex}");
                if (loadHandle.IsValid()) Addressables.Release(loadHandle);
                if (locHandle.IsValid()) Addressables.Release(locHandle);
                GlobalLoadingProgress.ReportItem(1, 1);
                return;
            }

            // ★ 统计数量并初始化进度
            int total = assets?.Count ?? 0;
            int loadedCount = 0;
            if (total <= 0)
            {
                GlobalLoadingProgress.ReportItem(1, 1);
            }
            else
            {
                // 3) 解析每个 JSON → ItemDef
                foreach (var ta in assets)
                {
                    try
                    {
                        GameDebug.Log($"[Items] Loading ItemDef from asset: {ta.name}");
                        Log.Info($"[Items] Loading ItemDef from asset: {ta?.name}");
                        if (ta == null) continue;
                        
                        var def = JsonConvert.DeserializeObject<ItemDef>(ta.text, _jsonSettings);
                        if (ItemValidation.Validate(def, out var msg))
                        {
                            if (!Defs.TryAdd(def.Id, def))
                            {
                                GameDebug.LogError($"[Items] 重复的物品ID：{def.Id}（资产名：{ta.name}）");
                                Log.Error($"[Items] 重复的物品ID：{def.Id}（资产名：{ta.name}）");
                            }
                        }
                        else
                        {
                            GameDebug.LogError($"[Items] 定义非法（资产名：{ta.name}）：\n{msg}");
                            Log.Error($"[Items] 定义非法（资产名：{ta.name}）：\n{msg}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        GameDebug.LogError($"[Items] 解析失败（资产名：{ta?.name}）：\n{ex}");
                        Log.Error($"[Items] 解析失败（资产名：{ta?.name}）：\n{ex}");
                    }
                    finally
                    {
                        // ★ 同样在 finally 里更新进度
                        loadedCount++;
                        GlobalLoadingProgress.ReportItem(loadedCount, total);
                    }
                }
            }
        }
        public static bool TryGet(string id, out ItemDef def) => Defs.TryGetValue(id, out def);

        public static ItemInstance CreateInstance(string id, int stack = 1)
        {
            if (!Defs.TryGetValue(id, out var def))
            {
                GameDebug.LogError($"[Items] 未找到物品ID：{id}");
                Log.Error($"[Items] 未找到物品ID：{id}");
                return null;
            }
            var inst = new ItemInstance { Def = def, Stack = Mathf.Clamp(stack, 1, def.MaxStack) };
            // 绑定行为（如果需要把IItemBehaviour挂在物品实例上，可在ItemFactory中继续处理）
            return inst;
        }

        public static async Task<Sprite> LoadIconAsync(ItemDef def) =>
            await AddressableRef.LoadAsync<Sprite>(def.IconAddress);

        public static async Task<GameObject> LoadPrefabAsync(ItemDef def) =>
            await AddressableRef.LoadAsync<GameObject>(def.PrefabAddress);

        // --- 工具 ---
        static async Task<string> ReadAllTextAsync(string path)
        {
            // 简单异步读文件，兼容性足够
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
            using var sr = new StreamReader(fs);
            return await sr.ReadToEndAsync();
        }
    }
}