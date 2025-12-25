using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Kernel.GameState;
using Kernel.UI;
using Lonize.Events;
using Lonize.Localization;
using Lonize.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace Kernel.Building
{
    public class BuildingDefHeader
    {
        /// <summary>
        /// 定义的唯一ID。
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 定义类型键，例如 Base、PowerGenerator 等。
        /// </summary>
        public string defType { get; set; }
    }
    public static class BuildingDatabase
    {
        public static readonly Dictionary<string, BuildingDef> Defs = new();
        static readonly Dictionary<string, Type> _defTypeMap = new()
        {
            // 普通建筑
            { "Base", typeof(BuildingDef) },

            // 发电机
            { "PowerGenerator", typeof(Colo.Def.Building.PowerGeneratorDef) },

            // 以后可以在这里继续添加其他子类
            // { "ComputeServer", typeof(Colo.Def.Building.ComputeServerDef) },
        };
        static readonly JsonSerializerSettings _jsonSettings = new()
        {
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// 根据 JSON 中的类型键名解析对应的 BuildingDef 具体类型。
        /// </summary>
        /// <param name="typeKey">JSON 中声明的类型键，例如 "PowerGenerator"。</param>
        /// <returns>解析得到的具体类型；如果失败则回退为 typeof(BuildingDef)。</returns>
        static Type ResolveDefType(string typeKey)
        {
            if (string.IsNullOrEmpty(typeKey))
            {
                return typeof(BuildingDef);
            }

            if (_defTypeMap.TryGetValue(typeKey, out var t))
            {
                return t;
            }
            GameDebug.LogWarning($"[Building] 未知 DefType：{typeKey}，回退为 BuildingDef");
            // Log.Warn($"[Building] 未知 DefType：{typeKey}，回退为 BuildingDef");
            return typeof(BuildingDef);
        }


        /// <summary>
        /// 异步加载所有建筑定义资源。
        /// </summary>
        /// <param name="labelOrGroup">Addressables 中的标签或组名，默认是 "BuildingDef"</param>
        /// <returns></returns>
        public static async Task LoadAllAsync(string labelOrGroup = "BuildingDef")
        {
            Defs.Clear();

            AsyncOperationHandle<IList<IResourceLocation>> locHandle =
                Addressables.LoadResourceLocationsAsync(labelOrGroup, typeof(TextAsset));

            IList<IResourceLocation> locations = null;
            try { locations = await locHandle.Task; }
            catch (System.Exception ex)
            {
                Log.Error($"[Building] 查询 Addressables 失败（{labelOrGroup}）：\n{ex}");
                GameDebug.LogError($"[Building] 查询 Addressables 失败（{labelOrGroup}）：\n{ex}");
                if (locHandle.IsValid()) Addressables.Release(locHandle);
                return;
            }

            if (locations == null || locations.Count == 0)
            {
                GameDebug.LogWarning($"[Building] 未找到任何 TextAsset（{labelOrGroup}）。");
                Log.Warn($"[Building] 未找到任何 TextAsset（{labelOrGroup}）。");
                if (locHandle.IsValid()) Addressables.Release(locHandle);
                return;
            }

            AsyncOperationHandle<IList<TextAsset>> loadHandle =
                Addressables.LoadAssetsAsync<TextAsset>(locations, null, true);

            IList<TextAsset> assets = null;
            try { assets = await loadHandle.Task; }
            catch (System.Exception ex)
            {

                GameDebug.LogError($"[Building] 批量加载 TextAsset 失败：\n{ex}");
                Log.Error($"[Building] 批量加载 TextAsset 失败：\n{ex}");
                if (loadHandle.IsValid()) Addressables.Release(loadHandle);
                if (locHandle.IsValid()) Addressables.Release(locHandle);
                return;
            }


            int total = assets?.Count ?? 0;
            int loadedCount = 0;
            if (total <= 0)
            {
                GlobalLoadingProgress.ReportBuilding(1, 1);
            }
            else
            {
                // 解析每个 TextAsset
                foreach (var ta in assets)
                {
                    try
                    {
                        if (!ta) continue;


                        // 第一步：先读头部，拿到 Id 和 DefType
                        var root = LocalizedJsonUtility.ParseAndLocalize(ta.text, "BuildingDef");
                        var header = root.ToObject<BuildingDefHeader>(JsonSerializer.Create(_jsonSettings));
                        // var root = JObject.Parse(ta.text);
                        
                        if (header == null)
                        {
                            GameDebug.LogError($"[Building] 解析头部失败（资产：{ta.name}）");
                            continue;
                        }

                        // LocalizationManager.TryApplyExternalJsonPatch("BuildingDef",header.Id, root);

                        var targetType = ResolveDefType(header.defType);

                        // 第二步：按具体类型反序列化
                        // var defObj = JsonConvert.DeserializeObject(ta.text, targetType, _jsonSettings);
                        var defObj = root.ToObject(targetType, JsonSerializer.Create(_jsonSettings));
                        
                        if (defObj is not BuildingDef def)
                        {
                            GameDebug.LogError($"[Building] 解析失败，结果不是 BuildingDef（资产：{ta.name}，类型：{targetType}）");
                            continue;
                        }

                        if (BuildingValidation.Validate(def, out var msg))
                        {
                            if (!Defs.TryAdd(def.Id, def))
                            {
                                GameDebug.LogError($"[Building] 重复ID：{def.Id}（资产：{ta.name}）");
                            }
                        }
                        else
                        {
                            GameDebug.LogError($"[Building] 定义非法（资产：{ta.name}）：\n{msg}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        GameDebug.LogError($"[Building] 解析失败（资产：{ta.name}）：\n{ex}");
                    }
                    finally
                    {
                        // ★ 无论成败都算处理完一个资产，更新进度
                        loadedCount++;
                        GlobalLoadingProgress.ReportBuilding(loadedCount, total);
                    }
                }
            }


            if (loadHandle.IsValid()) Addressables.Release(loadHandle);
            if (locHandle.IsValid()) Addressables.Release(locHandle);

            // BuildingEvents.RaiseDatabaseLoaded(Defs.Keys.ToList());
            // Events.eventBus.Publish(new BuildingLoadingProgress(Defs.Keys.Count, Defs.Count));
            Events.eventBus.Publish(new BuildingLoaded(Defs.Keys.Count));
        }

        public static bool TryGet(string id, out BuildingDef def) => Defs.TryGetValue(id, out def);
    }
}
