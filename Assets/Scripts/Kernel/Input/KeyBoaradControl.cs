using System.Collections.Generic;
using Lonize.Scribe;
using UnityEngine;
namespace Kernel
{
    public static class InputConfiguration
    {
    public static Dictionary<string, KeyCode> ControlCommand = new Dictionary<string, KeyCode>
    {
        { "Up", KeyCode.W },
        { "Down", KeyCode.S },
        { "Left", KeyCode.A },
        { "Right", KeyCode.D },
        { "Pause", KeyCode.Space },
        { "NormalSpeed", KeyCode.Alpha1 },
        { "FastSpeed", KeyCode.Alpha2 },
        { "SuperFastSpeed", KeyCode.Alpha3 },
        { "StepOneTick", KeyCode.Alpha0 },
        {"back",KeyCode.Escape}
    };
    }


}
