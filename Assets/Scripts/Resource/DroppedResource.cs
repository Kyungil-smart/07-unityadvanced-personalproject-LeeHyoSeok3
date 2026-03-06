using System.Collections;
using UnityEngine;

/// <summary>
/// 채집 완료 후 현장에 떨어진 자원 오브젝트
/// 스폰 시 위로 튀어올랐다가 중력으로 낙하하는 애니메이션 적용
///
/// 계층 구조:
///   DroppedResource (SpriteRenderer + Collider2D IsTrigger)
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

    void Start()
    {
        _groundY    = transform.position.y + groundOffsetY;
        _velocityY  = launchForce;
        _landed     = false;
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
        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!_landed) return;  // 착지 전엔 상호작용 불가
        var worker = other.GetComponent<WorkerUnit>();
        if (worker == null) return;
        if (_assignedWorker != worker) return;
        OnWorkerArrived(worker);
    }
}
