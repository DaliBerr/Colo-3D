using System.Collections.Generic;

namespace Kernel.Building
{
    public class ProducerBehaviour : IBuildingBehaviour
    {
        public float CraftTime;
        public Dictionary<string, int> Inputs = new();
        public Dictionary<string, int> Outputs = new();
        public void OnBind(BuildingRuntime r) { }

        public void OnUnbind(BuildingRuntime runtime)
        {
            // throw new NotImplementedException();
        }

        public ProducerBehaviour(float t, Dictionary<string, int> i, Dictionary<string, int> o)
        {
            CraftTime = t;
            Inputs = i ?? new();
            Outputs = o ?? new();
        }
        public void Tick(int ticks)
        {
            // throw new NotImplementedException();
        }
    }

}