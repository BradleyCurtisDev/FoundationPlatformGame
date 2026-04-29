using System;
using UnityEngine;

/// <summary>
/// Attach this to your bullet prefab.
/// The bullet moves forward, optionally uses gravity, and returns itself
/// to the object pool when it hits something or its lifetime expires.
/// </summary>
public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    [SerializeField] private float speed = 40f;
    [SerializeField] private float lifetime = 4f;           // Auto-return after this many seconds
    [SerializeField] private bool useGravity = false;       // Arc the bullet downward over time
    [SerializeField] private float gravityScale = 9.81f;

    [Header("Impact")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private LayerMask hitLayers = ~0;      // What layers this bullet can hit

    // Assigned by PlayerShoot via SetPool()
    private Action<Bullet> _returnToPool;

    private Vector3 _direction;
    private Vector3 _velocity;
    private float _spawnTime;
    private bool _active;

    /// <summary>The normalised travel direction the bullet was fired in.</summary>
    public Vector3 Direction => _direction;

    // -----------------------------------------------------------------------
    //  Public API called by PlayerShoot
    // -----------------------------------------------------------------------

    /// <summary>Give the bullet a reference back to the pool so it can return itself.</summary>
    public void SetPool(Action<Bullet> returnCallback)
    {
        _returnToPool = returnCallback;
    }

    /// <summary>Activate the bullet and send it flying.</summary>
    public void Launch(Vector3 position, Vector3 direction)
    {
        transform.position = position;
        transform.rotation = Quaternion.LookRotation(direction);

        _direction = direction.normalized;
        _velocity  = _direction * speed;
        _spawnTime = Time.time;
        _active    = true;

        gameObject.SetActive(true);
    }

    // -----------------------------------------------------------------------
    //  Movement & lifetime
    // -----------------------------------------------------------------------

    private void Update()
    {
        if (!_active) return;

        // Lifetime expiry
        if (Time.time - _spawnTime >= lifetime)
        {
            Deactivate();
            return;
        }

        // Optional gravity arc
        if (useGravity)
            _velocity.y -= gravityScale * Time.deltaTime;

        // Sweep move — use Physics.Raycast so fast bullets don't tunnel through thin walls
        float stepDist = _velocity.magnitude * Time.deltaTime;
        if (Physics.Raycast(transform.position, _velocity.normalized, out RaycastHit hit, stepDist, hitLayers, QueryTriggerInteraction.Ignore))
        {
            OnHit(hit);
            return;
        }

        transform.position += _velocity * Time.deltaTime;
        transform.rotation  = Quaternion.LookRotation(_velocity); // Keep oriented with arc
    }

    // -----------------------------------------------------------------------
    //  Collision fallback for slower / larger bullets
    // -----------------------------------------------------------------------

    private void OnTriggerEnter(Collider other)
    {
        if (!_active) return;
        if ((hitLayers.value & (1 << other.gameObject.layer)) == 0) return;

        NotifyHit(other.gameObject);
        Deactivate();
    }

    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    private void OnHit(RaycastHit hit)
    {
        // Move to exact impact point so VFX / decals can use transform.position
        transform.position = hit.point;

        NotifyHit(hit.collider.gameObject);
        Deactivate();
    }

    /// <summary>
    /// Calls every hit-response interface on the target so multiple behaviours
    /// (damage, growing, etc.) can all react independently.
    /// </summary>
    private void NotifyHit(GameObject target)
    {
        target.GetComponent<IDamageable>()?.TakeDamage(damage);
        target.GetComponent<IBulletHittable>()?.OnBulletHit(this);
    }

    private void Deactivate()
    {
        _active = false;
        _returnToPool?.Invoke(this);
    }
}

/// <summary>
/// Implement on any MonoBehaviour that should take damage from bullets.
/// e.g.  public class Enemy : MonoBehaviour, IDamageable { public void TakeDamage(float dmg) { ... } }
/// </summary>
public interface IDamageable
{
    void TakeDamage(float damage);
}

/// <summary>
/// Implement on any MonoBehaviour that should react to a bullet hit without taking damage.
/// e.g. growing platforms, switches, destructible props, etc.
/// </summary>
public interface IBulletHittable
{
    void OnBulletHit(Bullet bullet);
}
