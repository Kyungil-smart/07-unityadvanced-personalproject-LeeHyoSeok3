using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 건설 목록 팝업의 건물 항목 버튼
///
/// 계층 구조:
///   BuildingEntryButton
///     ├─ BuildingIcon (Image + Button) ← buildingIcon, iconButton 연결
///     ├─ AvatarIcon   (Image)          ← avatarIcon 연결
///     ├─ NameIcon     (Image)          ← nameIcon 연결
///     ├─ NameText     (TMP)            ← nameText 연결
///     └─ CostText     (TMP)            ← costText 연결
/// </summary>
public class BuildingEntryButton : MonoBehaviour
{
    [Header("아이콘 참조")]
    public Image  buildingIcon;
    public Image  avatarIcon;
    public Image  nameIcon;
    public Button iconButton;   // BuildingIcon의 Button 컴포넌트

    [Header("텍스트 참조")]
    public TMP_Text nameText;
    public TMP_Text woodCostText;
    public TMP_Text goldCostText;
    public TMP_Text meatCostText;

    private BuildingData      _data;
    private ConstructionPopup _popup;

    public void Init(BuildingData data, ConstructionPopup popup)
    {
        _data  = data;
        _popup = popup;

        // BuildingIcon
        if (buildingIcon != null)
        {
            buildingIcon.sprite = data.buildingIcon;
            buildingIcon.SetNativeSize();

            // 아이콘 높이 기준으로 Layout Element 자동 설정
            var layout = GetComponent<LayoutElement>();
            if (layout == null) layout = gameObject.AddComponent<LayoutElement>();

            float iconHeight = buildingIcon.rectTransform.sizeDelta.y;
            layout.minHeight       = iconHeight;
            layout.preferredHeight = iconHeight;
        }

        // AvatarIcon
        if (avatarIcon != null)
        {
            avatarIcon.sprite = data.avatarIcon;
            // avatarIcon.SetNativeSize();
        }

        // NameIcon
        if (nameIcon != null)
        {
            nameIcon.sprite = data.nameIcon;
            // nameIcon.SetNativeSize();
        }
        if (nameText     != null) nameText.text     = data.buildingName;
        if (woodCostText != null) woodCostText.text = data.woodCost > 0 ? $"{data.woodCost}" : "0";
        if (goldCostText != null) goldCostText.text = data.goldCost > 0 ? $"{data.goldCost}" : "0";
        if (meatCostText != null) meatCostText.text = data.meatCost > 0 ? $"{data.meatCost}" : "0";

        iconButton?.onClick.AddListener(OnClick);

        RefreshInteractable();
        EventBus.Subscribe<OnResourceChanged>(OnResourceChanged);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<OnResourceChanged>(OnResourceChanged);
    }

    void OnResourceChanged(OnResourceChanged e)
    {
        RefreshInteractable();
    }

    void RefreshInteractable()
    {
        if (_data == null) return;

        bool canAfford = true;
        if (ServiceLocator.Has<ResourceInventory>())
            canAfford = ServiceLocator.Get<ResourceInventory>().CanAffordAll(_data.GetBuildCost());

        if (iconButton != null) iconButton.interactable = canAfford;

        // 자원 부족 시 아이콘 반투명 + 텍스트 빨간색
        Color normal      = Color.white;
        Color dimmed      = new Color(1f, 1f, 1f, 0.4f);
        if (buildingIcon != null) buildingIcon.color = canAfford ? normal : dimmed;
        if (avatarIcon   != null) avatarIcon.color   = canAfford ? normal : dimmed;
        if (nameIcon     != null) nameIcon.color     = canAfford ? normal : dimmed;
        if (woodCostText != null) woodCostText.color = ServiceLocator.Has<ResourceInventory>() && ServiceLocator.Get<ResourceInventory>().CanAfford(ResourceType.Wood, _data.woodCost) ? normal : Color.red;
        if (goldCostText != null) goldCostText.color = ServiceLocator.Has<ResourceInventory>() && ServiceLocator.Get<ResourceInventory>().CanAfford(ResourceType.Gold, _data.goldCost) ? normal : Color.red;
        if (meatCostText != null) meatCostText.color = ServiceLocator.Has<ResourceInventory>() && ServiceLocator.Get<ResourceInventory>().CanAfford(ResourceType.Meat, _data.meatCost) ? normal : Color.red;
    }

    void OnClick()
    {
        _popup?.SelectBuilding(_data);
    }


}
