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
        // 260920_캐릭터별 이동 방식(2-22). 대각선 입력은 '가로 한 칸 → 세로 한 칸'으로 밟는다.
        // m_ePendingDir이 그 두 번째 칸이다 — 첫 칸에 도착하면 입력과 상관없이 곧바로 이어 간다.
        private MOVE_STYLE      m_eMoveStyle = MOVE_STYLE.FOUR_WAY;
        private MOVE_DIR        m_ePendingDir = MOVE_DIR.NONE;
        // 260921_나선형(2-22) — 입력이 없으면 몇 칸마다 스스로 꺾는다. 그 칸 수를 센다.
        private int             m_iSpiralStep;

        // 260916_런 스킬(뱀서라이크) — '월보'/'어디로든 신발'이 걸어 두는 플래그.
        // CPlayer가 CRunSkillEffect를 통해 켜고 끈다. 그리드 규칙 자체(Step_To)는 그대로 두고
        // 이동 가능 여부(Can_Move)만 완화/확장하는 자리라 여기 둔다.
        private bool            m_bAllowOwnedInterior;      // 월보 — 점령지 내부도 통과
        private bool            m_bEdgeWrap;                // 어디로든 신발 — 좌우 끝을 잇는다

        public Vector2Int   CUR_CELL    => m_vCurCell;
        public MOVE_DIR     CUR_DIR     => m_eCurDir;
        public bool         IS_MOVING   => m_bMoving;
        public float        SPEED       { get { return m_fSpeed; } set { m_fSpeed = Mathf.Max(0f, value); } }

        public void Set_AllowOwnedInterior(bool bAllow) => m_bAllowOwnedInterior = bAllow;
        public void Set_EdgeWrap(bool bWrap) => m_bEdgeWrap = bWrap;

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
            m_ePendingDir = MOVE_DIR.NONE;
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
            ++m_iSpiralStep;        // 260921_나선형이 '몇 칸 갔는지' 세는 자리
            vArrivedCell = m_vCurCell;
            return true;
        }

        /// <summary> 260920_캐릭터가 쓸 이동 방식. 스테이지에 들어갈 때 한 번 정한다(2-22). </summary>
        public void Set_MoveStyle(MOVE_STYLE eStyle)
        {
            m_eMoveStyle  = eStyle;
            m_ePendingDir = MOVE_DIR.NONE;
            m_iSpiralStep = 0;
        }

        // 260921_나선형 — 몇 칸을 가면 스스로 한 번 꺾는가. 소용돌이가 너무 촘촘하면 제자리를 맴돌고,
        // 너무 성기면 그냥 네모를 그린다. 4칸이 화면에서 '나선'으로 읽히는 최소치였다.
        private const int SPIRAL_TURN_STEP = 4;

        /// <summary> 시계 방향으로 90도. 나선은 한쪽으로만 꺾어야 소용돌이가 된다. </summary>
        public static MOVE_DIR Turn_Clockwise(MOVE_DIR eDir)
        {
            switch (eDir)
            {
                case MOVE_DIR.UP:    return MOVE_DIR.RIGHT;
                case MOVE_DIR.RIGHT: return MOVE_DIR.DOWN;
                case MOVE_DIR.DOWN:  return MOVE_DIR.LEFT;
                case MOVE_DIR.LEFT:  return MOVE_DIR.UP;
                default:             return MOVE_DIR.NONE;
            }
        }

        /// <summary>
        /// 260921_나선형 캐릭터의 방향 결정(2-22). **멈추지 못한다** — 입력이 없으면 가던 대로 가되,
        /// SPIRAL_TURN_STEP칸마다 시계 방향으로 한 번 꺾어 소용돌이를 그린다.
        /// 입력이 들어오면 그 방향이 우선이고 칸 수는 다시 센다 — 입력은 '어디로'가 아니라 '언제 꺾을지'다.
        /// </summary>
        private MOVE_DIR Resolve_Spiral(MOVE_DIR eDesiredDir)
        {
            if (eDesiredDir != MOVE_DIR.NONE && eDesiredDir != m_eCurDir && Can_Move(eDesiredDir) == true)
            {
                m_iSpiralStep = 0;
                return eDesiredDir;
            }

            // 첫 걸음 — 갈 수 있는 쪽 아무 데나. 시작 섬 안쪽처럼 막힌 쪽을 고르면 그대로 멈춰 버린다.
            if (m_eCurDir == MOVE_DIR.NONE)
                return Find_SpiralDir(MOVE_DIR.UP);

            if (m_iSpiralStep < SPIRAL_TURN_STEP && Can_Move(m_eCurDir) == true)
                return m_eCurDir;

            m_iSpiralStep = 0;
            return Find_SpiralDir(Turn_Clockwise(m_eCurDir));
        }

        /// <summary>
        /// 시계 방향으로 돌려 가며 갈 수 있는 첫 방향을 찾는다.
        /// **나선형은 멈추지 못하는 것이 규칙**이라, 막혔다고 서 있으면 조작 자체가 성립하지 않는다.
        /// 넷 다 막혀 있으면(사방이 벽) 그대로 돌려주고 평소 규칙(Try_StartMove)이 처리한다.
        /// </summary>
        private MOVE_DIR Find_SpiralDir(MOVE_DIR eStart)
        {
            MOVE_DIR eDir = eStart;

            for (int i = 0; i < 4; ++i)
            {
                if (Can_Move(eDir) == true)
                    return eDir;

                eDir = Turn_Clockwise(eDir);
            }

            return eStart;
        }

        public MOVE_STYLE MOVE_STYLE_NOW => m_eMoveStyle;

        /// <summary>
        /// 260920_지금 칸을 그대로 갈아 끼운다(이동 중이던 것은 취소). 점령 직후 경계선으로 되돌릴 때 쓴다.
        /// 규칙 판정(Step_To)을 거치지 않으므로 **이미 안전하다고 확인된 칸에만** 쓸 것.
        /// </summary>
        public void Snap_To(Vector2Int vCell)
        {
            m_vCurCell    = vCell;
            m_vNextCell   = vCell;
            m_bMoving     = false;
            m_bFollowing  = false;
            m_fProgress   = 0f;
            m_ePendingDir = MOVE_DIR.NONE;
        }

        /// <summary>
        /// 대각선 입력을 두 칸으로 나눈다. 갈 수 있는 축을 먼저 밟고 나머지를 예약한다 —
        /// 한 축이 막혀 있으면 나머지 한 축으로만 간다(코너에 끼지 않는다).
        /// </summary>
        private MOVE_DIR Resolve_Diagonal(MOVE_DIR eDesiredDir)
        {
            if (m_eMoveStyle != MOVE_STYLE.EIGHT_WAY || CTerritoryGrid.Is_Diagonal(eDesiredDir) == false)
                return eDesiredDir;

            CTerritoryGrid.Dir_Split(eDesiredDir, out MOVE_DIR eHorizontal, out MOVE_DIR eVertical);

            // 번갈아 가며 먼저 밟을 축을 바꾸면 계단이 잘게 쪼개져 대각선처럼 보인다.
            bool bHorizontalFirst = m_eCurDir != eHorizontal;
            MOVE_DIR eFirst  = bHorizontalFirst ? eHorizontal : eVertical;
            MOVE_DIR eSecond = bHorizontalFirst ? eVertical   : eHorizontal;

            if (Can_Move(eFirst) == false)
            {
                m_ePendingDir = MOVE_DIR.NONE;
                return eSecond;
            }

            m_ePendingDir = eSecond;
            return eFirst;
        }

        private bool Try_StartMove(MOVE_DIR eDesiredDir)
        {
            // 예약된 두 번째 칸이 있으면 입력보다 그쪽이 먼저다 — 대각선 한 번을 끝까지 마친다.
            if (m_ePendingDir != MOVE_DIR.NONE)
            {
                MOVE_DIR ePending = m_ePendingDir;
                m_ePendingDir = MOVE_DIR.NONE;

                if (Can_Move(ePending) == true)
                    eDesiredDir = ePending;
            }
            else if (m_eMoveStyle == MOVE_STYLE.SPIRAL)
            {
                eDesiredDir = Resolve_Spiral(eDesiredDir);
            }
            else
            {
                eDesiredDir = Resolve_Diagonal(eDesiredDir);
            }

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
            m_vNextCell = Get_NextCell(eDir);
            m_bMoving   = true;
            return true;
        }

        // 260916_다음 셀 계산을 한 곳으로 모은다 — '어디로든 신발'의 좌우 랩어라운드가
        // Can_Move/Can_Follow/Try_StartMove 세 곳 중 한 곳만 반영되면 판정과 실제 이동이 어긋난다.
        private Vector2Int Get_NextCell(MOVE_DIR eDir)
        {
            Vector2Int vNext = m_vCurCell + CTerritoryGrid.Dir_ToOffset(eDir);

            if (m_bEdgeWrap == true && m_cGrid.WIDTH > 0)
                vNext.x = ((vNext.x % m_cGrid.WIDTH) + m_cGrid.WIDTH) % m_cGrid.WIDTH;

            return vNext;
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

            Vector2Int vNext = Get_NextCell(eDir);
            if (m_cGrid.Get_Cell(vNext) != CELL_STATE.OWNED)
                return false;

            return Can_Move(eDir);
        }

        private bool Can_Move(MOVE_DIR eDir)
        {
            if (eDir == MOVE_DIR.NONE)
                return false;

            // 260920_대각선으로 한 칸에 가는 일은 없다 — 가로 · 세로 두 칸으로 쪼개 밟는다(2-22).
            // 4방향 캐릭터에게 대각선 입력이 들어와도 여기서 막힌다.
            if (CTerritoryGrid.Is_Diagonal(eDir) == true)
                return false;

            Vector2Int vNext = Get_NextCell(eDir);
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
            // 단, 내부에 갇힌 경우에는 선으로 빠져나가야 하므로 허용한다. 260920_점령 직후에는
            // CPlayer가 경계선으로 되돌려 놓으므로(Snap_To) 이 예외는 사실상 안전망으로만 남는다 —
            // 예전에는 이 예외 덕에 내부에 선 동안 '월보를 공짜로 얻은 것처럼' 마음대로 돌아다닐 수 있었다.
            // (선을 그리는 중에는 현재 칸이 TRAIL이라 Is_Boundary가 false → 도형을 닫는 이동은 항상 통과)
            // 260916_런 스킬 '월보'를 들고 있으면 이 제한 자체를 끈다.
            if (m_bAllowOwnedInterior == false
                && m_cGrid.Get_Cell(vNext) == CELL_STATE.OWNED
                && m_cGrid.Is_Boundary(vNext) == false
                && m_cGrid.Is_Boundary(m_vCurCell) == true)
            {
                return false;
            }

            return true;
        }
    }
}
