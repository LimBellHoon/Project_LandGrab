using UnityEngine;

namespace Client
{
    // 260912_세로 화면 맞춤
    // 260912_추적 모드 — 맵 전체를 보여주지 않고 플레이어 주변만 확대해서 보여준다
    /// <summary>
    /// 카메라가 하는 일은 두 가지다.
    ///
    /// 1. 얼마나 넓게 볼지 정한다.
    ///    · 추적 모드 — 세로로 몇 칸을 보여줄지(`fViewCellHeight`)만 정해 두고 그만큼만 본다.
    ///      **스테이지가 올라가 맵이 커져도 보이는 칸 수는 그대로**라, 맵이 커진 만큼
    ///      플레이어가 좁게 느끼고 이동 속도 강화가 실제로 필요해진다.
    ///    · 전체 모드 — 맵이 한 화면에 다 들어오게 맞춘다. 맵이 시야보다 작으면 자동으로 이쪽이 된다.
    ///
    /// 2. 어디를 볼지 정한다. 추적 모드는 플레이어를 따라가되 **맵 밖이 보이지 않게 가둔다.**
    ///
    /// 위아래 UI 띠는 맵이 쓸 수 없는 영역이라, 가운데 남은 띠를 기준으로 계산한다 —
    /// 화면 정중앙에 맞추면 조이스틱이 플레이어를 가린다.
    ///
    /// 계산은 전부 static이라 화면 없이도 검증할 수 있다(CProtoTest).
    /// </summary>
    public class CCameraFitter
    {
        private Camera      m_cCamera;
        private Vector2     m_vWorldSize;
        private Vector2     m_vWorldCenter;
        private float       m_fTopReserve;
        private float       m_fBottomReserve;
        private float       m_fMargin;

        // 260912_추적 설정
        private bool        m_bFollow;
        private float       m_fViewHeight;      // 월드 단위. 가운데 띠에 담을 세로 길이
        private float       m_fFollowTime;      // 0이면 즉시 따라붙는다

        private Vector2     m_vBandCenter;      // 지금 보고 있는 지점 (UI 띠를 뺀 가운데)
        private Vector2     m_vVelocity;        // SmoothDamp용
        private float       m_fSize;

        // 해상도가 바뀌었는지 보려고 들고 있는다 (회전 · 에디터 Game 뷰 크기 변경)
        private int         m_iLastWidth;
        private int         m_iLastHeight;
        private bool        m_bFitted;

        public bool  IS_FITTED => m_bFitted;
        public float SIZE      => m_fSize;

        /// <param name="fViewHeight"> 추적 모드에서 가운데 띠에 담을 세로 길이(월드). 0 이하면 전체 모드 </param>
        /// <param name="fFollowTime"> 따라붙는 데 걸리는 시간(초). 0이면 즉시 </param>
        public void Initialize(Camera cCamera, float fTopReserve, float fBottomReserve, float fMargin,
                               float fViewHeight = 0f, float fFollowTime = 0f)
        {
            m_cCamera        = cCamera;
            m_fTopReserve    = fTopReserve;
            m_fBottomReserve = fBottomReserve;
            m_fMargin        = fMargin;
            m_fViewHeight    = fViewHeight;
            m_fFollowTime    = Mathf.Max(0f, fFollowTime);
            m_bFollow        = fViewHeight > 0f;
            m_bFitted        = false;
        }

        public void Release()
        {
            m_cCamera = null;
            m_bFitted = false;
        }

        // 260912_스테이지를 깔 때 한 번 부른다. 맵이 바뀌면 다시 부른다.
        /// <param name="vWorldSize"> 맵 전체 크기 </param>
        /// <param name="vWorldCenter"> 맵 중심 </param>
        /// <param name="vTarget"> 처음 볼 지점 (보통 플레이어 시작 위치) </param>
        public void Fit(Vector2 vWorldSize, Vector2 vWorldCenter, Vector2 vTarget)
        {
            m_vWorldSize   = vWorldSize;
            m_vWorldCenter = vWorldCenter;
            m_vVelocity    = Vector2.zero;
            m_bFitted      = true;

            Refresh_Size();

            // 처음에는 따라붙는 연출 없이 바로 그 자리에 둔다 — 시작하자마자 카메라가 미끄러지면 어지럽다.
            m_vBandCenter = Clamp_Center(vTarget, m_vWorldSize, m_vWorldCenter, HALF_VIEW_W, HALF_VIEW_H);
            Apply();
        }

        /// <summary> 맵 전체를 보여주는 예전 방식. 추적을 끄고 쓴다. </summary>
        public void Fit(Vector2 vWorldSize) => Fit(vWorldSize, Vector2.zero, Vector2.zero);

