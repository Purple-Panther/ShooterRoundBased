using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;

namespace Controllers
{
    [RequireComponent(typeof(CharacterController))]
    public class FpController : MonoBehaviour
    {
        [Header("Movement Parameters")]
        [SerializeField] private float MaxSpeed => isCrouching ? (walkSpeed * 0.5f) : (sprintInput ? sprintSpeed : walkSpeed);
        [SerializeField] private float acceleration = 15f;

        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 8f;
        [SerializeField] private float sprintCooldown = 4f;
        [SerializeField] private float isCooldownSprint = 4f;
        
        [Space(15)]
        [Tooltip("Força do pulo.")]
        [SerializeField] private float jumpHeight = 2f;
        
        public bool Sprinting => sprintInput && CurrentSpeed > 0.1f && !isCrouching && !isSliding;

        [Header("Slide Parameters")]
        [Tooltip("Duração do slide em segundos.")]
        [SerializeField] private float slideDuration = 0.75f;
        [Tooltip("Multiplicador de velocidade aplicado no início do slide.")]
        [SerializeField] private float slideSpeedMultiplier = 1.2f;
        [Tooltip("Altura do CharacterController durante o slide.")]
        [SerializeField] private float slideHeight = 0.4f;
        [Tooltip("Fator de desaceleração durante o slide (maior = para mais rápido).")]
        [SerializeField] private float slideDeceleration = 20f;

        [Header("Crouch Parameters")]
        [Tooltip("Altura do CharacterController enquanto agachado.")]
        [SerializeField] private float crouchHeight = 0.7f;
        
        [Header("Looking Parameters")] 
        public Vector2 lookSensivity = new (.1f, .1f);

        public float pitchLimit = 85f;
        
        [SerializeField] private float currentPitch;

        private float CurrentPitch
        {
            get => currentPitch;
            set => currentPitch = Mathf.Clamp(value, -pitchLimit, pitchLimit);
        }
        
        [Header("Camera Parameters")]
        [SerializeField] private float cameraNormalFov = 60f;
        private float CameraSprintFov => (float)(cameraNormalFov * .3) + cameraNormalFov;

        private readonly float cameraFovSmoothing = 2f;

        private Vector3 baseCameraLocalPos;
        private float bobTime;
        private bool wasGrounded;
        private float landingOffset;

        [Header("Camera Bobbing / Shake")]
        [Tooltip("Ativa/desativa o head bobbing (balanço da câmera)")]
        [SerializeField] private bool enableHeadBob = true;
        [Tooltip("Amplitude do bob ao caminhar")]
        [SerializeField] private float walkBobAmplitude = 0.03f;
        [Tooltip("Frequência do bob ao caminhar")]
        [SerializeField] private float walkBobFrequency = 6f;
        [Tooltip("Amplitude do bob ao correr")]
        [SerializeField] private float sprintBobAmplitude = 0.06f;
        [Tooltip("Frequência do bob ao correr")]
        [SerializeField] private float sprintBobFrequency = 9f;
        [Tooltip("Amplitude do bob ao agachar")]
        [SerializeField] private float crouchBobAmplitude = 0.02f;
        [Tooltip("Frequência do bob ao agachar")]
        [SerializeField] private float crouchBobFrequency = 5f;
        [Tooltip("Quão rápido a câmera interpola até a posição alvo do bob")]
        [SerializeField] private float bobSmoothing = 12f;
        [Tooltip("Intensidade do efeito de aterrissagem ao tocar o chão")]
        [SerializeField] private float landingShakeAmount = 0.05f;
        [Tooltip("Velocidade de recuperação do efeito de aterrissagem")]
        [SerializeField] private float landingRecoverSpeed = 6f;
        
        [Header("Input")]
        [SerializeField] public Vector2 moveInput;
        [SerializeField] public Vector2 lookInput;
        [SerializeField] public bool sprintInput;
        [SerializeField] public bool crouchInput;

        [Header("Components")]
        [SerializeField] private CharacterController controller;
        [SerializeField] private CinemachineCamera fpCamera;

        [FormerlySerializedAs("gravityScaçe")]
        [Header("Physics Parameters")]
        [SerializeField] private float gravityScale = 3f;
        [SerializeField] private float verticalVelocity;
        public Vector3 CurrentVelocity { get; private set; }
        public float CurrentSpeed { get; private set; }
        public bool IsGrounded => controller.isGrounded;

        private bool isSliding;
        private bool isCrouching;
        private float slideTimer;
        private float originalHeight;
        private Vector3 originalCenter;
        private float bottomYOffset;
        private float originalRadius;
        private Vector3 originalScale;
        private float visualHeightScale = 1f;

