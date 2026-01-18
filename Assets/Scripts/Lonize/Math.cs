using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Lonize.Math
{
    internal static class MathUtils
    {
        /// <summary>
        /// 将 value 从 [inMin, inMax] 线性映射到 [outMin, outMax]
        /// </summary>
        public static float MapLinear(float value, float inMin, float inMax, float outMin, float outMax)
        {
            if (inMax - inMin == 0f) return outMin; // 避免除零
            float t = (value - inMin) / (inMax - inMin);
            return outMin + t * (outMax - outMin);
        }
        /// <summary>
        /// fBM：用 Unity 的 Mathf.PerlinNoise 叠加多频率，返回约 0..1
        /// </summary>
        public static float FBM(float x, float y, int octaves, float lacunarity, float persistence)
        {
            float amp = 1f, freq = 1f, sum = 0f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float nx = x * freq;
                float ny = y * freq;
                float n = Mathf.PerlinNoise(nx, ny); // [0,1]
                sum += n * amp;
                norm += amp;
                amp *= persistence;
                freq *= lacunarity;
            }
            return (norm > 0f) ? sum / norm : 0f;
        }

        /// <summary>
        /// 获取区块矿物用的确定性种子哈希。
        /// </summary>
        /// <param name="worldSeed">世界种子。</param>
        /// <param name="chunkX">区块 X 坐标。</param>
        /// <param name="chunkY">区块 Y 坐标。</param>
        /// <param name="chunkZ">区块 Z 坐标。</param>
        /// <param name="salt">额外盐值。</param>
        /// <returns>Hash128 种子。</returns>
        public static Hash128 GetChunkMineralSeed(int worldSeed, int chunkX, int chunkY, int chunkZ, string salt = "Mineral")
        {
            return Hash128.Compute($"{worldSeed}_{chunkX}_{chunkY}_{chunkZ}_{salt}");
        }

        // public static int GenerateBuildingID(string BuildingID = "")
        // {
        //     return Hash128.Compute(System.Guid.NewGuid().ToString()).GetHashCode();
        // }

        public static class BezierRopeMath
        {
            /// <summary>计算二次贝塞尔曲线上 t 点坐标。</summary>
            /// <param name="p0">起点。</param>
            /// <param name="p1">控制点。</param>
            /// <param name="p2">终点。</param>
            /// <param name="t">0~1。</param>
            /// <return>曲线点。</return>
            public static Vector2 EvalQuadratic(Vector2 p0, Vector2 p1, Vector2 p2, float t)
            {
                float u = 1f - t;
                return (u * u) * p0 + (2f * u * t) * p1 + (t * t) * p2;
            }

            /// <summary>根据距离动态计算下垂量。</summary>
            /// <param name="distance">两端点距离。</param>
            /// <param name="factor">下垂系数。</param>
            /// <param name="min">最小下垂。</param>
            /// <param name="max">最大下垂。</param>
            /// <return>下垂量。</return>
            public static float CalcSag(float distance, float factor, float min, float max)
            {
                float sag = distance * factor;
                if (sag < min) sag = min;
                if (sag > max) sag = max;
                return sag;
            }

            /// <summary>生成二次贝塞尔采样点列表。</summary>
            /// <param name="p0">起点。</param>
            /// <param name="p2">终点。</param>
            /// <param name="sag">下垂量。</param>
            /// <param name="segments">分段数。</param>
            /// <param name="outPoints">输出点列表（会清空并填充）。</param>
            /// <return>采样点数量。</return>
            public static int BuildQuadraticPoints(Vector2 p0, Vector2 p2, float sag, int segments, List<Vector2> outPoints)
            {
                outPoints.Clear();

                Vector2 mid = (p0 + p2) * 0.5f;
                Vector2 p1 = mid + Vector2.down * sag;

                if (segments < 2) segments = 2;

                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    outPoints.Add(EvalQuadratic(p0, p1, p2, t));
                }

                return outPoints.Count;
            }

            /// <summary>根据距离得到合理的采样段数。</summary>
            /// <param name="distance">两端点距离。</param>
            /// <param name="step">每多少距离增加一段。</param>
            /// <param name="min">最小段数。</param>
            /// <param name="max">最大段数。</param>
            /// <return>段数。</return>
            public static int CalcSegments(float distance, float step = 40f, int min = 12, int max = 64)
            {
                int seg = Mathf.CeilToInt(distance / Mathf.Max(1f, step));
                if (seg < min) seg = min;
                if (seg > max) seg = max;
                return seg;
            }

            /// <summary>
            /// 基于世界种子、区块坐标与矿物类型生成稳定的 Hash128（字节流方式，减少分配）。
            /// </summary>
            /// <param name="worldSeed">世界种子。</param>
            /// <param name="chunkX">区块X坐标。</param>
            /// <param name="chunkY">区块Y坐标。</param>
            /// <param name="mineralType">矿物类型字符串（建议使用稳定ID或DefName）。</param>
            /// <returns>稳定的 Hash128（极低概率碰撞，但不保证数学唯一）。</returns>
            public static Hash128 GetMineralSeed(int worldSeed, int chunkX, int chunkY, string mineralType)
            {
                mineralType ??= string.Empty;

                byte[] typeBytes = Encoding.UTF8.GetBytes(mineralType);
                byte[] data = new byte[12 + typeBytes.Length];

                WriteInt32LE(data, 0, worldSeed);
                WriteInt32LE(data, 4, chunkX);
                WriteInt32LE(data, 8, chunkY);
                Buffer.BlockCopy(typeBytes, 0, data, 12, typeBytes.Length);

                return Hash128.Compute(data);
            }

            private static void WriteInt32LE(byte[] buffer, int offset, int value)
            {
                unchecked
                {
                    buffer[offset + 0] = (byte)(value);
                    buffer[offset + 1] = (byte)(value >> 8);
                    buffer[offset + 2] = (byte)(value >> 16);
                    buffer[offset + 3] = (byte)(value >> 24);
                }
            }
        
            
        
        }
        
    }


}
