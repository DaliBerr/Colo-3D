using System.Collections;
using System.Threading.Tasks;
using Lonize;
using Lonize.Logging;
using UnityEngine;

namespace Colo.Animation
{
    public class InstantiateCube : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private float _forwardDistance = 2f;
        [SerializeField] private bool _spawnOnGround = true;
        [SerializeField] private float _groundRayLength = 10f;
        [SerializeField] private LayerMask _groundMask;

        private AnimationControls _inputActions;
        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
            _inputActions = InputActionManager.Instance.Animation;

        }
        public void Update()
        {
            if (_inputActions.Instantiate.Cube.WasPressedThisFrame())
            {
                GameDebug.Log("Instantiate Cube Input Detected");
                Vector3 spawnPos = GetSpawnPosition();

                Quaternion spawnRot = Quaternion.identity;

                GameDebug.Log("Spawning Cube at Position: " + spawnPos + " with Rotation: " + spawnRot);
                StartCoroutine(InstantiateCubeAtPositionCoroutine(spawnPos, spawnRot));
            }
        }
        public IEnumerator InstantiateCubeAtPositionCoroutine(Vector3 position, Quaternion rotation)
        {
            var task = InstantiateCubeAtPosition(position, rotation);
            yield return new WaitUntil(() => task.IsCompleted);
        }
        public async Task<GameObject> InstantiateCubeAtPosition(Vector3 position, Quaternion rotation)
        {
            var cubePrefab = await Kernel.AddressableRef.LoadAsync<GameObject>("Prefabs/Animation/Cube");
            if (cubePrefab != null)
            {
                GameDebug.Log("Instantiating Cube at position: " + position);
                var cubeInstance = Instantiate(cubePrefab, position, rotation);
                return cubeInstance;
            }
            else
            {

                GameDebug.LogError("Failed to load CubePrefab from Addressables.");
                return null;
            }
        }
        private Vector3 GetSpawnPosition()
        {
            Transform t = _camera.transform;
            Vector3 basePos = t.position + t.forward * Mathf.Max(0f, _forwardDistance);

            if (!_spawnOnGround)
                return basePos;

            Ray ray = new Ray(t.position, t.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, _groundRayLength, _groundMask, QueryTriggerInteraction.Ignore))
                return hit.point;

            return basePos;
        }
    }
}