        public FpController(float slideTimer)
        {
            this.slideTimer = slideTimer;
        }

        private void Awake()
        {
            controller ??= GetComponent<CharacterController>();
            originalHeight = controller.height;
            originalCenter = controller.center;
            bottomYOffset = originalCenter.y - originalHeight * 0.5f;
            originalRadius = controller.radius;
            originalScale = transform.localScale;

            if (fpCamera != null)
            {
                baseCameraLocalPos = fpCamera.transform.localPosition;
            }
            wasGrounded = IsGrounded;
        }
        
        private void OnValidate()
        {
            controller ??= GetComponent<CharacterController>();
        }

        private void Update()
        {
            MoveUpdate();
            LookUpdate();
            CameraUpdate();
        }

        private void CameraUpdate()
        {
            float targetFov = cameraNormalFov;

            if (Sprinting || isSliding)
            {
                float speedRation = Mathf.Clamp01(CurrentSpeed / sprintSpeed);

                targetFov = Mathf.Lerp(cameraNormalFov, CameraSprintFov, speedRation);
            }

            if (fpCamera != null)
            {
                if (enableHeadBob)
                {
                    if (!wasGrounded && IsGrounded)
                    {
                        landingOffset = -landingShakeAmount;
                    }

                    float amp = walkBobAmplitude;
                    float freq = walkBobFrequency;
                    if (isCrouching)
                    {
                        amp = crouchBobAmplitude;
                        freq = crouchBobFrequency;
                    }
                    else if (Sprinting)
                    {
                        amp = sprintBobAmplitude;
                        freq = sprintBobFrequency;
                    }

                    bool canBob = IsGrounded && !isSliding && CurrentSpeed > 0.05f;
                    Vector3 scaledBase = baseCameraLocalPos;
                    scaledBase.y = baseCameraLocalPos.y * visualHeightScale;
                    Vector3 targetLocal = scaledBase;
                    if (canBob)
                    {
                        float speedFactor = Mathf.Clamp01(CurrentSpeed / sprintSpeed);
                        float freqMul = Mathf.Lerp(0.8f, 1.4f, speedFactor);
                        bobTime += Time.deltaTime * freq * freqMul;
                        float bobY = Mathf.Abs(Mathf.Sin(bobTime)) * amp;            // up/down
                        float bobX = Mathf.Sin(bobTime * 0.5f) * (amp * 0.5f);       // side sway
                        targetLocal += new Vector3(bobX, bobY, 0f);
                    }

                    landingOffset = Mathf.MoveTowards(landingOffset, 0f, landingRecoverSpeed * Time.deltaTime);
                    targetLocal.y += landingOffset;

                    fpCamera.transform.localPosition = Vector3.Lerp(fpCamera.transform.localPosition, targetLocal, bobSmoothing * Time.deltaTime);
                }
                else
                {
                    {
                        Vector3 scaledBase = baseCameraLocalPos;
                        scaledBase.y = baseCameraLocalPos.y * visualHeightScale;
                        fpCamera.transform.localPosition = Vector3.Lerp(fpCamera.transform.localPosition, scaledBase, bobSmoothing * Time.deltaTime);
                    }
                }

                wasGrounded = IsGrounded;
            }

            fpCamera.Lens.FieldOfView = Mathf.Lerp(fpCamera.Lens.FieldOfView, targetFov, cameraFovSmoothing * Time.deltaTime);
        }

        private void LookUpdate()
        {
            Vector2 input = new Vector2(lookInput.x * lookSensivity.x, lookInput.y * lookSensivity.y);
            
            CurrentPitch -= input.y;
            fpCamera.transform.localRotation = Quaternion.Euler(CurrentPitch, 0f, 0f);
            
            transform.Rotate(Vector3.up * input.x);
        }

