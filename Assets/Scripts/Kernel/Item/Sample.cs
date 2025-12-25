
using Lonize.Logging;

namespace Kernel.Item
{
    public  class JsonSample
    {
        public void Sample()
        {
            if (ItemDatabase.TryGet("generator_small", out var def))
            {
                Log.Info(def.Name);       // 从 JSON 来的 name
                Log.Info(def.MaxStack.ToString());      // from json
                Log.Info(def.Description); // from json
            }
        }
    }
}