using UnityEngine;

/// <summary>
/// Moves the object out along one axis and back to where it started, then
/// reports IsFinished. Each leg takes 5 / moveSpeed seconds.
/// </summary>
public class TargetMovement : MonoBehaviour
{
    public enum MovementType
    {
        Up = 0,
        Left = 1,
        Forward = 2
    }

    // Duration of one leg (out or back) at moveSpeed = 1.
    private const float LegDuration = 5f;

    [Tooltip("Time multiplier. 1 = 5 s out + 5 s back, 2 = twice as fast.")]
    public float moveSpeed = 1f;
    [Tooltip("How far the object travels before returning.")]
    public float moveDistance = 2f;
    public MovementType movementType = MovementType.Up;

    private Vector3 startPosition;
    private float timer;

    public bool IsFinished { get; private set; }

    void OnEnable()
    {
        startPosition = transform.position;
        timer = 0f;
        IsFinished = false;
    }

    void Update()
    {
        if (IsFinished)
        {
            return;
        }

        timer += Time.deltaTime * moveSpeed;

        if (timer >= LegDuration * 2f)
        {
            transform.position = startPosition;
            IsFinished = true;

            Debug.Log(
                gameObject.name +
                " finished movement and returned to original position."
            );
            return;
        }

        // 0 -> 1 during the outbound leg, 1 -> 0 on the way back.
        float progress = timer / LegDuration;
        float amount = progress <= 1f ? progress : 2f - progress;

        transform.position = startPosition + Axis() * (amount * moveDistance);
    }

    Vector3 Axis()
    {
        switch (movementType)
        {
            case MovementType.Left: return Vector3.left;
            case MovementType.Forward: return Vector3.forward;
            default: return Vector3.up;
        }
    }
}
