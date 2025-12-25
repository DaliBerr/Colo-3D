using UnityEngine;
using Lonize.Scribe;
using System.Collections.Generic;

namespace Kernel
{
public class GlobalSettings : IExposable, ILoadReferenceable
{
    // 1. 定义你要存的设置
    public float MasterVolume = 1.0f;
    public float MusicVolume = 0.8f;
    public float SoundEffectVolume = 0.8f;


    // public bool FullScreen = true;
    public string FullScreen = "Fullscreen";
    public string UIScale = "100%";

    public Vector2Int Resolution = new Vector2Int(1920, 1080);
    public int MaxFrame = 60;


    // 2. 键位设置 (推荐用 KeyCode)
    // public KeyCode KeyJump = KeyCode.Space;
    // public KeyCode KeyInteract = KeyCode.F;
    // public KeyCode KeyAttack = KeyCode.Mouse0;

    private Dictionary<string, KeyCode> ControlCommand = InputConfiguration.ControlCommand;

    // 3. 实现 ExposeData
    public void ExposeData()
    {
        int ResolutionWidth = Resolution.x;
        int ResolutionHeight = Resolution.y;
        Scribe_Values.Look("vol_master", ref MasterVolume, 1.0f);
        Scribe_Values.Look("vol_music", ref MusicVolume, 0.8f);
        Scribe_Values.Look("vol_sfx", ref SoundEffectVolume, 0.8f);

        Scribe_Values.Look("fullscreen", ref FullScreen, "Fullscreen");
        Scribe_Values.Look("ui_scale", ref UIScale, "100%");

        Scribe_Values.Look("resolution_width", ref ResolutionWidth, 1920);
        Scribe_Values.Look("resolution_height", ref ResolutionHeight, 1080);
        Scribe_Values.Look("max_frame", ref MaxFrame, 60);

        
        Scribe_Values.LookDictStrEnumInt32("control_command", ref ControlCommand, ControlCommand);

        // 枚举类型的保存（KeyCode 本质是枚举）
        // Scribe_Values.LookEnum("key_jump", ref KeyJump, KeyCode.Space);
        // Scribe_Values.LookEnum("key_interact", ref KeyInteract, KeyCode.F);
        // Scribe_Values.LookEnum("key_attack", ref KeyAttack, KeyCode.Mouse0);
        Resolution = new Vector2Int(ResolutionWidth, ResolutionHeight);
    }
    
    // 如果不需要引用功能，这个可以返回 null
    public string GetSaveId() => "GlobalSettings";
}
}