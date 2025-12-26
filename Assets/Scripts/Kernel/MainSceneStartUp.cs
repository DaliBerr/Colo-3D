
using System;
using System.Collections;
using System.Threading.Tasks;
using Kernel.Item;
using Kernel.UI;
using Lonize.Logging;
using Lonize.UI;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Kernel
{
    public sealed class MainSceneStartUp : MonoBehaviour
    {
        public static MainSceneStartUp Instance { get; private set; }

        private static readonly bool useDontDestroyOnLoad = false;
        async void Awake()
        {
            // Addressables.InitializeAsync().Task;
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (useDontDestroyOnLoad) DontDestroyOnLoad(gameObject);
            // await InitItems();
            await InitAll();
            // UIManager.Instance.PushScreen<UI.MainUI>();
        }


        // void Start()
        // {
        //     StartCoroutine(LoadScreen());
        // }
        // private IEnumerator LoadScreen()
        // {
        //     yield return UIManager.Instance.PopScreenNoShowAndWait();
        //     UIManager.Instance.PushScreen<UI.MainUI>();
        // }
        private async Task InitItems()
        {
            await ItemDatabase.LoadAllAsync();

            //test
            // var inst = ItemFactory.CreateData("iron_sword", 5);
            // Log.Info($"Created Item Instance: Def={inst.Def.Id}, Stack={inst.Stack}");

        }
        private async Task InitBuildings()
        {
            await Building.BuildingDatabase.LoadAllAsync();
            //test
            // var hasDef = Building.BuildingDatabase.TryGet("generator_small", out var def);
            // if (hasDef)
            // {
            //     GameDebug.Log($"Building Def loaded: ID={def.Id}, Name={def.Name}");
            // }
        }
        private async Task InitInput()
        {
            // 确保 InputActionManager 已初始化
            if (!InputActionManager.Instance.IsInitialized)
            {
                // Log.Info("Initializing InputActionManager...");
                await Task.Run(() =>
                {
                    // 等待直到 InputActionManager 初始化完成
                    while (!InputActionManager.Instance.IsInitialized)
                    {
                        Task.Delay(1).Wait();
                    }
                });
                // Log.Info("InputActionManager initialized.");
            }
        }
        private async Task InitAll()
        {
            await InitItems();
            await InitBuildings();
            await InitInput();
        }
    }
}