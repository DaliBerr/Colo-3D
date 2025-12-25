
using System.Text;

namespace Kernel.Building
{
    public static class BuildingValidation
    {
        public static bool Validate(BuildingDef def, out string msg)
        {
            var sb = new StringBuilder();
            bool ok = true;

            if (def == null) { msg = "定义为空"; return false; }
            if (string.IsNullOrWhiteSpace(def.Id)) { ok = false; sb.AppendLine("缺少 id"); }
            if (string.IsNullOrWhiteSpace(def.PrefabAddress)) { ok = false; sb.AppendLine("缺少 prefab"); }
            if (def.Width <= 0 || def.Height <= 0) { ok = false; sb.AppendLine("width/height 必须 >= 1"); }
            if (def.MaxHP <= 0) { ok = false; sb.AppendLine("maxHP 必须 > 0"); }

            msg = sb.ToString();
            return ok;
        }
    }
}