using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("BGM Settings")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioClip[] bgmClips;
    [SerializeField] private float bgmVolume = 1f;
    [SerializeField] private float bgmPitch = 0.45f;
    [SerializeField] private bool loopBGM = true;

    [Header("Step Sound Settings")]
    [SerializeField] private AudioSource stepSource;
    [SerializeField] private float stepVolume = 0.5f;
    private AudioClip[] stepSoundClips;
    private bool isStepSoundActive = false;
    private Player player;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = loopBGM;
            bgmSource.volume = bgmVolume;
            bgmSource.pitch = bgmPitch;
            bgmSource.playOnAwake = false;
        }

        if (stepSource == null)
        {
            stepSource = gameObject.AddComponent<AudioSource>();
            stepSource.loop = false;
            stepSource.volume = stepVolume;
            stepSource.playOnAwake = false;
        }

        LoadStepSounds();
    }

    private void LoadStepSounds()
    {
        stepSoundClips = new AudioClip[8];
        for (int i = 1; i <= 8; i++)
        {
            AudioClip clip = Resources.Load<AudioClip>($"Audio/FootStep/{i}");
            if (clip != null)
            {
                stepSoundClips[i - 1] = clip;
            }
            else
            {
                Debug.LogWarning($"[AudioManager] Failed to load step sound: {i}");
            }
        }
    }

    private void Update()
    {
        if (!isStepSoundActive || stepSource == null)
            return;

        // 场景切换后旧 Player 已销毁，需重新绑定，否则 Update 无法续播
        if (player == null)
            player = FindFirstObjectByType<Player>();

        // 不再用 isGrounded() 暂停脚步：新场景若 Ground 图层/射线与 whatIsGround 不一致，
        // 会一直被 Pause 且走不到「clip 结束再播」的分支，表现为只有第一声。
        // 起跳/离地时由 PlayerMoveState.Exit -> StopStepSound 停止即可。
        if (!stepSource.isPlaying)
            PlayRandomStepSound();
    }
    public void Start()
    {
        PlayBGM(0);
    }
    public void PlayBGM(int clipIndex)
    {
        if (clipIndex < 0 || clipIndex >= bgmClips.Length)
        {
            Debug.LogError($"BGM clip index {clipIndex} is out of range");
            return;
        }

        if (bgmSource.clip == bgmClips[clipIndex] && bgmSource.isPlaying)
        {
            return;
        }

        bgmSource.clip = bgmClips[clipIndex];
        bgmSource.Play();
    }

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogError("BGM clip is null");
            return;
        }

        if (bgmSource.clip == clip && bgmSource.isPlaying)
        {
            return;
        }

        bgmSource.clip = clip;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        bgmSource.Stop();
    }

    public void PauseBGM()
    {
        bgmSource.Pause();
    }

    public void ResumeBGM()
    {
        bgmSource.UnPause();
    }

    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        bgmSource.volume = bgmVolume;
    }

    public bool IsBGMPlaying()
    {
        return bgmSource.isPlaying;
    }

    #region Step Sound
    private void PlayRandomStepSound()
    {
        if (stepSource == null || stepSoundClips == null || stepSoundClips.Length == 0) return;

        for (int t = 0; t < 8; t++)
        {
            int randomIndex = Random.Range(0, stepSoundClips.Length);
            AudioClip clip = stepSoundClips[randomIndex];
            if (clip == null) continue;

            stepSource.clip = clip;
            stepSource.Play();
            return;
        }
    }

    public void PlayStepSound()
    {
        if (stepSource == null) return;

        player = FindFirstObjectByType<Player>();

        isStepSoundActive = true;

        if (!stepSource.isPlaying)
            PlayRandomStepSound();
    }

    public void StopStepSound()
    {
        if (stepSource == null) return;

        isStepSoundActive = false;
        stepSource.Stop();
    }

    public bool IsStepSoundPlaying()
    {
        return stepSource != null && stepSource.isPlaying;
    }

    public void SetStepVolume(float volume)
    {
        stepVolume = Mathf.Clamp01(volume);
        if (stepSource != null)
        {
            stepSource.volume = stepVolume;
        }
    }
    #endregion
}
