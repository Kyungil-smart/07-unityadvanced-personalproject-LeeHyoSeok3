using UnityEngine;

/// <summary>
/// 전투 유닛 공통 기반 클래스
/// SpearMan / Warrior / Archer / Monk가 상속
///
/// 상태 흐름:
///   Idle → 적 탐지 시 → Attack (공격 반복)
///   Idle → 방어 지점 설정 시 → Move → 도착 → Idle
///   Attack → 적 소멸/사거리 이탈 시 → Idle
/// </summary>
public abstract class CombatUnit : UnitBase
{
    // Inspector에서 CombatUnitData를 data 슬롯에 할당
    protected CombatUnitData CombatData => data as CombatUnitData;

    // 방어 지점 (ProductionBuilding 스폰 후 DefensePointManager에서 설정)
    private Vector3 _defensePoint;
    private bool    _hasDefensePoint;

    // 현재 공격 대상
    private UnitBase _attackTarget;

    // 공격 쿨타임 타이머
    private float _attackTimer;

    // -----------------------------------------------------------
    // 방어 지점 설정
    // -----------------------------------------------------------
    /// <summary>
    /// 스폰 직후 ProductionBuilding에서 호출
    /// 설정된 위치로 즉시 이동 시작
    /// </summary>
    public void SetDefensePoint(Vector3 point)
    {
        _defensePoint    = point;
        _hasDefensePoint = true;
        MoveToDefensePoint();
    }

    // -----------------------------------------------------------
    // 업데이트
    // -----------------------------------------------------------
    protected virtual void Update()
    {
        if (IsDead) return;
        if (!GameManager.Instance.IsPlaying) return;

        switch (State)
        {
            case UnitState.Idle:   UpdateIdle();   break;
            case UnitState.Move:   UpdateMove();   break;
            case UnitState.Attack: UpdateAttack(); break;
        }
    }

    // Idle — 주변 적 탐색
    void UpdateIdle()
    {
        _attackTarget = FindNearestEnemy();
        if (_attackTarget != null)
            StateMachine.ChangeState(UnitState.Attack);
    }

    // Move — 방어 지점으로 이동, 이동 중 적 발견 시 공격 전환
    void UpdateMove()
    {
        _attackTarget = FindNearestEnemy();
        if (_attackTarget != null)
        {
            StopMove();
            StateMachine.ChangeState(UnitState.Attack);
            return;
        }

        if (IsArrived(_defensePoint))
        {
            StopMove();
            StateMachine.ChangeState(UnitState.Idle);
            return;
        }

        MoveTo(_defensePoint);
    }

    // Attack — 쿨타임마다 공격, 타겟 소멸/이탈 시 Idle 복귀
    void UpdateAttack()
    {
        // 타겟 유효성 검사
        if (_attackTarget == null || _attackTarget.IsDead)
        {
            _attackTarget = FindNearestEnemy();
            if (_attackTarget == null)
            {
                StateMachine.ChangeState(UnitState.Idle);
                return;
            }
        }

        // 사거리 이탈 여부 확인
        float dist = Vector3.Distance(transform.position, _attackTarget.transform.position);
        if (dist > CombatData.attackRange)
        {
            _attackTarget = FindNearestEnemy();
            if (_attackTarget == null)
                StateMachine.ChangeState(UnitState.Idle);
            return;
        }

        // 쿨타임 차감 후 공격
        _attackTimer -= Time.deltaTime;
        if (_attackTimer <= 0f)
        {
            _attackTimer = CombatData.attackCooldown;
            ExecuteAttack(_attackTarget);
        }
    }

    // -----------------------------------------------------------
    // 적 탐색 (Physics2D OverlapCircle — "Enemy" 태그 기준)
    // -----------------------------------------------------------
    UnitBase FindNearestEnemy()
    {
        var hits = Physics2D.OverlapCircleAll(transform.position, CombatData.attackRange);

        UnitBase nearest = null;
        float    minDist = float.MaxValue;

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;

            var unit = hit.GetComponent<UnitBase>();
            if (unit == null || unit.IsDead) continue;

            float d = Vector3.Distance(transform.position, hit.transform.position);
            if (d < minDist)
            {
                minDist  = d;
                nearest  = unit;
            }
        }
        return nearest;
    }

    // -----------------------------------------------------------
    // 공격 실행
    // -----------------------------------------------------------
    /// <summary>
    /// 실제 공격 처리 — 하위 클래스에서 오버라이드 가능
    /// (예: Archer는 투사체 생성, Monk는 광역 힐)
    /// </summary>
    protected virtual void ExecuteAttack(UnitBase target)
    {
        target.TakeDamage(CombatData.attackDamage);
        OnAttack(target);
    }

    /// <summary>
    /// 공격 훅 — 하위 클래스에서 애니메이션 이벤트 / 이펙트 연결
    /// </summary>
    protected virtual void OnAttack(UnitBase target) { }

    // -----------------------------------------------------------
    // 내부 유틸
    // -----------------------------------------------------------
    void MoveToDefensePoint()
    {
        if (!_hasDefensePoint) return;
        StateMachine.ChangeState(UnitState.Move);
    }

    // -----------------------------------------------------------
    // 상태 머신 훅
    // -----------------------------------------------------------
    public override void OnStateEnter(UnitState state)
    {
        switch (state)
        {
            case UnitState.Idle:
                StopMove();
                SetAnimBool("IsMove", false);
                SetAnimBool("IsAttack", false);
                break;

            case UnitState.Move:
                SetAnimBool("IsMove", true);
                SetAnimBool("IsAttack", false);
                break;

            case UnitState.Attack:
                StopMove();
                _attackTimer = 0f; // 진입 즉시 첫 공격 가능
                SetAnimBool("IsMove", false);
                SetAnimBool("IsAttack", true);
                break;

            case UnitState.Dead:
                StopMove();
                SetAnimBool("IsMove", false);
                SetAnimBool("IsAttack", false);
                SetAnim("Dead");
                break;
        }
    }

    public override void OnStateExit(UnitState state)
    {
        if (state == UnitState.Attack)
            _attackTarget = null;
    }
}
