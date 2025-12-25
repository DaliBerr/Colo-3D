using Lonize;
using Lonize.Logging;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Kernel
{
    public class CameraControl: MonoBehaviour
    {
        [Header("Move")]
        [SerializeField] private float moveSpeed = 12f;

        [Header("Rotate")]
        [SerializeField] private float rotateSpeed = 180f;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private bool requireGroundHitToRotate = true;
        [Header("Rotate - Pitch")]
        [SerializeField] private bool enablePitch = true;

        [Tooltip("Pitch 旋转速度（度/秒的倍率因子）")]
        [SerializeField] private float pitchSpeed = 160f;

        [Tooltip("俯仰角下限（度）。建议 25~45")]
        [SerializeField] private float minPitchDeg = 30f;

        [Tooltip("俯仰角上限（度）。建议 70~85")]
        [SerializeField] private float maxPitchDeg = 80f;

        [Tooltip("是否反转 Y 方向（拖动向上是抬头/低头）")]
        [SerializeField] private bool invertPitch = false;

        private bool _pitchInited;
        private float _pitchSignedDeg;
        private float _pitchSign = 1f;
        private float _camLocalYawDeg;
        private float _camLocalRollDeg;

        [Header("Zoom (Orthographic)")]
        [SerializeField] private float zoomSpeedOrtho = 2f;
        [SerializeField] private float minOrthoSize = 5f;
        [SerializeField] private float maxOrthoSize = 40f;

        [Header("Zoom (Perspective)")]
        [SerializeField] private float zoomSpeedFov = 25f;
        [SerializeField] private float minFov = 20f;
        [SerializeField] private float maxFov = 70f;


        [Header("Ground Follow（贴地高度）")]
        [SerializeField] private bool enableGroundFollow = true;

        [Tooltip("相机距离地面的目标高度（单位：米）")]
        [SerializeField] private float cameraHeightAboveGround = 25f;

        [Tooltip("跟随平滑度（越大越紧跟，建议 8~20）")]
        [SerializeField] private float followSharpness = 12f;

        [Tooltip("向下射线起点额外抬高，避免相机在地形内部时射线失败")]
        [SerializeField] private float rayStartOffset = 50f;

        [Tooltip("向下射线最大距离")]
        [SerializeField] private float rayMaxDistance = 5000f;

        [Tooltip("true=以 targetCamera 的位置为采样点；false=以当前脚本物体(transform)为采样点")]
        [SerializeField] private bool sampleUnderCamera = true;

        [Tooltip("每秒最大上升/下降速度（0=不限制）")]
        [SerializeField] private float maxVerticalSpeed = 200f;

        
        public virtual Camera targetCamera { get; set; }

        private CameraControls controls;

        private Vector2 moveInput;
        private float zoomInput;
        private Vector2 pointerPos;
        private Vector2 pointerDelta;

        private bool isRotating;

        private void Awake()
        {
            HandleAwake();
            controls = new CameraControls();
            controls.Camera.Enable();
            
        }
        public virtual void HandleAwake()
        {
            
        }

        private void OnDestroy()
        {
            controls.Disable();
        }

        /// <summary>
        /// 每帧读取输入并驱动移动/旋转/缩放
        /// </summary>
        /// <param name="无"></param>
        /// <returns>无</returns>
        private void Update()
        {
            if (targetCamera == null) return;
            HandleUpdate();
            ReadInputs();
            HandleMove(Time.deltaTime);
            HandleRotate(Time.deltaTime);
            HandleZoom(Time.deltaTime);
            HandleGroundFollow(Time.deltaTime);
        }

        public virtual void HandleUpdate()
        {
            
        }
        /// <summary>
        /// 从指定位置向下射线检测地面高度（groundMask）。
        /// </summary>
        /// <param name="samplePos">采样点（世界坐标）。</param>
        /// <param name="groundY">输出：命中的地面Y。</param>
        /// <returns>是否命中地面。</returns>
        private bool TryGetGroundY(Vector3 samplePos, out float groundY)
        {
            Vector3 origin = samplePos + Vector3.up * Mathf.Max(0f, rayStartOffset);
            float dist = Mathf.Max(0f, rayStartOffset) + Mathf.Max(0.1f, rayMaxDistance);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, dist, groundMask, QueryTriggerInteraction.Ignore))
            {
                groundY = hit.point.y;
                return true;
            }

            groundY = 0f;
            return false;
        }

        /// <summary>
        /// 保持相机与地面之间的固定高度：地面起伏时相机同步上升/下降（带平滑）。
        /// </summary>
        /// <param name="dt">帧时间（秒）。</param>
        /// <returns>无。</returns>
        private void HandleGroundFollow(float dt)
        {
            if (!enableGroundFollow) return;
            if (targetCamera == null) return;

            // 采样点：默认用相机位置（更符合“相机距地面固定高度”的直觉）
            Vector3 samplePos = sampleUnderCamera ? targetCamera.transform.position : transform.position;

            if (!TryGetGroundY(samplePos, out float groundY))
                return;

            // 关键：如果脚本挂在 Rig 上、Camera 是子物体，要把 Rig 的 Y 调整到让 Camera 达到目标高度
            float camOffsetY = targetCamera.transform.position.y - transform.position.y;
            float targetRigY = groundY + cameraHeightAboveGround - camOffsetY;

            Vector3 pos = transform.position;

            // 指数平滑：t = 1 - exp(-sharpness*dt)
            float t = 1f - Mathf.Exp(-Mathf.Max(0.01f, followSharpness) * dt);
            float desiredY = Mathf.Lerp(pos.y, targetRigY, t);

            // 可选：限制最大垂直速度，避免跨巨大高度差时瞬间跳
            if (maxVerticalSpeed > 0f)
            {
                float maxDelta = maxVerticalSpeed * dt;
                desiredY = Mathf.MoveTowards(pos.y, desiredY, maxDelta);
            }

            pos.y = desiredY;
            transform.position = pos;
        }


        /// <summary>
        /// 读取本帧输入值（新 Input System）
        /// </summary>
        /// <param name="无"></param>
        /// <returns>无</returns>
        private void ReadInputs()
        {
            moveInput = controls.Camera.Move.ReadValue<Vector2>();
            zoomInput = controls.Camera.Zoom.ReadValue<float>();
            pointerPos = controls.Camera.PointerPosition.ReadValue<Vector2>();
            pointerDelta = controls.Camera.PointerDelta.ReadValue<Vector2>();
        }

        /// <summary>
        /// 处理平面移动（沿相机朝向的 XZ 方向移动）
        /// </summary>
        /// <param name="dt">帧时间（秒）</param>
        /// <returns>无</returns>
        private void HandleMove(float dt)
        {
            if (moveInput.sqrMagnitude < 0.0001f) return;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward = forward.normalized;

            Vector3 right = transform.right;
            right.y = 0f;
            right = right.normalized;

            Vector3 delta = (right * moveInput.x + forward * moveInput.y) * (moveSpeed * dt);
            transform.position += delta;
        }
        /// <summary>
        /// 处理“点地面按住拖动旋转”
        /// </summary>
        /// <param name="dt">帧时间（秒）</param>
        /// <returns>无</returns>
        private void HandleRotate(float dt)
        {
            if (IsPointerOverUI())
            {
                isRotating = false;
                return;
            }

            bool rotateHeld = controls.Camera.RotateHold.IsPressed();

            if (!isRotating && rotateHeld)
            {
                // 按下那一刻：判断是否点到地面
                if (!requireGroundHitToRotate || RaycastGround(pointerPos))
                {
                    isRotating = true;
                }
            }
            else if (isRotating && !rotateHeld)
            {
                isRotating = false;
            }

            if (!isRotating) return;

            EnsurePitchInitialized();

            // yaw：保持你原来的行为
            float yawDelta = pointerDelta.x * rotateSpeed * dt / 100f;
            transform.Rotate(0f, yawDelta, 0f, Space.World);

            // pitch：可选
            if (enablePitch)
            {
                float pitchDelta = pointerDelta.y * pitchSpeed * dt / 100f;
                if (!invertPitch) pitchDelta = -pitchDelta;

                _pitchSignedDeg += pitchDelta;
                _pitchSignedDeg = ClampPitchSigned(_pitchSignedDeg);

                ApplyPitchToCamera();
            }
        }

        /// <summary>
        /// 处理缩放：正交改 size，透视改 FOV（保持高度固定）
        /// </summary>
        /// <param name="dt">帧时间（秒）</param>
        /// <returns>无</returns>
        private void HandleZoom(float dt)
        {
            if (Mathf.Abs(zoomInput) < 0.0001f) return;

            if (targetCamera.orthographic)
            {
                float size = targetCamera.orthographicSize;
                size -= zoomInput * zoomSpeedOrtho * dt;
                targetCamera.orthographicSize = Mathf.Clamp(size, minOrthoSize, maxOrthoSize);
            }
            else
            {
                float fov = targetCamera.fieldOfView;
                fov -= zoomInput * zoomSpeedFov * dt;
                targetCamera.fieldOfView = Mathf.Clamp(fov, minFov, maxFov);
            }
        }

        /// <summary>
        /// 射线检测鼠标是否点到地面（groundMask）
        /// </summary>
        /// <param name="screenPos">屏幕坐标</param>
        /// <returns>是否命中地面</returns>
        private bool RaycastGround(Vector2 screenPos)
        {
            Ray ray = targetCamera.ScreenPointToRay(screenPos);
            return Physics.Raycast(ray, out _, 10000f, groundMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        /// 判断指针是否在 UI 上（避免拖 UI 也触发旋转）
        /// </summary>
        /// <param name="无"></param>
        /// <returns>是否在 UI 上</returns>
        private bool IsPointerOverUI()
        {
            if (EventSystem.current == null) return false;
            return EventSystem.current.IsPointerOverGameObject();
        }

                /// <summary>
        /// 初始化 Pitch：读取相机当前本地欧拉角，确定 pitch 的正负方向与保留的 yaw/roll。
        /// </summary>
        /// <returns>无。</returns>
        private void EnsurePitchInitialized()
        {
            if (_pitchInited) return;
            if (targetCamera == null) return;

            var e = targetCamera.transform.localEulerAngles;
            _pitchSignedDeg = NormalizeSignedAngle(e.x);
            _camLocalYawDeg = e.y;
            _camLocalRollDeg = e.z;

            // 确定“向下看”的符号（有些层级会让 pitch 变成负数）
            _pitchSign = (_pitchSignedDeg < 0f) ? -1f : 1f;
            _pitchSignedDeg = ClampPitchSigned(_pitchSignedDeg);

            _pitchInited = true;
        }

        /// <summary>
        /// 将 pitch 应用到 targetCamera 的本地旋转（保留原本的本地 yaw/roll）。
        /// </summary>
        /// <returns>无。</returns>
        private void ApplyPitchToCamera()
        {
            if (targetCamera == null) return;
            targetCamera.transform.localRotation = Quaternion.Euler(_pitchSignedDeg, _camLocalYawDeg, _camLocalRollDeg);
        }

        /// <summary>
        /// 将角度归一化到 [-180, 180]，避免 0..360 引发的跳变。
        /// </summary>
        /// <param name="deg">输入角度（度）。</param>
        /// <returns>归一化后的角度（度）。</returns>
        private static float NormalizeSignedAngle(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            if (deg < -180f) deg += 360f;
            return deg;
        }

        /// <summary>
        /// 以“相机向下看”的符号为基准钳制 Pitch，保证不会翻转到奇怪角度。
        /// </summary>
        /// <param name="pitchSignedDeg">当前 pitch（度，可能为负）。</param>
        /// <returns>钳制后的 pitch（度）。</returns>
        private float ClampPitchSigned(float pitchSignedDeg)
        {
            float min = minPitchDeg;
            float max = maxPitchDeg;

            // 若向下看是负数，则钳制区间应为 [-max, -min]
            if (_pitchSign < 0f)
                return Mathf.Clamp(pitchSignedDeg, -max, -min);

            return Mathf.Clamp(pitchSignedDeg, min, max);
        }

        }


}