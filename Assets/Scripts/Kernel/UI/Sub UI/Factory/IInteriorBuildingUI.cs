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

    }
}