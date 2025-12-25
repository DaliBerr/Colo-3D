
using System.Collections.Generic;
using Kernel;
using Lonize.Events;
using TMPro;
using UnityEngine;

public class DisplayModeDropDown : DropdownHolder
{
    public TMP_Dropdown _dropdown;
    public List<string> _options = new List<string>()
    {
        "Fullscreen",
        "Windowed",
        "Borderless"
    };

    public string prev = "Fullscreen";

    public override TMP_Dropdown dropdown => _dropdown;
    public override List<string> Options { get => _options; set => _options = value; }

    protected override void Start()
    {
        base.Start();
        prev = OptionsManager.Instance.Settings.FullScreen;
        int defaultIndex = Options.IndexOf(prev);
        if (defaultIndex < 0) defaultIndex = 0;
        SetOptions(Options, defaultIndex);
        onValueChanged(index =>
        {
            OptionsManager.Instance.Settings.FullScreen = Options[index];
            Events.eventBus.Publish(new SettingChanged(true));
        });
        //TODO:添加确认弹窗,并计时回退
    }
}