using UnityEngine;

namespace Client
{
    // 260904_거미줄 기믹 — 지나간 자리에 덫을 깐다
    /// <summary>
    /// Cool=설치주기, Value=플레이어 속도배율, Duration=거미줄 지속(초).
    /// 다른 기믹과 달리 플레이어가 안전 지대에 있어도 계속 깐다 —
    /// 나오기 전에 미리 깔려 있어야 '길을 막는' 위협이 되기 때문이다.
    /// </summary>
    public class CEnemyGimmick_Web : CEnemyGimmick
    {
        protected override bool Can_Fire(Vector2 vPlayerPos) => true;

        protected override void Fire(Vector2 vPlayerPos)
        {
            // 몬스터는 미점령 지대만 돌아다니므로 거미줄도 그쪽에만 깔린다.
            // 점령된 칸에 깔아 봐야 플레이어가 안전한 곳이라 의미가 없다.
            Vector2Int vCell = m_cOwner.CUR_CELL;
            if (m_cGrid.Get_Cell(vCell) != CELL_STATE.EMPTY)
                return;

            m_cHost.Spawn_Web(vCell, m_fDuration, m_fValue);
        }
    }
}
