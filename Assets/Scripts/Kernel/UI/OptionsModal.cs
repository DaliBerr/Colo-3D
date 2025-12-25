using Kernel.GameState;
using Lonize.Events;
using Lonize.Logging;
using Lonize.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    [UIPrefab("Prefabs/UI/OptionsModal")]
    public sealed class OptionsModal : UIScreen
    {
        [Header("Options Modal Components")]
        public Text TitleText;
        public Button ScreenBtn;
        public Button AudioBtn;
        public Button VideoBtn;
        public Button ControlsBtn;
        public Button GameplayBtn;

        public Button ResetBtn;
        public Button ApplyBtn;
        public Button CancelBtn;

        public Button CloseBtn;

        [Header("Screen Panels")]
        public GameObject ScreenPanel;
        public Button Btn1;
        [Header("Audio Panels")]
        public GameObject AudioPanel;
        [Header("Video Panels")]
        public GameObject VideoPanel;
        [Header("Controls Panels")]
        public GameObject ControlsPanel;
        [Header("Gameplay Panels")]
        public GameObject GameplayPanel;

        public override Status currentStatus { get; } = StatusList.InMenuStatus;

        protected override void OnInit()
        {
            InitBaseButton();
            InitButtomButton();
            CloseBtn.onClick.AddListener(() =>{
                UIManager.Instance.PopScreen();
            });
            // base.OnInit();
            // Btn1.onClick.AddListener(() =>
            // {
            //     // Log.Info("Options Modal Button Clicked!");
            //     GameDebug.Log("Options Modal Button Clicked!");
            // });
        }
        private void OnEnable()
        {
            Events.eventBus.Subscribe<SettingChanged>(OnSettingsChanged);
            // TitleText.text = Localization.LocalizationManager.Instance.GetLocalizedText("Options");
        }
        private void OnDisable()
        {
            Events.eventBus.Unsubscribe<SettingChanged>(OnSettingsChanged);
        }
        private void OnSettingsChanged(SettingChanged evt)
        {
            // GameDebug.Log($"[{nameof(OptionsModal)}] 设置已更改，启用应用与取消按钮");
            ApplyBtn.gameObject.SetActive(true);
            CancelBtn.gameObject.SetActive(true);
            CloseBtn.onClick.RemoveAllListeners();
            CloseBtn.onClick.AddListener(() =>
            {
                //TODO: 弹出确认对话框

            });
        }
        private void InitBaseButton()
        {
            ScreenPanel.SetActive(true);
            AudioPanel.SetActive(false);
            VideoPanel.SetActive(false);
            ControlsPanel.SetActive(false);
            GameplayPanel.SetActive(false);
            
            ScreenBtn.onClick.AddListener(() =>
            {
                ScreenPanel.SetActive(true);
                AudioPanel.SetActive(false);
                VideoPanel.SetActive(false);
                ControlsPanel.SetActive(false);
                GameplayPanel.SetActive(false);
            });
            AudioBtn.onClick.AddListener(() =>
            {
                ScreenPanel.SetActive(false);
                AudioPanel.SetActive(true);
                VideoPanel.SetActive(false);
                ControlsPanel.SetActive(false);
                GameplayPanel.SetActive(false);
            });
            VideoBtn.onClick.AddListener(() =>
            {
                ScreenPanel.SetActive(false);
                AudioPanel.SetActive(false);
                VideoPanel.SetActive(true);
                ControlsPanel.SetActive(false);
                GameplayPanel.SetActive(false);
            });
            ControlsBtn.onClick.AddListener(() =>
            {
                ScreenPanel.SetActive(false);
                AudioPanel.SetActive(false);
                VideoPanel.SetActive(false);
                ControlsPanel.SetActive(true);
                GameplayPanel.SetActive(false);
            });
            GameplayBtn.onClick.AddListener(() =>
            {
                ScreenPanel.SetActive(false);
                AudioPanel.SetActive(false);
                VideoPanel.SetActive(false);
                ControlsPanel.SetActive(false);
                GameplayPanel.SetActive(true);
            });
        }
        private void InitButtomButton()
        {
            CancelBtn.onClick.AddListener(() =>
            {
                OptionsManager.Instance.CancelChanges();
                //回退设置
            });
            CancelBtn.gameObject.SetActive(false);
            ApplyBtn.onClick.AddListener(() =>
            {
                OptionsManager.Instance.ApplySettings();
                //保存并应用设置
            });
            ApplyBtn.gameObject.SetActive(false);
            ResetBtn.onClick.AddListener(() =>
            {
                OptionsManager.Instance.ResetToDefaults();
                //重置为默认设置
            });
        }
    }



}