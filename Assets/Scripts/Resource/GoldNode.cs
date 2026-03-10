using UnityEngine;

/// <summary>
/// 금광 자원 노드
/// - 채굴 애니메이션 + 파티클
/// - 소진 시 잔해 스프라이트로 교체
/// </summary>
public class GoldNode : ResourceNode
{
    [Header("소진 후 잔해 스프라이트")]
    public Sprite depletedSprite;
    [Tooltip("소진 시 적용할 색상 (어둡게)")]
    public Color depletedColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    [Header("채굴 파티클")]
    public ParticleSystem mineParticle;

    [Header("애니메이션 트리거")]
    public string mineAnimTrigger    = "Mine";
    public string depleteAnimTrigger = "Deplete";

    // 초기 스프라이트/색상 복원용
    private Sprite _originalSprite;
    private Color  _originalColor;

    protected override void Awake()
    {
        base.Awake();
        resourceType = ResourceType.Gold;

        if (_spriteRenderer != null)
        {
            _originalSprite = _spriteRenderer.sprite;
            _originalColor  = _spriteRenderer.color;
        }
    }

    protected override void OnHarvestPerformed(int amount)
    {
        if (_animator != null)
            _animator.SetTrigger(mineAnimTrigger);

        if (mineParticle != null)
            mineParticle.Play();
    }

    protected override void OnNodeDepleted()
    {
        if (_animator != null)
        {
            _animator.SetTrigger(depleteAnimTrigger);
            // 애니메이션 정지
            _animator.enabled = false;
        }

        // 잔해 스프라이트 교체 및 채색 낮추기
        if (_spriteRenderer != null)
        {
            if (depletedSprite != null)
                _spriteRenderer.sprite = depletedSprite;

            _spriteRenderer.color = depletedColor;
        }

        if (depletedSprite == null && _animator == null)
            gameObject.SetActive(false);
    }

    protected override void OnNodeReset()
    {
        // 스프라이트 / 색상 / 애니메이터 복원
        if (_spriteRenderer != null)
        {
            if (_originalSprite != null)
                _spriteRenderer.sprite = _originalSprite;
            _spriteRenderer.color = _originalColor;
        }

        if (_animator != null)
            _animator.enabled = true;

        gameObject.SetActive(true);
    }
}