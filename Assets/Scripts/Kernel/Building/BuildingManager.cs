using UnityEngine;

namespace Kernel.Building
{
    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }

        public void getBuildingById(long id, out BuildingRuntime building)
        {
            building = null;
            var hosts = FindObjectsByType<BuildingRuntimeHost>(FindObjectsSortMode.None);
            foreach (var host in hosts)
            {
                if (host.Runtime != null && host.Runtime.BuildingID == id)
                {
                    building = host.Runtime;
                    return;
                }
            }
        }
    }
}