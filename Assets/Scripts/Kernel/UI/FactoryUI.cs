using System.Collections;
using System.Collections.Generic;
using Kernel;
using Kernel.Building;
using Kernel.GameState;
using Lonize;
using Lonize.Logging;
using Lonize.UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Kernel.UI
{
    [UIPrefab("Prefabs/UI/Factory UI")]
    public sealed class FactoryUI : UIScreen
    {

        [SerializeField] private Button closeButton;
        [SerializeField] private List<Button> itemButtons = new List<Button>();
        [SerializeField] private GameObject itemSelectionPanel;
        

        public override Status currentStatus { get; } = StatusList.PlayingStatus;

        protected override void OnInit()
        {
            closeButton.onClick.AddListener(TryCloseUI);
            for(int i = 0; i < itemButtons.Count; i++)
            {
                int index = i; // 捕获当前索引
                itemButtons[i].onClick.AddListener(() => OnItemButtonClicked(index));
            }
        }
        private void OnDestroy()
        {
            closeButton.onClick.RemoveAllListeners();
            for (int i = 0; i < itemButtons.Count; i++)
            {
                itemButtons[i].onClick.RemoveAllListeners();
            }
        }

        private void TryCloseUI()
        {
            UIManager.Instance.CloseTopModal();
        }

        private void OnItemButtonClicked(int index)
        {
            GameDebug.Log($"Item button {index} clicked.");
            itemSelectionPanel.SetActive(true);
            // 在这里添加处理按钮点击的逻辑
        }
    }
}