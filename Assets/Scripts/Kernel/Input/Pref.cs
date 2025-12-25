
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using Lonize.Logging;
// using Lonize.Scribe;
// using Unity.VisualScripting;
// using UnityEngine;

// namespace Kernel
// {
//     public sealed class SaveControlCommand : ISaveItem
//     {
//         public string TypeId => "ControlCommandPref";
//         public Dictionary<string, KeyCode> ControlCommand;

//         public void ExposeData()
//         {
//             Scribe.Scribe_Generic.Look("ControlCommand", ref ControlCommand, defaultValue: null, forceSave: true);
//         }

//         public void ApplyControlPrefsFromSave()
//         {
//             var items = ScribeSaveManager.Instance.GetItems<SaveControlCommand>();
//             // Log.Info($"[SaveControlCommand] Found {items.Count()} saved control pref items.");
//             SaveControlCommand pref = null;

//             foreach (var it in items) { pref = it; break; } // 取第一个
//             if (pref == null)
//             {
//                 // Log.Info("[SaveControlCommand] No saved control prefs found, creating default.");
//                 // 存档里没有 → 创建一个带默认字典的条目并保存
//                 pref = new SaveControlCommand
//                 {
//                     ControlCommand = new Dictionary<string, KeyCode>(InputConfiguration.ControlCommand)
//                 };
//                 ScribeSaveManager.Instance.AddItem(pref);
//                 ScribeSaveManager.SaveNow();
//                 return;
//             }
//             // Log.Info(pref.ToString());
//             // 合并
//             // var loaded = pref.ControlCommand ?? new Dictionary<string, KeyCode>();
//             foreach (var kv in pref.ControlCommand)
//             {
//                 // Log.Info($"Loaded Pref - Key: {kv.Key}, Value: {kv.Value}");
//                 if (InputConfiguration.ControlCommand.ContainsKey(kv.Key))
//                     InputConfiguration.ControlCommand[kv.Key] = kv.Value;
//             }
//         }
//     }
// }