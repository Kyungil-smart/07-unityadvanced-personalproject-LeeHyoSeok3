using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 하단 액션 바 - 건설 / 벌목 / 채굴 / 사냥
///
/// 계층 구조:
///   ActionBarUI
///     └─ Panel (HorizontalLayoutGroup)
///           ├─ ConstructionButton
///           ├─ LoggingButton
///           ├─ MiningButton
///           └─ HuntingButton
/// </summary>
public class ActionBarUI : MonoBehaviour
{
    [Header("버튼 참조")]
    public Button constructionButton;
    public Button loggingButton;      // 벌목
    public Button miningButton;       // 채굴
    public Button huntingButton;      // 사냥

    [Header("팝업 참조")]
    public ConstructionPopup       constructionPopup;
    public WorkerAllocationPopup   allocationPopup;

    void Start()
    {
        constructionButton?.onClick.AddListener(OnConstructionClick);
        loggingButton?.onClick.AddListener(() => OnResourceButtonClick(ResourceType.Wood));
        miningButton?.onClick.AddListener(() => OnResourceButtonClick(ResourceType.Gold));
        huntingButton?.onClick.AddListener(() => OnResourceButtonClick(ResourceType.Meat));

        EventBus.Subscribe<OnPhaseChanged>(OnPhaseChanged);
        EventBus.Subscribe<OnWorkerBecameIdle>(OnWorkerBecameIdle);
        EventBus.Subscribe<OnUnitDied>(OnUnitDied);
        EventBus.Subscribe<OnResourceChanged>(OnResourceChanged);
        RefreshButtons();
        RefreshResourceButtons();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<OnPhaseChanged>(OnPhaseChanged);
        EventBus.Unsubscribe<OnWorkerBecameIdle>(OnWorkerBecameIdle);
        EventBus.Unsubscribe<OnUnitDied>(OnUnitDied);
        EventBus.Unsubscribe<OnResourceChanged>(OnResourceChanged);
    }

    public void OnPopupClosedExternally()
    {
        // PopupManager에 의해 외부에서 팝업이 닫힐 때 상태 초기화
        _currentFocusType = null;
        GetFocusUI()?.ClearFocus();
    }

    void OnPhaseChanged(OnPhaseChanged e) => RefreshButtons();

    // Meat 적재량이 꽉 차면 사냥 버튼 비활성화
    void OnResourceChanged(OnResourceChanged e)
    {
        if (e.type == ResourceType.Meat) RefreshResourceButtons();
    }

    void OnWorkerBecameIdle(OnWorkerBecameIdle e)
    {
        RefreshResourceButtons();
        RefreshAllocationPopup();
    }

    void OnUnitDied(OnUnitDied e)
    {
        RefreshResourceButtons();
        RefreshAllocationPopup();
    }

    // 팝업이 열려있으면 유휴 워커 수를 다시 계산해 반영
    void RefreshAllocationPopup()
    {
        if (allocationPopup == null || !allocationPopup.gameObject.activeSelf) return;
        if (_currentFocusType == null) return;
        allocationPopup.UpdateAvailable(GetMaxAllocatable(_currentFocusType.Value));
    }

    // 실제 할당 가능한 최대 워커 수
    // = Min(유휴 워커, 빈 노드 수) 이며, Meat는 남은 적재량 기반으로 추가 제한
    int GetMaxAllocatable(ResourceType type)
    {
        var nodes  = GetAvailableNodes(type);
        int byNode = Mathf.Min(GetIdleWorkerCount(), nodes.Count);

        if (type != ResourceType.Meat) return byNode;

        // Meat 전용: 이미 사냥 중인 워커가 가져올 고기를 감안한 실질 여유량으로 제한
        if (!ServiceLocator.Has<ResourceInventory>()) return 0;
        var inv       = ServiceLocator.Get<ResourceInventory>();

        int harvestPerWorker = nodes.Count > 0 ? nodes[0].harvestAmountPerAction : 0;
        if (harvestPerWorker <= 0) return 0;

        // 현재 사냥 중인 워커 수 (AssignedNode가 AnimalNode인 경우)
        int activeHunters = 0;
        if (ServiceLocator.Has<WorkerAssigner>())
        {
            foreach (var w in ServiceLocator.Get<WorkerAssigner>().GetAllWorkers())
                if (w != null && w.AssignedNode is AnimalNode) activeHunters++;
        }

        // 실질 여유량 = 총 적재량 - 현재 보유량 - (사냥 중 워커가 가져올 고기)
        int effectiveRemaining = inv.meatCapacity - inv.Get(ResourceType.Meat)
                                 - activeHunters * harvestPerWorker;
        if (effectiveRemaining <= 0) return 0;

        int byCapacity = effectiveRemaining / harvestPerWorker;
        return Mathf.Min(byNode, byCapacity);
    }

