using UnityEngine;

namespace Client
{
    // 260912_스킬 효과 — 종류별 모듈
    /// <summary>
    /// CPlayer에 조합으로 붙는 모듈이다 (CEnemyGimmick이 CEnemy에 붙는 것과 같은 자리).
    ///
    /// 예전에는 CPlayer.Try_UseSkill이 WARP 하나를 직접 처리했다.
    /// 스킬이 늘 때마다 CPlayer에 분기가 하나씩 쌓이는 모양이라, 표 한 줄로 스킬이 늘어나도록
    /// 효과를 여기로 떼어 냈다. CPlayer는 '무엇을 할 수 있는가'만 공개하고
    /// '무엇을 할지'는 SkillInfo.csv가 정한다.
    ///
    /// 수치의 의미는 스킬마다 다르다. SkillInfo.csv 머리의 주석이 기준이다.
    /// </summary>
    public abstract class CSkillEffect
    {
        protected CPlayer       m_cOwner;
        protected ISkillHost    m_cHost;

        /// <summary> eType에 맞는 모듈을 만든다. 패시브와 NONE이면 null (버튼으로 쓸 것이 없다). </summary>
        public static CSkillEffect Create(SKILL_TYPE eType)
        {
            switch (eType)
            {
                case SKILL_TYPE.WARP:   return new CSkillEffect_Warp();
                case SKILL_TYPE.SHIELD: return new CSkillEffect_Shield();
                case SKILL_TYPE.DASH:   return new CSkillEffect_Dash();
                case SKILL_TYPE.SLOW:   return new CSkillEffect_Slow();
                case SKILL_TYPE.SEAL:   return new CSkillEffect_Seal();
                default:                return null;
            }
        }

        public bool Initialize(CPlayer cOwner)
        {
            if (cOwner == null)
            {
                Debug.LogError("[CSkillEffect] 소유자가 null 입니다.");
                return false;
            }

            m_cOwner = cOwner;
            return true;
        }

        /// <summary> 플레이어 밖을 건드리는 스킬(감속)만 쓴다. 스테이지가 꽂아 준다. </summary>
        public void Set_Host(ISkillHost cHost) => m_cHost = cHost;

        // 실패하면 쿨타임을 쓰지 않는다 — 멈춘 채로 점멸을 눌러 쿨만 날리는 일을 막는다.
        /// <summary> 발동. 조건이 안 맞으면 false, 그러면 호출부가 쿨타임을 돌리지 않는다. </summary>
        /// <param name="fValue"> 레벨이 반영된 주 수치 </param>
        /// <param name="fDuration"> 효과가 이어지는 초. 즉발이면 0 </param>
        public abstract bool Try_Apply(float fValue, float fDuration);
    }

    /// <summary> 진행 방향으로 여러 칸 순간 이동. </summary>
    public class CSkillEffect_Warp : CSkillEffect
    {
        public override bool Try_Apply(float fValue, float fDuration)
        {
            return m_cOwner.Warp(Mathf.RoundToInt(fValue));
        }
    }

    /// <summary> 잠깐 무적. 소모품 보호막(한 대 막기)과 달리 시간으로 버틴다. </summary>
    public class CSkillEffect_Shield : CSkillEffect
    {
        public override bool Try_Apply(float fValue, float fDuration)
        {
            if (fValue <= 0f)
                return false;

            m_cOwner.Add_Invincible(fValue);
            return true;
        }
    }

    /// <summary> 잠깐 이동 속도 상승. fValue가 0.8이면 1.8배. </summary>
    public class CSkillEffect_Dash : CSkillEffect
    {
        public override bool Try_Apply(float fValue, float fDuration)
        {
            if (fValue <= 0f || fDuration <= 0f)
                return false;

            m_cOwner.Add_SkillSpeed(1f + fValue, fDuration);
            return true;
        }
    }

    /// <summary> 잠깐 몬스터 감속. fValue가 0.55면 몬스터가 0.45배로 움직인다. </summary>
    public class CSkillEffect_Slow : CSkillEffect
    {
        public override bool Try_Apply(float fValue, float fDuration)
        {
            // 몬스터는 플레이어 밖에 있으므로 스테이지에 요청한다.
            if (m_cHost == null || fDuration <= 0f)
                return false;

            m_cHost.Slow_Enemies(Mathf.Clamp(1f - fValue, 0.1f, 1f), fDuration);
            return true;
        }
    }

    /// <summary> 그은 선을 가장 가까운 점령지까지 이어 바로 마감한다. </summary>
    public class CSkillEffect_Seal : CSkillEffect
    {
        public override bool Try_Apply(float fValue, float fDuration)
        {
            return m_cOwner.Seal();
        }
    }
}
