using UnityEngine;

using Engine;

namespace Client
{
    // 260904_투사체 — 포수 몬스터가 쏘는 탄
    /// <summary>
    /// 쏜 방향으로 직진만 한다. 점령지·맵 밖에 닿거나, 사거리를 다 쓰거나, 수명이 다하면 사라진다.
    /// 플레이어와의 충돌 판정은 여기서 하지 않는다 — 몬스터와 마찬가지로 CStage_Manager가 한곳에서 본다.
    /// </summary>
    public class CProjectile : CGameObject
    {
        [SerializeField] private SpriteRenderer m_srBody;

        private CTerritoryGrid  m_cGrid;
        private Vector2         m_vPos;
        private Vector2         m_vDir;
        private float           m_fSpeed;           // 월드 유닛/초
        private float           m_fLifeTime;
        private float           m_fMaxDistance;     // 월드 유닛
        private float           m_fTravelled;
        private float           m_fHitRange;        // 셀
        private bool            m_bExpired;

        public Vector2  POS         => m_vPos;
        /// <summary> 플레이어와의 충돌 반경(셀). </summary>
        public float    HIT_RANGE   => m_fHitRange;
        public bool     IS_EXPIRED  => m_bExpired;

        #region Engine.CGameObject
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CProjectileDesc cDesc) == false)
            {
                Debug.LogError("[CProjectile] CProjectileDesc가 아닙니다.");
                return false;
            }

            m_cGrid = cDesc.cGrid;
            if (m_cGrid == null)
            {
                Debug.LogError("[CProjectile] Grid가 null 입니다.");
                return false;
            }

            m_vPos          = cDesc.vStartPos;
            m_vDir          = cDesc.vDir.sqrMagnitude > 0f ? cDesc.vDir.normalized : Vector2.up;
            m_fSpeed        = cDesc.fSpeed * m_cGrid.CELL_SIZE;
            m_fLifeTime     = cDesc.fLifeTime;
            m_fMaxDistance  = cDesc.fMaxRange > 0f ? cDesc.fMaxRange * m_cGrid.CELL_SIZE : 0f;
            m_fHitRange     = cDesc.fHitRange;
            m_fTravelled    = 0f;
            m_bExpired      = false;

            transform.position   = m_vPos;
            transform.localScale = Vector3.one * m_cGrid.CELL_SIZE * 0.9f;
            return true;
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cGrid == null || m_bExpired == true)
                return;

            m_fLifeTime -= fDeltaTime;
            if (m_fLifeTime <= 0f)
            {
                m_bExpired = true;
                return;
            }

            float fStep = m_fSpeed * fDeltaTime;
            Vector2 vNext = m_vPos + m_vDir * fStep;

            // 점령지와 맵 밖이 벽이다. 그리드 바깥은 World_ToCell이 테두리로 clamp하는데
            // 테두리는 점령지라 자동으로 걸린다.
            CELL_STATE eState = m_cGrid.Get_Cell(m_cGrid.World_ToCell(vNext));
            if (eState == CELL_STATE.OWNED || eState == CELL_STATE.BLOCK)
            {
                m_bExpired = true;
                return;
            }

            m_vPos = vNext;
            m_fTravelled += fStep;

            if (m_fMaxDistance > 0f && m_fTravelled >= m_fMaxDistance)
            {
                m_bExpired = true;
                return;
            }

            transform.position = m_vPos;
        }

        public override void Hide()
        {
            m_cGrid = null;
            base.Hide();
        }
        #endregion Engine.CGameObject

        /// <summary> 플레이어에게 맞았을 때처럼 밖에서 끝내야 할 때 부른다. </summary>
        public void Expire() => m_bExpired = true;
    }
}