        private void MoveUpdate()
        {
            if (isSliding)
            {
                // Durante o slide, ignoramos o input de movimento e aplicamos desaceleração horizontal
                Vector3 horizontalVel = new Vector3(CurrentVelocity.x, 0f, CurrentVelocity.z);
                horizontalVel = Vector3.MoveTowards(horizontalVel, Vector3.zero, slideDeceleration * Time.deltaTime);
                CurrentVelocity = new Vector3(horizontalVel.x, CurrentVelocity.y, horizontalVel.z);

                slideTimer -= Time.deltaTime;
                if (slideTimer <= 0f || horizontalVel.magnitude < 0.2f)
                {
                    EndSlide();
                }
            }
            else
            {
                // Se estamos agachados, movimento normal (opcionalmente poderia reduzir velocidade)
                Vector3 motion = transform.forward * moveInput.y + transform.right * moveInput.x;
                motion.y = 0f;
                motion.Normalize();

                if (motion.sqrMagnitude >= 0.1f)
                    CurrentVelocity =
                        Vector3.MoveTowards(CurrentVelocity, motion * MaxSpeed, acceleration * Time.deltaTime);
                else
                    CurrentVelocity = Vector3.MoveTowards(CurrentVelocity, Vector3.zero, acceleration * Time.deltaTime);
            }

            // Sair do agachamento quando soltar o botão
            if (isCrouching && !crouchInput)
            {
                EndCrouch();
            }

            if (IsGrounded && verticalVelocity <= 0.01f)
                verticalVelocity = -3f;
            else
                verticalVelocity += Physics.gravity.y * gravityScale * Time.deltaTime;
            
            var fullVelocity = new Vector3(CurrentVelocity.x, verticalVelocity, CurrentVelocity.z);

            controller.Move(fullVelocity * Time.deltaTime);

            CurrentSpeed = new Vector3(CurrentVelocity.x, 0f, CurrentVelocity.z).magnitude;
        }

        public void TryJump()
        {
            if (!IsGrounded || isSliding)
                return;

            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y * gravityScale);
        }

        public void TrySlide()
        {
            // Só pode deslizar se estiver correndo (sprint) e no chão e não estiver agachado
            if (isSliding || isCrouching || crouchInput || !IsGrounded || !Sprinting)
                return;

            isSliding = true;
            slideTimer = slideDuration;

            // Aumenta um pouco a velocidade na direção para frente no início do slide
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 boosted = forward * sprintSpeed * slideSpeedMultiplier;
            CurrentVelocity = new Vector3(boosted.x, verticalVelocity, boosted.z);

            // Reduz a altura do controller mantendo a base na mesma posição
            // Mantemos os valores originais capturados no Awake para garantir restauração correta
            SetControllerHeight(slideHeight);
        }

        private void EndSlide()
        {
            if (!isSliding) return;
            isSliding = false;
            
            // Se ainda estiver segurando o botão de agachar, entra no estado de agachado
            if (crouchInput && IsGrounded)
            {
                StartCrouch();
                return;
            }
            
            // Caso contrário, restaura o CharacterController
            if (CanStandUp())
            {
                SetControllerHeight(originalHeight);
                // Restore radius to original and re-apply center to keep bottom fixed
                controller.center = new Vector3(originalCenter.x, bottomYOffset + controller.height * 0.5f, originalCenter.z);
            }
            else
            {
                // Sem espaço para levantar: permanece/agacha
                StartCrouch();
            }
        }

        private void SetControllerHeight(float newHeight)
        {
            float desiredHeight = Mathf.Max(newHeight, 0.1f);

            float targetRadius = controller.radius;
            
            float finalHeight = Mathf.Max(desiredHeight, targetRadius * 2f + 0.01f);
            controller.height = finalHeight;
            visualHeightScale = originalHeight > 0.0001f ? (desiredHeight / originalHeight) : 1f;
            transform.localScale = new Vector3(originalScale.x, originalScale.y * visualHeightScale, originalScale.z);
            controller.center = new Vector3(originalCenter.x, bottomYOffset + finalHeight * 0.5f, originalCenter.z);
        }

        private bool CanStandUp()
        {
            return HasHeadroom(originalHeight, originalRadius);
        }

        private bool HasHeadroom(float targetHeight, float targetRadius)
        {
            float radius = Mathf.Max(0.01f, targetRadius);
            Vector3 bottomWorld = transform.position + new Vector3(controller.center.x, bottomYOffset, controller.center.z);
            Vector3 p1 = bottomWorld + Vector3.up * radius;
            Vector3 p2 = bottomWorld + Vector3.up * (Mathf.Max(targetHeight, radius * 2f) - radius);
            int layerMask = ~(1 << gameObject.layer);
            return !Physics.CheckCapsule(p1, p2, Mathf.Max(radius - 0.01f, 0.01f), layerMask, QueryTriggerInteraction.Ignore);
        }

        private void StartCrouch()
        {
            if (isCrouching) return;
            isCrouching = true;
            // Não sobrescreva os valores originais; use os capturados no Awake
            SetControllerHeight(crouchHeight);
        }

        private void EndCrouch()
        {
            if (!isCrouching) return;
            // Tenta levantar apenas se houver espaço
            if (CanStandUp())
            {
                isCrouching = false;
                SetControllerHeight(originalHeight);
                controller.radius = originalRadius;
                controller.center = new Vector3(originalCenter.x, bottomYOffset + controller.height * 0.5f, originalCenter.z);
            }
            else
            {
                // Sem espaço acima: permanece agachado
                isCrouching = true;
            }
        }
    }
}
