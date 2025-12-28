using System.IO;
using UnityEngine;
using Lonize.Scribe;
using Lonize.Logging;
using UnityEngine.UI;
using Lonize.Events;
using System.Collections.Generic;
using System.Collections;
using static Lonize.Events.EventList;

namespace Kernel{
public class OptionsManager : MonoBehaviour
{
    public static OptionsManager Instance { get; private set; }

    // ★★★ 核心：这里是你的设置数据实例 ★★★
    public GlobalSettings Settings = new GlobalSettings();

    // ★★★ 核心：指定一个不同的文件名 ★★★
    private string filePath;

    private UIScale _uiScale;

    // private DisplayModeDropDown displayMode;
    // private MaxFrameDropDown maxFrame;
    // private ResolutionDropDown resolutionDropDown;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 设置文件路径，与 save.json 分开
        filePath = Path.Combine(Application.persistentDataPath, "settings.json");
        _uiScale = GetComponentInChildren<UIScale>();
        // 游戏启动时立刻加载设置
        LoadOptions();
    }
    private void Start()
    {
        // displayMode = Object.FindAnyObjectByType<DisplayModeDropDown>();
        // maxFrame = Object.FindAnyObjectByType<MaxFrameDropDown>();
        // resolutionDropDown = Object.FindAnyObjectByType<ResolutionDropDown>();
    }
    public void SaveOptions()
    {
        try
        {
            // 1. 创建文件流 (Create/Overwrite)
            using (var fs = File.Create(filePath))
            {
                // 2. 初始化 Scribe (这是通用的，给它什么流它就写哪里)
                Scribe.InitSaving(fs);

                // 3. 写入你的设置对象
                // 注意：这里不需要 Items 列表，直接 Look 这个对象即可
                Scribe.Look(ref Settings);

                // 4. 结束写入
                Scribe.FinalizeWriting();
            }
            GameDebug.Log("[Options] Settings saved to " + filePath);
            Log.Info("[Options] Settings saved to " + filePath);
        }
        catch (System.Exception ex)
        {
            GameDebug.LogError("[Options] Failed to save settings: " + ex);
            Log.Error("[Options] Failed to save settings: " + ex);
            Scribe.FinalizeWriting(); // 确保状态复位
        }
    }

    public void LoadOptions()
    {
        if (!File.Exists(filePath))
        {
            GameDebug.LogWarning("[Options] No settings file found, using defaults.");
            Log.Warn("[Options] No settings file found, using defaults.");
            Settings = new GlobalSettings(); // 使用默认值
            SaveOptions(); // 生成一份默认文件
            return;
        }

        try
        {
            using (var fs = File.OpenRead(filePath))
            {
                Scribe.InitLoading(fs);
                
                // 读取数据到 Settings 变量中
                Scribe.Look(ref Settings);
                
                Scribe.FinalizeLoading();
            }
            // foreach(var setting in Settings.GetType().GetProperties())
            // {
            //     GameDebug.Log($"[Options] Loaded setting: {setting.Name} = {setting.GetValue(Settings)}");
            // }
            GameDebug.Log("[Options] Settings loaded from " + filePath);
            ApplySettings(); // ★ 读取完立刻应用（如修改音量）
            Log.Info("[Options] Settings loaded.");
            GameDebug.Log("[Options] Settings loaded.");
        }
        catch (System.Exception ex)
        {
            Log.Error("[Options] Failed to load settings: " + ex);
            GameDebug.LogError("[Options] Failed to load settings: " + ex);
            Settings = new GlobalSettings(); // 出错则重置为默认
            Scribe.FinalizeLoading();
        }
    }

    public void CancelChanges()
    {
        LoadOptions();
            Lonize.Events.Event.eventBus.Publish(new CancelSettingChange(new List<string>()));
    }

    public void ResetToDefaults()
    {
        Settings = new GlobalSettings();
        ApplySettings();
        SaveOptions();
    }

    // 应用设置的逻辑
    public void ApplySettings()
    {
        ApplyScreenSettings();
        ApplyAudioSettings();

        
        SaveOptions();
        // 例子：应用分辨率
        // Screen.fullScreen = Settings.FullScreen;
        // Screen.SetResolution((int)Settings.Resolution.x, (int)Settings.Resolution.y, Screen.fullScreenMode);
        // resolutionDropDown.ApplyResolution();
        // maxFrame.ApplyFrameRate();
        // 例子：应用音量
        // AudioListener.volume = Settings.MasterVolume;
        
        // 键位不需要“应用”，游戏逻辑直接访问 OptionsManager.Instance.Settings.KeyJump 即可
    }
    

    private void ApplyScreenSettings()
    {
        // Screen.fullScreen = Settings.FullScreen;
        var fullScreenMode = Settings.FullScreen switch
        {
            "Fullscreen" => FullScreenMode.ExclusiveFullScreen,
            "Windowed" => FullScreenMode.Windowed,
            "Borderless" => FullScreenMode.FullScreenWindow,
            _ => FullScreenMode.FullScreenWindow
        };
        // if(Screen.fullScreenMode != fullScreenMode)
        // {
        //     Screen.fullScreen = false;
        // }
        Application.targetFrameRate = Settings.MaxFrame == 0 ? -1 : Settings.MaxFrame;
        ApplyResolutionWithVerify((int)Settings.Resolution.x, (int)Settings.Resolution.y, fullScreenMode);
        // GameDebug.Log($"[Options] Applied FullScreenMode(Local): {fullScreenMode}");
        // GameDebug.Log($"[Options] Applied FullScreenMode(True): {Screen.fullScreenMode}");
            _ = float.TryParse(Settings.UIScale.Replace("%", ""), out float scaleValue) ? scaleValue / 100f : 1.0f;
            // 应用 UI 缩放
            _uiScale.ApplyUIScale(scaleValue/100f);

        // Screen.SetResolution((int)Settings.Resolution.x, (int)Settings.Resolution.y, Screen.fullScreenMode);
        // GameDebug.Log($"[Options] Applied Resolution: {Settings.Resolution.x}x{Settings.Resolution.y}, FullScreenMode: {Screen.fullScreenMode}, UI Scale: {uiScale}");
    }
    private void ApplyResolutionWithVerify(int width, int height, FullScreenMode mode)
    {
        StartCoroutine(ApplyWithVerifyCo(width, height, mode));
    }
    private static IEnumerator ApplyWithVerifyCo(int width, int height, FullScreenMode mode)
    {
        GameDebug.Log($"[Options] Apply Request: {width}x{height}, mode={mode}, isEditor={Application.isEditor}, platform={Application.platform}");
        Log.Info($"[Options] Apply Request: {width}x{height}, mode={mode}, isEditor={Application.isEditor}, platform={Application.platform}");
        Screen.SetResolution(width, height, mode);

        // 立刻读一次（通常是旧值）
        GameDebug.Log($"[Options] After SetResolution (Immediate): mode={Screen.fullScreenMode}, fullScreen={Screen.fullScreen}, size={Screen.width}x{Screen.height}");

        // 等一帧/两帧再读（更接近最终状态）
        yield return null;
        yield return null;

        GameDebug.Log($"[Options] After SetResolution (2 frames later): mode={Screen.fullScreenMode}, fullScreen={Screen.fullScreen}, size={Screen.width}x{Screen.height}");
        Log.Info($"[Options] Applied Resolution: {width}x{height}, FullScreenMode: {Screen.fullScreenMode}");
    }
    private void ApplyAudioSettings()
    {
        AudioListener.volume = Settings.MasterVolume;

    }
}
}