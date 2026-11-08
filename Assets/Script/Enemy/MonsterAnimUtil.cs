using UnityEngine;

// 몬스터 공격·스킬 공용 — 후딜레이(recoveryDuration)가 끝났는데 애니메이터가 아직 공격/스킬 모션 중이면
// 지정한 상태(보통 Idle)로 끊어서 넘긴다. 모션 클립이 후딜보다 길면 Exit Time까지 공격 자세로 남아
// 이동을 재개해도 미끄러지듯 움직이는 문제를 막기 위함 — "후딜이 끝나는 순간 = 모션이 끝나는 순간"
public static class MonsterAnimUtil
{
    /// <summary>전환 중이면 넘어가는 대상 상태, 아니면 현재 상태의 shortNameHash (Base Layer)</summary>
    public static int CurrentStateHash(Animator animator)
    {
        if (animator == null) return 0;
        return animator.IsInTransition(0)
            ? animator.GetNextAnimatorStateInfo(0).shortNameHash
            : animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
    }

    /// <summary>애니메이터가 아직 actionStateHash 상태면 exitStateName으로 CrossFade.
    /// 이미 Exit Time 등으로 빠져나갔거나, 해당 상태가 없거나, 이름이 비어 있으면 아무것도 안 함</summary>
    public static void ExitStateIfStill(Animator animator, int actionStateHash, string exitStateName, float fade)
    {
        if (animator == null || actionStateHash == 0 || string.IsNullOrEmpty(exitStateName)) return;
        if (CurrentStateHash(animator) != actionStateHash) return;

        int target = Animator.StringToHash(exitStateName);
        if (target == actionStateHash || !animator.HasState(0, target)) return;

        animator.CrossFadeInFixedTime(target, fade, 0);
    }
}
