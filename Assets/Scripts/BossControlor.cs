using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    [Header("Victory")]
    [Tooltip("二阶段 Boss 血量归零后加载的场景名（须加入 Build Settings）。")]
    [SerializeField] private string phase2VictorySceneName = "End";
    [SerializeField] private string victoryTransitionTip = "正在前往下一场景";
    [SerializeField] private float victoryTipSeconds = 1f;

    [Header("Shield (Phase 1)")]
    [Tooltip("仅一次：首次飞弹将血量打进半血及以下时锁到 50% 并开盾，同时播濒死台词；破盾后不再生成护盾。盾存在时飞弹无伤；破盾需 5 色各消至少 1 格。二阶段另有独立护盾逻辑。")]
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

    [Header("Shield (Phase 2)")]
    [Tooltip("二阶段：血量降至 ≤75% / ≤50% / ≤25%（相对二阶段最大生命）各触发一次护盾；每次随机两种颜色，仅统计这两色的消除分，合计达到该分数后破盾。")]
    [SerializeField] private int phase2ShieldCombinedScoreRequired = 40;

    private bool _phase2ShieldActive;

    private enum Phase2ShieldTierKind : byte
    {
        None = 0,
        HpAtOrBelow75Pct = 1,
        HpAtOrBelow50Pct = 2,
        HpAtOrBelow25Pct = 3,
    }

    private Phase2ShieldTierKind _activePhase2ShieldTier;
    private bool _phase2Tier75LineCleared;
    private bool _phase2Tier50LineCleared;
    private bool _phase2Tier25LineCleared;
    private readonly int[] _phase2ColorDamage = new int[6];
    private int _phase2TargetColorA;
    private int _phase2TargetColorB;

    /// <summary>类型 1～5 对应棋盘颜色，用于护盾提示文案。</summary>
    private static readonly string[] ShieldColorHintNames = { "", "红", "绿", "黑", "黄", "白" };

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

        if (_shieldActive || _phase2ShieldActive)
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

        if (_inPhase2 && !_defeated)
            TrySpawnPhase2ShieldIfDue();

        if (_currentHp > 0)
            return;

        if (!_inPhase2)
        {
            _inPhase2 = true;
            _shieldActive = false;
            ClearShieldProgress();
            ResetPhase2ShieldStateForNewPhase();
            _maxHp = phase2MaxHp;
            _currentHp = phase2MaxHp;
            healthBar?.SetHealthFullSync(_currentHp, _maxHp);
            ApplyShieldPresentation();
            bossDialogue?.SayInPhase2State();
        }
        else
        {
            _defeated = true;
            LoadPhase2VictoryScene();
        }
    }

    private void LoadPhase2VictoryScene()
    {
        string name = string.IsNullOrWhiteSpace(phase2VictorySceneName) ? "End" : phase2VictorySceneName.Trim();
        StartCoroutine(LoadPhase2VictoryRoutine(name));
    }

    private IEnumerator LoadPhase2VictoryRoutine(string sceneName)
    {
        float wait = Mathf.Max(0f, victoryTipSeconds);
        if (wait > 0f && !string.IsNullOrEmpty(victoryTransitionTip))
            UIManager.ShowTip(victoryTransitionTip, wait);
        if (wait > 0f)
            yield return new WaitForSecondsRealtime(wait);

        if (SceneTransition.instance != null)
            SceneTransition.instance.TransitionToScene(sceneName);
        else
            SceneManager.LoadScene(sceneName);
    }

    private void OnMatchEliminatedGemType(int gemType)
    {
        if (_inPhase2)
            return;
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

        if (_phase2ShieldActive)
        {
            string na = ShieldColorHintNames[_phase2TargetColorA];
            string nb = ShieldColorHintNames[_phase2TargetColorB];
            int da = _phase2ColorDamage[_phase2TargetColorA];
            int db = _phase2ColorDamage[_phase2TargetColorB];
            int need = Mathf.Max(1, phase2ShieldCombinedScoreRequired);
            int sum = da + db;
            shieldInfoText.text =
                $"二阶段护盾 【{na}】【{nb}】两色合计{need}分 (当前 {sum}/{need}，{na}{da}·{nb}{db})";
            return;
        }

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
            missing += ShieldColorHintNames[t];
        }

        if (missing.Length == 0)
            missing = "无";

        shieldInfoText.text = $"破盾 {n}/5  未消除：{missing}";
    }

    private void ApplyShieldPresentation()
    {
        bool show = _shieldActive || _phase2ShieldActive;
        if (!show && shieldInfoText != null)
            shieldInfoText.text = string.Empty;

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
        if (!_phase2ShieldActive || damage <= 0 || colorType < 1 || colorType > 5)
            return;
        if (colorType != _phase2TargetColorA && colorType != _phase2TargetColorB)
            return;

        _phase2ColorDamage[colorType] += damage;
        RefreshShieldInfoText();
        TryBreakPhase2Shield();
    }

    private void ResetPhase2ShieldStateForNewPhase()
    {
        _phase2ShieldActive = false;
        _activePhase2ShieldTier = Phase2ShieldTierKind.None;
        _phase2Tier75LineCleared = false;
        _phase2Tier50LineCleared = false;
        _phase2Tier25LineCleared = false;
        for (int i = 0; i < _phase2ColorDamage.Length; i++)
            _phase2ColorDamage[i] = 0;
    }

    private void TrySpawnPhase2ShieldIfDue()
    {
        if (!_inPhase2 || _defeated || _phase2ShieldActive)
            return;

        int max = _maxHp;
        int hp = _currentHp;
        int line75 = max * 3 / 4;
        int line50 = max / 2;
        int line25 = max / 4;

        if (!_phase2Tier75LineCleared && hp <= line75)
        {
            StartPhase2Shield(Phase2ShieldTierKind.HpAtOrBelow75Pct);
            return;
        }

        if (!_phase2Tier50LineCleared && hp <= line50)
        {
            StartPhase2Shield(Phase2ShieldTierKind.HpAtOrBelow50Pct);
            return;
        }

        if (!_phase2Tier25LineCleared && hp <= line25)
            StartPhase2Shield(Phase2ShieldTierKind.HpAtOrBelow25Pct);
    }

    private void StartPhase2Shield(Phase2ShieldTierKind tier)
    {
        PickTwoDistinctGemTypes(out _phase2TargetColorA, out _phase2TargetColorB);
        for (int i = 1; i <= 5; i++)
            _phase2ColorDamage[i] = 0;

        _activePhase2ShieldTier = tier;
        _phase2ShieldActive = true;
        RefreshShieldInfoText();
        ApplyShieldPresentation();
    }

    private static void PickTwoDistinctGemTypes(out int a, out int b)
    {
        a = UnityEngine.Random.Range(1, 6);
        do
        {
            b = UnityEngine.Random.Range(1, 6);
        } while (b == a);
    }

    private void TryBreakPhase2Shield()
    {
        if (!_phase2ShieldActive)
            return;

        int need = Mathf.Max(1, phase2ShieldCombinedScoreRequired);
        int sum = _phase2ColorDamage[_phase2TargetColorA] + _phase2ColorDamage[_phase2TargetColorB];
        if (sum < need)
            return;

        switch (_activePhase2ShieldTier)
        {
            case Phase2ShieldTierKind.HpAtOrBelow75Pct:
                _phase2Tier75LineCleared = true;
                break;
            case Phase2ShieldTierKind.HpAtOrBelow50Pct:
                _phase2Tier50LineCleared = true;
                break;
            case Phase2ShieldTierKind.HpAtOrBelow25Pct:
                _phase2Tier25LineCleared = true;
                break;
        }

        _phase2ShieldActive = false;
        _activePhase2ShieldTier = Phase2ShieldTierKind.None;
        RefreshShieldInfoText();
        ApplyShieldPresentation();
        TrySpawnPhase2ShieldIfDue();
    }
}
