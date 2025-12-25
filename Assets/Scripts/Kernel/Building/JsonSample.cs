
using Lonize.Logging;

namespace Kernel.Building
{
    public  class JsonSample
    {
        public void Sample()
        {
            if (BuildingDatabase.TryGet("generator_small", out var def))
            {
                Log.Info(def.Name);       // 从 JSON 来的 name
                Log.Info(def.Width.ToString());      // from json
                Log.Info(def.Height.ToString());     // from json
                Log.Info(def.Cost["iron_plate"].ToString()); // from json
            }
        }
    }
}