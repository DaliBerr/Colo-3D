
using Kernel.GameState;
using Lonize.Events;
using Lonize.Logging;
using Lonize.UI;
using UnityEngine.UI;
using static Lonize.Events.EventList;

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
        }

        private void OnCloseButtonClicked()
        {
            TryCloseUI();
        }

        private void OnAddButtonClicked()
        {
            Lonize.Events.Event.eventBus.Publish(new TryModifyInteriorBuildingEvent("factory_interior_default", true));
            // GameDebug.Log("Add Button Clicked in FactoryGridSelectionUI.");
        }
        private void OnRemoveButtonClicked()
        {
            Lonize.Events.Event.eventBus.Publish(new TryModifyInteriorBuildingEvent("factory_interior_default", false));
            // GameDebug.Log("Remove Button Clicked in FactoryGridSelectionUI.");
        }

        private void OnEnable()
        {
            Lonize.Events.Event.eventBus.Subscribe<FactoryGridSelected>(OnFactoryGridSelected);
        }

        private void OnDisable()
        {
            Lonize.Events.Event.eventBus.Unsubscribe<FactoryGridSelected>(OnFactoryGridSelected);
        }
        private void OnFactoryGridSelected(FactoryGridSelected evt)
        {
            // GameDebug.Log($"FactoryGridSelectionUI received FactoryGridSelected event: gridIndex={evt.gridIndex}, isEmpty={evt.isEmpty}");
            // 根据 evt.isEmpty 来决定按钮的状态
            if (evt.isEmpty)
            {
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