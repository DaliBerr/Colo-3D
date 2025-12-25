using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 小地图初始化：在运行时创建RenderTexture并绑定到相机和UI。
/// </summary>
public class MiniMapInitializer : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private RawImage _miniMapImage;

    [Header("相机查找")]
    [SerializeField] private string _miniMapCameraTag = "MiniMap";

    [Header("RenderTexture参数")]
    [SerializeField] private int _width = 256;
    [SerializeField] private int _height = 256;
    [SerializeField] private int _depth = 16;

    private Camera _miniMapCamera;
    private RenderTexture _rt;

    /// <summary>
    /// 初始化小地图渲染目标并进行绑定。
    /// </summary>
    /// <param name="void">无参数。</param>
    /// <returns>无返回值。</returns>
    private void Awake()
    {
        // 1. 找到场景里的小地图相机（给那台相机加上对应的Tag即可）
        var camObj = GameObject.FindGameObjectWithTag(_miniMapCameraTag);
        if (camObj == null)
        {
            Debug.LogError("[MiniMap] 找不到带 MiniMapCamera 标签的相机！");
            return;
        }

        _miniMapCamera = camObj.GetComponentInChildren<Camera>();
        if (_miniMapCamera == null)
        {
            Debug.LogError("[MiniMap] MiniMapCamera 对象上没有 Camera 组件！");
            return;
        }

        // 2. 创建运行时用的 RenderTexture（只有这一份）
        _rt = new RenderTexture(_width, _height, _depth, RenderTextureFormat.ARGB32)
        {
            name = "MiniMapRT_Runtime"
        };

        // 3. 同时绑定给相机和UI
        _miniMapCamera.targetTexture = _rt;
        if (_miniMapImage != null)
        {
            _miniMapImage.texture = _rt;
        }

        Debug.Log($"[MiniMap] RT 实例ID = {_rt.GetInstanceID()}  已绑定到相机和UI");
    }

    /// <summary>
    /// 销毁时清理RenderTexture资源。
    /// </summary>
    /// <param name="void">无参数。</param>
    /// <returns>无返回值。</returns>
    private void OnDestroy()
    {
        if (_rt != null)
        {
            _miniMapCamera.targetTexture = null;
            _miniMapImage.texture = null;
            _rt.Release();
            Destroy(_rt);
            _rt = null;
        }
    }
}
