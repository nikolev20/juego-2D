using UnityEngine;

namespace ForestBackgroundsPixelArtOriginal
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Player : MonoBehaviour
    {
        private Rigidbody2D rb;

        [SerializeField]
        [Range(2.0f, 8.0f)]
        private float speed = 4.0f;

        [SerializeField]
        [Range(10.0f, 20.0f)]
        private float jumpForce = 16.0f;

        [SerializeField]
        private bool endlessMove = false;

        [SerializeField]
        private bool flyMode = false;

        private void Start()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        private void Update()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            rb.linearVelocity = new Vector2(endlessMove ? speed : horizontal * speed, flyMode ? vertical * speed : rb.linearVelocity.y);
            rb.gravityScale = flyMode ? 0.0f : 4.0f;

            if (!flyMode)
            {
                if (Input.GetButtonDown("Jump"))
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
                }

                if (Input.GetButtonUp("Jump") && rb.linearVelocity.y > 0f)
                {
                    rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.5f);
                }
            }
        }
    }
}

