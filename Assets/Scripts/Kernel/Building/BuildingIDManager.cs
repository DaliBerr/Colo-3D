using System;
using UnityEngine;
using System.Linq;
using Lonize.Scribe;
using System.Collections.Generic;
using Lonize.Logging;

namespace Kernel.Building
{
    /// <summary>
    /// 统一管理 Building 的自增 ID，并通过 Scribe 系统持久化。
    /// </summary>
    public static class BuildingIDManager
    {
        // 存档里用于识别这个条目的 TypeId
        private const string SaveItemTypeId = "building-id-counter";

        // 下一次要用的 ID（内存中的真实计数器）
        private static long _nextBuildingID = 1;

        // 对应的存档条目对象（加载后会指向同一个实例）
        public static BuildingIdSaveData _saveItem;

        private static bool _initialized;

        // -------------------- Local ID（父节点内唯一） --------------------
        private static readonly Dictionary<long, int> _nextLocalIdByParent = new();

        /// <summary>
        /// summary: 生成一个新的建筑全局ID（long），并更新存档条目中的计数值。
        /// param: 无
        /// return: 新生成的全局建筑ID
        /// </summary>
        public static long GenerateBuildingID()
        {
            if (_saveItem == null)
            {
                _saveItem = new();
            }

            EnsureInitialized();
            GameDebug.Log($"Generating Building ID: {_nextBuildingID}");

            long id = _nextBuildingID++;
            if (_saveItem != null)
            {
                // 保存“下一次要使用的 ID”
                _saveItem.nextId = _nextBuildingID;
            }
            return id;
        }

        /// <summary>
        /// summary: 为指定父节点生成一个新的本地ID（localID），仅保证在该父节点内部唯一。
        /// param: parentBuildingId 父节点的全局ID（必须 > 0）
        /// param: startValue localID起始值（默认1）
        /// return: 新生成的localID
        /// </summary>
        public static int GenerateLocalBuildingID(long parentBuildingId, int startValue = 1)
        {
            if (parentBuildingId <= 0)
            {
                GameDebug.LogError($"[BuildingIDManager] GenerateLocalBuildingID: parentBuildingId 非法: {parentBuildingId}");
                return -1;
            }

            int safeStart = Math.Max(startValue, 1);

            if (!_nextLocalIdByParent.TryGetValue(parentBuildingId, out int nextLocal))
            {
                nextLocal = safeStart;
            }

            if (nextLocal == int.MaxValue)
            {
                throw new OverflowException($"[BuildingIDManager] localID 溢出: parent={parentBuildingId} 已达 int.MaxValue");
            }

            int id = nextLocal;
            _nextLocalIdByParent[parentBuildingId] = nextLocal + 1;
            return id;
        }

        /// <summary>
        /// summary: 初始化/同步某个父节点的localID发号器（通常在加载存档后调用）。
        /// param: parentBuildingId 父节点全局ID（必须 > 0）
        /// param: nextLocalId 下一个将要分配的localID（>=1）
        /// return: 无
        /// </summary>
        public static void SetNextLocalBuildingID(long parentBuildingId, int nextLocalId)
        {
            if (parentBuildingId <= 0)
            {
                GameDebug.LogError($"[BuildingIDManager] SetNextLocalBuildingID: parentBuildingId 非法: {parentBuildingId}");
                return;
            }

            _nextLocalIdByParent[parentBuildingId] = Math.Max(nextLocalId, 1);
        }

        /// <summary>
        /// summary: 尝试获取某个父节点当前的nextLocalId（便于调试或存档）。
        /// param: parentBuildingId 父节点全局ID
        /// param: nextLocalId 输出下一个将要分配的localID
        /// return: 是否存在该父节点的localID上下文
        /// </summary>
        public static bool TryGetNextLocalBuildingID(long parentBuildingId, out int nextLocalId)
        {
            return _nextLocalIdByParent.TryGetValue(parentBuildingId, out nextLocalId);
        }

