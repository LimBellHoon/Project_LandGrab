using System;

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
        // 260904_기믹별로 색을 달리해 위협 종류를 한눈에 읽을 수 있게 한다. 추적 중에는 밝아진다.
        private static readonly Color COLOR_NONE       = new Color(1f, 0.35f, 0.35f);
        private static readonly Color COLOR_PROJECTILE = new Color(1f, 0.55f, 0.2f);
        private static readonly Color COLOR_WEB        = new Color(0.65f, 0.95f, 0.35f);
        private static readonly Color COLOR_SUMMON     = new Color(0.75f, 0.5f, 1f);

        [SerializeField] private SpriteRenderer m_srBody;

        private readonly CEnemyMoveHandler m_cMoveHandler = new CEnemyMoveHandler();

        private CTerritoryGrid  m_cGrid;
        private float           m_fSpeed;           // 월드 유닛/초
        private float           m_fChaseSpeed;      // 월드 유닛/초
        private float           m_fTurnRate;

        private bool            m_bChase;
        private Vector2         m_vTargetPos;

        // 260904_몬스터 기믹
        private ENEMY_GIMMICK   m_eGimmick;
        private float           m_fGimmickCool;
        private float           m_fGimmickTimer;

        public Vector2Int   CUR_CELL    => m_cMoveHandler.CELL;
        public Vector2      POS         => m_cMoveHandler.POS;
        public bool         IS_CHASING  => m_bChase;

        public ENEMY_GIMMICK GIMMICK    => m_eGimmick;
        public Vector2      TARGET_POS  => m_vTargetPos;
        /// <summary> 이 몬스터가 소환해 살아있는 미니 몬스터 수. 스테이지 매니저가 관리한다. </summary>
        public int          SUMMON_COUNT { get; set; }
        /// <summary> 미니 몬스터라면 자신을 소환한 몬스터. 죽을 때 SUMMON_COUNT를 되돌리는 데 쓴다. </summary>
        public CEnemy       SUMMONER     { get; set; }

        /// <summary> 기믹 발동. 실제 스폰은 스테이지 매니저가 담당한다(오브젝트 소유를 한 곳에 모으기 위함). </summary>
        public event Action<CEnemy> OnGimmick;

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

            // 260904_몬스터 기믹
            m_eGimmick      = cDesc.eGimmick;
            m_fGimmickCool  = Mathf.Max(0.1f, cDesc.fGimmickCool);
            m_fGimmickTimer = m_fGimmickCool;   // 스폰 직후 바로 쏘지 않도록 쿨타임부터 시작
            SUMMON_COUNT    = 0;
            SUMMONER        = null;

            if (m_cMoveHandler.Initialize(m_cGrid, m_cGrid.Cell_ToWorld(cDesc.vStartCell),
                                          cDesc.vStartDir, m_fSpeed) == false)
                return false;

            float fScale = cDesc.fScale > 0f ? cDesc.fScale : 1f;
            transform.position = m_cMoveHandler.POS;
            transform.localScale = Vector3.one * m_cGrid.CELL_SIZE * 1.8f * fScale;

            Refresh_Color();
            return true;
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cGrid == null)
                return;

            m_cMoveHandler.Tick(fDeltaTime, m_bChase, m_vTargetPos, m_fTurnRate);
            transform.position = m_cMoveHandler.POS;

            Tick_Gimmick(fDeltaTime);
        }

        public override void Hide()
        {
            OnGimmick = null;
            m_cGrid   = null;
            base.Hide();
        }

        // 260904_몬스터 기믹
        // 추적 중일 때만 발동한다 — 플레이어가 선 위에 있으면 완전히 안전해야 하기 때문.
        private void Tick_Gimmick(float fDeltaTime)
        {
            if (m_eGimmick == ENEMY_GIMMICK.NONE || m_bChase == false)
                return;

            m_fGimmickTimer -= fDeltaTime;
            if (m_fGimmickTimer > 0f)
                return;

            m_fGimmickTimer = m_fGimmickCool;
            OnGimmick?.Invoke(this);
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

            Color cBase;
            switch (m_eGimmick)
            {
                case ENEMY_GIMMICK.PROJECTILE: cBase = COLOR_PROJECTILE; break;
                case ENEMY_GIMMICK.WEB:        cBase = COLOR_WEB;        break;
                case ENEMY_GIMMICK.SUMMON:     cBase = COLOR_SUMMON;     break;
                default:                       cBase = COLOR_NONE;       break;
            }

            m_srBody.color = m_bChase == true ? Color.Lerp(cBase, Color.white, 0.45f) : cBase;
        }
    }
}
