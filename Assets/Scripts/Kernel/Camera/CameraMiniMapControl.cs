using System;
using UnityEngine;
using UnityEngine.EventSystems;
using Lonize.Events;
using Lonize.UI;
using Unity.VisualScripting;
using Kernel.GameState;
using Lonize.Logging;

namespace Kernel
{
    public class MiniMapControl : CameraControl
    {
        // /// <summary>
        // /// 小地图使用的相机组件
        // /// </summary>
        // public override Camera CameraComponent { get; set; }

        // /// <summary>
        // /// 启用时订阅地图准备就绪事件
        // /// </summary>
        // /// <param name="无">无</param>
        // /// <returns>无</returns>
        // private void OnEnable()
        // {
        //     Events.eventBus.Subscribe<MapReady>(OnMapReady);
        // }

        // /// <summary>
        // /// 禁用时取消订阅地图准备就绪事件
        // /// </summary>
        // /// <param name="无">无</param>
        // /// <returns>无</returns>
        // private void OnDisable()
        // {
        //     Events.eventBus.Unsubscribe<MapReady>(OnMapReady);
        // }

        // /// <summary>
        // /// 初始化：获取子物体中的相机组件
        // /// </summary>
        // /// <param name="无">无</param>
        // /// <returns>无</returns>
        // private void Start()
        // {
        //     CameraComponent = GetComponentInChildren<Camera>();
        // }

        // /// <summary>
        // /// 每帧更新：仅在允许时处理小地图平移与缩放
        // /// </summary>
        // /// <param name="无">无</param>
        // /// <returns>无</returns>
        // private void Update()
        // {
        //     if (!CanMoveCamera())
        //         return;
        //     // GameDebug.LogWarning("[MiniMap] Handling mini-map camera control.");
        //     HandlePan();
        //     HandleZoom();
        // }

        // /// <summary>
        // /// 地图就绪事件回调：将小地图相机移动到地图中心
        // /// </summary>
        // /// <param name="evt">地图准备就绪事件，携带地图中心位置</param>
        // /// <returns>无</returns>
        // private void OnMapReady(MapReady evt)
        // {
        //     if (evt.value)
        //     {
        //         var mapCenter = evt.mapCenterPosition;
        //         Vector3 newPos = new Vector3(mapCenter.x, mapCenter.y, transform.position.z);
        //         transform.position = newPos;
        //     }
        // }

        // /// <summary>
        // /// 判断当前是否允许控制小地图相机
        // /// </summary>
        // /// <param name="无">无</param>
        // /// <returns>当且仅当鼠标位于小地图 UI 区域上方时返回 true</returns>
        // private bool IsPointerCanMoveMiniMapCamera()
        // {
        //     // 只要鼠标在小地图区域上，就允许操作
        //     return MiniMapInputArea.IsPointerOverMiniMap;
        // }

        // private bool isGamePaused()
        // {
        //     return StatusController.HasStatus(StatusList.InPauseMenuStatus);
        // }

        // private bool CanMoveCamera()
        // {
        //     var Res = IsPointerCanMoveMiniMapCamera();
        //     var paused = isGamePaused();
        //     // if (!Res)
        //     return Res && !paused;
        // }
    }
}
