using System;
using System.Collections.Generic;

namespace Kernel.Pool
{
    /// <summary>
    /// summary: 基于 Stack 的简单数据池。
    /// </summary>
    /// <typeparam name="T">对象类型。</typeparam>
    public sealed class DataPool<T> where T : class
    {
        private readonly Stack<T> _pool;
        private readonly Func<T> _factory;
        private readonly Action<T> _reset;

        /// <summary>
        /// summary: 创建数据池。
        /// </summary>
        /// <param name="factory">对象创建方法。</param>
        /// <param name="reset">对象重置方法。</param>
        /// <param name="initialCapacity">初始容量。</param>
        /// <returns>无</returns>
        public DataPool(Func<T> factory, Action<T> reset = null, int initialCapacity = 0)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _factory = factory;
            _reset = reset;
            _pool = initialCapacity > 0 ? new Stack<T>(initialCapacity) : new Stack<T>();
        }

        /// <summary>
        /// summary: 获取一个对象。
        /// </summary>
        /// <returns>对象实例</returns>
        public T Get()
        {
            return _pool.Count > 0 ? _pool.Pop() : _factory();
        }

        /// <summary>
        /// summary: 归还一个对象。
        /// </summary>
        /// <param name="item">对象实例。</param>
        /// <returns>无</returns>
        public void Release(T item)
        {
            if (item == null)
            {
                return;
            }

            _reset?.Invoke(item);
            _pool.Push(item);
        }
    }
}
