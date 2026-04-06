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
        if (isStepSoundActive)
        {
            if (player != null && !player.isGrounded())
            {
                stepSource.Pause();
            }
            else if (!stepSource.isPlaying)
            {
                stepSource.UnPause();
                PlayRandomStepSound();
            }
        }
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

        int randomIndex = Random.Range(0, stepSoundClips.Length);
        AudioClip clip = stepSoundClips[randomIndex];
        
        if (clip == null) return;

        stepSource.clip = clip;
        stepSource.Play();
    }

    public void PlayStepSound()
    {
        if (stepSource == null) return;

        if (player == null)
        {
            player = FindObjectOfType<Player>();
        }

        isStepSoundActive = true;

        if (player != null && !player.isGrounded())
        {
            return;
        }

        if (!stepSource.isPlaying)
        {
            PlayRandomStepSound();
        }
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
