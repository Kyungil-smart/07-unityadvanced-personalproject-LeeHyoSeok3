using UnityEngine;
using UnityEngine.AddressableAssets;

public class AnimalNode : ResourceNode
{
    // AnimalNodeData로 캐스팅해 동물 전용 수치를 읽는다
    private AnimalNodeData AnimalData => _data as AnimalNodeData;

    // 밸런싱 프로퍼티 (AnimalNodeData 우선, 없으면 기본값 폴백)
    private float WanderRadius       => AnimalData != null ? AnimalData.wanderRadius       : 3f;
    private float WanderIntervalMin  => AnimalData != null ? AnimalData.wanderIntervalMin  : 3f;
    private float WanderIntervalMax  => AnimalData != null ? AnimalData.wanderIntervalMax  : 6f;
    private float MoveSpeed          => AnimalData != null ? AnimalData.moveSpeed          : 1f;
    private float ArriveThreshold    => AnimalData != null ? AnimalData.arriveThreshold    : 0.08f;
    private float GrassIntervalMin   => AnimalData != null ? AnimalData.grassIntervalMin   : 6f;
    private float GrassIntervalMax   => AnimalData != null ? AnimalData.grassIntervalMax   : 12f;
    private float GrassAnimDuration  => AnimalData != null ? AnimalData.grassAnimDuration  : 1.2f;
    private int   HuntDamage         => AnimalData != null ? AnimalData.huntDamage         : 1;
    private int   MaxHp              => AnimalData != null ? AnimalData.maxHp              : 1;
    private float FleeSpeedMultiplier => AnimalData != null ? AnimalData.fleeSpeedMultiplier : 2.5f;
    private float DetectionRange     => AnimalData != null ? AnimalData.detectionRange     : 3f;

    [Header("피격 점멸 (시각 효과)")]
    public Color flashColor    = Color.red;
    public float flashDuration = 0.1f;

    [Header("Animator 파라미터 이름")]
    public string paramIsWalking    = "IsWalking";
    public string paramIsGrassing   = "IsGrassing";

    private enum AnimalState { Idle, Walk, Grass }
    private AnimalState _state = AnimalState.Idle;

    private Vector3     _originPosition;
    private Vector3     _targetPosition;
    private float       _wanderTimer;
    private float       _grassTimer;
    private float       _grassEndTimer;
    private bool        _isHarvesting;

    private Rigidbody2D _rb;
    private int         _currentHp;
    private bool        _isDead;
    private WorkerUnit  _huntingWorker;

    protected override void Awake()
    {
        base.Awake();
        resourceType    = ResourceType.Meat;
        _originPosition = transform.position;
        _targetPosition = transform.position;
        _rb         = GetComponent<Rigidbody2D>();
        _currentHp     = MaxHp;
        _originalScale = transform.localScale;

        if (_rb == null)
            Debug.LogWarning($"[AnimalNode] {gameObject.name} Rigidbody2D 없음!");

        ResetWanderTimer();
        ResetGrassTimer();
    }

    void Update()
    {
        if (!PhaseManager.Instance.IsPrepare) return;
        if (_isDead || IsDeplete) return;

        // 워커 감지 → 도망
        if (!_isHarvesting)
            DetectAndFlee();

        switch (_state)
        {
            case AnimalState.Idle:  UpdateIdle();  break;
            case AnimalState.Walk:  UpdateWalk();  break;
            case AnimalState.Grass: UpdateGrass(); break;
        }
    }

    void UpdateIdle()
    {
        _wanderTimer -= Time.deltaTime;
        _grassTimer  -= Time.deltaTime;

        if (_grassTimer <= 0f) { EnterGrass(); return; }
        if (_wanderTimer <= 0f)
        {
            Vector2 rand    = Random.insideUnitCircle * WanderRadius;
            _targetPosition = _originPosition + new Vector3(rand.x, rand.y, 0f);
            EnterWalk();
        }
    }

    // 벽 감지용
    private Vector2 _lastPosition;
    private float   _stuckTimer;
    private const float STUCK_CHECK_INTERVAL = 0.3f;  // 감지 주기 (초)
    private const float STUCK_THRESHOLD      = 0.02f; // 이 거리 이하면 막힌 것으로 판단

