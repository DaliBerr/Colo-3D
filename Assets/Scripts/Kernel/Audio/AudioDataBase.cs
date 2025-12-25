using System.Collections.Generic;
using System.Threading.Tasks;
using Lonize.Logging;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Kernel.Audio
{
    /// <summary>
    /// 音频定义数据库，负责从Addressables加载所有AudioDef。
    /// </summary>
    public static class AudioDatabase
    {
        /// <summary>
        /// 所有已加载的音频定义字典（id -> AudioDef）。
        /// </summary>
        public static readonly Dictionary<string, AudioDef> Defs = new();

        // 与ItemDatabase相同的Json配置
        static readonly JsonSerializerSettings _jsonSettings = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };


        private static Task _loadTask;
        private static bool _loaded;
        /// <summary>
        /// 异步加载所有音频定义，只在第一次真正执行，后续重复调用会等待同一个任务或直接返回。
        /// </summary>
        /// <param name="labelOrGroup">Addressables中的标签或组名。</param>
        /// <returns>异步任务。</returns>
        public static Task LoadAllAsync(string labelOrGroup = "AudioDef")
        {
            // 已经加载过且有数据，直接返回完成任务
            if (_loaded && Defs.Count > 0)
            {
                return _loadTask ?? Task.CompletedTask;
            }

            // 已经有一个加载任务在跑，后续调用复用同一个Task，避免并发。
            if (_loadTask != null)
            {
                return _loadTask;
            }

            // 第一次真正触发加载
            _loadTask = InnerLoadAllAsync(labelOrGroup);
            return _loadTask;
        }

        /// <summary>
        /// 异步加载所有AudioDef JSON。
        /// </summary>
        /// <param name="labelOrGroup">Addressables中的label或group名，默认AudioDef。</param>
        /// <returns>异步任务。</returns>
        private static async Task InnerLoadAllAsync(string labelOrGroup = "AudioDef")
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
                GameDebug.LogError($"[Audio] 查询 Addressables 失败（Group: {labelOrGroup}）：\n{ex}");
                if (locHandle.IsValid()) Addressables.Release(locHandle);
                return;
            }

            if (locations == null || locations.Count == 0)
            {
                GameDebug.LogWarning($"[Audio] 未在 Addressables 中找到任何 TextAsset（Group: {labelOrGroup}）。");
                if (locHandle.IsValid()) Addressables.Release(locHandle);
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
                GameDebug.LogError($"[Audio] 批量加载 TextAsset 失败：\n{ex}");
                if (loadHandle.IsValid()) Addressables.Release(loadHandle);
                if (locHandle.IsValid()) Addressables.Release(locHandle);
                return;
            }

            // 3) 解析每个 JSON -> AudioDef
            foreach (var ta in assets)
            {
                if (ta == null) continue;
                GameDebug.Log($"[Audio] Loading AudioDef from asset: {ta.name}");
                try
                {
                    var def = JsonConvert.DeserializeObject<AudioDef>(ta.text, _jsonSettings);
                    if (def == null || string.IsNullOrEmpty(def.Id))
                    {
                        GameDebug.LogError($"[Audio] 定义非法（资产名：{ta.name}）：ID为空。");
                        continue;
                    }

                    if (!Defs.TryAdd(def.Id, def))
                    {
                        GameDebug.LogError($"[Audio] 重复的音频ID：{def.Id}（资产名：{ta.name}）");
                    }
                }
                catch (System.Exception ex)
                {
                    GameDebug.LogError($"[Audio] 解析失败（资产名：{ta?.name}）：\n{ex}");
                }
            }

            // 4) 释放句柄
            if (loadHandle.IsValid()) Addressables.Release(loadHandle);
            if (locHandle.IsValid()) Addressables.Release(locHandle);

            // 5) 如需广播事件，可参照 ItemLoaded 自己加：
            // Events.eventBus.Publish(new AudioDefsLoaded(Defs.Count));
        }

        /// <summary>
        /// 尝试根据ID获取AudioDef。
        /// </summary>
        /// <param name="id">音频ID。</param>
        /// <param name="def">输出的AudioDef。</param>
        /// <returns>找到返回true，否则false。</returns>
        public static bool TryGet(string id, out AudioDef def)
        {
            return Defs.TryGetValue(id, out def);
        }

        /// <summary>
        /// 通过AudioDef异步加载AudioClip。
        /// </summary>
        /// <param name="def">音频定义。</param>
        /// <returns>返回对应的AudioClip，失败为null。</returns>
        public static async Task<AudioClip> LoadClipAsync(AudioDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Address))
                return null;

            return await AddressableRef.LoadAsync<AudioClip>(def.Address);
        }
    }
}
