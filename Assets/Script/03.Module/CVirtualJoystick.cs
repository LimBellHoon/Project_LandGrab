using UnityEngine;

namespace Client
{
    // 260904_모바일 가상 조이스틱 (판정)
    /// <summary>
    /// 화면 좌표만 다루는 순수 클래스다. 그리는 것은 CUI_InGame이 하고 여기서는 판정만 한다.
    ///
    /// EventSystem(uGUI 드래그)에 기대지 않고 Input을 직접 읽는다.
    /// 이 프로젝트는 Input System 패키지가 들어 있지만 코드가 구 Input을 쓰고 있어
    /// 어느 입력 모듈이 살아 있는지 확실하지 않다. 조작이 통째로 죽는 위험을 피하려고
    /// 이미 동작이 증명된 경로(구 Input)를 쓴다.
    ///
    /// 누른 자리가 곧 조이스틱의 중심이 되는 '플로팅' 방식이다 —
    /// 세로 화면에서 엄지가 닿는 자리가 매번 다르기 때문이다.
    /// </summary>
    public class CVirtualJoystick
    {
        private float   m_fRadius;          // 손잡이가 움직일 수 있는 최대 반경(픽셀)
        private float   m_fDeadZone;        // 이 안에서는 방향으로 치지 않는다(픽셀)
        private float   m_fActiveHeight;    // 화면 아래 이 비율 안에서 눌러야 조이스틱이 잡힌다

        private bool        m_bActive;
        private Vector2     m_vOrigin;
        private Vector2     m_vHandle;
        private MOVE_DIR    m_eDir = MOVE_DIR.NONE;

        public bool     IS_ACTIVE   => m_bActive;
        public Vector2  ORIGIN      => m_vOrigin;
        public Vector2  HANDLE      => m_vHandle;
        public MOVE_DIR DIR         => m_eDir;
        public float    RADIUS      => m_fRadius;

        /// <param name="fRadius"> 손잡이 최대 반경(픽셀) </param>
        /// <param name="fDeadZone"> 방향으로 인정하기 시작하는 최소 거리(픽셀) </param>
        /// <param name="fActiveHeight"> 조이스틱을 잡을 수 있는 화면 아래쪽 비율 (0~1) </param>
        public void Initialize(float fRadius, float fDeadZone, float fActiveHeight)
        {
            m_fRadius       = Mathf.Max(1f, fRadius);
            m_fDeadZone     = Mathf.Clamp(fDeadZone, 0f, m_fRadius);
            m_fActiveHeight = Mathf.Clamp01(fActiveHeight);
            Clear();
        }

        public void Clear()
        {
            m_bActive = false;
            m_eDir    = MOVE_DIR.NONE;
        }

        /// <summary> 매 프레임 Input을 읽어 상태를 갱신한다. </summary>
        public void Tick()
        {
            Read_Input(out bool bPressed, out Vector2 vScreenPos);
            Update_State(bPressed, vScreenPos, Screen.height);
        }

        // 판정만 떼어 둔다 — 화면도 입력 장치도 없는 곳에서 검증할 수 있어야 하기 때문이다.
        /// <param name="iScreenHeight"> 활성 영역 판정에 쓸 화면 높이 </param>
        public void Update_State(bool bPressed, Vector2 vScreenPos, int iScreenHeight)
        {
            if (bPressed == false)
            {
                Clear();
                return;
            }

            if (m_bActive == false)
            {
                // 화면 위쪽은 조이스틱으로 잡지 않는다 — 나중에 붙을 일시정지 버튼 같은 것을 위해서다.
                if (vScreenPos.y > iScreenHeight * m_fActiveHeight)
                    return;

                m_bActive = true;
                m_vOrigin = vScreenPos;
            }

            Vector2 vDelta = vScreenPos - m_vOrigin;
            if (vDelta.magnitude > m_fRadius)
                vDelta = vDelta.normalized * m_fRadius;

            m_vHandle = m_vOrigin + vDelta;
            m_eDir    = To_Dir(vDelta, m_fDeadZone);
        }

        /// <summary> 기울인 방향을 4방향 중 하나로 자른다. 대각선은 더 많이 기운 축을 따른다. </summary>
        public static MOVE_DIR To_Dir(Vector2 vDelta, float fDeadZone)
        {
            if (vDelta.magnitude < fDeadZone)
                return MOVE_DIR.NONE;

            if (Mathf.Abs(vDelta.x) >= Mathf.Abs(vDelta.y))
                return vDelta.x >= 0f ? MOVE_DIR.RIGHT : MOVE_DIR.LEFT;

            return vDelta.y >= 0f ? MOVE_DIR.UP : MOVE_DIR.DOWN;
        }

        // 터치가 있으면 터치, 없으면 마우스 — 에디터에서도 드래그로 그대로 조작된다.
        private static void Read_Input(out bool bPressed, out Vector2 vScreenPos)
        {
            if (Input.touchCount > 0)
            {
                Touch cTouch = Input.GetTouch(0);
                bPressed   = cTouch.phase != TouchPhase.Ended && cTouch.phase != TouchPhase.Canceled;
                vScreenPos = cTouch.position;
                return;
            }

            bPressed   = Input.GetMouseButton(0);
            vScreenPos = Input.mousePosition;
        }
    }
}
