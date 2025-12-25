using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lonize.Logging;
using UnityEngine;

namespace Kernel.Audio
{
    /// <summary>
    /// 音频分类枚举，用于控制不同类型音量。
    /// </summary>
    public enum AudioCategory
    {
        Bgm,
        Sfx,
        Ui,
        Voice,
        Ambient
    }

    /// <summary>
    /// 全局音频管理器，负责播放BGM/SFX等，并基于AudioDef+Addressables加载资源。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        /// <summary>
        /// 全局单例实例。
        /// </summary>
        public static AudioManager Instance { get; private set; }
        


        [Header("SFX Settings")]
        [SerializeField]
        private int _prewarmSfxSourceCount = 4;

        /// <summary>
        /// 音频定义对应的Clip缓存，key为AudioDef.Id。
        /// </summary>
        private readonly Dictionary<string, AudioClip> _clipCache = new();

        /// <summary>
        /// 各分类音量，key为AudioCategory。
        /// </summary>
        private readonly Dictionary<AudioCategory, float> _categoryVolumes = new();

        /// <summary>
        /// SFX用的AudioSource池。
        /// </summary>
        private readonly List<AudioSource> _sfxSources = new();

        /// <summary>
        /// BGM淡入淡出用的AudioSource A。
        /// </summary>
        private AudioSource _bgmSourceA;

        /// <summary>
        /// BGM淡入淡出用的AudioSource B。
        /// </summary>
        private AudioSource _bgmSourceB;

        /// <summary>
        /// 当前正在播放BGM的AudioSource。
        /// </summary>
        private AudioSource _currentBgmSource;

        /// <summary>
        /// 是否已经初始化过（加载过AudioDef）。
        /// </summary>
        private bool _initialized;

        /// <summary>
        /// 当前是否正在进行BGM淡入淡出。
        /// </summary>
        private bool _isFadingBgm;

