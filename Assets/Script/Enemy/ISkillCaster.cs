// 스킬을 시전할 수 있는 몬스터 컨트롤러 공통 인터페이스 (EliteMonsterSkillController, BossMonsterSkillController).
// StatusEffectHandler처럼 "어떤 컨트롤러가 붙어있든 상관없이 시전 중인 스킬을 강제 취소하고 싶은" 쪽에서
// 구체 타입을 몰라도 GetComponent<ISkillCaster>()로 찾아 호출할 수 있게 하기 위함.
public interface ISkillCaster
{
    void ForceCancelSkill();
}
