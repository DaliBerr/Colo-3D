using Lonize;
using UnityEngine;

namespace Kernel.Storage
{
    public class StorageDevTest : MonoBehaviour
    {
        private void Start()
        {
            DevControls inputActions = InputActionManager.Instance.Dev;

        }
        private void Update()
        {
            HandleStorageTest();
        }
        private void HandleStorageTest()
        {
            DevControls inputActions = InputActionManager.Instance.Dev;

            if (inputActions.Building.AddItem.WasPressedThisFrame())
            {
                var itemID = "iron_plate";
                var count = 5;
                var fromCell = Vector2Int.zero;
                int stored;
                long containerId;
                StorageSystem.Instance.TryStoreToBest(itemID, count, fromCell, out stored, out containerId);



            }

            if (inputActions.Building.RemoveItem.WasPressedThisFrame())
            {
                var containerId = 1L; // 假设容器ID为1
                var itemId = "iron_plate";
                var count = 3;
                int removed;
                StorageSystem.Instance.TryTake(containerId, itemId, count, out removed);
            }
        }
    }

}