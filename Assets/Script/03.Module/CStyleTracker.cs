using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260922_현란한 동작 판정 — "NEAR MISS!" · "CHAIN ×3" · "DOUBLE TRAP!" 같은 피드백을 띄울 때를 가린다
    /// <summary>
    /// **알아보고 알려 주기만 한다 — 보상은 없다**(스타일 게이지 · 랭크 보상은 기획에서 뺐다).
    /// 잘한 순간을 게임이 알아봐 주는 것만으로 "과감하게 하면 기분이 좋다"가 된다.
    ///
    /// 스테이지를 모르는 순수 계산이라 화면 없이 테스트한다(CProtoTest). 시각은 Tick으로 주입한다.
    /// 무엇을 봤는지는 CStage_Manager가 넘기고, 판정 결과는 CStage_Manager.OnStylish로 올라간다.
    /// </summary>
    public class CStyleTracker
    {
        public const float CHAIN_WINDOW     = 4f;       // 점령 사이가 이 안이면 연속 점령
        public const float BIG_CAPTURE      = 0.10f;    // 한 번에 맵의 이 비율 이상을 먹으면 대형 점령
        public const float NEAR_MARGIN      = 1.5f;     // 몸 판정보다 이만큼(칸) 더 가까이 스치면 아슬아슬
        public const float NEAR_COOLDOWN    = 0.6f;     // 아슬아슬이 연달아 터지면 소음이 된다

        private float m_fTime;
        private float m_fLastCapture = float.NegativeInfinity;
        private int   m_iChain;
        private float m_fLastNearMiss = float.NegativeInfinity;

        // 지금 가까이 붙어 있는 대상. 붙었다가 **맞지 않고 떨어지는 순간**이 아슬아슬이다 —
        // 붙는 순간에 띄우면 그대로 맞아 죽을 때도 "NEAR MISS!"가 떠 우습게 된다.
        private readonly HashSet<int> m_hsNear = new HashSet<int>();
        private readonly HashSet<int> m_hsSeen = new HashSet<int>();
        private readonly List<int>    m_lstGone = new List<int>();

        public int CHAIN => m_iChain;

        public void Tick(float fDeltaTime) => m_fTime += Mathf.Max(0f, fDeltaTime);

        public void Clear()
        {
            m_fLastCapture  = float.NegativeInfinity;
            m_fLastNearMiss = float.NegativeInfinity;
            m_iChain = 0;
            m_hsNear.Clear();
            m_hsSeen.Clear();
        }

        /// <summary> 점령할 때마다. 몇 번째 연속 점령인지 돌려준다(1이면 연속이 아니다). </summary>
        public int On_Capture()
        {
            m_iChain = m_fTime - m_fLastCapture <= CHAIN_WINDOW ? m_iChain + 1 : 1;
            m_fLastCapture = m_fTime;
            return m_iChain;
        }

        public static bool Is_BigCapture(float fRatio) => fRatio >= BIG_CAPTURE;

        #region 아슬아슬
        /// <summary> 한 프레임의 판정을 시작한다. 이번 프레임에 보고되지 않은 대상(죽었거나 사라진)은 조용히 잊는다. </summary>
        public void Begin_Near() => m_hsSeen.Clear();

        /// <summary> 대상 하나가 지금 가까이 있는가를 보고한다. 붙어 있다가 떨어졌으면 true(아슬아슬). </summary>
        public bool Report_Near(int iID, bool bNear)
        {
            m_hsSeen.Add(iID);

            if (bNear == true)
            {
                m_hsNear.Add(iID);
                return false;
            }

            if (m_hsNear.Remove(iID) == false)
                return false;

            if (m_fTime - m_fLastNearMiss < NEAR_COOLDOWN)
                return false;

            m_fLastNearMiss = m_fTime;
            return true;
        }

        public void End_Near()
        {
            m_lstGone.Clear();
            foreach (int iID in m_hsNear)
            {
                if (m_hsSeen.Contains(iID) == false)
                    m_lstGone.Add(iID);
            }

            for (int i = 0; i < m_lstGone.Count; ++i)
                m_hsNear.Remove(m_lstGone[i]);
        }

        /// <summary> 맞았거나 안전 지대로 돌아왔다 — 붙어 있던 것은 아슬아슬이 아니다. </summary>
        public void Clear_Near() => m_hsNear.Clear();
        #endregion 아슬아슬
    }
}
