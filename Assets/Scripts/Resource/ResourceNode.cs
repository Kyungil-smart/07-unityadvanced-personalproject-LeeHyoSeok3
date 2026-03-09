using UnityEngine;

/// <summary>
/// 맵에 배치되는 자원 노드 기반 클래스
/// TreeNode / GoldNode / AnimalNode 가 상속
///
/// 클릭 → OnResourceNodeClicked 이벤트 발행
/// → WorkerAssigner가 받아 가까운 일꾼을 이 노드에 할당
/// </summary>
[RequireComponent(typeof(Collider2D))]
public abstract class ResourceNode : MonoBehaviour
{
    [Header("밸런싱 데이터")]
    [SerializeField] protected ResourceNodeData _data;

    [Header("자원 설정")]
    public ResourceType resourceType;
    public int currentAmount;

    [Header("채집 설정")]
    [Tooltip("채집 완료 후 스폰할 DroppedResource 프리팹")]
    public GameObject droppedResourcePrefab;

    // 밸런싱 값 프로퍼티 (_data 우선, 없으면 기본값 폴백)
    public int   maxAmount              => _data != null ? _data.maxAmount              : 100;
    public int   harvestAmountPerAction => _data != null ? _data.harvestAmountPerAction : 10;
    public int   energyCostPerHarvest   => _data != null ? _data.energyCostPerHarvest   : 10;
    public float harvestDuration        => _data != null ? _data.harvestDuration        : 3f;

    // 하나의 노드에 워커 1명만 허용
    private WorkerUnit _assignedWorker;
    public  bool IsFull    => _assignedWorker != null;
    public  bool IsDeplete => currentAmount <= 0;

    protected SpriteRenderer _spriteRenderer;
    protected Animator _animator;

    protected virtual void Awake()
    {
        currentAmount    = maxAmount;
        _spriteRenderer  = GetComponent<SpriteRenderer>();
        _animator        = GetComponent<Animator>();
    }

    // -------------------------------------------------------
    // 클릭 → 일꾼 할당 요청
    // -------------------------------------------------------
    void OnMouseDown()
    {
        // 준비 페이즈에서만 클릭 유효
        if (!PhaseManager.Instance.IsPrepare) return;
        if (IsDeplete) return;
        if (IsFull)
        {
            Debug.Log($"[ResourceNode] {name} 이미 최대 일꾼 할당됨");
            return;
        }

        EventBus.Publish(new OnResourceNodeClicked { node = this });
    }

    // -------------------------------------------------------
    // 일꾼 등록 / 해제
    // -------------------------------------------------------
    public bool TryRegisterWorker(WorkerUnit worker)
    {
        if (IsFull || IsDeplete) return false;
        _assignedWorker = worker;
        OnWorkerRegistered();
        return true;
    }

    // 구버전 호환
    public bool TryRegisterWorker() => TryRegisterWorker(null);

    public void UnregisterWorker()
    {
        _assignedWorker = null;
        OnWorkerUnregistered();
    }

    // -------------------------------------------------------
    // 채집 완료 (WorkerUnit이 harvestDuration 후 호출)
    // -------------------------------------------------------
    public void CompleteHarvest(WorkerUnit worker)
    {
        if (IsDeplete) return;

        int amount = Mathf.Min(harvestAmountPerAction, currentAmount);
        currentAmount -= amount;

        EventBus.Publish(new OnScorePenalty
        {
            scoreType = ScoreType.ResourceHarvest,
            amount    = amount
        });

        OnHarvestPerformed(amount);

        // 채집물 오브젝트 현장에 스폰
        SpawnDroppedResource(worker, amount);

        if (IsDeplete)
        {
            OnNodeDepleted();
            Debug.Log($"[ResourceNode] {name} 자원 소진");
        }
    }

    void SpawnDroppedResource(WorkerUnit worker, int amount)
    {
        if (droppedResourcePrefab == null)
        {
            // 프리팹 없으면 바로 본진으로 이동
            worker.StartGrabMove(resourceType, amount);
            return;
        }

        var go      = Instantiate(droppedResourcePrefab, transform.position, Quaternion.identity);
        var dropped = go.GetComponent<DroppedResource>();
        if (dropped == null)
        {
            Debug.LogWarning("[ResourceNode] DroppedResource 컴포넌트 없음");
            worker.StartGrabMove(resourceType, amount);
            Destroy(go);
            return;
        }

        dropped.resourceType = resourceType;
        dropped.amount       = amount;
        dropped.TryAssign(worker);

        // 워커가 채집물 위치로 이동
        worker.MoveToDropped(dropped);
    }

    // 구버전 호환
    public int Harvest() { CompleteHarvest(null); return 0; }

    // -------------------------------------------------------
    // 초기화 (전략 리셋용)
    // -------------------------------------------------------
    public virtual void ResetNode()
    {
        currentAmount   = maxAmount;
        _assignedWorker = null;
        gameObject.SetActive(true);
        OnNodeReset();
    }

    // -------------------------------------------------------
    // 하위 클래스 훅
    // -------------------------------------------------------
    protected virtual void OnWorkerRegistered()   { }
    protected virtual void OnWorkerUnregistered() { }
    protected virtual void OnHarvestPerformed(int amount) { }
    protected virtual void OnNodeDepleted()       { gameObject.SetActive(false); }
    protected virtual void OnNodeReset()          { }
}
