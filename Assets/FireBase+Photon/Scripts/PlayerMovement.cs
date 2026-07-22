using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;

[RequireComponent(typeof(NetworkMecanimAnimator))]
public class PlayerMovement : NetworkBehaviour
{
    private CharacterController controller;
    public Animator animator;

    public float PlayerSpeed = 5f;
    public float WalkSpeedMultiplier = 0.5f;
    public float AnimatorSpeedMax = 15f;
    public float Gravity = -9.81f;
    public float JumpForce = 5;

    private Vector3 velocity;
    private bool isJumpPress;
    private bool isJumping = false;
    private bool isSprinting;

    public Camera Camera;

    public override void Spawned()
    {
        controller = GetComponent<CharacterController>();
       // animator = GetComponent<Animator>();

        if (HasStateAuthority)
        {
            var cam = Camera= Camera.main;

            var fpc = cam.GetComponent<FirstPersonCamera>();
            if (fpc != null) fpc.Target = transform;

            var tpc = cam.GetComponent<ThirdPersonCamera>();
            if (tpc != null) tpc.Target = transform;
        }
       
    }

    void Update()
    {
        if (!HasStateAuthority) return;

        Vector2 moveInput = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        bool hasMoveInput = moveInput.sqrMagnitude > 0.0001f;

        if (!hasMoveInput)
        {
            isSprinting = false;
        }
        else if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            isSprinting = true;
        }

        if (Input.GetButtonDown("Jump") && !isJumpPress)
        {
            isJumpPress = true;
        }
    }


    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;
       

        Quaternion cameraRotationY = Quaternion.Euler(0, Camera.transform.rotation.eulerAngles.y, 0);
        Vector3 moveInput = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
        moveInput = Vector3.ClampMagnitude(moveInput, 1f);

        float movementSpeed = isSprinting
            ? PlayerSpeed
            : PlayerSpeed * Mathf.Clamp01(WalkSpeedMultiplier);
        Vector3 move = cameraRotationY * moveInput * Runner.DeltaTime * movementSpeed;

        if (controller.isGrounded)
        {
            velocity = new Vector3(0, -1, 0);
            animator.SetBool("Grounded", true);
            animator.SetBool("FreeFall", false);
        }
        else
        {
            animator.SetBool("Grounded", false);
            animator.SetBool("FreeFall", true);
        }

        // Apply gravity
        velocity.y += Gravity * Runner.DeltaTime;

        // Ground check
        bool isGrounded = controller.isGrounded;

        // Animation state updates
        animator.SetBool("Grounded", isGrounded);
        animator.SetBool("FreeFall", !isGrounded);

        // Jump trigger only once
        if (isJumpPress && isGrounded && !isJumping)
        {
            velocity.y += JumpForce;
            animator.ResetTrigger("Jump");  // Ensure clean trigger
            animator.SetBool("Jump",true);
            isJumping = true;
        }

        // Detect landing
        if (isJumping && isGrounded)
        {
            isJumping = false; // Reset jump lock on landing
            animator.SetBool("Jump", false);
        }

        // Final move
        controller.Move(move + velocity * Runner.DeltaTime);


      //  controller.Move(move + velocity * Runner.DeltaTime);

        // Face move direction
        if (moveInput != Vector3.zero)
        {
            transform.forward = new Vector3(move.x, 0, move.z);
        }

        // 🔄 Sync animator parameters
        float inputMagnitude = moveInput.magnitude;
        float normalizedMovementSpeed = PlayerSpeed > 0f
            ? inputMagnitude * movementSpeed / PlayerSpeed
            : 0f;
        float animSpeed = normalizedMovementSpeed * AnimatorSpeedMax;
        animator.SetFloat("Speed", animSpeed); // maps input speed to the blend tree's 0-15 range

        animator.SetFloat("MotionSpeed", 1f);

        isJumpPress = false;
    }
}
