using System;
using System.Collections;
using System.Numerics;
using UnityEngine;
using UnityEngine.InputSystem;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class PlayerController : MonoBehaviour
{
     static PlayerController instance { get; set; }
    public static PlayerController Instance
    {
        get
        {
            return instance;
        }

        private set
        {
            instance = value;
            OnInitialize?.Invoke();
        }
    }
    
    
    public static event Action OnInitialize;
    
    [Header("States")]
    [SerializeField] private MovementParams walking;
    [SerializeField] private MovementParams running;
    [SerializeField] private MovementParams crouching;
    [SerializeField] MovementParams currentMovement;
    
    [Header("Adjustable Parameters")]
    [SerializeField] float jumpForce = 5f;
    [SerializeField] private float slideVelocity = 5f;
    [SerializeField] private float slideUpBias = 2.0f;
    [SerializeField] private float movementPauseTime = 1f;
    [SerializeField] public string characterName = "Player";

    // components and tracker variables
    private Rigidbody rb;
    private Animator animator;
    Collider collider;
    [SerializeField] private Vector3 currentInput;
    
    // State tracking
    private bool moving = false;
    private bool noMove = false;
    private bool sliding = false;
    private Vector3 cachedWallNormal = Vector3.zero;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        rb = GetComponent<Rigidbody>();
        animator = transform.GetChild(1).GetComponent<Animator>();
        collider = GetComponent<Collider>();
        currentMovement = walking;
    }
    

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Debug.Log("Instance cleared");
        }
    }

    void OnMove(InputValue value)
    {
        currentInput = value.Get<Vector2>();
        currentInput = new Vector3(currentInput.x, 0, currentInput.y);
        
        animator.SetFloat("VerticalInput", currentInput.z);
        animator.SetFloat("HorizontalInput", currentInput.x);
        if (!moving)
        {
            moving = true;
            StartCoroutine(Move());
            currentInput.Normalize();
        }
        
        if (currentInput.magnitude < 0.1f)
        {
            moving = false;
            //rb.velocity = Vector3.zero;
        }

    }
    
    
    void OnSprint(InputValue value)
    {
        if (currentMovement == crouching)
        {
            StopCrouch();        
        }
        
        
        if (value.isPressed)
        {
            currentMovement = running;
            return;
        }
        currentMovement = walking;
        
    }


    bool IsGrounded()
    {
        Vector3 size = collider.bounds.extents;
        RaycastHit hit;
        return Physics.Raycast(transform.position, Vector3.down, out hit, size.y + 0.1f);
    }
    
    
    void OnJump(InputValue value)
    {
        if (IsGrounded())
        {
            rb.AddForce(transform.up * jumpForce);
            return;
        }
        
        if (sliding)
        {
            WallHop();
            return;
        }
    }
    
    void OnCrouch(InputValue value)
    {
        
        if (value.isPressed)
        {
            float oldHeight;
            BoxCollider boxCollider = collider as BoxCollider;
            
            if (!boxCollider || currentMovement == crouching)
            {
                Debug.LogError("Collider is not a BoxCollider");
                return;
            }
            
            currentMovement = crouching;
            oldHeight = boxCollider.size.y;
            boxCollider.size = new Vector3(boxCollider.size.x, boxCollider.size.y * 0.5f, boxCollider.size.z);
            boxCollider.center = new Vector3(boxCollider.center.x,boxCollider.center.y - (oldHeight * 0.25f), boxCollider.center.z);
        
            
            return;
        }
        
        StopCrouch();
        
    }


    void StopCrouch()
    {
        BoxCollider boxCollider = collider as BoxCollider;

        boxCollider.size = new Vector3(boxCollider.size.x, boxCollider.size.y * 2f, boxCollider.size.z);
        boxCollider.center = new Vector3(boxCollider.center.x,boxCollider.center.y + (boxCollider.size.y * 0.25f), boxCollider.center.z);
        
        currentMovement = walking;
    }


    IEnumerator Move()
    {
        while (moving)
        {
            if (noMove)
            {
                yield return new WaitForFixedUpdate();
                continue;
            }
            
            rb.AddForce(currentInput * (currentMovement.acceleration * Time.deltaTime), ForceMode.Acceleration);
            
            // perserves the vertical speed
            float ySpeed = rb.linearVelocity.y;
            rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, currentMovement.maxSpeed);
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, ySpeed, rb.linearVelocity.z);
            
            yield return new WaitForFixedUpdate();
        }
    }
    
    
    private void OnCollisionEnter(Collision other)
    {
        if (IsGrounded())
        {
            sliding = false;
            return;
        }
        
        cachedWallNormal = other.GetContact(0).normal;
        if (moving)
        {
            StartCoroutine(WallSlide());
        }
        
    }

    private void OnCollisionExit(Collision other)
    {
        sliding = false;
    }


    void WallHop()
    {
        // hop off a wall
        if (cachedWallNormal.x != 0 || cachedWallNormal.z != 0)
        {
            rb.AddForce(jumpForce * cachedWallNormal + (Vector3.up * slideVelocity), ForceMode.Acceleration);
        }
    }
    
    
    // allow the player to slide down a wall
    IEnumerator WallSlide()
    {
        // if the player is not on a wall, don't slide
        if (!(cachedWallNormal.x != 0 || cachedWallNormal.z != 0 || IsGrounded()))
            yield break;

        // pause the movement for a bit to let hop occur
        StartCoroutine(PauseMovement());
        
        sliding = true;
        while (sliding)
        {
            // set slide to set speed
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, -slideVelocity, rb.linearVelocity.z);
            yield return new WaitForFixedUpdate();
        }
    }


    // pause the movement for a bit
    IEnumerator PauseMovement()
    {
        noMove = true;
        yield return new WaitForSeconds(movementPauseTime);
        noMove = false;
    }
    
    
    
}
