using System.Collections;
using UnityEngine;

/// <summary>
/// 채집 완료 후 현장에 떨어진 자원 오브젝트
/// 스폰 시 위로 튀어올랐다가 중력으로 낙하하는 애니메이션 적용
///
/// 계층 구조:
///   DroppedResource (SpriteRenderer + Collider2D IsTrigger)
///
/// 풀링 지원:
///   풀에서 꺼낼 때 Initialize()를 반드시 호출해야 함 (Start 재실행 안 됨)
///   OnWorkerArrived() → ReturnToPool()로 Destroy 대신 풀 반납
/// </summary>
public class DroppedResource : MonoBehaviour
{
    public ResourceType resourceType;
    public int          amount;

    [Header("낙하 애니메이션")]
    public float launchForce    = 3f;   // 위로 튀어오르는 힘
    public float gravity        = 8f;   // 중력 가속도
    public float groundOffsetY  = 0f;   // 착지 Y 오프셋 (스폰 위치 기준)

    private WorkerUnit  _assignedWorker;
    public  bool        IsAssigned => _assignedWorker != null;

    private bool        _landed;
    public  bool        IsLanded => _landed;
    private float       _velocityY;
    private float       _groundY;

    /// <summary>
    /// 풀 반납 시 어떤 풀로 돌아갈지 추적하는 prefab 참조
    /// ResourceNode/AnimalNode에서 Spawn 시 설정
    /// </summary>
    [HideInInspector] public GameObject sourcePrefab;

    void Start()
    {
        // 씬에 직접 배치된 경우 또는 풀 없이 Instantiate된 경우
        // 풀 사용 시에는 Initialize()가 Start 역할을 대신함
        if (!_initializedByPool)
            InitializeInternal();
    }

    private bool _initializedByPool;

    // ---

    /// <summary>
    /// 풀에서 꺼낼 때 반드시 호출
    /// Start()가 다시 실행되지 않으므로 이 메서드로 상태를 초기화
    /// </summary>
    public void Initialize(ResourceType type, int amt)
    {
        resourceType       = type;
        amount             = amt;
        _initializedByPool = true;

        // 현재 위치 기준으로 착지 Y 계산 (Spawn 후 위치가 설정된 상태)
        InitializeInternal();
    }

    private void InitializeInternal()
    {
        _groundY    = transform.position.y + groundOffsetY;
        _velocityY  = launchForce;
        _landed     = false;
        _assignedWorker = null;
    }

    void Update()
    {
        if (_landed) return;

        // 중력 적용
        _velocityY -= gravity * Time.deltaTime;
        Vector3 pos = transform.position;
        pos.y += _velocityY * Time.deltaTime;

        // 착지
        if (pos.y <= _groundY)
        {
            pos.y      = _groundY;
            _landed    = true;
            _velocityY = 0f;

            // 착지 완료 → 대기 중인 워커에게 알림
            _assignedWorker?.OnDroppedResourceLanded(this);
        }

        transform.position = pos;
    }

    public bool TryAssign(WorkerUnit worker)
    {
        if (IsAssigned) return false;
        _assignedWorker = worker;
        return true;
    }

    public void Unassign()
    {
        _assignedWorker = null;
    }

    public void OnWorkerArrived(WorkerUnit worker)
    {
        if (_assignedWorker != worker) return;
        worker.StartGrabMove(resourceType, amount);
        ReturnToPool();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!_landed) return;  // 착지 전엔 상호작용 불가
        var worker = other.GetComponent<WorkerUnit>();
        if (worker == null) return;
        if (_assignedWorker != worker) return;
        OnWorkerArrived(worker);
    }

    // ---
    // 풀 반납
    // ---

    private void ReturnToPool()
    {
        if (sourcePrefab != null && ServiceLocator.Has<PoolManager>())
        {
            // 다음 사용을 위해 초기화 플래그 리셋
            _initializedByPool = false;
            ServiceLocator.Get<PoolManager>().Despawn(sourcePrefab, gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
