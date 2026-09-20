using UnityEngine;

namespace Client
{
    // 260920_판이 커질수록 카메라가 물러난다 (2-10)
    /// <summary>
    /// 두 가지가 시야를 넓힌다. 둘 다 "지금 화면에 담아야 할 것이 늘었다"는 같은 뜻이라 한 모듈에서 더한다.
    ///
    /// - **그리는 선 길이** — 멀리 나갈수록 돌아올 길과 쫓아오는 몬스터를 같이 봐야 판단이 된다.
    ///   시야가 고정이면 화면 밖에서 잘려 억울하게 죽는다
    /// - **점령률** — 내 땅이 넓어질수록 '어디를 더 먹을지' 고르는 판이 커진다.
    ///   좁은 시야로는 남은 땅의 모양이 안 보여 길을 못 고른다
    ///
    /// <see cref="CCameraPunch"/>와 같은 자리에서 카메라 크기에 곱해진다(CGameManager.Tick_Camera).
    /// 다만 펀치는 순간 연출이라 스스로 줄어들고, 이쪽은 **지금 판 상태를 따라가는 값**이라
    /// 목표 배율을 향해 천천히 붙는다(SmoothDamp) — 한 칸 먹을 때마다 화면이 튀면 어지럽다.
    ///
    /// 화면 없이 테스트한다 — 계산만 하고 카메라는 모른다(CCameraShake와 같은 이유).
    /// </summary>
    public class CCameraZoomOut
    {
        private bool  m_bEnabled = true;
        private float m_fPerTrailCell;    // 트레일 한 칸당 넓어지는 비율
        private float m_fPerOwnedRatio;   // 점령률 1(100%)일 때 넓어지는 비율
        private float m_fMaxRate;         // 배율 상한
        private float m_fFollowTime;      // 목표까지 따라붙는 시간(초)

        private float m_fRate = 1f;
        private float m_fVelocity;

        /// <summary> 지금 카메라 크기에 곱할 배율. 1이면 그대로다. </summary>
        public float RATE => m_fRate;

        /// <param name="fPerTrailCell"> 트레일 한 칸당 넓히는 비율(0.012 = 칸마다 1.2%) </param>
        /// <param name="fPerOwnedRatio"> 점령률에 곱해 더하는 비율(0.5면 100% 점령에서 +50%) </param>
        /// <param name="fMaxRate"> 배율 상한(1.8이면 최대 1.8배까지 넓어진다) </param>
        /// <param name="fFollowTime"> 목표 배율까지 따라붙는 시간(초). 클수록 느긋하다 </param>
        public void Initialize(float fPerTrailCell, float fPerOwnedRatio, float fMaxRate, float fFollowTime)
        {
            m_fPerTrailCell  = Mathf.Max(0f, fPerTrailCell);
            m_fPerOwnedRatio = Mathf.Max(0f, fPerOwnedRatio);
            m_fMaxRate       = Mathf.Max(1f, fMaxRate);
            m_fFollowTime    = Mathf.Max(0.01f, fFollowTime);

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
        /// <param name="fOwnedRatio"> 지금 점령률 0~1(CTerritoryGrid.OWNED_RATIO) </param>
        /// <returns> 카메라 크기에 곱할 배율 </returns>
        public float Tick(int iTrailCount, float fOwnedRatio, float fDeltaTime)
        {
            if (m_bEnabled == false)
                return 1f;

            float fTarget = Get_TargetRate(iTrailCount, fOwnedRatio);
            m_fRate = Mathf.SmoothDamp(m_fRate, fTarget, ref m_fVelocity, m_fFollowTime, Mathf.Infinity, fDeltaTime);
            return m_fRate;
        }

        /// <summary>
        /// 목표 배율. 선 길이와 점령률을 **더한다** — 멀리 나가 있고 땅도 넓으면 둘 다 담아야 한다.
        /// 선을 거두고(0칸) 점령률이 0이면 1배로 돌아온다.
        /// </summary>
        public float Get_TargetRate(int iTrailCount, float fOwnedRatio)
        {
            float fRate = 1f;

            if (iTrailCount > 0)
                fRate += iTrailCount * m_fPerTrailCell;

            fRate += Mathf.Clamp01(fOwnedRatio) * m_fPerOwnedRatio;

            return Mathf.Min(fRate, m_fMaxRate);
        }
    }
}
