using System.Collections.Generic;
using Lonize.Logging;
using UnityEngine;

namespace Kernel.Building
{
    public class BuildingFactoryController : MonoBehaviour
    {
        public static BuildingFactoryController Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            Lonize.Events.Event.eventBus.Subscribe<Lonize.Events.EventList.BuildingSelected>(OnFactorySelected);
        }

        private void OnDisable()
        {
            Lonize.Events.Event.eventBus.Unsubscribe<Lonize.Events.EventList.BuildingSelected>(OnFactorySelected);
        }

        private static BuildingRuntime _currentFactoryRuntime;

        /// <summary>
        /// summary: 处理工厂建筑被选中/取消选中的事件，并维护当前工厂运行时引用（适配 List 选择事件）。
        /// param: evt 建筑选择事件（包含 buildingRuntimes 与 isSelected）。
        /// return: 无返回值。
        /// </summary>
        private void OnFactorySelected(Lonize.Events.EventList.BuildingSelected evt)
        {
            // 0) 没有任何选中：直接清空
            if (!evt.isSelected || evt.buildingRuntimes == null || evt.buildingRuntimes.Count == 0)
            {
                _currentFactoryRuntime = null;
                return;
            }

            // 0.5) 多选：不允许自动打开/切换工厂（避免误触进入内部界面）
            if (evt.buildingRuntimes.Count > 1)
            {
                _currentFactoryRuntime = null;
                GameDebug.Log($"[BuildingFactoryController] 检测到多选（count={evt.buildingRuntimes.Count}），跳过工厂打开/切换。");
                return;
            }

            long prevId = _currentFactoryRuntime != null ? _currentFactoryRuntime.BuildingID : -1;

            // 1) 如果当前工厂仍在选中列表里，则优先保留（避免多选时频繁跳变）
            if (_currentFactoryRuntime != null && evt.buildingRuntimes.Contains(_currentFactoryRuntime))
            {
                // 防御：Def 可能为空或类别不匹配时，降级为重新扫描
                if (_currentFactoryRuntime.Def != null && _currentFactoryRuntime.Def.Category == BuildingCategory.Factory)
                    return;

                _currentFactoryRuntime = null;
            }

            // 2) 扫描当前选中集合，寻找一个 Factory（优先从后往前：更接近“最近一次加入选中”的目标）
            _currentFactoryRuntime = null;
            for (int i = evt.buildingRuntimes.Count - 1; i >= 0; i--)
            {
                var rt = evt.buildingRuntimes[i];
                if (rt == null) continue;

                // 这里保留一个简单校验：负数 ID 视为非法
                if (rt.BuildingID < 0)
                {
                    GameDebug.LogError($"Invalid BuildingID received in BuildingSelected event: ID={rt.BuildingID}");
                    Log.Error($"Invalid BuildingID received in BuildingSelected event: ID={rt.BuildingID}");
                    continue;
                }

                if (rt.Def == null)
                {
                    // 外部可能发布了未完成初始化的 Runtime，跳过即可
                    GameDebug.LogWarning($"Selected building Def is null. ID={rt.BuildingID}");
                    continue;
                }

                if (rt.Def.Category != BuildingCategory.Factory)
                    continue;

                _currentFactoryRuntime = rt;
                break;
            }

            long newId = _currentFactoryRuntime != null ? _currentFactoryRuntime.BuildingID : -1;
            if (prevId != newId)
            {
                if (_currentFactoryRuntime != null)
                    GameDebug.Log($"Factory selected: ID={_currentFactoryRuntime.BuildingID}");
                else
                    GameDebug.Log("Factory deselected: no factory in current selection.");
            }
        }

        public BuildingRuntime GetCurrentFactoryRuntime()
        {
            return _currentFactoryRuntime;
        }
    }
}
