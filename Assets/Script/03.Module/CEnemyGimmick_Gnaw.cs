using UnityEngine;

namespace Client
{
    // 260921_땅 갉는 자 — 내 땅 가장자리를 도로 빈 땅으로 되돌린다
    /// <summary>
    /// Cool=갉는 주기(초), Value=한 번에 갉는 칸 수, Range=닿는 거리(칸).
    /// **먹은 땅도 영원하지 않다**는 압박을 만든다 — 점령률이 도로 떨어지므로 웨이브 목표가 멀어진다.
    ///
    /// 플레이어를 쫓지 않고 가장 가까운 내 땅 가장자리로 간다(Try_Get_MoveTarget). 몸에 닿으면 죽는 것은
    /// 다른 몬스터와 같다. 대처법은 **가둬서 죽이는 것**이다 — 내 땅에 붙어 있으니 점령으로 가두기 쉽다(2-3).
    /// 거미줄처럼 플레이어가 안전 지대에 있어도 계속 갉는다 — 쉬는 동안 땅이 줄어야 압박이 된다.
    /// </summary>
    public class CEnemyGimmick_Gnaw : CEnemyGimmick
    {
        private const float RETARGET_TIME = 0.5f;   // 가장 가까운 내 땅을 다시 찾는 간격 — 매 프레임 찾으면 판이 클수록 무겁다
        private const int   SEARCH_RADIUS = 80;     // 칸

        private float   m_fRetargetTimer;
        private bool    m_bHasTarget;
        private Vector2 m_vTarget;

        protected override bool Can_Fire(Vector2 vPlayerPos) => true;

        protected override void Fire(Vector2 vPlayerPos)
            => m_cHost.Gnaw_Territory(m_cOwner.CUR_CELL, m_fRange, Mathf.Max(1, Mathf.RoundToInt(m_fValue)));

        protected override void Tick_Pending(float fDeltaTime, Vector2 vPlayerPos)
        {
            m_fRetargetTimer -= fDeltaTime;
            if (m_fRetargetTimer > 0f)
                return;

            m_fRetargetTimer = RETARGET_TIME;
            m_bHasTarget = m_cGrid.Try_Find_NearestCell(m_cOwner.CUR_CELL, CELL_STATE.OWNED, SEARCH_RADIUS,
                                                        out Vector2Int vCell);
            if (m_bHasTarget == true)
                m_vTarget = m_cGrid.Cell_ToWorld(vCell);
        }

        public override bool Try_Get_MoveTarget(out Vector2 vTarget)
        {
            vTarget = m_vTarget;
            return m_bHasTarget;
        }
    }
}
