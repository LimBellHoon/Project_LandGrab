using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 셀 단위 이동 + 셀 사이 보간
    /// <summary>
    /// 플레이어는 항상 셀 격자 위를 움직인다. 셀과 셀 사이는 보간해서 부드럽게 보이지만,
    /// 게임 규칙(트레일/점령/사망) 판정은 셀에 '도착'하는 순간에만 일어난다.
    /// </summary>
    public partial class CMoveHandler
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
        // 260921_나선형 입력은 '새로 누른 순간'만 본다(CMoveHandler_Free). 누르고 있는 동안 계속 이기면
        // 나선이 매 프레임 처음부터 다시 시작돼 곧게 가 버린다
        private MOVE_DIR        m_eSpiralInput = MOVE_DIR.NONE;
        // 260921_대각선 두 칸을 몸은 비스듬히 한 번에 가는 것처럼 그린다. 0 = 대각선 아님, 1 = 첫 칸, 2 = 둘째 칸
        private int             m_iDiagPhase;
        private Vector2Int      m_vDiagFrom;

        // 260916_런 스킬(뱀서라이크) — '월보'/'어디로든 신발'이 걸어 두는 플래그.
        // CPlayer가 CRunSkillEffect를 통해 켜고 끈다. 그리드 규칙 자체(Step_To)는 그대로 두고
        // 이동 가능 여부(Can_Move)만 완화/확장하는 자리라 여기 둔다.
        private bool            m_bAllowOwnedInterior;      // 월보 — 점령지 내부도 통과
        private bool            m_bEdgeWrap;                // 어디로든 신발 — 좌우 끝을 잇는다

        public Vector2Int   CUR_CELL    => m_vCurCell;
        public MOVE_DIR     CUR_DIR     => m_eCurDir;
        public bool         IS_MOVING   => m_bMoving || m_bFree;
        public float        SPEED       { get { return m_fSpeed; } set { m_fSpeed = Mathf.Max(0f, value); } }

        public void Set_AllowOwnedInterior(bool bAllow) => m_bAllowOwnedInterior = bAllow;
        public void Set_EdgeWrap(bool bWrap) => m_bEdgeWrap = bWrap;

        public Vector3 WORLD_POS
        {
            get
            {
                // 260921_선을 긋는 동안의 자유 각도 이동(CMoveHandler_Free)
                if (m_bFree == true)
                    return Get_FreeWorldPos();

                // 260921_대각선은 가로 · 세로 두 칸을 밟지만(규칙) 몸은 비스듬히 곧게 미끄러진다(보이는 것).
                // 칸을 그대로 따라 그리면 계단이 좌우로 꺾이는 지그재그로 보여 대각선으로 읽히지 않았다.
                if (m_iDiagPhase != 0 && Is_DiagonalSpan() == true)
                {
                    Vector3 vDiagFrom = m_cGrid.Cell_ToWorld(m_vDiagFrom);
                    Vector3 vDiagTo   = m_cGrid.Cell_ToWorld(Get_DiagTarget());
                    float   fHalf     = m_iDiagPhase == 1 ? 0f : 0.5f;
                    float   fStep     = m_bMoving == true ? m_fProgress * 0.5f
                                      : (m_iDiagPhase == 1 && m_ePendingDir != MOVE_DIR.NONE ? 0.5f : -1f);
                    if (fStep >= 0f)
                        return Vector3.Lerp(vDiagFrom, vDiagTo, fHalf + fStep);
                }

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
            m_iDiagPhase  = 0;
            m_eSpiralInput = MOVE_DIR.NONE;
            Reset_Free();
        }

        /// <summary>
        /// 이동을 진행한다.
        /// </summary>
        /// <returns> 이번 프레임에 새 셀에 도착했으면 true (규칙 판정 시점) </returns>
        public bool Tick(float fDeltaTime, MOVE_DIR eDesiredDir, out Vector2Int vArrivedCell)
        {
            vArrivedCell = m_vCurCell;

            // 260921_8방향 · 나선형은 선을 긋는 동안 칸이 아니라 각도로 움직인다(CMoveHandler_Free)
            Refresh_FreeMode();
            if (m_bFree == true)
                return Tick_Free(fDeltaTime, eDesiredDir, out vArrivedCell);

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

        /// <summary> 260920_캐릭터가 쓸 이동 방식. 스테이지에 들어갈 때 한 번 정한다(2-22). </summary>
        public void Set_MoveStyle(MOVE_STYLE eStyle)
        {
            m_eMoveStyle  = eStyle;
            m_ePendingDir = MOVE_DIR.NONE;
            m_iDiagPhase  = 0;
            m_eSpiralInput = MOVE_DIR.NONE;
            Reset_Free();
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
            m_iDiagPhase  = 0;
            Reset_Free();
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
            m_vDiagFrom   = m_vCurCell;     // 260921_몸이 비스듬히 미끄러질 출발점
            m_iDiagPhase  = 1;
            return eFirst;
        }

        // 좌우 랩어라운드(어디로든 신발)로 반대편에 넘어간 경우는 비스듬히 그리지 않는다 — 화면을 가로질러 날아간다
        private bool Is_DiagonalSpan()
        {
            Vector2Int vDelta = Get_DiagTarget() - m_vDiagFrom;
            return Mathf.Abs(vDelta.x) == 1 && Mathf.Abs(vDelta.y) == 1;
        }

        // 두 칸을 다 밟았을 때 설 자리 = 출발점 + 첫 칸 + 둘째 칸
        private Vector2Int Get_DiagTarget()
            => m_vNextCell + (m_iDiagPhase == 1 ? CTerritoryGrid.Dir_ToOffset(m_ePendingDir) : Vector2Int.zero);

        private bool Try_StartMove(MOVE_DIR eDesiredDir)
        {
            // 예약된 두 번째 칸이 있으면 입력보다 그쪽이 먼저다 — 대각선 한 번을 끝까지 마친다.
            if (m_ePendingDir != MOVE_DIR.NONE)
            {
                MOVE_DIR ePending = m_ePendingDir;
                m_ePendingDir = MOVE_DIR.NONE;

                if (Can_Move(ePending) == true)
                {
                    eDesiredDir  = ePending;
                    m_iDiagPhase = 2;
                }
                else
                {
                    // 둘째 칸이 막혔다 — 대각선을 접고 이번 입력을 새로 해석한다(그대로 두면 날것의 대각선이 들어가 멈춘다)
                    m_iDiagPhase = 0;
                    eDesiredDir  = Resolve_Diagonal(eDesiredDir);
                }
            }
            else
            {
                m_iDiagPhase = 0;
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
                    m_fProgress   = 0f;
                    m_iDiagPhase  = 0;
                    m_ePendingDir = MOVE_DIR.NONE;
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
