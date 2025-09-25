using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

namespace Controllers
{
    [RequireComponent(typeof(CharacterController))]
    public class FpController : MonoBehaviour
    {
        [Header("Movement Parameters")]
        
        [SerializeField] private float acceleration = 15f;
        [SerializeField] private float brake = 40f;
        [SerializeField] private float turnResponsiveness = 25f;
        [SerializeField] private float counterStrafe = 60f;
        [SerializeField] private float walkSpeed = 3.5f;
        [SerializeField] private float sprintSpeed = 8f;
        
        [Space(15)]
        [Tooltip("Força do pulo.")]
        [SerializeField] private float jumpHeight = 2f;
        
        private float MaxSpeed => isCrouching ? (walkSpeed * 0.5f) : (sprintInput ? sprintSpeed : walkSpeed);
        private bool Sprinting => sprintInput && currentSpeed > 0.1f && !isCrouching && !isSliding;

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
        [SerializeField] private Vector2 lookSensitivity = new (.1f, .1f);
        [SerializeField] private float pitchLimit = 85f;
        [SerializeField] private float currentPitch;
        private float CurrentPitch
        {
            get => currentPitch;
            set => currentPitch = Mathf.Clamp(value, -pitchLimit, pitchLimit);
        }
        
        [Header("Camera Parameters")]
        [SerializeField] private float cameraNormalFov = 60f;
        private float CameraSprintFov => (float)(cameraNormalFov * .3) + cameraNormalFov;
        [SerializeField] 
        private float cameraFovSmoothing = 2f;
        [SerializeField] 
        private float cameraHeigthSmoothing = 2f;
        [SerializeField] 
        private float cameraHeightScale;
        
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
        
        [Header("Physics Parameters")]
        [SerializeField] private float gravityScale = 3f;
        [SerializeField] private float verticalVelocity;
        [SerializeField] public Vector3 currentVelocity;
        [SerializeField] public float currentSpeed; 
        
        [Header("Advanced Gravity")]
        [SerializeField] [Tooltip("> 1 acelera queda")]private float fallGravityMultiplier = 2.2f; 
        [SerializeField] [Tooltip("> 1 dá menos \"flutuação\" na subida")]private float ascentGravityMultiplier = 1.2f; 
        [SerializeField] [Tooltip("Clamp da velocidade de queda")]private float terminalFallSpeed = -55f; 
        [SerializeField] [Tooltip("A partir disso, aterrisagem forte")]private float hardLandingSpeed = 14f; 
        [SerializeField] [Tooltip("Shake máximo da câmera")]private float maxLandingShake = 0.18f; 
        [SerializeField] [Tooltip("Reduz vel. horizontal no impacto")]private float landingHorizontalDampen = 0.15f; 
        
        [Header("Slide Input Rules")]
        [SerializeField] [Tooltip("Quão \"para frente\" o input precisa estar (0..1)")] private float slideForwardThreshold = 0.6f; 
        [SerializeField] [Tooltip("Janela de buffer do slide no ar (segundos)")] private float slideQueueTime = 0.25f;       


        
        private bool IsGrounded => controller.isGrounded;
        private bool isSliding;
        private bool isCrouching;
        private float slideTimer;
        private float originalHeight;
        private Vector3 originalCenter;
        private float bottomYOffset;
        private float originalRadius;
        private Vector3 originalScale;
        private float visualHeightScale = 1f;
        private Vector3 baseCameraLocalPos;
        private float bobTime;
        private bool wasGrounded;
        private float landingOffset;
        private float fallStartY;
        private bool wasFalling; 
        private float peakFallSpeed;
        private bool slideQueued;
        private float slideQueuedUntil;
        private bool sprintQueuedDuringSlide;
        
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
                baseCameraLocalPos = fpCamera.transform.localPosition;

            if (fpCamera == null)
                fpCamera = GetComponentInChildren<CinemachineCamera>();
            
            wasGrounded = IsGrounded;
            cameraHeightScale = visualHeightScale;
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
                float speedRation = Mathf.Clamp01(currentSpeed / sprintSpeed);

                targetFov = Mathf.Lerp(cameraNormalFov, CameraSprintFov, speedRation);
            }

            if (fpCamera == null)
                return;

            cameraHeightScale =
                Mathf.Lerp(cameraHeightScale, visualHeightScale, cameraHeigthSmoothing * Time.deltaTime);

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

                bool canBob = IsGrounded && !isSliding && currentSpeed > 0.05f;

                Vector3 scaledBase = baseCameraLocalPos;
                scaledBase.y = baseCameraLocalPos.y * cameraHeightScale;
                Vector3 targetLocal = scaledBase;
                if (canBob)
                {
                    float speedFactor = Mathf.Clamp01(currentSpeed / sprintSpeed);
                    float freqMul = Mathf.Lerp(0.8f, 1.4f, speedFactor);
                    bobTime += Time.deltaTime * freq * freqMul;
                    float bobY = Mathf.Abs(Mathf.Sin(bobTime)) * amp; // Baixo/cima
                    float bobX = Mathf.Sin(bobTime * 0.5f) * (amp * 0.5f); // lateral
                    targetLocal += new Vector3(bobX, bobY, 0f);
                }

