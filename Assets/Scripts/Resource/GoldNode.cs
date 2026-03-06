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

    [Header("채굴 파티클")]
    public ParticleSystem mineParticle;

    [Header("애니메이션 트리거")]
    public string mineAnimTrigger    = "Mine";
    public string depleteAnimTrigger = "Deplete";

    protected override void Awake()
    {
        base.Awake();
        resourceType = ResourceType.Gold;
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
            _animator.SetTrigger(depleteAnimTrigger);

        // 잔해 스프라이트로 교체 후 비활성화
        if (depletedSprite != null && _spriteRenderer != null)
            _spriteRenderer.sprite = depletedSprite;
        else
            gameObject.SetActive(false);
    }

    protected override void OnNodeReset()
    {
        // 초기 스프라이트 복원은 Inspector 기본값으로
        gameObject.SetActive(true);
    }
}