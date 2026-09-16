using UnityEngine;

namespace Client
{
    // 260916_화면 플래시 — 피격 빨강 / 회피 하양
    /// <summary>
    /// 화면 전체를 잠깐 물들이는 연출. 카메라 흔들림·펀치와 달리 카메라가 아니라
    /// UI(전체화면 Image)에 적용되므로 CGameManager가 아니라 <c>CUI_InGame</c>이 들고 있다 —
    /// 세계(카메라)는 GameManager, 화면(UI)은 UI 클래스가 맡는다는 기존 구분을 그대로 따른다.
    ///
    /// 색이 다른 두 원인(피격/회피)이 같은 프레임에 겹치면 **나중에 들어온 쪽이 이긴다.**
    /// 두 색을 섞으면 어느 쪽도 아닌 애매한 색이 나와 무슨 일이 일어났는지 읽기 어려워진다.
    /// </summary>
    public class CFlashEffect
    {
        private Color m_cColor;
        private float m_fDuration;
        private float m_fRemain;

        /// <summary>
        /// 지금 화면에 칠할 색. 알파는 '막 터졌을 때의 최대 세기'다 — 실제로 그릴 때는
        /// 여기에 ALPHA(0~1 감쇠)를 곱해야 한다.
        /// </summary>
        public Color COLOR => m_cColor;

        /// <summary> 0~1. 막 터졌을 때 1이고 시간이 지나며 0으로 선형으로 줄어든다. </summary>
        public float ALPHA => m_fDuration > 0f ? Mathf.Clamp01(m_fRemain / m_fDuration) : 0f;

        /// <param name="cColor"> 칠할 색. 알파가 곧 최대 세기다(예: 0.35면 화면의 35%만 덮은 것처럼 보인다) </param>
        /// <param name="fDuration"> 다 사라지기까지 걸리는 시간(초) </param>
        public void Add_Flash(Color cColor, float fDuration)
        {
            if (fDuration <= 0f)
                return;

            m_cColor     = cColor;
            m_fDuration  = fDuration;
            m_fRemain    = fDuration;
        }

        public void Tick(float fDeltaTime)
        {
            if (m_fRemain <= 0f)
                return;

            m_fRemain = Mathf.Max(0f, m_fRemain - fDeltaTime);
        }

        /// <summary> 스테이지를 새로 깔 때 · UI가 다시 열릴 때 부른다. 이전 판의 플래시가 남지 않게 한다. </summary>
        public void Clear() => m_fRemain = 0f;
    }
}
