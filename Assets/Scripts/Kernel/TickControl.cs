using UnityEngine;
using Lonize.Tick;
using Lonize.Events;
using Lonize.Logging;
using Lonize;
namespace Kernel
{
    public  class TickControl: MonoBehaviour
    {
        public static GameSpeed currentSpeed = GameSpeed.Normal;
        [SerializeField] private TickDriver tickDriver;
        private SpeedControls speedControls;

        void Awake()
        {
            // if (tickDriver == null)
            //     tickDriver = GetComponentInChildren<TickDriver>() ?? Object.FindFirstObjectByType<TickDriver>();
            
            if (tickDriver == null)
            {
                GameDebug.LogError("TickControl: TickDriver not found.");
                Log.Error("TickControl: TickDriver not found.");
                enabled = false;
                return;
            }
            speedControls = InputActionManager.Instance.Speed;
            // speedControls.Speed.Enable();

        }
        // private TickController controller;
        private void Start()
        {  
            // 统一桥接：速度变化 → EventBus 事件
            tickDriver.tickManager.TimeCtrl.OnSpeedChanged += HandleSpeedChanged;

            // 刚进来先广播一次当前速度（相当于 sticky）
            var ctrl = tickDriver.tickManager.TimeCtrl;
            HandleSpeedChanged(ctrl.SpeedPreset, ctrl.SpeedMultiplier);
        }
            private void OnDisable()
            {
                if (tickDriver != null)
                    tickDriver.tickManager.TimeCtrl.OnSpeedChanged -= HandleSpeedChanged;
            }
        private void HandleSpeedChanged(GameSpeed preset, float mult)
        {
            // 通过 EventBus 广播（你的全局总线）
            Events.eventBus.Publish(new SpeedChange(mult,preset));
            // 也可以加日志
            Log.Info($"Speed -> {preset} ({mult}x)");
        }
        void Update()
        {
            if (tickDriver != null)
            {
                // tickDriver.UpdateTickManager(Time.unscaledDeltaTime);
                var controller = tickDriver.tickManager.TimeCtrl;
                if (speedControls.Speed.Normal.IsPressed()) // +
                {
                    controller.SetPreset(GameSpeed.Normal);
                    currentSpeed = GameSpeed.Normal;
                    // Events.eventBus.Publish(new SpeedChange(1f,currentSpeed));

                }
                else if (speedControls.Speed.Fast.IsPressed()) // -
                {
                    controller.SetPreset(GameSpeed.Fast);
                    currentSpeed = GameSpeed.Fast;
                    // Events.eventBus.Publish(new SpeedChange(2f,currentSpeed));
                }
                else if (speedControls.Speed.SuperFast.IsPressed()) // ||
                {
                    controller.SetPreset(GameSpeed.SuperFast);
                    currentSpeed = GameSpeed.SuperFast;
                    // Events.eventBus.Publish(new SpeedChange(3f,currentSpeed));
                }
                else if (speedControls.Speed.Pause.IsPressed())
                {
                    if (currentSpeed != GameSpeed.Paused)
                    {
                        controller.SetPreset(GameSpeed.Paused);
                        currentSpeed = GameSpeed.Paused;
                        // Events.eventBus.Publish(new SpeedChange(0f,currentSpeed));
                        Log.Info("Game Paused");
                    }
                    else
                    {
                        controller.SetPreset(GameSpeed.Normal);
                        currentSpeed = GameSpeed.Normal;
                        // Events.eventBus.Publish(new SpeedChange(1f,currentSpeed));
                        Log.Info("Game Resumed");
                    }
                }
                else if (speedControls.Speed.StepOneTick.IsPressed())
                {
                    controller.StepOneTick();
                }
            }
        }
    }
}