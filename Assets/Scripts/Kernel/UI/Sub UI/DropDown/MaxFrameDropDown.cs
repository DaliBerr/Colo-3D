
using System.Collections.Generic;
using Kernel;
using Lonize.EventSystem;
using TMPro;
using UnityEngine;
using static Lonize.EventSystem.EventList;

public class MaxFrameDropDown : DropdownHolder
{
    public TMP_Dropdown _dropdown;
    public int prev = 60;
    public List<string> _options = new List<string>()
    {
        "60 FPS",
        "120 FPS",
        "144 FPS",
        "240 FPS",
        "360 FPS",
        "Unlimited"
    };

    public override TMP_Dropdown dropdown => _dropdown;
    public override List<string> Options { get => _options; set => _options = value; }
    protected override void Start()
    {
        base.Start();
        prev = OptionsManager.Instance.Settings.MaxFrame;
        int defaultIndex = Options.IndexOf(prev + " FPS");
        if (defaultIndex < 0) defaultIndex = 0;
        SetOptions(Options, defaultIndex);
        onValueChanged(index =>
        {
            
            OptionsManager.Instance.Settings.MaxFrame = GetOptionFloat(Options[index]);
            Lonize.EventSystem.EventManager.eventBus.Publish(new SettingChanged(true));
        });
    }

    private int GetOptionFloat(string fps)
    {
        if (fps == "Unlimited")
            return 0;
        return int.Parse(fps.Replace(" FPS", ""));
    }
}