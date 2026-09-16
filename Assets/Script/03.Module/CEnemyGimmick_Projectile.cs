using UnityEngine;

namespace Client
{
    // 260904_투사체 기믹 — 사거리 안의 플레이어에게 탄을 쏜다
    // 260917_탄 종류는 ProjectileInfo.csv(RefID), 몇 발을 어떻게 뿌릴지는 발사 패턴 열이 정한다 (GYM CBulletFactory)
    /// <summary>
    /// Cool=발사주기, Range=쏘기 시작하는 거리(셀), RefID=ProjectileInfo ID.
    /// 탄속 · 수명 · 사거리는 이제 탄 표에 있다 — Value · Duration은 쓰지 않는다.
    /// 패턴(연발 · 회전 링)은 플레이어 무기와 같은 CProjectileFirer가 굴린다.
    /// </summary>
    public class CEnemyGimmick_Projectile : CEnemyGimmick
    {
        private readonly CProjectileFirer m_cFirer = new CProjectileFirer();
        private Vector2 m_vAim;

        protected override bool Can_Fire(Vector2 vPlayerPos)
        {
            if (base.Can_Fire(vPlayerPos) == false)
                return false;

            // 연발이 아직 남았으면 새로 쏘지 않는다.
            if (m_cFirer.IS_BURSTING == true)
                return false;

            return Vector2.Distance(m_cOwner.POS, vPlayerPos) <= m_fRange * m_cGrid.CELL_SIZE;
        }

        protected override void Fire(Vector2 vPlayerPos)
        {
            // 패턴 값은 Initialize 뒤에 정해지므로 처음 쏠 때 한 번 맞춘다(풀 재사용 때도 다시 들어온다).
            m_cFirer.Setup(m_eFirePattern, m_iFireCount, m_fFireAngle, m_fFireInterval, Spawn_Shot);

            m_vAim = vPlayerPos - m_cOwner.POS;
            m_cFirer.Fire(m_vAim);
        }

        protected override void Tick_Pending(float fDeltaTime, Vector2 vPlayerPos)
        {
            m_vAim = vPlayerPos - m_cOwner.POS;
            m_cFirer.Tick(fDeltaTime, m_vAim);
        }

        private void Spawn_Shot(Vector2 vDir) => m_cHost.Spawn_EnemyShot(m_iRefID, m_cOwner.POS, vDir, m_cOwner);
    }
}
