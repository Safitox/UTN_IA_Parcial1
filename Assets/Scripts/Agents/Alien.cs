using UnityEngine;
using System.Collections.Generic;

public class Alien : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float _health = 10f;
    [SerializeField] private float _respawnTime = 3f;
    private float _respawnTimer;
    private bool _isDead = false;
    private bool _isCollected = false;
    public bool IsDead => _isDead;
    public bool IsCollected => _isCollected;

    [SerializeField]
    private float _maxSpeed = 5f;
    [SerializeField]
    private float _maxSteeringForce = 5f;
    [SerializeField]
    private float _arriveRadius = 3f;
    [SerializeField]
    private float _stopRadius = .5f;
    [SerializeField]
    private float _viewRadius = 5f;

    [Header("Objects Of Interest")]
    [SerializeField] private float _objectOfInterestDamage = 1f;
    [SerializeField] private float _objectOfInterestDamageInterval = 1f;
    [SerializeField] private float _distSquaredToobjectOfInterestDetection = 1.5f;
    private float _objectOfInterestDamageTimer = 0f;

    [Header("Flocking")]
    [SerializeField]
    private float _separationRadius = 4f;
    [SerializeField, Range(0f, 3f)]
    private float _separationWeight = .8f;
    [SerializeField, Range(0f, 3f)]
    private float _cohesionWeight = 1f;
    [SerializeField, Range(0f, 3f)]
    private float _alignmentWeight = 1f;

    // Estados y referencias
    private ObjectOfInterest _currentTarget;
    private static readonly List<Alien> _allAgents = new();
    public static List<Alien> AllAgents => _allAgents;

    private Renderer[] _renderers;
    private Collider[] _colliders;
    private Vector3 _velocity;
    public Vector3 Velocity => _velocity;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _colliders = GetComponentsInChildren<Collider>();
    }

    private void Start()
    {
        Vector3 randomVector = new(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
        _velocity += randomVector.normalized * _maxSpeed;
    }
    private void Update()
    {
        if (_isDead)
        {
            ChangeVisualFeedback();
            if (_isCollected)
            {
                _respawnTimer += Time.deltaTime;

                if (_respawnTimer >= _respawnTime)
                {
                    Respawn();
                }
            }

            return;
        }
        _respawnTimer = 0f;

        HunterNPC hunter = GetHunter();
        bool hunterDetected =
            hunter != null &&
            InRange(hunter.transform.position, _viewRadius);

        if (hunterDetected)
        {
            _currentTarget = null;
            _objectOfInterestDamageTimer = 0f;

            _velocity += CalculateEvade(hunter);
        }
        else
        {
            _currentTarget = GetClosestObject();

            if (_currentTarget != null)
            {
                float distSqr = (
                    _currentTarget.transform.position -
                    transform.position
                ).sqrMagnitude;

                // Se acerca al morfi.
                _velocity += CalculateArrive(
                    _currentTarget.transform.position
                );

                // Mantengo separación respecto de los demás agentes mientras se acerca al morfi.
                _velocity += CalculateSeparation(
                    _allAgents,
                    _separationRadius
                ) * _separationWeight;

                if (distSqr < _distSquaredToobjectOfInterestDetection * _distSquaredToobjectOfInterestDetection)
                {
                    _objectOfInterestDamageTimer -= Time.deltaTime;

                    if (_objectOfInterestDamageTimer <= 0f)
                    {
                        _currentTarget.TakeDamage(_objectOfInterestDamage);
                        _objectOfInterestDamageTimer = _objectOfInterestDamageInterval;
                    }
                }
                else
                {
                    _objectOfInterestDamageTimer = 0f;
                }
            }
            else
            {
                CalculateFlocking();
            }
        }

        _velocity = Vector3.ClampMagnitude(_velocity, _maxSpeed);

        transform.position += _velocity * Time.deltaTime;
        transform.forward = _velocity;
        transform.position = Universe.Instance.CalculateLimitPosition(transform.position);
        //Vector3 newPosition = transform.position + _velocity * Time.deltaTime;

        //Universe.Instance.CalculateBounce(ref newPosition, ref _velocity);

        //transform.position = newPosition;

        //if (_velocity.sqrMagnitude > 0.001f)
        //{
        //    transform.forward = _velocity.normalized;
        //}

        ChangeVisualFeedback();
    }

    private Vector3 CalculateSteering(Vector3 desired)
    {
        Vector3 steering = desired - _velocity;
        steering = Vector3.ClampMagnitude(steering, _maxSteeringForce);
        return steering * Time.deltaTime;
    }


 
    private void CalculateFlocking()
    {
        Vector3 separationSum = Vector3.zero;
        Vector3 alignmentSum = Vector3.zero;
        Vector3 cohesionPositionSum = Vector3.zero;

        int separationCount = 0;
        int flockingCount = 0;

        float separationRadiusSqr = _separationRadius * _separationRadius;
        float viewRadiusSqr = _viewRadius * _viewRadius;

        foreach (Alien item in _allAgents)
        {
            if (item == null || item == this || item.IsDead) continue;

            Vector3 offset = item.transform.position - transform.position;
            float distanceSqr = offset.sqrMagnitude;

            if (distanceSqr <= separationRadiusSqr)
            {
                separationSum += offset;
                separationCount++;
            }

            if (distanceSqr <= viewRadiusSqr)
            {
                alignmentSum += item.Velocity;
                cohesionPositionSum += item.transform.position;
                flockingCount++;
            }
        }
        Vector3 separationSteering = Vector3.zero;
        Vector3 alignmentSteering = Vector3.zero;
        Vector3 cohesionSteering = Vector3.zero;

        if (separationCount > 0)
        {
            Vector3 averageSeparation = separationSum / separationCount;
            separationSteering = CalculateSteering(-averageSeparation.normalized * _maxSpeed);
        }

        if (flockingCount > 0)
        {
            Vector3 averageVelocity = alignmentSum / flockingCount;
            alignmentSteering = CalculateSteering(averageVelocity.normalized * _maxSpeed);

            Vector3 centerPosition = cohesionPositionSum / flockingCount;
            cohesionSteering = CalculateSeek(centerPosition);
        }

        _velocity += separationSteering * _separationWeight + alignmentSteering * _alignmentWeight + cohesionSteering * _cohesionWeight;
    }

    private Vector3 CalculateSeparation(IEnumerable<Alien> agents, float radius)
    {
        Vector3 desired = default;

        int count = 0;

        foreach (Alien item in agents)
        {
            if (item == this) continue;

            if (InRange(item.transform.position, radius))
            {
                desired += (item.transform.position - transform.position);
                count++;
            }
        }

        if (count == 0) return Vector3.zero;

        desired /= count;

        return CalculateSteering(-desired.normalized * _maxSpeed);
    }

    private bool InRange(Vector3 position, float radius) => (position - transform.position).sqrMagnitude <= radius * radius;



    private Vector3 CalculateSeek(Vector3 targetPosition)
    {
        Vector3 desired = (targetPosition - transform.position).normalized;
        desired *= _maxSpeed;

        return CalculateSteering(desired);
    }

    private Vector3 CalculateFlee(Vector3 targetPosition)
    {
        Vector3 desired = (targetPosition - transform.position).normalized;
        desired *= _maxSpeed;

        return CalculateSteering(-desired);
    }

    private Vector3 CalculateArrive(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - transform.position;
        float distance = toTarget.magnitude;

        if (distance < _stopRadius)
        {
            _velocity *= 0.8f;
            if (_velocity.magnitude < 0.05f)
                _velocity = Vector3.zero;

            return Vector3.zero;
        }

        float speed = _maxSpeed;

        if (distance < _arriveRadius)
        {
            float t = distance / _arriveRadius;

            speed *= t * t;
        }

        Vector3 desired = toTarget.normalized * speed;
        return CalculateSteering(desired);
    }

    private Vector3 CalculateEvade(HunterNPC target)
    {
        return CalculateFlee(GetFuturePosition(target));
    }

    private Vector3 GetFuturePosition(HunterNPC target)
    {
        float distanceToTarget = (target.transform.position - transform.position).magnitude;
        float predictedTime = distanceToTarget / (_maxSpeed + target.Velocity.magnitude);

        return target.transform.position + target.Velocity * predictedTime;
    }

    private void OnEnable()
    {
        _allAgents.Add(this);
    }

    private void OnDisable()
    {
        _allAgents.Remove(this);
    }


    public void TakeDamage(float dmg)
    {
        if (_isDead) return;

        _health -= dmg;

        if (_health <= 0)
        {
            _velocity = Vector3.zero;
            _respawnTimer = 0;
            _isDead = true;
        }
    }

    public void Collect()
    {
        if (!_isDead || _isCollected)
            return;

        _isCollected = true;
        _respawnTimer = 0f;
        _velocity = Vector3.zero;

        foreach (Renderer rend in _renderers)
        {
            rend.enabled = false;
        }

        foreach (Collider col in _colliders)
        {
            col.enabled = false;
        }
    }

    private ObjectOfInterest GetClosestObject()
    {
        ObjectOfInterest closest = null;
        float minDistSqr = _viewRadius * _viewRadius;

        foreach (ObjectOfInterest obj in ObjectsOfInterestManager.Items)
        {
            if (obj == null)
                continue;

            float distSqr = (obj.transform.position - transform.position).sqrMagnitude;

            if (distSqr < minDistSqr)
            {
                minDistSqr = distSqr;
                closest = obj;
            }
        }

        return closest;
    }
    private HunterNPC GetHunter()
    {
        return HunterNPC.Instance;
    }

    private void Respawn()
    {
        _isDead = false;
        _isCollected = false;
        _health = 10f;
        _respawnTimer = 0f;

        Vector3 randomPos = Universe.Instance.CalculateLimitPosition(new Vector3(Random.Range(-10f, 10f), transform.position.y,  Random.Range(-10f, 10f)));
        transform.position = randomPos;

        _velocity = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * _maxSpeed;
        foreach (Renderer rend in _renderers)
        {
            rend.enabled = true;
        }

        foreach (Collider col in _colliders)
        {
            col.enabled = true;
        }
    }

    private void ChangeVisualFeedback()
    {
        HunterNPC hunter = GetHunter();

        if (_isDead)
        {
            SetColor(Color.black);
            return;
        }

        if (_currentTarget != null)
        {
            SetColor(Color.green);
            return;
        }
        else if (hunter != null && InRange(hunter.transform.position, _viewRadius))
        {
            SetColor(Color.red);
            return;
        }
        else
        {
            SetColor(Color.blue);
            return;
        }
    }

    private void SetColor(Color color)
    {
        foreach (var rend in _renderers)
        {
            rend.material.color = color;
        }
    }
}