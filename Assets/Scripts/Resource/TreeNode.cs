using System.Collections;
using UnityEngine;

/// <summary>
/// 나무 자원 노드
/// - 채집 시 흔들림 애니메이션
/// - 자원량에 따라 스프라이트 단계 변경 (울창 → 앙상)
/// - 소진 시 쓰러짐 후 Stump 스프라이트로 교체
/// </summary>
public class TreeNode : ResourceNode
{
    [Header("나무 스프라이트 단계 (풍성 → 앙상 순서)")]
    public Sprite[] stageSprites;

    [Header("Stump 스프라이트 (소진 후 표시)")]
    public Sprite stumpSprite;
    [Tooltip("Fall 애니메이션 후 Stump로 교체될 때까지 대기 시간 (초)")]
    public float fallDuration = 1f;

    [Header("애니메이션 트리거")]
    public string shakeAnimTrigger = "Shake";
    public string fallAnimTrigger  = "Fall";

    // 초기 스프라이트 복원용
    private Sprite _originalSprite;

    protected override void Awake()
    {
        base.Awake();
        resourceType = ResourceType.Wood;

        // 초기 스프라이트 저장
        if (_spriteRenderer != null)
            _originalSprite = _spriteRenderer.sprite;

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
        {
            _animator.SetTrigger(fallAnimTrigger);
            // Fall 애니메이션 후 Stump 스프라이트로 교체
            StartCoroutine(SwitchToStumpAfterFall());
        }
        else
        {
            // 애니메이터 없으면 즉시 Stump 표시
            ApplyStumpSprite();
        }
    }

    protected override void OnNodeReset()
    {
        StopAllCoroutines();

        // 스프라이트 초기화 후 단계 갱신
        if (_spriteRenderer != null && _originalSprite != null)
            _spriteRenderer.sprite = _originalSprite;

        if (_animator != null)
            _animator.Rebind();

        gameObject.SetActive(true);
        UpdateStageSprite();
    }

    // Fall 애니메이션 대기 후 Stump 스프라이트 적용
    private IEnumerator SwitchToStumpAfterFall()
    {
        yield return new WaitForSeconds(fallDuration);
        ApplyStumpSprite();
    }

    private void ApplyStumpSprite()
    {
        if (_spriteRenderer == null) return;

        if (stumpSprite != null)
            _spriteRenderer.sprite = stumpSprite;
        else
            gameObject.SetActive(false);
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