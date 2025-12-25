using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Lonize.Logging;
using UnityEngine;

namespace Kernel.Audio
{
    /// <summary>
    /// BGM播放列表控制器，按顺序或随机循环播放一组AudioDef定义的BGM。
    /// </summary>
    public class BgmPlaylistPlayer : MonoBehaviour
    {
        [Header("Playlist")]
        [SerializeField]
        private List<string> _bgmIds = new(); // Inspector填AudioDef.id

        [Header("Behaviour")]
        [SerializeField]
        private bool _playOnStart = true;

        [SerializeField]
        private bool _loopPlaylist = true;

        [SerializeField]
        private bool _shuffle = false;

        [Header("Timing")]
        [SerializeField]
        private float _fadeTime = 0.5f;

        [SerializeField]
        private float _gapTime = 0f;

        /// <summary>
        /// 当前播放列表的取消令牌源，用于停止异步播放任务。
        /// </summary>
        private CancellationTokenSource _cts;

        /// <summary>
        /// 缓存每个BGM的时长（秒），避免重复查询。
        /// </summary>
        private readonly Dictionary<string, float> _lengthCache = new();

        /// <summary>
        /// 内部随机数生成器，用于随机播放模式。
        /// </summary>
        private System.Random _random;

        /// <summary>
        /// Awake生命周期回调，初始化随机数生成器。
        /// </summary>
        private void Awake()
        {
            _random = new System.Random();
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Start生命周期回调，根据配置决定是否自动开始播放列表。
        /// </summary>
        private void Start()
        {
            if (_playOnStart)
            {
                StartPlaylist();
            }
        }

        /// <summary>
        /// OnDisable生命周期回调，组件禁用时停止播放列表。
        /// </summary>
        private void OnDisable()
        {
            StopPlaylist();
        }

        /// <summary>
        /// OnDestroy生命周期回调，组件销毁时停止播放列表。
        /// </summary>
        private void OnDestroy()
        {
            StopPlaylist();
        }

        /// <summary>
        /// 开始播放BGM播放列表，如已有播放则先停止再重启。
        /// </summary>
        public void StartPlaylist()
        {
            StopPlaylist();

            if (_bgmIds == null || _bgmIds.Count == 0)
            {
                GameDebug.LogWarning("[BgmPlaylistPlayer] 播放列表为空，无法开始播放。");
                return;
            }

            if (!Application.isPlaying)
            {
                // 编辑器非运行状态不启动播放
                return;
            }

            _cts = new CancellationTokenSource();
            _ = RunPlaylistAsync(_cts.Token);
        }

        /// <summary>
        /// 停止当前播放列表并淡出当前BGM。
        /// </summary>
        public void StopPlaylist()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (Application.isPlaying && AudioManager.Instance != null)
            {
                AudioManager.Instance.StopBgm(_fadeTime);
            }
        }

        /// <summary>
        /// 跳到下一首BGM：终止当前循环并重新启动播放列表。
        /// </summary>
        public void SkipNext()
        {
            if (!Application.isPlaying)
                return;

            if (_cts == null)
            {
                StartPlaylist();
                return;
            }

            _cts.Cancel();
            _cts.Dispose();
            _cts = new CancellationTokenSource();
            _ = RunPlaylistAsync(_cts.Token);
        }

        /// <summary>
        /// 异步运行播放列表主循环，按配置顺序或随机播放，并在需要时循环列表。
        /// </summary>
        /// <param name="token">取消令牌，用于终止播放循环。</param>
        private async Task RunPlaylistAsync(CancellationToken token)
        {
            if (!Application.isPlaying)
                return;

            if (AudioManager.Instance == null)
            {
                GameDebug.LogError("[BgmPlaylistPlayer] 场景中没有 AudioManager 实例。");
                return;
            }

            await AudioManager.Instance.InitAsync();

            if (_bgmIds == null || _bgmIds.Count == 0)
                return;

            int index = 0;
            int playedCountInCycle = 0;

            while (!token.IsCancellationRequested && Application.isPlaying)
            {
                if (_bgmIds.Count == 0)
                    break;

                // Play Mode 被手动停止了，直接跳出循环
                if (!Application.isPlaying)
                    break;

                string id;

                if (_shuffle)
                {
                    int randomIndex = _random.Next(0, _bgmIds.Count);
                    id = _bgmIds[randomIndex];
                }
                else
                {
                    if (index >= _bgmIds.Count)
                    {
                        index = 0;
                        playedCountInCycle = 0;

                        if (!_loopPlaylist)
                        {
                            // 不循环则一轮播放完直接结束
                            break;
                        }
                    }

                    id = _bgmIds[index];
                }

                try
                {
                    await AudioManager.Instance.PlayBgmAsync(id, _fadeTime);
                }
                catch (System.Exception ex)
                {
                    // 如果是在退出Play模式过程中，不再刷错误日志
                    if (!Application.isPlaying || token.IsCancellationRequested)
                        break;

                    Log.Error($"[BgmPlaylistPlayer] 播放 BGM 失败（id={id}）：\n{ex}");
                    GameDebug.LogError($"[BgmPlaylistPlayer] 播放 BGM 失败（id={id}）：\n{ex}");
                }

                if (!Application.isPlaying || token.IsCancellationRequested)
                    break;

                // 计算这首歌需要等待的时长
                float length = await GetClipLengthAsync(id);
                if (length <= 0f)
                {
                    length = 10f; // 防御：拿不到长度给一个保守值
                }

                float waitSeconds = Mathf.Max(0f, length + _gapTime - _fadeTime);

                // 使用Task.Delay而不是Time.unscaledDeltaTime循环，避免编辑器非运行状态卡死
                if (waitSeconds > 0f)
                {
                    try
                    {
                        int delayMs = Mathf.RoundToInt(waitSeconds * 1000f);
                        await Task.Delay(delayMs, token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }

                if (!Application.isPlaying || token.IsCancellationRequested)
                    break;

                if (!_shuffle)
                {
                    index++;
                    playedCountInCycle++;

                    if (!_loopPlaylist && playedCountInCycle >= _bgmIds.Count)
                    {
                        // 非循环模式，一轮播放完退出
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 异步获取指定BGM的长度（秒），带本地缓存。
        /// </summary>
        /// <param name="id">AudioDef.id。</param>
        /// <returns>音频长度（秒），失败时返回0。</returns>
        private async Task<float> GetClipLengthAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
                return 0f;

            if (_lengthCache.TryGetValue(id, out var len))
                return len;

            if (!AudioDatabase.TryGet(id, out var def))
            {
                Log.Warn($"[BgmPlaylistPlayer] 未找到 AudioDef: {id}");
                GameDebug.LogWarning($"[BgmPlaylistPlayer] 未找到 AudioDef: {id}");
                return 0f;
            }

            // Play模式退出后就不要再去加载资源了
            if (!Application.isPlaying)
                return 0f;

            var clip = await AddressableRef.LoadAsync<AudioClip>(def.Address);
            if (clip == null)
            {
                Log.Warn($"[BgmPlaylistPlayer] 无法加载 AudioClip: {def.Address}");
                GameDebug.LogWarning($"[BgmPlaylistPlayer] 无法加载 AudioClip: {def.Address}");
                return 0f;
            }

            len = clip.length;
            _lengthCache[id] = len;
            return len;
        }
    }
}
