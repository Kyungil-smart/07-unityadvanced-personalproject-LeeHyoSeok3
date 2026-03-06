using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 왼쪽 패널 - 스테이지 클리어 Rank 표시
///
/// 별 3개 Image의 color를 tint로 변경해 몇 스타 클리어인지 표현
///
/// ── Hierarchy ──────────────────────────────────
/// PanelRank                ← 이 스크립트 부착
///   ├── TxtRankLabel       ← Text "Rank" (고정, Inspector에서 직접 입력)
///   └── StarsGroup
///         ├── StarImage0   ← Image (1번째 별 스프라이트)
///         ├── StarImage1   ← Image (2번째 별 스프라이트)
///         └── StarImage2   ← Image (3번째 별 스프라이트)
/// ───────────────────────────────────────────────
///
/// Inspector 설정:
///   Star Images[0] → StarImage0
///   Star Images[1] → StarImage1
///   Star Images[2] → StarImage2
///   Active Tint    → (1, 0.85, 0.15, 1)  골드
///   Inactive Tint  → (0.3, 0.3, 0.3, 0.5) 회색 반투명
/// </summary>
public class StageInfoPanel : MonoBehaviour
{
    [Header("별 이미지 (인덱스 0 = 1스타, 1 = 2스타, 2 = 3스타)")]
    public Image[] starImages = new Image[3];

    [Header("Tint 색상")]
    public Color activeTint   = new Color(1f, 0.85f, 0.15f, 1f);
    public Color inactiveTint = new Color(0.3f, 0.3f, 0.3f, 0.5f);

    /// <summary>StageSelectScene에서 스테이지 변경 시 호출</summary>
    public void Refresh(StageData data)
    {
        if (data == null) return;

        int stars = data.isClear ? Mathf.Clamp(data.bestStars, 0, 3) : 0;

        for (int i = 0; i < starImages.Length; i++)
        {
            if (starImages[i] == null) continue;
            starImages[i].color = (i < stars) ? activeTint : inactiveTint;
        }
    }
}