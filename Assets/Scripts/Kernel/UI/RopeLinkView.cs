using System.Collections.Generic;
using Lonize.Math;
using UnityEngine;

namespace Kernel.UI
{
    public class RopeLinkView : MonoBehaviour
    {
        public RectTransform container;
        public UIRopeGraphic ropeGraphic;
        public float sagFactor = 0.15f;
        public float sagMin = 8f;
        public float sagMax = 120f;
        public float sagSmooth = 8f;

        private readonly List<Vector2> points = new List<Vector2>();
        private float currentSag;

        /// <summary>设置两端点并更新绳索曲线。</summary>
        /// <param name="from">起点 RectTransform。</param>
        /// <param name="to">终点 RectTransform。</param>
        /// <return>无。</return>
        public void SetEndpoints(RectTransform from, RectTransform to)
        {
            if (container == null || ropeGraphic == null || from == null || to == null)
            {
                return;
            }

            if (!TryGetScreenPoint(from, out var fromScreen) || !TryGetScreenPoint(to, out var toScreen))
            {
                return;
            }

            SetEndpoints(fromScreen, toScreen);
        }

        /// <summary>
        /// summary: 设置起点 RectTransform 与屏幕终点。
        /// param: from 起点 RectTransform
        /// param: toScreen 终点屏幕坐标
        /// return: 无
        /// </summary>
        public void SetEndpoints(RectTransform from, Vector2 toScreen)
        {
            if (container == null || ropeGraphic == null || from == null)
            {
                return;
            }

            if (!TryGetScreenPoint(from, out var fromScreen))
            {
                return;
            }

            SetEndpoints(fromScreen, toScreen);
        }

        /// <summary>
        /// summary: 通过屏幕坐标设置两端点。
        /// param: fromScreen 起点屏幕坐标
        /// param: toScreen 终点屏幕坐标
        /// return: 无
        /// </summary>
        private void SetEndpoints(Vector2 fromScreen, Vector2 toScreen)
        {
            if (!TryGetCanvasCamera(out var cam))
            {
                cam = null;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(container, fromScreen, cam, out Vector2 p0))
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(container, toScreen, cam, out Vector2 p2))
            {
                return;
            }

            float distance = Vector2.Distance(p0, p2);
            float targetSag = MathUtils.BezierRopeMath.CalcSag(distance, sagFactor, sagMin, sagMax);
            float t = Mathf.Clamp01(sagSmooth * Time.deltaTime);
            currentSag = Mathf.Lerp(currentSag, targetSag, t);

            int segments = MathUtils.BezierRopeMath.CalcSegments(distance);
            MathUtils.BezierRopeMath.BuildQuadraticPoints(p0, p2, currentSag, segments, points);
            ropeGraphic.SetPoints(points);
        }

        /// <summary>
        /// summary: 尝试获取屏幕坐标。
        /// param: rect RectTransform
        /// param: screenPos 返回屏幕坐标
        /// return: 是否获取成功
        /// </summary>
        private bool TryGetScreenPoint(RectTransform rect, out Vector2 screenPos)
        {
            screenPos = default;
            if (rect == null)
            {
                return false;
            }

            TryGetCanvasCamera(out var cam);
            screenPos = RectTransformUtility.WorldToScreenPoint(cam, rect.position);
            return true;
        }

        /// <summary>
        /// summary: 尝试获取 UI 相机。
        /// param: cam 返回相机
        /// return: 是否获取成功
        /// </summary>
        private bool TryGetCanvasCamera(out Camera cam)
        {
            cam = null;
            if (container == null)
            {
                return false;
            }

            Canvas canvas = container.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                return false;
            }

            cam = canvas.worldCamera;
            return true;
        }
    }
}
