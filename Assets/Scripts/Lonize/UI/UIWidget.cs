

using UnityEngine;

namespace Lonize.UI
{
    public abstract class UIWidget : MonoBehaviour
    {
        protected virtual void OnEnable()  { Bind(); }
        protected virtual void OnDisable() { Unbind(); }
        protected abstract void Bind();
        protected abstract void Unbind();
    }
}