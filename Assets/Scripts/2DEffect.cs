using UnityEngine;

public class Effect : MonoBehaviour
{
    [Header("Effect Settings")]
    [SerializeField] private GameObject effectPrefab;
    [SerializeField] private float duration = 3f;

    private GameObject currentEffect;
    private float timer;

    public void PlayEffect(Vector3 position)
    {
        // 创建prefab
        if (effectPrefab != null)
        {
            currentEffect = Instantiate(effectPrefab, position, Quaternion.identity);
            timer = duration;

            Debug.Log("[2DEffect] Effect created at: " + position);
        }
        else
        {
            Debug.LogWarning("[2DEffect] Effect prefab is not assigned!");
        }
    }

    private void Update()
    {
        if (currentEffect != null)
        {
            timer -= Time.deltaTime;

            if (timer <= 0)
            {
                // 时间到了，关闭效果
                if (currentEffect != null)
                {
                    Destroy(currentEffect);
                    currentEffect = null;
                    Debug.Log("[2DEffect] Effect destroyed after duration");
                }
            }
        }
    }

    public void StopEffect()
    {
        if (currentEffect != null)
        {
            Destroy(currentEffect);
            currentEffect = null;
            Debug.Log("[2DEffect] Effect stopped manually");
        }
    }
}