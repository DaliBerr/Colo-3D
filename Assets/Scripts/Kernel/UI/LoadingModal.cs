using System.Collections.Generic;
using Kernel.GameState;
using Lonize.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    [UIPrefab("Prefabs/UI/LoadingModal")]
    public sealed class GameLoading : UIScreen
    {
        public override Status currentStatus { get; } = StatusList.GameLoadingStatus;

        public List<Image> loadingBackgroundImages;
        public Slider loadingSlider;

        // ★ 调试用的最小显示时间（秒）
        // 在编辑器里强制至少显示这么久，打包后可以设为0或者改小
#if UNITY_EDITOR
        [SerializeField] private float MinShowTime = 1f;
#else
        [SerializeField] private float MinShowTime = 1f;
#endif

        private float _elapsedTime ;

        /// <summary>
        /// 初始化加载界面，重置计时和进度条。
        /// </summary>
        /// <returns>无返回值。</returns>
        protected override void OnInit()
        {
            _elapsedTime = 0f;
            if (loadingSlider != null)
            {
                loadingSlider.value = 0f;
            }
        }

        /// <summary>
        /// 每帧更新加载进度，并在满足条件时关闭加载界面。
        /// </summary>
        /// <returns>无返回值。</returns>
        public void Update()
        {
            // ★ 游戏整体加载的情况
            if (StatusController.HasStatus(StatusList.GameLoadingStatus))
            {
                // 使用不受 Time.timeScale 影响的时间，避免暂停等情况干扰
                _elapsedTime += Time.unscaledDeltaTime;
                // Debug.Log($"GameLoading Update: elapsedTime={_elapsedTime}");
                // 从全局进度管理器读取真实进度（0-1）
                float realProgress = GlobalLoadingProgress.GameLoadingProgress;

                if (loadingSlider != null)
                {
                    loadingSlider.value = realProgress;
                }

                // 只有当：
                // 1) 真实进度已经完成（>=1）
                // 2) 且界面显示时间 >= MinShowTime
                // 才真正关闭加载界面
                if (realProgress >= 1f && _elapsedTime >= MinShowTime)
                {
                    // Debug.Log("GameLoading completed, popping screen.");
                    if (loadingSlider != null)
                        loadingSlider.value = 1f;

                    StatusController.CurrentStatus.RemoveAll(
                        s => s.StatusName == StatusList.GameLoadingStatus.StatusName);

                    UIManager.Instance.PopScreen();
                }
            }
            //TODO: 接入真实的存档加载进度
            // ★ 存档加载，先保留你原来的假进度逻辑（之后如果需要也可以接真实进度）
            else if (StatusController.HasStatus(StatusList.SaveLoadingStatus))
            {
                if (loadingSlider != null)
                {
                    // 模拟加载进度
                    loadingSlider.value += 0.02f;
                }

                if (loadingSlider != null && loadingSlider.value >= 1f)
                {
                    loadingSlider.value = 1f;
                    StatusController.CurrentStatus.RemoveAll(
                        s => s.StatusName == StatusList.SaveLoadingStatus.StatusName);
                    UIManager.Instance.PopScreen();
                    StatusController.AddStatus(StatusList.PlayingStatus);
                }
            }
        }
    }
}
