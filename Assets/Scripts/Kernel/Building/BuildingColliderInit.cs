using UnityEngine;
using Kernel.Building;
using Kernel.World;
using Lonize.Logging;

public class BuildingColliderInit : MonoBehaviour
{
    [SerializeField] private BuildingRuntimeHost buildingRuntimeHost;
    [SerializeField] private WorldGrid worldGrid;

    [Header("Collider")]
    [Min(0.01f)] public float colliderHeight = 3f;
    public bool isTrigger = true;
    public float centerYOffset = 0f;

    private bool _initialized;

    /// <summary>
    /// summary: 外部显式初始化入口（Factory/Pool 取出后可调用一次）。
    /// param: 无
    /// return: 无
    /// </summary>
    public void Initialize()
    {
        if (_initialized) return;

        if (buildingRuntimeHost == null)
            buildingRuntimeHost = GetComponent<BuildingRuntimeHost>();

        if (worldGrid == null)
            worldGrid = WorldGrid.Instance;

        if (buildingRuntimeHost == null || buildingRuntimeHost.Runtime == null || buildingRuntimeHost.Runtime.Def == null)
        {
            Log.Warn("[BuildingColliderInit] buildingRuntimeHost/Runtime/Def 为空，跳过初始化。");
            return;
        }

        var def = buildingRuntimeHost.Runtime.Def;
        float cs = worldGrid != null ? worldGrid.cellSize : 1f;

        Vector3 size = new Vector3(def.Width * cs, colliderHeight, def.Height * cs);
        Vector3 center = new Vector3(0f, colliderHeight * 0.5f + centerYOffset, 0f);

        // 确保是 3D Collider
        var col2d = GetComponent<BoxCollider2D>();
        if (col2d != null) Destroy(col2d);

        var collider = gameObject.GetComponent<BoxCollider>();
        if (collider == null) collider = gameObject.AddComponent<BoxCollider>();

        collider.size = size;
        collider.center = center;
        collider.isTrigger = isTrigger;

        Log.Info($"[BuildingColliderInit] 3D BoxCollider size={size}, center={center}, trigger={isTrigger}");
        _initialized = true;
    }
}
