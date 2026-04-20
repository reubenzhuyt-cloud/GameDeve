using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>挂在 Boss 上：命中时闪白；振动为若干正弦波叠加，每次受击延长振动时间并叠一层波。</summary>
[DisallowMultipleComponent]
public sealed class BossDamageHitFeedback : MonoBehaviour, IBossDamageVisualHitFeedback
{
    private const int MaxShakeLayers = 48;

    [SerializeField] private Collider2D hitCollider2D;
    [SerializeField] private SpriteRenderer[] spriteRenderers;
    [SerializeField] private Image[] uiImages;
    [SerializeField] private int flashCount = 5;
    [SerializeField] private float flashHalfPeriod = 0.022f;

    [Header("Shake (sine superposition)")]
    [Tooltip("每次受击将振动结束时刻推到「当前时间 + 该值」，连续受击会不断刷新")]
    [SerializeField] private float shakeDurationSeconds = 0.45f;
    [Tooltip("叠加正弦波的周期 T（秒），角频率 ω=2π/T")]
    [SerializeField] private float shakeWavePeriodSeconds = 0.14f;
    [Tooltip("位移 = 系数 × Σ(归一化速度分量 × sin(ωt+φ))")]
    [SerializeField] private float shakeAmplitudeCoefficient = 0.22f;

    private Coroutine _flashRoutine;
    private Vector3 _originLocalPosition;

    private readonly List<ShakeLayer> _shakeLayers = new List<ShakeLayer>(8);
    private float _shakeEndTime;

    private struct ShakeLayer
    {
        public float Ax;
        public float Ay;
        public float Phase;
    }

    public Collider2D HitCollider => hitCollider2D;

    /// <summary>每次受击刷新的振动总时长（从最后一次受击起算）。</summary>
    public float ShakeDurationSeconds
    {
        get => shakeDurationSeconds;
        set => shakeDurationSeconds = Mathf.Max(0.01f, value);
    }

    /// <summary>叠加正弦的周期（秒）。</summary>
    public float ShakeWavePeriodSeconds
    {
        get => shakeWavePeriodSeconds;
        set => shakeWavePeriodSeconds = Mathf.Max(0.01f, value);
    }

    /// <summary>正弦位移振幅系数。</summary>
    public float ShakeAmplitudeCoefficient
    {
        get => shakeAmplitudeCoefficient;
        set => shakeAmplitudeCoefficient = Mathf.Max(0f, value);
    }

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

    private void OnDisable()
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }

        _shakeLayers.Clear();
        _shakeEndTime = 0f;
        transform.localPosition = _originLocalPosition;
    }

    public void PlayHitFlash()
    {
        PushRandomShakeLayer();
        _shakeEndTime = Time.time + shakeDurationSeconds;

        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(CoFlash());
    }

    private void PushRandomShakeLayer()
    {
        Vector2 dir = Random.insideUnitCircle;
        if (dir.sqrMagnitude < 1e-8f)
            dir = Vector2.right;
        else
            dir.Normalize();

        float phase = Random.Range(0f, Mathf.PI * 2f);
        if (_shakeLayers.Count >= MaxShakeLayers)
            _shakeLayers.RemoveAt(0);
        _shakeLayers.Add(new ShakeLayer { Ax = dir.x, Ay = dir.y, Phase = phase });
    }

    private void Update()
    {
        if (Time.time >= _shakeEndTime)
        {
            if (_shakeLayers.Count > 0)
            {
                _shakeLayers.Clear();
                transform.localPosition = _originLocalPosition;
            }

            return;
        }

        float period = Mathf.Max(1e-4f, shakeWavePeriodSeconds);
        float omega = 2f * Mathf.PI / period;
        float t = Time.time;
        float coeff = shakeAmplitudeCoefficient;
        float sx = 0f;
        float sy = 0f;
        for (int i = 0; i < _shakeLayers.Count; i++)
        {
            ShakeLayer L = _shakeLayers[i];
            float s = Mathf.Sin(omega * t + L.Phase);
            sx += coeff * L.Ax * s;
            sy += coeff * L.Ay * s;
        }

        transform.localPosition = _originLocalPosition + new Vector3(sx, sy, 0f);
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

            yield return new WaitForSeconds(flashHalfPeriod);
        }

        _flashRoutine = null;
    }
}
