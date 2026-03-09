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
        RefreshButtons();
        RefreshResourceButtons();
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<OnPhaseChanged>(OnPhaseChanged);
    }

    public void OnPopupClosedExternally()
    {
        // PopupManager에 의해 외부에서 팝업이 닫힐 때 상태 초기화
        _currentFocusType = null;
        GetFocusUI()?.ClearFocus();
    }

    void OnPhaseChanged(OnPhaseChanged e) => RefreshButtons();

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

        bool hasTree   = HasNode<TreeNode>();
        bool hasGold   = HasNode<GoldNode>();
        bool hasAnimal = HasNode<AnimalNode>();

        if (loggingButton != null) loggingButton.interactable = isPrepare && hasTree;
        if (miningButton  != null) miningButton.interactable  = isPrepare && hasGold;
        if (huntingButton != null) huntingButton.interactable = isPrepare && hasAnimal;
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

        // 할당 가능한 유휴 워커 수 계산
        int available = GetIdleWorkerCount();
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
