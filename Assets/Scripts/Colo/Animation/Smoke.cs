
using Lonize;
using UnityEngine;

namespace Colo.Animation
{
    public class Smoke : MonoBehaviour
    {
        private AnimationControls inputActions;
        void Start()
        {
            inputActions = InputActionManager.Instance.Animation;
            var particleSystem = GetComponent<ParticleSystem>();
            // if (particleSystem == null)
            // {
            //     Debug.LogWarning("ParticleSystem component is missing.");
            //     return;
            // }
        }

        // Update is called once per frame
        void Update()
        {
            if(inputActions.Animation.Smoke.IsPressed())
            {
                var animator = GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.SetBool("smoke", true);
                }
            }
            else
            {
                var animator = GetComponentInChildren<Animator>();
                if (animator != null)
                {
                    animator.SetBool("smoke_off", true);
                }
            }
        }
    }
}