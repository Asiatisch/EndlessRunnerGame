using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

namespace EndlessRunner.Player {

[RequireComponent(typeof(CharacterController), typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
  [SerializeField, Tooltip("The starting speed of the player.")]
    private float initialPlayerSpeed = 4f;
    [SerializeField, Tooltip("The fastest speed the player can go.")]
    private float maximumPlayerSpeed = 30f;
    [SerializeField, Tooltip("The rate at which the speed increases by on each frame. This is multiplied by Time.deltaTime.")]
    private float playerSpeedIncreaseRate = .1f;
    [SerializeField, Tooltip("How hight the player should jump.")]
    private float jumpHeight = 1.0f;
    [SerializeField, Tooltip("The initial value of gravity.")]
    private float initialGravityValue = -9.81f;
    [SerializeField, Tooltip("The assigned layer for the ground. Used to check if the player is grounded.")]
    private LayerMask groundLayer;
    [SerializeField, Tooltip("The assigned layer where the obstacles are placed.")]
    private LayerMask obstacleLayer;
    [SerializeField]
    private LayerMask turnLayer;
     [SerializeField, Tooltip("Animator component of the player.")]
    private Animator animator;
    [SerializeField, Tooltip("Animation clip for the player slide. Used to retrieve the length.")]
    private AnimationClip slideAnimationClip;
    [SerializeField, Tooltip("The current speed of the player")]
    private float playerSpeed;
    [SerializeField, Tooltip("Used on each frame to calculate how much score to give the player.")]
    private float scoreMultiplier = 10f;


    private float gravity;
    private Vector3 movementDirection = Vector3.forward;
    private Vector3 playerVelocity;
    private PlayerInput playerInput;
    private InputAction turnAction;
    private InputAction jumpAction;
    private InputAction slideAction;

    private CharacterController controller;
    
     private int slidingAnimationId;
    private bool sliding = false;
    private float score = 0;

    [SerializeField]
    private UnityEvent<Vector3> turnEvent;
     [SerializeField, Tooltip("Called when the player loses the game.")]
    private UnityEvent<int> gameOverEvent;
    [SerializeField, Tooltip("Called on every frame when the score is updated.")]
    private UnityEvent<int> scoreUpdateEvent;



    private void Awake() {
        playerInput = GetComponent<PlayerInput>();
        controller = GetComponent<CharacterController>();
        slidingAnimationId = Animator.StringToHash("Sliding");
        turnAction = playerInput.actions["Turn"];
        jumpAction = playerInput.actions["Jump"];
        slideAction = playerInput.actions["Slide"];
    }

    private void OnEnable() {
        turnAction.performed += PlayerTurn;
        slideAction.performed += PlayerSlide;
        jumpAction.performed += PlayerJump;
    }

    private void OnDisable() {
        turnAction.performed -= PlayerTurn;
        slideAction.performed -= PlayerSlide;
        jumpAction.performed -= PlayerJump;
    }
    private void Start() {
        playerSpeed = initialPlayerSpeed;
        gravity = initialGravityValue;
    }

     private void PlayerTurn(InputAction.CallbackContext context) {
        Vector3? turnPosition = CheckTurn(context.ReadValue<float>());
        if (!turnPosition.HasValue) {
            GameOver();
            return;
        }
        Vector3 targetDirection = Quaternion.AngleAxis(90 * context.ReadValue<float>(), Vector3.up) * movementDirection;
        turnEvent.Invoke(targetDirection);
        Turn(context.ReadValue<float>(), turnPosition.Value);
    }

     private Vector3? CheckTurn(float turnValue) {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, .1f, turnLayer);
        if (hitColliders.Length != 0) {
            Tile tile = hitColliders[0].transform.parent.GetComponent<Tile>();
            TileType type = tile.type;
            if ((type == TileType.LEFT && turnValue == -1) ||
                (type == TileType.RIGHT && turnValue == 1) || 
                (type == TileType.SIDEWAYS)) {
                return tile.pivot.position;
            }
        }
        return null;
    }

     private void Turn(float turnValue, Vector3 turnPosition) {
        // Teleport the player to the pivot before turning.
        Vector3 tempPlayerPosition = new Vector3(turnPosition.x, transform.position.y, turnPosition.z);
        controller.enabled = false;
        transform.position = tempPlayerPosition;
        controller.enabled = true;

        // Rotate the player in the new direction.
        Quaternion targetRotation = transform.rotation * Quaternion.Euler(0, 90 * turnValue, 0);
        transform.rotation = targetRotation;
        movementDirection = transform.forward.normalized;
    }
      private void PlayerSlide(InputAction.CallbackContext context) {
        if (!sliding && IsGrounded()) {
            StartCoroutine(Slide());
        }
    }

      /// <summary>
    /// Slides the player by halving the size of the character controller collider and moving the center of the collider to the bottom of the character (to avoid having the character hit the obstacle). Plays the sliding animation and waits until it is finished. Once finished, the collider is reset.
    /// </summary>
    /// <returns>Coroutine.</returns>
    private IEnumerator Slide() {
        sliding = true;
        // Shrink the collider
        Vector3 originalControllerCenter = controller.center;
        Vector3 newControllerCenter = originalControllerCenter;
        controller.height /= 2;
        newControllerCenter.y -= controller.height / 2;
        controller.center = newControllerCenter;
        // Play the sliding animation
        animator.Play(slidingAnimationId);
        yield return new WaitForSeconds(slideAnimationClip.length / animator.speed);
        // Set the character controller collider back to normal after sliding.
        controller.height *= 2;
        controller.center = originalControllerCenter;
        sliding = false;
    }

        private void PlayerJump(InputAction.CallbackContext context) {
        if (IsGrounded()) {
            playerVelocity.y += Mathf.Sqrt(jumpHeight * gravity * -3f);
            controller.Move(playerVelocity * Time.deltaTime);
        }
    }
    private void Update() {
         // Check if the player fell from the map.
        if (!IsGrounded(20f)) {
            GameOver();
            return;
        }

        // Calculate the score for the player.
        score += scoreMultiplier * Time.deltaTime;
        scoreUpdateEvent.Invoke((int)score);


        controller.Move(transform.forward * playerSpeed * Time.deltaTime);

        if(IsGrounded() && playerVelocity.y < 0) {
            playerVelocity.y = 0f;
        }
        playerVelocity.y += gravity * Time.deltaTime;
        controller.Move(playerVelocity * Time.deltaTime	);
    }
    

    private bool IsGrounded(float length = .2f) {
        Vector3 raycastOriginFirst = transform.position;
        raycastOriginFirst.y -= controller.height / 2f;
        raycastOriginFirst.y += .1f;

        Vector3 raycastOriginSecond = raycastOriginFirst;
        raycastOriginFirst -= transform.forward * .2f;
        raycastOriginSecond += transform.forward * .2f;

      //  Debug.DrawLine(raycastOriginFirst, Vector3.down, Color.green, 2f);
      //  Debug.DrawLine(raycastOriginSecond, Vector3.down, Color.red, 2f);

        if(Physics.Raycast(raycastOriginFirst, Vector3.down, out RaycastHit hit, length, groundLayer) || Physics.
        Raycast(raycastOriginSecond, Vector3.down, out RaycastHit hit2, length, groundLayer)) {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Called when the player loses the game.
    /// </summary>
    private void GameOver() {
        Debug.Log("Game Over");
        gameOverEvent.Invoke((int)score);
        gameObject.SetActive(false);
    }

    /// Called when the player collides into something. This is a CharacterController included function.
    private void OnControllerColliderHit(ControllerColliderHit hit) {
        if (((1 << hit.collider.gameObject.layer) & obstacleLayer) != 0) {
            GameOver();
        }
    }
}

}
