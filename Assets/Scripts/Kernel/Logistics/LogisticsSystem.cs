using System.Collections.Generic;
using UnityEngine;
using Kernel.Pool;
using Kernel.Storage;
using Lonize.Tick;

namespace Kernel.Logistics
{
    /// <summary>
    /// summary: 运输指令。
    /// </summary>
    public sealed class TransitOrder
    {
        public long ReservationId { get; private set; }
        public long ContainerId { get; private set; }
        public string ItemId { get; private set; }
        public int Count { get; private set; }
        public Vector2Int FromCell { get; private set; }
        public float EtaSeconds { get; private set; }
        public float RemainingSeconds { get; set; }

        /// <summary>
        /// summary: 初始化运输指令。
        /// param: reservationId 预占ID
        /// param: containerId 容器ID
        /// param: itemId 物品ID
        /// param: count 数量
        /// param: fromCell 起点格子
        /// param: etaSeconds ETA秒数
        /// param: remainingSeconds 剩余秒数
        /// return: 无
        /// </summary>
        public void Initialize(
            long reservationId,
            long containerId,
            string itemId,
            int count,
            Vector2Int fromCell,
            float etaSeconds,
            float remainingSeconds)
        {
            ReservationId = reservationId;
            ContainerId = containerId;
            ItemId = itemId;
            Count = count;
            FromCell = fromCell;
            EtaSeconds = etaSeconds;
            RemainingSeconds = remainingSeconds;
        }

        /// <summary>
        /// summary: 重置运输指令。
        /// return: 无
        /// </summary>
        public void Reset()
        {
            ReservationId = 0;
            ContainerId = 0;
            ItemId = null;
            Count = 0;
            FromCell = default;
            EtaSeconds = 0f;
            RemainingSeconds = 0f;
        }
    }

    /// <summary>
    /// summary: 运输系统（管理运输队列与预占确认）。
    /// </summary>
    public sealed class LogisticsSystem : ITickable
    {
        private static readonly LogisticsSystem _instance = new LogisticsSystem();
        public static LogisticsSystem Instance => _instance;

        private readonly DataPool<TransitOrder> _orderPool = new(() => new TransitOrder(), order => order.Reset());
        private readonly List<TransitOrder> _queue = new();

        private LogisticsSystem() { }

        /// <summary>
        /// summary: 尝试加入运输队列（先预占存储）。
        /// param: itemId 物品ID
        /// param: count 数量
        /// param: fromCell 起点格子
        /// param: etaSeconds ETA秒数
        /// return: 是否成功加入
        /// </summary>
        public bool EnqueueOrder(string itemId, int count, Vector2Int fromCell, float etaSeconds)
        {
            if (!StorageSystem.Instance.TryReserveBest(itemId, count, fromCell, out var reservation))
            {
                Debug.LogWarning($"[Logistics] 预占失败：{itemId} x{count} from {fromCell}.");
                return false;
            }

            var order = _orderPool.Get();
            order.Initialize(
                reservation.ReservationId,
                reservation.ContainerId,
                reservation.ItemId,
                reservation.Count,
                fromCell,
                etaSeconds,
                etaSeconds);
            _queue.Add(order);
            return true;
        }

        /// <summary>
        /// summary: Tick 推进运输进度并确认预占。
        /// param: ticks Tick 数量
        /// return: 无
        /// </summary>
        public void Tick(int ticks)
        {
            if (ticks <= 0)
            {
                return;
            }

            float deltaSeconds = ticks * ResolveTickSeconds();
            for (int i = _queue.Count - 1; i >= 0; i--)
            {
                var order = _queue[i];
                order.RemainingSeconds -= deltaSeconds;

                if (order.RemainingSeconds > 0f)
                {
                    _queue[i] = order;
                    continue;
                }

                if (!StorageSystem.Instance.ConfirmReservedStore(order.ReservationId))
                {
                    StorageSystem.Instance.CancelReservation(order.ReservationId);
                    Debug.LogWarning($"[Logistics] 确认预占失败，已尝试取消：{order.ItemId} x{order.Count} reservation {order.ReservationId}.");
                }

                _queue.RemoveAt(i);
                _orderPool.Release(order);
            }
        }

        /// <summary>
        /// summary: 获取每 Tick 对应的秒数。
        /// return: 秒数
        /// </summary>
        private float ResolveTickSeconds()
        {
            var driver = TickDriver.Instance;
            if (driver != null && driver.tickManager != null)
            {
                return Mathf.Max(0.0001f, driver.tickManager.TimeCtrl.BaseTickSeconds);
            }

            return 1f / 60f;
        }
    }
}
