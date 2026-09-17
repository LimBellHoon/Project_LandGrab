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
    public class CEnemy : CGameObject, IImpactTarget
    {
        // 260904_기믹별로 색을 달리해 어떤 위협인지 한눈에 읽히게 한다. 추적 중에는 밝아진다.
        private static readonly Color COLOR_NONE       = new Color(1f, 0.35f, 0.35f);
        private static readonly Color COLOR_WEB        = new Color(0.65f, 0.95f, 0.35f);
        private static readonly Color COLOR_PROJECTILE = new Color(1f, 0.55f, 0.2f);
        private static readonly Color COLOR_SPAWN      = new Color(0.75f, 0.5f, 1f);

        [SerializeField] private SpriteRenderer m_srBody;

        private readonly CEnemyMoveHandler m_cMoveHandler = new CEnemyMoveHandler();
        // 260917_탄에 맞아 걸린 기절 · 감속 · 도트 · 번쩍임 (GYM CImpact)
        private readonly CImpactHandler    m_cImpact      = new CImpactHandler();
        private bool                       m_bWhiteShown;

        // 260904_기믹은 조합으로 붙인다. NONE이면 null이고, 그때는 배회/추적만 한다.
        private CEnemyGimmick m_cGimmick;

        private CTerritoryGrid  m_cGrid;
        private float           m_fSpeed;           // 월드 유닛/초
        // 260912_감속 스킬 배율. Set_ChaseState가 매번 속도를 다시 넣으므로
        // 여기에 따로 들고 있지 않으면 추격 상태가 바뀌는 순간 감속이 풀려 버린다.
        private float           m_fSpeedScale = 1f;
        private float           m_fChaseSpeed;      // 월드 유닛/초
        private float           m_fTurnRate;

        private bool            m_bChase;
        private Vector2         m_vTargetPos;
        // 260918_CStage_Manager가 매 프레임 넘겨 주는 "플레이어가 안전 지대 밖에 있는가".
        // 비헤이비어 트리가 있으면 이 값을 그대로 배회/추적으로 쓰지 않고 블랙보드에 넣어 트리가 판단한다.
        private bool            m_bExposed;

        // 260904_EnemyInfo.csv에서 들어온다. 기믹 수치는 m_cGimmick이 들고 있으므로 여기 두지 않는다.
        private int             m_iEnemyID;
        private ENEMY_GIMMICK   m_eGimmick;
        private float           m_fHitRange;        // 셀
        // 260918_기믹 사거리(셀). PROJECTILE류의 비헤이비어 트리가 ATTACK_RANGE로 쓴다.
        private float           m_fGimmickRange;

        // 260918_비헤이비어 트리(2-16) 첫 연결 — PROJECTILE 기믹(포수류)만 쓴다. 나머지는 null이라
        // Tick의 HAS_TREE 검사에서 그냥 건너뛴다(CEnemyBehaviorTree_Utility 참고).
        private CBehaviorTreeHandler m_cBehaviorTree;

        // 260916_런 스킬(회전탄/몽둥이)이 몬스터를 죽일 수 있어야 해서 처음 생긴 HP.
        // 260918_EnemyInfo.csv의 iHp로 몬스터별 값을 받는다. 이 상수는 표에 값이 없는
        // 행(0 이하)이나 CProtoTest처럼 손으로 만든 Desc를 위한 기본값으로만 남았다.
        // 260918_공격력(iAttack)은 없앴다 — 플레이어가 목숨제라 무엇에 맞든 한 목숨이다(2-14).
        private const int       DEFAULT_HP     = 3;
        private int             m_iHp;

        /// <summary> 260912_EnemyInfo.csv의 ID. 웨이브가 넘어갈 때 종류별 수를 셀 때 쓴다. </summary>
        public int              ENEMY_ID        => m_iEnemyID;
        /// <summary> 지금 배회 중인지 추적 중인지. 트리가 있는 몬스터는 트리가 정한 결과다. </summary>
        public bool              IS_CHASING      => m_bChase;
        public Vector2Int       CUR_CELL        => m_cMoveHandler.CELL;
        public Vector2          POS             => m_cMoveHandler.POS;
        /// <summary> 플레이어와의 충돌 반경(셀). 월드 거리로 쓰려면 CELL_SIZE를 곱한다. </summary>
        public float            HIT_RANGE       => m_fHitRange;
        /// <summary> Engine이 bCollect가 선 오브젝트를 알아서 풀로 돌려준다(CProjectile 설명 참고). </summary>
        public bool             IS_DEAD         => bCollect;

        #region IImpactTarget
        // 260917_탄 판정 반경은 플레이어 충돌 반경과 같은 값을 쓴다 — 몸 크기가 하나라서.
        public float            HIT_RADIUS      => m_cGrid != null ? m_fHitRange * m_cGrid.CELL_SIZE : 0f;
        public bool             IS_ALIVE        => bCollect == false && m_cGrid != null;
        public int              HP              => m_iHp;
        public CImpactHandler   IMPACT          => m_cImpact;

        public void Take_Damage(int iAmount) => Damage(iAmount);
        public void Push(Vector2 vDir, float fDistance, float fDuration) => Add_Knockback(vDir, fDistance, fDuration);
        #endregion IImpactTarget

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
            m_iEnemyID    = cDesc.iEnemyID;
            m_fSpeed      = cDesc.fSpeed * m_cGrid.CELL_SIZE;
            m_fSpeedScale = 1f;     // 풀에서 재사용되므로 지난 판의 감속을 지운다
            m_fChaseSpeed = cDesc.fChaseSpeed * m_cGrid.CELL_SIZE;
            m_fTurnRate   = cDesc.fTurnRate;

            m_eGimmick      = cDesc.eGimmick;
            m_fHitRange     = cDesc.fHitRange;
            m_fGimmickRange = cDesc.fGimmickRange;
            m_iHp           = cDesc.iHp > 0 ? cDesc.iHp : DEFAULT_HP;
            m_cImpact.Clear();      // 260917_풀에서 재사용되므로 지난 판의 기절 · 감속을 지운다
            m_bWhiteShown   = false;
            bCollect        = false;   // 풀에서 재사용되므로 지난 판의 죽음이 남지 않게 내려 둔다

            m_cGimmick = CEnemyGimmick.Create(cDesc.eGimmick);
            if (m_cGimmick != null && m_cGimmick.Initialize(this, m_cGrid, cDesc) == false)
                m_cGimmick = null;

            m_bChase     = false;
            m_bExposed   = false;
            m_vTargetPos = Vector2.zero;
            Setup_BehaviorTree();

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
            if (m_cGrid == null || bCollect == true)
                return;

            // 260917_탄 효과. 도트로 죽었으면 여기서 끝난다.
            Damage(m_cImpact.Tick(fDeltaTime));
            if (bCollect == true)
                return;

            // 260918_트리가 있으면 이번 프레임 배회/추적을 여기서 확정한다 — 아래 Apply_Speed/이동
            // 핸들러가 그 결과(m_bChase)를 쓴다. Set_MoveState를 부르는 쪽은 CEnemyBehaviorTree_Utility.
            if (m_cBehaviorTree != null && m_cBehaviorTree.HAS_TREE == true)
            {
                m_cBehaviorTree.BLACKBOARD.Set(BLACKBOARD_KEY.IS_TARGET_EXPOSED, m_bExposed);
                m_cBehaviorTree.BLACKBOARD.Set(BLACKBOARD_KEY.TARGET_POS, m_vTargetPos);
                m_cBehaviorTree.Tick(fDeltaTime);
            }

            Apply_Speed(m_bChase);
            Refresh_WhiteOut();

            // 기절 중에는 움직이지도 쏘지도 않는다. 넉백은 기절과 상관없이 밀려야 하므로 이동 핸들러는 돌리되 속도를 0으로 둔다.
            m_cMoveHandler.Tick(fDeltaTime, m_bChase, m_vTargetPos, m_fTurnRate);
            transform.position = m_cMoveHandler.POS;

            if (m_cImpact.IS_STUNNED == true)
                return;

            // m_vTargetPos는 추적 여부와 상관없이 매 프레임 갱신된다(Set_ChaseState).
            // 거미줄처럼 플레이어가 안 나와도 발동하는 기믹이 있어 여기서 항상 돌린다.
            m_cGimmick?.Tick(fDeltaTime, m_vTargetPos);
        }

        public override void Hide()
        {
            // 풀에 반납되므로 기믹과 창구를 끊는다. 다음 재사용 때 새로 만든다.
            m_cGimmick = null;
            m_cGrid    = null;
            m_cImpact.Clear();
            m_cBehaviorTree?.Release();     // 260918_진행 중이던 노드를 끊고 블랙보드를 비운다
            base.Hide();
        }
        #endregion Engine.CGameObject

        // 260918_비헤이비어 트리 첫 연결 — PROJECTILE 기믹(포수류)만 쓴다(CEnemyBehaviorTree_Utility.Build_Kite).
        // 풀에서 재사용된 오브젝트가 다른 기믹으로 바뀌면 Set_Tree(null)이 이전 트리를 걷어내 준다.
        private void Setup_BehaviorTree()
        {
            if (m_eGimmick != ENEMY_GIMMICK.PROJECTILE)
            {
                m_cBehaviorTree?.Set_Tree(null);
                return;
            }

            if (m_cBehaviorTree == null)
                m_cBehaviorTree = new CBehaviorTreeHandler();

            // 260918_순서 주의 — Set_Tree가 블랙보드를 비우므로 트리를 먼저 꽂고 값을 넣는다.
            // 거꾸로 하면 사거리가 0으로 지워져 포수가 늘 '사거리 밖'으로 보고 끝까지 쫓아온다.
            m_cBehaviorTree.Set_Tree(CEnemyBehaviorTree_Utility.Build_Kite(this, m_cBehaviorTree.BLACKBOARD, m_cGrid.CELL_SIZE));
            m_cBehaviorTree.BLACKBOARD.Set(BLACKBOARD_KEY.ATTACK_RANGE, m_fGimmickRange);
        }

        /// <summary> 기믹이 무언가를 소환할 창구를 꽂아 준다. 스테이지가 몬스터를 만든 직후 부른다. </summary>
        public void Set_GimmickHost(IGimmickHost cHost) => m_cGimmick?.Set_Host(cHost);

        // 260916_런 스킬(회전탄/몽둥이)이 때릴 때 부른다. HP가 0이 되면 bCollect가 서서
        // Engine이 다음 사이클에 알아서 풀로 돌려준다(Projectile/Web/Soul과 같은 자리) —
        // 여기서 직접 Collect_Object를 부르면 두 번 반납하게 된다.
        /// <summary> iAmount만큼 HP를 줄인다. 이미 죽었으면 무시한다. </summary>
        public void Damage(int iAmount)
        {
            if (iAmount <= 0 || bCollect == true)
                return;

            m_iHp = Mathf.Max(0, m_iHp - iAmount);
            if (m_iHp <= 0)
                bCollect = true;
        }

        /// <summary> 몽둥이 등 넉백 효과가 부른다. 잠깐 배회/추적을 멈추고 방향으로 밀려난다. </summary>
        public void Add_Knockback(Vector2 vDir, float fDistance, float fDuration)
            => m_cMoveHandler.Add_Knockback(vDir, fDistance, fDuration);

        /// <summary> 스테이지 매니저가 매 프레임 갱신한다. </summary>
        // 260912_감속 스킬. 1이면 원래 속도.
        public void Set_SpeedScale(float fScale)
        {
            m_fSpeedScale = Mathf.Clamp(fScale, 0.1f, 1f);
            Apply_Speed(m_bChase);
        }

        private void Apply_Speed(bool bChase)
        {
            // 260917_탄 효과(감속 · 기절)는 스킬 · 카드 감속과 따로 곱한다 — 스테이지가 넣는 배율에 덮어써지지 않게.
            float fImpact = m_cImpact.IS_STUNNED == true ? 0f : m_cImpact.SPEED_SCALE;
            m_cMoveHandler.SPEED = (bChase == true ? m_fChaseSpeed : m_fSpeed) * m_fSpeedScale * fImpact;
        }

        // 260917_번쩍임(WHITE_OUT)이 켜지고 꺼지는 순간에만 색을 다시 칠한다.
        private void Refresh_WhiteOut()
        {
            if (m_bWhiteShown == m_cImpact.IS_WHITE_OUT)
                return;

            m_bWhiteShown = m_cImpact.IS_WHITE_OUT;
            Refresh_Color();
        }


        /// <summary> CStage_Manager가 매 프레임 부른다. bExposed = 플레이어가 안전 지대 밖에 있는가. </summary>
        public void Set_ChaseState(bool bExposed, Vector2 vTargetPos)
        {
            m_bExposed   = bExposed;
            m_vTargetPos = vTargetPos;

            // 260918_트리가 있으면(PROJECTILE류) 실제 배회/추적은 Tick에서 트리가 정한다 —
            // 여기서는 상태만 갱신해 둔다(CEnemyBehaviorTree_Utility 참고).
            if (m_cBehaviorTree != null && m_cBehaviorTree.HAS_TREE == true)
                return;

            Set_MoveState(bExposed, vTargetPos);
        }

        /// <summary>
        /// 실제로 배회/추적 중 어느 쪽인지 정한다. 트리가 없는 몬스터는 Set_ChaseState가 곧바로 부르고,
        /// 트리가 있는 몬스터는 그 트리의 Action 노드가 대신 부른다 — 이동 규칙(CEnemyMoveHandler) 자체는
        /// 그대로 두고 '언제 쫓을지'만 갈아 끼우는 자리다.
        /// </summary>
        public void Set_MoveState(bool bChase, Vector2 vTargetPos)
        {
            m_vTargetPos = vTargetPos;

            if (m_bChase == bChase)
                return;

            m_bChase = bChase;
            Apply_Speed(bChase);
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

            if (m_bWhiteShown == true)
                cBase = Color.white;

            m_srBody.color = m_bChase == true ? Color.Lerp(cBase, Color.white, 0.45f) : cBase;
        }
    }
}
