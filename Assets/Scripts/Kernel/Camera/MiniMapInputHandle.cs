using UnityEngine;
using UnityEngine.EventSystems;

namespace Kernel
{
    /// <summary>
    /// 小地图交互区域检测；用于记录鼠标是否在小地图区域内
    /// </summary>
    public class MiniMapInputArea : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>
        /// 静态标记：当前鼠标是否位于任意一个小地图区域上
        /// </summary>
        public static bool IsPointerOverMiniMap { get; private set; }

        /// <summary>
        /// 当指针进入小地图区域时回调
        /// </summary>
        /// <param name="eventData">指针事件数据</param>
        /// <returns>无</returns>
        public void OnPointerEnter(PointerEventData eventData)
        {
            IsPointerOverMiniMap = true;
        }

        /// <summary>
        /// 当指针离开小地图区域时回调
        /// </summary>
        /// <param name="eventData">指针事件数据</param>
        /// <returns>无</returns>
        public void OnPointerExit(PointerEventData eventData)
        {
            IsPointerOverMiniMap = false;
        }

        /// <summary>
        /// 当物体被禁用时重置标记，避免悬空状态
        /// </summary>
        /// <param name="无">无</param>
        /// <returns>无</returns>
        private void OnDisable()
        {
            IsPointerOverMiniMap = false;
        }
    }
}
