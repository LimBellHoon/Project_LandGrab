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
        // 260922_0.12 → 0.06. 화면 대비 너무 컸다. 반경이 곧 '끝까지 민 거리'라 절반이면 더 적게 밀어도 방향이 잡힌다
        private const float RADIUS_RATIO    = 0.06f;    // 화면 높이 대비 조이스틱 반경
        private const float DEADZONE_RATIO  = 0.25f;    // 반경 대비 데드존
        private const float ACTIVE_HEIGHT   = 0.6f;     // 화면 아래 이 비율 안에서만 조이스틱을 잡는다
        private const float ACTIVE_WIDTH    = 0.55f;    // 오른쪽은 스킬/아이템 버튼 자리로 비워 둔다

        private readonly CVirtualJoystick m_cJoystick = new CVirtualJoystick();

        private MOVE_DIR m_eDesiredDir = MOVE_DIR.NONE;
        private MOVE_STYLE m_eMoveStyle = MOVE_STYLE.FOUR_WAY;

        public MOVE_DIR         DESIRED_DIR => m_eDesiredDir;
        // 260922_키보드 스킬 키(E · J). 모바일은 화면 버튼이 부른다
        public bool             SKILL_PRESSED { get; private set; }
        /// <summary> UI가 그리기 위해 읽는다. </summary>
        public CVirtualJoystick JOYSTICK    => m_cJoystick;

        public void Initialize()
        {
            float fRadius = Screen.height * RADIUS_RATIO;
            m_cJoystick.Initialize(fRadius, fRadius * DEADZONE_RATIO, ACTIVE_HEIGHT, ACTIVE_WIDTH);
        }

        public void Tick()
        {
            SKILL_PRESSED = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.J);
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

        // 260920_캐릭터별 이동 방식(2-22). 조이스틱과 키보드가 같은 값을 본다.
        public void Set_MoveStyle(MOVE_STYLE eStyle)
        {
            m_eMoveStyle = eStyle;
            m_cJoystick.Set_MoveStyle(eStyle);
        }

        public void Clear()
        {
            m_eDesiredDir = MOVE_DIR.NONE;
            m_cJoystick.Clear();
        }

        private MOVE_DIR Read_Dir()
        {
            // 260920_8방향 캐릭터는 두 축을 같이 누르면 대각선이다. 4방향 캐릭터는 이 분기를 타지 않는다.
            if (m_eMoveStyle == MOVE_STYLE.EIGHT_WAY)
            {
                MOVE_DIR eDiagonal = Read_Diagonal();
                if (eDiagonal != MOVE_DIR.NONE)
                    return eDiagonal;
            }

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

        private static MOVE_DIR Read_Diagonal()
        {
            bool bUp    = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
            bool bDown  = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
            bool bLeft  = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
            bool bRight = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

            if (bUp == true && bLeft  == true) return MOVE_DIR.UP_LEFT;
            if (bUp == true && bRight == true) return MOVE_DIR.UP_RIGHT;
            if (bDown == true && bLeft  == true) return MOVE_DIR.DOWN_LEFT;
            if (bDown == true && bRight == true) return MOVE_DIR.DOWN_RIGHT;

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
