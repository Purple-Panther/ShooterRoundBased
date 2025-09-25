using UnityEngine;

namespace Interactables
{
    public abstract class Interactable : MonoBehaviour
    {
        public string promptMessage;

        public void Interact()
        {
            InteractAction();
        }

        protected virtual void InteractAction()
        {
            Debug.Log(promptMessage);
        }
    }
}