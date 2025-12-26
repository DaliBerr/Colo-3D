
using System.Collections;
using Kernel.GameState;
using Lonize.Logging;
using Lonize.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kernel.UI
{
    [UIPrefab("Prefabs/UI/PauseMenuUI")]
    public sealed class PauseMenuUI : UIScreen
    {

        public override Status currentStatus { get; } = StatusList.InPauseMenuStatus;

        [Header("Buttons")]
        public Button resumeButton;
        public Button optionsButton;
        public Button saveButton;
        public Button loadButton;
        public Button mainMenuButton;
        public Button quitButton;


        [Header("Images")]
        public Image backgroundImage;

        protected override void OnInit()
        {
            resumeButton.onClick.AddListener(() =>
            {
                TryResumeGame();
            });

            optionsButton.onClick.AddListener(() =>
            {
                TryOpenOptions();
                // GameDebug.Log("Options Button Clicked!");
                // UIManager.Instance.PushScreen<OptionsModal>();
            });

            saveButton.onClick.AddListener(() =>
            {
                TrySaveGame();
                // GameDebug.Log("Save Button Clicked!");
                // Colo.GameSaveController.Instance.SaveGame();
            });

            loadButton.onClick.AddListener(() =>
            {
                TryLoadGame();
                // GameDebug.Log("Load Button Clicked!");
                // Colo.GameSaveController.Instance.LoadGame();
            });

            mainMenuButton.onClick.AddListener(() =>
            {
                // GameDebug.Log("Main Menu Button Clicked!");
                StartCoroutine(TryReturnToMainMenu());
            });
            quitButton.onClick.AddListener(() =>
            {
                TryQuitGame();
                // GameDebug.Log("Quit Button Clicked!");
                // Application.Quit();
            });
        }
        // private IEnumerator WaitUINotNavigating(UIManager ui)
        // {
        //     while (ui != null && ui.IsNavigating())
        //         yield return null;
        // }
        private void TryResumeGame()
        {
            try{
            StatusController.AddStatus(StatusList.PlayingStatus);
            UIManager.Instance.CloseTop();}
            catch(System.Exception e){
                GameDebug.LogError($"Error while trying to resume game: {e.Message}");
            }
        }
        private void TryOpenOptions()
        {
            try{
            UIManager.Instance.PushScreen<OptionsModal>();
            StatusController.AddStatus(StatusList.InMenuStatus);
            }
            catch(System.Exception e){
                GameDebug.LogError($"Error while trying to open options: {e.Message}");
            }
        }
        private void TrySaveGame()
        {
            try{
                //TODO : 弹出保存选择对话框
                Colo.GameSaveController.Instance.SaveGame();
            }
            catch(System.Exception e){
                GameDebug.LogError($"Error while trying to save game: {e.Message}");
            }
        }
        private void TryLoadGame()
        {
            try{
                Colo.GameSaveController.Instance.LoadGame();
                StatusController.AddStatus(StatusList.InPauseMenuStatus);
            }
            catch(System.Exception e){
                GameDebug.LogError($"Error while trying to load game: {e.Message}");
            }
        }
        private IEnumerator TryReturnToMainMenu()
        {
                // yield return WaitUINotNavigating(ui);

                yield return SceneManager.LoadSceneAsync("StartPage");
                // yield return WaitUINotNavigating(ui);
                // yield return UIManager.Instance.PushScreenAndWait<MainMenuScreen>();
                // SceneManager.UnloadSceneAsync("Main");
                StatusController.RemoveStatus(StatusList.PlayingStatus);
                StatusController.RemoveStatus(StatusList.InPauseMenuStatus);
                StatusController.AddStatus(StatusList.InMainMenuStatus);
                UIManager.Instance.RequestReturnToMainMenu();


        }
        private void TryQuitGame()
        {
            try{
                Application.Quit();
            }
            catch(System.Exception e){
                GameDebug.LogError($"Error while trying to quit game: {e.Message}");
            }
        }
    }

}