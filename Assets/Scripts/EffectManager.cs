using System.Collections;
using UnityEngine;

/// <summary>
/// 拖入粒子特效 Prefab。Play(duration) 用协程：生成实例 → 每帧跟玩家 → 时间到停粒子并 Destroy。
/// </summary>
public class EffectManager : MonoBehaviour
{
    public static EffectManager instance { get; private set; }

    [Header("粒子特效预制体（根物体上可有 ParticleSystem，或在子物体上）")]
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
