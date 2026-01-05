using UnityEngine;
using Lonize.UI;
using UnityEngine.EventSystems;
using TMPro;
using Lonize.Events;
using Lonize.Logging;
using Kernel.GameState;
using static Lonize.Events.EventList;
namespace Kernel
{
    public class CameraMainControl : CameraControl
    {

        // public override Camera targetCamera { get; set; }

        public override void HandleAwake()
        {
            targetCamera = GetComponentInChildren<Camera>();

        }
        void OnEnable()
        {
            Lonize.Events.Event.eventBus.Subscribe<SpeedChange>(OnSpeedChanged);
        }
        private void OnSpeedChanged(SpeedChange evt)
        {
            GameDebug.Log($"Main Camera received speed change: {evt.currentGameSpeed} ({evt.speedMultiplier}x)");
            Log.Info($"Main Camera received speed change: {evt.currentGameSpeed} ({evt.speedMultiplier}x)");
        }
        public override void HandleUpdate()
        {
            if (!CanMoveCamera())
            {
                canMoveCamera = false;
            }
            else
            {
                canMoveCamera = true;
            }
                // return;
            // HandlePan();
            // HandleZoom();
        }
        private bool CanMoveCamera()
        {
            var Res = IsPointerCanMoveMainCamera();
            var playing = isGamePlaying();
            return Res && playing;
        }
        private bool isGamePlaying()
        {
            return StatusController.HasStatus(StatusList.PlayingStatus);
        }
        /// <summary>
        /// 鼠标（或触摸）是否可以驱动主摄像机移动。
        /// 如果指针当前在 UI 上，则返回 false。
        /// </summary>
        private bool IsPointerCanMoveMainCamera()
        {
            // 没有 EventSystem 就当作不在 UI 上
            if (EventSystem.current == null)
                return true;

#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
            // 移动端：任意触摸点若在 UI 上，则不允许
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (EventSystem.current.IsPointerOverGameObject(Input.touches[i].fingerId))
                    return false;
            }
            return true;
#else
            // PC/编辑器：鼠标在 UI 上则不允许
            return !EventSystem.current.IsPointerOverGameObject();
#endif
        }
    }
}