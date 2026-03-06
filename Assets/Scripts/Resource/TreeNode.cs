using UnityEngine;

/// <summary>
/// 나무 자원 노드
/// - 채집 시 흔들림 애니메이션
/// - 자원량에 따라 스프라이트 단계 변경 (울창 → 앙상)
/// - 소진 시 쓰러짐 후 비활성화
/// </summary>
public class TreeNode : ResourceNode
{
    [Header("나무 스프라이트 단계 (풍성 → 앙상 순서)")]
    public Sprite[] stageSprites;

    [Header("애니메이션 트리거")]
    public string shakeAnimTrigger = "Shake";
    public string fallAnimTrigger  = "Fall";

    protected override void Awake()
    {
        base.Awake();
        resourceType = ResourceType.Wood;
        UpdateStageSprite();
    }

    protected override void OnHarvestPerformed(int amount)
    {
        if (_animator != null)
            _animator.SetTrigger(shakeAnimTrigger);

        UpdateStageSprite();
    }

    protected override void OnNodeDepleted()
    {
        if (_animator != null)
            _animator.SetTrigger(fallAnimTrigger);
        else
            gameObject.SetActive(false);
    }

    protected override void OnNodeReset()
    {
        UpdateStageSprite();
    }

    // 자원량 비율에 따라 스프라이트 단계 선택
    private void UpdateStageSprite()
    {
        if (stageSprites == null || stageSprites.Length == 0) return;
        if (_spriteRenderer == null) return;

        float ratio = (float)currentAmount / maxAmount;
        int index = Mathf.FloorToInt((1f - ratio) * (stageSprites.Length - 1));
        index = Mathf.Clamp(index, 0, stageSprites.Length - 1);
        _spriteRenderer.sprite = stageSprites[index];
    }
}