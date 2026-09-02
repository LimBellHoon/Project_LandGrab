using UnityEngine;

using Engine;

namespace Client
{
    // 260902_몬스터
    /// <summary>
    /// 미점령 지대만 돌아다닌다. 플레이어가 안전 지대에 있는 동안에는 쫓지 않고 배회하다가,
    /// 플레이어가 땅을 먹으러 나오는 순간부터 추적한다.
    /// 기믹(거미줄 / 투사체 / 미니 부하)은 Projectile과 함께 이 클래스를 상속해 붙일 예정.
    /// </summary>
    public class CEnemy : CGameObject
    {
        private static readonly Color COLOR_WANDER = new Color(1f, 0.35f, 0.35f);
        private static readonly Color COLOR_CHASE  = new Color(1f, 0.85f, 0.25f);

        [SerializeField] private SpriteRenderer m_srBody;

        private readonly CEnemyMoveHandler m_cMoveHandler = new CEnemyMoveHandler();

        private CTerritoryGrid  m_cGrid;
        private float           m_fSpeed;           // 월드 유닛/초
        private float           m_fChaseSpeed;      // 월드 유닛/초
        private float           m_fTurnRate;

        private bool            m_bChase;
        private Vector2         m_vTargetPos;

        public Vector2Int   CUR_CELL    => m_cMoveHandler.CELL;
        public Vector2      POS         => m_cMoveHandler.POS;
        public bool         IS_CHASING  => m_bChase;

        #region Engine.CGameObject
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CEnemyDesc cDesc) == false)
            {
                Debug.LogError("[CEnemy] CEnemyDesc가 아닙니다.");
                return false;
            }

            m_cGrid = cDesc.cGrid;

            // Desc의 속도는 '초당 셀' 단위 — 월드 단위로 환산해 둔다.
            m_fSpeed      = cDesc.fSpeed * m_cGrid.CELL_SIZE;
            m_fChaseSpeed = cDesc.fChaseSpeed * m_cGrid.CELL_SIZE;
            m_fTurnRate   = cDesc.fTurnRate;

            m_bChase     = false;
            m_vTargetPos = Vector2.zero;

            if (m_cMoveHandler.Initialize(m_cGrid, m_cGrid.Cell_ToWorld(cDesc.vStartCell),
                                          cDesc.vStartDir, m_fSpeed) == false)
                return false;

            transform.position = m_cMoveHandler.POS;
            transform.localScale = Vector3.one * m_cGrid.CELL_SIZE * 1.8f;

            Refresh_Color();
            return true;
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cGrid == null)
                return;

            m_cMoveHandler.Tick(fDeltaTime, m_bChase, m_vTargetPos, m_fTurnRate);
            transform.position = m_cMoveHandler.POS;
        }

        public override void Hide()
        {
            m_cGrid = null;
            base.Hide();
        }
        #endregion Engine.CGameObject

        /// <summary> 스테이지 매니저가 매 프레임 갱신한다. </summary>
        public void Set_ChaseState(bool bChase, Vector2 vTargetPos)
        {
            m_vTargetPos = vTargetPos;

            if (m_bChase == bChase)
                return;

            m_bChase = bChase;
            m_cMoveHandler.SPEED = bChase == true ? m_fChaseSpeed : m_fSpeed;
            Refresh_Color();
        }

        private void Refresh_Color()
        {
            if (m_srBody == null)
                return;

            m_srBody.color = m_bChase == true ? COLOR_CHASE : COLOR_WANDER;
        }
    }
}
