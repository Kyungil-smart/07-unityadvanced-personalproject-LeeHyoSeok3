using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 일꾼 에너지 관리
///
/// - 채집 1회마다 energyCostPerHarvest 소모
/// - 에너지 0 도달 시 OnEnergyDepleted 호출 → 일꾼 제거
/// - UI 슬라이더와 연동 (선택적)
/// </summary>
public class WorkerEnergy : MonoBehaviour
{
    [Header("에너지 UI (선택)")]
    public Slider energySlider;

    public int MaxEnergy    { get; private set; }
    public int CurrentEnergy{ get; private set; }
    public bool IsDepleted  => CurrentEnergy <= 0;

    private WorkerUnit _owner;

    public void Initialize(WorkerUnit owner, int maxEnergy)
    {
        _owner       = owner;
        MaxEnergy    = maxEnergy;
        CurrentEnergy= maxEnergy;
        RefreshUI();
    }

    // -------------------------------------------------------
    // 에너지 소모
    // -------------------------------------------------------
    /// <summary>
    /// 에너지를 amount만큼 소모.
    /// 소진 시 OnWorkerEnergyDepleted 이벤트 발행 후 유닛 제거
    /// </summary>
    public void Consume(int amount)
    {
        if (IsDepleted) return;

        CurrentEnergy = Mathf.Max(0, CurrentEnergy - amount);
        RefreshUI();

        Debug.Log($"[WorkerEnergy] {_owner.name} 에너지: {CurrentEnergy}/{MaxEnergy}");

        if (IsDepleted)
            OnEnergyDepleted();
    }

    // -------------------------------------------------------
    // 초기화 (전략 리셋용)
    // -------------------------------------------------------
    public void ResetEnergy()
    {
        CurrentEnergy = MaxEnergy;
        RefreshUI();
    }

    // -------------------------------------------------------
    // 내부
    // -------------------------------------------------------
    private void OnEnergyDepleted()
    {
        Debug.Log($"[WorkerEnergy] {_owner.name} 에너지 소진 → 제거");
        EventBus.Publish(new OnWorkerEnergyDepleted { worker = _owner });

        // 유닛 제거
        Destroy(_owner.gameObject);
    }

    private void RefreshUI()
    {
        if (energySlider == null) return;
        energySlider.value = MaxEnergy > 0
            ? (float)CurrentEnergy / MaxEnergy
            : 0f;
    }
}