                landingOffset = Mathf.MoveTowards(landingOffset, 0f, landingRecoverSpeed * Time.deltaTime);
                targetLocal.y += landingOffset;

                fpCamera.transform.localPosition = Vector3.Lerp(fpCamera.transform.localPosition, targetLocal,
                    bobSmoothing * Time.deltaTime);
            }
            else
            {
                {
                    Vector3 scaledBase = baseCameraLocalPos;
                    scaledBase.y = baseCameraLocalPos.y * cameraHeightScale;
                    fpCamera.transform.localPosition = Vector3.Lerp(fpCamera.transform.localPosition, scaledBase,
                        bobSmoothing * Time.deltaTime);
                }
            }

            wasGrounded = IsGrounded;

            fpCamera.Lens.FieldOfView =
                Mathf.Lerp(fpCamera.Lens.FieldOfView, targetFov, cameraFovSmoothing * Time.deltaTime);
        }

        private void LookUpdate()
        {
            Vector2 input = new Vector2(lookInput.x * lookSensitivity.x, lookInput.y * lookSensitivity.y);
            CurrentPitch -= input.y;
            if(fpCamera != null)
                fpCamera.transform.localRotation = Quaternion.Euler(CurrentPitch, 0f, 0f);
            
            transform.Rotate(Vector3.up * input.x);
        }

        private void MoveUpdate()
        {
            if (isSliding)
            {
                if (sprintInput)
                    sprintQueuedDuringSlide = true;
                
                Vector3 horizontalVel = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
                // Vector3 targetVel = new Vector3(currentVelocity.x * .3f, 0f, currentVelocity.z * .3f);
                horizontalVel = Vector3.MoveTowards(horizontalVel, Vector3.zero, slideDeceleration * Time.deltaTime);
                currentVelocity = new Vector3(horizontalVel.x, currentVelocity.y, horizontalVel.z);

                slideTimer -= Time.deltaTime;
                if (slideTimer <= 0.2f || horizontalVel.magnitude < 0.2f)
                {
                    EndSlide();
                }
            }
            else
            {
                // Se estamos agachados, movimento normal (opcionalmente poderia reduzir velocidade)
                Vector3 motion = transform.forward * moveInput.y + transform.right * moveInput.x;
                motion.y = 0f;
                
                float inputMagnitude = motion.magnitude;
                Vector3 desiredDir = inputMagnitude > 0.001f ? motion.normalized : Vector3.zero;
                
                Vector3 horizVel = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
                bool hasInput = inputMagnitude > 0.1f;

                if (hasInput)
                {
                    //Reorienta rapidamente a velocidade para a direção desejada (diminui a patinada)
                    if (horizVel.sqrMagnitude > 0.0001f && desiredDir != Vector3.zero)
                    {
                        Vector3 target = desiredDir * horizVel.magnitude; //Mantem módulo, muda direção
                        horizVel = Vector3.RotateTowards(horizVel, target, turnResponsiveness * Mathf.Deg2Rad * Time.deltaTime, Mathf.Infinity);
                    }
                    
                    //Se o input aponta Contra a velocidade atual, aplicar um freio mais forte primeiro
                    float against = horizVel.sqrMagnitude > 0.0001f ? Vector3.Dot(horizVel.normalized, desiredDir) : 1f;
                    float accelThisFrame = against < 0f ? counterStrafe : acceleration;
                    
                    //Acelera até a velocidade alvo
                    Vector3 targetDir = desiredDir * MaxSpeed;
                    horizVel = Vector3.MoveTowards(horizVel, targetDir, accelThisFrame * Time.deltaTime);
                }
                else
                {
                    // Sem Input = freio agressivo
                    horizVel = Vector3.MoveTowards(horizVel, Vector3.zero, brake * Time.deltaTime);
                }

                currentVelocity = new Vector3(horizVel.x, currentVelocity.y, horizVel.z);
            }

            // Sair do agachamento quando soltar o botão
            if (isCrouching && !crouchInput)
            {
                EndCrouch();
            }

            if (IsGrounded)
            {
                if (wasFalling)
                    HandleLandingImpact();

                if (slideQueued)
                {
                    if (Time.time <= slideQueuedUntil)
                    {
                        BeginSlide(true);
                    }

                    slideQueued = false;
                }
                
                wasFalling = false;
                peakFallSpeed = 0f;
                
                if(verticalVelocity <= 0f)
                    verticalVelocity = -3f; // mantém colado no chão
            }
            else
            {
                // No ar: aplica gravidade diferenciada
                float baseG = Physics.gravity.y * gravityScale;
                bool rising = verticalVelocity > 0f;
                float gMul = rising ? ascentGravityMultiplier : fallGravityMultiplier;

                verticalVelocity += baseG * gMul * Time.deltaTime;
                
                // Clamp da velocidade terminal
                if (verticalVelocity < terminalFallSpeed)
                    verticalVelocity = terminalFallSpeed;
                
                //Marcar inicio da queda e acompanhar pico de velocidade de queda
                if (!wasFalling)
                {
                    wasFalling = true;
                    fallStartY = transform.position.y;
                    peakFallSpeed = 0f;
                }
                if(verticalVelocity < peakFallSpeed)
                    peakFallSpeed = verticalVelocity;
            }
            
            if (IsGrounded && verticalVelocity < 0f)
                verticalVelocity = -3f;

            Vector3 fullVelocity = new Vector3(currentVelocity.x, verticalVelocity, currentVelocity.z);

            currentSpeed = new Vector2(fullVelocity.x, fullVelocity.z).magnitude;

            controller.Move(fullVelocity * Time.deltaTime);
        }

        private void HandleLandingImpact()
        {
            // baseada na velocidade de impacto, converter para intensidade [0 || 1]
            float impactSpeed = Mathf.Abs(peakFallSpeed);

            // normaliza contra a threshold de grande impacto
            float t = Mathf.InverseLerp(0f, hardLandingSpeed, impactSpeed);
            
            // Amplitude do shake proporcional até um teto
            float extraShake = Mathf.Lerp(0f, maxLandingShake, t);
            
            // Empurra o offset de aterrissagem
            landingOffset = -Mathf.Max(landingShakeAmount, extraShake);
            
            // Amortece um pouco a velocidade horizontal dependendo do impacto
            Vector3 horiz = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
            horiz *= 1f - landingHorizontalDampen * t;

            currentVelocity = new Vector3(horiz.x, currentVelocity.y, horiz.z);
        }

        public void TryJump()
        {
            if (!IsGrounded || isSliding)
                return;

            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y * gravityScale);
        }

        public void TrySlide()
        {
            if (isSliding || isCrouching || crouchInput)
                return;

            if (!IsGrounded)
            {
                slideQueued = true;
                slideQueuedUntil = Time.time + slideQueueTime;
                return;
            }
            
            if(!Sprinting) return;
            if(!HasForwardSlideInput()) return;
            
            BeginSlide(false);
        }

        private void EndSlide()
        {
            if (!isSliding) return;
            isSliding = false;

            if (crouchInput && IsGrounded)
            {
                StartCrouch();
                return;
            }
            
            if (CanStandUp())
            {
                SetControllerHeight(originalHeight);
                controller.center = new Vector3(originalCenter.x, bottomYOffset + controller.height * 0.5f, originalCenter.z);
            }
            else
            {
                // Sem espaço para levantar: permanece/agacha
                StartCrouch();
            }
            
            if (sprintQueuedDuringSlide && IsGrounded && HasForwardSlideInput())
            {
                Vector3 fwd = transform.forward;
                fwd.y = 0f;
                fwd.Normalize();
                Vector3 boosted = fwd * sprintSpeed;
                currentVelocity = new Vector3(boosted.x, currentVelocity.y, boosted.z);
                sprintQueuedDuringSlide = false;
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
        
        // Considera apenas input para frente, rejeitando lateral/trás
        private bool HasForwardSlideInput()
        {
            // moveInput.y é o eixo para frente; moveInput.x é lateral
            // Exige componente para frente suficiente e limita o lateral
            if (moveInput.magnitude < 0.001f) return false;

            float forwardComp = moveInput.y;               // -1 (trás) a +1 (frente)
            float lateralComp = Mathf.Abs(moveInput.x);    // 0 (sem strafe) a 1

            // Regras: precisa empurrar o suficiente para frente e não estar strafando muito
            return forwardComp >= slideForwardThreshold && lateralComp <= (1f - slideForwardThreshold);
        }

        // Inicia o slide, aplicando velocidade para frente. Se forceMax for true, usa sprintSpeed
        private void BeginSlide(bool forceMax)
        {
            isSliding = true;
            slideTimer = slideDuration;

            sprintQueuedDuringSlide = false;

            // Direção para frente do jogador no plano
            Vector3 fwd = transform.forward; fwd.y = 0f; fwd.Normalize();

            float baseSpeed = forceMax ? sprintSpeed : MaxSpeed; // quando aterrissar do buffer, forçamos velocidade máxima
            Vector3 boosted = fwd * (baseSpeed * slideSpeedMultiplier);

            currentVelocity = new Vector3(boosted.x, verticalVelocity, boosted.z);

            SetControllerHeight(slideHeight);
        }
    }
}
