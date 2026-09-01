using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 입력 → 4방향
    /// <summary>
    /// 프로토타입은 WASD/방향키. 추후 조이스틱으로 교체할 때 이 클래스 하나만 갈아끼우면 된다.
    /// (가장 마지막에 누른 방향을 우선하여 대각 입력에서도 방향이 흔들리지 않게 한다)
    /// </summary>
    public class CInputHandler
    {
        private MOVE_DIR m_eDesiredDir = MOVE_DIR.NONE;

        public MOVE_DIR DESIRED_DIR => m_eDesiredDir;

        public void Tick()
        {
            MOVE_DIR eNew = Read_Dir();

            // 아무 키도 안 눌렸으면 NONE을 유지한다 (안전 지대에서 멈추기 위함)
            m_eDesiredDir = eNew;
        }

        public void Clear() => m_eDesiredDir = MOVE_DIR.NONE;

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
