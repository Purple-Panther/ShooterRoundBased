using UnityEngine;

namespace Interactables.Sphere
{
    public class SphereInteract : Interactable
    {
        protected override void InteractAction()
        {
            Debug.Log("Interact with the sphere.");
        }
    }
}