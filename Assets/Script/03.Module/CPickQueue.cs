using UnityEngine;

namespace Client
{
    // 260921_3지선다 대기열
    /// <summary>
    /// 3지선다는 **한 번에 한 장만** 연다. 전체 자석으로 조각을 한꺼번에 주우면 게이지가 여러 번 넘치는데,
    /// 예전에는 그 자리에서 창을 연달아 열어 앞 창이 닫히지 않고 남아 화면 입력을 전부 막았다.
    /// 이제 넘친 만큼 쌓아 두고, 지금 창을 고른 뒤에 다음 창을 연다.
    /// 스테이지도 UI도 모르는 순수 계산이라 화면 없이 테스트한다(CProtoTest).
    /// </summary>
    public class CPickQueue
    {
        private int  m_iPending;
        private bool m_bOpen;

        public int  PENDING => m_iPending;
        public bool IS_OPEN => m_bOpen;

        public void Add(int iCount) => m_iPending += Mathf.Max(0, iCount);

        /// <summary> 열린 창이 없고 기다리는 것이 있으면 하나를 꺼내 연 것으로 친다. </summary>
        public bool Try_Open()
        {
            if (m_bOpen == true || m_iPending <= 0)
                return false;

            --m_iPending;
            m_bOpen = true;
            return true;
        }

        public void Close() => m_bOpen = false;

        public void Clear()
        {
            m_iPending = 0;
            m_bOpen    = false;
        }
    }
}
