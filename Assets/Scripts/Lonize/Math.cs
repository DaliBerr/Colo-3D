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
    
        // public static int GenerateBuildingID(string BuildingID = "")
        // {
        //     return Hash128.Compute(System.Guid.NewGuid().ToString()).GetHashCode();
        // }
    }


}