using Controllers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Entities
{
    [RequireComponent(typeof(FpController))]
    public class Player : MonoBehaviour
    {
        [Header("Components")] 
        [SerializeField] private FpController controller;

        private void OnMove(InputValue value)
        {
            controller.moveInput = value.Get<Vector2>();
        }

        private void OnLook(InputValue value)
        {
            controller.lookInput = value.Get<Vector2>();
        }

        private void OnSprint(InputValue value)
        {
            controller.sprintInput = value.isPressed;
        }

        private void OnJump(InputValue value)
        {
            if (!value.isPressed) return;

            controller.TryJump();
        }

        private void OnCrouch(InputValue value)
        {
            bool pressed = value.isPressed;
            if (pressed)
            {
                // Primeiro tenta iniciar o slide, depois mantém o estado de agachar pressionado
                controller.TrySlide();
            }
            controller.crouchInput = pressed;
        }

        private void OnValidate()
        {
            controller ??= GetComponent<FpController>();
        }

        private void Start()
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }
}