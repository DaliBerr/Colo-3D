using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Kernel.UI
{
    public class UIRopeGraphic : Graphic
    {
        [SerializeField] public float thickness = 10f;
        [SerializeField] public float uvTileUnit = 100f;

        private readonly List<Vector2> points = new List<Vector2>();
        private readonly List<Vector2> normals = new List<Vector2>();
        private readonly List<float> lengths = new List<float>();

        /// <summary>设置折线采样点并刷新绘制。</summary>
        /// <param name="newPoints">新的采样点列表。</param>
        /// <return>无。</return>
        public void SetPoints(List<Vector2> newPoints)
        {
            points.Clear();
            if (newPoints != null)
            {
                for (int i = 0; i < newPoints.Count; i++)
                {
                    points.Add(newPoints[i]);
                }
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int count = points.Count;
            if (count < 2)
            {
                return;
            }

            EnsureCapacity(normals, count);
            EnsureCapacity(lengths, count);

            lengths.Clear();
            normals.Clear();

            float cumulative = 0f;
            lengths.Add(cumulative);
            for (int i = 1; i < count; i++)
            {
                cumulative += Vector2.Distance(points[i - 1], points[i]);
                lengths.Add(cumulative);
            }

            for (int i = 0; i < count; i++)
            {
                Vector2 dir;
                if (i == 0)
                {
                    dir = points[1] - points[0];
                }
                else if (i == count - 1)
                {
                    dir = points[count - 1] - points[count - 2];
                }
                else
                {
                    dir = points[i + 1] - points[i - 1];
                }

                if (dir.sqrMagnitude < 0.0001f)
                {
                    dir = Vector2.right;
                }
                dir.Normalize();
                normals.Add(new Vector2(-dir.y, dir.x));
            }

            float half = thickness * 0.5f;
            float tile = Mathf.Max(0.0001f, uvTileUnit);
            Color32 col = color;

            for (int i = 0; i < count; i++)
            {
                Vector2 normal = normals[i];
                Vector2 p = points[i];
                Vector2 left = p - normal * half;
                Vector2 right = p + normal * half;
                float u = lengths[i] / tile;

                UIVertex vert = UIVertex.simpleVert;
                vert.color = col;

                vert.position = left;
                vert.uv0 = new Vector2(u, 0f);
                vh.AddVert(vert);

                vert.position = right;
                vert.uv0 = new Vector2(u, 1f);
                vh.AddVert(vert);
            }

            for (int i = 0; i < count - 1; i++)
            {
                int baseIndex = i * 2;
                vh.AddTriangle(baseIndex, baseIndex + 1, baseIndex + 2);
                vh.AddTriangle(baseIndex + 2, baseIndex + 1, baseIndex + 3);
            }
        }

        private static void EnsureCapacity<T>(List<T> list, int capacity)
        {
            if (list.Capacity < capacity)
            {
                list.Capacity = capacity;
            }
        }
    }
}
