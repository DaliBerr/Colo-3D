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
            // 子类重写以实现输入按钮点击逻辑
        }
        protected virtual void OnOutputButtonClicked(int index)
        {
            // 子类重写以实现输出按钮点击逻辑
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

    }
}
