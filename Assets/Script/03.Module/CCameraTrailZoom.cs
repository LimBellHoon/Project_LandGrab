using UnityEngine;

namespace Client
{
    // 260920_선을 길게 그을수록 카메라가 부드럽게 물러난다 (2-10)
    /// <summary>
    /// 멀리 나갈수록 **돌아올 길과 쫓아오는 몬스터를 같이 봐야** 판단할 수 있는데,
    /// 시야가 고정이면 화면 밖에서 잘리는 정보가 많아 억울하게 죽는다.
    /// 그래서 트레일 길이에 따라 시야를 넓힌다 — 길게 나가는 선택이 '보이지 않아서' 불리해지지 않게.
    ///
    /// <see cref="CCameraPunch"/>와 같은 자리에서 카메라 크기에 곱해진다(CGameManager.Tick_Camera).
    /// 다만 펀치는 순간 연출이라 스스로 줄어들고, 이쪽은 **지금 선 길이를 따라가는 상태값**이라
    /// 목표 배율을 향해 천천히 붙는다(SmoothDamp) — 선이 한 칸 늘 때마다 화면이 튀면 어지럽다.
    ///
    /// 화면 없이 테스트한다 — 계산만 하고 카메라는 모른다(CCameraShake와 같은 이유).
    /// </summary>
    public class CCameraTrailZoom
    {
        private bool  m_bEnabled = true;
        private float m_fPerCell;       // 트레일 한 칸당 넓어지는 비율
        private float m_fMaxRate;       // 가장 많이 넓어졌을 때의 배율 상한
        private float m_fFollowTime;    // 목표까지 따라붙는 시간(초)

        private float m_fRate = 1f;
        private float m_fVelocity;

        /// <summary> 지금 카메라 크기에 곱할 배율. 1이면 그대로다. </summary>
        public float RATE => m_fRate;

        /// <param name="fPerCell"> 트레일 한 칸당 넓히는 비율(0.01 = 칸마다 1%) </param>
        /// <param name="fMaxRate"> 배율 상한(1.6이면 최대 1.6배까지 넓어진다) </param>
        /// <param name="fFollowTime"> 목표 배율까지 따라붙는 시간(초). 클수록 느긋하다 </param>
        public void Initialize(float fPerCell, float fMaxRate, float fFollowTime)
        {
            m_fPerCell    = Mathf.Max(0f, fPerCell);
            m_fMaxRate    = Mathf.Max(1f, fMaxRate);
            m_fFollowTime = Mathf.Max(0.01f, fFollowTime);

            Reset();
        }

        /// <summary> 스테이지를 새로 깔 때 — 지난 판의 배율을 물고 들어가지 않게 한다. </summary>
        public void Reset()
        {
            m_fRate     = 1f;
            m_fVelocity = 0f;
        }

        /// <summary> 끄면 곧바로 1배로 돌아온다(CCameraShake.Set_Enabled와 같은 자리, 1-6). </summary>
        public void Set_Enabled(bool bEnabled)
        {
            m_bEnabled = bEnabled;
            if (bEnabled == false)
                Reset();
        }

        /// <param name="iTrailCount"> 지금 그리는 중인 선의 칸 수(CTerritoryGrid.TRAIL_COUNT) </param>
        /// <returns> 카메라 크기에 곱할 배율 </returns>
        public float Tick(int iTrailCount, float fDeltaTime)
        {
            if (m_bEnabled == false)
                return 1f;

            float fTarget = Get_TargetRate(iTrailCount);
            m_fRate = Mathf.SmoothDamp(m_fRate, fTarget, ref m_fVelocity, m_fFollowTime, Mathf.Infinity, fDeltaTime);
            return m_fRate;
        }

        /// <summary> 선 길이에 대한 목표 배율. 선을 거두면(0칸) 1배로 돌아온다. </summary>
        public float Get_TargetRate(int iTrailCount)
        {
            if (iTrailCount <= 0)
                return 1f;

            return Mathf.Min(1f + iTrailCount * m_fPerCell, m_fMaxRate);
        }
    }
}
