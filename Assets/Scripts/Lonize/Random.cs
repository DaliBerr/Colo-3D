using System;

namespace Lonize
{
    public sealed class Random
    {
        /// <summary>
        /// 稳定的伪随机数生成器（PCG32）。
        /// 用于替代 System.Random，以获得跨版本/跨后端更可控的确定性输出。
        /// </summary>

        private ulong _state;
        private ulong _inc;

        /// <summary>
        /// 创建随机数生成器（非确定性）：使用当前时间作为种子。
        /// </summary>
        public Random()
            : this(unchecked((int)DateTime.UtcNow.Ticks))
        {
        }

        /// <summary>
        /// 创建随机数生成器（确定性）：相同 seed + 相同调用序列 = 相同输出。
        /// </summary>
        /// <param name="seed">32位种子。</param>
        public Random(int seed)
        {
            unchecked
            {
                uint s = (uint)seed;
                _state = 0UL;

                // 让不同 seed 走不同序列：把 seed 映射到 stream（必须为奇数）
                _inc = ((ulong)s << 1) | 1UL;

                // PCG 推荐的初始化流程
                NextUInt();
                _state += s;
                NextUInt();
            }
        }

        /// <summary>
        /// 生成一个 32 位无符号随机数。
        /// </summary>
        /// <returns>随机 uint。</returns>
        public uint NextUInt()
        {
            unchecked
            {
                ulong old = _state;
                _state = old * 6364136223846793005UL + _inc;

                uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
                int rot = (int)(old >> 59);

                return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
            }
        }

        /// <summary>
        /// 生成一个非负随机整数（范围：[0, int.MaxValue]）。
        /// </summary>
        /// <returns>非负随机 int。</returns>
        public int Next()
        {
            return (int)(NextUInt() & 0x7FFFFFFFu);
        }

        /// <summary>
        /// 生成一个随机整数（范围：[0, exclusiveMax)）。
        /// </summary>
        /// <param name="exclusiveMax">上界（不包含），必须大于 0。</param>
        /// <returns>随机 int。</returns>
        public int Next(int exclusiveMax)
        {
            unchecked
            {
                if (exclusiveMax <= 0) return 0;

                uint bound = (uint)exclusiveMax;

                // 拒绝采样，避免取模偏差
                uint threshold = (uint)(-bound) % bound;
                while (true)
                {
                    uint r = NextUInt();
                    if (r >= threshold)
                        return (int)(r % bound);
                }
            }
        }

        /// <summary>
        /// 生成一个随机整数（范围：[minInclusive, maxExclusive)）。
        /// </summary>
        /// <param name="minInclusive">下界（包含）。</param>
        /// <param name="maxExclusive">上界（不包含）。</param>
        /// <returns>随机 int。</returns>
        public int Next(int minInclusive, int maxExclusive)
        {
            unchecked
            {
                if (maxExclusive <= minInclusive) return minInclusive;
                int range = maxExclusive - minInclusive;
                return minInclusive + Next(range);
            }
        }

        /// <summary>
        /// 生成一个随机浮点数（范围：[0, 1)）。
        /// </summary>
        /// <returns>随机 float。</returns>
        public float NextFloat01()
        {
            return (float)Sample();
        }

        /// <summary>
        /// 生成一个随机双精度数（范围：[0, 1)）。
        /// </summary>
        /// <returns>随机 double。</returns>
        public double NextDouble01()
        {
            return Sample();
        }

        /// <summary>
        /// 填充随机字节到指定数组中。
        /// </summary>
        /// <param name="buffer">接收随机字节的数组。</param>
        public virtual void NextBytes(byte[] buffer)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
#if NETSTANDARD2_1 || NETSTANDARD2_1_OR_GREATER
            FillBytes(buffer.AsSpan());
#else
            FillBytes(buffer, 0, buffer.Length);
#endif
        }

#if NETSTANDARD2_1 || NETSTANDARD2_1_OR_GREATER
        /// <summary>
        /// 填充随机字节到指定缓冲区中。
        /// </summary>
        /// <param name="buffer">接收随机字节的缓冲区。</param>
        public virtual void NextBytes(Span<byte> buffer)
        {
            FillBytes(buffer);
        }
#endif

        /// <summary>
        /// 生成一个随机双精度数（范围：[0, 1)）。
        /// </summary>
        /// <returns>随机 double。</returns>
        public virtual double NextDouble()
        {
            return Sample();
        }

        /// <summary>
        /// 生成一个随机双精度采样值（范围：[0, 1)）。
        /// </summary>
        /// <returns>采样 double。</returns>
        protected virtual double Sample()
        {
            unchecked
            {
                // 用 53 位随机数生成 double（IEEE 754 有效尾数 53 位）
                ulong a = NextUInt();
                ulong b = NextUInt();
                ulong r = (a << 21) ^ b;                 // 混合到 53 位左右
                r &= ((1UL << 53) - 1UL);
                return r * (1.0 / (1UL << 53));
            }
        }

        private void FillBytes(byte[] buffer, int offset, int count)
        {
            int index = offset;
            int end = offset + count;
            while (index < end)
            {
                uint value = NextUInt();
                for (int i = 0; i < 4 && index < end; i++)
                {
                    buffer[index++] = (byte)value;
                    value >>= 8;
                }
            }
        }

#if NETSTANDARD2_1 || NETSTANDARD2_1_OR_GREATER
        private void FillBytes(Span<byte> buffer)
        {
            int index = 0;
            while (index < buffer.Length)
            {
                uint value = NextUInt();
                for (int i = 0; i < 4 && index < buffer.Length; i++)
                {
                    buffer[index++] = (byte)value;
                    value >>= 8;
                }
            }
        }
#endif

        /// <summary>
        /// 生成一个随机布尔值。
        /// </summary>
        /// <returns>随机 bool。</returns>
        public bool NextBool()
        {
            return (NextUInt() & 1u) != 0u;
        }
    }
}
