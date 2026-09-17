using System.Collections.Generic;
using UnityEngine;
public class HunterNPC : MonoBehaviour
{
    // ATAQUE / DETECCIÓN
    [SerializeField] private float _tba = 3f;
    private float _attackCooldownTimer;
    public bool CanAttack => _attackCooldownTimer >= _tba;

    [SerializeField] private float _rangeAttackRadius = 6f;
    [SerializeField] private float _meleeRadius = 2f;
    [SerializeField] private float _viewRadius = 10f;
    public float RangeRadius => _rangeAttackRadius;
    public float MeleeRadius => _meleeRadius;
    public float ViewRadius => _viewRadius;

    // MOVIMIENTO
    [SerializeField]
    private float _speed = 3f;
    public float Speed => _speed;

    [SerializeField] private float _rotationSpeed = 100f;
    public float RotationSpeed => _rotationSpeed;

    public Vector3 Velocity { get; private set; }

    // SPAWN / OBJETOS DE INTERÉS
    [SerializeField] private GameObject _interestPrefab;
    [SerializeField] private float _spawnRadius = 10f;
    [SerializeField] private float _spawnInterval = 8f;
    private float _spawnTimer;
    private List<GameObject> _spawnedObjects = new();

    // PROYECTILES
    [SerializeField] private GameObject _bulletPrefab;
    [SerializeField] private Transform _spawnBulletPoint;
    [SerializeField] private float _bulletSpeed = 20f;
    public GameObject BulletPrefab => _bulletPrefab;
    public Transform SpawnBulletPoint => _spawnBulletPoint;
    public float BulletSpeed => _bulletSpeed;

    // FSM / DATOS
    [SerializeField]
    private PatrolData _patrolData;
    private readonly FiniteStateMachine _fsm = new();
    public FiniteStateMachine FSM => _fsm;

    // ESTADOS
    public PatrolState Patrol { get; private set; }
    public AttackState Attack { get; private set; }
    public GatherState Gather { get; private set; }

    // REFERENCIAS RUNTIME
    private Renderer[] _renderers;

    public static HunterNPC Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Ya existe un cazador en la escena. Solo puede haber uno. Como Highlander... pero sin cortarse cabezas");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        _renderers = GetComponentsInChildren<Renderer>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {

        Patrol = new(_patrolData, this);
        Attack = new(this);
        Gather = new(this);

        _fsm.AddState(Patrol);
        _fsm.AddState(Attack);
        _fsm.AddState(Gather);

        _fsm.ChangeState(Patrol);
    }

    private void Update()
    {
        if (_attackCooldownTimer < _tba)
        {
            _attackCooldownTimer += Time.deltaTime;
        }

        _fsm.Update();
        ChangeVisualFeedback();
    }

    public void ResetAttackCooldown()
    {
        _attackCooldownTimer = 0f;
    }

    public void SpawnObject()
    {
        //Solo si hay menos de 5 en la escena y si ha pasado el tiempo de spawn, se spawnea un objeto de interés.
        _spawnTimer += Time.deltaTime;

        if (_spawnTimer < _spawnInterval) return;

        if (_spawnedObjects.Count >= 5) return;

        _spawnTimer = 0;

        Vector3 randomPos = transform.position + new Vector3(Random.Range(-_spawnRadius, _spawnRadius), 0, Random.Range(-_spawnRadius, _spawnRadius));

        GameObject obj = Instantiate(_interestPrefab, randomPos, Quaternion.identity);
        _spawnedObjects.Add(obj);

        ObjectOfInterest objectOfInterestComponent = obj.GetComponent<ObjectOfInterest>();
        ObjectsOfInterestManager.AddItem(objectOfInterestComponent);
    }

    private void ChangeVisualFeedback()
    {
        HunterState current = _fsm.CurrentState;

        if (current is PatrolState)
        {
            SetColor(Color.white);
        }
        else if (current is AttackState)
        {
            SetColor(Color.red);
        }
        else if (current is GatherState)
        {
            SetColor(Color.cyan);
        }
    }

    private void SetColor(Color color)
    {
        foreach (var r in _renderers)
        {
            r.material.color = color;
        }
    }
}