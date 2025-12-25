

using System.Collections.Generic;
using Lonize.Scribe;
using UnityEngine;

namespace Kernel
{
    public sealed class SaveBool : ISaveItem
    {
        public string TypeId => "Bool";
        public bool Value;


        public void ExposeData()
        {
            Scribe_Values.Look("v", ref Value, false);
        }
    }

    public sealed class SaveInt : ISaveItem
    {
        public string TypeId => "Int";
        public int Value;


        public void ExposeData()
        {
            Scribe_Values.Look("v", ref Value, 0);
        }
    }

    public sealed class SaveFloat : ISaveItem
    {
        public string TypeId => "Float";
        public float Value;


        public void ExposeData()
        {
            Scribe_Values.Look("v", ref Value, 0f);
        }
    }
    public sealed class SaveString : ISaveItem
    {
        public string TypeId => "String";
        public string Value;
        public void ExposeData()
        {
            Scribe_Values.Look("v", ref Value, null);
        }
    }


    // public sealed class SaveDictionaryStringKeyCode : ISaveItem
    // {
    //     public string TypeId => "DictionaryStringKeyCode";
    //     public Dictionary<string, KeyCode> Value = new();

    //     public void ExposeData()
    //     {
    //         Scribe_Deep.Look("v", ref Value);
    //         Scribe_Collections.LookDictionary("v", ref Value, LookMode.Value, LookMode.Value);
    //     }

    // }
}