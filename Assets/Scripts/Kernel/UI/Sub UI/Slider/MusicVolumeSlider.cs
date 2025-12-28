using Kernel;
using Lonize.Events;
using UnityEngine.UI;
using static Lonize.Events.EventList;

public class MusicVolumeSlider : SliderHolder
{
    public override Slider slider { get; }
    public  float preVal { get; set; }

    protected override void Start()
    {
        
        preVal = OptionsManager.Instance.Settings.MusicVolume;
        // SetValueWithoutNotify(preVal);
        float defaultVal = preVal;
        if(defaultVal < 0f || defaultVal > 1f) defaultVal = 0.8f;
        
        base.Start();
        onValueChanged(value =>
        {
            preVal = value;
            OptionsManager.Instance.Settings.MusicVolume = preVal;
            Event.eventBus.Publish(new SettingChanged(true));
        });
    }
}