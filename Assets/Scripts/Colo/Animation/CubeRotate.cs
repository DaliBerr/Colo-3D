using Lonize;
using Lonize.Logging;
using UnityEngine;

namespace Colo.Animation
{
    public class CubeRotate : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        // [SerializeField, Range(0f, 1f)] private float _speedLerp = 10f;
        
        private AnimationControls inputActions;


        private void Awake()
        {
            if(_animator == null)
            {
                TryGetComponent(out _animator);
            }
            inputActions = InputActionManager.Instance.Animation;
        }

        private void Update()
        {
            if(_animator == null)
            {
                GameDebug.LogWarning("Animator component is missing.");
                return;
            }
            if(inputActions.Animation.ET1.IsPressed())
            {
                _animator.SetBool("ETAT1", true);
            }
            else
            {
                _animator.SetBool("ETAT1", false);
            }
            if(inputActions.Animation.ET2.IsPressed())
            {
                _animator.SetBool("ETAT2", true);
            }
            else
            {
                _animator.SetBool("ETAT2", false);
            }
        }
    }
}