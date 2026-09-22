using UnityEngine;

/// <summary>
/// Drives the character's head and eyes through a sequence of gaze targets.
/// Targets are visited in name order. Whenever there is no target to follow
/// (before the sequence starts and after it ends) the character idles using
/// the IdleGaze settings: neutral holds, looking around, looking away,
/// eye contact, sway, micro-saccades and blinking.
/// </summary>
public class GazeController : MonoBehaviour
{
    private enum State { Tracking, Pausing, Idle }

    [Header("Targets")]
    public string targetTag = "GazeTarget";

    [Header("Body Parts")]
    public Transform head;
    public Transform leftEye;
    public Transform rightEye;

    [Header("Tracking")]
    public float headSpeed = 3f;
    public float eyeSpeed = 5f;
    public float pauseTime = 1f;

    [Header("Idle")]
    [Tooltip("Seconds to idle before the first target. 0 starts tracking immediately.")]
    public float startIdleDuration = 3f;
    [Tooltip("Restart the target sequence after idling for Idle Duration seconds.")]
    public bool loopSequence = false;
    public float idleDuration = 5f;
    public IdleGaze idle = new IdleGaze();

    private GameObject[] gazeTargets;
    private TargetMovement[] movements;
    private int currentTarget;

    private State state;
    private float pauseTimer;
    private float idleTimer;
    private bool sequenceStarted;

    void Start()
    {
        if (head == null || leftEye == null || rightEye == null)
        {
            Debug.LogError("GazeController: head, leftEye and rightEye must be assigned.", this);
            enabled = false;
            return;
        }

        idle.Init(head, leftEye, rightEye);

        gazeTargets = GameObject.FindGameObjectsWithTag(targetTag);

        // FindGameObjectsWithTag has no guaranteed order, so sort for repeatable runs.
        System.Array.Sort(gazeTargets, (a, b) => string.CompareOrdinal(a.name, b.name));

        movements = new TargetMovement[gazeTargets.Length];
        for (int i = 0; i < gazeTargets.Length; i++)
        {
            movements[i] = gazeTargets[i].GetComponent<TargetMovement>();

            if (movements[i] != null)
            {
                movements[i].enabled = false;
            }
        }

        Debug.Log("Total targets found: " + gazeTargets.Length);

        currentTarget = 0;
        if (gazeTargets.Length > 0 && startIdleDuration <= 0f)
        {
            sequenceStarted = true;
            BeginCurrentTarget();
        }
        else
        {
            EnterIdle();
        }
    }

    // LateUpdate so an Animator on the character can't overwrite the rotations.
    void LateUpdate()
    {
        idle.TickBlink();

        switch (state)
        {
            case State.Tracking:
                TrackCurrentTarget();

                // A target without TargetMovement has nothing to wait for.
                if (movements[currentTarget] == null || movements[currentTarget].IsFinished)
                {
                    state = State.Pausing;
                    pauseTimer = 0f;

                    Debug.Log(
                        "Target finished: " +
                        gazeTargets[currentTarget].name +
                        " - " + pauseTime + " second pause"
                    );
                }
                break;

            case State.Pausing:
                TrackCurrentTarget();

                pauseTimer += Time.deltaTime;
                if (pauseTimer >= pauseTime)
                {
                    GoToNextTarget();
                }
                break;

            case State.Idle:
                idle.Tick();

                idleTimer += Time.deltaTime;

                float wait = sequenceStarted ? idleDuration : startIdleDuration;
                bool mayStart = gazeTargets.Length > 0 && (!sequenceStarted || loopSequence);

                if (mayStart && idleTimer >= wait)
                {
                    sequenceStarted = true;
                    currentTarget = 0;
                    BeginCurrentTarget();
                }
                break;
        }
    }

    void TrackCurrentTarget()
    {
        Vector3 point = gazeTargets[currentTarget].transform.position;

        AimAt(head, point, headSpeed);
        AimAt(leftEye, point, eyeSpeed);
        AimAt(rightEye, point, eyeSpeed);
    }

    void AimAt(Transform bone, Vector3 point, float speed)
    {
        Vector3 direction = point - bone.position;

        // LookRotation logs a warning and misbehaves on a zero vector.
        if (direction.sqrMagnitude < 1e-6f)
        {
            return;
        }

        bone.rotation = Quaternion.Slerp(
            bone.rotation,
            Quaternion.LookRotation(direction, Vector3.up),
            IdleGaze.Smoothing(speed)
        );
    }

    void BeginCurrentTarget()
    {
        state = State.Tracking;

        if (movements[currentTarget] != null)
        {
            movements[currentTarget].enabled = true;
        }

        Debug.Log("Now looking at: " + gazeTargets[currentTarget].name);
    }

    void GoToNextTarget()
    {
        if (movements[currentTarget] != null)
        {
            movements[currentTarget].enabled = false;
        }

        currentTarget++;

        if (currentTarget >= gazeTargets.Length)
        {
            Debug.Log("ALL " + gazeTargets.Length + " TARGETS COMPLETED!");
            EnterIdle();
            return;
        }

        BeginCurrentTarget();
    }

    void EnterIdle()
    {
        state = State.Idle;
        idleTimer = 0f;
        idle.Enter();

        Debug.Log("Idle");
    }
}
