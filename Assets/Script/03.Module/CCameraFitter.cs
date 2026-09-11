using UnityEngine;

namespace Client
{
    // 260912_세로 화면 맞춤
    /// <summary>
    /// 맵 전체가 화면 안에 들어오도록 직교 카메라 크기와 위치를 정한다.
    ///
    /// 높이로만 맞추면 9:16처럼 좁은 화면에서 맵 좌우가 잘린다.
    /// (맵 1은 월드 7.2 x 12.0인데 9:16에서 높이로만 맞추면 가로가 7.09밖에 안 보인다.)
    /// 그래서 가로 기준과 세로 기준으로 각각 필요한 크기를 구해 큰 쪽을 쓴다.
    ///
    /// 위아래로 예약해 둔 UI 띠는 맵이 쓸 수 없는 영역이다.
    /// 남은 띠 안에 맵을 넣고 카메라를 그 띠의 한가운데로 옮긴다 —
    /// 화면 정중앙에 두면 조이스틱이 맵 아래쪽을 가린다.
    ///
    /// 계산은 전부 static이라 화면 없이도 검증할 수 있다(CProtoTest).
    /// </summary>
    public class CCameraFitter
    {
        private Camera      m_cCamera;
        private Vector2     m_vWorldSize;
        private float       m_fTopReserve;
        private float       m_fBottomReserve;
        private float       m_fMargin;

        // 해상도가 바뀌었는지 보려고 들고 있는다 (회전 · 에디터 Game 뷰 크기 변경)
        private int         m_iLastWidth;
        private int         m_iLastHeight;
        private bool        m_bFitted;

        public bool IS_FITTED => m_bFitted;

        public void Initialize(Camera cCamera, float fTopReserve, float fBottomReserve, float fMargin)
        {
            m_cCamera        = cCamera;
            m_fTopReserve    = fTopReserve;
            m_fBottomReserve = fBottomReserve;
            m_fMargin        = fMargin;
            m_bFitted        = false;
        }

        public void Release()
        {
            m_cCamera = null;
            m_bFitted = false;
        }

        /// <summary> 맵이 정해졌을 때 한 번 부른다. 그 뒤에는 Tick이 알아서 다시 맞춘다. </summary>
        public void Fit(Vector2 vWorldSize)
        {
            m_vWorldSize = vWorldSize;
            m_bFitted    = true;
            Apply();
        }

        /// <summary> 화면 크기가 바뀌었을 때만 다시 맞춘다. 매 프레임 카메라를 건드리지 않는다. </summary>
        public void Tick()
        {
            if (m_bFitted == false)
                return;

            if (Screen.width == m_iLastWidth && Screen.height == m_iLastHeight)
                return;

            Apply();
        }

        private void Apply()
        {
            if (m_cCamera == null)
                return;

            m_iLastWidth  = Screen.width;
            m_iLastHeight = Screen.height;

            float fAspect = m_iLastHeight > 0 ? (float)m_iLastWidth / m_iLastHeight : 1f;
            float fSize   = Calc_Size(m_vWorldSize, fAspect, USABLE_RATIO, m_fMargin);

            m_cCamera.orthographic     = true;
            m_cCamera.orthographicSize = fSize;

            Vector3 vPos = m_cCamera.transform.position;
            m_cCamera.transform.position = new Vector3(0f, Calc_PositionY(fSize, m_fTopReserve, m_fBottomReserve),
                                                       vPos.z);
        }

        private float USABLE_RATIO => 1f - m_fTopReserve - m_fBottomReserve;

        /// <summary>
        /// 맵이 다 들어가는 직교 카메라 크기(= 보이는 높이의 절반).
        /// 가로가 모자라면 가로가, 세로가 모자라면 세로가 기준이 된다.
        /// </summary>
        /// <param name="fAspect"> 화면 가로 / 세로 </param>
        /// <param name="fUsableRatio"> UI 띠를 뺀 뒤 맵이 쓸 수 있는 세로 비율 </param>
        public static float Calc_Size(Vector2 vWorldSize, float fAspect, float fUsableRatio, float fMargin)
        {
            float fSafeAspect = Mathf.Max(0.01f, fAspect);
            float fSafeUsable = Mathf.Clamp(fUsableRatio, 0.05f, 1f);

            float fByWidth  = Mathf.Max(0f, vWorldSize.x) / (2f * fSafeAspect);
            float fByHeight = Mathf.Max(0f, vWorldSize.y) / (2f * fSafeUsable);

            return Mathf.Max(fByWidth, fByHeight) + Mathf.Max(0f, fMargin);
        }

        /// <summary>
        /// 맵을 UI 띠 사이 한가운데에 두기 위한 카메라 Y.
        /// 아래를 더 많이 비웠으면 카메라가 내려가고, 맵은 화면에서 위로 올라간다.
        /// </summary>
        public static float Calc_PositionY(float fSize, float fTopReserve, float fBottomReserve)
        {
            return fSize * (fTopReserve - fBottomReserve);
        }
    }
}
