using UnityEngine;

/// <summary>
/// Idle gaze for a virtual human: what the head and eyes do when no target
/// needs tracking. A random mix of neutral holds, looking around, looking
/// slightly away and eye contact, each held for a natural-length interval,
/// with head sway and eye micro-saccades layered on top. Blinking runs
/// independently so it can also be used while tracking.
///
/// Directions are built from the head's rest pose using world-space rotations,
/// so they don't depend on the bones' local axis conventions (only that +Z
/// points forward, as GazeController already assumes).
/// </summary>
[System.Serializable]
public class IdleGaze
{
    private enum Behaviour { Neutral, LookAround, LookAway, EyeContact }

    [Header("Behaviour Mix (relative weights)")]
    public float neutralWeight = 3f;
    public float lookAroundWeight = 3f;
    public float lookAwayWeight = 1.5f;
    public float eyeContactWeight = 2f;

    [Header("Hold Time (min, max seconds)")]
    public Vector2 neutralDuration = new Vector2(1f, 3f);
    public Vector2 lookAroundDuration = new Vector2(1f, 2.5f);
    public Vector2 lookAwayDuration = new Vector2(0.8f, 2f);
    public Vector2 eyeContactDuration = new Vector2(1.5f, 4f);

    [Header("Gaze Range (degrees)")]
    public float lookAroundYaw = 30f;
    public float lookAroundPitch = 12f;
    [Tooltip("Looking slightly away is sideways and downward.")]
    public float lookAwayYaw = 20f;
    public float lookAwayPitch = 12f;
    [Tooltip("Largest angle from the rest pose the character will turn to meet the target's gaze.")]
    public float maxEyeContactAngle = 60f;
    [Tooltip("Share of a gaze shift done by the head; the eyes cover the rest.")]
    [Range(0f, 1f)] public float headShare = 0.5f;

    [Header("Eye Contact")]
    [Tooltip("What to make eye contact with. Falls back to the Main Camera.")]
    public Transform eyeContactTarget;

    [Header("Speeds")]
    public float headSpeed = 2f;
    public float eyeSpeed = 10f;

    [Header("Micro Movement")]
    [Tooltip("Max head sway in degrees.")]
    public float headSwayAngle = 1.5f;
    public float headSwaySpeed = 0.3f;
    [Tooltip("Max eye micro-saccade offset in degrees.")]
    public float saccadeAngle = 1.5f;
    public Vector2 saccadeInterval = new Vector2(0.4f, 1.6f);

    [Header("Blinking")]
    public bool blink = true;
    public Vector2 blinkInterval = new Vector2(2f, 6f);
    public float blinkDuration = 0.15f;
    [Range(0f, 1f)] public float doubleBlinkChance = 0.15f;
    [Tooltip("Chance of a blink when the gaze moves to a new spot.")]
    [Range(0f, 1f)] public float blinkOnGazeShiftChance = 0.5f;
    [Tooltip("Eye height, as a fraction of open height, when fully closed.")]
    [Range(0f, 1f)] public float eyeClosedScale = 0.05f;

    private const float DefaultGazeDistance = 5f;

    private Transform head;
    private Transform leftEye;
    private Transform rightEye;

    private Quaternion headRest;
    private Quaternion leftEyeRest;
    private Quaternion rightEyeRest;
    private Vector3 leftEyeOpenScale;
    private Vector3 rightEyeOpenScale;

    private Behaviour behaviour;
    private float behaviourTimer;
    private float gazeYaw;
    private float gazePitch;

    private float saccadeTimer;
    private float saccadeYaw;
    private float saccadePitch;
    private float noiseSeed;

    private bool blinking;
    private float blinkProgress;
    private float blinkTimer;
    private int blinksRemaining;

    // Frame-rate independent replacement for Slerp(a, b, dt * speed).
    public static float Smoothing(float speed)
    {
        return 1f - Mathf.Exp(-speed * Time.deltaTime);
    }

