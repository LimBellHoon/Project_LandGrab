using UnityEngine;

using Engine;

namespace Client
{
    // 260902_몬스터
    /// <summary>
    /// 미점령 지대만 돌아다닌다. 플레이어가 안전 지대에 있는 동안에는 쫓지 않고 배회하다가,
    /// 플레이어가 땅을 먹으러 나오는 순간부터 추적한다.
    /// 기믹(거미줄 / 투사체 / 부하 소환)은 CEnemyGimmick 모듈로 붙는다 — 상속이 아니라 조합이다.
    /// 그래서 몬스터 종류가 늘어도 프리팹은 하나면 된다 (EnemyInfo.csv의 eGimmick 한 칸).
    /// </summary>
    public class CEnemy : CGameObject
    {
        // 260904_기믹별로 색을 달리해 어떤 위협인지 한눈에 읽히게 한다. 추적 중에는 밝아진다.
        private static readonly Color COLOR_NONE       = new Color(1f, 0.35f, 0.35f);
        private static readonly Color COLOR_WEB        = new Color(0.65f, 0.95f, 0.35f);
        private static readonly Color COLOR_PROJECTILE = new Color(1f, 0.55f, 0.2f);
        private static readonly Color COLOR_SPAWN      = new Color(0.75f, 0.5f, 1f);

        [SerializeField] private SpriteRenderer m_srBody;

        private readonly CEnemyMoveHandler m_cMoveHandler = new CEnemyMoveHandler();

        // 260904_기믹은 조합으로 붙인다. NONE이면 null이고, 그때는 배회/추적만 한다.
        private CEnemyGimmick m_cGimmick;

        private CTerritoryGrid  m_cGrid;
        private float           m_fSpeed;           // 월드 유닛/초
        private float           m_fChaseSpeed;      // 월드 유닛/초
        private float           m_fTurnRate;

        private bool            m_bChase;
        private Vector2         m_vTargetPos;

        // 260904_EnemyInfo.csv에서 들어온다. 기믹 수치는 m_cGimmick이 들고 있으므로 여기 두지 않는다.
        private ENEMY_GIMMICK   m_eGimmick;
        private float           m_fHitRange;        // 셀

        public Vector2Int       CUR_CELL        => m_cMoveHandler.CELL;
        public Vector2          POS             => m_cMoveHandler.POS;
        /// <summary> 플레이어와의 충돌 반경(셀). 월드 거리로 쓰려면 CELL_SIZE를 곱한다. </summary>
        public float            HIT_RANGE       => m_fHitRange;

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

            // 260904_아래에서 CELL_SIZE를 바로 쓰므로 여기서 걸러야 한다 (CPlayer는 CMoveHandler가 걸러준다).
            if (m_cGrid == null)
            {
                Debug.LogError("[CEnemy] Grid가 null 입니다.");
                return false;
            }

            // Desc의 속도는 '초당 셀' 단위 — 월드 단위로 환산해 둔다.
            m_fSpeed      = cDesc.fSpeed * m_cGrid.CELL_SIZE;
            m_fChaseSpeed = cDesc.fChaseSpeed * m_cGrid.CELL_SIZE;
            m_fTurnRate   = cDesc.fTurnRate;

            m_eGimmick      = cDesc.eGimmick;
            m_fHitRange     = cDesc.fHitRange;

            m_cGimmick = CEnemyGimmick.Create(cDesc.eGimmick);
            if (m_cGimmick != null && m_cGimmick.Initialize(this, m_cGrid, cDesc) == false)
                m_cGimmick = null;

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

            // m_vTargetPos는 추적 여부와 상관없이 매 프레임 갱신된다(Set_ChaseState).
            // 거미줄처럼 플레이어가 안 나와도 발동하는 기믹이 있어 여기서 항상 돌린다.
            m_cGimmick?.Tick(fDeltaTime, m_vTargetPos);
        }

        public override void Hide()
        {
            // 풀에 반납되므로 기믹과 창구를 끊는다. 다음 재사용 때 새로 만든다.
            m_cGimmick = null;
            m_cGrid    = null;
            base.Hide();
        }
        #endregion Engine.CGameObject

        /// <summary> 기믹이 무언가를 소환할 창구를 꽂아 준다. 스테이지가 몬스터를 만든 직후 부른다. </summary>
        public void Set_GimmickHost(IGimmickHost cHost) => m_cGimmick?.Set_Host(cHost);

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

            Color cBase;
            switch (m_eGimmick)
            {
                case ENEMY_GIMMICK.WEB:        cBase = COLOR_WEB;        break;
                case ENEMY_GIMMICK.PROJECTILE: cBase = COLOR_PROJECTILE; break;
                case ENEMY_GIMMICK.SPAWN:      cBase = COLOR_SPAWN;      break;
                default:                       cBase = COLOR_NONE;       break;
            }

            m_srBody.color = m_bChase == true ? Color.Lerp(cBase, Color.white, 0.45f) : cBase;
        }
    }
}
