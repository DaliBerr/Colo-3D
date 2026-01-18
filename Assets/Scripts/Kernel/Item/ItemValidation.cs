
using System.Collections.Generic;
using System.Text;

namespace Kernel.Item
{
    public static class ItemValidation
    {
        static readonly HashSet<string> MineralTypes = new()
        {
            "sulfide",
            "oxide",
            "carbonate",
            "silicate"
        };

        /// <summary>
        /// 校验物品定义数据是否合法。
        /// </summary>
        /// <param name="def">物品定义数据。</param>
        /// <param name="message">错误信息。</param>
        /// <param name="checkAssociatedMineralId">是否校验伴生矿物ID。</param>
        /// <returns>是否合法。</returns>
        public static bool Validate(ItemDef def, out string message, bool checkAssociatedMineralId = false)
        {
            var sb = new StringBuilder();
            bool ok = true;

            if (def == null) { message = "为空"; return false; }
            if (string.IsNullOrWhiteSpace(def.Id)) { ok = false; sb.AppendLine("缺少 id"); }
            if (def.StorageOccupation <= 0) { ok = false; sb.AppendLine("storageOccupation 必须 > 0"); }

            if (def.MineralComposition != null)
            {
                foreach (var entry in def.MineralComposition)
                {
                    if (!IsRangeBetweenZeroAndOne(entry.Value))
                    {
                        ok = false;
                        sb.AppendLine($"mineralComposition[{entry.Key}] 的 min/max 必须在 0~1 之间且 min<=max");
                    }
                }
            }

            if (def.ProcessingInfo != null)
            {
                if (!IsRangeBetweenZeroAndOne(def.ProcessingInfo.Magnetism))
                {
                    ok = false;
                    sb.AppendLine("processingInfo.magnetism 的 min/max 必须在 0~1 之间且 min<=max");
                }

                if (!IsRangeBetweenZeroAndOne(def.ProcessingInfo.ParticleSize))
                {
                    ok = false;
                    sb.AppendLine("processingInfo.particleSize 的 min/max 必须在 0~1 之间且 min<=max");
                }

                if (!IsRangeBetweenZeroAndOne(def.ProcessingInfo.Floatability))
                {
                    ok = false;
                    sb.AppendLine("processingInfo.floatability 的 min/max 必须在 0~1 之间且 min<=max");
                }

                if (!IsRangeBetweenZeroAndOne(def.ProcessingInfo.Leachability))
                {
                    ok = false;
                    sb.AppendLine("processingInfo.leachability 的 min/max 必须在 0~1 之间且 min<=max");
                }

                if (string.IsNullOrWhiteSpace(def.ProcessingInfo.MineralType) ||
                    !MineralTypes.Contains(def.ProcessingInfo.MineralType))
                {
                    ok = false;
                    sb.AppendLine("processingInfo.mineralType 必须为 sulfide/oxide/carbonate/silicate 之一");
                }

                if (checkAssociatedMineralId &&
                    !string.IsNullOrWhiteSpace(def.ProcessingInfo.AssociatedMineralId) &&
                    !ItemDatabase.TryGet(def.ProcessingInfo.AssociatedMineralId, out _))
                {
                    ok = false;
                    sb.AppendLine($"processingInfo.associatedMineralId 未找到: {def.ProcessingInfo.AssociatedMineralId}");
                }
            }

            message = sb.ToString();
            return ok;
        }

        static bool IsRangeBetweenZeroAndOne(FloatRange range) =>
            range.Min >= 0f && range.Max <= 1f && range.Min <= range.Max;
    }
}
