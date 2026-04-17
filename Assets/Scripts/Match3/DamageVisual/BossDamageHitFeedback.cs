using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>挂在 Boss 上：命中时把 Sprite / UI Image 颜色闪白并恢复，同时做较大振幅抖动。</summary>
[DisallowMultipleComponent]
public sealed class BossDamageHitFeedback : MonoBehaviour, IBossDamageVisualHitFeedback
{
    [SerializeField] private Collider2D hitCollider2D;
    [SerializeField] private SpriteRenderer[] spriteRenderers;
    [SerializeField] private Image[] uiImages;
    [SerializeField] private int flashCount = 5;
    [SerializeField] private float flashHalfPeriod = 0.022f;
    [SerializeField] private float shakeAmount = 0.18f;

    private Coroutine _flashRoutine;
    private Vector3 _originLocalPosition;

    public Collider2D HitCollider => hitCollider2D;

    private void Awake()
    {
        _originLocalPosition = transform.localPosition;
        if (hitCollider2D == null)
            hitCollider2D = GetComponent<Collider2D>();

        if (spriteRenderers == null || spriteRenderers.Length == 0)
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);

        if (uiImages == null || uiImages.Length == 0)
            uiImages = GetComponentsInChildren<Image>(true);
    }

    public void PlayHitFlash()
    {
        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(CoFlash());
    }

    private IEnumerator CoFlash()
    {
        var srColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                srColors[i] = spriteRenderers[i].color;
        }

        var imgColors = new Color[uiImages.Length];
        for (int i = 0; i < uiImages.Length; i++)
        {
            if (uiImages[i] != null)
                imgColors[i] = uiImages[i].color;
        }

        for (int n = 0; n < flashCount; n++)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == null) continue;
                spriteRenderers[i].color = Color.white;
            }

            for (int i = 0; i < uiImages.Length; i++)
            {
                if (uiImages[i] == null) continue;
                uiImages[i].color = Color.white;
            }

            Vector2 shake = Random.insideUnitCircle * shakeAmount;
            transform.localPosition = _originLocalPosition + new Vector3(shake.x, shake.y, 0f);

            yield return new WaitForSeconds(flashHalfPeriod);

            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (spriteRenderers[i] == null) continue;
                spriteRenderers[i].color = srColors[i];
            }

            for (int i = 0; i < uiImages.Length; i++)
            {
                if (uiImages[i] == null) continue;
                uiImages[i].color = imgColors[i];
            }

            shake = Random.insideUnitCircle * shakeAmount;
            transform.localPosition = _originLocalPosition + new Vector3(shake.x, shake.y, 0f);

            yield return new WaitForSeconds(flashHalfPeriod);
        }

        transform.localPosition = _originLocalPosition;
        _flashRoutine = null;
    }
}
