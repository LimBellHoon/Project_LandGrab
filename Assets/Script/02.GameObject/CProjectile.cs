using System;

using UnityEngine;

using Engine;

namespace Client
{
    // 260904_몬스터 기믹: 투사체
    /// <summary>
    /// 미점령 지대를 직선으로 날아간다. 점령지(=플레이어의 안전 지대)에 닿으면 소멸하므로
    /// 선 위에 있는 플레이어는 절대 맞지 않는다.
    /// bLeaveWeb이 true면 소멸 위치를 OnExpire로 알려 거미줄을 깔게 한다.
    /// </summary>
    public class CProjectile : CGameObject
    {
        [SerializeField] private SpriteRenderer m_srBody;

        private CTerritoryGrid  m_cGrid;
        private Vector2         m_vPos;
        private Vector2         m_vDir;
        private float           m_fSpeed;       // 월드 유닛/초
        private float           m_fLifeTime;
        private bool            m_bLeaveWeb;

        public Vector2      POS         => m_vPos;
        public Vector2Int   CUR_CELL    => m_cGrid.World_ToCell(m_vPos);
        public bool         LEAVE_WEB   => m_bLeaveWeb;

        /// <summary> 소멸 시 호출. 거미줄 생성은 스테이지 매니저가 담당한다. </summary>
        public event Action<CProjectile> OnExpire;

        #region Engine.CGameObject
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CProjectileDesc cDesc) == false)
            {
                Debug.LogError("[CProjectile] CProjectileDesc가 아닙니다.");
                return false;
            }

            m_cGrid     = cDesc.cGrid;
            m_vPos      = cDesc.vStartPos;
            m_vDir      = cDesc.vDir.sqrMagnitude > 0f ? cDesc.vDir.normalized : Vector2.up;
            m_fSpeed    = cDesc.fSpeed * m_cGrid.CELL_SIZE;     // Desc는 '초당 셀' 단위
            m_fLifeTime = cDesc.fLifeTime;
            m_bLeaveWeb = cDesc.bLeaveWeb;

            transform.position   = m_vPos;
            transform.localScale = Vector3.one * m_cGrid.CELL_SIZE * (m_bLeaveWeb == true ? 1.2f : 0.9f);

            if (m_srBody != null)
                m_srBody.color = m_bLeaveWeb == true ? new Color(0.8f, 1f, 0.5f) : new Color(1f, 0.55f, 0.2f);

            return true;
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cGrid == null)
                return;

            m_fLifeTime -= fDeltaTime;
            m_vPos += m_vDir * m_fSpeed * fDeltaTime;
            transform.position = m_vPos;

            // 점령지에 닿으면 소멸한다. World_ToCell이 맵 밖을 테두리로 clamp하고
            // 테두리는 점령지라, 맵을 벗어나는 경우도 여기서 함께 걸린다.
            if (m_fLifeTime <= 0f || m_cGrid.Get_Cell(CUR_CELL) == CELL_STATE.OWNED)
                Expire();
        }

        public override void Hide()
        {
            OnExpire = null;
            m_cGrid  = null;
            base.Hide();
        }
        #endregion Engine.CGameObject

        private void Expire()
        {
            OnExpire?.Invoke(this);
            bCollect = true;    // 다음 Tick에 Engine이 풀로 반납한다
        }
    }
}
