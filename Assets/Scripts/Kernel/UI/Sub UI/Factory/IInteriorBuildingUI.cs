using System;
using System.Collections.Generic;
using Kernel.Building;
using Kernel.Factory.Connections;
using Lonize.Logging;
using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    public class IInteriorBuildingUI : MonoBehaviour
    {
        /// <summary>
        /// summary: UI 按钮端口元数据。
        /// </summary>
        [System.Serializable]
        public struct InteriorPortButtonMeta
        {
            public string PortId;
            public ConnectionChannel Channel;
        }

        [SerializeField] public List<Button> InputButtons ;
        [SerializeField] public List<Button> OutputButtons ;
        [SerializeField] public GameObject TexturePanel ;
        [SerializeField] public List<InteriorPortButtonMeta> InputPortMetas = new();
        [SerializeField] public List<InteriorPortButtonMeta> OutputPortMetas = new();
        [SerializeField] public long BuildingParentId;
        [SerializeField] public int BuildingLocalId;

        public event Action<PortKey, PortDirection> PortClicked;

        private void OnEnable()
        {
            for (int i = 0; i < InputButtons.Count; i++)
            {
                int index_i = i;
                InputButtons[index_i].onClick.AddListener(() => 
                {
                    OnInputButtonClicked(index_i);
                });
            }
            for (int i = 0; i < OutputButtons.Count; i++)
            {
                int index_o = i;
                OutputButtons[index_o].onClick.AddListener(() => 
                {
                    OnOutputButtonClicked(index_o);
                });
            }
        }

        private void OnDisable()
        {
            ClearListeners();
        }

        protected virtual void OnInputButtonClicked(int index)
        {
            HandlePortButtonClicked(index, PortDirection.Input);
        }
        protected virtual void OnOutputButtonClicked(int index)
        {
            HandlePortButtonClicked(index, PortDirection.Output);
        }

        private void Start()
        {
            handleStart();
        }

        private void Awake()
        {
            handleAwake();
        }

        protected virtual void handleStart()
        {
            // 子类重写以实现初始化逻辑
        }

        protected virtual void handleAwake()
        {
            // 子类重写以实现初始化逻辑
        }

        /// <summary>
        /// summary: 处理端口按钮点击并触发回调。
        /// param: index 端口按钮索引
        /// param: direction 端口方向
        /// return: 无
        /// </summary>
        private void HandlePortButtonClicked(int index, PortDirection direction)
        {
            if (!TryGetPortKey(direction, index, out var key))
            {
                return;
            }

            PortClicked?.Invoke(key, direction);
        }

        /// <summary>
        /// summary: 尝试根据按钮索引解析端口键。
        /// param: direction 端口方向
        /// param: index 端口按钮索引
        /// param: key 返回端口键
        /// return: 是否解析成功
        /// </summary>
        private bool TryGetPortKey(PortDirection direction, int index, out PortKey key)
        {
            key = default;

            var metas = direction == PortDirection.Input ? InputPortMetas : OutputPortMetas;
            if (metas == null || metas.Count == 0)
            {
                GameDebug.LogWarning($"[InteriorUI] 端口元数据为空，direction={direction}");
                return false;
            }

            if (index < 0 || index >= metas.Count)
            {
                GameDebug.LogWarning($"[InteriorUI] 端口按钮索引越界：{index} / {metas.Count}");
                return false;
            }

            var meta = metas[index];
            if (string.IsNullOrEmpty(meta.PortId))
            {
                GameDebug.LogWarning($"[InteriorUI] 端口ID为空，direction={direction} index={index}");
                return false;
            }

            if (BuildingParentId <= 0 || BuildingLocalId <= 0)
            {
                GameDebug.LogWarning($"[InteriorUI] 建筑标识无效：Parent={BuildingParentId} Local={BuildingLocalId}");
                return false;
            }

            key = new PortKey(BuildingParentId, BuildingLocalId, meta.PortId);
            return true;
        }

        /// <summary>
        /// summary: 初始化端口元数据并绑定建筑标识。
        /// param: child 内部建筑运行时
        /// return: 无
        /// </summary>
        public void InitializePortMeta(FactoryChildRuntime child)
        {
            InputPortMetas ??= new List<InteriorPortButtonMeta>();
            OutputPortMetas ??= new List<InteriorPortButtonMeta>();
            InputPortMetas.Clear();
            OutputPortMetas.Clear();

            if (child == null)
            {
                BuildingParentId = 0;
                BuildingLocalId = 0;
                return;
            }

            BuildingParentId = child.BuildingParentID;
            BuildingLocalId = child.BuildingLocalID;

            var ports = CollectPorts(child);
            if (ports == null || ports.Count == 0)
            {
                ValidatePortMetaCounts();
                return;
            }

            foreach (var desc in ports)
            {
                if (string.IsNullOrEmpty(desc.PortId)) continue;

                var meta = new InteriorPortButtonMeta
                {
                    PortId = desc.PortId,
                    Channel = desc.Channel
                };

                switch (desc.Direction)
                {
                    case PortDirection.Input:
                        InputPortMetas.Add(meta);
                        break;
                    case PortDirection.Output:
                        OutputPortMetas.Add(meta);
                        break;
                    case PortDirection.Bidirectional:
                        InputPortMetas.Add(meta);
                        OutputPortMetas.Add(meta);
                        break;
                }
            }

            ValidatePortMetaCounts();
        }

        /// <summary>
        /// summary: 收集内部建筑提供的端口声明。
        /// param: child 内部建筑运行时
        /// return: 端口声明列表
        /// </summary>
        private static List<PortDescriptor> CollectPorts(FactoryChildRuntime child)
        {
            if (child?.Behaviours == null || child.Behaviours.Count == 0)
            {
                return null;
            }

            var list = new List<PortDescriptor>();
            foreach (var behaviour in child.Behaviours)
            {
                if (behaviour is IInteriorPortProvider provider)
                {
                    var ports = provider.GetPorts();
                    if (ports == null) continue;
                    list.AddRange(ports);
                }
            }

            return list;
        }

        /// <summary>
        /// summary: 校验端口元数据数量与按钮数量是否一致。
        /// param: 无
        /// return: 无
        /// </summary>
        private void ValidatePortMetaCounts()
        {
            int inputButtonsCount = InputButtons != null ? InputButtons.Count : 0;
            int outputButtonsCount = OutputButtons != null ? OutputButtons.Count : 0;

            if (InputPortMetas.Count != inputButtonsCount)
            {
                GameDebug.LogWarning($"[InteriorUI] 输入端口元数据数量与按钮数量不一致：Ports={InputPortMetas.Count}, Buttons={inputButtonsCount}");
            }

            if (OutputPortMetas.Count != outputButtonsCount)
            {
                GameDebug.LogWarning($"[InteriorUI] 输出端口元数据数量与按钮数量不一致：Ports={OutputPortMetas.Count}, Buttons={outputButtonsCount}");
            }
        }

        public void ClearListeners()
        {
            foreach (var button in InputButtons)
            {
                button.onClick.RemoveAllListeners();
            }
            foreach (var button in OutputButtons)
            {
                button.onClick.RemoveAllListeners();
            }
        }

        /// <summary>
        /// summary: 设置所有按钮的可交互状态。
        /// param: isInteractable 是否可交互
        /// return: 无
        /// </summary>
        public void SetAllButtonsInteractable(bool isInteractable)
        {
            var buttons = GetComponentsInChildren<Button>(true);
            if (buttons == null)
            {
                return;
            }

            foreach (var button in buttons)
            {
                if (button == null)
                {
                    continue;
                }

                button.gameObject.SetActive(isInteractable);
            }
        }

        /// <summary>
        /// summary: 设置输入与输出按钮的可交互状态。
        /// param: isInteractable 是否可交互
        /// return: 无
        /// </summary>
        public void SetPortButtonsInteractable(bool isInteractable)
        {
            if (InputButtons != null)
            {
                foreach (var button in InputButtons)
                {
                    if (button == null)
                    {
                        continue;
                    }

                    button.gameObject.SetActive(isInteractable);
                }
            }

            if (OutputButtons != null)
            {
                foreach (var button in OutputButtons)
                {
                    if (button == null)
                    {
                        continue;
                    }

                    button.gameObject.SetActive(isInteractable);
                }
            }
        }

    }
}