    void RefreshButtons()
    {
        bool isPrepare = PhaseManager.Instance != null && PhaseManager.Instance.IsPrepare;

        if (constructionButton != null) constructionButton.interactable = isPrepare;

        if (!isPrepare)
        {
            constructionPopup?.Close();
            allocationPopup?.Close();
        }

        RefreshResourceButtons();
    }

    void RefreshResourceButtons()
    {
        bool isPrepare = PhaseManager.Instance != null && PhaseManager.Instance.IsPrepare;

        if (loggingButton != null)
            loggingButton.interactable = isPrepare && GetMaxAllocatable(ResourceType.Wood) > 0;
        if (miningButton != null)
            miningButton.interactable  = isPrepare && GetMaxAllocatable(ResourceType.Gold) > 0;
        if (huntingButton != null)
            huntingButton.interactable = isPrepare && GetMaxAllocatable(ResourceType.Meat) > 0;
    }

    bool IsMeatFull()
    {
        if (!ServiceLocator.Has<ResourceInventory>()) return false;
        var inv = ServiceLocator.Get<ResourceInventory>();
        return inv.Get(ResourceType.Meat) >= inv.meatCapacity;
    }

    bool HasNode<T>() where T : ResourceNode
    {
        var nodes = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
        return nodes != null && System.Array.Exists(nodes, n => n != null && !n.IsDeplete);
    }

    // -------------------------------------------------------
    // 건설 버튼
    // -------------------------------------------------------
    void OnConstructionClick()
    {
        allocationPopup?.Close();

        if (constructionPopup == null) return;
        if (constructionPopup.gameObject.activeSelf)
            constructionPopup.Close();
        else
            constructionPopup.Open();
    }

    private ResourceType? _currentFocusType = null;

    ResourceFocusUI GetFocusUI() =>
        ServiceLocator.Has<ResourceFocusUI>()
            ? ServiceLocator.Get<ResourceFocusUI>()
            : Object.FindFirstObjectByType<ResourceFocusUI>();

    void ApplyFocus(ResourceType type)
    {
        var focusUI = GetFocusUI();
        if (focusUI == null) return;
        switch (type)
        {
            case ResourceType.Wood: focusUI.FocusNearest<TreeNode>(out _);   break;
            case ResourceType.Gold: focusUI.FocusNearest<GoldNode>(out _);   break;
            case ResourceType.Meat: focusUI.FocusNearest<AnimalNode>(out _); break;
        }
    }

    // -------------------------------------------------------
    // 자원 버튼 (포커스 + 할당 팝업)
    // -------------------------------------------------------
    void OnResourceButtonClick(ResourceType type)
    {
        bool popupOpen = allocationPopup != null && allocationPopup.gameObject.activeSelf;

        // 팝업 열린 상태에서 다른 버튼 클릭 → 포커스만 이동, 팝업 유지
        if (popupOpen && _currentFocusType != type)
        {
            _currentFocusType = type;
            ApplyFocus(type);
            return;
        }

        // 같은 버튼 다시 클릭 → 팝업 닫기 + 선택 UI 제거
        if (popupOpen && _currentFocusType == type)
        {
            allocationPopup.Close();
            GetFocusUI()?.ClearFocus();
            _currentFocusType = null;
            return;
        }

        // 팝업 닫힌 상태 → 포커스 이동 + 팝업 열기
        ApplyFocus(type);
        _currentFocusType = type;
        OnAllocationClick(type);
    }

