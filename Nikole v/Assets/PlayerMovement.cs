using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Script de movimiento para jugador 2D en Unity.
/// Requiere: Rigidbody2D, Collider2D en el GameObject del jugador.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Vida")]
    public Slider VidaSlider;
    // public Button BotonPotencia;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private int maxHealth = 1000;

    // ─────────────────────────────────────────────
    //  PARÁMETROS DE MOVIMIENTO
    // ─────────────────────────────────────────────
    [Header("Movimiento Horizontal")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float acceleration = 15f;   // Qué tan rápido alcanza la velocidad máxima
    [SerializeField] private float deceleration = 20f;   // Qué tan rápido frena al soltar la tecla
    [SerializeField] private float airControlFactor = 0.6f; // Control reducido en el aire (0-1)

    [Header("Salto")]
    [SerializeField] private float jumpForce = 16f;
    [SerializeField] private float jumpCutMultiplier = 0.4f;  // Salto corto al soltar la tecla
    [SerializeField] private float fallGravityMultiplier = 2.5f; // Caída más rápida y natural
    [SerializeField] private float maxFallSpeed = 25f;
    [SerializeField] private int extraJumps = 1;         // 0 = sin doble salto, 1 = doble salto

    [Header("Detección de Suelo")]
    [SerializeField] private Transform groundCheck;      // Punto hijo en los pies del jugador
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;      // Asignar la capa "Ground" en el Inspector

    [Header("Coyote Time & Jump Buffer")]
    [SerializeField] private float coyoteTime = 0.15f;   // Segundos tras caer del borde donde aún se puede saltar
    [SerializeField] private float jumpBufferTime = 0.15f; // Segundos antes de tocar suelo donde se guarda el salto

    // ─────────────────────────────────────────────
    //  REFERENCIAS INTERNAS
    // ─────────────────────────────────────────────
    private Rigidbody2D rb;
    private Animator animator;         // Opcional — asigna si tienes Animator
    private SpriteRenderer spriteRenderer;

    // ─────────────────────────────────────────────
    //  ESTADO
    // ─────────────────────────────────────────────
    private float horizontalInput;
    private bool isGrounded;
    private bool wasGrounded;
    private int jumpsRemaining;

    private float coyoteTimer;
    private float jumpBufferTimer;
    private bool isJumping;

    // Gravedad base del Rigidbody2D
    private float defaultGravityScale;

    // ─────────────────────────────────────────────
    //  UNITY CALLBACKS
    // ─────────────────────────────────────────────
    public void Potenciador()
    {
        VidaSlider.value += 5f;
        maxHealth += 5;
    }
    private void Awake()
    {
        healthText.text = $"Health: {maxHealth}";
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();            // null si no existe
        spriteRenderer = GetComponent<SpriteRenderer>(); // null si no existe

        defaultGravityScale = rb.gravityScale;
        jumpsRemaining = extraJumps + 1;
        // Button btn = BotonPotencia.GetComponent<Button>(); 
    }

    private void Update()
    {
        GatherInput();
        HandleCoyoteTime();
        HandleJumpBuffer();
        HandleJumpInput();
        FlipSprite();
        UpdateAnimator();

       healthText.text = $"Health: {maxHealth}";

        if (VidaSlider.value <= 0)
        {
            Destroy(gameObject);
        }  


    }

    private void FixedUpdate()
    {
        CheckGround();
        HandleHorizontalMovement();
        HandleGravity();
        ClampFallSpeed();
    }

    // ─────────────────────────────────────────────
    //  INPUT
    // ─────────────────────────────────────────────
    private void GatherInput()
    {
        horizontalInput = Input.GetAxisRaw("Horizontal"); // -1, 0, 1
    }

    // ─────────────────────────────────────────────
    //  DETECCIÓN DE SUELO
    // ─────────────────────────────────────────────
    private void CheckGround()
    {
        wasGrounded = isGrounded;
        isGrounded = Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);

        // Al aterrizar, restaura saltos disponibles
        if (!wasGrounded && isGrounded)
        {
            jumpsRemaining = extraJumps + 1;
            isJumping = false;
        }
    }

    // ─────────────────────────────────────────────
    //  MOVIMIENTO HORIZONTAL
    // ─────────────────────────────────────────────
    private void HandleHorizontalMovement()
    {
        float targetSpeed = horizontalInput * moveSpeed;
        float currentSpeed = rb.linearVelocity.x;
        float speedDiff = targetSpeed - currentSpeed;

        // Selecciona aceleración o desaceleración
        float accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? acceleration : deceleration;

        // Reduce el control en el aire
        if (!isGrounded)
            accelRate *= airControlFactor;

        // Calcula la fuerza a aplicar
        float movement = speedDiff * accelRate;
        rb.AddForce(new Vector2(movement, 0f), ForceMode2D.Force);
    }

    // ─────────────────────────────────────────────
    //  SALTO
    // ─────────────────────────────────────────────
    private void HandleJumpInput()
    {
        // Presionar salto → llenar buffer
        if (Input.GetButtonDown("Jump"))
            jumpBufferTimer = jumpBufferTime;

        // Soltar salto → recortar altura (salto variable)
        if (Input.GetButtonUp("Jump") && isJumping && rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * jumpCutMultiplier);

        // Ejecutar salto si hay buffer activo y algún salto disponible
        bool canJump = (coyoteTimer > 0f || jumpsRemaining > 0) && jumpBufferTimer > 0f;

        if (canJump)
        {
            PerformJump();
        }
    }

    private void PerformJump()
    {
        jumpBufferTimer = 0f;
        coyoteTimer = 0f;
        isJumping = true;

        // Descuenta un salto; si no está en suelo, descuenta de los extras
        if (!isGrounded)
            jumpsRemaining--;
        else
            jumpsRemaining = Mathf.Max(0, jumpsRemaining - 1);

        // Aplica fuerza de salto (reemplaza velocidad vertical para consistencia)
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, 0f);
        rb.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
    }

    // ─────────────────────────────────────────────
    //  COYOTE TIME & JUMP BUFFER
    // ─────────────────────────────────────────────
    private void HandleCoyoteTime()
    {
        if (isGrounded)
            coyoteTimer = coyoteTime;
        else
            coyoteTimer -= Time.deltaTime;
    }

    private void HandleJumpBuffer()
    {
        jumpBufferTimer -= Time.deltaTime;
    }

    // ─────────────────────────────────────────────
    //  GRAVEDAD DINÁMICA
    // ─────────────────────────────────────────────
    private void HandleGravity()
    {
        if (rb.linearVelocity.y < 0f)
        {
            // Caída más pesada
            rb.gravityScale = defaultGravityScale * fallGravityMultiplier;
        }
        else if (rb.linearVelocity.y > 0f && !Input.GetButton("Jump"))
        {
            // Subida con botón suelto → también un poco más rápida
            rb.gravityScale = defaultGravityScale * 1.5f;
        }
        else
        {
            rb.gravityScale = defaultGravityScale;
        }
    }

    private void ClampFallSpeed()
    {
        if (rb.linearVelocity.y < -maxFallSpeed)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -maxFallSpeed);
    }

    // ─────────────────────────────────────────────
    //  VISUAL: FLIP DEL SPRITE
    // ─────────────────────────────────────────────
    private void FlipSprite()
    {
        if (spriteRenderer == null) return;

        if (horizontalInput > 0f)
            spriteRenderer.flipX = false;
        else if (horizontalInput < 0f)
            spriteRenderer.flipX = true;
    }

    // ─────────────────────────────────────────────
    //  ANIMATOR (OPCIONAL)
    // ─────────────────────────────────────────────
    private void UpdateAnimator()
    {
        if (animator == null) return;

        // Usa el input directo para respuesta inmediata
        animator.SetFloat("Speed", Mathf.Abs(horizontalInput));
        animator.SetBool("IsGrounded", isGrounded);
        animator.SetFloat("VerticalVelocity", rb.linearVelocity.y);
    }

    // ─────────────────────────────────────────────
    //  GIZMOS: visualiza el groundCheck en el editor
    // ─────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if(collision.gameObject.CompareTag("Enemy"))
        {
            // maxHealth -= 2;
        }
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.gameObject.CompareTag("Enemy"))
        {
            // maxHealth -= 2; 
        }
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        if(collision.gameObject.CompareTag("Enemy"))
        {
            smaxHealth -= 3;
            VidaSlider.value -= 3; 
        }
    }
}