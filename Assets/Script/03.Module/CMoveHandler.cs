using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 셀 단위 이동 + 셀 사이 보간
    /// <summary>
    /// 플레이어는 항상 셀 격자 위를 움직인다. 셀과 셀 사이는 보간해서 부드럽게 보이지만,
    /// 게임 규칙(트레일/점령/사망) 판정은 셀에 '도착'하는 순간에만 일어난다.
    /// </summary>
    public class CMoveHandler
    {
        private CTerritoryGrid  m_cGrid;

        private Vector2Int      m_vCurCell;
        private Vector2Int      m_vNextCell;
        private MOVE_DIR        m_eCurDir   = MOVE_DIR.NONE;
        private float           m_fProgress;                // 현재 셀 → 다음 셀 진행도 0~1
        private float           m_fSpeed;                   // 초당 이동 셀 수
        private bool            m_bMoving;

        public Vector2Int   CUR_CELL    => m_vCurCell;
        public MOVE_DIR     CUR_DIR     => m_eCurDir;
        public float        SPEED       { get { return m_fSpeed; } set { m_fSpeed = Mathf.Max(0f, value); } }

        public Vector3 WORLD_POS
        {
            get
            {
                Vector3 vFrom = m_cGrid.Cell_ToWorld(m_vCurCell);
                if (m_bMoving == false)
                    return vFrom;

                return Vector3.Lerp(vFrom, m_cGrid.Cell_ToWorld(m_vNextCell), m_fProgress);
            }
        }

        public bool Initialize(CTerritoryGrid cGrid, Vector2Int vStartCell, float fSpeed)
        {
            if (cGrid == null)
            {
                Debug.LogError("[CMoveHandler] Grid가 null 입니다.");
                return false;
            }

            m_cGrid  = cGrid;
            m_fSpeed = Mathf.Max(0f, fSpeed);
            Teleport(vStartCell);
            return true;
        }

        /// <summary> 사망 후 부활 등, 이동 상태를 통째로 리셋하고 특정 셀로 옮긴다. </summary>
        public void Teleport(Vector2Int vCell)
        {
            m_vCurCell  = vCell;
            m_vNextCell = vCell;
            m_eCurDir   = MOVE_DIR.NONE;
            m_fProgress = 0f;
            m_bMoving   = false;
        }

        /// <summary>
        /// 이동을 진행한다.
        /// </summary>
        /// <returns> 이번 프레임에 새 셀에 도착했으면 true (규칙 판정 시점) </returns>
        public bool Tick(float fDeltaTime, MOVE_DIR eDesiredDir, out Vector2Int vArrivedCell)
        {
            vArrivedCell = m_vCurCell;

            if (m_bMoving == false && Try_StartMove(eDesiredDir) == false)
                return false;

            m_fProgress += m_fSpeed * fDeltaTime;
            if (m_fProgress < 1f)
                return false;

            // 남은 진행도는 다음 셀로 이월해 프레임레이트에 따라 속도가 달라지지 않게 한다.
            m_fProgress = Mathf.Min(m_fProgress - 1f, 0.999f);

            m_vCurCell   = m_vNextCell;
            m_bMoving    = false;
            vArrivedCell = m_vCurCell;
            return true;
        }

        private bool Try_StartMove(MOVE_DIR eDesiredDir)
        {
            MOVE_DIR eDir = eDesiredDir;

            if (Can_Move(eDir) == false)
            {
                // 미점령 지대에서는 멈출 수 없다 — 입력이 없거나 막혔으면 진행 방향을 유지한다.
                eDir = m_cGrid.IS_DRAWING == true ? m_eCurDir : MOVE_DIR.NONE;

                if (Can_Move(eDir) == false)
                {
                    m_fProgress = 0f;
                    return false;
                }
            }

            m_eCurDir   = eDir;
            m_vNextCell = m_vCurCell + CTerritoryGrid.Dir_ToOffset(eDir);
            m_bMoving   = true;
            return true;
        }

        private bool Can_Move(MOVE_DIR eDir)
        {
            if (eDir == MOVE_DIR.NONE)
                return false;

            Vector2Int vNext = m_vCurCell + CTerritoryGrid.Dir_ToOffset(eDir);
            if (m_cGrid.Is_InBounds(vNext.x, vNext.y) == false)
                return false;

            // 선을 그리는 중 뒤로 꺾어 자기 선을 밟는 즉사를 막는다(입력 실수 방지).
            if (m_cGrid.IS_DRAWING == true
                && m_cGrid.Try_Get_PrevTrailCell(out Vector2Int vPrevTrail) == true
                && vNext == vPrevTrail)
            {
                return false;
            }

            return true;
        }
    }
}