        // 260912_매 프레임 돈다. 화면 크기가 바뀌었을 때만 다시 재고, 나머지는 따라가기만 한다.
        /// <param name="vTarget"> 따라갈 지점 (플레이어 위치) </param>
        public void Tick(Vector2 vTarget, float fDeltaTime)
        {
            if (m_bFitted == false || m_cCamera == null)
                return;

            if (Screen.width != m_iLastWidth || Screen.height != m_iLastHeight)
                Refresh_Size();

            if (m_bFollow == false)
            {
                Apply();
                return;
            }

            Vector2 vWant = Clamp_Center(vTarget, m_vWorldSize, m_vWorldCenter, HALF_VIEW_W, HALF_VIEW_H);

            m_vBandCenter = m_fFollowTime <= 0f
                          ? vWant
                          : Vector2.SmoothDamp(m_vBandCenter, vWant, ref m_vVelocity, m_fFollowTime, float.MaxValue,
                                               fDeltaTime);

            Apply();
        }

        /// <summary> 해상도만 바뀌었는지 보는 예전 호출부용. </summary>
        public void Tick() => Tick(m_vBandCenter, 0f);

        private void Refresh_Size()
        {
            m_iLastWidth  = Screen.width;
            m_iLastHeight = Screen.height;

            float fAspect = m_iLastHeight > 0 ? (float)m_iLastWidth / m_iLastHeight : 1f;

            // 맵이 시야보다 작으면 추적할 것이 없다 — 전체를 보여주는 쪽이 낫다.
            float fFitSize = Calc_Size(m_vWorldSize, fAspect, USABLE_RATIO, m_fMargin);

            if (m_fViewHeight <= 0f)
            {
                m_bFollow = false;
                m_fSize   = fFitSize;
            }
            else
            {
                float fFollowSize = Calc_Size(new Vector2(0f, m_fViewHeight), fAspect, USABLE_RATIO, 0f);
                m_bFollow = fFollowSize < fFitSize;
                m_fSize   = m_bFollow == true ? fFollowSize : fFitSize;
            }

            if (m_bFollow == false)
                m_vBandCenter = m_vWorldCenter;
        }

        private void Apply()
        {
            if (m_cCamera == null)
                return;

            m_cCamera.orthographic     = true;
            m_cCamera.orthographicSize = m_fSize;

            Vector3 vPos = m_cCamera.transform.position;
            m_cCamera.transform.position = new Vector3(
                m_vBandCenter.x,
                m_vBandCenter.y + Calc_PositionY(m_fSize, m_fTopReserve, m_fBottomReserve),
                vPos.z);
        }

        private float USABLE_RATIO => 1f - m_fTopReserve - m_fBottomReserve;
        private float HALF_VIEW_W  => m_fSize * (m_iLastHeight > 0 ? (float)m_iLastWidth / m_iLastHeight : 1f);
        private float HALF_VIEW_H  => m_fSize * USABLE_RATIO;

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
        /// 맵을 UI 띠 사이 한가운데에 두기 위한 카메라 Y 보정값.
        /// 아래를 더 많이 비웠으면 카메라가 내려가고, 보는 지점은 화면에서 위로 올라간다.
        /// </summary>
        public static float Calc_PositionY(float fSize, float fTopReserve, float fBottomReserve)
        {
            return fSize * (fTopReserve - fBottomReserve);
        }

        // 260912_맵 밖이 보이지 않도록 가둔다.
        // 한 축이 시야보다 좁으면 그 축은 가운데 고정 — 가둘 여유가 없는데 억지로 밀면 맵이 흔들린다.
        /// <param name="fHalfViewW"> 보이는 가로의 절반 </param>
        /// <param name="fHalfViewH"> UI 띠를 뺀 세로의 절반 </param>
        public static Vector2 Clamp_Center(Vector2 vTarget, Vector2 vWorldSize, Vector2 vWorldCenter,
                                           float fHalfViewW, float fHalfViewH)
        {
            return new Vector2(
                Clamp_Axis(vTarget.x, vWorldCenter.x, vWorldSize.x, fHalfViewW),
                Clamp_Axis(vTarget.y, vWorldCenter.y, vWorldSize.y, fHalfViewH));
        }

        private static float Clamp_Axis(float fTarget, float fCenter, float fWorldLength, float fHalfView)
        {
            float fHalfWorld = fWorldLength * 0.5f;
            if (fHalfView >= fHalfWorld)
                return fCenter;     // 시야가 맵보다 넓다 — 가운데에 둔다

            return Mathf.Clamp(fTarget, fCenter - fHalfWorld + fHalfView, fCenter + fHalfWorld - fHalfView);
        }
    }
}
