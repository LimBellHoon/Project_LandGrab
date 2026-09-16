using UnityEngine;

namespace Client
{
    // 260916_카메라 펀치줌 — 점령 순간을 강조한다
    /// <summary>
    /// CCameraShake와 같은 모양이지만 흔드는 대신 **잠깐 확대했다가 되돌아온다.**
    /// 계산(감쇠·세기 곡선)은 <see cref="CCameraFeel_Utility"/>를 같이 쓴다 —
    /// 둘 다 "0~1이 쌓였다가 스스로 줄어든다"는 같은 모양이라서다(1-1).
    ///
    /// 흔들림과 마찬가지로 "얼마나 세게 당길지"만 안다. 무엇이 당길 자격이 있는지는 모른다.
    /// 새 원인을 추가할 때 여기는 고치지 않는다 — CGameConfig에 값 하나, Add_Punch 호출 한 줄.
    /// </summary>
    public class CCameraPunch
    {
        private bool  m_bEnabled = true;
        private float m_fMaxZoomRatio;     // 펀치 1일 때 줄어드는 비율(0.06 = 6% 확대)
        private float m_fDecayPerSecond;

        private float m_fPunch;            // 0~1

        /// <summary> 디버그 표시용. </summary>
        public float PUNCH => m_fPunch;

        /// <param name="fMaxZoomRatio"> 펀치가 가득 찼을 때 카메라 크기를 줄이는 비율. 0.06이면 6% 확대되어 보인다 </param>
        /// <param name="fDecayPerSecond"> 펀치가 초당 줄어드는 양 </param>
        public void Initialize(float fMaxZoomRatio, float fDecayPerSecond)
        {
            m_fMaxZoomRatio   = Mathf.Clamp01(fMaxZoomRatio);
            m_fDecayPerSecond = Mathf.Max(0.01f, fDecayPerSecond);
        }

        /// <summary> CCameraShake.Set_Enabled와 같은 자리 — 옵션창이 생기면 여기를 토글한다. </summary>
        public void Set_Enabled(bool bEnabled)
        {
            m_bEnabled = bEnabled;
            if (bEnabled == false)
                m_fPunch = 0f;
        }

        /// <summary> 스테이지를 새로 깔 때 부른다. </summary>
        public void Reset() => m_fPunch = 0f;

        /// <param name="fAmount"> 더할 펀치 양 0~1. 여러 번 겹치면 누적된다(최대 1) </param>
        public void Add_Punch(float fAmount)
        {
            if (m_bEnabled == false || fAmount <= 0f)
                return;

            m_fPunch = Mathf.Clamp01(m_fPunch + fAmount);
        }

        /// <returns> 이번 프레임 카메라 크기에 곱할 배율. 1이면 평소 그대로, 작을수록 확대되어 보인다 </returns>
        public float Tick(float fDeltaTime)
        {
            if (m_fPunch <= 0f)
                return 1f;

            m_fPunch = CCameraFeel_Utility.Decay(m_fPunch, m_fDecayPerSecond, fDeltaTime);
            return Calc_ZoomScale(m_fPunch, m_fMaxZoomRatio);
        }

        // 260916_화면 없이 검증하려고 순수 함수로 뺐다.
        public static float Calc_ZoomScale(float fPunch, float fMaxZoomRatio)
        {
            float fStrength = CCameraFeel_Utility.Curve(fPunch);
            return 1f - fStrength * Mathf.Clamp01(fMaxZoomRatio);
        }
    }
}
