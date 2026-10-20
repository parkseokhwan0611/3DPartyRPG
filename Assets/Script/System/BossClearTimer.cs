using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// [임시] 파티 조합별 보스 클리어 시간 비교용 기록기 — 비교가 끝나면 이 파일만 지우면 된다 (씬·프리팹 수정 없음).
// 씬이 열릴 때 등급이 Boss인 몬스터를 찾아, 보스가 처음 타겟을 잡거나 처음 피해를 입은 순간부터
// 보스 사망(클리어) 또는 파티 전멸(실패)까지의 게임 시간(일시정지·메뉴 시간 제외)을 콘솔과 CSV에 남긴다.
// CSV: Application.persistentDataPath/boss_clear_log.csv
public class BossClearTimer : MonoBehaviour
{
    private const string FileName = "boss_clear_log.csv";

    private EnemyHp _boss;
    private AttackBase _bossAttack;
    private bool _running;
    private float _startTime;
    private int _hpPotions;
    private int _mpPotions;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("[BossClearTimer]");
        DontDestroyOnLoad(go);
        go.AddComponent<BossClearTimer>();
    }

    void Awake()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
        PotionQuickSlotManager.OnPotionUsed += HandlePotionUsed;
        FindBoss();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        PotionQuickSlotManager.OnPotionUsed -= HandlePotionUsed;
        Unhook();
    }

    // 전투 중에 쓴 물약만 센다 (전투 전 미리 마신 건 제외)
    private void HandlePotionUsed(ConsumableType type)
    {
        if (!_running) return;
        if (type == ConsumableType.HpPotion) _hpPotions++;
        else if (type == ConsumableType.MpPotion) _mpPotions++;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_running) Write("중단(씬 이동)");
        Unhook();
        FindBoss();
    }

    private void FindBoss()
    {
        foreach (var e in FindObjectsOfType<EnemyHp>())
        {
            if (e.grade != EnemyHp.MonsterGrade.Boss || e.isDead) continue;
            _boss       = e;
            _bossAttack = e.GetComponent<AttackBase>();
            _boss.OnHpChanged += HandleBossHpChanged;
            _boss.OnDied      += HandleBossDied;
            if (_bossAttack != null) _bossAttack.OnAttackStarted += StartTimer;
            return;
        }
    }

    private void Unhook()
    {
        if (_boss != null)
        {
            _boss.OnHpChanged -= HandleBossHpChanged;
            _boss.OnDied      -= HandleBossDied;
        }
        if (_bossAttack != null) _bossAttack.OnAttackStarted -= StartTimer;
        _boss = null;
        _bossAttack = null;
        _running = false;
    }

    private void HandleBossHpChanged(float hp, float maxHp)
    {
        if (hp < maxHp) StartTimer();
    }

    private void StartTimer()
    {
        if (_running || _boss == null || _boss.isDead) return;
        _running   = true;
        _startTime = Time.time;
        _hpPotions = 0;
        _mpPotions = 0;
        Debug.Log($"[BossClearTimer] 전투 시작 — {_boss.enemyName} / {PartyText()}");
    }

    private void HandleBossDied()
    {
        if (_running) Write("클리어");
    }

    void Update()
    {
        if (!_running) return;
        var pm = PartyManager.instance;
        if (pm != null && pm.partyMembers.Count > 0
            && pm.partyMembers.TrueForAll(m => m == null || m.CurrentState == PartyMemberScript.MemberState.Dead))
            Write("실패(전멸)");
    }

    private void Write(string result)
    {
        _running = false;
        float elapsed  = Time.time - _startTime;
        int   level    = DataManager.instance != null ? DataManager.instance.partyLevel : 0;
        string boss    = _boss != null ? _boss.enemyName : "?";
        float bossHpPct = _boss != null && _boss.maxHp > 0f ? Mathf.Max(0f, _boss.hp) / _boss.maxHp * 100f : 0f;

        Debug.Log($"[BossClearTimer] {result} — {elapsed:F1}초 | 파티 Lv.{level} | {PartyText()} | 생존 {AliveCount()}명" +
                  $" | HP 물약 {_hpPotions}개, MP 물약 {_mpPotions}개" +
                  (result == "클리어" ? "" : $" | 보스 남은 체력 {bossHpPct:F0}%"));

        string path = Path.Combine(Application.persistentDataPath, FileName);
        try
        {
            bool newFile = !File.Exists(path);
            var sb = new StringBuilder();
            if (newFile) sb.AppendLine("일시,결과,시간(초),파티레벨,보스,구성,생존,HP물약,MP물약,보스남은체력(%)");
            sb.AppendLine(string.Join(",", System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), result, elapsed.ToString("F1"),
                                      level, boss, PartyText(), AliveCount(), _hpPotions, _mpPotions, bossHpPct.ToString("F0")));
            // 엑셀에서 한글이 깨지지 않게 BOM 포함 UTF-8
            File.AppendAllText(path, sb.ToString(), new UTF8Encoding(newFile));
        }
        catch (IOException e)
        {
            Debug.LogWarning($"[BossClearTimer] CSV 기록 실패: {e.Message}");
        }
    }

    // "Varunius:검방패 / Solaris:지팡이 / ..." — 쉼표는 CSV 구분자라 쓰지 않음
    private static string PartyText()
    {
        var pm = PartyManager.instance;
        if (pm == null) return "?";
        var parts = new List<string>();
        foreach (var m in pm.partyMembers)
        {
            if (m == null) continue;
            var cls = m.StatComp != null ? m.StatComp.CurrentClass : null;
            string clsName = cls == null ? "?" : (string.IsNullOrEmpty(cls.displayName) ? cls.classType.ToString() : cls.displayName);
            parts.Add($"{m.name}:{clsName}");
        }
        return string.Join(" / ", parts);
    }

    private static int AliveCount()
    {
        var pm = PartyManager.instance;
        if (pm == null) return 0;
        int n = 0;
        foreach (var m in pm.partyMembers)
            if (m != null && m.CurrentState != PartyMemberScript.MemberState.Dead) n++;
        return n;
    }
}