        /// <summary>
        /// Awake中初始化单例、音量字典和必要的AudioSource。
        /// </summary>
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitCategoryVolumes();
            CreateBgmSources();
            PrewarmSfxSources();
        }

        /// <summary>
        /// Start中触发异步初始化，加载所有AudioDef。
        /// </summary>
        private async void Start()
        {
            await InitAsync();
        }

        /// <summary>
        /// 初始化各个分类的默认音量。
        /// </summary>
        private void InitCategoryVolumes()
        {
            foreach (AudioCategory cat in Enum.GetValues(typeof(AudioCategory)))
            {
                _categoryVolumes[cat] = 1f;
            }
        }
        /// <summary>
        /// OnDestroy回调，销毁时清理单例引用。
        /// </summary>
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        /// <summary>
        /// 创建用于BGM播放的双AudioSource（支持交叉淡入淡出）。
        /// </summary>
        private void CreateBgmSources()
        {
            _bgmSourceA = gameObject.AddComponent<AudioSource>();
            _bgmSourceA.playOnAwake = false;
            _bgmSourceA.loop = true;

            _bgmSourceB = gameObject.AddComponent<AudioSource>();
            _bgmSourceB.playOnAwake = false;
            _bgmSourceB.loop = true;

            _currentBgmSource = _bgmSourceA;
        }

        /// <summary>
        /// 预创建一定数量的SFX AudioSource以减少运行时GC。
        /// </summary>
        private void PrewarmSfxSources()
        {
            for (int i = 0; i < _prewarmSfxSourceCount; i++)
            {
                var src = CreateNewSfxSource();
                _sfxSources.Add(src);
            }
        }

        /// <summary>
        /// 创建一个新的SFX AudioSource并挂在AudioManager下。
        /// </summary>
        /// <returns>新创建的AudioSource。</returns>
        private AudioSource CreateNewSfxSource()
        {
            var go = new GameObject("SFX_AudioSource");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            return src;
        }
        // private bool _initialized;
        private Task _initTask;
        /// <summary>
        /// 异步初始化音频系统，加载所有AudioDef。
        /// </summary>
        /// <returns>异步任务。</returns>
        public Task InitAsync()
        {
            // 已经初始化完成
            if (_initialized)
                return Task.CompletedTask;

            // 已经有一个初始化任务在进行中，复用它
            if (_initTask != null)
                return _initTask;

            // 创建新的初始化任务
            _initTask = InitInternalAsync();
            return _initTask;
        }
        /// <summary>
        /// 内部实际执行初始化的协程。
        /// </summary>
        /// <returns>异步任务。</returns>
        private async Task InitInternalAsync()
        {
            await AudioDatabase.LoadAllAsync("AudioDef");
            _initialized = true;
        }

        /// <summary>
        /// 异步加载指定AudioDef对应的AudioClip，并使用本地缓存。
        /// </summary>
        /// <param name="def">音频定义。</param>
        /// <returns>加载成功的AudioClip，失败返回null。</returns>
        private async Task<AudioClip> LoadClipAsync(AudioDef def)
        {
            if (def == null)
                return null;

            if (_clipCache.TryGetValue(def.Id, out var cached))
                return cached;

            if (string.IsNullOrEmpty(def.Address))
            {
                GameDebug.LogError($"[AudioManager] AudioDef({def.Id}) 的 Address 为空。");
                return null;
            }

            var clip = await AddressableRef.LoadAsync<AudioClip>(def.Address);
            if (clip == null)
            {
                GameDebug.LogError($"[AudioManager] 无法通过Addressables加载AudioClip: {def.Address}");
                return null;
            }

            _clipCache[def.Id] = clip;
            return clip;
        }

        /// <summary>
        /// 获取一个空闲的SFX AudioSource，如没有则新建一个。
        /// </summary>
        /// <returns>可用的AudioSource。</returns>
        private AudioSource GetFreeSfxSource()
        {
            for (int i = 0; i < _sfxSources.Count; i++)
            {
                var src = _sfxSources[i];
                if (!src.isPlaying)
                    return src;
            }

            var newSrc = CreateNewSfxSource();
            _sfxSources.Add(newSrc);
            return newSrc;
        }

        /// <summary>
        /// 将AudioDef中的字符串分类映射到AudioCategory枚举。
        /// </summary>
        /// <param name="def">音频定义。</param>
        /// <returns>对应的AudioCategory枚举值。</returns>
        private AudioCategory GetCategoryEnum(AudioDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Category))
                return AudioCategory.Sfx;

            if (Enum.TryParse<AudioCategory>(def.Category, true, out var cat))
                return cat;

            return AudioCategory.Sfx;
        }

        /// <summary>
        /// 在指定位置播放一次性音效（SFX/Ui），使用AudioDef和音量混音。
        /// </summary>
        /// <param name="id">音频ID，对应AudioDef.id。</param>
        /// <param name="position">世界坐标位置。</param>
        /// <param name="parent">可选父物体，为null则挂在AudioManager下。</param>
        /// <returns>用于播放的AudioSource，失败返回null。</returns>
        public async Task<AudioSource> PlaySfxAsync(string id, Vector3 position, Transform parent = null)
        {
            if (!_initialized)
                await InitAsync();

            if (!AudioDatabase.TryGet(id, out var def))
            {
                Log.Warn($"[AudioManager] 未找到AudioDef: {id}");
                GameDebug.LogWarning($"[AudioManager] 未找到AudioDef: {id}");
                return null;
            }

            var clip = await LoadClipAsync(def);
            if (clip == null)
                return null;

            var src = GetFreeSfxSource();

            if (parent != null)
            {
                src.transform.SetParent(parent);
                src.transform.localPosition = Vector3.zero;
            }
            else
            {
                src.transform.SetParent(transform);
                src.transform.position = position;
            }

            var cat = GetCategoryEnum(def);

            src.clip = clip;
            src.loop = def.Loop;
            src.volume = def.DefaultVolume * GetCategoryVolume(cat);
            src.spatialBlend = 0f; // 如需3D音效可改为1
            src.Play();

            return src;
        }

        /// <summary>
        /// 在AudioManager位置播放一次性音效，常用于UI点击等。
        /// </summary>
        /// <param name="id">音频ID，对应AudioDef.id。</param>
        /// <returns>用于播放的AudioSource，失败返回null。</returns>
        public async Task<AudioSource> PlaySfxAsync(string id)
        {
            return await PlaySfxAsync(id, transform.position, transform);
        }

        /// <summary>
        /// 播放或切换BGM，可指定淡入淡出时间。
        /// </summary>
        /// <param name="id">BGM的AudioDef.id。</param>
        /// <param name="fadeTime">淡入淡出时间（秒），小于等于0时直接切换。</param>
        /// <returns>异步任务。</returns>
        public async Task PlayBgmAsync(string id, float fadeTime = 0.5f)
        {
            if (!_initialized)
                await InitAsync();

            if (!AudioDatabase.TryGet(id, out var def))
            {
                Log.Warn($"[AudioManager] 未找到BGM的AudioDef: {id}");
                GameDebug.LogWarning($"[AudioManager] 未找到BGM的AudioDef: {id}");
                return;
            }

            var clip = await LoadClipAsync(def);
            if (clip == null)
                return;

            var target = (_currentBgmSource == _bgmSourceA) ? _bgmSourceB : _bgmSourceA;
            target.clip = clip;
            target.loop = true;
            target.volume = 0f;
            target.Play();

            float targetVolume = def.DefaultVolume * GetCategoryVolume(AudioCategory.Bgm);

            if (fadeTime <= 0f)
            {
                _currentBgmSource.Stop();
                target.volume = targetVolume;
                _currentBgmSource = target;
            }
            else
            {
                StartCoroutine(CrossFadeBgmCoroutine(_currentBgmSource, target, fadeTime, targetVolume));
            }
        }

        /// <summary>
        /// 停止当前BGM，可带淡出时间。
        /// </summary>
        /// <param name="fadeTime">淡出时间（秒），小于等于0时立刻停止。</param>
        public void StopBgm(float fadeTime = 0.5f)
        {
            if (_currentBgmSource == null || !_currentBgmSource.isPlaying)
                return;

            if (fadeTime <= 0f)
            {
                _currentBgmSource.Stop();
                return;
            }

            StartCoroutine(FadeOutBgmCoroutine(_currentBgmSource, fadeTime));
        }

        /// <summary>
        /// 设置某个音频分类的音量（0~1），并应用到当前播放中的音源。
        /// </summary>
        /// <param name="category">音频分类。</param>
        /// <param name="volume">音量值，0到1之间。</param>
        public void SetCategoryVolume(AudioCategory category, float volume)
        {
            volume = Mathf.Clamp01(volume);
            _categoryVolumes[category] = volume;
            ApplyCategoryVolume(category);
        }

        /// <summary>
        /// 获取指定音频分类的当前音量。
        /// </summary>
        /// <param name="category">音频分类。</param>
        /// <returns>音量值（0~1）。</returns>
        public float GetCategoryVolume(AudioCategory category)
        {
            if (_categoryVolumes.TryGetValue(category, out var v))
                return v;

            return 1f;
        }

        /// <summary>
        /// 将分类音量应用到相关的AudioSource上。
        /// </summary>
        /// <param name="category">音频分类。</param>
        private void ApplyCategoryVolume(AudioCategory category)
        {
            // BGM分类：更新当前BGM音量
            if (category == AudioCategory.Bgm && _currentBgmSource != null && _currentBgmSource.clip != null)
            {
                _currentBgmSource.volume = GetCategoryVolume(AudioCategory.Bgm);
            }

            // SFX/UI：简单示例，遍历SFX池（若需要更精细可以按Def分类缓存）
            if (category == AudioCategory.Sfx || category == AudioCategory.Ui)
            {
                foreach (var src in _sfxSources)
                {
                    if (src.isPlaying && src.clip != null)
                    {
                        src.volume = GetCategoryVolume(category);
                    }
                }
            }
        }

        /// <summary>
        /// BGM交叉淡入淡出协程。
        /// </summary>
        /// <param name="from">原BGM AudioSource。</param>
        /// <param name="to">目标BGM AudioSource。</param>
        /// <param name="fadeTime">淡入淡出时间（秒）。</param>
        /// <param name="targetVolume">目标音量。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator CrossFadeBgmCoroutine(AudioSource from, AudioSource to, float fadeTime, float targetVolume)
        {
            _isFadingBgm = true;

            float time = 0f;
            float fromStartVolume = (from != null) ? from.volume : 0f;

            while (time < fadeTime)
            {
                time += Time.unscaledDeltaTime;
                float t = time / fadeTime;

                if (from != null)
                    from.volume = Mathf.Lerp(fromStartVolume, 0f, t);

                to.volume = Mathf.Lerp(0f, targetVolume, t);

                yield return null;
            }

            if (from != null)
                from.Stop();

            to.volume = targetVolume;
            _currentBgmSource = to;
            _isFadingBgm = false;
        }

        /// <summary>
        /// BGM淡出协程，用于优雅地停止背景音乐。
        /// </summary>
        /// <param name="source">需要淡出的AudioSource。</param>
        /// <param name="fadeTime">淡出时间（秒）。</param>
        /// <returns>协程迭代器。</returns>
        private IEnumerator FadeOutBgmCoroutine(AudioSource source, float fadeTime)
        {
            float time = 0f;
            float startVolume = source.volume;

            while (time < fadeTime)
            {
                time += Time.unscaledDeltaTime;
                float t = time / fadeTime;
                source.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }

            source.Stop();
            source.volume = 0f;
        }
    }
}
