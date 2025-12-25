using Kernel;
using Lonize;
using Lonize.Logging;
using UnityEngine;

namespace Colo
{
    public class GameSaveController : MonoBehaviour
    {
        public static GameSaveController Instance;

        private SaveControls saveControls;
        public void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            saveControls = new SaveControls();
            saveControls.SaveLoad.Enable();
        }
        public void Update()
        {
            if(saveControls.SaveLoad.Save.IsPressed())
            {
                SaveGame();
            }
            if(saveControls.SaveLoad.Load.IsPressed())
            {
                LoadGame();
            }
        }

        // 在这里添加游戏存档管理的相关代码
        public void SaveGame()
        {
            var saveMgr = ScribeSaveManager.Instance;
        
            saveMgr.Data.Items.Clear();

            // Debug.Log($"Collected {allBuildings.Length} buildings for saving.");
            // 添加状态数据保存项
            saveMgr.AddItem(new Kernel.Building.SaveAllBuildings());
            saveMgr.AddItem(new Kernel.GameState.StatusSaveData());
            // Debug.Log("saveItem : " + BuildingIdGenerator._saveItem);

            saveMgr.AddItem(Kernel.Building.BuildingIDManager._saveItem);
            // 4. 落盘
            saveMgr.Save();
            GameDebug.Log("游戏已保存！");

        }

        
    public void LoadGame()
    {
        var saveMgr = ScribeSaveManager.Instance;

        // 1. 读文件
        if (!saveMgr.Load())
        {
            GameDebug.LogWarning("没有存档文件！");
            Log.Warn("没有存档文件！");
            return;
        }
        //这里什么也没做，只是为了触发 ScribeSaveManager 的 Load 逻辑
        //加载部分都在各个 SaveItem 的 ExposeData中的Loading 部分处理
        GameDebug.Log("游戏读取完毕！");
    }
    }
}