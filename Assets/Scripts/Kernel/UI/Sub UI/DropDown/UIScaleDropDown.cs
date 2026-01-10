using System.Collections.Generic;
using Kernel;
using Lonize.EventSystem;
using TMPro;
using UnityEngine.UI;
using static Lonize.EventSystem.EventList;

public class UIScaleDropDown: DropdownHolder
{
    public TMP_Dropdown _dropdown;
    public List<string> _options = new List<string>()
    {
        "100%",
        "125%",
        "150%",
        "175%",
        "200%"
    };

    public string prev = "100%";

    public override TMP_Dropdown dropdown => _dropdown;
    public override List<string> Options { get => _options; set => _options = value; }

    protected override void Start()
    {
        base.Start();
        prev = OptionsManager.Instance.Settings.UIScale;
        int defaultIndex = Options.IndexOf(prev);
        if (defaultIndex < 0) defaultIndex = 0;
        SetOptions(Options, defaultIndex);
        onValueChanged(index =>
        {
            OptionsManager.Instance.Settings.UIScale = Options[index];
            EventManager.eventBus.Publish(new SettingChanged(true));
        });
        //TODO:添加确认弹窗,并计时回退
    }
}
    