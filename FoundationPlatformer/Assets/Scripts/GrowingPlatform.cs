using UnityEngine;

/// <summary>
/// Attach to any platform. Each bullet hit grows the platform in the direction
/// the bullet was travelling (i.e. the direction the player was looking).
/// The platform's near face (closest to the shooter) stays anchored while the
/// far face extends outward. Growth smoothly animates with a small punch effect.
/// </summary>
public class GrowingPlatform : MonoBehaviour, IBulletHittable
{
    [Header("Growth")]
    [Tooltip("Maximum growth added per bullet hit on each local axis.")]
    [SerializeField] private Vector3 growAmount = new Vector3(0.5f, 0f, 0.5f);
    [Tooltip("Maximum scale on any axis (0 = no limit).")]
    [SerializeField] private Vector3 maxScale = new Vector3(10f, 1f, 10f);
    [Tooltip("How smoothly the platform lerps to its target scale and position.")]
    [SerializeField] private float growSmoothSpeed = 8f;

    [Header("Punch Effect")]
    [Tooltip("A quick over-shoot on hit for a satisfying bounce feel.")]
    [SerializeField] private float punchStrength = 0.15f;
    [Tooltip("How fast the punch settles back.")]
    [SerializeField] private float punchDecay = 10f;

    [Header("Optional")]
    [Tooltip("If set, the bottom face stays in place as the platform grows vertically.")]
    [SerializeField] private bool anchorToBottom = true;

    private Vector3 _initialScale;
    private Vector3 _initialLocalPosition;
    private Vector3 _targetScale;
    private Vector3 _punchOffset;

    // Accumulated local-space XZ offset so the platform's centre shifts as it grows
    // directionally.  Net effect: the face nearest the shooter stays put while the
    // opposite face extends outward in the bullet's travel direction.
    private Vector3 _localPositionOffset;

    private int _hitCount;

    private void Awake()
    {
        _initialScale         = transform.localScale;
        _initialLocalPosition = transform.localPosition;
        _targetScale          = _initialScale;
    }

    // -----------------------------------------------------------------------
    //  IBulletHittable
    // -----------------------------------------------------------------------

    public void OnBulletHit(Bullet bullet)
    {
        _hitCount++;

        // --- Determine growth direction in this platform's local space ---
        // Flatten the bullet's world-space direction onto the platform's local XZ
        // plane so that only lateral spread is considered (not vertical).
        Vector3 localDir = transform.InverseTransformDirection(bullet.Direction);
        localDir.y = 0f;
        if (localDir.sqrMagnitude < 0.001f)
            localDir = Vector3.forward; // safe fallback
        localDir.Normalize();

        // Weight the grow amount by how much the bullet direction aligns with each axis.
        // A bullet flying straight along local-X grows mostly on X; one at 45° grows
        // equally on both axes.
        Vector3 scaleDelta = new Vector3(
            growAmount.x * Mathf.Abs(localDir.x),
            growAmount.y,               // Y (height) grows uniformly if non-zero
            growAmount.z * Mathf.Abs(localDir.z)
        );

        // --- Apply scale (clamped) ---
        Vector3 proposed = _targetScale + scaleDelta;
        _targetScale = new Vector3(
            maxScale.x > 0f ? Mathf.Min(proposed.x, maxScale.x) : proposed.x,
            maxScale.y > 0f ? Mathf.Min(proposed.y, maxScale.y) : proposed.y,
            maxScale.z > 0f ? Mathf.Min(proposed.z, maxScale.z) : proposed.z
        );

        // --- Shift the centre so only the far face moves ---
        // When a platform grows by `d` on one axis its centre normally moves by d/2
        // in both directions.  By offsetting the position by +d/2 in the bullet's
        // direction, the near face (facing the shooter) stays fixed.
        _localPositionOffset += new Vector3(
            scaleDelta.x * 0.5f * Mathf.Sign(localDir.x),
            0f,
            scaleDelta.z * 0.5f * Mathf.Sign(localDir.z)
        );

        // Add punch so growth feels snappy even when already near the maximum
        _punchOffset = scaleDelta.normalized * punchStrength;
    }

    // -----------------------------------------------------------------------
    //  Smooth animation
    // -----------------------------------------------------------------------

    private void Update()
    {
        // Decay punch toward zero
        _punchOffset = Vector3.Lerp(_punchOffset, Vector3.zero, punchDecay * Time.deltaTime);

        // Lerp scale toward target + punch
        Vector3 desiredScale = _targetScale + _punchOffset;
        transform.localScale = Vector3.Lerp(transform.localScale, desiredScale, growSmoothSpeed * Time.deltaTime);

        // Build the target local position:
        //   start from initial position + directional XZ offset
        Vector3 targetLocalPos = _initialLocalPosition + _localPositionOffset;

        // If anchoring to the bottom, compensate for vertical scale change
        if (anchorToBottom)
        {
            float scaleYDelta = transform.localScale.y - _initialScale.y;
            targetLocalPos.y += scaleYDelta * 0.5f;
        }

        transform.localPosition = Vector3.Lerp(
            transform.localPosition, targetLocalPos, growSmoothSpeed * Time.deltaTime);
    }

    // -----------------------------------------------------------------------
    //  Public helpers
    // -----------------------------------------------------------------------

    /// <summary>Instantly snap back to the original size (e.g. on level reset).</summary>
    public void ResetSize()
    {
        _targetScale         = _initialScale;
        _punchOffset         = Vector3.zero;
        _localPositionOffset = Vector3.zero;
        _hitCount            = 0;

        transform.localScale    = _initialScale;
        transform.localPosition = _initialLocalPosition;
    }

    /// <summary>How many times this platform has been hit.</summary>
    public int HitCount => _hitCount;
}
