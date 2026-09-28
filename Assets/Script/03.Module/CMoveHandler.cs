using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 셀 단위 이동 + 셀 사이 보간
    // 260928_로그라이트 전면 재작성 — 연속 다각형 경계 이동(260923)을 걷어내고 칸 중심 스냅 이동으로
    // 되돌린다(Docs/Design_Roguelite_Rewrite.md 2장).
    /// <summary>
    /// 플레이어는 항상 칸 격자 위를 움직인다. 칸과 칸 사이는 보간해서 부드럽게 보이지만,
    /// 게임 규칙(트레일 · 점령 · 사망) 판정은 칸에 '도착'하는 순간에만 일어난다(CTerritoryGrid.Step_To).
    ///
    /// **손을 떼거나 반대로 꺾어도 선을 긋는 중이면 멈추지 않는다**(원본 스펙 §1) — 마지막 방향을 그대로
    /// 잇는다. 예전(260921)에는 손을 떼면 그 자리에 섰지만, 이번 재작성은 "역주행 입력만 막고 나머지는
    /// 계속 나아간다"로 바뀌었다. 내 땅 위에서는 그대로 정지가 기본이다(260902_선분 자동 추적).
    ///
    /// 이번 재작성 범위에서는 4방향만 다룬다 — 8방향 · 나선형(2-22)은 보류다. 대각선 입력은 무시된다.
    /// </summary>
    public class CMoveHandler
    {
        private CTerritoryGrid  m_cGrid;

        private Vector2Int      m_vCurCell;
        private Vector2Int      m_vNextCell;
        private MOVE_DIR        m_eCurDir = MOVE_DIR.NONE;
        private Vector2         m_vHeading = Vector2.up;   // 선을 긋는 중 나아가는 방향(단위 벡터)
        private float           m_fProgress;               // 현재 칸 → 다음 칸 진행도 0~1
        private float           m_fSpeed;                  // 초당 이동 칸 수
        private bool            m_bMoving;
        private bool            m_bFollowing;              // 직전 이동이 선분 자동 추적이었는가
        private MOVE_STYLE      m_eMoveStyle = MOVE_STYLE.FOUR_WAY;

        // 260916_런 스킬 — '월보'/'어디로든 신발'이 걸어 두는 플래그. 규칙(Step_To)은 그대로 두고
        // 이동 가능 여부(Can_Move)만 완화/확장하는 자리라 여기 둔다.
        private bool            m_bAllowOwnedInterior;      // 월보 — 점령지 내부도 통과
        private bool            m_bEdgeWrap;                // 어디로든 신발 — 좌우 끝을 잇는다

        public Vector2Int   CUR_CELL    => m_vCurCell;
        public MOVE_DIR     CUR_DIR     => m_eCurDir;
        public Vector2      HEADING     => m_vHeading;
        public bool         IS_MOVING   => m_bMoving;
        public float        SPEED       { get { return m_fSpeed; } set { m_fSpeed = Mathf.Max(0f, value); } }
        public MOVE_STYLE   MOVE_STYLE_NOW => m_eMoveStyle;

        /// <summary> 칸 중심의 그리드 공간 좌표(연속 보간). Distance_ToBoundary 같은 옛 연속 API와 맞춘다. </summary>
        public Vector2 POS => m_bMoving == false
            ? CTerritoryGrid.Cell_ToGrid(m_vCurCell)
            : Vector2.Lerp(CTerritoryGrid.Cell_ToGrid(m_vCurCell), CTerritoryGrid.Cell_ToGrid(m_vNextCell), m_fProgress);

        public Vector3 WORLD_POS => m_cGrid != null ? m_cGrid.Grid_ToWorld(POS) : Vector3.zero;

        public void Set_AllowOwnedInterior(bool bAllow) => m_bAllowOwnedInterior = bAllow;
        public void Set_EdgeWrap(bool bWrap) => m_bEdgeWrap = bWrap;

        /// <param name="vStartGridPos"> 그리드 공간의 시작 자리(보통 시작 섬의 경계 위) </param>
        public bool Initialize(CTerritoryGrid cGrid, Vector2 vStartGridPos, float fSpeed)
        {
            if (cGrid == null)
            {
                Debug.LogError("[CMoveHandler] Grid가 null 입니다.");
                return false;
            }

            m_cGrid  = cGrid;
            m_fSpeed = Mathf.Max(0f, fSpeed);
            Teleport(vStartGridPos);
            return true;
        }

        /// <summary> 캐릭터가 쓸 이동 방식. 이번 재작성 범위에서는 값만 저장한다(8방향 · 나선형 보류, 2-22). </summary>
        public void Set_MoveStyle(MOVE_STYLE eStyle) => m_eMoveStyle = eStyle;

        /// <summary> 사망 후 부활 등, 이동 상태를 통째로 리셋하고 특정 칸으로 옮긴다. </summary>
        public void Teleport(Vector2Int vCell)
        {
            m_vCurCell   = vCell;
            m_vNextCell  = vCell;
            m_eCurDir    = MOVE_DIR.NONE;
            m_vHeading   = Vector2.up;
            m_fProgress  = 0f;
            m_bMoving    = false;
            m_bFollowing = false;
        }

        /// <param name="vGridPos"> 그리드 공간의 자리. 그 자리가 속한 칸으로 옮긴다 </param>
        public void Teleport(Vector2 vGridPos) => Teleport(m_cGrid != null ? m_cGrid.Grid_ToCell(vGridPos) : Vector2Int.zero);

        /// <summary> 점령 직후 · 부활 때 가장 가까운 경계 위로 옮긴다. 안전한 곳은 경계선뿐이다(2-3). </summary>
        public void Snap_ToBoundary()
        {
            if (m_cGrid == null)
                return;

            if (m_cGrid.Try_Find_NearestBoundary(POS, out Vector2 vNearest) == true)
                Snap_To(m_cGrid.Grid_ToCell(vNearest));
        }

        /// <summary>
        /// 지금 칸을 그대로 갈아 끼운다(이동 중이던 것은 취소). 규칙 판정(Step_To)을 거치지 않으므로
        /// **이미 안전하다고 확인된 칸에만** 쓸 것.
        /// </summary>
        public void Snap_To(Vector2Int vCell)
        {
            m_vCurCell   = vCell;
            m_vNextCell  = vCell;
            m_bMoving    = false;
            m_bFollowing = false;
            m_fProgress  = 0f;
        }

        /// <summary> 한 프레임 이동. 칸에 도착하는 순간 규칙(Step_To)을 넘겨 그 결과를 돌려준다. </summary>
        /// <param name="iCapturedCount"> CAPTURE일 때 새로 점령한 칸 수 </param>
        public STEP_RESULT Tick(float fDeltaTime, MOVE_DIR eDesiredDir, out int iCapturedCount)
        {
            iCapturedCount = 0;

            if (m_cGrid == null)
                return STEP_RESULT.SAFE;

            if (m_bMoving == false && Try_StartMove(eDesiredDir) == false)
                return Cur_State();

            m_fProgress += m_fSpeed * fDeltaTime;
            if (m_fProgress < 1f)
                return Cur_State();

            // 남은 진행도는 다음 칸으로 이월해 프레임레이트에 따라 속도가 달라지지 않게 한다.
            m_fProgress = Mathf.Min(m_fProgress - 1f, 0.999f);

            Vector2Int vFrom = m_vCurCell;
            Vector2Int vTo   = m_vNextCell;
            m_vCurCell = vTo;
            m_bMoving  = false;

            STEP_RESULT eResult = m_cGrid.Step_To(vFrom, vTo, out iCapturedCount);
            if (eResult != STEP_RESULT.DRAW)
                m_fProgress = 0f;

            return eResult;
        }

        private STEP_RESULT Cur_State() => m_cGrid.IS_DRAWING == true ? STEP_RESULT.DRAW : STEP_RESULT.SAFE;

        /// <summary> 점멸 · 쾌속돌진 — 그 방향으로 fDistance(칸, 반올림)만큼 칸 단위로 이어 간다. </summary>
        public STEP_RESULT Warp(MOVE_DIR eDir, float fDistance, out int iCapturedCount)
        {
            iCapturedCount = 0;

            if (m_cGrid == null || eDir == MOVE_DIR.NONE || CTerritoryGrid.Is_Diagonal(eDir) == true || fDistance <= 0f)
                return m_cGrid != null ? Cur_State() : STEP_RESULT.SAFE;

            int iSteps = Mathf.Max(1, Mathf.RoundToInt(fDistance));
            STEP_RESULT eLast = Cur_State();

            for (int i = 0; i < iSteps; ++i)
            {
                if (Can_Move(eDir) == false)
                    break;

                Vector2Int vFrom = m_vCurCell;
                Vector2Int vTo   = Get_NextCell(eDir);

                eLast = m_cGrid.Step_To(vFrom, vTo, out int iCap);
                iCapturedCount += iCap;

                m_vCurCell = vTo;
                m_eCurDir  = eDir;
                m_vHeading = CTerritoryGrid.Dir_ToVector(eDir);

                if (eLast != STEP_RESULT.DRAW)
                    break;
            }

            m_vNextCell = m_vCurCell;
            m_bMoving   = false;
            m_fProgress = 0f;
            return eLast;
        }

        /// <summary> 마감 — 선을 긋는 중이면 vTarget(점령지 경계, 그리드 공간)까지 ㄱ자로 이어 붙인다. </summary>
        public STEP_RESULT Draw_To(Vector2 vTarget, out int iCapturedCount)
        {
            iCapturedCount = 0;

            if (m_cGrid == null || m_cGrid.IS_DRAWING == false)
                return STEP_RESULT.SAFE;

            Vector2Int vTargetCell = m_cGrid.Grid_ToCell(vTarget);
            STEP_RESULT eLast = STEP_RESULT.DRAW;
            int iGuard = m_cGrid.WIDTH + m_cGrid.HEIGHT + 4;

            while (m_vCurCell != vTargetCell && iGuard-- > 0)
            {
                MOVE_DIR eDir = Vector_ToStepDir(vTargetCell - m_vCurCell);
                if (eDir == MOVE_DIR.NONE || Can_Move(eDir) == false)
                    break;

                Vector2Int vFrom = m_vCurCell;
                Vector2Int vTo   = Get_NextCell(eDir);

                eLast = m_cGrid.Step_To(vFrom, vTo, out int iCap);
                iCapturedCount += iCap;

                m_vCurCell = vTo;
                m_eCurDir  = eDir;
                m_vHeading = CTerritoryGrid.Dir_ToVector(eDir);

                if (eLast != STEP_RESULT.DRAW)
                    break;
            }

            m_vNextCell = m_vCurCell;
            m_bMoving   = false;
            m_fProgress = 0f;
            return eLast;
        }

        // ㄱ자로 잇기 — 더 많이 남은 축(가로 · 세로)부터 줄인다
        private static MOVE_DIR Vector_ToStepDir(Vector2Int vDelta)
        {
            if (vDelta == Vector2Int.zero)
                return MOVE_DIR.NONE;

            if (Mathf.Abs(vDelta.x) >= Mathf.Abs(vDelta.y))
                return vDelta.x > 0 ? MOVE_DIR.RIGHT : MOVE_DIR.LEFT;

            return vDelta.y > 0 ? MOVE_DIR.UP : MOVE_DIR.DOWN;
        }

        private bool Try_StartMove(MOVE_DIR eDesiredDir)
        {
            if (CTerritoryGrid.Is_Diagonal(eDesiredDir) == true)
                eDesiredDir = MOVE_DIR.NONE;    // 이번 재작성 범위에서는 8방향을 다루지 않는다(2-22 보류)

            MOVE_DIR eDir = eDesiredDir;
            bool bFollowing = false;

            if (m_cGrid.IS_DRAWING == true)
            {
                // 원본 스펙 §1 — 손을 떼거나(NONE) 역주행이면 마지막 방향을 그대로 잇는다.
                // 예전(260921)에는 손을 떼면 그 자리에 섰지만 이번엔 스펙을 따른다.
                if (eDir == MOVE_DIR.NONE || eDir == CTerritoryGrid.Dir_Reverse(m_eCurDir))
                    eDir = m_eCurDir;

                if (Can_Move(eDir) == false)
                {
                    m_fProgress = 0f;
                    return false;   // 맵 끝 · 잘린 칸 앞에서 선다
                }
            }
            else
            {
                if (Can_Move(eDir) == false)
                {
                    // 260902_선분 자동 추적 — 내 땅 위에서 가려던 방향이 막히면(점령지 내부 · 벽)
                    // 그 방향과 수직으로 이어지는 경계를 대신 찾는다.
                    eDir = Find_FollowDir(eDesiredDir);
                    bFollowing = eDir != MOVE_DIR.NONE;

                    if (Can_Move(eDir) == false)
                    {
                        m_fProgress = 0f;
                        return false;
                    }
                }
            }

            m_bFollowing = bFollowing;
            m_eCurDir    = eDir;
            m_vHeading   = CTerritoryGrid.Dir_ToVector(eDir);
            m_vNextCell  = Get_NextCell(eDir);
            m_bMoving    = true;
            return true;
        }

        // 260916_다음 칸 계산을 한 곳으로 모은다 — '어디로든 신발'의 좌우 랩어라운드가 여기 한 곳만
        // 반영되면 판정과 실제 이동이 어긋나지 않는다.
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

            // 이번 재작성 범위에서는 대각선 한 칸 이동을 다루지 않는다(2-22 보류).
            if (CTerritoryGrid.Is_Diagonal(eDir) == true)
                return false;

            Vector2Int vNext = Get_NextCell(eDir);
            if (m_cGrid.Is_InBounds(vNext.x, vNext.y) == false)
                return false;

            // 260904_맵 모양 마스크로 잘라낸 칸은 맵 밖과 똑같이 취급한다.
            if (m_cGrid.Is_Blocked(vNext) == true)
                return false;

            // 260902_점령지 '내부'는 통과할 수 없다 — 영토의 선(경계)만 따라 움직인다.
            // 단, 내부에 갇힌 경우에는 선으로 빠져나가야 하므로 허용한다(안전망 — 점령 직후에는
            // CPlayer가 Snap_ToBoundary로 되돌려 놓으므로 사실상 극단적인 맵에서만 쓰인다).
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
