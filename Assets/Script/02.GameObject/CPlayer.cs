using System;
using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 플레이어
    /// <summary>
    /// Engine.CGameObject를 상속해 오브젝트 풀/레이어 Tick에 그대로 올라탄다.
    /// 260923_규칙 판정은 이동 핸들러가 한 걸음마다 CTerritoryGrid.Step_To에 넘기고, 여기는 그 결과에 반응만 한다.
    /// </summary>
    public class CPlayer : CGameObject, IImpactTarget
    {
        private const float INVINCIBLE_TIME  = 1.2f;    // 피격 후 무적 시간
        private const float EVADE_GRACE_TIME = 0.4f;    // 260905_회피 성공 후 빠져나갈 틈
        // 260923_부활할 자리가 경계에서 이만큼 넘게 떨어졌으면(갉혔으면) 가장 가까운 경계로 옮긴다(칸)
        private const float SAFE_POS_DRIFT = 0.05f;
        // 260923_자기 선을 밟았을 때의 고정 피해량 — 특정 몬스터의 공격력이 아니므로 EnemyInfo.iAttack과 무관하다.
        // 항상 HP를 전부 비우도록 충분히 큰 값을 쓴다(HP 풀이 아무리 커져도 즉사와 같은 결과).
        private const int   SELF_TRAIL_DAMAGE = 9999;
        // 260923_보호막이 한 번에 쌓을 수 있는 최대 충전 수.
        private const int   SHIELD_MAX = 3;
        // 260928_카드 30종 상한(태스크 #23, Docs/Design_Roguelite_Rewrite.md §7) — 최대 HP는 카드(G05/M07대형)로
        // 아무리 불려도 이 값을 넘지 않는다. SHIELD_MAX와 같은 자리(코드 상수, CSV/GameConfig가 아니다 —
        // 규칙 숫자가 아니라 밸런스 상한이라 여기 둔다).
        private const int   MAX_LIFE_CAP = 6;

        private readonly CInputHandler m_cInputHandler = new CInputHandler();
        private readonly CMoveHandler  m_cMoveHandler  = new CMoveHandler();
        // 260905_액티브 스킬. 쿨타임은 핸들러가, 실제 효과는 CSkillEffect가 맡는다.
        private readonly CSkillHandler m_cSkillHandler = new CSkillHandler();
        // 260917_적탄에 맞아 걸린 기절 · 감속 · 도트 · 번쩍임 (몬스터와 같은 모듈)
        private readonly CImpactHandler m_cImpact      = new CImpactHandler();

        [SerializeField] private SpriteRenderer m_srBody;

        private CTerritoryGrid  m_cGrid;
        private Vector2         m_vLastSafePos;         // 260923_안전 지대를 벗어나기 직전 자리(그리드 공간) — 사망 시 복귀 지점
        // 260923_다시 HP 풀이다 — 몬스터·탄마다 공격력이 달라 몇 번은 버틴다(EnemyInfo.iAttack, 2-14).
        private int             m_iLife;
        private int             m_iMaxLife;
        private float           m_fInvincibleTimer;
        private float           m_fBaseSpeed;       // 260904_거미줄 감속의 기준이 되는 원래 속도
        private float           m_fEvasion;         // 260905_피격 회피 확률 0~1
        // 260923_보호막 충전 수. 맞을 때마다 하나씩 소모해 그 피해를 통째로 막는다(여러 번 버틴다) — 소모품/카드가 중첩해서 쌓을 수 있다.
        private int             m_iShield;

        // 260912_스킬 효과. 종류별 모듈이라 CPlayer에는 분기가 없다.
        private CSkillEffect    m_cSkillEffect;
        private ISkillHost      m_cSkillHost;

        // 260916_런 전용 스킬(뱀서라이크) — 장착 스킬 하나(위)와 달리 여러 개를 동시에 든다.
        private readonly CRunSkillHandler          m_cRunSkillHandler = new CRunSkillHandler();
        private readonly List<CRunSkillEffect>     m_lstRunSkillEffect = new List<CRunSkillEffect>();
        private float           m_fPickupRadius;    // 260916_자석 스킬이 걸어 두는 값(셀). 픽업 쪽이 읽는다
        // 260916_영혼 수집가가 맵 위에 영혼을 놓을 창구. ISkillHost처럼 스테이지가 꽂아 준다.
        private IRunSkillHost   m_cRunSkillHost;

        // 260928_카드 30종(태스크 #22, Docs/Design_Card_Catalog_Spec.md) — CRunSkillHandler/Effect와 같은
        // 조합 구조(CCardHandler/CCardEffect, 1-1). 몬스터·그리드를 건드리는 카드는 CCardEffect가 아니라
        // CStage_Manager.Apply_Card가 직접 다룬다 — "그리드는 칸만 알고 몬스터는 스테이지가 본다"(2-3)와
        // 같은 구분. 여기서는 레벨 추적 + 플레이어 혼자 해결되는 수치만 든다.
        private readonly CCardHandler          m_cCardHandler = new CCardHandler();
        private readonly List<CCardEffect>     m_lstCardEffect = new List<CCardEffect>();
        // K01_ELECTRIC_LINE — 3번째 선 긋기마다 전기화된다. 카운터는 여기, 실제 판정(기절)은 CStage_Manager가 한다.
        private int              m_iDrawStartCount;
        private bool             m_bTrailElectrified;
        private float            m_fTrailElectrifyStun;
        // F03_FIREBREAK/G07_LAST_STAND — fuseMul에 더할 값의 합(스펙 §6.2, 음수면 느려진다). 매 프레임 카드 목록에서
        // 다시 계산한다(G07은 HP 1일 때만 적용되는 조건부라 값을 들고 있지 않고 매번 물어본다).
        // F07의 대가(+0.25)는 몬스터를 만지는 카드라 CStage_Manager가 따로 더한다.
        private float            m_fFuseMulCardDelta;
        // M07_SIZE_SHIFT(축소) — 몸 충돌 판정 거리에 곱할 배율. 플레이어가 몸집을 줄이는 카드라 여기 둔다.
        private float            m_fHitboxCardScale = 1f;
        // 260928_G04_MASS_SHIELD/M05_GHOST_STEP처럼 "한 번에 X% 이상 점령"이 조건인 카드가
        // OnCapture 핸들러 안에서 바로 읽을 수 있게, 점령 직후 매번 갱신해 둔다.
        private float            m_fLastCaptureRatio;
        // 260929_카드가 실제로 늘린 최대 HP · 카드 이동속도(M07 소형) — 스테이지(웨이브)마다 카드를 초기화할 때 정확히 되돌리려고 센다.
        // MAX_LIFE_CAP에 잘려 요청보다 적게 붙을 수 있어 요청값이 아니라 실제로 붙은 값을 적는다.
        private int              m_iCardMaxLifeBonus;
        private float            m_fCardSpeedBonusFromCards;
        // F05_FUSE_BOMB — 판당 무효화 횟수. CStage_Manager.Tick_Fuse가 따라잡히기 직전에 소모를 시도한다.
        private int              m_iFuseBombCharge;
        // G01_EXTINGUISHER — 판당 충전 수. CStage_Manager.Tick_Fuse가 발화 임박(1.5초 이내)에 소모를 시도한다.
        private int              m_iExtinguisherCharge;
        // M01_SPRINTER/M03_CORNERING/M06_NEARMISS_MASTER — 짧게 터지는 가속(시한부). 여러 장이면 가장 큰/긴 값으로 갱신한다.
        private float            m_fCardBurstSpeedBonus;
        private float            m_fCardBurstSpeedTimer;
        // M02_MOMENTUM — 직진할수록 누적되는 증가율(B에 더한다). 카드 자신이 매 프레임 다시 계산해 넣는다.
        private float            m_fMomentumSpeedBonus;
        // G07_LAST_STAND/F01_BURNING_HASTE — "조건이 맞는 동안만" B에 더하는 증가율의 합. 매 프레임 카드 목록에서 다시 계산한다.
        private float            m_fConditionalCardSpeedBonus;

        // 260912_속도는 두 갈래로 곱해진다 — 거미줄(환경)과 질주(스킬).
        // 스테이지가 매 프레임 환경 배율을 넣어 주므로, 스킬 배율을 따로 두지 않으면
        // 질주를 걸어도 다음 프레임에 덮어써져 아무 일도 일어나지 않는다.
        private float           m_fEnvSpeedScale   = 1f;
        private float           m_fSkillSpeedScale = 1f;
        // 260921_나선 가속(런 스킬) — 그리는 선이 길수록 빨라진다. 다른 갈래에 덮어써지지 않게 따로 든다.
        private float           m_fTrailSpeedScale = 1f;
        // 260921_선 긋기 시작 · 방향 전환을 알아채기 위한 직전 상태
        private bool            m_bWasDrawing;
        private MOVE_DIR        m_ePrevDir = MOVE_DIR.NONE;
        private float           m_fSkillSpeedTimer;
        // 260912_카드로 얹은 속도. 판이 끝날 때까지 유지되므로 타이머가 없다.
        private float           m_fCardSpeedScale = 1f;

        // 260923_사냥형 카드(HUNT_*, Docs/Design_Card_Pool.md 1장) — 전부 판이 끝날 때까지 누적된다.
        // 몬스터를 직접 만지는 것은 CStage_Manager라, 여기서는 수치만 들고 있다가 그쪽이 읽어 간다.
        private float           m_fBodyDamageScale   = 1f;   // 돌가죽 — 몸 충돌 피해 배율(곱해서 누적)
        private float           m_fKnockbackCardScale = 1f;  // 거센 팔뚝 — 넉백 거리 배율(더해서 누적)
        private int             m_iThornDamage;              // 가시 갑옷 — 몸 충돌한 몬스터에게 주는 고정 피해
        private int             m_iFeastHeal;                // 만찬 — 가시 갑옷으로 피해를 줄 때마다 회복(0이면 없음)
        private int             m_iTauntExtraHit;             // 도발 — 몸 충돌마다 On_MonsterHit을 추가로 부르는 횟수

        // 260928_회피형 카드(DODGE_*, Docs/Design_Card_Pool.md 2장). 확률·시간 가산값 중 일부는
        // 고정 상수다(NEARMISS_DODGE_BONUS 등) — CSV의 fValue는 "지속시간·쿨타임" 쪽만 조절한다.
        private const float     NEARMISS_DODGE_BONUS      = 0.15f;  // 아슬아슬한 본능 — 고정 회피 확률 가산
        private const float     PANIC_SPEED_BONUS         = 0.25f;  // 도주 본능 — 고정 이동속도 가산
        private const float     TURN_GRACE_INVINCIBLE_TIME = 0.15f; // 스치는 그림자 — 고정 무적시간
        private float           m_fInvincibleCardBonus;    // 여유로운 몸놀림 — 피격 후 무적시간 가산(누적)
        private float           m_fBoundarySpeedBonus;     // 외줄타기 — 경계선 위(안 긋는 동안)에서만 곱하는 배율 가산
        private float           m_fNearMissDodgeDuration;  // 아슬아슬한 본능 — NEAR MISS 성공 시 걸리는 지속시간
        private float           m_fNearMissDodgeTimer;
        private float           m_fPanicSpeedDuration;     // 도주 본능 — 피격 직후 가속 지속시간
        private float           m_fPanicSpeedTimer;
        private float           m_fTurnGraceCooldown;      // 스치는 그림자 — 쿨타임(짧을수록 좋으니 여러 장이면 가장 짧은 값)
        private float           m_fTurnGraceTimer;
        private float           m_fLuckyChainDuration;     // 요행 — 회피 성공 후 확정 회피가 예약되는 시간
        private bool            m_bLuckyChainArmed;
        private float           m_fLuckyChainTimer;
        private int             m_iFreeHitCharge;          // 잔영 — 몸 충돌 피해 무효화 1회권 개수

        // 260917_번쩍임에서 돌아올 원래 몸 색. 풀에서 재사용돼도 처음 한 번만 읽는다.
        private Color           m_cBodyColor;
        private bool            m_bBodyColorSaved;

        public int          LIFE            => m_iLife;
        public int          MAX_LIFE        => m_iMaxLife;
        public Vector2Int   CUR_CELL        => m_cMoveHandler.CUR_CELL;
        /// <summary> 260923_그리드 공간의 자리(칸 하나 = 1). 땅 판정은 이 좌표로 한다 </summary>
        public Vector2      GRID_POS        => m_cMoveHandler.POS;

        #region IImpactTarget
        public Vector2          POS             => m_cGrid != null ? (Vector2)m_cMoveHandler.WORLD_POS : (Vector2)transform.position;
        // 260917_탄의 판정 반경만으로 맞는다 — 예전 적탄 판정(탄 반경 안에 플레이어 중심)과 같다.
        public float            HIT_RADIUS      => 0f;
        public bool             IS_ALIVE        => m_cGrid != null && m_iLife > 0;
        // 조준(체력이 가장 많은 적)은 몬스터에게만 쓴다 — 플레이어는 남은 목숨을 낸다.
        int IImpactTarget.HP => m_iLife;
        public CImpactHandler   IMPACT          => m_cImpact;

        // 260923_탄 피해량(ProjectileInfo.iDamage)을 그대로 HP에서 뺀다 — 선 접촉이 아니므로 회피는 그대로 듣는다.
        public void Take_Damage(int iAmount) => Damage(iAmount);

        /// <summary> 플레이어는 칸을 따라 움직이므로 밀리지 않는다. 밀면 선이 끊겨 점령 규칙이 깨진다(2-3). </summary>
        public void Push(Vector2 vDir, float fDistance, float fDuration) { }
        #endregion IImpactTarget
        public bool         IS_INVINCIBLE   => m_fInvincibleTimer > 0f;
        /// <summary> 260905_보호막을 들고 있는가. UI가 표시에 쓴다. </summary>
        public bool         HAS_SHIELD      => m_iShield > 0;
        /// <summary> 260923_지금 몇 번 더 버티는가. UI가 개수를 보여주려면 이걸 읽는다. </summary>
        public int          SHIELD_COUNT    => m_iShield;
        /// <summary> 260904_UI가 조이스틱을 그리려고 읽는다. </summary>
        public CVirtualJoystick JOYSTICK    => m_cInputHandler.JOYSTICK;
        /// <summary> 260905_UI가 쿨타임 게이지를 그리려고 읽는다. </summary>
        public CSkillHandler    SKILL       => m_cSkillHandler;
        /// <summary> 260916_지금 들고 있는 런 스킬들의 레벨. UI/디버그가 읽는다. </summary>
        public CRunSkillHandler RUN_SKILL   => m_cRunSkillHandler;
        /// <summary> 260916_지금 이동 중인가. 분노 게이지가 '달릴 때' 조건으로 쓴다. </summary>
        public bool             IS_MOVING   => m_cMoveHandler.IS_MOVING;
        /// <summary> 260916_자석 스킬이 걸어 둔 습득 범위(셀). 픽업 쪽이 읽는다. </summary>
        public float            PICKUP_RADIUS => m_fPickupRadius;
        /// <summary> 260928_지금 들고 있는 카드들의 레벨(태스크 #22). UI/디버그가 읽는다. </summary>
        public CCardHandler      CARD           => m_cCardHandler;
        /// <summary> K01_ELECTRIC_LINE — 지금 긋는 중인 선이 전기화됐는가. CStage_Manager의 트레일 접촉 판정이 읽는다. </summary>
        public bool              TRAIL_ELECTRIFIED     => m_bTrailElectrified;
        public float             TRAIL_ELECTRIFY_STUN  => m_fTrailElectrifyStun;
        /// <summary> F03/F07/G07 등이 도화선 전파 속도에 곱하는 배율. 1이면 영향 없음. CStage_Manager.Tick_Fuse가 읽는다. </summary>
        /// <summary> 260929_카드가 fuseMul에 더하는 값의 합(F03/G07, 스펙 §6.2). 0이면 영향 없음. </summary>
        public float             FUSE_MUL_DELTA => m_fFuseMulCardDelta;
        /// <summary> M07_SIZE_SHIFT(축소)가 몸 충돌 판정 거리에 곱하는 배율. 1이면 영향 없음. </summary>
        public float             HITBOX_SCALE     => m_fHitboxCardScale;
        /// <summary> 260928_방금 점령이 맵 전체에서 차지한 비율(0~1). OnCapture 핸들러 안에서만 유효하다. </summary>
        public float             LAST_CAPTURE_RATIO => m_fLastCaptureRatio;
        /// <summary> 260928_지금 긋고 있는 선이 도화선에 타는 중인가. F01_BURNING_HASTE가 읽는다. </summary>
        public bool              IS_TRAIL_BURNING  => m_cGrid != null && m_cGrid.IS_TRAIL_BURNING;
        // 260923_사냥형 카드(HUNT_*) — CStage_Manager가 몸 충돌 처리부에서 읽어 간다(1-1, 몸 충돌은 스테이지가 본다)
        /// <summary> 돌가죽 — 몸 충돌 피해에 곱할 배율. 1이면 감소 없음 </summary>
        public float             BODY_DAMAGE_SCALE => m_fBodyDamageScale;
        /// <summary> 거센 팔뚝 — 넉백 거리에 곱할 배율. 1이면 가산 없음 </summary>
        public float             KNOCKBACK_SCALE   => m_fKnockbackCardScale;
        /// <summary> 가시 갑옷 — 몸 충돌한 몬스터에게 줄 고정 피해. 0이면 없음(만찬도 걸리지 않는다) </summary>
        public int               THORN_DAMAGE      => m_iThornDamage;
        /// <summary> 만찬 — 가시 갑옷 피해를 줄 때마다 회복할 양. 0이면 없음 </summary>
        public int               FEAST_HEAL        => m_iFeastHeal;
        /// <summary> 도발 — 몸 충돌마다 추가로 부를 On_MonsterHit 횟수 </summary>
        public int               TAUNT_EXTRA_HIT   => m_iTauntExtraHit;
        // 260921_이동 런 스킬이 읽는 상태(2-11-3)
        /// <summary> 260923_지금 긋고 있는 선의 길이(칸). 나선 가속이 본다 </summary>
        public float            TRAIL_LENGTH => m_cGrid != null ? m_cGrid.TRAIL_LENGTH : 0f;
        /// <summary> 지금 선을 긋는 중인가(= 안전 지대 밖) </summary>
        public bool             IS_DRAWING  => m_cGrid != null && m_cGrid.IS_DRAWING;
        /// <summary> 지금 향하고 있는 방향. 멈춰 있으면 마지막 방향, 그것도 없으면 위쪽 </summary>
        public Vector2          FACING
        {
            get
            {
                // 260923_선을 긋는 중이면 실제로 나아가는 방향(사선 · 나선), 아니면 마지막으로 움직인 방향
                if (IS_DRAWING == true)
                    return m_cMoveHandler.HEADING;

                Vector2 vDir = CTerritoryGrid.Dir_ToVector(m_cMoveHandler.CUR_DIR);
                return vDir == Vector2.zero ? Vector2.up : vDir;
            }
        }

        /// <summary> 새로 점령한 셀 개수를 전달 </summary>
        public event Action<int> OnCapture;
        /// <summary> 남은 HP를 전달 </summary>
        public event Action<int> OnLifeChanged;
        /// <summary> HP를 전부 잃음 </summary>
        public event Action OnDead;
        /// <summary> 260905_회피 성공. 연출/사운드를 붙일 자리. </summary>
        public event Action OnEvade;
        // 260916_OnLifeChanged는 Add_Life에도 불려 '맞았다'만 골라 듣기 어렵다.
        /// <summary> 실제로 맞아 HP가 줄었을 때만. 카메라 흔들림 같은 피격 연출은 이걸 들을 것. </summary>
        public event Action OnDamaged;
        // 260921_이동 런 스킬이 듣는 훅(2-11-1). 새 이벤트를 뚫은 이유는 기존 훅으로는
        // '선을 긋기 시작한 순간'과 '방향을 꺾은 순간'을 가려낼 수 없어서다.
        /// <summary> 안전 지대를 벗어나 선을 긋기 시작한 순간 (유령 걸음) </summary>
        public event Action OnDrawStart;
        /// <summary> 이동 방향이 바뀐 순간 (잔상) </summary>
        public event Action OnTurn;
        // 260928_카드 30종(태스크 #22)이 듣는 훅.
        /// <summary> NEAR MISS 판정에 성공한 순간(2-24). CStage_Manager가 On_NearMiss()를 부를 때 같이 올라간다. </summary>
        public event Action OnNearMissSuccess;
        // 260920_점령 판정에 몬스터를 더는 넘기지 않는다 — 가두면 무조건 먹고, 갇힌 몬스터는 죽는다(2-3).

        #region Engine.CGameObject
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CPlayerDesc cDesc) == false)
            {
                Debug.LogError("[CPlayer] CPlayerDesc가 아닙니다.");
                return false;
            }

            m_cGrid             = cDesc.cGrid;
            m_iMaxLife          = Mathf.Max(1, cDesc.iLife);
            m_iLife             = m_iMaxLife;
            m_fInvincibleTimer  = 0f;
            m_vLastSafePos      = cDesc.vStartPos;
            m_fBaseSpeed        = cDesc.fMoveSpeed;
            m_fEvasion          = Mathf.Clamp01(cDesc.fEvasion);
            m_iShield           = 0;
            m_cSkillHandler.Initialize(cDesc.cSkillInfo, cDesc.iSkillLevel);

            // 풀에서 재사용되므로 지난 판의 효과가 남지 않게 전부 되돌린다.
            m_fEnvSpeedScale   = 1f;
            m_fSkillSpeedScale = 1f;
            m_fSkillSpeedTimer = 0f;
            m_fTrailSpeedScale = 1f;
            m_bWasDrawing      = false;
            m_ePrevDir         = MOVE_DIR.NONE;
            m_fCardSpeedScale  = 1f;
            m_fBodyDamageScale = 1f;
            m_fKnockbackCardScale = 1f;
            m_iThornDamage     = 0;
            m_iFeastHeal       = 0;
            m_iTauntExtraHit   = 0;
            m_fInvincibleCardBonus   = 0f;
            m_fBoundarySpeedBonus    = 0f;
            m_fNearMissDodgeDuration = 0f;
            m_fNearMissDodgeTimer    = 0f;
            m_fPanicSpeedDuration    = 0f;
            m_fPanicSpeedTimer       = 0f;
            m_fTurnGraceCooldown     = 0f;
            m_fTurnGraceTimer        = 0f;
            m_fLuckyChainDuration    = 0f;
            m_bLuckyChainArmed       = false;
            m_fLuckyChainTimer       = 0f;
            m_iFreeHitCharge         = 0;
            m_cImpact.Clear();

            // 260916_런 스킬은 판마다 완전히 초기화된다(뱀서라이크 — 스테이지를 나가면 사라진다).
            Clear_RunSkill();
            // 260928_카드도 마찬가지다(태스크 #22) — 판이 끝나면 전부 사라진다.
            // 260929_최대 HP · 이동속도는 위에서 이미 처음 값으로 되돌렸으므로, Clear_Card가 또 빼지 않게 센 값을 먼저 0으로 만든다.
            m_iCardMaxLifeBonus        = 0;
            m_fCardSpeedBonusFromCards = 0f;
            Clear_Card();

            m_cSkillEffect = CSkillEffect.Create(cDesc.cSkillInfo != null ? cDesc.cSkillInfo.eType
                                                                         : SKILL_TYPE.NONE);
            if (m_cSkillEffect != null && m_cSkillEffect.Initialize(this) == true)
                m_cSkillEffect.Set_Host(m_cSkillHost);

            // 260920_캐릭터별 이동 방식(2-22). 판이 시작할 때 한 번 정하고 도중에는 바뀌지 않는다.
            m_cMoveHandler.Set_MoveStyle(cDesc.eMoveStyle);
            m_cInputHandler.Set_MoveStyle(cDesc.eMoveStyle);

            if (m_cMoveHandler.Initialize(m_cGrid, cDesc.vStartPos, cDesc.fMoveSpeed) == false)
                return false;

            m_cMoveHandler.Snap_ToBoundary();
            transform.position = m_cMoveHandler.WORLD_POS;
            // 바디 스프라이트는 1 월드 유닛 크기로 제작되어 있으므로, 셀 크기의 1.6배로 맞춘다.
            transform.localScale = Vector3.one * m_cGrid.CELL_SIZE * 1.6f;

            m_cInputHandler.Initialize();
            m_cInputHandler.Clear();
            return true;
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cGrid == null)
                return;

            if (m_fInvincibleTimer > 0f)
            {
                m_fInvincibleTimer -= fDeltaTime;
                Refresh_InvincibleBlink();
            }

            Tick_SkillSpeed(fDeltaTime);
            m_cSkillHandler.Tick(fDeltaTime);
            Tick_SkillBuffer(fDeltaTime);
            Tick_RunSkill(fDeltaTime);
            Tick_DodgeCards(fDeltaTime);
            Tick_Card(fDeltaTime);

            // 260917_적탄 효과(기절 · 감속 · 번쩍임).
            // 260918_도트는 플레이어에게 걸지 않는다 — 목숨제라 초마다 목숨이 하나씩 빠지면 맞자마자 끝난다.
            m_cImpact.Tick(fDeltaTime);
            if (m_cGrid == null || m_iLife <= 0)
                return;

            Apply_Speed();
            Refresh_WhiteOut();

            // 기절 중에는 입력도 이동도 멈춘다. 입력을 버려야 풀리는 순간 눌러 둔 방향으로 튀어 나가지 않는다.
            if (m_cImpact.IS_STUNNED == true)
            {
                m_cInputHandler.Clear();
                transform.position = m_cMoveHandler.WORLD_POS;
                return;
            }

            m_cInputHandler.Tick();

            // 260922_에디터 · 키보드 — E 또는 J가 스킬 버튼이다
            if (m_cInputHandler.SKILL_PRESSED == true && fDeltaTime > 0f)
                Request_Skill();

            // 260923_한 걸음의 판정은 이동 핸들러가 그리드(Step_To)에 넘긴다 — 여기는 결과에 반응만 한다
            STEP_RESULT eResult = m_cMoveHandler.Tick(fDeltaTime, m_cInputHandler.DESIRED_DIR, out int iCapturedCount);
            Handle_Step(eResult, iCapturedCount);

            Tick_MoveSignal();
            transform.position = m_cMoveHandler.WORLD_POS;
        }

        public override void Hide()
        {
            Clear_RunSkill();

            // 풀에 반납되므로 외부 구독을 끊어 다음 재사용에 새지 않게 한다.
            // 260929_Clear_Card는 최대 HP를 되돌리며 OnLifeChanged를 부를 수 있어 구독을 끊은 뒤에 부른다.
            OnCapture       = null;
            OnLifeChanged   = null;
            OnDead          = null;
            OnEvade         = null;
            OnDrawStart     = null;
            OnTurn          = null;
            OnDamaged       = null;
            OnNearMissSuccess = null;
            Clear_Card();
            m_cGrid         = null;
            m_cImpact.Clear();

            base.Hide();
        }
        #endregion Engine.CGameObject

        #region 규칙 판정
        private STEP_RESULT Handle_Step(STEP_RESULT eResult, int iCapturedCount)
        {
            // 규칙 판정 자체는 그리드가 소유한다. 플레이어는 결과에 반응만 한다.
            switch (eResult)
            {
                case STEP_RESULT.DEAD:
                    // 260923_자기 선 밟기는 특정 몬스터가 준 피해가 아니라 고정 피해량을 쓴다 —
                    // 항상 HP를 전부 비워 즉사와 같은 결과를 낸다(회피는 못 흘리고, 보호막·무적은 그대로 막는다).
                    Damage(SELF_TRAIL_DAMAGE, true);
                    break;

                case STEP_RESULT.CAPTURE:
                    // 260920_점령하고 나면 방금 그은 선이 점령지 '안쪽'이 되는 일이 잦다.
                    // 그대로 두면 이동 규칙(2-3)의 예외에 걸려 내부를 마음대로 돌아다니게 된다 —
                    // 월보(런 스킬)를 공짜로 얻은 셈이라, 가장 가까운 경계선으로 되돌려 놓는다.
                    m_cMoveHandler.Snap_ToBoundary();
                    m_vLastSafePos = m_cMoveHandler.POS;
                    // 260928_"한 번에 X% 이상 점령" 카드(G04/M05 등)가 OnCapture 핸들러 안에서 바로 읽는다.
                    m_fLastCaptureRatio = m_cGrid != null && m_cGrid.PLAYABLE_COUNT > 0
                        ? (float)iCapturedCount / m_cGrid.PLAYABLE_COUNT : 0f;
                    OnCapture?.Invoke(iCapturedCount);
                    break;

                case STEP_RESULT.SAFE:
                    m_vLastSafePos = m_cMoveHandler.POS;
                    break;
            }

            return eResult;
        }

        // 260904_거미줄 감속. 스테이지가 매 프레임 '지금 밟고 있는 칸'을 보고 넣어 준다.
        /// <param name="fScale"> 원래 속도에 곱할 값. 1이면 감속 없음. </param>
        public void Set_SpeedScale(float fScale)
        {
            m_fEnvSpeedScale = Mathf.Max(0.1f, fScale);
            Apply_Speed();
        }

        // 260912_질주. 환경 감속과 곱해지므로 거미줄 위에서 써도 둘 다 살아 있다.
        /// <param name="fScale"> 원래 속도에 곱할 값. 1.8이면 1.8배 </param>
        public void Add_SkillSpeed(float fScale, float fDuration)
        {
            if (fScale <= 0f || fDuration <= 0f)
                return;

            m_fSkillSpeedScale = fScale;
            m_fSkillSpeedTimer = fDuration;
            Apply_Speed();
        }

        /// <summary> 260921_나선 가속 — 선 길이에 따른 배율. 1이면 없는 것과 같다. </summary>
        public void Set_TrailSpeedScale(float fScale)
        {
            m_fTrailSpeedScale = Mathf.Max(0.1f, fScale);
            Apply_Speed();
        }

        // 260912_카드 효과. 판이 끝날 때까지 유지된다.
        /// <param name="fRatio"> 더할 비율. 0.15면 15% 빨라진다 </param>
        public void Add_CardSpeed(float fRatio)
        {
            if (fRatio <= 0f)
                return;

            m_fCardSpeedScale += fRatio;
            Apply_Speed();
        }

        /// <param name="fRatio"> 더할 회피 확률. 1을 넘지 않는다 </param>
        public void Add_Evasion(float fRatio)
        {
            if (fRatio <= 0f)
                return;

            m_fEvasion = Mathf.Clamp01(m_fEvasion + fRatio);
        }

        // 260923_사냥형 카드(HUNT_*, Docs/Design_Card_Pool.md 1장). SLOW 카드(곱해서 누적)와
        // SPEED/EVASION 카드(더해서 누적)의 패턴을 그대로 따른다 — 새 누적 방식을 만들지 않는다.
        /// <summary> 돌가죽 — 몸 충돌 피해 배율을 곱해서 줄인다. </summary>
        public void Add_BodyDamageReduction(float fScale)
        {
            if (fScale <= 0f)
                return;

            m_fBodyDamageScale = Mathf.Clamp(m_fBodyDamageScale * fScale, 0.1f, 1f);
        }

        /// <summary> 거센 팔뚝 — 넉백 거리 배율을 더한다. </summary>
        public void Add_KnockbackBonus(float fRatio)
        {
            if (fRatio <= 0f)
                return;

            m_fKnockbackCardScale += fRatio;
        }

        /// <summary> 가시 갑옷 — 몸 충돌한 몬스터에게 줄 고정 피해를 더한다. </summary>
        public void Add_ThornDamage(int iAmount)
        {
            if (iAmount <= 0)
                return;

            m_iThornDamage += iAmount;
        }

        /// <summary> 만찬 — 가시 갑옷 피해를 줄 때마다 회복할 양을 더한다. </summary>
        public void Add_FeastHeal(int iAmount)
        {
            if (iAmount <= 0)
                return;

            m_iFeastHeal += iAmount;
        }

        /// <summary> 도발 — 몸 충돌마다 추가로 부를 On_MonsterHit 횟수를 더한다. </summary>
        public void Add_TauntBonus(int iCount)
        {
            if (iCount <= 0)
                return;

            m_iTauntExtraHit += iCount;
        }

        // 260928_회피형 카드(DODGE_*, Docs/Design_Card_Pool.md 2장).
        /// <summary> 여유로운 몸놀림 — 피격 후 무적시간 가산을 더한다. </summary>
        public void Add_InvincibleBonus(float fSeconds)
        {
            if (fSeconds <= 0f)
                return;

            m_fInvincibleCardBonus += fSeconds;
        }

        /// <summary> 외줄타기 — 경계선 위에서만 곱하는 속도 배율 가산을 더한다. </summary>
        public void Add_BoundarySpeed(float fRatio)
        {
            if (fRatio <= 0f)
                return;

            m_fBoundarySpeedBonus += fRatio;
        }

        /// <summary> 아슬아슬한 본능 — NEAR MISS 성공 시 걸릴 지속시간을 늘린다(여러 장이면 가장 긴 값). </summary>
        public void Add_NearMissDodge(float fDuration)
        {
            if (fDuration <= 0f)
                return;

            m_fNearMissDodgeDuration = Mathf.Max(m_fNearMissDodgeDuration, fDuration);
        }

        /// <summary> 아슬아슬한 본능 — NEAR MISS 판정 성공 시(2-24) CStage_Manager가 부른다. </summary>
        public void On_NearMiss()
        {
            if (m_fNearMissDodgeDuration > 0f)
                m_fNearMissDodgeTimer = m_fNearMissDodgeDuration;

            OnNearMissSuccess?.Invoke();
        }

        /// <summary> 도주 본능 — 피격 직후 가속 지속시간을 늘린다(여러 장이면 가장 긴 값). </summary>
        public void Add_PanicSpeed(float fDuration)
        {
            if (fDuration <= 0f)
                return;

            m_fPanicSpeedDuration = Mathf.Max(m_fPanicSpeedDuration, fDuration);
        }

        /// <summary> 스치는 그림자 — 방향 전환 무적의 쿨타임을 짧게 한다(여러 장이면 가장 짧은 값). </summary>
        public void Add_TurnGrace(float fCooldown)
        {
            if (fCooldown <= 0f)
                return;

            m_fTurnGraceCooldown = m_fTurnGraceCooldown <= 0f ? fCooldown : Mathf.Min(m_fTurnGraceCooldown, fCooldown);
        }

        /// <summary> 요행 — 회피 성공 뒤 확정 회피가 예약되는 시간을 늘린다(여러 장이면 가장 긴 값). </summary>
        public void Add_LuckyChain(float fDuration)
        {
            if (fDuration <= 0f)
                return;

            m_fLuckyChainDuration = Mathf.Max(m_fLuckyChainDuration, fDuration);
        }

        /// <summary> 잔영 — 몸 충돌 피해를 무효화할 1회권을 더한다. </summary>
        public void Add_FreeHit(int iCount)
        {
            if (iCount <= 0)
                return;

            m_iFreeHitCharge += iCount;
        }

        // 260928_회피형 카드 시한부 타이머 — 한곳에서 줄인다(2-24 아슬아슬 등과 같은 결).
        private void Tick_DodgeCards(float fDeltaTime)
        {
            if (m_fNearMissDodgeTimer > 0f)
                m_fNearMissDodgeTimer = Mathf.Max(0f, m_fNearMissDodgeTimer - fDeltaTime);

            if (m_fPanicSpeedTimer > 0f)
                m_fPanicSpeedTimer = Mathf.Max(0f, m_fPanicSpeedTimer - fDeltaTime);

            if (m_fTurnGraceTimer > 0f)
                m_fTurnGraceTimer = Mathf.Max(0f, m_fTurnGraceTimer - fDeltaTime);

            if (m_fLuckyChainTimer > 0f)
            {
                m_fLuckyChainTimer -= fDeltaTime;
                if (m_fLuckyChainTimer <= 0f)
                {
                    m_fLuckyChainTimer = 0f;
                    m_bLuckyChainArmed = false;
                }
            }
        }

        /// <summary> 잠깐 무적. 이미 더 길게 걸려 있으면 줄이지 않는다. </summary>
        public void Add_Invincible(float fSeconds)
        {
            if (fSeconds <= 0f)
                return;

            m_fInvincibleTimer = Mathf.Max(m_fInvincibleTimer, fSeconds);
        }

        /// <summary> 스킬이 몬스터처럼 플레이어 밖을 건드릴 때 쓰는 창구. 스테이지가 꽂아 준다. </summary>
        public void Set_SkillHost(ISkillHost cHost)
        {
            m_cSkillHost = cHost;
            m_cSkillEffect?.Set_Host(cHost);
        }

        // 260916_런 전용 스킬(뱀서라이크) — 스테이지 내 3지선다가 고른 것을 여기로 넘긴다.
        /// <summary> 새로 얻으면 1레벨로 붙고, 이미 있으면 다음 레벨로 오른다. </summary>
        public void Add_RunSkill(CRunSkillInfo cInfo)
        {
            if (cInfo == null)
                return;

            int iLevel = m_cRunSkillHandler.Add_Or_LevelUp(cInfo);

            CRunSkillEffect cOwned = Find_RunSkillEffect(cInfo.eType);
            if (cOwned != null)
            {
                cOwned.On_LevelChanged(cInfo, iLevel);
                return;
            }

            CRunSkillEffect cEffect = CRunSkillEffect.Create(cInfo.eType);
            if (cEffect == null)
                return;

            cEffect.Initialize(this);
            cEffect.Set_Host(m_cRunSkillHost);
            cEffect.On_LevelChanged(cInfo, iLevel);
            m_lstRunSkillEffect.Add(cEffect);
        }

        // 260917_런 스킬 각성 — 3지선다에서 고른 각성을 그 액티브에 건다. 슬롯을 새로 먹지 않는다.
        /// <returns> 그 액티브를 들고 있지 않거나 이미 각성했으면 false </returns>
        public bool Awaken_RunSkill(CAwakenInfo cInfo)
        {
            if (cInfo == null || m_cRunSkillHandler.Awaken(cInfo.eActiveType) == false)
                return false;

            CRunSkillEffect cEffect = Find_RunSkillEffect(cInfo.eActiveType);
            cEffect?.On_Awaken(cInfo);
            return true;
        }

        public CRunSkillEffect Find_RunSkillEffect(RUN_SKILL_TYPE eType)
        {
            for (int i = 0; i < m_lstRunSkillEffect.Count; ++i)
            {
                if (m_lstRunSkillEffect[i].TYPE == eType)
                    return m_lstRunSkillEffect[i];
            }
            return null;
        }

        /// <summary> 260917_분노가 터져 있는가. 효과끼리 서로를 몰라도 이 값 하나로 겹친다(광란의 칼바람). </summary>
        public bool IS_FEVER
        {
            get
            {
                for (int i = 0; i < m_lstRunSkillEffect.Count; ++i)
                {
                    if (m_lstRunSkillEffect[i].IS_FEVER == true)
                        return true;
                }
                return false;
            }
        }

        /// <summary> 영혼 수집가가 맵 위에 영혼을 놓을 창구. 스테이지가 꽂아 준다. </summary>
        public void Set_RunSkillHost(IRunSkillHost cHost)
        {
            m_cRunSkillHost = cHost;

            for (int i = 0; i < m_lstRunSkillEffect.Count; ++i)
                m_lstRunSkillEffect[i].Set_Host(cHost);
        }

        /// <summary> 영혼을 주웠을 때(CStage_Manager가 판정) 모든 런 스킬 효과에 알린다. </summary>
        public void On_SoulCollected()
        {
            for (int i = 0; i < m_lstRunSkillEffect.Count; ++i)
                m_lstRunSkillEffect[i].On_SoulCollected();
        }

        /// <summary> 회전탄/몽둥이가 몬스터를 때렸을 때(CStage_Manager가 판정) 모든 런 스킬 효과에 알린다. </summary>
        public void On_MonsterHit()
        {
            for (int i = 0; i < m_lstRunSkillEffect.Count; ++i)
                m_lstRunSkillEffect[i].On_MonsterHit();
        }

        // 260928_카드 30종(태스크 #22) — 3지선다가 고른 것을 여기로 넘긴다. 레벨 추적은 항상 하고,
        // CCardEffect.Create가 뭔가를 만들면 그것도 같이 레벨을 맞춘다(만들지 않으면 순수 스테이지 카드라는
        // 뜻이다 — CStage_Manager.Apply_Card가 자기 몫을 따로 처리한다).
        /// <returns> 적용된 이후 레벨 </returns>
        public int Add_Card(CCardInfo cInfo)
        {
            if (cInfo == null)
                return 0;

            int iLevel = m_cCardHandler.Add_Or_LevelUp(cInfo.eType, Mathf.Max(1, cInfo.iMaxLevel));

            CCardEffect cOwned = Find_CardEffect(cInfo.eType);
            if (cOwned != null)
            {
                cOwned.On_LevelChanged(cInfo, iLevel);
                return iLevel;
            }

            CCardEffect cEffect = CCardEffect.Create(cInfo.eType);
            if (cEffect == null)
                return iLevel;

            cEffect.Initialize(this);
            cEffect.On_LevelChanged(cInfo, iLevel);
            m_lstCardEffect.Add(cEffect);
            return iLevel;
        }

        public CCardEffect Find_CardEffect(CARD_TYPE eType)
        {
            for (int i = 0; i < m_lstCardEffect.Count; ++i)
            {
                if (m_lstCardEffect[i].TYPE == eType)
                    return m_lstCardEffect[i];
            }
            return null;
        }

        private void Tick_Card(float fDeltaTime)
        {
            if (m_fCardBurstSpeedTimer > 0f)
            {
                m_fCardBurstSpeedTimer -= fDeltaTime;
                if (m_fCardBurstSpeedTimer <= 0f)
                {
                    m_fCardBurstSpeedTimer = 0f;
                    m_fCardBurstSpeedBonus = 0f;
                }
            }

            float fFuseDelta        = 0f;
            float fHitboxScale      = 1f;
            float fSpeedBonus       = 0f;

            for (int i = 0; i < m_lstCardEffect.Count; ++i)
            {
                m_lstCardEffect[i].Tick(fDeltaTime);
                fFuseDelta        += m_lstCardEffect[i].Get_FuseMulDelta();
                fHitboxScale      *= m_lstCardEffect[i].Get_HitboxScale();
                fSpeedBonus       += m_lstCardEffect[i].Get_SpeedBonus();
            }

            m_fFuseMulCardDelta          = fFuseDelta;
            m_fHitboxCardScale           = Mathf.Max(0.1f, fHitboxScale);
            m_fConditionalCardSpeedBonus = fSpeedBonus;
            // 260928_Apply_Speed()는 CPlayer.Tick()이 이 함수 뒤에 매 프레임 다시 부른다 — 여기서 두 번 부르지 않는다.
        }

        /// <summary>
        /// 카드를 전부 잃는다 — 스테이지(웨이브)가 시작될 때마다 CStage_Manager가, 풀에 돌아갈 때 Hide가 부른다.
        /// 260929_카드로 늘린 최대 HP · 이동속도도 함께 되돌린다(예전에는 판이 끝나야만 지웠다).
        /// </summary>
        public void Clear_Card()
        {
            for (int i = 0; i < m_lstCardEffect.Count; ++i)
                m_lstCardEffect[i].Release();

            m_lstCardEffect.Clear();
            m_cCardHandler.Clear();

            if (m_iCardMaxLifeBonus > 0)
            {
                m_iMaxLife -= m_iCardMaxLifeBonus;
                m_iLife     = Mathf.Min(m_iLife, m_iMaxLife);
                m_iCardMaxLifeBonus = 0;
                OnLifeChanged?.Invoke(m_iLife);
            }

            if (m_fCardSpeedBonusFromCards > 0f)
            {
                m_fCardSpeedScale = Mathf.Max(1f, m_fCardSpeedScale - m_fCardSpeedBonusFromCards);
                m_fCardSpeedBonusFromCards = 0f;
            }

            m_iDrawStartCount      = 0;
            m_bTrailElectrified    = false;
            m_fTrailElectrifyStun  = 0f;
            m_fFuseMulCardDelta    = 0f;
            m_fHitboxCardScale     = 1f;
            m_fLastCaptureRatio    = 0f;
            m_iFuseBombCharge      = 0;
            m_iExtinguisherCharge  = 0;
            m_fCardBurstSpeedBonus = 0f;
            m_fCardBurstSpeedTimer = 0f;
            m_fMomentumSpeedBonus  = 0f;
            m_fConditionalCardSpeedBonus = 0f;
        }

        /// <summary> K01_ELECTRIC_LINE 전용 — 효과 모듈이 부른다. </summary>
        public void Set_TrailElectrified(bool bOn, float fStunDuration)
        {
            m_bTrailElectrified   = bOn;
            m_fTrailElectrifyStun = fStunDuration;
        }

        /// <summary> K01_ELECTRIC_LINE 전용 — 선을 새로 긋기 시작할 때마다 1씩 늘려 돌려준다. </summary>
        public int Count_DrawStart() => ++m_iDrawStartCount;

        /// <summary> M01_SPRINTER/M03_CORNERING/M06_NEARMISS_MASTER — 짧게 터지는 가속. 여러 장이면 큰/긴 값으로 갱신한다. </summary>
        public void Add_CardBurstSpeed(float fBonus, float fDuration)
        {
            if (fBonus <= 0f || fDuration <= 0f)
                return;

            m_fCardBurstSpeedBonus = Mathf.Max(m_fCardBurstSpeedBonus, fBonus);
            m_fCardBurstSpeedTimer = Mathf.Max(m_fCardBurstSpeedTimer, fDuration);
        }

        /// <summary> M02_MOMENTUM 전용 — 카드 자신이 매 프레임 다시 계산해 넣는다(0.15 = +15%). 0이면 영향 없음. </summary>
        public void Set_MomentumSpeedBonus(float fBonus) => m_fMomentumSpeedBonus = Mathf.Max(0f, fBonus);

        /// <summary> F05_FUSE_BOMB — 충전을 더한다(레벨업마다 직전 레벨과의 차이만 넘길 것). </summary>
        public void Add_FuseBombCharge(int iAmount)
        {
            if (iAmount > 0) m_iFuseBombCharge += iAmount;
        }

        /// <summary> 도화선에 따라잡히기 직전 CStage_Manager가 부른다. 충전이 있으면 소모하고 true. </summary>
        public bool Try_ConsumeFuseBomb()
        {
            if (m_iFuseBombCharge <= 0)
                return false;

            --m_iFuseBombCharge;
            return true;
        }

        /// <summary> G01_EXTINGUISHER — 충전을 더한다(레벨업마다 직전 레벨과의 차이만 넘길 것). </summary>
        public void Add_ExtinguisherCharge(int iAmount)
        {
            if (iAmount > 0) m_iExtinguisherCharge += iAmount;
        }

        /// <summary> 도화선이 거의 다 탔을 때 CStage_Manager가 부른다. 충전이 있으면 소모하고 true. </summary>
        public bool Try_ConsumeExtinguisher()
        {
            if (m_iExtinguisherCharge <= 0)
                return false;

            --m_iExtinguisherCharge;
            return true;
        }

        /// <summary> G01_EXTINGUISHER 충전을 ON_CLOSE(점령)로 회복한다 — 카드 최대치까지. </summary>
        public void Refill_ExtinguisherCharge(int iMax)
        {
            if (iMax > 0)
                m_iExtinguisherCharge = iMax;
        }

        /// <summary>
        /// G05_STURDY/M07_SIZE_SHIFT(대형) — 최대 HP를 늘리고 그만큼 그 자리에서 채워 준다.
        /// 260928_MAX_LIFE_CAP(6)을 넘지 않는다 — 두 카드를 같이 만렙까지 찍으면(3+4=7) 넘칠 수 있어
        /// 늘어난 만큼만 실제로 적용한다(요청한 iAmount보다 적게 붙을 수 있다는 뜻).
        /// </summary>
        public void Add_CardMaxLife(int iAmount)
        {
            if (iAmount <= 0)
                return;

            int iApplied = Mathf.Min(iAmount, MAX_LIFE_CAP - m_iMaxLife);
            if (iApplied <= 0)
                return;

            m_iMaxLife += iApplied;
            m_iCardMaxLifeBonus += iApplied;
            m_iLife = Mathf.Min(m_iMaxLife, m_iLife + iApplied);
            OnLifeChanged?.Invoke(m_iLife);
        }

        /// <summary>
        /// 260929_M07 소형처럼 카드가 주는 이동속도 가산 — Add_CardSpeed와 같지만 스테이지마다 카드를 지울 때 이 몫만 되돌린다
        /// (영혼 수집가 같은 런 스킬이 Add_CardSpeed로 쌓은 몫은 그대로 둔다).
        /// </summary>
        public void Add_CatalogCardSpeed(float fRatio)
        {
            if (fRatio <= 0f)
                return;

            m_fCardSpeedBonusFromCards += fRatio;
            Add_CardSpeed(fRatio);
        }

        // 260918_몽둥이는 좌우로만 휘두른다 — 셀 크기를 아는 곳이 여기라 셀 단위 오프셋을 월드 좌표로 바꿔 준다.
        /// <param name="vOffsetCells"> 플레이어 기준 오프셋(셀) </param>
        public Vector2 Get_OffsetPoint(Vector2 vOffsetCells)
            => m_cGrid != null ? POS + vOffsetCells * m_cGrid.CELL_SIZE : POS;

        private void Tick_RunSkill(float fDeltaTime)
        {
            for (int i = 0; i < m_lstRunSkillEffect.Count; ++i)
                m_lstRunSkillEffect[i].Tick(fDeltaTime);
        }

        private void Clear_RunSkill()
        {
            for (int i = 0; i < m_lstRunSkillEffect.Count; ++i)
                m_lstRunSkillEffect[i].Release();

            m_lstRunSkillEffect.Clear();
            m_cRunSkillHandler.Clear();
            m_fPickupRadius = 0f;

            Set_MoveFlag_AllowOwnedInterior(false);
            Set_MoveFlag_EdgeWrap(false);
        }

        /// <summary> 월보 — 점령지 내부(이미지 위)도 지나갈 수 있게 한다. </summary>
        public void Set_MoveFlag_AllowOwnedInterior(bool bAllow) => m_cMoveHandler.Set_AllowOwnedInterior(bAllow);

        /// <summary> 어디로든 신발 — 맵 좌우 끝을 잇는다. </summary>
        public void Set_MoveFlag_EdgeWrap(bool bWrap) => m_cMoveHandler.Set_EdgeWrap(bWrap);

        /// <summary> 자석 — 습득 범위(셀). 0이면 스킬 없음. </summary>
        public void Set_PickupRadius(float fRadius) => m_fPickupRadius = Mathf.Max(0f, fRadius);

        private void Tick_SkillSpeed(float fDeltaTime)
        {
            if (m_fSkillSpeedTimer <= 0f)
                return;

            m_fSkillSpeedTimer -= fDeltaTime;
            if (m_fSkillSpeedTimer > 0f)
                return;

            m_fSkillSpeedScale = 1f;
            Apply_Speed();
        }

        private void Apply_Speed()
        {
            // 260928_외줄타기(DODGE_BOUNDARY_SPEED) — 선을 긋는 중이 아닐 때(=경계선 위)만 곱한다.
            float fBoundaryScale = (m_fBoundarySpeedBonus > 0f && m_cGrid.IS_DRAWING == false)
                ? 1f + m_fBoundarySpeedBonus : 1f;
            // 260928_도주 본능(DODGE_PANIC_SPEED) — 피격 직후 시한부로만 곱한다.
            float fPanicScale = m_fPanicSpeedTimer > 0f ? 1f + PANIC_SPEED_BONUS : 1f;

            // 260929_속도 = 기본 × (1 + B) × D × S (Docs/Design_Card_Balance_Spec.md §6.1, CBalance_Utility).
            //  B  카드로 얻은 증가분의 합 — 합산해서 +100%로 자른다(카드 기본 가산 · 짧은 가속 M01/M03/M06 · 직진 누적 M02 ·
            //     조건부 G07/F01 · 옛 카드의 외줄타기/도주 본능). 예전에는 전부 곱해서 겹쳐 상한이 없었다.
            //     M03(코너링)은 스펙에 속도 증가가 없어 원래 B 밖이지만, 지금 이동에 "감속"이 없어 대신 준 작은
            //     가속(+10%)을 B 안에 뒀다 — 상한에 걸리게 하는 쪽이 안전하다.
            //  D  거미줄(환경) × 피격 감속의 곱, 하한 0.30 — 슬로우는 상한과 무관하다.
            //  S  상태 배율(상한 없음). 스펙의 안전 귀환 0.8 · M04 1.5 · K08 1.2는 귀환 버튼이 없어 아직 들어갈 곳이
            //     없다 — 로비 스킬(질주)과 나선 가속(런 스킬)을 같은 "상한 밖 배율" 자리로 뒀다.
            float fBonusSum = (m_fCardSpeedScale - 1f)
                            + (m_fCardBurstSpeedTimer > 0f ? m_fCardBurstSpeedBonus : 0f)
                            + m_fMomentumSpeedBonus + m_fConditionalCardSpeedBonus
                            + (fBoundaryScale - 1f) + (fPanicScale - 1f);
            float fStateScale = m_fSkillSpeedScale * m_fTrailSpeedScale;

            m_cMoveHandler.SPEED = m_fBaseSpeed
                                 * CBalance_Utility.Calc_SpeedMultiplier(fBonusSum, m_fEnvSpeedScale * m_cImpact.SPEED_SCALE, fStateScale);
        }

        // 260921_선 긋기 시작 · 방향 전환을 알린다. 상태를 비교하는 자리를 한곳에 모아 둔다 —
        // 규칙(Step_To)과 무관한 '연출/스킬용 신호'라 판정 흐름에 끼워 넣지 않는다.
        private void Tick_MoveSignal()
        {
            bool bDrawing = m_cGrid.IS_DRAWING;
            if (bDrawing == true && m_bWasDrawing == false)
                OnDrawStart?.Invoke();

            m_bWasDrawing = bDrawing;

            MOVE_DIR eDir = m_cMoveHandler.CUR_DIR;
            if (eDir != MOVE_DIR.NONE && eDir != m_ePrevDir && m_ePrevDir != MOVE_DIR.NONE)
            {
                OnTurn?.Invoke();

                // 260928_스치는 그림자(DODGE_TURN_GRACE) — 쿨이 돌지 않았을 때만 짧은 무적을 준다.
                if (m_fTurnGraceCooldown > 0f && m_fTurnGraceTimer <= 0f)
                {
                    Add_Invincible(TURN_GRACE_INVINCIBLE_TIME);
                    m_fTurnGraceTimer = m_fTurnGraceCooldown;
                }
            }

            if (eDir != MOVE_DIR.NONE)
                m_ePrevDir = eDir;
        }

        // 260917_번쩍임(WHITE_OUT). 몸 색을 잠깐 밝힌다 — 무적 깜빡임은 알파만 쓰므로 서로 부딪히지 않는다.
        private void Refresh_WhiteOut()
        {
            if (m_srBody == null)
                return;

            if (m_bBodyColorSaved == false)
            {
                m_cBodyColor      = m_srBody.color;
                m_bBodyColorSaved = true;
            }

            Color cWant = m_cImpact.IS_WHITE_OUT == true ? Color.white : m_cBodyColor;
            cWant.a = m_srBody.color.a;
            m_srBody.color = cWant;
        }

        // 260904_웨이브가 넘어가면 판을 새로 깔기 때문에 플레이어도 새 시작 칸으로 옮겨야 한다.
        // 목숨과 무적 상태는 웨이브를 넘어가도 이어진다.
        /// <summary> 지정한 칸으로 옮기고 이동/입력 상태를 리셋한다. </summary>
        /// <param name="vPos"> 그리드 공간의 자리. 가장 가까운 경계 위로 붙인다 </param>
        public void Respawn(Vector2 vPos)
        {
            if (m_cGrid == null)
                return;

            m_cMoveHandler.Teleport(vPos);
            m_cMoveHandler.Snap_ToBoundary();
            m_vLastSafePos = m_cMoveHandler.POS;
            m_cInputHandler.Clear();
            transform.position = m_cMoveHandler.WORLD_POS;
        }

        // 260904_이미 죽었거나 풀에 반납된 뒤의 호출을 막는다.
        // 같은 프레임에 여러 몬스터가 겹치거나 스테이지가 끝난 뒤에도 판정이 한 번 더 들어올 수 있어,
        // HP가 음수로 내려가거나 m_cGrid가 null인 채로 Clear_Trail을 부를 여지가 있었다.
        // 260923_목숨제(260918)에서 다시 HP 풀로 돌렸다 — 몬스터 · 탄마다 공격력이 달라야
        // 보스급이 더 아프게 때릴 수 있고, 보호막 · 강화로 "몇 번은 버틴다"는 손맛이 산다.
        /// <summary> 몬스터 · 탄에 맞거나 자기 선을 밟았을 때. 보호막 · 회피가 막지 못하면 iAmount만큼 HP를 잃고 안전 칸에서 다시 시작한다. </summary>
        /// <param name="iAmount"> 깎을 HP. 몬스터는 EnemyInfo.iAttack, 탄은 ProjectileInfo.iDamage, 자기 선 밟기는 SELF_TRAIL_DAMAGE(즉사) </param>
        /// <param name="bLineCut"> 260922_그리던 선이 끊겼다(몬스터가 선에 닿음 · 자기 선 밟기). 회피로 흘리지 못한다 —
        /// 선이 끊기면 죽는 것이 이 장르의 규칙이고, 회피가 높으면 몬스터가 선을 지나가도 멀쩡해 규칙이 사라진 것처럼 보였다.
        /// 보호막 · 무적은 그대로 막는다(유령 걸음 · 잔상처럼 '통과'를 약속한 효과라서) </param>
        public void Damage(int iAmount, bool bLineCut = false)
        {
            if (m_cGrid == null || m_iLife <= 0 || IS_INVINCIBLE == true || iAmount <= 0)
                return;

            // 260905_보호막이 있으면 확정으로 한 번 막는다. 확률인 회피보다 먼저 쓴다 —
            // 회피가 먼저 터지면 아껴 둔 보호막이 그대로 남아 손해처럼 느껴진다.
            // 260923_충전이 여러 개면 하나만 소모한다 — 피해량과 무관하게 그 한 번을 통째로 막는다.
            if (m_iShield > 0)
            {
                --m_iShield;
                m_fInvincibleTimer = EVADE_GRACE_TIME;
                OnEvade?.Invoke();
                return;
            }


            // 260928_잔영(DODGE_FREE_HIT) — 몸 충돌 피해를 1회권으로 완전히 무효화한다(선 끊김 제외).
            // 확률이 아니라 확정 자원이라 회피·요행보다 먼저 쓴다 — 아껴 둔 보호막이 손해로 느껴지는 것과 같은 이유(위 260905 참고).
            if (bLineCut == false && m_iFreeHitCharge > 0)
            {
                --m_iFreeHitCharge;
                m_fInvincibleTimer = EVADE_GRACE_TIME;
                OnEvade?.Invoke();
                return;
            }

            // 260928_요행(DODGE_LUCKY_CHAIN) — 직전 회피가 예약해 둔 확정 회피.
            if (bLineCut == false && m_bLuckyChainArmed == true)
            {
                m_bLuckyChainArmed = false;
                m_fLuckyChainTimer = 0f;
                m_fInvincibleTimer = EVADE_GRACE_TIME;
                OnEvade?.Invoke();
                return;
            }

            // 260905_회피(능력치 강화 + 260928_아슬아슬한 본능의 시한부 보너스). 성공하면 짧은 무적을 함께 준다 —
            // 몬스터와 겹쳐 있는 동안 매 프레임 판정하면 확률이 아무리 높아도 결국 죽는다.
            float fEvasionNow = m_fEvasion + (m_fNearMissDodgeTimer > 0f ? NEARMISS_DODGE_BONUS : 0f);
            if (bLineCut == false && fEvasionNow > 0f && UnityEngine.Random.value < fEvasionNow)
            {
                m_fInvincibleTimer = EVADE_GRACE_TIME;
                OnEvade?.Invoke();

                // 260928_이번 회피가 성공했으니 요행(DODGE_LUCKY_CHAIN)이 있으면 다음 한 번을 예약한다.
                if (m_fLuckyChainDuration > 0f)
                {
                    m_bLuckyChainArmed = true;
                    m_fLuckyChainTimer = m_fLuckyChainDuration;
                }
                return;
            }

            m_cGrid.Clear_Trail();

            m_iLife = Mathf.Max(0, m_iLife - iAmount);
            OnDamaged?.Invoke();
            OnLifeChanged?.Invoke(m_iLife);

            if (m_iLife <= 0)
            {
                OnDead?.Invoke();
                return;
            }

            // 260921_나가 있는 사이 땅 갉는 자가 돌아갈 자리를 갉았으면 가장 가까운 경계에서 다시 시작한다
            m_cMoveHandler.Teleport(m_vLastSafePos);
            if (m_cGrid.Distance_ToBoundary(m_vLastSafePos) > SAFE_POS_DRIFT)
                m_cMoveHandler.Snap_ToBoundary();
            m_cInputHandler.Clear();
            // 260928_여유로운 몸놀림(DODGE_INVINCIBLE_UP) — 무적시간에 카드 가산을 더한다.
            m_fInvincibleTimer = INVINCIBLE_TIME + m_fInvincibleCardBonus;
            // 260928_도주 본능(DODGE_PANIC_SPEED) — 실제로 맞았을 때만 가속을 건다.
            if (m_fPanicSpeedDuration > 0f)
                m_fPanicSpeedTimer = m_fPanicSpeedDuration;
        }

        /// <summary>
        /// 260922_점멸 방향 — **지금 누르고 있는 방향**이 먼저다. 누르고 있지 않으면 마지막으로 움직인 방향.
        /// 이제 선을 긋다가도 손을 떼면 멈추므로(2-3), 가던 방향만 보면 멈춘 채로 원하는 쪽으로 튈 수 없다.
        /// 대각선 입력은 가로 · 세로 중 가던 방향 쪽을 쓴다(한 번에 비스듬히 건너뛰면 선이 끊긴다).
        /// 선을 긋는 중 뒤로 튀는 것은 막는다 — 자기 선을 밟아 죽는다.
        /// </summary>
        private MOVE_DIR Get_WarpDir()
        {
            MOVE_DIR eCur   = m_cMoveHandler.CUR_DIR;
            MOVE_DIR eInput = m_cInputHandler.DESIRED_DIR;

            if (CTerritoryGrid.Is_Diagonal(eInput) == true)
            {
                CTerritoryGrid.Dir_Split(eInput, out MOVE_DIR eHorizontal, out MOVE_DIR eVertical);
                eInput = eCur == eVertical ? eVertical : eHorizontal;
            }

            if (eInput == MOVE_DIR.NONE)
                return eCur;

            if (m_cGrid.IS_DRAWING == true && eInput == CTerritoryGrid.Dir_Reverse(eCur))
                return eCur;

            return eInput;
        }

        // 260905_액티브 스킬 — 워프
        /// <summary> 스킬 버튼이 눌렸을 때. 쿨타임이 남았거나 멈춰 있으면 아무 일도 없다. </summary>
        // 260912_무엇을 하는지는 CSkillEffect가 안다. 스킬이 늘어도 여기는 그대로다.
        // 효과가 실패하면 쿨타임을 돌리지 않는다 — 멈춘 채로 점멸을 눌러 쿨만 날리면 억울하다.
        // 260922_스킬 입력 버퍼 — 쿨이 끝나기 직전이나 칸 사이를 지나는 순간에 누른 것도 놓치지 않는다.
        // 모바일은 누른 느낌이 둔해서, 눌렀는데 아무 일도 없으면 버튼이 고장 난 것처럼 느껴진다
        private const float SKILL_BUFFER_TIME = 0.25f;
        private float m_fSkillBuffer;

        public void Request_Skill()
        {
            if (Try_UseSkill() == false)
                m_fSkillBuffer = SKILL_BUFFER_TIME;
        }

        private void Tick_SkillBuffer(float fDeltaTime)
        {
            if (m_fSkillBuffer <= 0f)
                return;

            m_fSkillBuffer -= fDeltaTime;
            if (Try_UseSkill() == true)
                m_fSkillBuffer = 0f;
        }

        public bool Try_UseSkill()
        {
            if (m_cGrid == null || m_iLife <= 0 || m_cSkillEffect == null)
                return false;

            if (m_cSkillHandler.IS_READY == false)
                return false;

            if (m_cSkillEffect.Try_Apply(m_cSkillHandler.VALUE, m_cSkillHandler.DURATION) == false)
                return false;

            m_cSkillHandler.Try_Use();
            return true;
        }

        // 260923_점멸 — 그 방향으로 한 번에 간다. 판정은 평소 이동과 같은 길(CMoveHandler → Step_To)을 지난다 —
        // 지나간 자리가 선으로 남아야 도형이 닫힌다. 내 땅 위라면 경계를 따라 미끄러지거나 그 방향으로 나가 선을 긋는다.
        public bool Warp(int iCellCount)
        {
            MOVE_DIR eDir = Get_WarpDir();
            if (eDir == MOVE_DIR.NONE || iCellCount <= 0)
                return false;   // 한 번도 움직인 적이 없으면 어디로 갈지 알 수 없다

            Vector2 vBefore = m_cMoveHandler.POS;
            STEP_RESULT eResult = m_cMoveHandler.Warp(eDir, iCellCount, out int iCapturedCount);
            bool bMoved = (m_cMoveHandler.POS - vBefore).sqrMagnitude > 1e-6f;

            Handle_Step(eResult, iCapturedCount);
            transform.position = m_cMoveHandler.WORLD_POS;
            return bMoved;
        }

        // 260912_마감 — 그은 선을 가장 가까운 점령지까지 이어 붙여 도형을 닫는다.
        // 260923_ㄱ자가 아니라 곧게 잇는다(땅이 다각형이라). 판정은 평소 이동과 같은 Step_To를 지난다.
        public bool Seal()
        {
            if (m_cGrid == null || m_cGrid.IS_DRAWING == false)
                return false;   // 선을 긋고 있지 않으면 마감할 것이 없다

            if (m_cGrid.Try_Find_NearestBoundary(m_cMoveHandler.POS, out Vector2 vTarget) == false)
                return false;

            STEP_RESULT eResult = m_cMoveHandler.Draw_To(vTarget, out int iCapturedCount);
            Handle_Step(eResult, iCapturedCount);
            transform.position = m_cMoveHandler.WORLD_POS;
            return true;
        }

        // 260923_쾌속 돌진 — 점멸과 같은 길(Warp → Step_To)을 타므로 맵 끝 · 내 점령지에서 서는 것은
        // 그 경로가 이미 한다(2-3). 여기서 새로 하는 일은 몬스터에 닿기 전에 멈추도록 거리를 줄이는 것뿐이다 —
        // 몬스터는 CPlayer가 모르는 대상이라 스테이지(ISkillHost)에 거리를 물어본다.
        public bool Rush(float fMaxCellDist)
        {
            MOVE_DIR eDir = Get_WarpDir();
            if (eDir == MOVE_DIR.NONE || fMaxCellDist <= 0f || m_cGrid == null)
                return false;

            float fMaxWorldDist = fMaxCellDist * m_cGrid.CELL_SIZE;
            if (m_cSkillHost != null)
            {
                Vector2 vDirWorld = CTerritoryGrid.Dir_ToVector(eDir).normalized;
                fMaxWorldDist = m_cSkillHost.Get_RushDistance(POS, vDirWorld, fMaxWorldDist);
            }

            float fCellDist = fMaxWorldDist / m_cGrid.CELL_SIZE;
            if (fCellDist <= 0f)
                return false;

            Vector2 vBefore = m_cMoveHandler.POS;
            STEP_RESULT eResult = m_cMoveHandler.Warp(eDir, fCellDist, out int iCapturedCount);
            bool bMoved = (m_cMoveHandler.POS - vBefore).sqrMagnitude > 1e-6f;

            Handle_Step(eResult, iCapturedCount);
            transform.position = m_cMoveHandler.WORLD_POS;
            return bMoved;
        }


        // 260905_소모품 효과
        /// <summary> 보호막을 얻는다. 이미 있으면 그대로 둔다(중첩하지 않는다). </summary>
        /// <summary> 260923_충전 하나를 더한다. 여러 개면 그만큼 더 버틴다(SHIELD_MAX까지 쌓인다). </summary>
        public void Add_Shield() => m_iShield = Mathf.Min(SHIELD_MAX, m_iShield + 1);

        /// <summary> 목숨을 되찾는다. 최대 목숨을 넘기지 않는다. </summary>
        public void Add_Life(int iAmount)
        {
            if (iAmount <= 0 || m_iLife <= 0)
                return;

            m_iLife = Mathf.Min(m_iMaxLife, m_iLife + iAmount);
            OnLifeChanged?.Invoke(m_iLife);
        }

        #endregion 규칙 판정

        private void Refresh_InvincibleBlink()
        {
            if (m_srBody == null)
                return;

            Color cColor = m_srBody.color;
            cColor.a = m_fInvincibleTimer > 0f && Mathf.Repeat(m_fInvincibleTimer, 0.2f) < 0.1f ? 0.25f : 1f;
            m_srBody.color = cColor;
        }
    }
}
