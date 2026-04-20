using UnityEngine;

/// <summary>
/// Boss 关专用音频：Boss BGM、飞弹命中受击、棋盘一轮消除完成时的消除音效。
/// 挂在 Boss 场景任意激活物体上；<see cref="AudioManager"/> 存在时切换 BGM，离开时恢复原片段。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossAudioControl : MonoBehaviour
{
    [Header("Clips")]
    [Tooltip("Boss 关循环 BGM")]
    [SerializeField] private AudioClip bossBattleBgm;
    [Tooltip("秒；>0 时从第二次循环起从该时刻开始播（跳过片头）。0 表示整段自然循环。")]
    [SerializeField] private double bgmLoopRestartTime;
    [Tooltip("飞弹命中 Boss 时的受击音效")]
    [SerializeField] private AudioClip bossHitSfx;
    [Tooltip("棋盘一轮消除计分完成时的消除音效")]
    [SerializeField] private AudioClip eliminationSfx;

    [Header("Refs (optional auto-find)")]
    [SerializeField] private Match3Manager match3Manager;
    [SerializeField] private Match3DamageSwarmManager damageSwarm;

    [Header("Levels")]
    [SerializeField] [Range(0f, 1f)] private float hitSfxVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float eliminationSfxVolume = 1f;
    [Tooltip("受击音效最短间隔（秒），减轻连中时的刺耳叠加")]
    [SerializeField] private float hitSfxCooldownSeconds = 0.07f;

    private AudioSource _sfx;
    private AudioClip _restoreBgmClip;
    private AudioSource _localBgm;
    private float _nextHitSfxTime;
    private bool _bossBgmStarted;
    private bool _localBgmManualLoop;

    private void Awake()
    {
        Transform holder = transform.Find("BossAudio_Sfx");
        if (holder == null)
        {
            var go = new GameObject("BossAudio_Sfx");
            go.transform.SetParent(transform, false);
            holder = go.transform;
        }

        _sfx = holder.GetComponent<AudioSource>();
        if (_sfx == null)
        {
            _sfx = holder.gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _sfx.loop = false;
            _sfx.spatialBlend = 0f;
        }
    }

    private void OnEnable()
    {
        if (match3Manager == null)
            match3Manager = FindFirstObjectByType<Match3Manager>();
        if (damageSwarm == null)
            damageSwarm = FindFirstObjectByType<Match3DamageSwarmManager>();

        if (match3Manager != null)
            match3Manager.OnEliminationWaveCompleted += HandleEliminationWave;
        if (damageSwarm != null)
            damageSwarm.OnBossDamagedByOrb += HandleBossDamagedByOrb;

        if (!_bossBgmStarted && bossBattleBgm != null)
        {
            _bossBgmStarted = true;
            StartBossBgm();
        }
    }

    private void OnDisable()
    {
        if (match3Manager != null)
            match3Manager.OnEliminationWaveCompleted -= HandleEliminationWave;
        if (damageSwarm != null)
            damageSwarm.OnBossDamagedByOrb -= HandleBossDamagedByOrb;

        RestoreWorldBgm();
        _bossBgmStarted = false;
        _localBgmManualLoop = false;
    }

    private void LateUpdate()
    {
        TickLocalBgmLoopRestart();
    }

    private void TickLocalBgmLoopRestart()
    {
        if (!_localBgmManualLoop || _localBgm == null || !_localBgm.isPlaying || _localBgm.clip == null)
            return;

        float len = _localBgm.clip.length;
        if (len <= 0.1f)
            return;

        if (_localBgm.time < len - 0.08f)
            return;

        float restart = Mathf.Clamp((float)bgmLoopRestartTime, 0f, len - 0.01f);
        _localBgm.Stop();
        _localBgm.time = restart;
        _localBgm.Play();
    }

    private void StartBossBgm()
    {
        if (bossBattleBgm == null)
            return;

        if (AudioManager.instance != null)
        {
            _restoreBgmClip = AudioManager.instance.CurrentBGMClip;
            AudioManager.instance.PlayBGM(bossBattleBgm);
            if (bgmLoopRestartTime > 0.0)
                AudioManager.instance.SetBgmLoopRestartFromSeconds(bgmLoopRestartTime);
            return;
        }

        if (_localBgm == null)
        {
            var go = new GameObject("BossBgmLocal");
            go.transform.SetParent(transform, false);
            _localBgm = go.AddComponent<AudioSource>();
            _localBgm.playOnAwake = false;
            _localBgm.spatialBlend = 0f;
        }

        _localBgmManualLoop = bgmLoopRestartTime > 0.0;
        _localBgm.loop = !_localBgmManualLoop;
        _localBgm.clip = bossBattleBgm;
        _localBgm.Play();
    }

    private void RestoreWorldBgm()
    {
        if (_localBgm != null)
        {
            _localBgm.Stop();
            _localBgm.clip = null;
            _localBgm.loop = true;
        }

        _localBgmManualLoop = false;

        if (bossBattleBgm == null)
            return;

        if (AudioManager.instance != null)
        {
            AudioManager.instance.ClearBgmLoopRestartOverride();
            if (_restoreBgmClip != null)
                AudioManager.instance.PlayBGM(_restoreBgmClip);
            else
                AudioManager.instance.StopBGM();
        }

        _restoreBgmClip = null;
    }

    private void HandleBossDamagedByOrb(int damage)
    {
        if (damage <= 0 || bossHitSfx == null || _sfx == null)
            return;
        if (Time.time < _nextHitSfxTime)
            return;
        _nextHitSfxTime = Time.time + Mathf.Max(0.01f, hitSfxCooldownSeconds);
        _sfx.PlayOneShot(bossHitSfx, hitSfxVolume);
    }

    private void HandleEliminationWave()
    {
        if (eliminationSfx == null || _sfx == null)
            return;
        _sfx.PlayOneShot(eliminationSfx, eliminationSfxVolume);
    }
}
