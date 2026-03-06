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
        StateMachine.ChangeState(UnitState.Idle);
        EventBus.Publish(new OnUnitSpawned { unit = this });
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

        EventBus.Publish(new OnUnitDied { unit = this });
        EventBus.Publish(new OnScorePenalty
        {
            scoreType = ScoreType.UnitDeath,
            amount    = 1
        });

        OnDead();
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
