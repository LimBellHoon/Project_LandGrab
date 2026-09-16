using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260904_투사체 기믹 — 사거리 안의 플레이어에게 탄을 쏜다
    // 260917_탄 종류는 ProjectileInfo.csv(RefID), 몇 발을 어떻게 뿌릴지는 발사 패턴 열이 정한다 (GYM CBulletFactory)
    /// <summary>
    /// Cool=발사주기, Range=쏘기 시작하는 거리(셀), RefID=ProjectileInfo ID.
    /// 탄속 · 수명 · 사거리는 이제 탄 표에 있다 — Value · Duration은 쓰지 않는다.
    /// </summary>
    public class CEnemyGimmick_Projectile : CEnemyGimmick
    {
        private readonly List<Vector2> m_lstDir = new List<Vector2>();

        private float m_fSpinOffset;        // SPIN이 지금까지 돌아간 각도
        private int   m_iBurstRemain;       // BURST의 남은 발
        private float m_fBurstTimer;

        protected override bool Can_Fire(Vector2 vPlayerPos)
        {
            if (base.Can_Fire(vPlayerPos) == false)
                return false;

            // 연발이 아직 남았으면 새로 쏘지 않는다.
            if (m_iBurstRemain > 0)
                return false;

            return Vector2.Distance(m_cOwner.POS, vPlayerPos) <= m_fRange * m_cGrid.CELL_SIZE;
        }

        protected override void Fire(Vector2 vPlayerPos)
        {
            Vector2 vAim = vPlayerPos - m_cOwner.POS;

            if (m_eFirePattern == FIRE_PATTERN.BURST)
            {
                m_iBurstRemain = m_iFireCount;
                m_fBurstTimer  = 0f;
                Tick_Pending(0f, vPlayerPos);
                return;
            }

            CProjectileFire_Utility.Get_Directions(m_eFirePattern, vAim, m_iFireCount, m_fFireAngle,
                                                   m_fSpinOffset, m_lstDir);
            for (int i = 0; i < m_lstDir.Count; ++i)
                m_cHost.Spawn_EnemyShot(m_iRefID, m_cOwner.POS, m_lstDir[i], m_cOwner);

            if (m_eFirePattern == FIRE_PATTERN.SPIN)
                m_fSpinOffset = Mathf.Repeat(m_fSpinOffset + m_fFireAngle, 360f);
        }

        // 연발은 쏘는 순간마다 다시 조준한다 — 한 방향에 몰아 쏘면 한 번 비키는 것으로 다 피해진다.
        protected override void Tick_Pending(float fDeltaTime, Vector2 vPlayerPos)
        {
            if (m_iBurstRemain <= 0)
                return;

            m_fBurstTimer -= fDeltaTime;
            if (m_fBurstTimer > 0f)
                return;

            m_fBurstTimer = m_fFireInterval;
            --m_iBurstRemain;
            m_cHost.Spawn_EnemyShot(m_iRefID, m_cOwner.POS, vPlayerPos - m_cOwner.POS, m_cOwner);
        }
    }
}
