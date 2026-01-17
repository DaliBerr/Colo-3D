
using System.Text;

namespace Kernel.Item
{
    public static class ItemValidation
    {
        /// <summary>
        /// 校验物品定义数据是否合法。
        /// </summary>
        /// <param name="def">物品定义数据。</param>
        /// <param name="message">错误信息。</param>
        /// <returns>是否合法。</returns>
        public static bool Validate(ItemDef def, out string message)
        {
            var sb = new StringBuilder();
            bool ok = true;

            if (def == null) { message = "为空"; return false; }
            if (string.IsNullOrWhiteSpace(def.Id)) { ok = false; sb.AppendLine("缺少 id"); }
            if (def.StorageOccupation <= 0) { ok = false; sb.AppendLine("storageOccupation 必须 > 0"); }

            message = sb.ToString();
            return ok;
        }
    }
}
