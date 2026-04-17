using TMPro;
using UnityEngine;

public sealed class BossControlor : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Match3Manager match3Manager;
    [SerializeField] private Match3DamageSwarmManager damageSwarm;
    [SerializeField] private BossBattleDialogueBox bossDialogue;
    [SerializeField] private BossHealthBarView healthBar;

    [Header("HP")]
    [SerializeField] private int phase1MaxHp = 100;
    [SerializeField] private int phase2MaxHp = 300;

    [Header("Shield (Phase 1)")]
    [Tooltip("仅一次：首次飞弹将血量打进半血及以下时锁到 50% 并开盾，同时播濒死台词；破盾后不再生成护盾。盾存在时飞弹无伤；破盾需 5 色各消至少 1 格。")]
    [SerializeField] private TMP_Text shieldInfoText;

    [Tooltip("护盾存在时保持 Active 的物体（特效/壳层等）。")]
    [SerializeField] private GameObject shieldVisualRoot;

    private bool _saidBattleStart;
    private bool _inPhase2;
    private bool _defeated;
    private int _currentHp;
    private int _maxHp;

    private bool _shieldActive;
    /// <summary>破盾一次后为 true，之后不再生成护盾。</summary>
    private bool _shieldBrokenOnce;
    private readonly bool[] _shieldColorDone = new bool[6];

    private void Awake()
    {
        if (match3Manager == null)
            match3Manager = FindFirstObjectByType<Match3Manager>();
        if (damageSwarm == null)
            damageSwarm = FindFirstObjectByType<Match3DamageSwarmManager>();
        if (bossDialogue == null)
            bossDialogue = FindFirstObjectByType<BossBattleDialogueBox>();
        if (healthBar == null)
            healthBar = FindFirstObjectByType<BossHealthBarView>();

        _maxHp = phase1MaxHp;
        _currentHp = phase1MaxHp;
        _inPhase2 = false;
        _defeated = false;
        _shieldActive = false;
        _shieldBrokenOnce = false;
    }

    private void Start()
    {
        healthBar?.SetHealthFullSync(_currentHp, _maxHp);
        ApplyShieldPresentation();
    }

    private void OnEnable()
    {
        if (match3Manager == null)
            return;

        match3Manager.OnBattleStarted += HandleBattleStarted;
        match3Manager.OnEliminationDamage += HandleEliminationDamage;
        match3Manager.OnFreezeSkillTriggered += HandleFreezeSkill;
        match3Manager.OnMatchEliminatedGemType += OnMatchEliminatedGemType;
        if (damageSwarm != null)
            damageSwarm.OnBossDamagedByOrb += HandleBossDamagedByOrb;

        int moves = match3Manager.SuccessfulPlayerMoveCount;
        if (moves == 1)
            HandleBattleStarted();
        else if (moves > 1)
            _saidBattleStart = true;
    }

    private void OnDisable()
    {
        if (match3Manager != null)
        {
            match3Manager.OnBattleStarted -= HandleBattleStarted;
            match3Manager.OnEliminationDamage -= HandleEliminationDamage;
            match3Manager.OnFreezeSkillTriggered -= HandleFreezeSkill;
            match3Manager.OnMatchEliminatedGemType -= OnMatchEliminatedGemType;
        }

        if (damageSwarm != null)
            damageSwarm.OnBossDamagedByOrb -= HandleBossDamagedByOrb;
    }

    private void HandleBossDamagedByOrb(int damage)
    {
        if (_defeated || damage <= 0)
            return;

        if (_shieldActive)
            return;

        int nextHp = Mathf.Max(0, _currentHp - damage);
        int half = phase1MaxHp / 2;

        if (!_inPhase2 && !_shieldBrokenOnce && nextHp <= half)
        {
            _currentHp = half;
            healthBar?.OnHealthChangedAfterDamage(_currentHp, _maxHp);
            bossDialogue?.SayInNearDeathState();

            _shieldActive = true;
            ClearShieldProgress();
            RefreshShieldInfoText();
            ApplyShieldPresentation();
            return;
        }

        _currentHp = nextHp;
        healthBar?.OnHealthChangedAfterDamage(_currentHp, _maxHp);

        if (_currentHp > 0)
            return;

        if (!_inPhase2)
        {
            _inPhase2 = true;
            _shieldActive = false;
            ClearShieldProgress();
            _maxHp = phase2MaxHp;
            _currentHp = phase2MaxHp;
            healthBar?.SetHealthFullSync(_currentHp, _maxHp);
            ApplyShieldPresentation();
            bossDialogue?.SayInPhase2State();
        }
        else
        {
            _defeated = true;
        }
    }

    private void OnMatchEliminatedGemType(int gemType)
    {
        if (!_shieldActive || gemType < 1 || gemType > 5)
            return;

        _shieldColorDone[gemType] = true;
        RefreshShieldInfoText();

        if (!ShieldAllFiveColorsDone())
            return;

        _shieldActive = false;
        _shieldBrokenOnce = true;
        ClearShieldProgress();
        ApplyShieldPresentation();
    }

    private bool ShieldAllFiveColorsDone()
    {
        for (int t = 1; t <= 5; t++)
        {
            if (!_shieldColorDone[t])
                return false;
        }

        return true;
    }

    private void ClearShieldProgress()
    {
        for (int t = 0; t < _shieldColorDone.Length; t++)
            _shieldColorDone[t] = false;
    }

    private void RefreshShieldInfoText()
    {
        if (shieldInfoText == null)
            return;
        if (!_shieldActive)
        {
            shieldInfoText.text = string.Empty;
            return;
        }

        int n = 0;
        for (int t = 1; t <= 5; t++)
        {
            if (_shieldColorDone[t])
                n++;
        }

        string missing = "";
        for (int t = 1; t <= 5; t++)
        {
            if (_shieldColorDone[t])
                continue;
            if (missing.Length > 0)
                missing += "、";
            missing += t.ToString();
        }

        if (missing.Length == 0)
            missing = "无";

        shieldInfoText.text = $"破盾 {n}/5  未消除种类：{missing}";
    }

    private void ApplyShieldPresentation()
    {
        bool show = _shieldActive;

        if (shieldVisualRoot != null)
        {
            shieldVisualRoot.SetActive(show);
            if (show)
            {
                var rt = shieldVisualRoot.transform as RectTransform;
                if (rt != null)
                    rt.SetAsLastSibling();
            }
        }

        if (shieldInfoText == null)
            return;

        if (shieldVisualRoot != null && shieldInfoText.transform.IsChildOf(shieldVisualRoot.transform))
        {
            if (show)
                shieldInfoText.gameObject.SetActive(true);
        }
        else
        {
            shieldInfoText.gameObject.SetActive(show);
        }
    }

    private bool _alternateFreezeSkillLine;

    private void HandleFreezeSkill(int moveCountAtTrigger, int frozenCellCount)
    {
        if (bossDialogue == null)
            return;
        _alternateFreezeSkillLine = !_alternateFreezeSkillLine;
        if (_alternateFreezeSkillLine)
            bossDialogue.SayInCastSkillFirstState();
        else
            bossDialogue.SayInCastSkillSecondState();
    }

    private void HandleBattleStarted()
    {
        if (_saidBattleStart || bossDialogue == null)
            return;
        _saidBattleStart = true;
        bossDialogue.SayInBattleStartState();
    }

    private void HandleEliminationDamage(int damage, int colorType)
    {
        Debug.Log($"[BossControlor] damage={damage}, colorType={colorType}");
    }
}
