using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 게임 화면 자원 UI
///
/// 계층 구조:
///   ResourceUI (Canvas)
///     └─ Panel
///           ├─ WoodEntry  (ResourceEntry)
///           ├─ GoldEntry  (ResourceEntry)
///           └─ MeatEntry  (ResourceEntry)
///
/// 표시 형식:
///   Wood : 300
///   Gold : 200
///   Meat : 3 / 10
/// </summary>
public class ResourceUI : MonoBehaviour
{
    [Header("자원 텍스트")]
    public TMP_Text woodText;
    public TMP_Text goldText;
    public TMP_Text meatText;

    private ResourceInventory _inventory;

    void Start()
    {
        _inventory = ServiceLocator.Has<ResourceInventory>()
            ? ServiceLocator.Get<ResourceInventory>()
            : Object.FindFirstObjectByType<ResourceInventory>();

        // 초기값 표시
        RefreshAll();

        // 자원 변경 이벤트 구독
        EventBus.Subscribe<OnResourceChanged>(OnResourceChanged);
    }

    void OnDestroy()
    {
        EventBus.Unsubscribe<OnResourceChanged>(OnResourceChanged);
    }

    void OnResourceChanged(OnResourceChanged e)
    {
        switch (e.type)
        {
            case ResourceType.Wood: UpdateWood(e.current);  break;
            case ResourceType.Gold: UpdateGold(e.current);  break;
            case ResourceType.Meat: UpdateMeat(e.current);  break;
        }
    }

    void RefreshAll()
    {
        if (_inventory == null) return;
        UpdateWood(_inventory.Get(ResourceType.Wood));
        UpdateGold(_inventory.Get(ResourceType.Gold));
        UpdateMeat(_inventory.Get(ResourceType.Meat));
    }

    void UpdateWood(int value)
    {
        if (woodText != null)
            woodText.text = $"{value}";
    }

    void UpdateGold(int value)
    {
        if (goldText != null)
            goldText.text = $"{value}";
    }

    void UpdateMeat(int value)
    {
        int capacity = _inventory != null ? _inventory.meatCapacity : 0;
        if (meatText != null)
            meatText.text = $"{value}/{capacity}";
    }
}