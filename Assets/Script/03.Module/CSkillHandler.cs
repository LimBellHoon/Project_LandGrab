using UnityEngine;

namespace Client
{
    // 260905_액티브 스킬 쿨타임
    /// <summary>
    /// 쿨타임만 센다. 무슨 일이 일어나는지는 스킬을 가진 쪽(CPlayer)이 정한다 —
    /// 그래야 스킬이 늘어나도 이 클래스는 그대로 쓸 수 있다.
    /// UI는 COOL_RATIO만 읽어 게이지를 채운다.
    /// </summary>
    public class CSkillHandler
    {
        private CSkillInfo  m_cInfo;
        private float       m_fRemain;
        private int         m_iLevel;       // 260905_강화 레벨

        public CSkillInfo   INFO        => m_cInfo;
        public bool         HAS_SKILL   => m_cInfo != null;
        public bool         IS_READY    => m_cInfo != null && m_fRemain <= 0f;
        public float        REMAIN      => m_fRemain;
        public int          LEVEL       => m_iLevel;
        /// <summary> 260905_강화 레벨이 반영된 수치. WARP은 이동할 칸 수. </summary>
        public float        VALUE       => m_cInfo != null ? m_cInfo.Get_Value(m_iLevel) : 0f;

        /// <summary> 남은 쿨타임 비율 0~1. 1이면 방금 썼고, 0이면 쓸 수 있다. </summary>
        public float COOL_RATIO
        {
            get
            {
                if (m_cInfo == null || m_cInfo.fCoolTime <= 0f)
                    return 0f;

                return Mathf.Clamp01(m_fRemain / m_cInfo.fCoolTime);
            }
        }

        /// <param name="iLevel"> 강화 레벨. 0이면 기본 수치 </param>
        public void Initialize(CSkillInfo cInfo, int iLevel)
        {
            m_cInfo   = cInfo;
            m_iLevel  = Mathf.Max(0, iLevel);
            m_fRemain = 0f;     // 스테이지 시작하자마자 한 번은 쓸 수 있게 한다
        }

        public void Tick(float fDeltaTime)
        {
            if (m_fRemain <= 0f)
                return;

            m_fRemain = Mathf.Max(0f, m_fRemain - fDeltaTime);
        }

        /// <summary> 쿨타임을 돌린다. 준비되지 않았으면 아무 일도 없다. </summary>
        /// <returns> 실제로 발동됐으면 true </returns>
        public bool Try_Use()
        {
            if (IS_READY == false)
                return false;

            m_fRemain = m_cInfo.fCoolTime;
            return true;
        }

        public void Clear() => m_fRemain = 0f;
    }
}
