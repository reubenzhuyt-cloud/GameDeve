using System.Collections;
using UnityEngine;

/// <summary>
/// 单例：拖入 LightEffect 等粒子 Prefab（不要挂在 Player 上）。
/// Play(duration)：实例化 → 每帧跟随玩家 → 到时销毁。由 PlayerLightState 调用。
/// </summary>
public class EffectManager : MonoBehaviour
{
    public static EffectManager instance { get; private set; }

    [Header("光效预制体（例：Assets/Perfabs/LightEffect）")]
    [SerializeField] private GameObject particleEffectPrefab;

    private Coroutine playRoutine;
    private GameObject spawnedInstance;
    private Transform cachedPlayer;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);
    }

    private Transform GetPlayerTransform()
    {
        if (cachedPlayer != null)
            return cachedPlayer;

        Player p = FindFirstObjectByType<Player>();
        if (p != null)
            cachedPlayer = p.transform;
        return cachedPlayer;
    }

    /// <summary>
    /// 播放特效：内部启动协程。再次调用会先停掉上一次并清掉实例，可重复用。
    /// </summary>
    public void Play(float duration)
    {
        Stop();
        if (particleEffectPrefab == null || duration <= 0f)
            return;

        playRoutine = StartCoroutine(PlayRoutine(duration));
    }

    private IEnumerator PlayRoutine(float duration)
    {
        Transform player = GetPlayerTransform();
        Vector3 pos = player != null ? player.position : transform.position;
        spawnedInstance = Instantiate(particleEffectPrefab, pos, particleEffectPrefab.transform.rotation);

        PlayAllParticleSystems(spawnedInstance);

        float t = 0f;
        while (t < duration)
        {
            player = GetPlayerTransform();
            if (player != null && spawnedInstance != null)
                spawnedInstance.transform.position = player.position;

            t += Time.unscaledDeltaTime;
            yield return null;
        }

        ClearSpawnedInstance();
        playRoutine = null;
    }

    private static void PlayAllParticleSystems(GameObject root)
    {
        if (root == null)
            return;
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = ps.main;
            main.useUnscaledTime = true;
            ps.Play(true);
        }
    }

    private static void StopAndClearParticles(GameObject root)
    {
        if (root == null)
            return;
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void ClearSpawnedInstance()
    {
        if (spawnedInstance == null)
            return;
        StopAndClearParticles(spawnedInstance);
        Destroy(spawnedInstance);
        spawnedInstance = null;
    }

    /// <summary>
    /// 打断协程并立刻销毁当前特效实例。
    /// </summary>
    public void Stop()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }
        ClearSpawnedInstance();
    }
}
