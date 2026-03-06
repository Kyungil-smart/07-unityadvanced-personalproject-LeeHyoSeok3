using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 오른쪽 패널 - Stage N 텍스트 + Go! 버튼
///
/// ── Hierarchy ──────────────────────────────────
/// PanelStage               ← 이 스크립트 부착
///   ├── TxtStageNumber     ← Text "Stage 1" (우 상단)
///   └── BtnGo              ← Button "Go!"
/// ───────────────────────────────────────────────
/// </summary>
public class StagePanel : MonoBehaviour
{
    [Header("UI 레퍼런스")]
    public TMP_Text txtStageNumber;
    public Button btnGo;

    [Header("텍스트 형식")]
    [Tooltip("{0} 자리에 스테이지 번호가 들어감")]
    public string stageNumberFormat = "Stage {0}";

    /// <summary>Go! 버튼 클릭 이벤트 (StageSelectScene이 구독)</summary>
    public System.Action OnGoClicked;

    void Start()
    {
        btnGo?.onClick.AddListener(() => OnGoClicked?.Invoke());
    }

    /// <summary>StageSelectScene에서 스테이지 변경 시 호출</summary>
    public void Refresh(StageData data, int stageNumber)
    {
        if (data == null) return;

        if (txtStageNumber != null)
            txtStageNumber.text = string.Format(stageNumberFormat, stageNumber);
    }
}