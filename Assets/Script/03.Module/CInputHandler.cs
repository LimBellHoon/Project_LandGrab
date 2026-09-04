using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 입력 → 4방향
    // 260904_모바일 가상 조이스틱 추가
    /// <summary>
    /// 입력이 어디서 오든 결과는 4방향 하나다.
    /// 조이스틱을 잡고 있으면 그쪽을 따르고, 아니면 WASD/방향키를 본다 —
    /// 덕분에 에디터에서는 키보드로, 기기에서는 터치로 같은 코드가 굴러간다.
    /// (키보드는 가장 마지막에 누른 방향을 우선해 대각 입력에서도 방향이 흔들리지 않는다)
    /// </summary>
    public class CInputHandler
    {
        // 화면 크기가 제각각이라 픽셀을 그대로 적지 않고 화면 높이에 대한 비율로 잡는다.
        private const float RADIUS_RATIO    = 0.12f;    // 화면 높이 대비 조이스틱 반경
        private const float DEADZONE_RATIO  = 0.25f;    // 반경 대비 데드존
        private const float ACTIVE_HEIGHT   = 0.6f;     // 화면 아래 이 비율 안에서만 조이스틱을 잡는다

        private readonly CVirtualJoystick m_cJoystick = new CVirtualJoystick();

        private MOVE_DIR m_eDesiredDir = MOVE_DIR.NONE;

        public MOVE_DIR         DESIRED_DIR => m_eDesiredDir;
        /// <summary> UI가 그리기 위해 읽는다. </summary>
        public CVirtualJoystick JOYSTICK    => m_cJoystick;

        public void Initialize()
        {
            float fRadius = Screen.height * RADIUS_RATIO;
            m_cJoystick.Initialize(fRadius, fRadius * DEADZONE_RATIO, ACTIVE_HEIGHT);
        }

        public void Tick()
        {
            m_cJoystick.Tick();

            // 조이스틱을 잡고 있는 동안에는 키보드를 보지 않는다.
            // 데드존 안이면 NONE이 나오는데, 그건 '멈춤'이라는 뜻이라 그대로 쓴다.
            if (m_cJoystick.IS_ACTIVE == true)
            {
                m_eDesiredDir = m_cJoystick.DIR;
                return;
            }

            // 아무것도 안 눌렸으면 NONE을 유지한다 (안전 지대에서 멈추기 위함)
            m_eDesiredDir = Read_Dir();
        }

        public void Clear()
        {
            m_eDesiredDir = MOVE_DIR.NONE;
            m_cJoystick.Clear();
        }

        private MOVE_DIR Read_Dir()
        {
            // 새로 눌린 키를 최우선으로 잡아 방향 전환 반응을 즉각적으로 만든다.
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))    return MOVE_DIR.UP;
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))  return MOVE_DIR.DOWN;
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))  return MOVE_DIR.LEFT;
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) return MOVE_DIR.RIGHT;

            // 누르고 있는 중이면 기존 방향 유지
            if (m_eDesiredDir != MOVE_DIR.NONE && Is_Holding(m_eDesiredDir) == true)
                return m_eDesiredDir;

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    return MOVE_DIR.UP;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  return MOVE_DIR.DOWN;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  return MOVE_DIR.LEFT;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) return MOVE_DIR.RIGHT;

            return MOVE_DIR.NONE;
        }

        private bool Is_Holding(MOVE_DIR eDir)
        {
            switch (eDir)
            {
                case MOVE_DIR.UP:    return Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
                case MOVE_DIR.DOWN:  return Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
                case MOVE_DIR.LEFT:  return Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
                case MOVE_DIR.RIGHT: return Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
                default:             return false;
            }
        }
    }
}
