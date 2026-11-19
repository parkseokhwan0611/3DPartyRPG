using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// 보스전 측정 로거 — 보스(BossClearActivator가 붙은 몬스터)가 처음 타겟을 잡은 순간부터
// 쓰러질 때까지의 처치 시간과 포션 사용 수를 재서 콘솔과 CSV 파일에 남긴다.
// 씬·프리팹을 건드리지 않도록 씬이 열릴 때 보스에 자동으로 붙는다. 측정이 끝나면 Enabled를 false로.
// CSV 위치: Application.persistentDataPath/BossFightLog.csv (에디터 기준 AppData/LocalLow/회사명/제품명)
public class BossFightLogger : MonoBehaviour
{
    public const bool Enabled = true;
    private const string FileName = "BossFightLog.csv";

    private static bool _initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        if (!Enabled || _initialized) return;
        _initialized = true;
        SceneManager.sceneLoaded += (scene, mode) => AttachToBosses();
        AttachToBosses();
    }

    private static void AttachToBosses()
    {
        foreach (var boss in FindObjectsOfType<BossClearActivator>(true))
            if (boss.GetComponent<BossFightLogger>() == null)
                boss.gameObject.AddComponent<BossFightLogger>();
    }

    private EnemyHp    _hp;
    private AttackBase _attack;

    private bool  _fighting;
    private float _startTime;
    private int   _hpPotions;
    private int   _mpPotions;
    private int   _startPartyLevel;

    void Awake()
    {
        _hp     = GetComponent<EnemyHp>();
        _attack = GetComponent<AttackBase>();
    }

    void OnEnable()
    {
        if (_attack != null) _attack.OnAttackStarted += HandleAggro;
        if (_hp != null)     _hp.OnDied += HandleDeath;
        PotionQuickSlotManager.OnPotionUsed += HandlePotion;
    }

    void OnDisable()
    {
        if (_attack != null) _attack.OnAttackStarted -= HandleAggro;
        if (_hp != null)     _hp.OnDied -= HandleDeath;
        PotionQuickSlotManager.OnPotionUsed -= HandlePotion;
    }

    // 보스가 처음 타겟을 잡은 순간 = 전투 시작 (보스 BGM이 바뀌는 시점과 같음).
    // 타겟이 파티원 사이에서 바뀔 때도 다시 불리므로 이미 측정 중이면 무시
    private void HandleAggro()
    {
        if (_fighting || (_hp != null && _hp.isDead)) return;
        _fighting        = true;
        _startTime       = Time.time;
        _hpPotions       = 0;
        _mpPotions       = 0;
        _startPartyLevel = DataManager.instance != null ? DataManager.instance.partyLevel : 0;
    }

    private void HandlePotion(ConsumableType type)
    {
        if (!_fighting) return;
        if (type == ConsumableType.HpPotion) _hpPotions++;
        else if (type == ConsumableType.MpPotion) _mpPotions++;
    }

    private void HandleDeath() => Finish("처치");

    void Update()
    {
        // 전멸하면 게임 오버 화면이 뜨고 시간이 멈추므로 그 시점에 실패로 기록
        if (_fighting && PartyManager.IsGameOver) Finish("전멸");
    }

    // 보스전 도중 씬을 떠난 경우(포탈·타이틀로 등)
    void OnDestroy()
    {
        if (_fighting) Finish("이탈");
    }

    private void Finish(string result)
    {
        if (!_fighting) return;
        _fighting = false;

        float  seconds   = Time.time - _startTime; // 게임 시간 기준 (일시정지 중인 시간은 빠짐)
        string bossName  = _hp != null && !string.IsNullOrEmpty(_hp.enemyName) ? _hp.enemyName : gameObject.name;
        float  hpPercent = _hp != null && _hp.maxHp > 0f ? Mathf.Max(0f, _hp.hp) / _hp.maxHp * 100f : 0f;
        string scene     = gameObject.scene.name;

        Debug.Log($"[BossFightLogger] {bossName} {result} — 시간 {FormatTime(seconds)} ({seconds:F1}초), " +
                  $"HP 포션 {_hpPotions}개, MP 포션 {_mpPotions}개, 파티 Lv.{_startPartyLevel}, 보스 남은 체력 {hpPercent:F1}%, " +
                  $"조합 {PartyComposition()}");

        string row = string.Join(",",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), scene, bossName, result,
            seconds.ToString("F1"), FormatTime(seconds), _hpPotions, _mpPotions, _startPartyLevel,
            hpPercent.ToString("F1"), PartyComposition());

        string mainPath = Path.Combine(Application.persistentDataPath, FileName);
        if (TryAppend(mainPath, row, out string error)) return;

        // 엑셀 등으로 기록 파일이 열려 있으면 잠겨서 못 쓴다 → 시각을 붙인 별도 파일에 남겨 기록을 잃지 않게
        string fallback = Path.Combine(Application.persistentDataPath,
            $"{Path.GetFileNameWithoutExtension(FileName)}_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        if (TryAppend(fallback, row, out string error2))
            Debug.LogWarning($"[BossFightLogger] 기록 파일이 열려 있어 별도 파일에 저장했습니다: {fallback} ({error})");
        else
            Debug.LogWarning($"[BossFightLogger] 기록 파일 저장 실패: {error2}");
    }

    private const string Header = "날짜,씬,보스,결과,처치 시간(초),처치 시간,HP 포션,MP 포션,파티 레벨,보스 남은 체력(%),파티 조합";

    // 파티 순서(바루니우스/솔라리스/루나리스)대로 클래스 이름 — 예: 전사/건슬링어/사령술사
    private static string PartyComposition()
    {
        if (DataManager.instance == null) return "";
        var names = new System.Collections.Generic.List<string>();
        foreach (var status in DataManager.instance.partyStatuses)
            if (status?.classData != null) names.Add(ClassName(status.classData.classType));
        return string.Join("/", names);
    }

    private static string ClassName(ClassData.ClassType type) => type switch
    {
        ClassData.ClassType.Tanker     => "탱커",
        ClassData.ClassType.Warrior    => "전사",
        ClassData.ClassType.Mage       => "마법사",
        ClassData.ClassType.GunSlinger => "건슬링어",
        ClassData.ClassType.Healer     => "성직자",
        ClassData.ClassType.Summoner   => "사령술사",
        _                              => type.ToString(),
    };

    private static bool TryAppend(string path, string row, out string error)
    {
        error = null;
        try
        {
            bool isNew = !File.Exists(path);
            var  sb    = new StringBuilder();
            if (isNew) sb.AppendLine(Header);
            sb.AppendLine(row);
            // BOM 포함 UTF-8 — 엑셀에서 열어도 한글이 깨지지 않게 (파일을 새로 만들 때만 BOM이 붙음)
            File.AppendAllText(path, sb.ToString(), new UTF8Encoding(true));
            if (isNew) Debug.Log($"[BossFightLogger] 기록 파일: {path}");
            return true;
        }
        catch (Exception e)
        {
            error = e.Message;
            return false;
        }
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.FloorToInt(seconds);
        return $"{total / 60}분 {total % 60:00}초";
    }
}
