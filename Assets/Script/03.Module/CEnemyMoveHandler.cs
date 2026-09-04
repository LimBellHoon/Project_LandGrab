using UnityEngine;

namespace Client
{
    // 260902_몬스터 이동: 미점령 지대만 돌아다니며 점령지에 튕긴다
    /// <summary>
    /// 플레이어(CMoveHandler)와 달리 셀에 고정되지 않고 연속 좌표로 움직인다.
    /// 점령지(OWNED)에 부딪히면 튕기고, 미점령(EMPTY)과 선분(TRAIL) 위는 그대로 지나간다.
    /// — 선분을 통과시키는 이유: 선분에 닿는 것 자체가 플레이어 사망 판정이라 막으면 안 된다.
    /// </summary>
    public class CEnemyMoveHandler
    {
        private const int ESCAPE_SEARCH_RADIUS = 16;    // 점령지에 갇혔을 때 탈출구를 찾는 최대 반경(셀)

        private CTerritoryGrid  m_cGrid;
        private Vector2         m_vPos;         // 월드 좌표
        private Vector2         m_vDir;         // 진행 방향 (정규화)
        private float           m_fSpeed;       // 월드 유닛/초

        public Vector2      POS     => m_vPos;
        public Vector2      DIR     => m_vDir;
        public Vector2Int   CELL    => m_cGrid.World_ToCell(m_vPos);
        public float        SPEED   { get { return m_fSpeed; } set { m_fSpeed = Mathf.Max(0f, value); } }

        public bool Initialize(CTerritoryGrid cGrid, Vector2 vStartPos, Vector2 vStartDir, float fSpeed)
        {
            if (cGrid == null)
            {
                Debug.LogError("[CEnemyMoveHandler] Grid가 null 입니다.");
                return false;
            }

            m_cGrid  = cGrid;
            m_vPos   = vStartPos;
            m_vDir   = vStartDir.sqrMagnitude > 0f ? vStartDir.normalized : Vector2.up;
            m_fSpeed = Mathf.Max(0f, fSpeed);
            return true;
        }

        /// <param name="bChase"> true면 vTargetPos 쪽으로 선회한다 </param>
        public void Tick(float fDeltaTime, bool bChase, Vector2 vTargetPos, float fTurnRate)
        {
            if (m_cGrid == null)
                return;

            // 점령 판정과 겹치는 드문 타이밍에 점령지 안에 갇힐 수 있다 — 가장 가까운 미점령 칸으로 복귀시킨다.
            if (Escape_IfTrapped() == true)
                return;

            if (bChase == true)
                Steer_Toward(vTargetPos, fTurnRate, fDeltaTime);

            Move_WithBounce(fDeltaTime);
        }

        #region private
        private void Steer_Toward(Vector2 vTargetPos, float fTurnRate, float fDeltaTime)
        {
            Vector2 vToTarget = vTargetPos - m_vPos;
            if (vToTarget.sqrMagnitude <= Mathf.Epsilon)
                return;

            // 즉시 꺾지 않고 초당 fTurnRate 라디안만큼만 돌려서 관성이 느껴지게 한다.
            float fMaxRadian = fTurnRate * fDeltaTime;
            m_vDir = Vector3.RotateTowards(m_vDir, vToTarget.normalized, fMaxRadian, 0f);
            m_vDir = m_vDir.normalized;
        }

        private void Move_WithBounce(float fDeltaTime)
        {
            Vector2 vNext = m_vPos + m_vDir * m_fSpeed * fDeltaTime;

            // X축과 Y축을 따로 검사해야 모서리에서 한쪽 축만 뒤집혀 자연스럽게 미끄러진다.
            // (한 번에 검사하면 벽을 스칠 때마다 정반대로 튕겨 부자연스럽다)
            if (Is_Blocked(new Vector2(vNext.x, m_vPos.y)) == true)
            {
                m_vDir.x = -m_vDir.x;
                vNext.x  = m_vPos.x;
            }

            if (Is_Blocked(new Vector2(m_vPos.x, vNext.y)) == true)
            {
                m_vDir.y = -m_vDir.y;
                vNext.y  = m_vPos.y;
            }

            m_vPos = vNext;
        }

        // 점령지와 맵 밖(BLOCK)이 벽. 그리드 바깥은 World_ToCell이 테두리로 clamp하고
        // 테두리는 점령지라 자동으로 막힌다.
        // 260904_모양 마스크로 잘라낸 칸도 여기서 함께 막지 않으면 몬스터가 맵 밖으로 샌다.
        private bool Is_Blocked(Vector2 vWorld)
        {
            CELL_STATE eState = m_cGrid.Get_Cell(m_cGrid.World_ToCell(vWorld));
            return eState == CELL_STATE.OWNED || eState == CELL_STATE.BLOCK;
        }

        private bool Escape_IfTrapped()
        {
            if (m_cGrid.Get_Cell(CELL) != CELL_STATE.OWNED)
                return false;

            if (m_cGrid.Try_Find_NearestCell(CELL, CELL_STATE.EMPTY, ESCAPE_SEARCH_RADIUS, out Vector2Int vEscape) == false)
                return false;

            m_vPos = m_cGrid.Cell_ToWorld(vEscape);
            return true;
        }
        #endregion private
    }
}
