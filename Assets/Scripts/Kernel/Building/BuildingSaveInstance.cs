
using System.Collections.Generic;
using Lonize.Logging;
using Lonize.Scribe;
using UnityEngine;

namespace Kernel.Building
{
    public class SaveBuildingInstance
    {
        public string DefId;
        public long RuntimeId;

        // 位置 & 旋转（基于网格）
        public int CellX;
        public int CellY;
        public byte RotSteps;

        // 状态
        public int HP;

        // 运行时统计（字典拆成两组数组以方便序列化）
        public string[] StatKeys;
        public float[] StatValues;

        // 工厂内部子建筑存档
        public List<SaveFactoryBuildingInstance> InteriorBuildings;
    }

    public class SaveFactoryBuildingInstance
    {
        public string DefId;
        public long RuntimeId;

        public int CellX;
        public int CellY;
        public byte RotSteps;

        public int HP;

        public string[] StatKeys;
        public float[] StatValues;
    }
    /// <summary>
    /// 全部建筑的存档数据（作为一个 ISaveItem 被 Scribe 管理）。
    /// </summary>
    public class SaveAllBuildings :  ISaveItem
    {
        

        /// <summary>
        /// summary: 存档类型唯一标识符
        /// return: 用于 Scribe 识别本类型的字符串
        /// </summary>
        public string TypeId => "AllBuildings";

        public List<SaveBuildingInstance> Buildings = new List<SaveBuildingInstance>();
        /// <summary>
        /// summary: 告诉 Scribe 如何读写全部建筑列表
        /// param: 无
        /// return: 无
        /// </summary>
        public void ExposeData()
        {
            // 存档前收集当前场景中的建筑数据
            if (Scribe.mode == ScribeMode.Saving)
            {
                BuildingSaveRuntime.CollectBuildingsForSave(ref Buildings);
            }
            // 读档或写档
            Scribe_Collections.Look(TypeId, ref Buildings);

            // 读档后还原场景中的建筑
            if(Scribe.mode == ScribeMode.Loading)
            {
                GameDebug.Log($"[SaveAllBuildings] Buildings count loaded: {(Buildings != null ? Buildings.Count : 0)}");
                // BuildingSaveRuntime.RestoreBuildingsFromSave(Buildings);
                // SaveBuilding I = Object.FindFirstObjectByType<SaveBuilding>();
                // if (I != null)
                // {
                SaveBuilding.Buildings = Buildings;
                // }
                //TODO: 延后到地图生成后再还原建筑
                
            }
        }
    }
}
