
using System.Text;

namespace Kernel.Item
{
    public static class ItemValidation
    {
        public static bool Validate(ItemDef def, out string message)
        {
            var sb = new StringBuilder();
            bool ok = true;

            if (def == null) { message = "为空"; return false; }
            if (string.IsNullOrWhiteSpace(def.Id)) { ok = false; sb.AppendLine("缺少 id"); }
            if (def.MaxStack <= 0) { ok = false; sb.AppendLine("maxStack 必须 > 0"); }

            message = sb.ToString();
            return ok;
        }
    }
}