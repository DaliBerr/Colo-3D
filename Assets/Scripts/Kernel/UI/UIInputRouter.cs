using System.Collections;
using Kernel;
using Kernel.GameState;
using Kernel.UI;
using Lonize;
using Lonize.Logging;
using Lonize.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    public sealed class UIInputRouter : MonoBehaviour
    {
        // private bool isProcessingBack = false;
        // public KeyCode backKey = KeyCode.Escape;

        private UIControls uicontrols;
        private void Awake()
        {
            uicontrols = new UIControls();
            uicontrols.Enable();
        }

        void Update()
        {
            if (!UIManager.Instance) return;

            // UI 正在过渡：直接忽略 back
            if (UIManager.Instance.IsNavigating()) return;

            if (uicontrols.UI.Router.WasPressedThisFrame())
            {
                StartCoroutine(HandleBack());
            }
        }
        /// <summary>
        /// 处理返回键逻辑：所有 UI 操作都用 AndWait，避免并发与状态不同步。
        /// </summary>
        /// <param name="none">无</param>
        /// <return>用于yield的协程枚举器</return>
        private IEnumerator HandleBack()
        {
            if (!UIManager.Instance) yield break;

            if (StatusController.HasStatus(StatusList.PopUpStatus))
            {
                // 建议不要 GetTopModal(true)，容易拿到失活对象；用 false 更安全
                var top = UIManager.Instance.GetTopModal(false);
                if (top is PopupModal popup)
                {
                    StatusController.RemoveStatus(StatusList.PopUpStatus);
                    popup.CancelButtonAction(); // 让弹窗自己走关闭逻辑
                }
                yield break;
            }

            if (StatusController.HasStatus(StatusList.PlayingStatus))
            {
                // StatusController.AddStatus(StatusList.InPauseMenuStatus);
                if (UIManager.Instance.GetTopModal())
                {
                    var a = UIManager.Instance.GetTopModal();
                    Lonize.Events.Event.eventBus.Publish(new Lonize.Events.EventList.CloseModalRequest(a));
                    yield return UIManager.Instance.PopModalAndWait();
                    yield break;
                }
                yield return UIManager.Instance.PushScreenAndWait<PauseMenuUI>();
                yield break;
            }

            if (StatusController.HasStatus(StatusList.InPauseMenuStatus))
            {
                // StatusController.AddStatus(StatusList.PlayingStatus);
                yield return UIManager.Instance.PopScreenAndWait();
                yield break;
            }

            if (StatusController.HasStatus(StatusList.InMainMenuStatus))
            {
                yield return UIManager.Instance.ShowModalAndWait<QuitConfirmPopupModal>();
                yield break;
            }

            yield return CloseAndSync();
        }
        IEnumerator CloseAndSync()
        {
            yield return UIManager.Instance.PopScreenAndWait();
            var top = UIManager.Instance.GetTopScreen(true);
            StatusController.AddStatus(top?.currentStatus ?? StatusList.InMenuStatus);
            GameDebug.Log("[UIInputRouter] New Top Screen: " + (top?.name ?? "No Screen AfterPop"));
            
        }
    }
}