    void UpdateWalk()
    {
        float dist = Vector3.Distance(transform.position, _targetPosition);

        if (dist > ArriveThreshold)
        {
            Vector2 dir     = (_targetPosition - transform.position).normalized;
            Vector2 nextPos = _rb != null
                ? _rb.position + dir * MoveSpeed * Time.fixedDeltaTime
                : (Vector2)transform.position + dir * MoveSpeed * Time.deltaTime;

            if (_rb != null)
                _rb.MovePosition(nextPos);
            else
                transform.position = nextPos;

            FlipByDirection(_targetPosition.x - transform.position.x);

            // 벽 감지: 일정 시간 동안 거의 움직이지 않으면 새 목적지로 전환
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= STUCK_CHECK_INTERVAL)
            {
                float moved = Vector2.Distance(transform.position, _lastPosition);
                if (moved < STUCK_THRESHOLD)
                {
                    // 막힘 감지 → 새 목적지 선택 후 계속 Walk
                    Vector2 rand    = Random.insideUnitCircle * WanderRadius;
                    _targetPosition = _originPosition + new Vector3(rand.x, rand.y, 0f);
                }
                _lastPosition = transform.position;
                _stuckTimer   = 0f;
            }
        }
        else
        {
            EnterIdle();
        }
    }

    void UpdateGrass()
    {
        _grassEndTimer -= Time.deltaTime;
        if (_grassEndTimer <= 0f) EnterIdle();
    }

    void EnterIdle()
    {
        _state = AnimalState.Idle;
        SetAnimator(false, false);
        ResetWanderTimer();
        ResetGrassTimer();
    }

    void EnterWalk()
    {
        if (_state == AnimalState.Grass) return;
        _state        = AnimalState.Walk;
        _stuckTimer   = 0f;
        _lastPosition = transform.position;
        SetAnimator(false, true);
    }

    void EnterGrass()
    {
        if (_state != AnimalState.Idle) return;
        _state         = AnimalState.Grass;
        _grassEndTimer = GrassAnimDuration;
        SetAnimator(true, false);
    }

    void SetAnimator(bool isGrassing, bool isWalking)
    {
        if (_animator == null) return;
        _animator.SetBool(paramIsGrassing, isGrassing);
        _animator.SetBool(paramIsWalking,  isWalking);
    }

    void ResetWanderTimer() =>
        _wanderTimer = Random.Range(WanderIntervalMin, WanderIntervalMax);

    void ResetGrassTimer() =>
        _grassTimer = Random.Range(GrassIntervalMin, GrassIntervalMax);

    void FlipByDirection(float dirX)
    {
        if (Mathf.Abs(dirX) < 0.01f) return;
        float scaleX = dirX < 0
            ? -Mathf.Abs(transform.localScale.x)
            :  Mathf.Abs(transform.localScale.x);
        transform.localScale = new Vector3(
            scaleX, transform.localScale.y, transform.localScale.z);
    }

    // -------------------------------------------------------
    // 도망 / 데미지 / 사망
    // -------------------------------------------------------
    void DetectAndFlee()
    {
        // 가장 가까운 워커 감지
        var workers = Object.FindObjectsByType<WorkerUnit>(FindObjectsSortMode.None);
        WorkerUnit nearest = null;
        float minDist = DetectionRange;

        foreach (var w in workers)
        {
            if (w == null) continue;
            float d = Vector3.Distance(transform.position, w.transform.position);
            if (d < minDist) { minDist = d; nearest = w; }
        }

        if (nearest == null) return;

        // 워커 반대 방향으로 도망
        Vector3 fleeDir = (transform.position - nearest.transform.position).normalized;
        _targetPosition = transform.position + fleeDir * WanderRadius;
        FlipByDirection(fleeDir.x);

        Vector2 nextPos = _rb != null
            ? _rb.position + (Vector2)fleeDir * MoveSpeed * FleeSpeedMultiplier * Time.fixedDeltaTime
            : (Vector2)transform.position + (Vector2)fleeDir * MoveSpeed * FleeSpeedMultiplier * Time.deltaTime;

        if (_rb != null) _rb.MovePosition(nextPos);
        else             transform.position = nextPos;

        if (_state != AnimalState.Walk) EnterWalk();
    }

    public void TakeDamage(WorkerUnit attacker)
    {
        if (_isDead) return;
        _huntingWorker = attacker;
        _currentHp    -= HuntDamage;

        // 점멸 + 스트레치 효과
        if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
        if (_stretchCoroutine != null) StopCoroutine(_stretchCoroutine);
        _flashCoroutine   = StartCoroutine(FlashRed());
        _stretchCoroutine = StartCoroutine(StretchEffect());

        if (_currentHp <= 0)
            Die();
    }

    private Coroutine _flashCoroutine;

    [Header("스트레치 효과")]
    public float stretchAmount   = 1.3f;  // 가로 최대 배율
    public float stretchDuration = 0.06f; // 늘어나는 시간
    public float bounceDuration  = 0.12f; // 탄력 복원 시간

    private Coroutine _stretchCoroutine;
    private Vector3   _originalScale;

    System.Collections.IEnumerator StretchEffect()
    {
        if (_originalScale == Vector3.zero)
            _originalScale = transform.localScale;

        float absX = Mathf.Abs(_originalScale.x);
        float absY = Mathf.Abs(_originalScale.y);
        float signX = Mathf.Sign(_originalScale.x);

        // 가로로 늘어나기
        float elapsed = 0f;
        while (elapsed < stretchDuration)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / stretchDuration;
            float sx = Mathf.Lerp(absX, absX * stretchAmount, t);
            float sy = Mathf.Lerp(absY, absY / stretchAmount, t);
            transform.localScale = new Vector3(sx * signX, sy, _originalScale.z);
            yield return null;
        }

        // 탄력 있게 복원 (오버슈트)
        elapsed = 0f;
        while (elapsed < bounceDuration)
        {
            elapsed += Time.deltaTime;
            float t  = elapsed / bounceDuration;
            // SmoothStep으로 탄력감 표현
            float bounce = Mathf.Sin(t * Mathf.PI);
            float sx = Mathf.Lerp(absX * stretchAmount, absX, t) + bounce * 0.05f;
            float sy = Mathf.Lerp(absY / stretchAmount, absY, t);
            transform.localScale = new Vector3(sx * signX, sy, _originalScale.z);
            yield return null;
        }

        transform.localScale = _originalScale;
        _stretchCoroutine = null;
    }

    System.Collections.IEnumerator FlashRed()
    {
        _spriteRenderer.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        _spriteRenderer.color = Color.white;
        _flashCoroutine = null;
    }

    void Die()
    {
        _isDead       = true;
        _isHarvesting = true;

        if (_rb != null) _rb.linearVelocity = Vector2.zero;


        // 채집 완료 → DroppedResource 스폰
        if (_huntingWorker != null)
        {
            currentAmount = 0;
            SpawnDroppedOnDeath();
        }

        OnNodeDepleted();
    }

    void SpawnDroppedOnDeath()
    {
        var worker = _huntingWorker;
        int amt    = harvestAmountPerAction;

        // 어드레서블 참조 우선, 없으면 직접 참조 폴백
        bool useAddressable = droppedResourceRef != null
            && droppedResourceRef.RuntimeKeyIsValid()
            && ServiceLocator.Has<PoolManager>();

        if (useAddressable)
        {
            ServiceLocator.Get<PoolManager>().SpawnAsync(
                droppedResourceRef,
                transform.position,
                Quaternion.identity,
                (go) => OnAnimalDroppedSpawned(go, worker, amt, null)
            );
        }
        else if (droppedResourcePrefab != null)
        {
            GameObject go;
            if (ServiceLocator.Has<PoolManager>())
                go = ServiceLocator.Get<PoolManager>().Spawn(
                        droppedResourcePrefab, transform.position, Quaternion.identity);
            else
                go = Instantiate(droppedResourcePrefab, transform.position, Quaternion.identity);

            OnAnimalDroppedSpawned(go, worker, amt, droppedResourcePrefab);
        }
        else
        {
            worker?.StartGrabMove(resourceType, amt);
        }
    }

    void OnAnimalDroppedSpawned(GameObject go, WorkerUnit worker, int amt, GameObject prefabKey)
    {
        if (go == null) { worker?.StartGrabMove(resourceType, amt); return; }

        var dropped = go.GetComponent<DroppedResource>();
        if (dropped == null)
        {
            if (prefabKey != null && ServiceLocator.Has<PoolManager>())
                ServiceLocator.Get<PoolManager>().Despawn(prefabKey, go);
            else
                Destroy(go);
            return;
        }

        dropped.sourcePrefab = prefabKey;
        dropped.Initialize(resourceType, amt);
        dropped.TryAssign(worker);
        worker?.MoveToDropped(dropped);
    }

    protected override void OnWorkerRegistered()
    {
        // 워커가 등록되어도 도망 계속 허용 (추적 사냥 방식)
        // _huntingWorker는 TakeDamage()에서 설정됨
    }

    protected override void OnWorkerUnregistered()
    {
        _isHarvesting = false;
        EnterIdle();
    }

    protected override void OnHarvestPerformed(int amount)
    {
        // 데미지는 WorkerUnit이 직접 TakeDamage() 호출
    }

    protected override void OnNodeDepleted()
    {
        Destroy(gameObject);
    }

    protected override void OnNodeReset()
    {
        _isHarvesting = false;
        _state        = AnimalState.Idle;
        _targetPosition = _originPosition;

        if (_rb != null)
        {
            _rb.linearVelocity = Vector2.zero;
            _rb.MovePosition(_originPosition);
        }
        else
        {
            transform.position = _originPosition;
        }

        SetAnimator(false, false);
        ResetWanderTimer();
        ResetGrassTimer();
    }
}
