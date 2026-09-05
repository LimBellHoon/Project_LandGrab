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
        private bool            m_bFollowing;               // 직전 이동이 선분 자동 추적이었는가

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
        public void Teleport(Vector2Int vCell) => Teleport(vCell, MOVE_DIR.NONE);

        // 260905_워프는 진행 방향을 지키며 순간 이동한다.
        // 방향을 NONE으로 되돌리면 미점령 지대에서 멈추지 못하는 규칙에 걸려 그대로 군다.
        /// <param name="eKeepDir"> 유지할 진행 방향. NONE이면 멈춘다. </param>
        public void Teleport(Vector2Int vCell, MOVE_DIR eKeepDir)
        {
            m_vCurCell  = vCell;
            m_vNextCell = vCell;
            m_eCurDir    = eKeepDir;
            m_fProgress  = 0f;
            m_bMoving    = false;
            m_bFollowing = false;
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

            bool bFollowing = false;

            if (Can_Move(eDir) == false)
            {
                // 미점령 지대에서는 멈출 수 없다 — 입력이 없거나 막혔으면 진행 방향을 유지한다.
                // 반대로 선 위에서는 가려던 방향이 막혀도 선이 꺾여 이어지면 그쪽으로 따라간다.
                if (m_cGrid.IS_DRAWING == true)
                {
                    eDir = m_eCurDir;
                }
                else
                {
                    eDir = Find_FollowDir(eDesiredDir);
                    bFollowing = eDir != MOVE_DIR.NONE;
                }

                if (Can_Move(eDir) == false)
                {
                    m_fProgress = 0f;
                    return false;
                }
            }

            m_bFollowing = bFollowing;
            m_eCurDir   = eDir;
            m_vNextCell = m_vCurCell + CTerritoryGrid.Dir_ToOffset(eDir);
            m_bMoving   = true;
            return true;
        }

        // 260902_선분 자동 추적
        /// <summary>
        /// 가려던 방향이 막혔을 때, 그 방향과 수직으로 이어지는 '선'을 찾는다.
        /// 미점령 지대로 나가는 방향은 후보에서 제외한다 — 안 그러면 벽에 부딪힐 때마다
        /// 의도치 않게 땅따먹기가 시작돼 그대로 죽는다.
        /// </summary>
        private MOVE_DIR Find_FollowDir(MOVE_DIR eDesiredDir)
        {
            if (eDesiredDir == MOVE_DIR.NONE)
                return MOVE_DIR.NONE;

            CTerritoryGrid.Dir_Perpendicular(eDesiredDir, out MOVE_DIR eFirst, out MOVE_DIR eSecond);

            bool bFirst  = Can_Follow(eFirst);
            bool bSecond = Can_Follow(eSecond);

            // 길이 하나뿐이면 고민 없이 그 길로
            if (bFirst != bSecond)
                return bFirst == true ? eFirst : eSecond;

            if (bFirst == false)
                return MOVE_DIR.NONE;

            // 갈림길 — 가던 방향을 이어갈 수 있으면 잇고, 아니면 멈춰서 플레이어가 고르게 한다.
            if (m_eCurDir == eFirst || m_eCurDir == eSecond)
                return m_eCurDir;

            return MOVE_DIR.NONE;
        }

        private bool Can_Follow(MOVE_DIR eDir)
        {
            // 자동 추적으로 들어온 길을 자동 추적으로 되돌아가면 막다른 길에서 무한 왕복한다.
            // (플레이어가 직접 방향을 눌러 되돌아가는 것은 막지 않는다)
            if (m_bFollowing == true && eDir == CTerritoryGrid.Dir_Reverse(m_eCurDir))
                return false;

            Vector2Int vNext = m_vCurCell + CTerritoryGrid.Dir_ToOffset(eDir);
            if (m_cGrid.Get_Cell(vNext) != CELL_STATE.OWNED)
                return false;

            return Can_Move(eDir);
        }

        private bool Can_Move(MOVE_DIR eDir)
        {
            if (eDir == MOVE_DIR.NONE)
                return false;

            Vector2Int vNext = m_vCurCell + CTerritoryGrid.Dir_ToOffset(eDir);
            if (m_cGrid.Is_InBounds(vNext.x, vNext.y) == false)
                return false;

            // 260904_맵 모양 마스크로 잘라낸 칸은 맵 밖과 똑같이 취급한다.
            if (m_cGrid.Is_Blocked(vNext) == true)
                return false;

            // 선을 그리는 중 뒤로 꺾어 자기 선을 밟는 즉사를 막는다(입력 실수 방지).
            if (m_cGrid.IS_DRAWING == true
                && m_cGrid.Try_Get_PrevTrailCell(out Vector2Int vPrevTrail) == true
                && vNext == vPrevTrail)
            {
                return false;
            }

            // 260902_점령지 '내부'는 통과할 수 없다 — 영토의 선(경계)만 따라 움직인다.
            // 단, 점령 직후 자기가 내부에 갇힌 경우에는 선으로 빠져나가야 하므로 허용한다.
            // (선을 그리는 중에는 현재 칸이 TRAIL이라 Is_Boundary가 false → 도형을 닫는 이동은 항상 통과)
            if (m_cGrid.Get_Cell(vNext) == CELL_STATE.OWNED
                && m_cGrid.Is_Boundary(vNext) == false
                && m_cGrid.Is_Boundary(m_vCurCell) == true)
            {
                return false;
            }

            return true;
        }
    }
}
