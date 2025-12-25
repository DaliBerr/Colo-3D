

using System.Collections.Generic;
using Lonize.Scribe;

namespace Kernel
{
    public sealed class PolySaveData : IExposable
    {
        public List<ISaveItem> Items = new();

        public void ExposeData()
        {
            Scribe_Polymorph.LookList("items", ref Items);
        }
    }
}