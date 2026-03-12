using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 나무 소진 시 쓰러지는 연출 전용 오브젝트
/// - 스프라이트 하단을 bottomCropOffset 픽셀만큼 잘라 밑동이 사라진 것처럼 표현
/// - Z축 회전으로 쓰러지는 연출
/// - 쓰러짐 완료(90°) 시 EffectSpawnController로 이펙트 발동
/// - 연출 완료 후 onComplete 콜백 호출 뒤 자체 소멸
/// </summary>
public class TreeFallEffect : MonoBehaviour
{
    [Header("낙하 애니메이션")]
    public float fallDuration = 0.6f;
    [Tooltip("낙하 이징 곡선 — EaseIn 권장 (느리게 시작 → 빠르게 쓰러짐)")]
    public AnimationCurve fallCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("쓰러지는 방향")]
    [Tooltip("양수: 오른쪽, 음수: 왼쪽, 0: 랜덤")]
    public float fallDirectionSign = 0f;

    // ---

    private SpriteRenderer        _spriteRenderer;
    private EffectSpawnController _effectController;
    private Action                _onComplete;

    void Awake()
    {
        _spriteRenderer   = GetComponent<SpriteRenderer>();
        _effectController = GetComponent<EffectSpawnController>();
    }

    // -------------------------------------------------------
    // 공개 API
    // -------------------------------------------------------

    /// <summary>
    /// 낙하 연출 시작
    /// </summary>
    /// <param name="source">원본 나무의 SpriteRenderer (스프라이트 + 소팅 레이어 복사)</param>
    /// <param name="bottomCropOffset">하단에서 잘라낼 픽셀 수 (텍스처 Read/Write Enabled 필요)</param>
    /// <param name="onComplete">연출 완료 후 호출할 콜백</param>
    public void Play(SpriteRenderer source, int bottomCropOffset, Action onComplete)
    {
        if (source != null)
        {
            Sprite cropped = BuildCroppedSprite(source.sprite, bottomCropOffset, out float yOffset);

            if (yOffset > 0f)
            {
                // pivot이 rect 아래로 벗어남:
                // 자식 오브젝트에 스프라이트 배치 → 회전 중심(transform.position = 밑동)은 고정,
                // 스프라이트 비주얼만 yOffset만큼 위로 올려 원본 여백을 보존
                if (_spriteRenderer != null)
                    _spriteRenderer.enabled = false;

                var visual = new GameObject("_Visual");
                visual.transform.SetParent(transform, false);
                visual.transform.localPosition = new Vector3(0f, yOffset, 0f);

                var sr = visual.AddComponent<SpriteRenderer>();
                sr.sprite         = cropped;
                sr.sortingLayerID = source.sortingLayerID;
                sr.sortingOrder   = source.sortingOrder + 1; // 밑동(원본) 위에 그려짐
            }
            else if (_spriteRenderer != null)
            {
                // pivot이 rect 안에 있으므로 기존 SpriteRenderer 그대로 사용
                _spriteRenderer.sprite         = cropped;
                _spriteRenderer.sortingLayerID = source.sortingLayerID;
                _spriteRenderer.sortingOrder   = source.sortingOrder + 1; // 밑동(원본) 위에 그려짐
            }
        }

        _onComplete = onComplete;
        StartCoroutine(FallRoutine());
    }

    // -------------------------------------------------------
    // 내부
    // -------------------------------------------------------

    /// <summary>
    /// 원본 스프라이트의 하단을 bottomCropOffset 픽셀만큼 잘라낸 새 스프라이트 생성
    /// </summary>
    private Sprite BuildCroppedSprite(Sprite original, int bottomCropOffset, out float yOffset)
    {
        yOffset = 0f;
        if (original == null) return null;
        if (bottomCropOffset <= 0) return original;

        // 크롭 후 높이가 0 이하가 되지 않도록 클램프
        int cropAmount = Mathf.Clamp(bottomCropOffset, 0, (int)original.textureRect.height - 1);

        // 하단을 cropAmount 픽셀 잘라낸 새 rect
        var originalRect = original.textureRect;
        var newRect = new Rect(
            originalRect.x,
            originalRect.y + cropAmount,
            originalRect.width,
            originalRect.height - cropAmount
        );

        // 새 rect 기준 pivot Y (음수면 pivot이 rect 아래로 벗어남)
        float rawPivotY = original.pivot.y - cropAmount;

        // pivot이 rect 아래로 벗어난 만큼: 자식 스프라이트를 위로 올릴 거리 반환
        if (rawPivotY < 0f)
            yOffset = -rawPivotY / original.pixelsPerUnit;

        var newPivot = new Vector2(
            original.pivot.x / newRect.width,
            Mathf.Max(0f, rawPivotY) / newRect.height
        );
        newPivot.x = Mathf.Clamp01(newPivot.x);
        newPivot.y = Mathf.Clamp01(newPivot.y);

        return Sprite.Create(original.texture, newRect, newPivot, original.pixelsPerUnit);
    }

    private IEnumerator FallRoutine()
    {
        // 방향 결정 (1 = 오른쪽, -1 = 왼쪽)
        float dir = fallDirectionSign == 0f
            ? (UnityEngine.Random.value < 0.5f ? 1f : -1f)
            : Mathf.Sign(fallDirectionSign);

        // 회전 중심 = transform.position (나무 밑동), Z 회전만 적용
        float elapsed = 0f;

        while (elapsed < fallDuration)
        {
            float t     = fallCurve.Evaluate(elapsed / fallDuration);
            float angle = t * 90f * dir;
            transform.rotation = Quaternion.Euler(0f, 0f, -angle);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 최종 각도 고정
        transform.rotation = Quaternion.Euler(0f, 0f, -90f * dir);

        // 쓰러짐 완료 → 이펙트 발동
        _effectController?.Play();

        // 완료 콜백 (SpriteRenderer 재활성화 등)
        _onComplete?.Invoke();

        Destroy(gameObject);
    }
}
