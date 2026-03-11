using UnityEngine;

/// <summary>
/// 모든 유닛의 기반 클래스
/// WorkerUnit / CombatUnit / EnemyUnit 이 상속
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public abstract class UnitBase : MonoBehaviour
{
    [Header("유닛 데이터")]
    public UnitData data;

    [Header("이펙트")]
    [Tooltip("스폰/사망 시 생성할 이펙트 프리팹")]
    public GameObject effectPrefab;
    [Tooltip("이펙트 생성 개수")]
    [SerializeField] private int _effectCount = 3;
    [Tooltip("스프라이트 중심 기준 이펙트 산포 반지름")]
    [SerializeField] private float _effectSpawnRadius = 0.3f;
    [Tooltip("이펙트 랜덤 스케일 최솟값")]
    [SerializeField] private float _effectScaleMin = 1f;
    [Tooltip("이펙트 랜덤 스케일 최댓값")]
    [SerializeField] private float _effectScaleMax = 1f;

    // 상태 머신
    public UnitStateMachine StateMachine { get; private set; }
    public UnitState State => StateMachine.Current;

    // 컴포넌트
    protected Rigidbody2D _rb;
    protected Animator    _animator;
    protected SpriteRenderer _spriteRenderer;

    // HP
    public int CurrentHp { get; private set; }
    public bool IsDead => StateMachine.Is(UnitState.Dead);

    // 풀링 재활성화 시 중복 스폰 방지용 플래그
    private bool _initialized;

    /// <summary>풀 반납 후 재사용 시 HP를 최대치로 복구</summary>
    protected void RestoreFullHp()
    {
        if (data != null) CurrentHp = data.maxHp;
    }

    protected virtual void Awake()
    {
        StateMachine     = new UnitStateMachine(this);
        _rb              = GetComponent<Rigidbody2D>();
        _animator        = GetComponent<Animator>();
        _spriteRenderer  = GetComponent<SpriteRenderer>();

        _rb.gravityScale = 0f; // 2D 탑다운
        _rb.freezeRotation = true;

        if (data != null)
            CurrentHp = data.maxHp;
    }

    protected virtual void Start()
    {
        _initialized = true;
        StateMachine.ChangeState(UnitState.Idle);
        SpawnScatteredEffects(effectPrefab);
        EventBus.Publish(new OnUnitSpawned { unit = this });
    }

    // 풀에서 재활성화될 때 스폰 이펙트 재생
    protected virtual void OnEnable()
    {
        // Start() 이전 초기화 단계(풀 워밍업 등)에서는 무시
        if (!_initialized) return;
        SpawnScatteredEffects(effectPrefab);
    }

    // -------------------------------------------------------
    // 이동
    // -------------------------------------------------------
    protected void MoveTo(Vector3 target)
    {
        Vector2 dir = (target - transform.position).normalized;
        _rb.linearVelocity = dir * data.moveSpeed;

        // 이동 방향에 따라 스프라이트 좌우 반전
        if (Mathf.Abs(dir.x) > 0.01f)
        {
            float scaleX = dir.x < 0
                ? -Mathf.Abs(transform.localScale.x)
                :  Mathf.Abs(transform.localScale.x);
            transform.localScale = new Vector3(
                scaleX, transform.localScale.y, transform.localScale.z
            );
        }
    }

    protected void StopMove()
    {
        _rb.linearVelocity = Vector2.zero;
    }

    // -------------------------------------------------------
    // 목표 도달 여부
    // -------------------------------------------------------
    protected bool IsArrived(Vector3 target, float threshold = 0.2f)
    {
        return Vector3.Distance(transform.position, target) <= threshold;
    }

    // -------------------------------------------------------
    // 피격 / 사망
    // -------------------------------------------------------
    public virtual void TakeDamage(int damage)
    {
        if (IsDead) return;
        CurrentHp -= damage;

        if (CurrentHp <= 0)
            Die();
    }

    protected virtual void Die()
    {
        StopMove();
        StateMachine.ChangeState(UnitState.Dead);

        SpawnScatteredEffects(effectPrefab);

        EventBus.Publish(new OnUnitDied { unit = this });
        EventBus.Publish(new OnScorePenalty
        {
            scoreType = ScoreType.UnitDeath,
            amount    = 1
        });

        OnDead();
    }

    // -------------------------------------------------------
    // 이펙트 산포 생성
    // -------------------------------------------------------

    protected void SpawnScatteredEffects(GameObject prefab)
    {
        EffectSpawner.SpawnScattered(prefab, _spriteRenderer, transform.position, _effectCount, _effectSpawnRadius, _effectScaleMin, _effectScaleMax);
    }

    void OnDrawGizmosSelected()
    {
        var sr     = GetComponent<SpriteRenderer>();
        Vector3 center = (sr != null) ? sr.bounds.center : transform.position;

        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(center, _effectSpawnRadius);
    }

    // -------------------------------------------------------
    // 상태 머신 훅 (하위 클래스에서 오버라이드)
    // -------------------------------------------------------
    public virtual void OnStateEnter(UnitState state) { }
    public virtual void OnStateExit(UnitState state)  { }
    protected virtual void OnDead() { Destroy(gameObject, 1f); }

    // -------------------------------------------------------
    // 애니메이션 헬퍼
    // -------------------------------------------------------
    protected void SetAnim(string trigger)
    {
        if (_animator != null) _animator.SetTrigger(trigger);
    }

    protected void SetAnimBool(string param, bool value)
    {
        if (_animator != null) _animator.SetBool(param, value);
    }
}
