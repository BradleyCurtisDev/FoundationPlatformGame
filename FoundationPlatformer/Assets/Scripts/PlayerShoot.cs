using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerShoot : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Assign your bullet prefab here. It must have a Bullet component on it.")]
    [SerializeField] private GameObject bulletPrefab;
    [Tooltip("The camera transform used to aim. Auto-found if left empty.")]
    [SerializeField] private Transform cameraTransform;
    [Tooltip("The point bullets spawn from. Defaults to the camera position if left empty.")]
    [SerializeField] private Transform muzzlePoint;

    [Header("Shoot Settings")]
    [SerializeField] private float fireRate = 0.15f;       // Seconds between shots (0 = no limit)
    [SerializeField] private bool holdToFire = false;       // True = hold LMB, False = click per shot

    [Header("Object Pool")]
    [SerializeField] private int poolSize = 20;             // How many bullets to pre-allocate

    // Input
    private InputAction _shootAction;

    // Pool
    private readonly Queue<Bullet> _pool = new Queue<Bullet>();
    private float _nextFireTime;

    private void Awake()
    {
        _shootAction = new InputAction("Shoot", binding: "<Mouse>/leftButton");

        if (cameraTransform == null)
            cameraTransform = GetComponentInChildren<Camera>()?.transform;

        if (muzzlePoint == null)
            muzzlePoint = cameraTransform;
    }

    private void Start()
    {
        WarmPool();
    }

    private void OnEnable()
    {
        _shootAction.Enable();

        if (!holdToFire)
            _shootAction.performed += OnShootPerformed;
    }

    private void OnDisable()
    {
        if (!holdToFire)
            _shootAction.performed -= OnShootPerformed;

        _shootAction.Disable();
    }

    private void Update()
    {
        if (holdToFire && _shootAction.ReadValue<float>() > 0.5f)
            TryFire();
    }

    // -----------------------------------------------------------------------
    //  Pool
    // -----------------------------------------------------------------------

    private void WarmPool()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("PlayerShoot: No bullet prefab assigned!");
            return;
        }

        for (int i = 0; i < poolSize; i++)
        {
            Bullet b = CreateBullet();
            b.gameObject.SetActive(false);
            _pool.Enqueue(b);
        }
    }

    private Bullet CreateBullet()
    {
        GameObject go = Instantiate(bulletPrefab);
        Bullet b = go.GetComponent<Bullet>();

        if (b == null)
        {
            Debug.LogError("PlayerShoot: bulletPrefab is missing a Bullet component!", bulletPrefab);
            b = go.AddComponent<Bullet>();
        }

        b.SetPool(ReturnToPool);
        return b;
    }

    /// <summary>Grab a bullet from the pool (or create a new one if the pool is empty).</summary>
    private Bullet GetFromPool()
    {
        if (_pool.Count > 0)
            return _pool.Dequeue();

        // Pool exhausted — grow it rather than dropping shots
        Bullet b = CreateBullet();
        return b;
    }

    private void ReturnToPool(Bullet bullet)
    {
        bullet.gameObject.SetActive(false);
        _pool.Enqueue(bullet);
    }

    // -----------------------------------------------------------------------
    //  Firing
    // -----------------------------------------------------------------------

    private void OnShootPerformed(InputAction.CallbackContext ctx) => TryFire();

    private void TryFire()
    {
        if (Time.time < _nextFireTime) return;
        if (bulletPrefab == null) return;

        _nextFireTime = Time.time + fireRate;

        Bullet bullet = GetFromPool();

        Vector3 spawnPos = muzzlePoint != null ? muzzlePoint.position : transform.position;
        Vector3 direction = cameraTransform != null ? cameraTransform.forward : transform.forward;

        bullet.Launch(spawnPos, direction);
    }
}
