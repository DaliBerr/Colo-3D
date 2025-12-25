
using Kernel;
using Lonize.Events;
using UnityEngine.UI;

public class MasterVolumeSlider : SliderHolder
{
    public override Slider slider { get; }
    public  float preVal { get; set; }

    protected override void Start()
    {
        preVal = OptionsManager.Instance.Settings.MasterVolume;
        if(preVal<0f || preVal>1f) preVal = 0.8f;
        base.SetValue(preVal, false);
        base.Start();
        onValueChanged((value) =>
        {
            OptionsManager.Instance.Settings.MasterVolume = value;
            Events.eventBus.Publish(new SettingChanged(true));
        });
    }
}