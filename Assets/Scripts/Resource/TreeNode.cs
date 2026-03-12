using UnityEngine;

/// <summary>
/// 나무 자원 노드
/// - 자원량에 따라 스프라이트 단계 변경 (울창 → 앙상)
/// - 소진 시 TreeFallEffect로 쓰러짐 연출 후 Chopped 상태 전환
/// </summary>
public class TreeNode : ResourceNode
{
    [Header("나무 스프라이트 단계 (풍성 → 앙상 순서)")]
    public Sprite[] stageSprites;

    [Header("낙하 연출")]
    [Tooltip("TreeFallEffect 컴포넌트를 가진 프리팹")]
    public TreeFallEffect fallEffectPrefab;
    [Tooltip("낙하 연출 스프라이트 하단 크롭 오프셋 (픽셀 단위)")]
    public int bottomCropOffset = 0;

    [Header("애니메이션 트리거")]
    public string choppedAnimTrigger = "Chopped";

    // 초기 스프라이트 복원용
    private Sprite _originalSprite;

    protected override void Awake()
    {
        base.Awake();
        resourceType = ResourceType.Wood;

        if (_spriteRenderer != null)
            _originalSprite = _spriteRenderer.sprite;

        UpdateStageSprite();
    }

    protected override void OnHarvestPerformed(int amount)
    {
        UpdateStageSprite();
    }

    protected override void OnNodeDepleted()
    {
        if (fallEffectPrefab != null && _spriteRenderer != null)
        {
            // 낙하 연출 오브젝트 생성 후 현재 스프라이트 전달
            // 원본 렌더는 그대로 유지 — 밑동과 낙하 연출이 동시에 표시됨
            var effect = Instantiate(fallEffectPrefab, transform.position, transform.rotation);
            effect.Play(_spriteRenderer, bottomCropOffset, null);

            // effect 생성 즉시 Chopped 전환 (자연스러운 시각 처리)
            TriggerChopped();
        }
        else
        {
            // 프리팹 미설정 시 즉시 전환
            TriggerChopped();
        }
    }

    protected override void OnNodeReset()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = true;

            if (_originalSprite != null)
                _spriteRenderer.sprite = _originalSprite;
        }

        if (_animator != null)
            _animator.Rebind();

        gameObject.SetActive(true);
        UpdateStageSprite();
    }

    // -------------------------------------------------------
    // 내부
    // -------------------------------------------------------

    // Chopped 상태 전환
    private void TriggerChopped()
    {
        if (_animator != null)
            _animator.SetTrigger(choppedAnimTrigger);
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

    // -------------------------------------------------------
    // 기즈모
    // -------------------------------------------------------

    void OnDrawGizmosSelected()
    {
        if (bottomCropOffset <= 0) return;

        // 현재 스프라이트 참조 (에디터에서는 stageSprites[0] 폴백)
        var sr     = GetComponent<SpriteRenderer>();
        var sprite = sr != null ? sr.sprite : null;
        if (sprite == null && stageSprites != null && stageSprites.Length > 0)
            sprite = stageSprites[0];
        if (sprite == null) return;

        float ppu = sprite.pixelsPerUnit;

        // 크롭 라인 로컬 Y = (밑동 픽셀 오프셋) / ppu
        // sprite.pivot.y = 스프라이트 rect 기준 pivot 픽셀 위치
        float cropLocalY = (-sprite.pivot.y + bottomCropOffset) / ppu;
        Vector3 cropWorldPos = transform.position + Vector3.up * cropLocalY;

        float halfWidth = sprite.bounds.extents.x;

        // 크롭 라인 (빨간 실선)
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 1f);
        Gizmos.DrawLine(
            cropWorldPos - Vector3.right * halfWidth,
            cropWorldPos + Vector3.right * halfWidth
        );

        // 크롭될 하단 영역 (반투명 빨간 박스)
        float bottomLocalY = -sprite.pivot.y / ppu;
        float cropHeight   = bottomCropOffset / ppu;
        Vector3 boxCenter  = transform.position + Vector3.up * (bottomLocalY + cropHeight * 0.5f);
        Vector3 boxSize    = new Vector3(halfWidth * 2f, cropHeight, 0f);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.2f);
        Gizmos.DrawCube(boxCenter, boxSize);
    }
}
