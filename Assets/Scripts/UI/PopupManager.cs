using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// 빈 화면 클릭 시 모든 팝업 닫기
///
/// 사용법:
///   PopupManager.Register(popup.gameObject)  → 팝업 등록
///   PopupManager.Unregister(popup.gameObject) → 팝업 해제
/// </summary>
public class PopupManager : MonoBehaviour
{
    public static PopupManager Instance { get; private set; }

    private readonly List<GameObject> _popups = new();

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        ServiceLocator.Register<PopupManager>(this);
    }

    void Update()
    {
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        // UI 위에서 클릭한 경우 무시
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            return;

        CloseAll();
    }

    public void Register(GameObject popup)
    {
        if (!_popups.Contains(popup))
            _popups.Add(popup);
    }

    public void Unregister(GameObject popup)
    {
        _popups.Remove(popup);
    }

    public void CloseAll()
    {
        foreach (var popup in _popups)
        {
            if (popup == null || !popup.activeSelf) continue;

            // ConstructionPopup
            var construction = popup.GetComponent<ConstructionPopup>();
            if (construction != null) { construction.Close(); continue; }

            // WorkerAllocationPopup
            var allocation = popup.GetComponent<WorkerAllocationPopup>();
            if (allocation != null) { allocation.Close(); continue; }

            // 그 외 일반 팝업
            popup.SetActive(false);
        }

        // ActionBarUI 상태 초기화
        var actionBar = Object.FindFirstObjectByType<ActionBarUI>();
        actionBar?.OnPopupClosedExternally();
    }
}