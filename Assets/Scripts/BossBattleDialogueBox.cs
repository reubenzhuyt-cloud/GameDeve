using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Boss 战斗台词框：默认隐藏；说话时显示，语音结束（或无音时按估算时长）后再次隐藏。
/// 若 <see cref="dialogueRoot"/> 与本组件同物体，不能 SetActive(false)，改为开关 Renderer / TMP，避免脚本被停用。
/// </summary>
[DisallowMultipleComponent]
public sealed class BossBattleDialogueBox : MonoBehaviour
{
    [Header("Refs")]
    [Tooltip("整段语言框根物体；为空则用本物体。")]
    [SerializeField] private GameObject dialogueRoot;

    [Tooltip("台词 TMP；为空则在子级中查找 TMP_Text。")]
    [SerializeField] private TMP_Text dialogueText;

    [Tooltip("播放台词语音；为空则自动在本物体上添加 AudioSource。")]
    [SerializeField] private AudioSource voiceSource;

    [Header("Timing")]
    [Tooltip("语音播完后额外停留再隐藏（秒）。")]
    [SerializeField] private float hidePaddingAfterVoiceSeconds = 0.35f;

    [Tooltip("无音频时每字估算显示时长（秒）。")]
    [SerializeField] private float secondsPerCharWhenNoClip = 0.12f;

    [Tooltip("无音频时最短显示时长（秒）。")]
    [SerializeField] private float minVisibleSecondsWhenNoClip = 2f;

    /// <summary>与生成脚本 Tools/generate_tts.py（boss / edge-boss）中台词一致。</summary>
    public const string LineBattleStart = "我不想害人。可我更不想被人害。";

    public const string LineCastSkillFirst = "你们……都有道理！";
    public const string LineCastSkillSecond = "那我呢？那我呢！";
    public const string LineHurt = "我不疼……我不疼……";
    public const string LinePhase2 = "我死过一次了。我不怕。";
    public const string LineNearDeath = "天……快亮了……";

    private static readonly (string line, string resourcesPath)[] VoiceBindings =
    {
        (LineBattleStart, "Audio/BossBattle/boss_battle_start"),
        (LineCastSkillFirst, "Audio/BossBattle/boss_battle_cast_skill_1"),
        (LineCastSkillSecond, "Audio/BossBattle/boss_battle_cast_skill_2"),
        (LineHurt, "Audio/BossBattle/boss_battle_hurt"),
        (LinePhase2, "Audio/BossBattle/boss_battle_phase2"),
        (LineNearDeath, "Audio/BossBattle/boss_battle_near_death"),
    };

    private readonly Dictionary<string, AudioClip> _clipByLine = new Dictionary<string, AudioClip>();
    private bool _voiceClipsLoaded;

    /// <summary>为 true 时用 dialogueRoot.SetActive；为 false 时表示 root 即本物体，只关渲染。</summary>
    private bool _hideUsesSeparateRootActive;

    private Coroutine _hideAfterRoutine;

    private void Awake()
    {
        if (dialogueRoot == null)
            dialogueRoot = gameObject;
        if (dialogueText == null)
            dialogueText = GetComponentInChildren<TMP_Text>(true);

        _hideUsesSeparateRootActive = dialogueRoot != null && dialogueRoot != gameObject;

        EnsureVoiceSource();
        EnsureWorldTextDrawOrder();
    }

    private void Start()
    {
        SetDialogueVisible(false);
    }

    private void OnEnable()
    {
        EnsureVoiceClipsLoaded();
    }

    private void OnDisable()
    {
        if (_hideAfterRoutine != null)
        {
            StopCoroutine(_hideAfterRoutine);
            _hideAfterRoutine = null;
        }
    }

    private void EnsureVoiceSource()
    {
        if (voiceSource != null)
        {
            ApplyVoiceDefaults(voiceSource);
            return;
        }

        voiceSource = GetComponent<AudioSource>() ?? GetComponentInChildren<AudioSource>(true);
        if (voiceSource == null)
            voiceSource = gameObject.AddComponent<AudioSource>();
        ApplyVoiceDefaults(voiceSource);
    }

    private static void ApplyVoiceDefaults(AudioSource s)
    {
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 0f;
        if (s.volume <= 0f)
            s.volume = 1f;
    }

    private void EnsureWorldTextDrawOrder()
    {
        if (dialogueText == null)
            return;
        var mr = dialogueText.GetComponent<MeshRenderer>();
        if (mr != null && mr.sortingOrder < 50)
            mr.sortingOrder = 50;
    }

    private void EnsureVoiceClipsLoaded()
    {
        if (_voiceClipsLoaded)
            return;

        _voiceClipsLoaded = true;
        _clipByLine.Clear();

        foreach (var (line, resourcesPath) in VoiceBindings)
        {
            var clip = Resources.Load<AudioClip>(resourcesPath);
            if (clip == null)
            {
                Debug.LogWarning($"BossBattleDialogueBox: 未找到音频 Resources/{resourcesPath}，对应台词已跳过配音。");
                continue;
            }

            _clipByLine[line] = clip;
        }
    }

    private void SetDialogueVisible(bool visible)
    {
        if (dialogueRoot == null)
            return;

        if (_hideUsesSeparateRootActive)
        {
            dialogueRoot.SetActive(visible);
            return;
        }

        foreach (var r in GetComponentsInChildren<Renderer>(true))
            r.enabled = visible;

        if (dialogueText != null)
            dialogueText.enabled = visible;
    }

    private void ShowLine(string line)
    {
        EnsureVoiceClipsLoaded();
        EnsureVoiceSource();

        if (_hideAfterRoutine != null)
        {
            StopCoroutine(_hideAfterRoutine);
            _hideAfterRoutine = null;
        }

        SetDialogueVisible(true);
        if (dialogueText != null)
            dialogueText.text = line;

        float waitSeconds;
        AudioClip clip = null;
        if (voiceSource != null && _clipByLine.TryGetValue(line, out clip) && clip != null)
        {
            voiceSource.Stop();
            voiceSource.clip = clip;
            voiceSource.Play();
            waitSeconds = clip.length + hidePaddingAfterVoiceSeconds;
        }
        else
        {
            Debug.LogWarning($"BossBattleDialogueBox: 无绑定音频，台词：{line}");
            waitSeconds = Mathf.Max(minVisibleSecondsWhenNoClip, line.Length * secondsPerCharWhenNoClip);
        }

        _hideAfterRoutine = StartCoroutine(CoHideAfterDelay(waitSeconds));
    }

    private IEnumerator CoHideAfterDelay(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        SetDialogueVisible(false);
        _hideAfterRoutine = null;
    }

    public void SayInBattleStartState() => ShowLine(LineBattleStart);

    public void SayInCastSkillFirstState() => ShowLine(LineCastSkillFirst);

    public void SayInCastSkillSecondState() => ShowLine(LineCastSkillSecond);

    public void SayInHurtState() => ShowLine(LineHurt);

    public void SayInPhase2State() => ShowLine(LinePhase2);

    public void SayInNearDeathState() => ShowLine(LineNearDeath);
}
