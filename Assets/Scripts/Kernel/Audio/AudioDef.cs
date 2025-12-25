using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kernel.Audio
{
    /// <summary>
    /// 音频定义数据，使用JSON配置并通过Addressables加载。
    /// </summary>
    [Serializable]
    public class AudioDef
    {
        /// <summary>
        /// 唯一ID，代码中通过这个ID索引音频。
        /// </summary>
        [JsonProperty("id", Required = Required.Always)]
        public string Id;

        /// <summary>
        /// 配置版本号，预留字段。
        /// </summary>
        [JsonProperty("version", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Version = "1.0.0";

        /// <summary>
        /// 显示用名称，可用于调试或编辑器中展示。
        /// </summary>
        [JsonProperty("name")]
        public string Name;

        /// <summary>
        /// 描述信息，纯展示/文案用途。
        /// </summary>
        [JsonProperty("description")]
        public string Description;

        /// <summary>
        /// Addressables中的音频资源地址或label。
        /// </summary>
        [JsonProperty("address", Required = Required.Always)]
        public string Address;

        /// <summary>
        /// 音频分类（例如 Bgm / Sfx / Ui / Voice / Ambient），字符串形式方便扩展。
        /// </summary>
        [JsonProperty("category", DefaultValueHandling = DefaultValueHandling.Populate)]
        public string Category = "Sfx";

        /// <summary>
        /// 默认音量（0~1），在未做混音调整时使用。
        /// </summary>
        [JsonProperty("volume", DefaultValueHandling = DefaultValueHandling.Populate)]
        public float DefaultVolume = 1f;

        /// <summary>
        /// 是否循环播放，常用于BGM或环境音。
        /// </summary>
        [JsonProperty("loop", DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool Loop = false;

        /// <summary>
        /// 标签列表，用于搜索、筛选或分组。
        /// </summary>
        [JsonProperty("tags")]
        public List<string> Tags = new();
    }
}