    public void Init(Transform head, Transform leftEye, Transform rightEye)
    {
        this.head = head;
        this.leftEye = leftEye;
        this.rightEye = rightEye;

        headRest = head.localRotation;
        leftEyeRest = leftEye.localRotation;
        rightEyeRest = rightEye.localRotation;
        leftEyeOpenScale = leftEye.localScale;
        rightEyeOpenScale = rightEye.localScale;

        noiseSeed = Random.value * 100f;

        if (eyeContactTarget == null && Camera.main != null)
        {
            eyeContactTarget = Camera.main.transform;
        }

        ScheduleBlink();
    }

    // Called when idling begins: settle to the neutral pose first.
    public void Enter()
    {
        StartBehaviour(Behaviour.Neutral);
        saccadeTimer = 0f;
    }

    public void Tick()
    {
        behaviourTimer -= Time.deltaTime;
        if (behaviourTimer <= 0f)
        {
            StartBehaviour(PickNextBehaviour());
        }

        UpdateSaccade();
        ApplyGaze();
    }

    // Runs in every state so the character blinks while tracking too.
    public void TickBlink()
    {
        if (!blink)
        {
            SetEyeClosure(0f);
            return;
        }

        if (blinking)
        {
            blinkProgress += Time.deltaTime / Mathf.Max(blinkDuration, 0.01f);

            if (blinkProgress >= 1f)
            {
                blinking = false;
                SetEyeClosure(0f);

                if (blinksRemaining > 0)
                {
                    blinkTimer = 0.1f; // short gap before the second blink
                }
                else
                {
                    ScheduleBlink();
                }
            }
            else
            {
                // Quick close (40%), slower open (60%).
                float closure = blinkProgress < 0.4f
                    ? blinkProgress / 0.4f
                    : 1f - (blinkProgress - 0.4f) / 0.6f;
                SetEyeClosure(closure);
            }
        }
        else
        {
            blinkTimer -= Time.deltaTime;
            if (blinkTimer <= 0f)
            {
                if (blinksRemaining == 0)
                {
                    blinksRemaining = Random.value < doubleBlinkChance ? 2 : 1;
                }

                blinksRemaining--;
                blinking = true;
                blinkProgress = 0f;
            }
        }
    }

    void ScheduleBlink()
    {
        blinkTimer = Random.Range(blinkInterval.x, blinkInterval.y);
        blinksRemaining = 0;
    }

    void SetEyeClosure(float closure)
    {
        float k = Mathf.Lerp(1f, eyeClosedScale, closure);

        leftEye.localScale = new Vector3(
            leftEyeOpenScale.x, leftEyeOpenScale.y * k, leftEyeOpenScale.z);
        rightEye.localScale = new Vector3(
            rightEyeOpenScale.x, rightEyeOpenScale.y * k, rightEyeOpenScale.z);
    }

    Behaviour PickNextBehaviour()
    {
        bool canContact = eyeContactTarget != null;

        // Never repeat the same excursion twice in a row.
        float wNeutral = neutralWeight;
        float wAround = behaviour == Behaviour.LookAround ? 0f : lookAroundWeight;
        float wAway = behaviour == Behaviour.LookAway ? 0f : lookAwayWeight;
        float wContact = !canContact || behaviour == Behaviour.EyeContact ? 0f : eyeContactWeight;

        float total = wNeutral + wAround + wAway + wContact;
        if (total <= 0f)
        {
            return Behaviour.Neutral;
        }

        float roll = Random.value * total;
        if ((roll -= wNeutral) < 0f) return Behaviour.Neutral;
        if ((roll -= wAround) < 0f) return Behaviour.LookAround;
        if ((roll -= wAway) < 0f) return Behaviour.LookAway;
        return wContact > 0f ? Behaviour.EyeContact : Behaviour.Neutral;
    }

