using System.Collections;
using System.Collections.Generic;
using Kernel;
using Kernel.Building;
using Kernel.GameState;
using Lonize;
using Lonize.Logging;
using Lonize.UI;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Kernel.UI
{
    [UIPrefab("Prefabs/UI/Factory UI")]
    public sealed class FactoryUI : UIScreen
    {

        [SerializeField] private Button closeButton;
        [SerializeField] private List<Button> itemButtons = new List<Button>();
        [SerializeField] private GameObject itemSelectionPanel;
        
        //Obsolete
        [SerializeField] private Button AddInteriorBuildingButton;

        private int selectedGridIndex = 0;

        public override Status currentStatus { get; } = StatusList.PlayingStatus;

        protected override void OnInit()
        {
            closeButton.onClick.AddListener(TryCloseUI);
            for(int i = 0; i < itemButtons.Count; i++)
            {
                int index = i; // 捕获当前索引
                itemButtons[i].onClick.AddListener(() => OnItemButtonClicked(index));
            }
            AddInteriorBuildingButton.onClick.AddListener(() =>
            {

                if (TryAddInteriorBuilding("factory_interior_default", selectedGridIndex))
                {
                    TryShowInteriorBuilding(selectedGridIndex);
                    BuildingFactory.InitializeInternalBehaviours(
                        BuildingFactoryController.Instance.GetCurrentFactoryRuntime()
                        .FactoryInterior.Children[selectedGridIndex]
                    );
                    GameDebug.Log("Successfully added interior building.");
                }
                
                GameDebug.Log("Add Interior Building Button Clicked - Obsolete");
            });
        }
        private void OnEnable()
        {
            var runtime = BuildingFactoryController.Instance.GetCurrentFactoryRuntime();
            if (runtime == null)
            {
                GameDebug.LogError("当前没有选中任何工厂，无法打开工厂界面！");
                UIManager.Instance.CloseTopModal();
                return;
            }
            // 初始化界面内容，比如显示工厂内部建筑等
            foreach (var child in runtime.FactoryInterior.Children)
            {
                GetIndexByCellPosition(child.CellPosition);
                TryShowInteriorBuilding(GetIndexByCellPosition(child.CellPosition));
            }
        }
        private void OnDestroy()
        {
            closeButton.onClick.RemoveAllListeners();
            for (int i = 0; i < itemButtons.Count; i++)
            {
                itemButtons[i].onClick.RemoveAllListeners();
            }
        }

        private void TryCloseUI()
        {
            UIManager.Instance.CloseTopModal();
        }

        private void OnItemButtonClicked(int index)
        {
            // GameDebug.Log($"Item button {index} clicked.");
            selectedGridIndex = index;
            itemSelectionPanel.SetActive(true);
            // 在这里添加处理按钮点击的逻辑
        }

        private void TryShowInteriorBuilding(int index)
        {
            itemButtons[index].TryGetComponent<Image>(out var img);
            if (img != null)
            {
                GameDebug.Log($"Showing interior building at index {index} with image {img.sprite.name}");
                img.color = Color.green; // 示例操作：将按钮颜色改为绿色
            }
        }

        private bool TryAddInteriorBuilding(string defID, int index)
        {
            var factoryCtrl = BuildingFactoryController.Instance;
            if (factoryCtrl == null) return false;

            var currentFactoryRuntime = factoryCtrl.GetCurrentFactoryRuntime();
            if (currentFactoryRuntime == null)
            {
                GameDebug.LogWarning("当前没有选中任何工厂，无法添加内部建筑哦！");
                return false;
            }
            
            Vector2Int position = GetCellPositionByIndex(index);
            // 2. 检查位置是否被占用了 (简单防呆)
            // 遍历当前的所有子建筑，看看有没有人在这个格子上
            if (currentFactoryRuntime.FactoryInterior != null)
            {
                foreach (var child in currentFactoryRuntime.FactoryInterior.Children)
                {
                    if (child.CellPosition == position)
                    {
                        GameDebug.LogError($"位置 {position} 已经有东西啦！添加失败。");
                        return false;
                    }
                }
            }

            var newChild = BuildingFactory.CreateInternalRuntime(
            currentFactoryRuntime.BuildingID, // 父建筑 ID
                defID,                            // 内部建筑 Def ID
                position                          // 放置位置
            );

            if (newChild == null)
            {
                GameDebug.LogError($"创建失败，可能是 Def ID {defID} 不存在或者类型不对。");
                return false;
            }

            currentFactoryRuntime.EnsureFactoryInterior().Children.Add(newChild);
            GameDebug.Log($"✨ 成功向工厂 {currentFactoryRuntime.BuildingID} 添加了内部建筑 {defID  } @ {position}");
            return true;
        }

        private static Vector2Int GetCellPositionByIndex(int index)
        {
            // 假设每行有4个格子
            int columns = 5;
            int x = index % columns;
            int y = index / columns;
            return new Vector2Int(x, y);
        }
        private static int GetIndexByCellPosition(Vector2Int position)
        {
            int columns = 5;
            return position.y * columns + position.x;
        }
    }
}