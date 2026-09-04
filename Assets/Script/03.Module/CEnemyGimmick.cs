using UnityEngine;

namespace Client
{
    // 260904_몬스터 기믹 — 쿨타임을 재고 조건이 맞으면 발동한다
    /// <summary>
    /// CEnemy에 조합으로 붙는 모듈이다 (CMoveHandler와 같은 자리).
    /// 상속으로 CEnemy를 늘리지 않는 이유는 기믹이 바뀌어도 프리팹은 하나로 충분하기 때문 —
    /// EnemyInfo.csv의 eGimmick 한 칸만 바꾸면 몬스터 종류가 늘어난다.
    ///
    /// 수치의 의미는 기믹마다 다르다. EnemyInfo.csv 머리의 주석이 기준이다.
    /// </summary>
    public abstract class CEnemyGimmick
    {
        private float m_fTimer;

        protected CEnemy            m_cOwner;
        protected CTerritoryGrid    m_cGrid;
        protected IGimmickHost      m_cHost;

        protected float m_fCool;
        protected float m_fValue;
        protected float m_fRange;       // 셀
        protected float m_fDuration;
        protected int   m_iRefID;

        /// <summary> eGimmick에 맞는 모듈을 만든다. NONE이면 null (기믹 없는 몬스터). </summary>
        public static CEnemyGimmick Create(ENEMY_GIMMICK eGimmick)
        {
            switch (eGimmick)
            {
                case ENEMY_GIMMICK.WEB:         return new CEnemyGimmick_Web();
                case ENEMY_GIMMICK.PROJECTILE:  return new CEnemyGimmick_Projectile();
                case ENEMY_GIMMICK.SPAWN:       return new CEnemyGimmick_Spawn();
                default:                        return null;
            }
        }

        public bool Initialize(CEnemy cOwner, CTerritoryGrid cGrid, CEnemyDesc cDesc)
        {
            if (cOwner == null || cGrid == null || cDesc == null)
            {
                Debug.LogError("[CEnemyGimmick] 소유자 / Grid / Desc가 null 입니다.");
                return false;
            }

            m_cOwner    = cOwner;
            m_cGrid     = cGrid;
            m_fCool     = Mathf.Max(0.1f, cDesc.fGimmickCool);  // 0이면 매 프레임 터진다
            m_fValue    = cDesc.fGimmickValue;
            m_fRange    = cDesc.fGimmickRange;
            m_fDuration = cDesc.fGimmickDuration;
            m_iRefID    = cDesc.iGimmickRefID;

            // 스폰하자마자 터지면 플레이어가 반응할 수 없다 — 한 주기 기다린다.
            m_fTimer = m_fCool;
            return true;
        }

        /// <summary> 소환 창구. 스테이지가 몬스터를 만든 뒤 꽂아 준다. </summary>
        public void Set_Host(IGimmickHost cHost) => m_cHost = cHost;

        public void Tick(float fDeltaTime, Vector2 vPlayerPos)
        {
            if (m_cHost == null)
                return;

            if (m_fTimer > 0f)
                m_fTimer -= fDeltaTime;

            if (m_fTimer > 0f)
                return;

            // 쿨은 찼는데 조건이 아직이면 타이머를 그대로 둔다 — 조건이 맞는 순간 바로 나간다.
            if (Can_Fire(vPlayerPos) == false)
                return;

            m_fTimer = m_fCool;
            Fire(vPlayerPos);
        }

        /// <summary> 기본은 플레이어가 땅을 먹으러 나와 있을 때만 발동한다. </summary>
        protected virtual bool Can_Fire(Vector2 vPlayerPos) => m_cHost.IS_PLAYER_EXPOSED;

        protected abstract void Fire(Vector2 vPlayerPos);
    }
}
