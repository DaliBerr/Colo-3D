using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    public class IInteriorBuildingUI : MonoBehaviour
    {
        
        [SerializeField] public List<Button> InputButtons ;
        [SerializeField] public List<Button> OutputButtons ;
        [SerializeField] public GameObject TexturePanel ;

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

                button.interactable = isInteractable;
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

                    button.interactable = isInteractable;
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

                    button.interactable = isInteractable;
                }
            }
        }

    }
}
