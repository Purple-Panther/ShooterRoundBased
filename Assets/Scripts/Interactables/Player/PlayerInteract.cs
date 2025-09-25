using Unity.Cinemachine;
using UnityEngine;

namespace Interactables.Player
{
    public class PlayerInteract : MonoBehaviour
    {
        [Header("Interact Settings")] 
        [SerializeField] private float distance = 3f;

        [SerializeField] private LayerMask interactableMask = ~0;
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        private CinemachineCamera fpCam;
        private Transform viewTransform;
        private Interactable hovered;

        private void Awake()
        {
            fpCam = GetComponentInChildren<CinemachineCamera>();
            if (fpCam != null)
                viewTransform = fpCam.transform;
        }

        private void Update()
        {
            if (!viewTransform)
            {
                fpCam = GetComponentInChildren<CinemachineCamera>();
                viewTransform = fpCam.transform;
                if (!viewTransform) return;
            }

            Ray ray = new Ray(viewTransform.position, viewTransform.forward);
            bool hitSomething = Physics.Raycast(ray, out var hit, distance, interactableMask,
                QueryTriggerInteraction.Collide);

            Color color = Color.red;
            hovered = null;
            if (hitSomething)
            {
                color = Color.yellow;
                if (hit.collider)
                {
                    hovered = hit.collider.GetComponentInParent<Interactable>();

                    if (!hovered)
                        return;

                    color = Color.green;
                }
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.DrawRay(ray.origin, ray.direction * distance, color);
#endif

            if (hovered && Input.GetKeyDown(interactKey))
            {
                hovered.Interact();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (!string.IsNullOrEmpty(hovered.promptMessage))
                {
                    Debug.Log($"Interact: {hovered.promptMessage}");
                }
#endif
            }
        }
    }
}