using UnityEngine;
using TMPro;

// 전투 중 캐릭터 머리 위에 떠서 카메라를 바라보는 HP/MP 바
public class CombatHpUi : HpMpBarUI
{
    [Tooltip("디버프 상태 표시 텍스트 (선택 — 비워두면 표시 안 함). 스턴=\"기절\", 슬로우=\"둔화\". " +
             "DamageText처럼 Canvas 없는 3D 월드스페이스 TextMeshPro (UGUI 아님)")]
    public TextMeshPro debuffText;

    private Transform camTransform;
    private PartyStatusEffectHandler statusHandler;

    protected override void Start()
    {
        base.Start();
        if (Camera.main != null) camTransform = Camera.main.transform;

        if (stat != null) statusHandler = stat.GetComponent<PartyStatusEffectHandler>();
        RefreshDebuffText();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (statusHandler == null && stat != null) statusHandler = stat.GetComponent<PartyStatusEffectHandler>();
        if (statusHandler != null) statusHandler.OnBuffChanged += HandleBuffChanged;
        if (stat != null) stat.OnStatusTextRequested += HandleStatusTextRequested;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (statusHandler != null) statusHandler.OnBuffChanged -= HandleBuffChanged;
        if (stat != null) stat.OnStatusTextRequested -= HandleStatusTextRequested;
        _tempUntil = 0f;
    }

    // ─────────────────────────────────────────────────────────────────
    // 잠깐 띄우는 상태 텍스트 (부활 등) — 표시 시간 동안은 디버프 표시보다 우선
    // ─────────────────────────────────────────────────────────────────

    private string    _tempText;
    private Color     _tempColor;
    private float     _tempUntil;
    private Coroutine _tempRoutine;

    private void HandleStatusTextRequested(string text, Color color, float duration)
    {
        _tempText  = text;
        _tempColor = color;
        _tempUntil = Time.time + duration;
        RefreshDebuffText();

        if (_tempRoutine != null) StopCoroutine(_tempRoutine);
        _tempRoutine = StartCoroutine(ClearTempTextAfter(duration));
    }

    private System.Collections.IEnumerator ClearTempTextAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        _tempRoutine = null;
        RefreshDebuffText();
    }

    void LateUpdate()
    {
        if (camTransform == null) return;
        transform.LookAt(transform.position + camTransform.rotation * Vector3.forward,
                         camTransform.rotation * Vector3.up);
    }

    // ─────────────────────────────────────────────────────────────────
    // 디버프 표시 — 스턴 > 슬로우 우선순위로 하나만 표시 (둘 다 걸려도 한 줄이면 충분)
    // ─────────────────────────────────────────────────────────────────

    private void HandleBuffChanged(StatusEffectType type, bool active) => RefreshDebuffText();

    private void RefreshDebuffText()
    {
        if (debuffText == null) return;

        if (Time.time < _tempUntil)
        {
            debuffText.text  = _tempText;
            debuffText.color = _tempColor;
            return;
        }

        if (statusHandler == null) { debuffText.text = ""; return; }

        if (statusHandler.HasDebuff(StatusEffectType.Stun))
        {
            debuffText.text  = "기절";
            debuffText.color = StatusTextColors.Stun;
        }
        else if (statusHandler.HasDebuff(StatusEffectType.Slow) || statusHandler.HasDebuff(StatusEffectType.MoveSpeedDown))
        {
            debuffText.text  = "둔화";
            debuffText.color = StatusTextColors.Slow;
        }
        else
            debuffText.text = "";
    }
}
