
using Kernel.GameState;
using Lonize.EventSystem;
using Lonize.Logging;
using Lonize.UI;
using UnityEngine.UI;
using static Lonize.EventSystem.EventList;

namespace Kernel.UI
{
    [UIPrefab("Prefabs/UI/Sub UI/Factory/FactoryGridSelectionUI")]
    public sealed class FactoryGridSelectionUI : UIScreen
    {
        public override Status currentStatus { get; } = StatusList.PlayingStatus;

        public Button closeButton;
        public Button addButton;
        public Button removeButton;

        protected override void OnInit()
        {
            closeButton.onClick.AddListener(OnCloseButtonClicked);
            addButton.onClick.AddListener(OnAddButtonClicked);
            removeButton.onClick.AddListener(OnRemoveButtonClicked);

            removeButton.gameObject.SetActive(false);
            addButton.gameObject.SetActive(false);
            closeButton.gameObject.SetActive(true);
        }

        private void OnCloseButtonClicked()
        {
            TryCloseUI();
        }

        private void OnAddButtonClicked()
        {
            Lonize.EventSystem.EventManager.eventBus.Publish(new TryModifyInteriorBuildingEvent("factory_interior_default", true));
            // GameDebug.Log("Add Button Clicked in FactoryGridSelectionUI.");
        }
        private void OnRemoveButtonClicked()
        {
            Lonize.EventSystem.EventManager.eventBus.Publish(new TryModifyInteriorBuildingEvent("factory_interior_default", false));
            // GameDebug.Log("Remove Button Clicked in FactoryGridSelectionUI.");
        }

        private void OnEnable()
        {
            Lonize.EventSystem.EventManager.eventBus.Subscribe<FactoryGridSelected>(OnFactoryGridSelected);
        }

        private void OnDisable()
        {
            Lonize.EventSystem.EventManager.eventBus.Unsubscribe<FactoryGridSelected>(OnFactoryGridSelected);
        }
        private void OnFactoryGridSelected(FactoryGridSelected evt)
        {
            // GameDebug.Log($"FactoryGridSelectionUI received FactoryGridSelected event: gridIndex={evt.gridIndex}, isEmpty={evt.isEmpty}");
            // 根据 evt.isEmpty 来决定按钮的状态
            if (evt.isEmpty)
            {
                GameDebug.Log($"addButton GameObject {addButton.gameObject.name} set to active.");
                addButton.gameObject.SetActive(true);
                removeButton.gameObject.SetActive(false);
            }
            else
            {
                addButton.gameObject.SetActive(false);
                removeButton.gameObject.SetActive(true);
            }
            
        }
        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(OnCloseButtonClicked);
            addButton.onClick.RemoveListener(OnAddButtonClicked);
            removeButton.onClick.RemoveListener(OnRemoveButtonClicked);
        }

        private void TryCloseUI()
        {
            UIManager.Instance.CloseTopModal();
            // Lonize.Events.Event.eventBus.Publish(new CloseModalRequest(this));
        }
        
    }



}