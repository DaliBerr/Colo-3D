
using System;
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

        void Start()
        {
            UIManager.Instance.PushScreen<MainUI>();
        }

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
        private async Task InitAll()
        {
            await InitItems();
            await InitBuildings();
        }
    }
}