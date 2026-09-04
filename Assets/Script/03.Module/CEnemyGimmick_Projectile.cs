using UnityEngine;

namespace Client
{
    // 260904_투사체 기믹 — 사거리 안의 플레이어에게 탄을 쏜다
    /// <summary>
    /// Cool=발사주기, Value=탄속(초당 셀), Range=사거리(셀), Duration=탄 수명(초).
    /// 쏘는 순간의 방향으로 직진할 뿐 유도하지 않는다 — 피할 수 있어야 하기 때문이다.
    /// </summary>
    public class CEnemyGimmick_Projectile : CEnemyGimmick
    {
        protected override bool Can_Fire(Vector2 vPlayerPos)
        {
            if (base.Can_Fire(vPlayerPos) == false)
                return false;

            return Vector2.Distance(m_cOwner.POS, vPlayerPos) <= m_fRange * m_cGrid.CELL_SIZE;
        }

        protected override void Fire(Vector2 vPlayerPos)
        {
            Vector2 vDir = vPlayerPos - m_cOwner.POS;
            if (vDir.sqrMagnitude <= Mathf.Epsilon)
                return;

            m_cHost.Spawn_Projectile(m_cOwner.POS, vDir.normalized, m_fValue, m_fRange, m_fDuration);
        }
    }
}
