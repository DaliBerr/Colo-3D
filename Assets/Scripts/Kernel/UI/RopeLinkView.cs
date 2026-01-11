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

            Canvas canvas = container.GetComponentInParent<Canvas>();
            Camera cam = canvas != null ? canvas.worldCamera : null;

            Vector2 fromScreen = RectTransformUtility.WorldToScreenPoint(cam, from.position);
            Vector2 toScreen = RectTransformUtility.WorldToScreenPoint(cam, to.position);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(container, fromScreen, cam, out Vector2 p0);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(container, toScreen, cam, out Vector2 p2);

            float distance = Vector2.Distance(p0, p2);
            float targetSag = MathUtils.BezierRopeMath.CalcSag(distance, sagFactor, sagMin, sagMax);
            float t = Mathf.Clamp01(sagSmooth * Time.deltaTime);
            currentSag = Mathf.Lerp(currentSag, targetSag, t);

            int segments = MathUtils.BezierRopeMath.CalcSegments(distance);
            MathUtils.BezierRopeMath.BuildQuadraticPoints(p0, p2, currentSag, segments, points);
            ropeGraphic.SetPoints(points);
        }
    }
}
