using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 본진 (Castle)
///
/// - 파괴 시 OnHeadquartersDestroyed 이벤트 발행 → GameManager가 GameOver 처리
/// - HP 슬라이더 UI 연동
/// - 적군 AI의 최종 목표 지점
/// - 워커가 자원을 들고 도착 시 ResourceInventory에 저장
/// </summary>
public class Castle : BuildingBase
{
    [Header("본진 HP UI (선택)")]
    public Slider hpSlider;
    public Text   hpText;

    [Header("피격 연출")]
    public string hitAnimTrigger     = "Hit";
    public string destroyAnimTrigger = "Destroy";

    // 적군 AI가 이 위치를 목표로 이동
    public Vector3 Position => transform.position;

    protected override void Awake()
    {
        base.Awake();
        ServiceLocator.Register<Castle>(this);
    }

    protected override void OnBuilt()
    {
        RefreshHpUI();
        Debug.Log($"[Castle] 본진 배치 완료. HP: {CurrentHp}");
    }

    // -------------------------------------------------------
    // 피격
    // -------------------------------------------------------
    public override void TakeDamage(int damage)
    {
        base.TakeDamage(damage);
        RefreshHpUI();
    }

    protected override void OnDamaged(int damage)
    {
        if (_animator != null)
            _animator.SetTrigger(hitAnimTrigger);

        Debug.Log($"[Castle] 피격 -{damage}  HP: {CurrentHp}");
    }

    // -------------------------------------------------------
    // 파괴 → 게임오버
    // -------------------------------------------------------
    protected override void OnDestroyed()
    {
        if (_animator != null)
            _animator.SetTrigger(destroyAnimTrigger);

        Debug.Log("[Castle] 본진 파괴 → 게임오버");
        EventBus.Publish(new OnHeadquartersDestroyed());
    }

    // -------------------------------------------------------
    // 워커 자원 반납
    // -------------------------------------------------------

    /// <summary>본진 위치 반환</summary>
    public Vector3 GetPosition() => transform.position;

    /// <summary>워커가 가져온 자원을 Inventory에 저장</summary>
    public void DepositResource(ResourceType type, int amount)
    {
        if (!ServiceLocator.Has<ResourceInventory>()) return;
        ServiceLocator.Get<ResourceInventory>().Add(type, amount);
        Debug.Log($"[Castle] {type} +{amount} 저장 완료");
    }

    /// <summary>워커 trigger 진입 콜백</summary>
    protected override void OnTriggerEnter2D(Collider2D other)
    {
        base.OnTriggerEnter2D(other);

        var worker = other.GetComponent<WorkerUnit>();
        if (worker == null) return;
        worker.OnArrivedAtCastle(this);
    }

    // -------------------------------------------------------
    // HP UI 갱신
    // -------------------------------------------------------
    private void RefreshHpUI()
    {
        if (data == null) return;

        float ratio = (float)CurrentHp / data.maxHp;

        if (hpSlider != null)
            hpSlider.value = ratio;

        if (hpText != null)
            hpText.text = $"{CurrentHp} / {data.maxHp}";
    }
}