    // -------------------------------------------------------
    // 벌목 / 채굴 / 사냥 버튼
    // -------------------------------------------------------
    void OnAllocationClick(ResourceType type)
    {
        // 건설 팝업 닫기
        if (constructionPopup != null && constructionPopup.gameObject.activeSelf)
            constructionPopup.Close();

        if (allocationPopup == null) return;

        // 할당 가능한 워커 수 (유휴 워커 수와 빈 노드 수 중 작은 값)
        int available = GetMaxAllocatable(type);
        if (available <= 0)
        {
            Debug.LogWarning("[ActionBarUI] 할당 가능한 워커 없음");
            return;
        }

        allocationPopup.Open(available, count => OnAllocationConfirm(type, count));
    }

    void OnAllocationConfirm(ResourceType type, int count)
    {
        var assigner = ServiceLocator.Has<WorkerAssigner>()
            ? ServiceLocator.Get<WorkerAssigner>()
            : Object.FindFirstObjectByType<WorkerAssigner>();

        if (assigner == null)
        {
            Debug.LogWarning("[ActionBarUI] WorkerAssigner 없음");
            return;
        }

        var workers = new System.Collections.Generic.List<WorkerUnit>(assigner.GetAllWorkers());
        int assigned = 0;

        // 유휴 워커 목록
        var idleWorkers = workers.FindAll(w =>
            w != null &&
            w.StateMachine.Is(UnitState.Idle) &&
            w.AssignedConstruction == null);

        if (idleWorkers.Count == 0)
        {
            Debug.LogWarning("[ActionBarUI] 유휴 워커 없음");
            return;
        }

        // Meat 적재량 초과 방지 (확인 버튼 누르는 시점 재검사)
        if (type == ResourceType.Meat && IsMeatFull())
        {
            Debug.LogWarning("[ActionBarUI] 고기 적재량이 가득 찼습니다");
            return;
        }

        // 할당 가능한 노드 목록 (IsFull 아닌 것만)
        var nodes = GetAvailableNodes(type);
        if (nodes.Count == 0)
        {
            Debug.LogWarning($"[ActionBarUI] 할당 가능한 {type} 노드 없음");
            return;
        }

        // 워커마다 가장 가까운 노드에 할당
        foreach (var worker in idleWorkers)
        {
            if (assigned >= count) break;

            // 이 워커와 가장 가까운 할당 가능한 노드 선택
            ResourceNode nearestNode = null;
            float minDist = float.MaxValue;
            foreach (var node in nodes)
            {
                if (node.IsFull) continue;
                float dist = Vector3.Distance(worker.transform.position, node.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearestNode = node;
                }
            }

            if (nearestNode == null) break;

            worker.AssignNode(nearestNode, type);
            assigned++;
        }

        Debug.Log($"[ActionBarUI] {type} 워커 {assigned}명 할당 완료");
    }

    System.Collections.Generic.List<ResourceNode> GetAvailableNodes(ResourceType type)
    {
        var result = new System.Collections.Generic.List<ResourceNode>();
        switch (type)
        {
            case ResourceType.Wood:
                foreach (var n in Object.FindObjectsByType<TreeNode>(FindObjectsSortMode.None))
                    if (n != null && !n.IsDeplete && !n.IsFull) result.Add(n);
                break;
            case ResourceType.Gold:
                foreach (var n in Object.FindObjectsByType<GoldNode>(FindObjectsSortMode.None))
                    if (n != null && !n.IsDeplete && !n.IsFull) result.Add(n);
                break;
            case ResourceType.Meat:
                foreach (var n in Object.FindObjectsByType<AnimalNode>(FindObjectsSortMode.None))
                    if (n != null && !n.IsDeplete && !n.IsFull) result.Add(n);
                break;
        }
        return result;
    }

    int GetIdleWorkerCount()
    {
        if (!ServiceLocator.Has<WorkerAssigner>()) return 0;
        int count = 0;
        foreach (var w in ServiceLocator.Get<WorkerAssigner>().GetAllWorkers())
            if (w != null && w.StateMachine.Is(UnitState.Idle)) count++;
        return count;
    }
}