    void StartBehaviour(Behaviour next)
    {
        behaviour = next;
        gazeYaw = 0f;
        gazePitch = 0f;

        Vector2 duration;
        switch (next)
        {
            case Behaviour.LookAround:
                gazeYaw = Random.Range(-lookAroundYaw, lookAroundYaw);
                gazePitch = Random.Range(-lookAroundPitch, lookAroundPitch);
                duration = lookAroundDuration;
                break;

            case Behaviour.LookAway:
                gazeYaw = Random.Range(0.5f, 1f) * lookAwayYaw * (Random.value < 0.5f ? -1f : 1f);
                gazePitch = -Random.Range(0.3f, 1f) * lookAwayPitch;
                duration = lookAwayDuration;
                break;

            case Behaviour.EyeContact:
                duration = eyeContactDuration;
                break;

            default:
                duration = neutralDuration;
                break;
        }

        behaviourTimer = Random.Range(duration.x, duration.y);

        // People often blink as their gaze jumps somewhere new.
        if (next != Behaviour.Neutral && blink && !blinking
            && Random.value < blinkOnGazeShiftChance)
        {
            blinksRemaining = 1;
            blinkTimer = 0f;
        }
    }

    void UpdateSaccade()
    {
        saccadeTimer -= Time.deltaTime;
        if (saccadeTimer <= 0f)
        {
            saccadeYaw = Random.Range(-saccadeAngle, saccadeAngle);
            saccadePitch = Random.Range(-saccadeAngle, saccadeAngle);
            saccadeTimer = Random.Range(saccadeInterval.x, saccadeInterval.y);
        }
    }

    void ApplyGaze()
    {
        Quaternion parentRotation = head.parent != null ? head.parent.rotation : Quaternion.identity;
        Quaternion headRestWorld = parentRotation * headRest;
        Vector3 restForward = headRestWorld * Vector3.forward;

        // Where the character wants to look, and how far away that is.
        Vector3 gazeDirection;
        float gazeDistance = DefaultGazeDistance;

        if (behaviour == Behaviour.EyeContact && eyeContactTarget != null)
        {
            Vector3 toTarget = eyeContactTarget.position - head.position;
            gazeDistance = Mathf.Max(toTarget.magnitude, 1f);

            gazeDirection = toTarget.sqrMagnitude > 1e-6f
                ? Vector3.RotateTowards(restForward, toTarget.normalized,
                    maxEyeContactAngle * Mathf.Deg2Rad, 0f)
                : restForward;
        }
        else
        {
            gazeDirection = Offset(restForward, gazeYaw, gazePitch);
        }

        // Head takes its share of the shift, plus slow Perlin-noise sway.
        float t = Time.time * headSwaySpeed;
        float swayPitch = (Mathf.PerlinNoise(noiseSeed, t) - 0.5f) * 2f * headSwayAngle;
        float swayYaw = (Mathf.PerlinNoise(noiseSeed + 50f, t) - 0.5f) * 2f * headSwayAngle;

        Vector3 headDirection = Offset(
            Vector3.Slerp(restForward, gazeDirection, headShare), swayYaw, swayPitch);

        head.rotation = Quaternion.Slerp(
            head.rotation,
            Quaternion.FromToRotation(restForward, headDirection) * headRestWorld,
            Smoothing(headSpeed)
        );

        // Both eyes aim at one point, so they converge on near targets.
        Vector3 gazePoint = head.position
            + Offset(gazeDirection, saccadeYaw, saccadePitch) * gazeDistance;

        AimEye(leftEye, leftEyeRest, gazePoint);
        AimEye(rightEye, rightEyeRest, gazePoint);
    }

    void AimEye(Transform eye, Quaternion restLocal, Vector3 point)
    {
        Vector3 direction = point - eye.position;
        if (direction.sqrMagnitude < 1e-6f)
        {
            return;
        }

        Quaternion parentRotation = eye.parent != null ? eye.parent.rotation : Quaternion.identity;
        Quaternion restWorld = parentRotation * restLocal;

        eye.rotation = Quaternion.Slerp(
            eye.rotation,
            Quaternion.FromToRotation(restWorld * Vector3.forward, direction) * restWorld,
            Smoothing(eyeSpeed)
        );
    }

    // Turns a direction by yaw (around world up) and pitch (positive = up).
    static Vector3 Offset(Vector3 forward, float yaw, float pitch)
    {
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (right.sqrMagnitude < 1e-6f)
        {
            return forward;
        }

        return Quaternion.AngleAxis(yaw, Vector3.up)
            * Quaternion.AngleAxis(-pitch, right.normalized)
            * forward;
    }
}
