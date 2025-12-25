using System;
using System.Collections.Generic;
using Lonize.Logging;

// using Lonize.Item;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kernel.Item
{
    [Serializable]
    public class ItemComponentData
    {
        [JsonProperty("type", Required = Required.Always)]
        public string Type;

        [JsonProperty("params")]
        public JObject Params; // 原始参数，工厂来解释
    }

    // 运行时行为接口
    public interface IItemBehaviour
    {
        // 绑定到某个物品实例时触发
        void OnBind(ItemInstance inst);
    }

    // 运行时物品实例（数据 + 临时状态）
    [Serializable]
    public class ItemInstance
    {
        public ItemDef Def;
        public int Stack = 1;

        // 动态状态举例：耐久、改名、随机种子等
        public Dictionary<string, float> RuntimeStats = new();
    }

    // 一些内置组件示例：

    // ① 可消耗：恢复生命/体力…
    public class ConsumableBehaviour : IItemBehaviour
    {
        public float HealAmount;

        public ConsumableBehaviour(float heal) => HealAmount = heal;

        public void OnBind(ItemInstance inst) { /* 可留空 */ }

        public void Consume(Action<float> onHeal)
        {
            onHeal?.Invoke(HealAmount);
        }
    }

    // ② 可装备：定义槽位与属性加成
    public class EquippableBehaviour : IItemBehaviour
    {
        public string Slot; // e.g. "mainhand", "head"
        public Dictionary<string, float> Modifiers = new();

        public EquippableBehaviour(string slot, Dictionary<string, float> mods)
        {
            Slot = slot; Modifiers = mods;
        }

        public void OnBind(ItemInstance inst) { /* 可留空 */ }
    }

    // 组件工厂：把 ItemComponentData → IItemBehaviour
    public static class ItemBehaviourFactory
    {
        public static IItemBehaviour Create(ItemComponentData data)
        {
            switch (data.Type)
            {
                case "consumable":
                {
                    float heal = data.Params?["heal"]?.Value<float>() ?? 0f;
                    return new ConsumableBehaviour(heal);
                }
                case "equippable":
                {
                    string slot = data.Params?["slot"]?.Value<string>() ?? "mainhand";
                    var mods = new Dictionary<string, float>();
                    var jmods = data.Params?["mods"] as JObject;
                    if (jmods != null)
                        foreach (var p in jmods)
                            mods[p.Key] = p.Value.Value<float>();
                    return new EquippableBehaviour(slot, mods);
                }
                // 继续扩展你的类型……
                default:
                    GameDebug.LogWarning($"[Items] 未知组件类型: {data.Type}");
                    return null;
            }
        }
    }
}