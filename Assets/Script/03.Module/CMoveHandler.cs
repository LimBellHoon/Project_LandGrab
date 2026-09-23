using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 셀 단위 이동 + 셀 사이 보간
    // 260923_땅이 다각형이 되면서 **칸 없이 연속 좌표로** 움직인다 (2-3, 2-22)
    /// <summary>
    /// 위치는 그리드 공간(칸 하나 = 1)의 점이다. 두 가지 상태가 있다.
    ///   · 내 땅 위(안전) — **경계선을 따라 미끄러진다.** 누른 방향과 가장 잘 맞는 쪽으로 변을 타고 가고,
    ///     꼭짓점에서 다음 변이 누른 방향과 너무 어긋나면 거기서 선다(예전 칸 시절의 '선 자동 추적'과 같은 감각).
    ///     누른 방향이 빈 땅 쪽이면 그 자리에서 선을 긋기 시작한다.
    ///   · 선을 긋는 중 — 캐릭터 방식대로 곧게(4방향 · 8방향) 또는 나선으로 나아가고, 한 걸음마다
    ///     CTerritoryGrid.Step_To가 판정한다(자기 선 · 점령 · 계속). 규칙은 거기에만 있다(2-3).
    /// 손을 떼면 어느 상태든 그 자리에 선다(260921).
    /// </summary>
    public class CMoveHandler
    {
        private const float EXIT_PROBE     = 0.12f;    // 나갈지 볼 때 누른 방향으로 이만큼 앞의 점을 본다(칸)
        private const float EXIT_MARGIN    = 0.03f;    // 그 점이 경계에서 이만큼은 떨어진 바깥이어야 '나간다'
        private const float SLIDE_MIN_DOT  = 0.25f;    // 변을 타려면 누른 방향이 변 방향과 이만큼은 맞아야 한다(약 75도)
        // 260923_선을 긋는 중 '되돌아가는' 입력만 막는다. 예전 -0.5(120도)는 8방향의 135도 꺾기까지 막아
        // 셀레스티안이 나갔다가 비스듬히 돌아오지 못했다 — 135도는 자기 선에서 옆으로 벌어지므로 밟지 않는다.
        private const float REVERSE_DOT    = -0.9f;    // 거의 정반대(154도 이상)만 무시하고 선다
        private const float VERTEX_SNAP    = 0.1f;     // 꼭짓점에서 이만큼 안이면 꼭짓점에 선 것으로 본다(칸) — 모서리 코앞에서 꺾을 때 걸리지 않게
        private const int   MAX_SLIDE_STEP = 64;       // 한 프레임에 넘길 꼭짓점 수 상한(곡선은 꼭짓점이 촘촘하다)
        private const int   CLAMP_ITERATION = 12;

        // 아르키메데스 나선 r = a + bθ(2-22). 시작 반지름과 한 바퀴에 벌어지는 폭(칸).
        // 폭이 2칸보다 좁으면 다음 고리가 앞 고리에 붙어 자기 선을 밟기 쉽다.
        private const float SPIRAL_START_RADIUS  = 4f;
        private const float SPIRAL_GROW_PER_TURN = 4f;
        private const float SPIRAL_TURN_DONE     = Mathf.PI * 2f;    // 한 바퀴 돌면 닫으러 간다

        private CTerritoryGrid  m_cGrid;
        private Vector2         m_vPos;             // 그리드 공간
        private float           m_fSpeed;           // 초당 칸
        private MOVE_DIR        m_eCurDir = MOVE_DIR.NONE;  // 마지막으로 움직인 방향을 상하좌우로 — 잔상 · 점멸이 읽는다
        private Vector2         m_vHeading = Vector2.up;    // 선을 긋는 중 나아가는 방향
        private bool            m_bMoving;
        private MOVE_STYLE      m_eMoveStyle = MOVE_STYLE.FOUR_WAY;

        // 260923_경계를 따라 돌던 방향(+1 / -1). 누른 방향이 변과 직각이라 탈 수 없을 때 이 방향으로 계속 간다
        private int             m_iFollowSign;

        // 경계 위 자리 — 몇 번째 고리의 몇 번째 변, 변 위 어디(0~1). 점령지가 바뀌면 다시 잡는다
        private int             m_iRing = -1;
        private int             m_iSeg;
        private float           m_fT;
        private int             m_iRingVersion = -1;

        // 260921_나선형
        private float           m_fSpiralAngle;
        private bool            m_bSpiralClosing;
        private Vector2         m_vSpiralHome;
        // 새로 누른 방향만 본다 — 누르고 있는 동안 계속 이기면 나선이 매 프레임 처음부터 다시 시작돼 곧게 가 버린다
        private MOVE_DIR        m_eSpiralInput = MOVE_DIR.NONE;

        // 260916_런 스킬 — '월보'/'어디로든 신발'이 걸어 두는 플래그. 규칙(Step_To)은 그대로 두고 갈 수 있는 곳만 넓힌다
        private bool            m_bAllowOwnedInterior;      // 월보 — 점령지 내부도 통과
        private bool            m_bEdgeWrap;                // 어디로든 신발 — 좌우 끝을 잇는다

        public Vector2      POS         => m_vPos;
        public Vector3      WORLD_POS   => m_cGrid != null ? m_cGrid.Grid_ToWorld(m_vPos) : Vector3.zero;
        public Vector2Int   CUR_CELL    => m_cGrid != null ? m_cGrid.Grid_ToCell(m_vPos) : Vector2Int.zero;
        public MOVE_DIR     CUR_DIR     => m_eCurDir;
        public Vector2      HEADING     => m_vHeading;
        public bool         IS_MOVING   => m_bMoving;
        public float        SPEED       { get { return m_fSpeed; } set { m_fSpeed = Mathf.Max(0f, value); } }
        public MOVE_STYLE   MOVE_STYLE_NOW => m_eMoveStyle;

        public void Set_AllowOwnedInterior(bool bAllow) => m_bAllowOwnedInterior = bAllow;
        public void Set_EdgeWrap(bool bWrap) => m_bEdgeWrap = bWrap;

        /// <param name="vStartPos"> 그리드 공간의 시작 자리(보통 시작 섬의 경계 위) </param>
        public bool Initialize(CTerritoryGrid cGrid, Vector2 vStartPos, float fSpeed)
        {
            if (cGrid == null)
            {
                Debug.LogError("[CMoveHandler] Grid가 null 입니다.");
                return false;
            }

            m_cGrid  = cGrid;
            m_fSpeed = Mathf.Max(0f, fSpeed);
            m_eCurDir = MOVE_DIR.NONE;
            Teleport(vStartPos);
            return true;
        }

        /// <summary> 260920_캐릭터가 쓸 이동 방식. 스테이지에 들어갈 때 한 번 정한다(2-22). </summary>
        public void Set_MoveStyle(MOVE_STYLE eStyle)
        {
            m_eMoveStyle = eStyle;
            Reset_Spiral();
        }

        /// <summary> 사망 후 부활 등, 이동 상태를 통째로 리셋하고 그 자리로 옮긴다. </summary>
        public void Teleport(Vector2 vPos)
        {
            m_vPos         = vPos;
            m_bMoving      = false;
            m_iRing        = -1;
            m_iRingVersion = -1;
            m_iFollowSign  = 0;
            Reset_Spiral();
        }

        /// <summary> 260920_점령 직후 · 부활 때 가장 가까운 경계 위로 옮긴다. 안전한 곳은 경계선뿐이다(2-3). </summary>
        public void Snap_ToBoundary()
        {
            m_iRingVersion = -1;
            Ensure_OnRing();
        }

        /// <summary> 한 프레임 이동. 입력이 없으면 선다. 선을 긋는 중 판정이 나오면 그 결과를 돌려준다. </summary>
        /// <param name="iCapturedCount"> CAPTURE일 때 새로 점령한 넓이(칸) </param>
        public STEP_RESULT Tick(float fDeltaTime, MOVE_DIR eDesiredDir, out int iCapturedCount)
        {
            m_bMoving = false;
            return Move(eDesiredDir, m_fSpeed * fDeltaTime, false, out iCapturedCount);
        }

        /// <summary> 260923_점멸 — 그 방향으로 fDistance(칸)만큼 한 번에 간다. 판정은 평소 이동과 같다 </summary>
        public STEP_RESULT Warp(MOVE_DIR eDir, float fDistance, out int iCapturedCount)
            => Move(eDir, fDistance, true, out iCapturedCount);

        /// <summary> 260923_마감 — 선을 긋는 중이면 vTarget(점령지 경계)까지 곧게 이어 붙인다 </summary>
        public STEP_RESULT Draw_To(Vector2 vTarget, out int iCapturedCount)
        {
            iCapturedCount = 0;
            if (m_cGrid.IS_DRAWING == false)
                return STEP_RESULT.SAFE;

            Vector2 vDir = vTarget - m_vPos;
            if (vDir.sqrMagnitude < 1e-10f)
                return STEP_RESULT.DRAW;

            m_vHeading = vDir.normalized;
            return Step(vTarget + m_vHeading * 0.02f, out iCapturedCount);   // 경계를 살짝 넘겨야 '들어갔다'가 된다
        }

        private STEP_RESULT State => m_cGrid.IS_DRAWING == true ? STEP_RESULT.DRAW : STEP_RESULT.SAFE;

        private STEP_RESULT Move(MOVE_DIR eDir, float fDistance, bool bStraight, out int iCapturedCount)
        {
            iCapturedCount = 0;

            if (m_cGrid == null || eDir == MOVE_DIR.NONE || fDistance <= 0f)
                return m_cGrid != null ? State : STEP_RESULT.SAFE;

            // 4방향 · 나선형은 대각선 입력을 받지 않는다(조이스틱이 이미 4방향으로 자르지만 한 번 더 막는다)
            if (m_eMoveStyle != MOVE_STYLE.EIGHT_WAY && CTerritoryGrid.Is_Diagonal(eDir) == true)
                return State;

            if (m_cGrid.IS_DRAWING == true)
                return Move_Drawing(eDir, fDistance, bStraight, out iCapturedCount);

            return Move_Safe(eDir, fDistance, bStraight, out iCapturedCount);
        }

        #region 내 땅 위 — 경계를 따라 미끄러진다
        private STEP_RESULT Move_Safe(MOVE_DIR eDir, float fDistance, bool bStraight, out int iCapturedCount)
        {
            iCapturedCount = 0;
            Vector2 vInput = CTerritoryGrid.Dir_ToVector(eDir);

            // 260916_월보 — 점령지 안쪽으로 누르면 경계를 떠나 안을 가로지른다
            if (m_bAllowOwnedInterior == true && (Is_Interior(m_vPos) == true || Is_Interior(m_vPos + vInput * EXIT_PROBE) == true))
                return Move_Interior(eDir, fDistance, bStraight, out iCapturedCount);

            if (Ensure_OnRing() == false)
                return STEP_RESULT.SAFE;

            float fLeft = fDistance;
            int   iSign = 0;

            for (int iStep = 0; iStep < MAX_SLIDE_STEP; ++iStep)
            {
                // 누른 쪽이 빈 땅이면 그 자리에서 선을 긋기 시작한다 — 꼭짓점에 닿을 때마다도 본다(모서리를 돌아 나가기)
                if (Can_Exit(vInput) == true)
                {
                    Start_Drawing(eDir);
                    return Move_Drawing(eDir, fLeft, bStraight, out iCapturedCount);
                }

                if (fLeft <= 1e-6f)
                    break;

                Vector2[] arrRing = m_cGrid.RINGS[m_iRing];
                int iCount = arrRing.Length;
                Vector2 a = arrRing[m_iSeg];
                Vector2 b = arrRing[(m_iSeg + 1) % iCount];
                Vector2 vSeg = b - a;
                float fLen = vSeg.magnitude;

                if (fLen < 1e-6f)
                {
                    m_iSeg = (m_iSeg + (iSign >= 0 ? 1 : iCount - 1)) % iCount;
                    m_fT   = iSign >= 0 ? 0f : 1f;
                    continue;
                }

                Vector2 vSegDir = vSeg / fLen;
                float fDot = Vector2.Dot(vInput, vSegDir);

                // 꼭짓점 위에서 막 출발할 때는 붙어 있는 두 변 중 누른 방향에 더 맞는 쪽을 탄다
                if (iSign == 0 && Mathf.Abs(fDot) < SLIDE_MIN_DOT && Try_SwitchAtVertex(vInput, iCount) == true)
                    continue;

                int iWant = fDot >= SLIDE_MIN_DOT ? 1 : fDot <= -SLIDE_MIN_DOT ? -1 : 0;

                // 260923_누른 방향이 변과 직각이라 탈 수 없으면 **돌던 방향으로 계속 간다**(260902_선분 자동 추적).
                // 예전 칸 시절처럼 "아래를 누르고 있으면 좌우 경계도 선을 따라 알아서 간다"가 이 한 줄이다.
                // 따라가다 그 방향으로 나갈 수 있게 되면 위 Can_Exit에서 저절로 나간다.
                if (iWant == 0 && iSign == 0 && m_iFollowSign != 0 && Can_Exit(vInput) == false)
                    iWant = m_iFollowSign;

                // 누른 방향이 이 변과 너무 어긋나거나, 꼭짓점을 넘은 뒤 되돌아가야 한다면 선다
                if (iWant == 0 || (iSign != 0 && iWant != iSign))
                    break;

                iSign = iWant;
                float fAvail = iSign > 0 ? (1f - m_fT) * fLen : m_fT * fLen;
                float fMove  = Mathf.Min(fLeft, fAvail);

                m_fT   = Mathf.Clamp01(m_fT + iSign * fMove / fLen);
                fLeft -= fMove;
                m_vPos = Vector2.Lerp(a, b, m_fT);

                if (fMove > 0f)
                {
                    m_bMoving     = true;
                    m_eCurDir     = CTerritoryGrid.Vector_ToDir4(vSegDir * iSign);
                    m_iFollowSign = iSign;      // 다음에 직각 입력이 들어와도 이 방향으로 이어 간다
                }

                // 변 끝에 닿았으면 다음 변으로 넘어간다
                if (iSign > 0 && m_fT >= 1f - 1e-5f)
                {
                    m_iSeg = (m_iSeg + 1) % iCount;
                    m_fT   = 0f;
                }
                else if (iSign < 0 && m_fT <= 1e-5f)
                {
                    m_iSeg = (m_iSeg - 1 + iCount) % iCount;
                    m_fT   = 1f;
                }
                else
                {
                    break;      // 변 중간에서 거리를 다 썼다
                }
            }

            return STEP_RESULT.SAFE;
        }

        // 지금 꼭짓점 위(VERTEX_SNAP 안)에 있으면 옆 변으로 갈아탄다(그 변이 누른 방향과 맞을 때만)
        private bool Try_SwitchAtVertex(Vector2 vInput, int iCount)
        {
            Vector2[] arrRing = m_cGrid.RINGS[m_iRing];
            float fLen = (arrRing[(m_iSeg + 1) % iCount] - arrRing[m_iSeg]).magnitude;

            int   iOther;
            float fOtherT;
            if (m_fT * fLen <= VERTEX_SNAP)
            {
                iOther  = (m_iSeg - 1 + iCount) % iCount;
                fOtherT = 1f;
            }
            else if ((1f - m_fT) * fLen <= VERTEX_SNAP)
            {
                iOther  = (m_iSeg + 1) % iCount;
                fOtherT = 0f;
            }
            else
            {
                return false;
            }

            Vector2 vOther = arrRing[(iOther + 1) % iCount] - arrRing[iOther];
            if (vOther.sqrMagnitude < 1e-12f || Mathf.Abs(Vector2.Dot(vInput, vOther.normalized)) < SLIDE_MIN_DOT)
                return false;

            m_iSeg = iOther;
            m_fT   = fOtherT;
            m_vPos = Vector2.Lerp(arrRing[iOther], arrRing[(iOther + 1) % iCount], fOtherT);
            return true;
        }

        // 누른 방향으로 조금 앞이 '확실히' 빈 땅인가 — 변을 따라 누른 것은 경계 위라 나가지 않는다
        private bool Can_Exit(Vector2 vInput)
        {
            Vector2 vProbe = m_vPos + vInput * EXIT_PROBE;
            return m_cGrid.Is_PlayablePoint(vProbe) == true
                && m_cGrid.Is_OwnedPoint(vProbe) == false
                && m_cGrid.Distance_ToBoundary(vProbe) > EXIT_MARGIN;
        }

        private bool Is_Interior(Vector2 vPoint)
            => m_cGrid.Is_OwnedPoint(vPoint) == true && m_cGrid.Distance_ToBoundary(vPoint) > EXIT_MARGIN;

        // 경계 위 자리를 다시 잡는다(점령지가 바뀌었거나 순간 이동했으면). 점령지가 없으면 false
        private bool Ensure_OnRing()
        {
            if (m_iRing >= 0 && m_iRingVersion == m_cGrid.OWNED_VERSION && m_iRing < m_cGrid.RINGS.Count)
                return true;

            if (m_cGrid.Try_Find_BoundaryLocation(m_vPos, out Vector2 vNearest, out int iRing, out int iSeg, out float fT) == false)
            {
                m_iRing = -1;
                return false;
            }

            m_vPos         = vNearest;
            m_iRing        = iRing;
            m_iSeg         = iSeg;
            m_fT           = fT;
            m_iRingVersion = m_cGrid.OWNED_VERSION;
            return true;
        }

        private void Start_Drawing(MOVE_DIR eDir)
        {
            m_cGrid.Begin_Trail(m_vPos);
            m_vHeading      = CTerritoryGrid.Dir_ToVector(eDir);
            m_iFollowSign   = 0;
            m_iRing         = -1;
            m_fSpiralAngle  = 0f;
            m_bSpiralClosing = false;
            m_vSpiralHome   = m_vPos;
            m_eSpiralInput  = eDir;     // 나올 때 누른 방향 — 그대로 누르고 있어도 나선이 다시 시작되지 않는다
        }

        // 260916_월보 — 점령지 안을 곧게 가로지른다. 안쪽에서 빈 땅으로 나가면 그 경계에서 선을 긋기 시작한다
        private STEP_RESULT Move_Interior(MOVE_DIR eDir, float fDistance, bool bStraight, out int iCapturedCount)
        {
            iCapturedCount = 0;
            Vector2 vInput = CTerritoryGrid.Dir_ToVector(eDir);
            Vector2 vTo    = Clamp_ToPlayable(m_vPos, m_vPos + vInput * fDistance);

            m_iRing = -1;
            if ((vTo - m_vPos).sqrMagnitude < 1e-12f)
                return STEP_RESULT.SAFE;

            m_bMoving = true;
            m_eCurDir = CTerritoryGrid.Vector_ToDir4(vInput);

            if (m_cGrid.Is_OwnedPoint(vTo) == true)
            {
                m_vPos = vTo;
                return STEP_RESULT.SAFE;
            }

            // 점령지 끝을 찾아 거기까지 가고, 남은 거리로 선을 긋는다
            float fLo = 0f, fHi = 1f;
            Vector2 vFrom = m_vPos;
            for (int i = 0; i < CLAMP_ITERATION; ++i)
            {
                float fMid = (fLo + fHi) * 0.5f;
                if (m_cGrid.Is_OwnedPoint(Vector2.Lerp(vFrom, vTo, fMid)) == true)
                    fLo = fMid;
                else
                    fHi = fMid;
            }

            m_vPos = Vector2.Lerp(vFrom, vTo, fLo);
            Start_Drawing(eDir);
            return Move_Drawing(eDir, Vector2.Distance(vFrom, vTo) * (1f - fLo), bStraight, out iCapturedCount);
        }
        #endregion 내 땅 위

        #region 선을 긋는 중
        private STEP_RESULT Move_Drawing(MOVE_DIR eDir, float fDistance, bool bStraight, out int iCapturedCount)
        {
            iCapturedCount = 0;
            Vector2 vInput = CTerritoryGrid.Dir_ToVector(eDir);

            if (bStraight == false && m_eMoveStyle == MOVE_STYLE.SPIRAL)
            {
                Steer_Spiral(fDistance, eDir);
            }
            else
            {
                // 뒤로 꺾는 입력은 무시하고 그 자리에 선다(260921_막힌 방향도 멈춘다)
                if (Vector2.Dot(vInput, m_vHeading) < REVERSE_DOT)
                    return STEP_RESULT.DRAW;

                m_vHeading = vInput;
            }

            Vector2 vTo = m_vPos + m_vHeading * fDistance;

            // 260916_어디로든 신발 — 맵 끝을 넘으면 거기서 선을 끊고 반대편에서 이어 긋는다
            // 260923_좌우만 이었는데 위아래도 잇는다. 두 축을 같이 넘으면 먼저 닿는 쪽부터
            if (m_bEdgeWrap == true)
            {
                int iAxis = Find_WrapAxis(vTo, out float fToEdge, out float fEdge);
                if (iAxis >= 0)
                {
                    Vector2 vEdge = m_vPos + m_vHeading * fToEdge;

                    STEP_RESULT eResult = Step(vEdge, out iCapturedCount);
                    if (eResult != STEP_RESULT.DRAW || Vector2.Distance(m_vPos, vEdge) > 1e-3f)
                        return eResult;

                    float fSize = iAxis == 0 ? m_cGrid.WIDTH : m_cGrid.HEIGHT;
                    float fOpposite = fEdge <= 0f ? fSize - 1e-3f : 1e-3f;

                    m_vPos = iAxis == 0 ? new Vector2(fOpposite, m_vPos.y) : new Vector2(m_vPos.x, fOpposite);
                    m_cGrid.Break_Trail(m_vPos);
                    vTo = m_vPos + m_vHeading * Mathf.Max(0f, fDistance - fToEdge);
                }
            }

            // 맵 끝 · 잘린 칸 앞에서는 선다. 비스듬히 부딪혔으면 벽을 따라 한 축만 간다
            Vector2 vClamped = Clamp_ToPlayable(m_vPos, vTo);
            if ((vClamped - m_vPos).sqrMagnitude < (vTo - m_vPos).sqrMagnitude * 0.25f)
            {
                Vector2 vSlide = Try_WallSlide(vTo - m_vPos);
                if (vSlide != Vector2.zero)
                    vClamped = vSlide;
            }

            if ((vClamped - m_vPos).sqrMagnitude < 1e-12f)
                return STEP_RESULT.DRAW;

            return Step(vClamped, out iCapturedCount);
        }

        // 260923_이번 걸음에 맵 끝을 넘는 축을 찾는다(먼저 닿는 쪽). 0=가로 1=세로, 없으면 -1
        private int Find_WrapAxis(Vector2 vTo, out float fToEdge, out float fEdge)
        {
            fToEdge = float.MaxValue;
            fEdge   = 0f;
            int iAxis = -1;

            for (int i = 0; i < 2; ++i)
            {
                float fHead = i == 0 ? m_vHeading.x : m_vHeading.y;
                float fNow  = i == 0 ? m_vPos.x : m_vPos.y;
                float fNext = i == 0 ? vTo.x : vTo.y;
                float fSize = i == 0 ? m_cGrid.WIDTH : m_cGrid.HEIGHT;

                if (Mathf.Abs(fHead) < 1e-5f || (fNext >= 0f && fNext < fSize))
                    continue;

                float fLine = fNext < 0f ? 0f : fSize - 1e-3f;
                float fDist = (fLine - fNow) / fHead;
                if (fDist >= fToEdge)
                    continue;

                fToEdge = fDist;
                fEdge   = fLine;
                iAxis   = i;
            }

            return iAxis;
        }

        // 한 걸음을 규칙에 넘긴다 — 판정은 전부 CTerritoryGrid.Step_To에 있다(2-3)
        private STEP_RESULT Step(Vector2 vTo, out int iCapturedCount)
        {
            Vector2 vMove = vTo - m_vPos;
            STEP_RESULT eResult = m_cGrid.Step_To(m_vPos, vTo, out Vector2 vEnd, out iCapturedCount);

            if ((vEnd - m_vPos).sqrMagnitude > 1e-12f)
            {
                m_bMoving = true;
                m_eCurDir = CTerritoryGrid.Vector_ToDir4(vMove);
            }

            m_vPos = vEnd;

            if (eResult != STEP_RESULT.DRAW)
            {
                m_iRing = -1;
                Reset_Spiral();
            }

            return eResult;
        }

        // 맵 밖 · 잘린 칸으로 나가는 걸음은 그 앞까지만 간다
        private Vector2 Clamp_ToPlayable(Vector2 vFrom, Vector2 vTo)
        {
            if (m_cGrid.Is_PlayablePoint(vTo) == true)
                return vTo;

            float fLo = 0f, fHi = 1f;
            for (int i = 0; i < CLAMP_ITERATION; ++i)
            {
                float fMid = (fLo + fHi) * 0.5f;
                if (m_cGrid.Is_PlayablePoint(Vector2.Lerp(vFrom, vTo, fMid)) == true)
                    fLo = fMid;
                else
                    fHi = fMid;
            }

            return Vector2.Lerp(vFrom, vTo, fLo);
        }

        // 벽에 비스듬히 부딪혔을 때 가로 · 세로 중 갈 수 있는 한 축으로 미끄러진다
        private Vector2 Try_WallSlide(Vector2 vMove)
        {
            Vector2 vX = m_vPos + new Vector2(vMove.x, 0f);
            Vector2 vY = m_vPos + new Vector2(0f, vMove.y);

            if (Mathf.Abs(vMove.x) > 1e-6f && m_cGrid.Is_PlayablePoint(vX) == true)
                return vX;

            if (Mathf.Abs(vMove.y) > 1e-6f && m_cGrid.Is_PlayablePoint(vY) == true)
                return vY;

            return Vector2.zero;
        }

        /// <summary>
        /// 나선형 — 시계 방향으로 돌며 반지름이 r = a + bθ로 커진다(아르키메데스 나선).
        /// 곡률 1/r로 방향을 틀어 가며 적분하므로 어느 방향으로 나가든 그 자리에서 나선이 시작된다.
        /// 한 바퀴를 돌면 가장 가까운 내 땅으로 방향을 틀어 도형을 닫는다 — 선이 스스로는 닫히지 않기 때문이다.
        /// </summary>
        private void Steer_Spiral(float fDistance, MOVE_DIR eDesiredDir)
        {
            // 다른 방향을 누르면 그쪽으로 곧게 틀고 나선을 처음부터 다시 그린다 — 입력은 방향만 바꾼다.
            // 같은 방향을 다시 누르는 것(손을 뗐다 다시 누름)은 방향을 바꾸지 않는다 — 멈췄던 자리에서 이어서 돈다
            if (eDesiredDir != m_eSpiralInput)
            {
                m_eSpiralInput = eDesiredDir;
                Vector2 vWant = CTerritoryGrid.Dir_ToVector(eDesiredDir);
                if (Vector2.Dot(vWant, m_vHeading) > REVERSE_DOT)
                {
                    m_vHeading       = vWant;
                    m_fSpiralAngle   = 0f;
                    m_bSpiralClosing = false;
                    return;
                }
            }

            if (m_bSpiralClosing == false)
            {
                float fRadius = Get_SpiralRadius(m_fSpiralAngle);
                float fTurn   = fDistance / Mathf.Max(0.5f, fRadius);
                m_fSpiralAngle += fTurn;
                m_vHeading = Rotate(m_vHeading, -fTurn);

                if (m_fSpiralAngle >= SPIRAL_TURN_DONE)
                {
                    m_bSpiralClosing = true;
                    if (m_cGrid.Try_Find_NearestBoundary(m_vPos, out Vector2 vHome) == true)
                        m_vSpiralHome = vHome;
                }
                return;
            }

            // 닫으러 간다 — 처음 반지름과 같은 곡률까지만 틀어 급하게 꺾지 않는다
            Vector2 vToHome = m_vSpiralHome - m_vPos;
            if (vToHome.sqrMagnitude < 1e-8f)
                return;

            float fMaxTurn = fDistance / SPIRAL_START_RADIUS;
            float fAngle   = Vector2.SignedAngle(m_vHeading, vToHome) * Mathf.Deg2Rad;
            m_vHeading = Rotate(m_vHeading, Mathf.Clamp(fAngle, -fMaxTurn, fMaxTurn));
        }

        /// <summary> 나선이 θ만큼 돌았을 때의 반지름(칸). 한 바퀴에 SPIRAL_GROW_PER_TURN만큼 벌어진다. </summary>
        public static float Get_SpiralRadius(float fAngle)
            => SPIRAL_START_RADIUS + SPIRAL_GROW_PER_TURN * Mathf.Max(0f, fAngle) / SPIRAL_TURN_DONE;

        private static Vector2 Rotate(Vector2 v, float fRad)
        {
            float c = Mathf.Cos(fRad);
            float s = Mathf.Sin(fRad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c).normalized;
        }

        private void Reset_Spiral()
        {
            m_fSpiralAngle   = 0f;
            m_bSpiralClosing = false;
            m_eSpiralInput   = MOVE_DIR.NONE;
        }
        #endregion 선을 긋는 중
    }
}
