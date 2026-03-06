using UnityEngine;

/// <summary>
/// 모든 건물의 기반 클래스
///
/// BuildingConstruction 유무에 따라 OnBuilt() 호출 시점이 달라짐
///   - 씬에 미리 배치된 건물 (Construction 없음) → Start()에서 즉시 OnBuilt()
///   - BuildingPlacer로 배치한 건물 (Construction 있음) → 건설 완료 후 CompleteBuilding()
/// </summary>
public abstract class BuildingBase : MonoBehaviour
{
    [Header("건물 데이터")]
    public BuildingData data;

    public int  CurrentHp   { get; private set; }
    public bool IsDestroyed { get; private set; }
    public bool IsBuilt     { get; private set; }  // 건설 완료 여부

    public bool MaintenanceActive { get; private set; } = true;

    protected SpriteRenderer _spriteRenderer;
    protected Animator       _animator;

    protected virtual void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _animator       = GetComponent<Animator>();

        if (data != null)
            CurrentHp = data.maxHp;
    }

    // Collider2D가 BuildingBase에 있으므로 여기서 trigger 감지
    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        var construction = GetComponent<BuildingConstruction>();
        if (construction == null) return;

        var worker = other.GetComponent<WorkerUnit>();
        if (worker == null) return;

        // 이 건물에 배정된 워커만 처리
        if (worker.AssignedConstruction != construction) return;

        // 이동 즉시 중단 + 경로 초기화 → OnArrived 호출
        worker.ForceArrive();

        // 건설 조건 확인 후 시작
        construction.NotifyTriggerEnter(worker);
    }

    protected virtual void Start()
    {
        // BuildingConstruction이 있으면 건설 완료 후 CompleteBuilding() 호출
        // 없으면 (씬에 미리 배치) 즉시 완성 처리
        if (GetComponent<BuildingConstruction>() == null)
            CompleteBuilding();
    }

    // Collider2D가 BuildingBase에 있으므로 여기서 trigger 감지


    /// <summary>
    /// 건설 완료 시 호출
    /// BuildingConstruction.Complete() 또는 Start()에서 호출
    /// </summary>
    public void CompleteBuilding()
    {
        if (IsBuilt) return;
        IsBuilt = true;

        OnBuilt();

        EventBus.Publish(new OnBuildingPlaced { building = this });
        EventBus.Publish(new OnScorePenalty
        {
            scoreType = ScoreType.BuildingPlaced,
            amount    = 1
        });

        Debug.Log($"[BuildingBase] {name} 건설 완료");
    }

    // -------------------------------------------------------
    // 피격 / 파괴
    // -------------------------------------------------------
    public virtual void TakeDamage(int damage)
    {
        if (IsDestroyed || !IsBuilt) return;

        CurrentHp -= damage;
        OnDamaged(damage);

        if (CurrentHp <= 0)
            Destroy_();
    }

    private void Destroy_()
    {
        if (IsDestroyed) return;
        IsDestroyed = true;

        OnDestroyed();

        EventBus.Publish(new OnBuildingDestroyed { building = this });
        EventBus.Publish(new OnScorePenalty
        {
            scoreType = ScoreType.BuildingDestroyed,
            amount    = 1
        });

        Destroy(gameObject, 1f);
    }

    // -------------------------------------------------------
    // 유지비
    // -------------------------------------------------------
    public void SetMaintenanceActive(bool active)
    {
        if (!data.HasMaintenance) return;

        var inventory = ServiceLocator.Get<ResourceInventory>();

        if (active && !inventory.CanAffordAll(data.GetMaintenanceCost()))
        {
            Debug.LogWarning($"[BuildingBase] {data.buildingName} 유지비 부족");
            return;
        }

        MaintenanceActive = active;
        EventBus.Publish(new OnMaintenanceToggled { building = this, active = active });
    }

    public bool PayMaintenance()
    {
        if (!MaintenanceActive) return false;
        if (!data.HasMaintenance) return true;

        bool paid = ServiceLocator.Get<ResourceInventory>().SpendAll(data.GetMaintenanceCost());
        if (!paid)
        {
            MaintenanceActive = false;
            Debug.LogWarning($"[BuildingBase] {data.buildingName} 유지비 부족으로 비활성화");
        }
        return paid;
    }

    // -------------------------------------------------------
    // 전략 초기화
    // -------------------------------------------------------
    public virtual void ResetBuilding() { }

    // -------------------------------------------------------
    // 하위 클래스 훅
    // -------------------------------------------------------
    protected virtual void OnBuilt()             { }
    protected virtual void OnDamaged(int damage) { }
    protected virtual void OnDestroyed()         { }
}