        /// <summary>
        /// summary: 释放某个父节点的localID上下文（父节点销毁/卸载时可调用，避免字典增长）。
        /// param: parentBuildingId 父节点全局ID
        /// return: 是否成功移除
        /// </summary>
        public static bool ReleaseLocalIdContext(long parentBuildingId)
        {
            return _nextLocalIdByParent.Remove(parentBuildingId);
        }

        /// <summary>
        /// summary: 根据已存在的localID集合计算“下一个可用的nextLocalId”（max+1）。
        /// param: usedLocalIds 已被占用的localID集合（可为null）
        /// param: startValue 起始值（默认1）
        /// return: 计算得到的nextLocalId（>=1）
        /// </summary>
        public static int ComputeNextLocalIdFromUsed(IEnumerable<int> usedLocalIds, int startValue = 1)
        {
            int safeStart = Math.Max(startValue, 1);
            if (usedLocalIds == null) return safeStart;

            int max = safeStart - 1;
            foreach (var id in usedLocalIds)
            {
                if (id > max) max = id;
            }

            if (max == int.MaxValue)
            {
                throw new OverflowException("[BuildingIDManager] ComputeNextLocalIdFromUsed: 已存在localID达到 int.MaxValue");
            }

            return max + 1;
        }

        /// <summary>
        /// summary: 清空所有localID上下文（新游戏/回主菜单/重载时可用）。
        /// param: 无
        /// return: 无
        /// </summary>
        public static void ResetAllLocalContexts()
        {
            _nextLocalIdByParent.Clear();
        }

        /// <summary>
        /// summary: 初始化：从当前存档中读取全局计数器，没有的话就创建一个新的条目。
        /// param: _savedNextId 从存档读取到的nextId（<=0表示无效）
        /// return: 无
        /// </summary>
        public static void InitializeFromSave(long _savedNextId = -1)
        {
            GameDebug.Log("BuildingIdGenerator.InitializeFromSave called." + _savedNextId);
            if (_savedNextId > 0)
            {
                _nextBuildingID = Math.Max(_savedNextId, 1L);
                _initialized = true;
                return;
            }
            else
            {
                _nextBuildingID = 1L;
                _initialized = true;
                return;
            }
        }

        /// <summary>
        /// summary: 重置全局计数器（例如新游戏时从 1 开始）。
        /// param: startValue 起始值（默认1）
        /// return: 无
        /// </summary>
        public static void Reset(long startValue = 1)
        {
            _nextBuildingID = Math.Max(startValue, 1L);
            if (_saveItem != null)
            {
                _saveItem.nextId = _nextBuildingID;
            }
        }

        /// <summary>
        /// summary: 提供给 ScribeSaveManager 调用，用于注册这个条目类型到多态系统。
        /// param: 无
        /// return: 无
        /// </summary>
        public static void RegisterSaveType()
        {
            PolymorphRegistry.Register<BuildingIdSaveData>(SaveItemTypeId);
        }

        // —— 内部工具 —— //
        /// <summary>
        /// summary: 确保全局ID管理器已初始化（兜底，不做硬初始化逻辑）。
        /// param: 无
        /// return: 无
        /// </summary>
        private static void EnsureInitialized()
        {
            if (!_initialized)
            {
                // 兜底：避免忘记初始化导致炸裂；这里保持你原来的行为
            }
        }

        /// <summary>
        /// summary: 存档条目：实际存储“下一次要使用的全局ID”。
        /// param: 无
        /// return: 无
        /// </summary>
        [Serializable]
        public class BuildingIdSaveData : ISaveItem
        {
            public string TypeId => SaveItemTypeId;

            public long nextId;

            /// <summary>
            /// summary: Scribe序列化入口：保存/加载 nextId，并在加载时初始化全局计数器。
            /// param: 无
            /// return: 无
            /// </summary>
            public void ExposeData()
            {
                Scribe_Values.Look(TypeId, ref nextId, -1L);

                if (Scribe.mode == ScribeMode.Loading)
                {
                    InitializeFromSave(nextId);
                    BuildingIDManager._saveItem = this;
                }
            }
        }
    }
}
