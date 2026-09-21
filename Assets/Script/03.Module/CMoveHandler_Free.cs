using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260921_자유 각도 이동 — 대각선 · 아르키메데스 나선으로 점령한다 (2-22)
    /// <summary>
    /// 8방향 · 나선형 캐릭터는 **선을 긋는 동안만** 칸이 아니라 각도로 움직인다.
    /// 칸 단위로 움직이면 대각선은 계단, 나선은 네모가 되어 그 모양으로 점령할 수 없었다.
    ///
    /// 규칙은 그대로다 — 몸은 곧게/둥글게 가지만, 지나간 칸을 **4방향으로 이어지게** 골라 순서대로
    /// Step_To에 넘긴다(2-3). 칸이 대각선으로만 이어지면 점령 판정의 4방향 플러드필이 그 틈으로 새어 나간다.
    /// 그래서 점령한 모양은 대각선 · 원이 되고, 가장자리만 칸 크기 계단으로 남는다.
    ///
    /// 점령지 경계선 위(안전 지대)에서는 예전처럼 칸 단위로 움직인다 — 경계선이 곧 칸의 줄이라서다.
    /// 좌표는 '칸 공간'(칸 (x,y)의 중심 = (x,y))으로 든다.
    /// </summary>
    public partial class CMoveHandler
    {
        // 아르키메데스 나선 r = a + bθ. 시작 반지름과 한 바퀴에 벌어지는 폭(칸).
        // 폭이 2칸보다 좁으면 다음 고리가 앞 고리에 붙어 자기 선을 밟기 쉽다.
        private const float SPIRAL_START_RADIUS = 4f;
        private const float SPIRAL_GROW_PER_TURN = 4f;
        private const float SPIRAL_TURN_DONE    = Mathf.PI * 2f;    // 한 바퀴 돌면 닫으러 간다
        private const int   SPIRAL_HOME_SEARCH  = 40;               // 돌아갈 내 땅을 찾는 반경(칸)

        private bool            m_bFree;
        private Vector2         m_vFreePos;         // 칸 공간 좌표
        private Vector2         m_vHeading = Vector2.up;
        private Vector2Int      m_vLogicCell;       // 지나간 칸 중 마지막(아직 판정 전일 수 있다)
        private Vector2Int      m_vPrevLogicCell;   // 그 앞 칸 — 되돌아 밟으면 자기 선이라 막는다
        private float           m_fSpiralAngle;     // 선을 긋기 시작하고 돈 각도
        private bool            m_bSpiralClosing;
        private Vector2Int      m_vSpiralHome;
        private readonly Queue<Vector2Int> m_qArrive = new Queue<Vector2Int>();

        public bool IS_FREE => m_bFree;

        /// <summary> 한 프레임에 칸을 여럿 지나면 나머지를 여기서 차례로 꺼낸다(CPlayer가 같은 프레임에 판정한다). </summary>
        public bool Try_PopArrived(out Vector2Int vCell)
        {
            vCell = m_vCurCell;
            if (m_qArrive.Count <= 0)
                return false;

            vCell = m_qArrive.Dequeue();
            m_vCurCell = vCell;
            return true;
        }

        private bool Is_FreeStyle => m_eMoveStyle == MOVE_STYLE.EIGHT_WAY || m_eMoveStyle == MOVE_STYLE.SPIRAL;

        private void Reset_Free()
        {
            m_bFree          = false;
            m_bSpiralClosing = false;
            m_fSpiralAngle   = 0f;
            m_qArrive.Clear();
        }

        // 선을 긋기 시작했으면 자유 이동으로, 선이 끝났으면(점령 · 사망) 칸 이동으로 돌아간다.
        private void Refresh_FreeMode()
        {
            bool bWant = Is_FreeStyle == true && m_cGrid.IS_DRAWING == true;

            if (bWant == m_bFree)
                return;

            if (bWant == false)
            {
                Reset_Free();
                m_vNextCell = m_vCurCell;
                m_bMoving   = false;
                m_fProgress = 0f;
                return;
            }

            // 칸 한가운데에 도착한 순간에만 넘어간다 — 반쯤 간 칸을 버리면 선이 끊긴다
            if (m_bMoving == true)
                return;

            Vector2Int vOffset = CTerritoryGrid.Dir_ToOffset(m_eCurDir);
            m_bFree          = true;
            m_vFreePos       = m_vCurCell;
            m_vHeading       = vOffset != Vector2Int.zero ? ((Vector2)vOffset).normalized : Vector2.up;
            m_vLogicCell     = m_vCurCell;
            m_vPrevLogicCell = m_vCurCell - vOffset;    // 방금 떠나온 내 땅 칸
            m_vSpiralHome    = m_vPrevLogicCell;
            m_fSpiralAngle   = 0f;
            m_bSpiralClosing = false;
            m_ePendingDir    = MOVE_DIR.NONE;
            m_iDiagPhase     = 0;
        }

        private bool Tick_Free(float fDeltaTime, MOVE_DIR eDesiredDir, out Vector2Int vArrivedCell)
        {
            vArrivedCell = m_vCurCell;

            float fDistance = m_fSpeed * fDeltaTime;
            if (m_eMoveStyle == MOVE_STYLE.SPIRAL)
                Steer_Spiral(fDistance, eDesiredDir);
            else
                Steer_EightWay(eDesiredDir);

            Advance_Free(m_vFreePos + m_vHeading * fDistance);
            return Try_PopArrived(out vArrivedCell);
        }

        // 8방향 — 누른 방향으로 곧게 간다. 뒤로 꺾는 입력(135도 이상)은 무시한다 — 자기 선을 밟는 즉사를 막는다.
        private void Steer_EightWay(MOVE_DIR eDesiredDir)
        {
            if (eDesiredDir == MOVE_DIR.NONE)
                return;

            Vector2 vWant = ((Vector2)CTerritoryGrid.Dir_ToOffset(eDesiredDir)).normalized;
            if (Vector2.Dot(vWant, m_vHeading) > -0.5f)
                m_vHeading = vWant;
        }

        /// <summary>
        /// 나선형 — 시계 방향으로 돌며 반지름이 r = a + bθ로 커진다(아르키메데스 나선).
        /// 곡률 1/r로 방향을 틀어 가며 적분하므로 어느 방향으로 나가든 그 자리에서 나선이 시작된다.
        /// 한 바퀴를 돌면 가장 가까운 내 땅으로 방향을 틀어 도형을 닫는다 — 선이 스스로는 닫히지 않기 때문이다.
        /// 그 전에 내 땅에 닿으면 평소처럼 그 자리에서 점령된다.
        /// </summary>
        private void Steer_Spiral(float fDistance, MOVE_DIR eDesiredDir)
        {
            // 새로 누른 방향으로 곧게 틀고 나선을 처음부터 다시 그린다 — 입력은 '언제 꺾을지'다
            if (eDesiredDir == MOVE_DIR.NONE)
            {
                m_eSpiralInput = MOVE_DIR.NONE;
            }
            else if (eDesiredDir != m_eSpiralInput)
            {
                m_eSpiralInput = eDesiredDir;
                Vector2 vWant = ((Vector2)CTerritoryGrid.Dir_ToOffset(eDesiredDir)).normalized;
                if (Vector2.Dot(vWant, m_vHeading) > -0.5f)
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
                    if (m_cGrid.Try_Find_NearestCell(m_vLogicCell, CELL_STATE.OWNED, SPIRAL_HOME_SEARCH,
                                                     out Vector2Int vHome) == true)
                        m_vSpiralHome = vHome;
                }
                return;
            }

            // 닫으러 간다 — 처음 반지름과 같은 곡률까지만 틀어 급하게 꺾지 않는다
            Vector2 vToHome = (Vector2)m_vSpiralHome - m_vFreePos;
            if (vToHome.sqrMagnitude < 0.0001f)
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

        /// <summary>
        /// 몸을 목표 지점까지 옮기며 지나간 칸을 **4방향으로 이어지게** 쌓는다.
        /// 한 번에 두 축이 모두 바뀌면(모서리를 스치면) 더 많이 넘어간 축부터 밟는다.
        /// 맵 끝 · 잘린 칸 · 방금 떠나온 칸은 들어가지 않고 그 축으로는 멈춘다(벽을 따라 미끄러진다).
        /// </summary>
        private void Advance_Free(Vector2 vTarget)
        {
            Vector2Int vCell = m_vLogicCell;

            for (int iGuard = 0; iGuard < 8; ++iGuard)
            {
                Vector2Int vGoal = Vector2Int.RoundToInt(vTarget);
                if (vGoal == vCell)
                    break;

                int iDx = System.Math.Sign(vGoal.x - vCell.x);
                int iDy = System.Math.Sign(vGoal.y - vCell.y);

                bool bXFirst = iDx != 0 && (iDy == 0 || Mathf.Abs(vTarget.x - vCell.x) >= Mathf.Abs(vTarget.y - vCell.y));
                Vector2Int vStep = bXFirst ? new Vector2Int(iDx, 0) : new Vector2Int(0, iDy);

                if (Can_EnterFree(vCell, ref vStep, ref vTarget) == false)
                {
                    // 이 축은 막혔다 — 이 축으로는 칸 안에 붙잡아 두고 나머지 축만 간다
                    if (vStep.x != 0) vTarget.x = vCell.x;
                    else              vTarget.y = vCell.y;
                    continue;
                }

                m_vPrevLogicCell = vCell;
                vCell += vStep;
                m_qArrive.Enqueue(vCell);
                m_eCurDir = Offset_ToDir(vStep);     // 워프 · 마감이 이어 쓸 수 있게 4방향으로 남긴다
            }

            m_vLogicCell = vCell;
            m_vFreePos   = vTarget;
        }

        private bool Can_EnterFree(Vector2Int vFrom, ref Vector2Int vStep, ref Vector2 vTarget)
        {
            Vector2Int vNext = vFrom + vStep;

            // 어디로든 신발 — 좌우 끝을 넘으면 반대편으로 옮긴다(칸 이동의 Get_NextCell과 같은 규칙)
            if (m_bEdgeWrap == true && m_cGrid.WIDTH > 0 && (vNext.x < 0 || vNext.x >= m_cGrid.WIDTH))
            {
                int iShift = vNext.x < 0 ? m_cGrid.WIDTH : -m_cGrid.WIDTH;
                vNext.x   += iShift;
                vTarget.x += iShift;
                vStep      = vNext - vFrom;
            }

            if (m_cGrid.Is_InBounds(vNext.x, vNext.y) == false || m_cGrid.Is_Blocked(vNext) == true)
                return false;

            return vNext != m_vPrevLogicCell;
        }

        private static MOVE_DIR Offset_ToDir(Vector2Int vStep)
        {
            if (vStep.x > 0) return MOVE_DIR.RIGHT;
            if (vStep.x < 0) return MOVE_DIR.LEFT;
            return vStep.y > 0 ? MOVE_DIR.UP : MOVE_DIR.DOWN;
        }

        private Vector3 Get_FreeWorldPos()
        {
            Vector3 vOrigin = m_cGrid.Cell_ToWorld(0, 0);
            return vOrigin + (Vector3)(m_vFreePos * m_cGrid.CELL_SIZE);
        }
    }
}
