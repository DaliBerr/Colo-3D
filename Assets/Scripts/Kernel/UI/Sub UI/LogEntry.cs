using System;
using Lonize.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    public sealed class LogEntryItemView : MonoBehaviour
    {
        public Button Btn;
        public TextMeshProUGUI PreviewText;

        [Header("Visual")]
        public Image SelectedBackground;   // 选中高亮（可选）
        public Image LevelStripe;         // 等级色条（可选）

        private int _index;
        private Action<int> _onClick;

        /// <summary>
        /// summary: 绑定条目数据与点击回调，并应用等级样式
        /// param: index 在“可见列表”中的索引
        /// param: e 日志事件
        /// param: previewMaxChars 摘要最大字符数
        /// param: previewColor 摘要文字颜色
        /// param: stripeColor 色条颜色
        /// param: onClick 点击回调（回传可见索引）
        /// return: 无
        /// </summary>
        public void Bind(
            int index,
            in LogEvent e,
            int previewMaxChars,
            Color previewColor,
            Color stripeColor,
            Action<int> onClick)
        {
            _index = index;
            _onClick = onClick;

            if (PreviewText != null)
            {
                PreviewText.text = BuildPreview(e.Message, previewMaxChars);
                PreviewText.color = previewColor;
            }

            if (LevelStripe != null)
                LevelStripe.color = stripeColor;

            if (Btn != null)
            {
                Btn.onClick.RemoveListener(OnClick);
                Btn.onClick.AddListener(OnClick);
            }
        }

        /// <summary>
        /// summary: 设置选中状态（高亮背景）
        /// param: selected 是否选中
        /// return: 无
        /// </summary>
        public void SetSelected(bool selected)
        {
            if (SelectedBackground != null)
                SelectedBackground.enabled = selected;
        }

        /// <summary>
        /// summary: 释放条目（回收到对象池前调用）
        /// param: 无
        /// return: 无
        /// </summary>
        public void Release()
        {
            if (Btn != null)
                Btn.onClick.RemoveListener(OnClick);

            _onClick = null;
            _index = -1;
            SetSelected(false);
        }

        /// <summary>
        /// summary: 点击条目
        /// param: 无
        /// return: 无
        /// </summary>
        private void OnClick()
        {
            _onClick?.Invoke(_index);
        }

        /// <summary>
        /// summary: 构建摘要（首行 + 截断）
        /// param: msg 原始消息
        /// param: maxChars 最大字符数
        /// return: 摘要字符串
        /// </summary>
        private static string BuildPreview(string msg, int maxChars)
        {
            if (string.IsNullOrEmpty(msg)) return string.Empty;

            int nl = msg.IndexOf('\n');
            string oneLine = nl >= 0 ? msg.Substring(0, nl) : msg;

            oneLine = oneLine.Replace('\r', ' ');
            if (oneLine.Length <= maxChars) return oneLine;

            return oneLine.Substring(0, maxChars) + "…";
        }
    }
}
