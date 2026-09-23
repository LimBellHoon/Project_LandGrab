using System;
using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 스테이지 진행/클리어 판정
    // 260904_MapInfo.csv / EnemyInfo.csv 기반 + 웨이브 진행
    /// <summary>
    /// 그리드 · 렌더러 · 플레이어 · 몬스터를 소유하고 웨이브 규칙을 판정한다.
    /// 규칙 값은 전부 CSV에서 온다 — 이 클래스에 숫자를 다시 적지 말 것.
    ///
    /// 웨이브: N웨이브를 fClearRatio만큼 점령하면 그리드를 다시 깔고 이미지 스택을 한 장 벗긴다.
    /// 마지막 웨이브까지 넘기면 CLEAR.
    /// </summary>
    public class CStage_Manager : IGimmickHost, ISkillHost, IRunSkillHost, IProjectileHost
    {
        private const string PREFAB_PLAYER      = "Prefab_Player";
        private const string PREFAB_PROJECTILE  = "Prefab_Projectile";
        private const string PREFAB_WEB         = "Prefab_Web";
        private const string PREFAB_SOUL        = "Prefab_Soul";
        private const string PREFAB_FIELD_ITEM  = "Prefab_FieldItem";

        private const string PREFAB_DECOY       = "Prefab_Decoy";
        private const int    SPAWN_SEARCH_RADIUS = 24;  // 스폰 자리가 막혔을 때 대신 찾아볼 반경(셀)

        // 260920_필드 아이템은 영혼과 같은 반경으로 줍는다(자석 보너스도 그대로 탄다).
        // 사고 방지용 상한 — 아이템이 판을 뒤덮지 않게 한다. 규칙 값이 아니라 안전장치라 MAX_ENEMY와 같은 자리다.
        private const int    MAX_FIELD_ITEM     = 8;

        // 260916_런 스킬 '영혼 수집가'. 자석(CPlayer.PICKUP_RADIUS)이 없어도 이 정도는 기본으로 줍는다 —
        // 몬스터/탄 충돌 반경(PROJECTILE_HIT_RANGE)과 같은 자리라 CSV로 뺄 이유가 아직 없다.
        private const float  SOUL_PICKUP_RADIUS_BASE = 0.6f;   // 셀
        private const float  SOUL_LIFETIME           = 12f;    // 초. 안 주우면 사라진다

        // 260904_소환 기믹의 안전장치. RefID가 다시 SPAWN 몬스터를 가리키면 끝없이 늘어난다.
        // 규칙 값이 아니라 사고 방지용 상한이라 CSV로 빼지 않는다.
        private const int    MAX_ENEMY          = 32;

        // 260923_플레이어와 부딪힌 몬스터가 튕겨나는 거리 · 시간 — 연출 값이라 CSV로 빼지 않는다(REVEAL_TIME과 같은 자리).
        private const float  PLAYER_HIT_KNOCKBACK_CELL     = 1.5f;
        private const float  PLAYER_HIT_KNOCKBACK_DURATION = 0.2f;

        // 260924_도화선(2-3) — 몬스터가 몸으로 부딪히면 위처럼 즉시 맞지만, "선"에 닿으면 그 자리에서
        // 불이 붙어 트레일 끝(플레이어)을 향해 타들어온다. 불보다 먼저 안전 지대로 돌아오면 선만 잃고
        // 끝나지만(점령은 안 된다, CTerritoryGrid.Step_To 참고), 따라잡히면 몸에 닿은 것과 같은 값이 깎인다.
        // "선을 긋는 동안은 약하다"는 그대로 두되, 즉사 대신 반응할 시간을 준다.
        private const float  FUSE_SPEED_CELL_PER_SEC = 3f;   // 초당 몇 칸을 태우는가 — 연출 값이라 CSV로 빼지 않는다

        private bool          m_bFuseArmed;       // 지금 도화선이 타고 있는가
        // 260923_선이 칸이 아니라 선분이 되면서 '몇 번째 칸' 대신 '선 시작점부터 잰 길이'로 불의 자리를 든다
        private float         m_fFuseFrom;        // 불이 붙은 자리(고정)
        private float         m_fFuseFront;       // 불이 지금 닿은 자리
        private int           m_iFuseDamage;      // 발화시킨 몬스터의 공격력 — 따라잡히는 순간 그대로 쓴다
        // 260924_불이 붙은 자리 — 스파크 연출을 붙일 자리가 필요해지면 CGameManager가 듣는다(UI는 로컬에서, 3-1).
        public event Action<Vector2Int> OnFuseIgnited;

        // 260924_3지선다 트리거 — 점유율이 이 지점을 하나 넘을 때마다 연다(2-21). 웨이브 목표 70%를
        // 일곱 번으로 고르게 나눴다(균등 5% 간격이던 260923 값을 대신한다) — 뒤로 갈수록 다음 카드까지
        // 더 많이 먹어야 하지만, 처음 몇 장은 빨리 나와 손맛을 준다. 규칙 값이지만 맵마다 다르게 줄
        // 이유가 아직 없어 CSV로 빼지 않았다 — 필요해지면 MapInfo.csv 열로 옮길 것.
        private static readonly float[] s_arrCardThreshold =
            { 0.05f, 0.12f, 0.20f, 0.30f, 0.42f, 0.56f, 0.65f };

        // 260904_보상 공개 연출 길이(초). 규칙 값이 아니라 연출 타이밍이라 코드에 둔다.
        private const float  REVEAL_TIME = 0.5f;     // 가림막이 걷히는 시간
        private const float  HOLD_TIME   = 0.9f;     // 드러난 보상을 보여주는 시간
        private const float  COVER_TIME  = 0.5f;     // 다음 가림막이 덮이는 시간

        /// <summary> 웨이브를 넘길 때의 연출 단계. NONE이면 평소대로 게임이 돌아간다. </summary>
        // 260921_START_SELECT — 웨이브를 시작하기 전에 시작 섬 자리를 슬롯처럼 돌려 고른다(2-3)
        private enum WAVE_PHASE { NONE, REVEAL, HOLD, COVER, START_SELECT }

        // 260921_시작 위치 슬롯. 연출 타이밍이라 코드에 둔다(REVEAL_TIME과 같은 자리)
        private const int    SLOT_CANDIDATE     = 10;       // 돌려 볼 후보 수
        private const float  SLOT_INTERVAL      = 0.08f;    // 도는 동안 한 칸 넘기는 간격
        private const int    SLOT_STOP_HOPS     = 5;        // 누른 뒤 몇 칸 더 넘기며 멈추는가
        private const float  SLOT_SLOWDOWN      = 1.55f;    // 멈추는 동안 간격이 이만큼씩 늘어난다
        private const float  SLOT_SETTLE_TIME   = 0.45f;    // 멈춘 자리를 보여 주고 시작하기까지
        private const int    SLOT_ENEMY_GAP     = 4;        // 섬 가장자리에서 몬스터까지 최소 칸

        private readonly List<Vector2Int>   m_lstStartCandidate = new List<Vector2Int>();
        private int                         m_iSlotIndex;
        private float                       m_fSlotTimer;
        private float                       m_fSlotInterval;
        private int                         m_iSlotHopsLeft;    // -1이면 누르기를 기다리며 돈다
        private bool                        m_bSlotSettling;

        private readonly CTerritoryGrid m_cGrid         = new CTerritoryGrid();
        private readonly CGridRenderer  m_cGridRenderer = new CGridRenderer();

        private readonly List<CEnemy>       m_lstEnemy      = new List<CEnemy>();

        // 260912_감속 스킬. 몬스터는 스테이지가 들고 있으므로 지속 시간도 여기서 잰다.
        // 도중에 새로 소환되는 몬스터에게도 같은 배율을 걸어야 해서 값을 남겨 둔다.
        private float                       m_fEnemySlowScale = 1f;
        private float                       m_fEnemySlowTimer;
        // 260912_카드로 얹은 몬스터 감속. 판이 끝날 때까지 유지되므로 타이머가 없다.
        private float                       m_fEnemyCardSlow = 1f;

        // 260923_사냥형 카드(HUNT_*, Docs/Design_Card_Pool.md 1장) 중 몬스터 쪽 수치. 플레이어 쪽 수치는
        // CPlayer가 들고 있다(THORN_DAMAGE 등) — 여기 있는 것은 '몸 충돌 이후'가 아니라 '몬스터를 직접
        // 건드리는' 효과라 스테이지가 들고 있어야 한다(1-1, 그리드는 칸만 알고 스테이지가 몬스터를 본다).
        private float                       m_fLootCardBonus;      // 노획 본능 — 필드 아이템 드롭 확률 가산
        private float                       m_fExecuteCardRadius;  // 처형자 — 가둬 잡기 스플래시 반경(칸). 0이면 없음
        private const int                   EXECUTE_SPLASH_DAMAGE = 1;
        private const float                 HUNT_MARK_SLOW_RATIO  = 0.5f;   // 사냥감 표식 — 감속 배율(고정값)
        // 같은 출처(이 인스턴스)로 다시 걸면 CImpactHandler가 새로 걸지 않고 긴 쪽으로 늘린다 — fTime만 카드가 갱신한다.
        private readonly CImpactInfo        m_cHuntMarkImpact = new CImpactInfo { eType = IMPACT_TYPE.SLOW, fValue = HUNT_MARK_SLOW_RATIO };
        private readonly List<Vector2>      m_lstExecuteKillPos = new List<Vector2>();

        // 260923_3지선다 트리거 — 다시 점유율 기준이다(2-21 조각 게이지를 되돌림). 점유율이
        // s_arrCardThreshold의 지점을 하나 더 넘을 때마다 하나씩 연다. 웨이브가 넘어가 점유율이
        // 0으로 돌아가면 이 값도 같이 리셋된다.
        private int                         m_iRatioStep;
        private readonly CPickQueue         m_cPickQueue = new CPickQueue();   // 260921_한 번에 한 장만
        // 260922_현란한 동작 판정 — 알아보고 알려 주기만 한다(OnStylish)
        private readonly CStyleTracker      m_cStyle = new CStyleTracker();
        public event Action<STYLE_ACTION, int> OnStylish;
        // 260921_다시 뽑기 · 버리기(2-10-1). 판 전체에서 쓰는 횟수이고, 버린 것은 판이 끝날 때까지 안 나온다
        private int                         m_iRerollLeft;
        private int                         m_iBanishLeft;
        private readonly HashSet<string>    m_hsBanished = new HashSet<string>();

        // 260918_점령 재화 — 안전하게 조금씩과 위험을 감수하고 크게 한 방 사이에 실제 이득 차이를 만든다.
        // 이번 판 누적만 여기서 들고, 실제 보유 코인 반영(디스크 저장)은 스테이지가 끝날 때 CGameManager가 한 번만 한다.
        private CCSVData_CaptureRewardInfo  m_cCaptureRewardTable;
        private int                         m_iStageCoin;

        // 260904_기믹이 소환한 것들. 수명과 충돌을 여기서 한꺼번에 본다.
        private readonly List<CProjectile>  m_lstProjectile = new List<CProjectile>();
        private readonly List<CWeb>         m_lstWeb        = new List<CWeb>();
        // 260916_런 스킬이 떨어뜨린 것들. 위 두 목록과 같은 자리다.
        private readonly List<CSoul>        m_lstSoul       = new List<CSoul>();
        // 260920_맵 위 상호작용 아이템. 표가 없으면 아무것도 안 나올 뿐 판은 그대로 돈다.
        private readonly List<CFieldItem>   m_lstFieldItem  = new List<CFieldItem>();
        private CCSVData_FieldItemInfo      m_cFieldItemTable;
        // 260920_캐릭터별 이동 방식(2-22). 개발 스위치가 켜져 있으면 캐릭터와 무관하게 그 방식으로 덮어쓴다.
        private MOVE_STYLE                  m_eCharMoveStyle = MOVE_STYLE.FOUR_WAY;
        private MOVE_STYLE?                 m_eDevMoveStyle;

        /// <summary> 이번 판에 실제로 쓰는 이동 방식. </summary>
        public MOVE_STYLE MOVE_STYLE_NOW => m_eDevMoveStyle ?? m_eCharMoveStyle;

        /// <summary> 260920_개발용 — 캐릭터를 바꾸지 않고 이동 방식만 바꿔 본다(1-6). null이면 캐릭터를 따른다. </summary>
        public void Set_DevMoveStyle(MOVE_STYLE? eStyle) => m_eDevMoveStyle = eStyle;
        // 260921_분신(2-11-3). 한 번에 하나만 둔다 — 여러 개면 어그로가 흩어져 무엇을 노리는지 안 읽힌다.
        private CDecoy                      m_cDecoy;
        private bool                        m_bFieldItemEnabled = true;     // 개발용 스위치(CGameConfig)
        private float                       m_fFieldItemTimer;              // 시간마다 하나씩 떨어뜨리는 타이머
        // 260917_탄 표. 없으면 탄을 쏘지 않을 뿐 판은 돈다.
        private CCSVData_ProjectileInfo     m_cProjectileTable;
        private CCSVData_ImpactInfo         m_cImpactTable;
        // 탄 ID → 효과 목록. 쏠 때마다 표를 뒤지지 않게 처음 한 번만 찾아 둔다.
        private readonly Dictionary<int, List<CImpactInfo>> m_dicImpactCache = new Dictionary<int, List<CImpactInfo>>();
        // 탄이 맞힐 수 있는 대상 목록. 매 프레임 새로 만들지 않고 비웠다 채운다.
        private readonly List<IImpactTarget> m_lstEnemyTarget  = new List<IImpactTarget>();
        private readonly List<IImpactTarget> m_lstPlayerTarget = new List<IImpactTarget>();

        // 260917_개발용 스위치(CGameConfig). 0이면 꺼져 있다.
        private int                         m_iDevAutoFireID;       // 플레이어가 저절로 쏘는 탄
        private float                       m_fDevAutoFireCool;
        private float                       m_fDevAutoFireTimer;
        private int                         m_iDevEnemyShotID;      // 포수가 표 대신 쏘는 탄


        private CMapInfo            m_cMapInfo;
        private CCSVData_EnemyInfo  m_cEnemyTable;
        private CPlayer             m_cPlayer;
        private STAGE_STATE         m_eState = STAGE_STATE.READY;
        private int                 m_iWave;
        private int                 m_iStar;    // 260905_이번 판에서 완료한 웨이브 수
        private float               m_fRemainTime;
        private bool                m_bPlayerExposed;   // 기믹 발동 조건 — 매 프레임 Tick_Enemy가 갱신한다
        private bool                m_bPaused;

        // 260904_웨이브 전환 연출
        private WAVE_PHASE          m_eWavePhase;
        private float               m_fPhaseTimer;
        private int                 m_iNextWave;        // 0이면 이번이 마지막 웨이브였다는 뜻

        // 260904_클리어/실패를 밖(CGameManager)이 알아야 진행도를 저장하고 선택 화면으로 돌아갈 수 있다.
        public event Action<STAGE_STATE> OnStateChanged;

        // 260912_점령률이 카드 지급 지점을 넘었다. 화면을 띄우는 것은 CGameManager가 한다 —
        // 스테이지가 UI를 직접 열면 화면 전환이 두 군데로 갈라진다(2-7).
        public event Action OnCardReady;

        // 260918_전체 마비가 터졌다 — 흔들림 · 펀치 · 플래시 같은 화면 연출은 CGameManager가 한다(2-10-2).
        public event Action OnMassStun;

        // 260918_점령 재화를 얻었다 — 이번 점령으로 번 코인과, 파티클 연출 기준점으로 쓸 위치.
        public event Action<int, Vector2> OnCoinGained;

        // 260920_필드 아이템을 주웠다 — 화면 연출(소리 · 진동)을 CGameManager가 낸다.
        public event Action<FIELD_ITEM_TYPE> OnFieldItemUsed;

        public bool             IS_PAUSED       => m_bPaused;
        public int              MAP_ID          => m_cMapInfo != null ? m_cMapInfo.iMapID : 0;
        public CTerritoryGrid   GRID            => m_cGrid;
        public CPlayer          PLAYER          => m_cPlayer;
        public STAGE_STATE      STATE           => m_eState;
        public float            REMAIN_TIME     => m_fRemainTime;
        public float            OWNED_RATIO     => m_cGrid.OWNED_RATIO;
        // 260920_카메라가 선 길이만큼 물러날 때 본다(2-10)
        /// <summary> 260923_지금 긋는 선의 길이(칸) — 카메라가 물러나는 정도를 정한다(2-10) </summary>
        public float            TRAIL_LENGTH    => m_cGrid.TRAIL_LENGTH;
        public int              LIFE            => m_cPlayer != null ? m_cPlayer.LIFE : 0;
        public int              MAX_LIFE        => m_cPlayer != null ? m_cPlayer.MAX_LIFE : 0;
        public int              ENEMY_COUNT     => m_lstEnemy.Count;
        public int              WAVE            => m_iWave;
        // 260905_별 = 이번 판에서 완료한 웨이브 수. 도중에 죽거나 시간이 끝나도 여기까지는 남는다.
        public int              STAR            => m_iStar;
        // 260921_계정 경험치를 나눌 진행도 0~1 — 달성한 웨이브 + 지금 웨이브의 점령률(2-23)
        public float            PROGRESS        => m_eState == STAGE_STATE.CLEAR ? 1f
                                                 : CAccount_Utility.Calc_StageProgress(m_iStar, WAVE_COUNT,
                                                                                        OWNED_RATIO, CLEAR_RATIO);
        // 260918_이번 판에서 점령으로 번 코인 누적. 클리어/실패와 무관하게 CGameManager가 종료 시 지급한다.
        public int              STAGE_COIN      => m_iStageCoin;

        // 260905_능력치 강화 반영. Start_Stage 전에 넣어 둔다.
        private float           m_fSpeedRate = 1f;      // 이동 속도 배율
        private float           m_fEvasion;             // 피격 회피 확률 0~1
        private int             m_iBonusLife;           // 260918_강화 · 장비로 늘어난 목숨
        private CSkillInfo      m_cSkillInfo;           // 260905_장착한 액티브 스킬
        private int             m_iSkillLevel;          // 260905_스킬 강화 레벨

        // 260917_장착 캐릭터(스킨 + 스탯 배율만 다르다. 전용 스킬 없음 — 노션 "캐릭터 시스템" 카드 4장).
        private string          m_strCharacterPrefab;   // 비어 있으면 기본 PREFAB_PLAYER
        private float           m_fCharSpeedRate   = 1f;
        private float           m_fCharHpRate      = 1f;
        private float           m_fCharEvasionBonus;

        /// <param name="fSpeedRate"> 이동 속도에 곱할 값 (1 = 강화 없음) </param>
        /// <param name="fEvasion"> 피격을 무시할 확률 0~1 </param>
        /// <param name="iBonusLife"> 맵 기본 목숨에 더할 수 </param>
        public void Set_PlayerUpgrade(float fSpeedRate, float fEvasion, int iBonusLife)
        {
            m_fSpeedRate = Mathf.Max(0.1f, fSpeedRate);
            m_fEvasion   = Mathf.Clamp01(fEvasion);
            m_iBonusLife = Mathf.Max(0, iBonusLife);
        }

        // 260917_가방에서 장착한 캐릭터. Start_Stage 전에 넣어 둔다.
        /// <param name="cInfo"> 없으면(null) 기본 스킨·배율 1로 되돌아간다 </param>
        public void Set_Character(CCharacterInfo cInfo, int iLevel)
        {
            m_strCharacterPrefab = cInfo != null ? cInfo.strPrefabName : null;
            m_fCharSpeedRate     = cInfo != null ? cInfo.Get_SpeedRate(iLevel)    : 1f;
            m_fCharHpRate        = cInfo != null ? cInfo.Get_MaxHpRate(iLevel)    : 1f;
            m_fCharEvasionBonus  = cInfo != null ? cInfo.Get_EvasionBonus(iLevel) : 0f;
            // 260920_이동 방식은 레벨과 무관한 그 캐릭터의 성질이다(2-22)
            m_eCharMoveStyle     = cInfo != null ? cInfo.eMoveStyle : MOVE_STYLE.FOUR_WAY;
        }

        // 260905_장착 시스템이 생기기 전까지는 CGameManager가 표에서 골라 넣어 준다.
        public void Set_PlayerSkill(CSkillInfo cSkillInfo, int iSkillLevel)
        {
            m_cSkillInfo = cSkillInfo;
            m_iSkillLevel = Mathf.Max(0, iSkillLevel);
        }

        // 260917_탄 표와 개발용 스위치. Start_Stage 전에 넣어 둔다.
        /// <param name="iDevAutoFireID"> 0이 아니면 플레이어가 그 탄을 가장 가까운 몬스터에게 저절로 쏜다 </param>
        /// <param name="iDevEnemyShotID"> 0이 아니면 포수가 표에 적힌 탄 대신 이 탄을 쏜다 </param>
        public void Set_ProjectileSetting(CCSVData_ProjectileInfo cProjectileTable, CCSVData_ImpactInfo cImpactTable,
                                          int iDevAutoFireID, float fDevAutoFireCool, int iDevEnemyShotID)
        {
            m_cProjectileTable = cProjectileTable;
            m_cImpactTable     = cImpactTable;
            m_dicImpactCache.Clear();

            m_iDevAutoFireID    = Mathf.Max(0, iDevAutoFireID);
            m_fDevAutoFireCool  = Mathf.Max(0.05f, fDevAutoFireCool);
            m_fDevAutoFireTimer = m_fDevAutoFireCool;
            m_iDevEnemyShotID   = Mathf.Max(0, iDevEnemyShotID);
        }

        // 260918_점령 재화 배율 표. 없으면 배율 없이(1배) 지급한다. Start_Stage 전에 넣어 둔다.
        public void Set_CaptureRewardTable(CCSVData_CaptureRewardInfo cTable)
        {
            m_cCaptureRewardTable = cTable;
        }

        public int              WAVE_COUNT      => m_cMapInfo != null ? m_cMapInfo.iWaveCount : 0;
        public string           MAP_NAME        => m_cMapInfo != null ? m_cMapInfo.strMapName : string.Empty;

        public float CLEAR_RATIO
        {
            get
            {
                CWaveInfo cWave = m_cMapInfo != null ? m_cMapInfo.Get_Wave(m_iWave) : null;
                return cWave != null ? cWave.fClearRatio : 0f;
            }
        }

        #region 초기화
        /// <param name="srCover"> 가림막을 그릴 SpriteRenderer (그리드 마스크) </param>
        /// <param name="srReveal"> 점령하면 드러날 보상 이미지를 깔 SpriteRenderer </param>
        public bool Initialize(CMapInfo cMapInfo, CCSVData_EnemyInfo cEnemyTable,
                               SpriteRenderer srCover, SpriteRenderer srReveal)
        {
            if (cMapInfo == null)
            {
                Debug.LogError("[CStage_Manager] CMapInfo가 null 입니다. MapInfo.csv를 확인하세요.");
                return false;
            }

            if (cMapInfo.bIsValid == false)
                Debug.LogWarning($"[CStage_Manager] 맵 {cMapInfo.iMapID}의 표가 어긋나 있습니다. "
                               + "웨이브 수와 이미지 스택 장수를 확인하세요.");

            m_cMapInfo    = cMapInfo;
            m_cEnemyTable = cEnemyTable;

            // 그리드를 월드 원점 기준으로 가운데 정렬한다.
            Vector2 vWorldSize = new Vector2(cMapInfo.iGridWidth * cMapInfo.fCellSize,
                                             cMapInfo.iGridHeight * cMapInfo.fCellSize);
            Vector2 vOrigin = -vWorldSize * 0.5f;

            if (m_cGrid.Initialize(cMapInfo.iGridWidth, cMapInfo.iGridHeight, cMapInfo.fCellSize,
                                   vOrigin, cMapInfo.iBorderThick, Load_ShapeMask(cMapInfo),
                                   cMapInfo.iStartRadius) == false)
                return false;

            return m_cGridRenderer.Initialize(m_cGrid, srCover, srReveal);
        }

        public void Release()
        {
            Set_ActorTimeScale(1f);

            OnStateChanged = null;
            m_bPaused      = false;
            m_eWavePhase   = WAVE_PHASE.NONE;
            m_fEnemySlowScale = 1f;     // 260912_다음 판에 감속이 남아 있지 않게
            m_fEnemySlowTimer = 0f;
            m_fEnemyCardSlow  = 1f;
            m_fLootCardBonus  = 0f;
            m_fExecuteCardRadius = 0f;
            m_cHuntMarkImpact.fTime = 0f;
            m_iRatioStep      = 0;
            m_cPickQueue.Clear();
            OnCardReady       = null;
            OnMassStun        = null;
            OnStylish         = null;     // 260922_현란한 동작
            m_cStyle.Clear();
            OnStartSlotHop    = null;     // 260921_시작 위치 슬롯
            OnStartSelected   = null;
            m_iStageCoin      = 0;      // 260918_다음 판으로 넘어가지 않게
            OnCoinGained      = null;
            OnFieldItemUsed   = null;
            m_fFieldItemTimer = 0f;
            m_bFuseArmed      = false;  // 260924_도화선
            OnFuseIgnited     = null;

            Collect_Player();
            Collect_Enemies();
            m_cGridRenderer.Release();
            m_eState = STAGE_STATE.READY;
        }

        // 260904_스테이지를 다시 고를 수 있으므로 플레이어도 풀에 돌려줘야 한다.
        private void Collect_Player()
        {
            if (m_cPlayer != null)
                CGameInstance.Instance.Collect_Object(m_cPlayer);

            m_cPlayer = null;
        }

        // 260904_맵 모양 마스크 — 밝은 픽셀이 플레이 가능한 칸이다.
        /// <summary> strShapeMask 텍스처를 셀 격자로 샘플링해 '플레이 가능' 배열로 만든다. 없으면 null. </summary>
        private static bool[] Load_ShapeMask(CMapInfo cMapInfo)
        {
            if (string.IsNullOrEmpty(cMapInfo.strShapeMask) == true)
                return null;

            Texture2D texShape = Get_Texture(cMapInfo.strShapeMask);
            if (texShape == null)
                return null;

            if (texShape.isReadable == false)
            {
                Debug.LogError($"[CStage_Manager] 모양 마스크 '{cMapInfo.strShapeMask}'는 Read/Write가 꺼져 있습니다. "
                             + "텍스처 임포트 설정에서 Read/Write Enabled를 켜세요.");
                return null;
            }

            Color32[] arrSrc = texShape.GetPixels32();
            bool[] arrPlayable = new bool[cMapInfo.iGridWidth * cMapInfo.iGridHeight];

            for (int y = 0; y < cMapInfo.iGridHeight; ++y)
            {
                int sy = y * texShape.height / cMapInfo.iGridHeight;

                for (int x = 0; x < cMapInfo.iGridWidth; ++x)
                {
                    int sx = x * texShape.width / cMapInfo.iGridWidth;
                    Color32 cColor = arrSrc[sy * texShape.width + sx];

                    // 밝기 절반을 기준으로 자른다. 알파가 0인 칸도 맵 밖으로 본다.
                    int iLuma = (cColor.r + cColor.g + cColor.b) / 3;
                    arrPlayable[y * cMapInfo.iGridWidth + x] = cColor.a > 127 && iLuma > 127;
                }
            }

            return arrPlayable;
        }

        private static Texture2D Get_Texture(string strName)
        {
            // 이미지 스택의 칸이 비어 있을 수 있다 — 그건 잘못이 아니라 '가림막 없음'이다.
            if (string.IsNullOrEmpty(strName) == true)
                return null;

            Texture texture = CGameInstance.Instance.Get_Texture(strName);
            if (texture == null)
            {
                Debug.LogError($"[CStage_Manager] 텍스처 '{strName}'을 찾을 수 없습니다. "
                             + $"Addressable 라벨 '{CAddressableLabel.TEXTURE}'에 등록됐는지 확인하세요.");
                return null;
            }

            return texture as Texture2D;
        }
        #endregion 초기화

        #region 스테이지 / 웨이브 진행
        public bool Start_Stage()
        {
            if (Spawn_Player() == false)
                return false;

            // 이전 스테이지가 CLEAR/FAIL로 끝나며 0으로 내려둔 타임스케일을 되돌린다.
            Set_ActorTimeScale(1f);

            m_bPaused    = false;
            m_iStar      = 0;               // 260905_판을 새로 시작하면 별도 처음부터
            m_eWavePhase = WAVE_PHASE.NONE;
            m_cGridRenderer.Set_CoverAlpha(1f);

            // 260921_다시 뽑기 · 버리기는 판 전체에서 센다
            m_iRerollLeft = Mathf.Max(0, m_cMapInfo.iPickReroll);
            m_iBanishLeft = Mathf.Max(0, m_cMapInfo.iPickBanish);
            m_hsBanished.Clear();

            m_eState = STAGE_STATE.PLAYING;
            Enter_Wave(1);
            Begin_StartSelect();
            return true;
        }

        public void Tick(float fDeltaTime)
        {
            m_cGridRenderer.Tick();

            if (m_eState != STAGE_STATE.PLAYING || m_bPaused == true)
                return;

            // 260904_연출 중에는 규칙을 멈추고 연출만 돌린다.
            // 제한 시간도 같이 멈춘다 — 연출 때문에 시간을 잃으면 억울하다.
            if (m_eWavePhase != WAVE_PHASE.NONE)
            {
                Tick_WaveTransition(fDeltaTime);
                return;
            }

            Tick_EnemySlow(fDeltaTime);
            m_cStyle.Tick(fDeltaTime);
            Tick_Enemy(fDeltaTime);
            Tick_DevAutoFire(fDeltaTime);
            Tick_Projectile(fDeltaTime);
            Tick_Web();
            Tick_Soul();
            Tick_FieldItem();
            Tick_Decoy();
            Tick_FieldItemSpawn(fDeltaTime);

            m_fRemainTime -= fDeltaTime;
            if (m_fRemainTime <= 0f)
            {
                m_fRemainTime = 0f;
                Set_State(STAGE_STATE.FAIL);
            }
        }

        // 260920_웨이브 진입 — 판을 다시 깐다.
        // 점령한 칸을 비우고(가림막도 구멍 없는 상태로 돌아간다) 플레이어를 시작 칸에 다시 세운다.
        // 화면에서 바뀌는 것은 '드러난 보상 위에 새 가림막이 덮인다'뿐이고, 지운 땅을 처음부터 다시 먹는다.
        // 260912에는 점령을 유지한 채 이어서 했는데, 그러면 뒤 웨이브가 '조금만 더 먹으면 끝'이 되어
        // 새 그림을 드러내는 맛이 없었다. 몬스터는 그대로 둔다 — 웨이브는 심해지기만 한다(2-5).
        /// <param name="iWave"> 1부터 시작 </param>
        private void Enter_Wave(int iWave)
        {
            CWaveInfo cWave = m_cMapInfo.Get_Wave(iWave);
            if (cWave == null)
            {
                Debug.LogError($"[CStage_Manager] {iWave}웨이브 정보가 없습니다.");
                Set_State(STAGE_STATE.CLEAR);
                return;
            }

            m_iWave       = iWave;
            m_fRemainTime = cWave.fTimeLimit;

            // 260920_점령을 비우므로 점령률도 0으로 돌아간다. 카드 문턱(m_iRatioStep)도 같이 되돌려
            // 웨이브마다 다시 준다 — 안 그러면 2·3웨이브에서는 카드가 아예 안 나온다(2-10-1).
            m_cGrid.Reset(m_cMapInfo.iBorderThick, m_cMapInfo.iStartRadius);
            m_iRatioStep = 0;
            m_bFuseArmed = false;   // 260924_그리드가 트레일을 비웠으니 타던 도화선도 같이 끈다
            Respawn_Player();

            m_cGridRenderer.Set_WaveTexture(Get_Texture(m_cMapInfo.Get_CoverTex(iWave)),
                                            Get_Texture(m_cMapInfo.Get_RevealTex(iWave)));

            Collect_All(m_lstFieldItem);       // 260920_지난 웨이브에 못 주운 것은 판과 함께 사라진다
            m_fFieldItemTimer = 0f;
            // 260921_웨이브 시작 아이템은 시작 섬 자리를 고른 뒤에 깐다(Finish_StartSelect) — 먼저 깔면 섬 밑에 깔려 공짜로 먹힌다

            Spawn_Enemies(cWave);

            Debug.Log($"[CStage_Manager] {m_cMapInfo.strMapName} — {iWave}/{m_cMapInfo.iWaveCount} 웨이브 시작 "
                    + $"(목표 {cWave.fClearRatio:P0}, {cWave.fTimeLimit:F0}초, 몬스터 {cWave.TOTAL_ENEMY}마리)");
        }

        // 260904_웨이브를 넘길 때 바로 갈아 끼우지 않는다.
        // 가림막을 걷어 보상을 보여주고, 잠깐 감상할 틈을 준 뒤에 다음 판을 덮는다.
        // 이 게임에서 '드러났다'는 순간이 재미의 전부라 그냥 툭 바꾸면 남는 게 없다.
        private void Next_Wave()
        {
            // 260905_웨이브를 하나 넘길 때마다 별 하나. 이 시점에 확정되므로
            // 뒤 웨이브에서 죽더라도 여기까지의 별은 남는다.
            m_iStar = m_iWave;

            Begin_WaveTransition(m_iWave >= m_cMapInfo.iWaveCount ? 0 : m_iWave + 1);
        }

        private void Begin_WaveTransition(int iNextWave)
        {
            m_iNextWave   = iNextWave;
            m_eWavePhase  = WAVE_PHASE.REVEAL;
            m_fPhaseTimer = 0f;

            Set_ActorTimeScale(0f);     // 연출 동안에는 아무도 움직이지 않는다
        }

        private void Tick_WaveTransition(float fDeltaTime)
        {
            m_fPhaseTimer += fDeltaTime;

            switch (m_eWavePhase)
            {
                case WAVE_PHASE.REVEAL:
                    m_cGridRenderer.Set_CoverAlpha(1f - Mathf.Clamp01(m_fPhaseTimer / REVEAL_TIME));
                    if (m_fPhaseTimer >= REVEAL_TIME)
                        Go_Phase(WAVE_PHASE.HOLD);
                    break;

                case WAVE_PHASE.HOLD:
                    if (m_fPhaseTimer < HOLD_TIME)
                        break;

                    // 마지막 웨이브였다면 최종 보상이 드러난 화면 그대로 끝낸다.
                    if (m_iNextWave <= 0)
                    {
                        m_eWavePhase = WAVE_PHASE.NONE;
                        Set_State(STAGE_STATE.CLEAR);
                        break;
                    }

                    Enter_Wave(m_iNextWave);
                    m_cGridRenderer.Set_CoverAlpha(0f);
                    Go_Phase(WAVE_PHASE.COVER);
                    break;

                case WAVE_PHASE.COVER:
                    m_cGridRenderer.Set_CoverAlpha(Mathf.Clamp01(m_fPhaseTimer / COVER_TIME));
                    if (m_fPhaseTimer < COVER_TIME)
                        break;

                    m_cGridRenderer.Set_CoverAlpha(1f);
                    Begin_StartSelect();
                    break;

                case WAVE_PHASE.START_SELECT:
                    Tick_StartSelect(fDeltaTime);
                    break;
            }
        }

        #region 260921_시작 위치 슬롯 (2-3)
        /// <summary> 슬롯이 도는 중인가 — 화면이 'TAP TO START'를 띄우고 카메라가 맵 전체를 본다. </summary>
        public bool IS_START_SELECT => m_eWavePhase == WAVE_PHASE.START_SELECT;
        /// <summary> 아직 누르기를 기다리는가(누른 뒤 멈추는 중에는 false). </summary>
        public bool IS_START_WAITING => IS_START_SELECT == true && m_iSlotHopsLeft < 0;

        public event Action OnStartSlotHop;     // 한 칸 넘길 때마다 — 딸깍 소리
        public event Action OnStartSelected;    // 멈춰서 시작할 때

        /// <summary>
        /// 웨이브를 시작하기 전에 시작 섬을 깔 후보 자리를 여럿 뽑아 슬롯처럼 돌린다. 누르면 몇 칸 더 넘기며
        /// 느려지다 멈추고, 그 자리에 시작 섬을 깔아 거기서 시작한다. 매번 가운데에서 시작하면 판이 늘 같아진다.
        /// 도는 동안 액터와 제한 시간은 멈춰 있다(연출과 같다).
        /// </summary>
        private void Begin_StartSelect()
        {
            Collect_StartCandidates();

            m_eWavePhase    = WAVE_PHASE.START_SELECT;
            m_iSlotIndex    = 0;
            m_fSlotTimer    = 0f;
            m_fSlotInterval = SLOT_INTERVAL;
            m_iSlotHopsLeft = -1;
            m_bSlotSettling = false;

            Set_ActorTimeScale(0f);
            Show_StartCandidate(m_iSlotIndex);
        }

        /// <summary> 화면을 누르면 부른다. 도는 중이 아니면 아무 일도 없다. </summary>
        public void Confirm_StartSelect()
        {
            if (IS_START_WAITING == true)
                m_iSlotHopsLeft = SLOT_STOP_HOPS;
        }

        private void Tick_StartSelect(float fDeltaTime)
        {
            m_fSlotTimer += fDeltaTime;

            if (m_bSlotSettling == true)
            {
                if (m_fSlotTimer >= SLOT_SETTLE_TIME)
                    Finish_StartSelect();
                return;
            }

            if (m_fSlotTimer < m_fSlotInterval)
                return;

            m_fSlotTimer = 0f;

            // 누른 뒤에는 한 칸씩 느려지다 멈춘다
            if (m_iSlotHopsLeft == 0)
            {
                m_bSlotSettling = true;
                return;
            }

            if (m_iSlotHopsLeft > 0)
            {
                --m_iSlotHopsLeft;
                m_fSlotInterval *= SLOT_SLOWDOWN;
            }

            m_iSlotIndex = Next_SlotIndex(m_iSlotIndex, m_lstStartCandidate.Count);
            Show_StartCandidate(m_iSlotIndex);
            OnStartSlotHop?.Invoke();
        }

        /// <summary> 다음에 보여 줄 후보. 같은 자리가 연달아 나오면 멈춘 것처럼 보여 한 칸은 반드시 옮긴다. </summary>
        public static int Next_SlotIndex(int iCurrent, int iCount)
        {
            if (iCount <= 1)
                return 0;

            int iNext = UnityEngine.Random.Range(0, iCount - 1);
            return iNext >= iCurrent ? iNext + 1 : iNext;
        }

        // 후보 자리에 실제로 섬을 깔아 보여 준다 — 가림막에 섬 모양 구멍이 뚫려 그 자리가 한눈에 읽힌다
        private void Show_StartCandidate(int iIndex)
        {
            Vector2Int vCenter = m_lstStartCandidate.Count > 0 ? m_lstStartCandidate[iIndex] : m_cGrid.START_CENTER;
            m_cGrid.Reset(m_cMapInfo.iBorderThick, m_cMapInfo.iStartRadius, vCenter);
            Respawn_Player();
        }

        private void Finish_StartSelect()
        {
            m_eWavePhase = WAVE_PHASE.NONE;
            Spawn_WaveFieldItems();
            m_fFieldItemTimer = 0f;
            Set_ActorTimeScale(1f);
            OnStartSelected?.Invoke();
        }

        /// <summary>
        /// 섬이 맵 안에 들어가고, 몬스터와 SLOT_ENEMY_GAP칸 이상 떨어진 자리만 후보로 삼는다 —
        /// 몬스터 위에 섬을 깔면 시작하자마자 부딪힌다. 하나도 없으면 맵 한가운데 하나로 돌린다.
        /// </summary>
        private void Collect_StartCandidates()
        {
            m_lstStartCandidate.Clear();

            int iRadius = Mathf.Max(0, m_cMapInfo.iStartRadius);
            int iWidth  = m_cMapInfo.iGridWidth;
            int iHeight = m_cMapInfo.iGridHeight;

            for (int iTry = 0; iTry < 400 && m_lstStartCandidate.Count < SLOT_CANDIDATE; ++iTry)
            {
                Vector2Int vCenter = new Vector2Int(UnityEngine.Random.Range(iRadius + 1, iWidth - iRadius - 1),
                                                    UnityEngine.Random.Range(iRadius + 1, iHeight - iRadius - 1));

                if (m_cGrid.Can_PlaceStartArea(vCenter, iRadius) == false || Is_NearEnemy(vCenter, iRadius + SLOT_ENEMY_GAP) == true)
                    continue;

                // 후보끼리 섬 하나 넓이만큼은 떨어뜨린다 — 옆 칸으로만 옮겨 다니면 도는 느낌이 안 난다
                bool bTooClose = false;
                for (int i = 0; i < m_lstStartCandidate.Count && bTooClose == false; ++i)
                {
                    Vector2Int vGap = m_lstStartCandidate[i] - vCenter;
                    bTooClose = Mathf.Abs(vGap.x) <= iRadius * 2 && Mathf.Abs(vGap.y) <= iRadius * 2;
                }

                if (bTooClose == false)
                    m_lstStartCandidate.Add(vCenter);
            }

            if (m_lstStartCandidate.Count == 0)
                m_lstStartCandidate.Add(new Vector2Int(iWidth / 2, iHeight / 2));
        }

        private bool Is_NearEnemy(Vector2Int vCell, int iRange)
        {
            for (int i = 0; i < m_lstEnemy.Count; ++i)
            {
                CEnemy cEnemy = m_lstEnemy[i];
                if (cEnemy == null || cEnemy.IS_ALIVE == false)
                    continue;

                Vector2Int vGap = cEnemy.CUR_CELL - vCell;
                if (Mathf.Abs(vGap.x) <= iRange && Mathf.Abs(vGap.y) <= iRange)
                    return true;
            }

            return false;
        }
        #endregion 시작 위치 슬롯

        private void Go_Phase(WAVE_PHASE ePhase)
        {
            m_eWavePhase  = ePhase;
            m_fPhaseTimer = 0f;
        }

        // 260904_일시정지. 액터를 세우는 길은 Set_ActorTimeScale 하나뿐이다(2-8).
        // 260905_소모품은 인벤토리에서 개수를 깎는 쪽(CGameManager)이 먼저 판단하고,
        // 실제 효과만 여기서 플레이어에게 건다.
        /// <returns> 효과를 걸었으면 true </returns>
        /// <summary> 점령으로 점유율이 바뀔 때마다 부른다. 한 번에 많이 먹으면 연달아 여러 번 열릴 수도 있다. </summary>
        // 260924_점유율이 s_arrCardThreshold의 지점을 하나 더 넘을 때마다 3지선다를 하나씩 연다(2-21).
        // 큰 도형을 한 번에 닫으면 문턱을 여럿 건너뛸 수 있어, 넘긴 개수만큼 큐에 쌓아 한 장씩 연다
        // (여러 창이 겹쳐 뜨면 입력이 막히는 문제는 260921에 이미 CPickQueue로 해결해 뒀다 — 그대로 재사용한다).
        public void Check_CardReady()
        {
            int iThreshold = 0;
            while (iThreshold < s_arrCardThreshold.Length && m_cGrid.OWNED_RATIO >= s_arrCardThreshold[iThreshold])
                ++iThreshold;

            if (iThreshold <= m_iRatioStep)
                return;

            int iReady = iThreshold - m_iRatioStep;
            m_iRatioStep = iThreshold;

            m_cPickQueue.Add(iReady);
            Try_OpenPick();
        }

        private void Try_OpenPick()
        {
            if (m_cPickQueue.Try_Open() == true)
                OnCardReady?.Invoke();
        }

        /// <summary>
        /// 3지선다 창이 닫혔을 때(골랐거나, 낼 선택지가 없어 열지 못했을 때) 부른다.
        /// 기다리는 것이 있으면 곧바로 다음 창을 연다.
        /// </summary>
        public void Close_Pick()
        {
            m_cPickQueue.Close();
            Try_OpenPick();
        }

        // 260912_카드 효과. 고른 판이 끝날 때까지 유지된다.
        // 즉시 효과(보호막 / 회복)와 누적 효과(속도 / 회피 / 감속)를 한곳에서 본다.
        /// <returns> 효과를 걸었으면 true </returns>
        // 260917_3지선다에서 고른 것 — 카드면 카드 효과, 런 스킬이면 플레이어에게 붙이거나 레벨을 올린다.
        /// <returns> 효과를 걸었으면 true </returns>
        public bool Apply_Pick(CPickOption cOption)
        {
            if (cOption == null)
                return false;

            if (cOption.eKind == PICK_KIND.CARD)
                return Apply_Card(cOption.cCard);

            // 260917_각성 — 그 액티브가 그 자리에서 바뀐다
            if (cOption.eKind == PICK_KIND.AWAKEN)
                return m_cPlayer != null && m_cPlayer.Awaken_RunSkill(cOption.cAwaken);

            if (m_cPlayer == null || cOption.cRunSkill == null)
                return false;

            m_cPlayer.Add_RunSkill(cOption.cRunSkill);
            return true;
        }

        public bool Apply_Card(CCardInfo cInfo)
        {
            if (cInfo == null || m_cPlayer == null)
                return false;

            switch (cInfo.eType)
            {
                case CARD_TYPE.SHIELD:
                    m_cPlayer.Add_Shield();
                    return true;

                case CARD_TYPE.HEAL:
                    m_cPlayer.Add_Life(Mathf.Max(1, Mathf.RoundToInt(cInfo.fValue)));
                    return true;

                case CARD_TYPE.SPEED:
                    m_cPlayer.Add_CardSpeed(cInfo.fValue);
                    return true;

                case CARD_TYPE.EVASION:
                    m_cPlayer.Add_Evasion(cInfo.fValue);
                    return true;

                case CARD_TYPE.SLOW:
                    // 곱해서 쌓는다 — 여러 장을 먹어도 0으로 떨어지지 않는다.
                    m_fEnemyCardSlow = Mathf.Clamp(m_fEnemyCardSlow * cInfo.fValue, 0.15f, 1f);
                    Apply_EnemySpeed();
                    return true;

                // 260923_사냥형 8종(Docs/Design_Card_Pool.md 1장). fValue 뜻은 CardInfo.csv 머리 주석 참고.
                case CARD_TYPE.HUNT_THORN:
                    m_cPlayer.Add_ThornDamage(Mathf.Max(1, Mathf.RoundToInt(cInfo.fValue)));
                    return true;

                case CARD_TYPE.HUNT_KNOCKBACK:
                    m_cPlayer.Add_KnockbackBonus(cInfo.fValue);
                    return true;

                case CARD_TYPE.HUNT_STONESKIN:
                    m_cPlayer.Add_BodyDamageReduction(cInfo.fValue);
                    return true;

                case CARD_TYPE.HUNT_TAUNT:
                    m_cPlayer.Add_TauntBonus(Mathf.Max(1, Mathf.RoundToInt(cInfo.fValue)));
                    return true;

                case CARD_TYPE.HUNT_MARK:
                    // 여러 장을 먹으면 더 긴 지속시간으로 갱신한다(CImpactHandler의 '갱신' 규칙과 같은 결).
                    m_cHuntMarkImpact.fTime = Mathf.Max(m_cHuntMarkImpact.fTime, cInfo.fValue);
                    return true;

                case CARD_TYPE.HUNT_LOOT:
                    m_fLootCardBonus += cInfo.fValue;
                    return true;

                case CARD_TYPE.HUNT_EXECUTE:
                    m_fExecuteCardRadius = Mathf.Max(m_fExecuteCardRadius, cInfo.fValue);
                    return true;

                case CARD_TYPE.HUNT_FEAST:
                    m_cPlayer.Add_FeastHeal(Mathf.Max(1, Mathf.RoundToInt(cInfo.fValue)));
                    return true;

                default:
                    return false;
            }
        }

        public bool Apply_Consumable(CONSUME_EFFECT eEffect)
        {
            if (m_eState != STAGE_STATE.PLAYING || m_bPaused == true || m_cPlayer == null)
                return false;

            switch (eEffect)
            {
                case CONSUME_EFFECT.SHIELD: m_cPlayer.Add_Shield(); return true;
                case CONSUME_EFFECT.HEAL:   m_cPlayer.Add_Life(1);  return true;
                default:                    return false;
            }
        }


        // 260905_스킬 버튼은 UI에 있고 플레이어는 스테이지가 갖고 있으므로 여기를 거친다.
        /// <summary> 연출 중이거나 멈춰 있을 때는 발동하지 않는다. </summary>
        // 260912_ISkillHost — 감속 스킬이 부른다.
        /// <param name="fScale"> 원래 속도에 곱할 값 </param>
        public void Slow_Enemies(float fScale, float fDuration)
        {
            if (fDuration <= 0f)
                return;

            m_fEnemySlowScale = Mathf.Clamp(fScale, 0.1f, 1f);
            m_fEnemySlowTimer = Mathf.Max(m_fEnemySlowTimer, fDuration);

            Apply_EnemySpeed();
        }

        // 260923_ISkillHost — 쾌속 돌진(RUSH)이 길 위 몬스터까지 거리를 묻는다.
        // 몸 판정과 같은 반경(HIT_RANGE)을 쓴다 — 새 충돌 반경을 만들지 않는다.
        public float Get_RushDistance(Vector2 vFromWorld, Vector2 vDirWorld, float fMaxDistWorld)
        {
            float fBest = fMaxDistWorld;

            for (int i = 0; i < m_lstEnemy.Count; ++i)
            {
                CEnemy cEnemy = m_lstEnemy[i];
                if (cEnemy == null || cEnemy.IS_ALIVE == false)
                    continue;

                float   fRadius   = cEnemy.HIT_RANGE * m_cGrid.CELL_SIZE;
                Vector2 vToEnemy  = cEnemy.POS - vFromWorld;
                float   fAlong    = Vector2.Dot(vToEnemy, vDirWorld);
                if (fAlong < 0f || fAlong > fBest)
                    continue;

                float fPerpSq = vToEnemy.sqrMagnitude - fAlong * fAlong;
                if (fPerpSq > fRadius * fRadius)
                    continue;

                float fEnter = fAlong - Mathf.Sqrt(Mathf.Max(0f, fRadius * fRadius - fPerpSq));
                fBest = Mathf.Max(0f, Mathf.Min(fBest, fEnter));
            }

            return fBest;
        }

        // 260912_감속은 두 갈래다 — 스킬(한동안)과 카드(판 내내). 곱해서 넣는다.
        private void Apply_EnemySpeed()
        {
            float fScale = m_fEnemySlowScale * m_fEnemyCardSlow;

            for (int i = 0; i < m_lstEnemy.Count; ++i)
                m_lstEnemy[i].Set_SpeedScale(fScale);
        }

        private void Tick_EnemySlow(float fDeltaTime)
        {
            if (m_fEnemySlowTimer <= 0f)
                return;

            m_fEnemySlowTimer -= fDeltaTime;
            if (m_fEnemySlowTimer > 0f)
                return;

            m_fEnemySlowScale = 1f;
            Apply_EnemySpeed();
        }


        // 260922_스킬 버튼. 바로 못 쓰면 잠깐 기억했다가 쓸 수 있게 되는 순간 쓴다(CPlayer.Request_Skill)
        public void Request_Skill()
        {
            if (m_eState != STAGE_STATE.PLAYING || m_bPaused == true)
                return;

            if (m_eWavePhase != WAVE_PHASE.NONE || m_cPlayer == null)
                return;

            m_cPlayer.Request_Skill();
        }

        public void Set_Pause(bool bPause)
        {
            if (m_eState != STAGE_STATE.PLAYING || m_bPaused == bPause)
                return;

            m_bPaused = bPause;

            // 연출 중이면 원래도 멈춰 있어야 하므로 풀어 줄 때도 0을 유지한다.
            bool bResume = bPause == false && m_eWavePhase == WAVE_PHASE.NONE;
            Set_ActorTimeScale(bResume == true ? 1f : 0f);
        }
        #endregion 스테이지 / 웨이브 진행

        #region 플레이어
        // 260903_Engine은 프리팹이 없으면 Instantiate에서 예외를 던진다(반환값 null이 아님).
        // 호출 전에 미리 확인해야 스테이지 전체가 초기화 도중 죽는 것을 막을 수 있다.
        private static bool Has_Prefab(string strPrefabName)
        {
            return CGameInstance.Instance.Has_Prefab(strPrefabName);
        }

        private bool Spawn_Player()
        {
            // 260917_캐릭터 스킨 프리팹이 아직 없으면(Setup Assets 전) 기본 프리팹으로 대체한다 —
            // 프리팹 누락이 스테이지 전체를 죽인 적이 있다(260903 결정, 3장). 스탯 배율은 스킨과 상관없이 그대로 적용된다.
            string strPrefab = PREFAB_PLAYER;
            if (string.IsNullOrEmpty(m_strCharacterPrefab) == false)
            {
                if (Has_Prefab(m_strCharacterPrefab) == true)
                    strPrefab = m_strCharacterPrefab;
                else
                    Debug.LogWarning($"[CStage_Manager] 캐릭터 스킨 '{m_strCharacterPrefab}'이 없어 기본 스킨으로 대체합니다. "
                                    + "Tools/LandGrab/Setup Assets 를 실행하세요.");
            }

            if (Has_Prefab(strPrefab) == false)
            {
                Debug.LogError($"[CStage_Manager] '{strPrefab}'를 찾을 수 없습니다. "
                             + "Unity 메뉴 Tools/LandGrab/Setup Assets 를 실행한 뒤 다시 시도하세요.");
                return false;
            }

            CPlayerDesc cPlayerDesc = new CPlayerDesc
            {
                eObjectType     = OBJECT_TYPE.PLAYER,
                strPrefabName   = strPrefab,
                cGrid           = m_cGrid,
                vStartPos       = Find_StartPos(),
                // 260905_능력치 강화 + 260917_캐릭터 배율을 함께 반영
                fMoveSpeed      = m_cMapInfo.fPlayerSpeed * m_fSpeedRate * m_fCharSpeedRate,
                fEvasion        = m_fEvasion + m_fCharEvasionBonus,
                cSkillInfo      = m_cSkillInfo,
                iSkillLevel     = m_iSkillLevel,
                eMoveStyle      = MOVE_STYLE_NOW,
                // 260918_캐릭터 체력 배율(fMaxHpRate)은 목숨 수에 곱한다 — 반올림이라 목숨이 적으면 차이가 안 날 수 있다
                iLife           = Mathf.Max(1, Mathf.RoundToInt((m_cMapInfo.iLife + m_iBonusLife) * m_fCharHpRate)),
            };

            GameObject goPlayer = CGameInstance.Instance.Reuse_Object(cPlayerDesc);
            if (goPlayer == null)
            {
                Debug.LogError("[CStage_Manager] Player 생성 실패 — Addressable 라벨/프리팹 이름을 확인하세요.");
                return false;
            }

            m_cPlayer = goPlayer.GetComponent<CPlayer>();
            m_cPlayer?.Set_SkillHost(this);     // 260912_감속 스킬이 몬스터를 건드릴 창구
            m_cPlayer?.Set_RunSkillHost(this);  // 260916_영혼 수집가가 맵 위에 영혼을 놓을 창구
            if (m_cPlayer == null)
            {
                Debug.LogError("[CStage_Manager] 프리팹에 CPlayer 컴포넌트가 없습니다.");
                return false;
            }

            m_cPlayer.OnCapture     += On_PlayerCapture;
            m_cPlayer.OnDead        += On_PlayerDead;

            return true;
        }

        private void Respawn_Player()
        {
            if (m_cPlayer != null)
                m_cPlayer.Respawn(Find_StartPos());
        }

        /// <summary>
        /// 시작 위치는 아래쪽 테두리 가운데. 모양 마스크로 그 자리가 잘려 나갔을 수 있으므로
        /// 가장 가까운 안전 지대를 대신 찾는다.
        /// </summary>
        private Vector2 Find_StartPos()
        {
            // 260920_시작 섬의 **아래 경계**에서 시작한다(2-3). 섬 한가운데는 점령지 '내부'라
            // 이동 규칙(2-3)에 막혀 한 칸도 못 움직인다 — 안전한 곳은 경계선 위뿐이다.
            // 섬이 없는 맵(테두리만 있는 옛 설정)은 예전처럼 아래 테두리에서 시작한다.
            // 260923_땅이 다각형이라 칸이 아니라 경계 위의 점을 돌려준다(가장 가까운 경계로 붙인다).
            Vector2 vDesired = m_cMapInfo.iStartRadius > 0
                             ? new Vector2(m_cGrid.START_CENTER.x + 0.5f, m_cGrid.START_CENTER.y - m_cMapInfo.iStartRadius)
                             : new Vector2(m_cMapInfo.iGridWidth * 0.5f, m_cMapInfo.iBorderThick);

            if (m_cGrid.Try_Find_NearestBoundary(vDesired, out Vector2 vStart) == true)
                return vStart;

            Debug.LogError("[CStage_Manager] 플레이어가 설 안전 지대를 찾지 못했습니다. 모양 마스크를 확인하세요.");
            return vDesired;
        }
        #endregion 플레이어

        #region 몬스터
        // 260904_웨이브가 정한 조합대로 소환한다 (MapInfo.csv의 strWaveEnemy).
        // 260912_웨이브가 넘어가도 있던 몬스터는 그대로 둔다.
        // 표에 적힌 수보다 모자란 종류만 그 차이만큼 새로 넣는다 —
        // 전부 회수했다가 다시 뿌리면 플레이어 코앞에 몬스터가 순간이동하는 셈이 된다.
        //
        // 표에 적힌 수보다 많으면 줄이지 않는다. 웨이브는 심해지기만 하는 것이 기획이고,
        // 도중에 몬스터가 사라지면 방금까지 피하던 것이 증발해 오히려 혼란스럽다.
        private void Spawn_Enemies(CWaveInfo cWave)
        {
            if (m_cEnemyTable == null || cWave.lstEnemy.Count == 0)
                return;

            int iSpawned = 0;
            int iTotal   = cWave.TOTAL_ENEMY;

            for (int i = 0; i < cWave.lstEnemy.Count; ++i)
            {
                CWaveEnemy cEntry = cWave.lstEnemy[i];
                CEnemyInfo cInfo  = m_cEnemyTable.Get_Info(cEntry.iEnemyID);
                if (cInfo == null)
                    continue;

                if (Has_Prefab(cInfo.strPrefabName) == false)
                {
                    // 260903_몬스터가 없어도 스테이지는 굴러가야 한다 — 경고만 남기고 넘어간다.
                    Debug.LogWarning($"[CStage_Manager] '{cInfo.strPrefabName}'가 없어 "
                                   + $"{cInfo.strName}을(를) 건너뜁니다. Tools/LandGrab/Setup Assets 를 실행하세요.");
                    continue;
                }

                int iNeed = cEntry.iCount - Count_Enemy(cEntry.iEnemyID);

                for (int n = 0; n < iNeed; ++n)
                {
                    Spawn_Enemy(cInfo, Find_EnemySpawnCell(iSpawned, iTotal), Get_EnemySpawnDir(iSpawned));
                    ++iSpawned;
                }
            }
        }

        /// <summary> 지금 살아 있는 그 종류의 몬스터 수. </summary>
        private int Count_Enemy(int iEnemyID)
        {
            int iCount = 0;

            for (int i = 0; i < m_lstEnemy.Count; ++i)
            {
                if (m_lstEnemy[i] != null && m_lstEnemy[i].ENEMY_ID == iEnemyID)
                    ++iCount;
            }

            return iCount;
        }

        private void Spawn_Enemy(CEnemyInfo cInfo, Vector2Int vCell, Vector2 vDir)
        {
            if (m_lstEnemy.Count >= MAX_ENEMY)
                return;

            CEnemyDesc cEnemyDesc = new CEnemyDesc
            {
                eObjectType     = OBJECT_TYPE.ENEMY,
                strPrefabName   = cInfo.strPrefabName,
                cGrid           = m_cGrid,
                vStartCell      = vCell,
                vStartDir       = vDir,
                iEnemyID        = cInfo.iEnemyID,
                eGimmick        = cInfo.eGimmick,
                fSpeed          = cInfo.fSpeed,
                fChaseSpeed     = cInfo.fChaseSpeed,
                fTurnRate       = cInfo.fTurnRate,
                fHitRange       = cInfo.fHitRange,
                fGimmickCool    = cInfo.fGimmickCool,
                fGimmickValue   = cInfo.fGimmickValue,
                fGimmickRange   = cInfo.fGimmickRange,
                fGimmickDuration= cInfo.fGimmickDuration,
                iGimmickRefID   = cInfo.iGimmickRefID,
                eFirePattern    = cInfo.eFirePattern,
                iFireCount      = cInfo.iFireCount,
                fFireAngle      = cInfo.fFireAngle,
                fFireInterval   = cInfo.fFireInterval,
                iHp             = cInfo.iHp,
                iAttack         = cInfo.iAttack,
            };

            GameObject goEnemy = CGameInstance.Instance.Reuse_Object(cEnemyDesc);
            if (goEnemy == null)
            {
                Debug.LogError("[CStage_Manager] Enemy 생성 실패 — Addressable 라벨/프리팹 이름을 확인하세요.");
                return;
            }

            CEnemy cEnemy = goEnemy.GetComponent<CEnemy>();
            if (cEnemy == null)
            {
                Debug.LogError("[CStage_Manager] 프리팹에 CEnemy 컴포넌트가 없습니다.");
                return;
            }

            cEnemy.Set_GimmickHost(this);

            // 260912_감속이 걸려 있는 동안 소환된 몬스터만 멀쩡하면 스킬이 반쪽이 된다.
            cEnemy.Set_SpeedScale(m_fEnemySlowScale * m_fEnemyCardSlow);
            m_lstEnemy.Add(cEnemy);
        }

        /// <summary> 웨이브가 바뀔 때 몬스터와 기믹 소환물을 전부 풀에 돌려준다. </summary>
        private void Collect_Enemies()
        {
            Collect_All(m_lstEnemy);
            Collect_All(m_lstProjectile);
            Collect_All(m_lstWeb);
            Collect_All(m_lstSoul);
            Collect_All(m_lstFieldItem);
            Collect_Decoy();
        }

        private void Collect_Decoy()
        {
            if (m_cDecoy != null)
                CGameInstance.Instance.Collect_Object(m_cDecoy);

            m_cDecoy = null;
        }

        // 목록 세 개가 같은 일을 하므로 하나로 묶는다.
        // 이쪽은 아직 살아 있는 오브젝트를 즉시 걷어내는 길이라 Engine의 자동 회수를 기다리지 않는다.
        private static void Collect_All<T>(List<T> lstObject) where T : CGameObject
        {
            for (int i = 0; i < lstObject.Count; ++i)
            {
                if (lstObject[i] != null)
                    CGameInstance.Instance.Collect_Object(lstObject[i]);
            }

            lstObject.Clear();
        }

        // 플레이어 시작 지점(아래쪽)에서 멀리 떨어진 가운데~위쪽 대역에 고르게 배치한다.
        // 모양 마스크로 잘린 자리에 걸릴 수 있으므로 가장 가까운 미점령 칸으로 보정한다.
        private Vector2Int Find_EnemySpawnCell(int iIndex, int iTotal)
        {
            float fT = (iIndex + 1f) / (iTotal + 1f);

            int x = Mathf.RoundToInt(Mathf.Lerp(m_cMapInfo.iGridWidth * 0.2f, m_cMapInfo.iGridWidth * 0.8f, fT));
            int y = Mathf.RoundToInt(m_cMapInfo.iGridHeight * (0.4f + 0.15f * (iIndex % 3)));
            Vector2Int vDesired = new Vector2Int(x, y);

            return m_cGrid.Try_Find_NearestCell(vDesired, CELL_STATE.EMPTY, SPAWN_SEARCH_RADIUS,
                                                out Vector2Int vSpawn) ? vSpawn : vDesired;
        }

        private static Vector2 Get_EnemySpawnDir(int iIndex)
        {
            // 대각선으로 출발시켜야 벽에 튕기며 맵 전체를 고르게 돈다.
            float fX = (iIndex % 2 == 0) ? 1f : -1f;
            float fY = ((iIndex / 2) % 2 == 0) ? 1f : -1f;
            return new Vector2(fX, fY).normalized;
        }

        private void Tick_Enemy(float fDeltaTime)
        {
            if (m_cPlayer == null)
                return;

            // 플레이어가 안전 지대(선) 위에 있으면 몬스터는 쫓지 않고 배회한다.
            // 260923_선을 긋는 중 = 안전 지대 밖이다(내 땅 위에서는 경계선을 따라 걷는다)
            bool bExposed = m_cPlayer.IS_DRAWING;
            Vector2 vPlayerPos = m_cPlayer.transform.position;
            bool bHit = false;          // 260924_이제 '몸'에 부딪혔을 때만 선다 — '선'은 도화선(아래)을 탄다
            CEnemy cHitEnemy = null;    // 260923_HP 풀 — 어느 몬스터가 닿았는지 알아야 공격력 · 넉백을 낼 수 있다

            m_bPlayerExposed = bExposed;

            // 260922_아슬아슬 — 선을 긋는 중에만 센다. 안전 지대로 돌아오면 붙어 있던 것은 잊는다
            if (bExposed == false)
                m_cStyle.Clear_Near();
            m_cStyle.Begin_Near();

            // 260904_피격이 확정돼도 루프를 끊지 않는다 — break로 빠지면 뒤쪽 몬스터의 추적 상태가 갱신되지
            // 않아, 플레이어가 안전 지대로 돌아간 뒤에도 한 프레임 더 추적 속도로 달려든다.
            // 260916_회전탄/몽둥이에 죽은 몬스터(bCollect)를 여기서 걷어낸다 — 다음 프레임이면
            // Engine이 이미 풀로 돌려줘 다른 몬스터 데이터로 바뀌어 있을 수 있다. 뒤에서부터
            // 지워야 앞쪽 인덱스가 밀리지 않는다.
            for (int i = m_lstEnemy.Count - 1; i >= 0; --i)
            {
                CEnemy cEnemy = m_lstEnemy[i];
                if (cEnemy == null || cEnemy.IS_DEAD == true)
                {
                    // 260920_잡은 자리에 확률로 아이템을 떨어뜨린다. 걷어내기 전에 위치를 먼저 읽어야 한다.
                    if (cEnemy != null)
                        Drop_FieldItem(cEnemy.transform.position);

                    m_lstEnemy.RemoveAt(i);
                    continue;
                }

                // 260921_분신이 살아 있으면 몬스터는 분신을 쫓는다(2-11-3).
                cEnemy.Set_ChaseState(bExposed, m_cDecoy != null ? m_cDecoy.POS : vPlayerPos);

                if (bHit == true)
                    continue;

                // 260924_그리는 중인 선분에 몬스터가 닿으면 그 자리에서 도화선에 불이 붙는다 — 더 이상 즉사가
                // 아니다(2-3). 이미 타고 있으면 새로 붙이지 않는다 — 발화 지점을 뒤로 미루면 오히려 유리해진다.
                // 무적 중(유령 걸음 등, 2-11-3)에는 몸처럼 선도 그냥 통과한다 — 안 그러면 무적이 끝난 뒤에도
                // 그때 붙은 불이 계속 쫓아오는, "통과했는데 나중에 맞는" 모순이 생긴다.
                Vector2 vEnemyGrid = m_cGrid.World_ToGrid(cEnemy.POS);
                if (m_bFuseArmed == false && m_cPlayer.IS_INVINCIBLE == false
                 && m_cGrid.Try_Find_TrailTouch(vEnemyGrid, cEnemy.HIT_RANGE, out float fTouchArc) == true)
                {
                    Ignite_Fuse(fTouchArc, cEnemy);
                    continue;
                }

                // 플레이어와의 직접 충돌은 땅을 먹으러 나와 있을 때만 판정한다. 이건 그대로 즉시 피해다.
                if (bExposed == true
                    && Vector2.Distance(cEnemy.POS, vPlayerPos) <= cEnemy.HIT_RANGE * m_cGrid.CELL_SIZE)
                {
                    bHit = true;
                    cHitEnemy = cEnemy;
                    continue;
                }

                // 260922_아슬아슬 — 몸 판정보다 조금 더 가까이(몸 · 선 어느 쪽이든) 붙었다가 맞지 않고 떨어지면
                if (bExposed == true)
                {
                    float fNear = cEnemy.HIT_RANGE + CStyleTracker.NEAR_MARGIN;
                    bool bNear = Vector2.Distance(cEnemy.POS, vPlayerPos) <= fNear * m_cGrid.CELL_SIZE
                              || m_cGrid.Try_Find_TrailTouch(vEnemyGrid, fNear, out float _) == true;

                    if (m_cStyle.Report_Near(cEnemy.GetInstanceID(), bNear) == true)
                        OnStylish?.Invoke(STYLE_ACTION.NEAR_MISS, 1);
                }
            }

            m_cStyle.End_Near();

            // 260924_몸통 박치기만 여기서 바로 처리한다 — 선(도화선)은 Tick_Fuse가 따로 잰다.
            if (bHit == true)
            {
                m_cStyle.Clear_Near();      // 260922_맞았으면 아슬아슬이 아니다
                // 260924_몸에 부딪힌 것은 선이 끊긴 것이 아니므로 회피가 그대로 듣는다(bLineCut=false).
                // 260923_돌가죽(HUNT_STONESKIN)이 있으면 배율만큼 줄어든다.
                int iBodyDamage = cHitEnemy != null
                    ? Mathf.Max(1, Mathf.RoundToInt(cHitEnemy.ATTACK * m_cPlayer.BODY_DAMAGE_SCALE))
                    : 1;
                m_cPlayer.Damage(iBodyDamage, false);

                // 260923_몸에 부딪힌 몬스터는 튕겨난다 — 피해가 보호막 · 회피로 막혀도 충돌 자체는 일어난 것이라 넉백은 그대로 건다.
                if (cHitEnemy != null)
                {
                    Vector2 vAway = cHitEnemy.POS - vPlayerPos;
                    if (vAway.sqrMagnitude < 0.0001f)
                        vAway = Vector2.up;

                    // 260923_거센 팔뚝(HUNT_KNOCKBACK) — 카드가 없으면 배율이 1이라 그대로다.
                    cHitEnemy.Push(vAway.normalized,
                        PLAYER_HIT_KNOCKBACK_CELL * m_cPlayer.KNOCKBACK_SCALE * m_cGrid.CELL_SIZE,
                        PLAYER_HIT_KNOCKBACK_DURATION);

                    // 260923_가시 갑옷(HUNT_THORN) — 반격 피해. 만찬(HUNT_FEAST)은 이 피해가 실제로
                    // 들어갔을 때만 회복한다(가시 갑옷 없이는 발동하지 않는 조합 전제, Design_Card_Pool.md 1장).
                    if (m_cPlayer.THORN_DAMAGE > 0)
                    {
                        cHitEnemy.Damage(m_cPlayer.THORN_DAMAGE);
                        if (m_cPlayer.FEAST_HEAL > 0)
                            m_cPlayer.Add_Life(m_cPlayer.FEAST_HEAL);
                    }

                    // 260923_사냥감 표식(HUNT_MARK) — 넉백당한 몬스터가 잠깐 감속한다.
                    if (m_cHuntMarkImpact.fTime > 0f)
                        cHitEnemy.IMPACT.Apply(m_cHuntMarkImpact, cHitEnemy, vPlayerPos, m_cGrid.CELL_SIZE, this, PROJECTILE_SIDE.PLAYER_SHOT);
                }

                // 260923_도발(HUNT_TAUNT) — 몸 충돌마다 분노 게이지를 추가로 올린다(회전탄 · 몽둥이가
                // 쓰는 것과 같은 훅). 카드가 없으면 TAUNT_EXTRA_HIT이 0이라 그대로 아무 일도 없다.
                for (int i = 0; i < m_cPlayer.TAUNT_EXTRA_HIT; ++i)
                    m_cPlayer.On_MonsterHit();
            }

            Tick_Fuse(fDeltaTime);
        }

        // 260924_도화선 발화 — 몬스터가 처음 닿은 선 자리에서 불이 붙는다. 그 몬스터의 공격력을 미리
        // 적어 둔다 — 불이 다 타들어올 때쯤엔 그 몬스터가 어디 있는지(살아 있는지조차) 알 수 없어서다.
        /// <param name="fArc"> 선 시작점부터 잰 발화 자리(칸) </param>
        private void Ignite_Fuse(float fArc, CEnemy cEnemy)
        {
            m_bFuseArmed             = true;
            m_fFuseFrom              = fArc;
            m_fFuseFront             = fArc;
            m_iFuseDamage            = cEnemy != null ? cEnemy.ATTACK : 1;
            m_cGrid.IS_TRAIL_BURNING = true;
            m_cGrid.Set_Burn(m_fFuseFrom, m_fFuseFront);

            OnFuseIgnited?.Invoke(m_cGrid.Grid_ToCell(m_cGrid.Get_TrailPoint(fArc)));
        }

        /// <summary>
        /// 260924_도화선을 매 프레임 태운다. 발화 지점부터 트레일 끝(플레이어가 있는 칸)까지 따라잡으면
        /// 선을 통째로 잃고 HP가 깎인다 — 그 전에 플레이어가 안전 지대로 돌아오면(CTerritoryGrid.Step_To)
        /// 선만 잃고 끝난다. 화면 없이도 발화 지점 · 속도만으로 검증할 수 있다(CProtoTest).
        /// </summary>
        private void Tick_Fuse(float fDeltaTime)
        {
            if (m_bFuseArmed == false)
                return;

            // 몸에 맞아 죽거나 마감 스킬로 트레일이 먼저 사라지면 도화선도 같이 꺼진다.
            if (m_cGrid.IS_DRAWING == false)
            {
                m_bFuseArmed = false;
                m_cGrid.IS_TRAIL_BURNING = false;
                return;
            }

            // 불은 선 끝(플레이어) 쪽으로 타들어온다. 탄 구간은 그리드에 적어 두면 렌더러가 그리지 않는다
            m_fFuseFront += FUSE_SPEED_CELL_PER_SEC * fDeltaTime;
            m_cGrid.Set_Burn(m_fFuseFrom, Mathf.Min(m_fFuseFront, m_cGrid.TRAIL_LENGTH));

            if (m_fFuseFront < m_cGrid.TRAIL_LENGTH)
                return;

            // 트레일 끝까지 태웠다 — 따라잡혔다. 선은 무조건 사라지고(보호막은 HP만 막는다),
            // 발화 당시 몬스터의 공격력만큼 깎인다. 선이 끊긴 것이므로 회피는 못 흘린다(bLineCut=true).
            m_bFuseArmed = false;
            m_cGrid.IS_TRAIL_BURNING = false;
            m_cGrid.Clear_Trail();
            m_cPlayer.Damage(m_iFuseDamage, true);
        }

        /// <summary>
        /// 260920_점령한 땅 안에 갇힌 몬스터를 죽인다(2-3).
        /// 죽이는 길은 기존과 같다 — HP를 모두 깎아 bCollect가 서면 Tick_Enemy가 목록에서 걷어내고,
        /// 그 자리에 조각 · 아이템이 떨어진다(2-20, 2-21). 새 회수 경로를 만들지 않는다.
        /// </summary>
        /// <returns> 이번에 죽인 마리 수 </returns>
        private int Kill_EnemiesInOwned()
        {
            int iKilled = 0;
            m_lstExecuteKillPos.Clear();

            for (int i = 0; i < m_lstEnemy.Count; ++i)
            {
                CEnemy cEnemy = m_lstEnemy[i];
                if (cEnemy == null || cEnemy.IS_ALIVE == false)
                    continue;

                if (m_cGrid.Is_OwnedPoint(m_cGrid.World_ToGrid(cEnemy.POS)) == false)
                    continue;

                if (m_fExecuteCardRadius > 0f)
                    m_lstExecuteKillPos.Add(cEnemy.POS);

                cEnemy.Damage(cEnemy.HP);
                ++iKilled;
            }

            // 260923_처형자(HUNT_EXECUTE) — 가둬 죽인 자리 반경 안 생존 몬스터에게도 고정 피해를 준다.
            if (m_fExecuteCardRadius > 0f && m_lstExecuteKillPos.Count > 0)
            {
                float fRadiusWorld = m_fExecuteCardRadius * m_cGrid.CELL_SIZE;
                for (int i = 0; i < m_lstExecuteKillPos.Count; ++i)
                {
                    for (int j = 0; j < m_lstEnemy.Count; ++j)
                    {
                        CEnemy cOther = m_lstEnemy[j];
                        if (cOther == null || cOther.IS_ALIVE == false)
                            continue;

                        if (Vector2.Distance(cOther.POS, m_lstExecuteKillPos[i]) <= fRadiusWorld)
                            cOther.Damage(EXECUTE_SPLASH_DAMAGE);
                    }
                }
            }

            // 가두는 것도 '때린 것'이다 — 분노 게이지가 오른다(회전탄 · 몽둥이와 같은 자리).
            for (int i = 0; i < iKilled; ++i)
                m_cPlayer?.On_MonsterHit();

            return iKilled;
        }
        #endregion 몬스터

        #region 기믹 소환물 (IGimmickHost)
        // 260904_투사체·거미줄·부하는 전부 여기서 만들고 여기서 회수한다.
        // 기믹 모듈이 직접 풀을 만지면 웨이브가 넘어갈 때 회수할 방법이 없어진다.
        public bool IS_PLAYER_EXPOSED => m_bPlayerExposed;

        // 260921_땅 갉는 자. 플레이어 둘레 GNAW_PROTECT_RADIUS칸은 지킨다 — 서 있거나 막 밟으려는 자리가
        // 발밑에서 사라지면 선을 긋지도 않았는데 빈 땅 위에 서 버린다.
        private const float GNAW_PROTECT_RADIUS = 2f;

        public int Gnaw_Territory(Vector2Int vCell, float fRange, int iCount)
        {
            if (m_cGrid == null || m_cPlayer == null)
                return 0;

            return m_cGrid.Erode_Near(CTerritoryGrid.Cell_ToGrid(vCell), fRange, iCount, m_cPlayer.GRID_POS, GNAW_PROTECT_RADIUS);
        }

        // 260917_포수가 쏜다. 탄의 수치는 전부 ProjectileInfo.csv에 있다.
        public void Spawn_EnemyShot(int iProjectileID, Vector2 vPos, Vector2 vDir, IImpactTarget cOwner)
        {
            if (m_iDevEnemyShotID > 0)
                iProjectileID = m_iDevEnemyShotID;

            Spawn_Projectile(iProjectileID, vPos, vDir, PROJECTILE_SIDE.ENEMY_SHOT, cOwner);
        }

        public void Spawn_Web(Vector2Int vCell, float fLifeTime, float fSlowRatio)
        {
            if (Has_Prefab(PREFAB_WEB) == false)
                return;

            // 같은 칸에 겹쳐 깔아 봐야 효과는 같고 오브젝트만 는다.
            for (int i = 0; i < m_lstWeb.Count; ++i)
            {
                if (m_lstWeb[i].CELL == vCell)
                    return;
            }

            CWebDesc cDesc = new CWebDesc
            {
                eObjectType     = OBJECT_TYPE.ENEMY_EFFECT,
                strPrefabName   = PREFAB_WEB,
                cGrid           = m_cGrid,
                vCell           = vCell,
                fLifeTime       = fLifeTime,
                fSlowRatio      = fSlowRatio,
            };

            GameObject goWeb = CGameInstance.Instance.Reuse_Object(cDesc);
            if (goWeb == null)
                return;

            CWeb cWeb = goWeb.GetComponent<CWeb>();
            if (cWeb != null)
                m_lstWeb.Add(cWeb);
        }

        public void Spawn_Minion(int iEnemyID, int iCount, Vector2 vPos)
        {
            if (m_cEnemyTable == null)
                return;

            CEnemyInfo cInfo = m_cEnemyTable.Get_Info(iEnemyID);
            if (cInfo == null || Has_Prefab(cInfo.strPrefabName) == false)
                return;

            Vector2Int vFrom = m_cGrid.World_ToCell(vPos);

            for (int i = 0; i < iCount; ++i)
            {
                // 소환된 자리가 점령지면 몬스터가 갇힌다 — 가장 가까운 미점령 칸을 찾아 준다.
                if (m_cGrid.Try_Find_NearestCell(vFrom, CELL_STATE.EMPTY, SPAWN_SEARCH_RADIUS,
                                                 out Vector2Int vCell) == false)
                    return;

                Spawn_Enemy(cInfo, vCell, Get_EnemySpawnDir(m_lstEnemy.Count));
            }
        }

        // 260917_탄이 누구에게 닿았는지는 여기서 한 번에 본다. 닿기 시작 · 닿아 있음 · 떨어짐은 탄 본체가 가린다.
        private void Tick_Projectile(float fDeltaTime)
        {
            Build_ImpactTargets();

            // 뒤에서부터 지운다 — 앞에서 지우면 인덱스가 밀린다.
            for (int i = m_lstProjectile.Count - 1; i >= 0; --i)
            {
                CProjectile cProjectile = m_lstProjectile[i];

                // 260904_만료된 것은 Engine이 bCollect를 보고 알아서 풀로 돌려준다.
                // 여기서 Collect_Object까지 부르면 같은 오브젝트를 두 번 반납하게 되므로
                // 목록에서 빼기만 한다.
                if (cProjectile == null || cProjectile.IS_EXPIRED == true)
                {
                    m_lstProjectile.RemoveAt(i);
                    continue;
                }

                CProjectileCore cCore = cProjectile.CORE;
                int iHitBefore = cCore.HIT_COUNT;
                cCore.Update_Contact(cCore.SIDE == PROJECTILE_SIDE.ENEMY_SHOT ? m_lstPlayerTarget : m_lstEnemyTarget,
                                     fDeltaTime);

                // 260917_플레이어 탄이 몬스터를 때렸다 — 회전탄 · 몽둥이와 같이 분노 게이지를 올린다(2-11-1).
                if (cCore.SIDE == PROJECTILE_SIDE.PLAYER_SHOT && m_cPlayer != null)
                {
                    for (int h = iHitBefore; h < cCore.HIT_COUNT; ++h)
                        m_cPlayer.On_MonsterHit();
                }

                if (cCore.IS_EXPIRED == true)
                    cProjectile.Expire();
            }

            Cancel_EnemyShots();
        }

        // 260917_회전탄처럼 CANCEL_SHOT이 붙은 플레이어 탄은 닿은 적탄을 지운다.
        // 예전에는 회전탄 좌표만 따로 받아 여기서 판정했다 — 이제 탄 표의 속성이라 어떤 탄에든 붙일 수 있다.
        // 지우는 것은 작은 적탄(POINT)뿐이다. 레이저 · 충격파 · 폭발은 지워지지 않는다.
        private void Cancel_EnemyShots()
        {
            for (int i = 0; i < m_lstProjectile.Count; ++i)
            {
                CProjectile cShield = m_lstProjectile[i];
                if (cShield == null || cShield.IS_EXPIRED == true || cShield.SIDE != PROJECTILE_SIDE.PLAYER_SHOT
                    || cShield.CORE.CAN_CANCEL_SHOT == false)
                    continue;

                for (int e = 0; e < m_lstProjectile.Count; ++e)
                {
                    CProjectile cShot = m_lstProjectile[e];
                    if (cShot == null || cShot.IS_EXPIRED == true || cShot.SIDE != PROJECTILE_SIDE.ENEMY_SHOT
                        || cShot.CORE.IS_CANCELABLE == false)
                        continue;

                    if (Vector2.Distance(cShot.POS, cShield.POS) <= cShot.CORE.HIT_RADIUS + cShield.CORE.HIT_RADIUS)
                        cShot.Expire();
                }
            }
        }

        private void Build_ImpactTargets()
        {
            m_lstEnemyTarget.Clear();
            for (int i = 0; i < m_lstEnemy.Count; ++i)
            {
                if (m_lstEnemy[i] != null && m_lstEnemy[i].IS_ALIVE == true)
                    m_lstEnemyTarget.Add(m_lstEnemy[i]);
            }

            // 몬스터 충돌과 같은 규칙 — 땅을 먹으러 나와 있을 때만 맞는다.
            // 무적 중(맞고 안전 지대로 돌아온 직후)에는 기절 · 감속도 걸지 않는다 — 피해만 막으면 굳은 채로 다시 맞는다.
            m_lstPlayerTarget.Clear();
            if (m_cPlayer != null && m_bPlayerExposed == true && m_cPlayer.IS_INVINCIBLE == false
                && m_cPlayer.IS_ALIVE == true)
                m_lstPlayerTarget.Add(m_cPlayer);
        }

        // 260917_개발용 — 플레이어 탄을 쏘는 스킬이 아직 없어 이 스위치로만 확인한다(CGameConfig).
        private void Tick_DevAutoFire(float fDeltaTime)
        {
            if (m_iDevAutoFireID <= 0 || m_cPlayer == null)
                return;

            m_fDevAutoFireTimer -= fDeltaTime;
            if (m_fDevAutoFireTimer > 0f)
                return;

            IImpactTarget cTarget = CTargetFinder_Utility.Find(m_lstEnemy, m_cPlayer.POS, TARGET_FIND.NEAREST);
            if (cTarget == null)
                return;     // 쏠 대상이 생기면 바로 나가게 타이머를 그대로 둔다

            m_fDevAutoFireTimer = m_fDevAutoFireCool;
            Spawn_PlayerShot(m_iDevAutoFireID, m_cPlayer.POS, cTarget.POS - m_cPlayer.POS);
        }

        private void Tick_Web()
        {
            Vector2Int vPlayerCell = m_cPlayer != null ? m_cPlayer.CUR_CELL : Vector2Int.zero;
            float fSlowRatio = 1f;

            for (int i = m_lstWeb.Count - 1; i >= 0; --i)
            {
                CWeb cWeb = m_lstWeb[i];

                if (cWeb == null || cWeb.IS_EXPIRED == true)
                {
                    m_lstWeb.RemoveAt(i);
                    continue;
                }

                if (cWeb.CELL == vPlayerCell)
                    fSlowRatio = Mathf.Min(fSlowRatio, cWeb.SLOW_RATIO);
            }

            // 밟고 있지 않으면 1이 들어가 원래 속도로 돌아온다.
            m_cPlayer?.Set_SpeedScale(fSlowRatio);
        }
        #endregion 기믹 소환물 (IGimmickHost)

        #region 투사체 (IProjectileHost)
        // 260917_Project_GYM 이식. 적탄 · 플레이어 탄 모두 여기서 만들고 여기서 회수한다(기믹 소환물과 같은 이유).
        public Rect WORLD_BOUNDS => new Rect(m_cGrid.ORIGIN, m_cGrid.WORLD_SIZE);

        /// <summary>
        /// 맵 밖과 모양 마스크로 잘린 칸은 누구에게나 벽이다. 적탄에게는 점령지도 벽이다 —
        /// 땅을 먹은 만큼 막아 주는 것이 이 게임의 규칙이라 튕기는 탄 · 레이저도 점령지 가장자리에서 멈춘다.
        /// 플레이어 탄은 점령지 위를 지나간다(몬스터는 미점령 지대에만 있으므로).
        /// </summary>
        public bool Is_Wall(Vector2 vWorldPos, PROJECTILE_SIDE eSide)
        {
            if (WORLD_BOUNDS.Contains(vWorldPos) == false)
                return true;

            if (m_cGrid.Get_Cell(m_cGrid.World_ToCell(vWorldPos)) == CELL_STATE.BLOCK)
                return true;

            // 260923_점령지는 다각형으로 본다 — 사선 · 원 모양 가장자리에서 정확히 멈춘다
            return eSide == PROJECTILE_SIDE.ENEMY_SHOT && m_cGrid.Is_OwnedPoint(m_cGrid.World_ToGrid(vWorldPos)) == true;
        }

        public IImpactTarget Find_Target(Vector2 vFrom, PROJECTILE_SIDE eSide)
        {
            if (eSide == PROJECTILE_SIDE.PLAYER_SHOT)
                return CTargetFinder_Utility.Find(m_lstEnemy, vFrom, TARGET_FIND.NEAREST);

            return m_cPlayer != null && m_cPlayer.IS_ALIVE == true ? m_cPlayer : null;
        }

        // 260917_CHAIN 특성이 다음으로 튈 대상을 찾을 때 쓴다. 플레이어는 하나뿐이라 적탄 쪽은
        // 이미 맞았으면(hsExclude) 더 튈 곳이 없다 — 몬스터만 여럿이라 실제로 튀는 건 플레이어 탄이다.
        public IImpactTarget Find_ChainTarget(Vector2 vFrom, PROJECTILE_SIDE eSide, float fRadius,
                                              ICollection<IImpactTarget> hsExclude)
        {
            if (eSide == PROJECTILE_SIDE.PLAYER_SHOT)
                return CTargetFinder_Utility.Find_Nearby(m_lstEnemy, vFrom, fRadius, hsExclude);

            if (m_cPlayer == null || m_cPlayer.IS_ALIVE == false || hsExclude.Contains(m_cPlayer) == true)
                return null;

            return Vector2.Distance(vFrom, m_cPlayer.POS) <= fRadius ? m_cPlayer : null;
        }

        // 폭발 효과처럼 탄이 다른 탄을 부를 때. 쏜 쪽은 모른다.
        public void Spawn_Projectile(int iProjectileID, Vector2 vPos, Vector2 vDir, PROJECTILE_SIDE eSide)
            => Spawn_Projectile(iProjectileID, vPos, vDir, eSide, null);

        private CProjectileCore Spawn_Projectile(int iProjectileID, Vector2 vPos, Vector2 vDir, PROJECTILE_SIDE eSide,
                                                 IImpactTarget cOwner, float fScale = 1f)
        {
            if (m_cProjectileTable == null || Has_Prefab(PREFAB_PROJECTILE) == false)
                return null;

            CProjectileInfo cInfo = m_cProjectileTable.Get_Info(iProjectileID);
            if (cInfo == null)
            {
                Debug.LogError($"[CStage_Manager] ProjectileInfo.csv에 탄 {iProjectileID}가 없습니다.");
                return null;
            }

            CProjectileDesc cDesc = new CProjectileDesc
            {
                // 적탄 · 플레이어 탄 모두 ENEMY_EFFECT에 올린다 — 연출 · 일시정지 때 함께 세워야 한다(2-8).
                eObjectType     = OBJECT_TYPE.ENEMY_EFFECT,
                strPrefabName   = PREFAB_PROJECTILE,
                cInfo           = cInfo,
                lstImpact       = Get_ImpactList(cInfo),
                cHost           = this,
                fCellSize       = m_cGrid.CELL_SIZE,
                vStartPos       = vPos,
                vDir            = vDir,
                eSide           = eSide,
                cOwner          = cOwner,
                fScale          = fScale,
            };

            GameObject goProjectile = CGameInstance.Instance.Reuse_Object(cDesc);
            if (goProjectile == null)
                return null;

            CProjectile cProjectile = goProjectile.GetComponent<CProjectile>();
            if (cProjectile == null || cProjectile.IS_EXPIRED == true)
                return null;

            m_lstProjectile.Add(cProjectile);
            return cProjectile.CORE;
        }

        private List<CImpactInfo> Get_ImpactList(CProjectileInfo cInfo)
        {
            if (m_dicImpactCache.TryGetValue(cInfo.iProjectileID, out List<CImpactInfo> lstImpact) == true)
                return lstImpact;

            lstImpact = new List<CImpactInfo>();
            for (int i = 0; i < cInfo.lstImpactID.Count; ++i)
            {
                CImpactInfo cImpact = m_cImpactTable != null ? m_cImpactTable.Get_Info(cInfo.lstImpactID[i]) : null;
                if (cImpact == null)
                {
                    Debug.LogError($"[CStage_Manager] 탄 {cInfo.iProjectileID}의 효과 {cInfo.lstImpactID[i]}가 "
                                 + "ImpactInfo.csv에 없습니다.");
                    continue;
                }

                lstImpact.Add(cImpact);
            }

            m_dicImpactCache.Add(cInfo.iProjectileID, lstImpact);
            return lstImpact;
        }
        #endregion 투사체 (IProjectileHost)

        #region 런 스킬 소환물 (IRunSkillHost)
        // 260916_영혼 수집가. 위 기믹 소환물과 같은 이유로 여기서 만들고 여기서 회수한다.
        public void Spawn_Soul()
        {
            if (Has_Prefab(PREFAB_SOUL) == false)
                return;

            Vector2Int vDesired = new Vector2Int(UnityEngine.Random.Range(0, m_cMapInfo.iGridWidth),
                                                 UnityEngine.Random.Range(0, m_cMapInfo.iGridHeight));
            if (m_cGrid.Try_Find_NearestCell(vDesired, CELL_STATE.EMPTY, SPAWN_SEARCH_RADIUS,
                                             out Vector2Int vCell) == false)
                return;

            CSoulDesc cDesc = new CSoulDesc
            {
                eObjectType     = OBJECT_TYPE.ENEMY_EFFECT,
                strPrefabName   = PREFAB_SOUL,
                cGrid           = m_cGrid,
                vCell           = vCell,
                fLifeTime       = SOUL_LIFETIME,
            };

            GameObject goSoul = CGameInstance.Instance.Reuse_Object(cDesc);
            if (goSoul == null)
                return;

            CSoul cSoul = goSoul.GetComponent<CSoul>();
            if (cSoul != null)
                m_lstSoul.Add(cSoul);
        }

        // 260917_투사체 무기(CRunSkillEffect_Weapon)
        public IImpactTarget Find_Enemy(Vector2 vFrom, TARGET_FIND eFind)
            => CTargetFinder_Utility.Find(m_lstEnemy, vFrom, eFind);

        // 260918_전체 마비 — 같은 출처 키로 걸어 두 번 터지면 새로 걸지 않고 긴 쪽으로 늘어난다(CImpactHandler).
        private static readonly object MASS_STUN_KEY = new object();

        public int Stun_AllEnemies(float fDuration)
        {
            if (fDuration <= 0f)
                return 0;

            int iCount = 0;
            for (int i = 0; i < m_lstEnemy.Count; ++i)
            {
                CEnemy cEnemy = m_lstEnemy[i];
                if (cEnemy == null || cEnemy.IS_ALIVE == false)
                    continue;

                cEnemy.IMPACT.Set_Stun(MASS_STUN_KEY, fDuration);
                // 걸렸다는 걸 몬스터마다 보여 준다 — 화면 플래시만으로는 누가 멈췄는지 안 읽힌다
                cEnemy.IMPACT.Set_WhiteOut(MASS_STUN_KEY, Mathf.Min(0.3f, fDuration));
                ++iCount;
            }

            if (iCount > 0)
                OnMassStun?.Invoke();

            return iCount;
        }

        public CProjectileCore Spawn_PlayerShot(int iProjectileID, Vector2 vPos, Vector2 vDir, float fScale = 1f)
            => Spawn_Projectile(iProjectileID, vPos, vDir, PROJECTILE_SIDE.PLAYER_SHOT, m_cPlayer, fScale);

        // 260921_다시 뽑기 · 버리기(2-10-1). 판 전체에서 센다
        public int REROLL_LEFT => m_iRerollLeft;
        public int BANISH_LEFT => m_iBanishLeft;

        public bool Try_UseReroll()
        {
            if (m_iRerollLeft <= 0)
                return false;

            --m_iRerollLeft;
            return true;
        }

        /// <summary> 그 선택지를 판이 끝날 때까지 다시 안 나오게 한다. 횟수가 없으면 false. </summary>
        public bool Try_Banish(CPickOption cOption)
        {
            if (cOption == null || m_iBanishLeft <= 0 || m_hsBanished.Add(cOption.KEY) == false)
                return false;

            --m_iBanishLeft;
            return true;
        }

        public bool Is_Banished(CPickOption cOption) => cOption != null && m_hsBanished.Contains(cOption.KEY);

        #region 분신 (260921)
        // 260921_분신은 피해를 주지도 받지도 않는다. 하는 일은 몬스터의 시선을 가져가는 것뿐이다(2-11-3).
        public bool Spawn_Decoy(Vector2 vPos, Vector2 vDir, float fDuration)
        {
            if (m_cDecoy != null || m_cMapInfo == null || Has_Prefab(PREFAB_DECOY) == false)
                return false;

            CDecoyDesc cDesc = new CDecoyDesc
            {
                eObjectType   = OBJECT_TYPE.ENEMY_EFFECT,
                strPrefabName = PREFAB_DECOY,
                cGrid         = m_cGrid,
                vStartPos     = vPos,
                vDir          = vDir,
                // 본체보다 조금 빠르게 달려 나가야 몬스터가 분신 쪽으로 방향을 튼다.
                fSpeed        = m_cMapInfo.fPlayerSpeed * 1.1f,
                fLifeTime     = fDuration,
            };

            GameObject goDecoy = CGameInstance.Instance.Reuse_Object(cDesc);
            if (goDecoy == null)
                return false;

            m_cDecoy = goDecoy.GetComponent<CDecoy>();
            return m_cDecoy != null;
        }

        private void Tick_Decoy()
        {
            if (m_cDecoy == null)
                return;

            if (m_cDecoy.IS_EXPIRED == true)
            {
                m_cDecoy = null;
                return;
            }

            // 몬스터에게 닿으면 분신만 사라진다 — 본체는 멀쩡하다.
            for (int i = 0; i < m_lstEnemy.Count; ++i)
            {
                CEnemy cEnemy = m_lstEnemy[i];
                if (cEnemy == null || cEnemy.IS_ALIVE == false)
                    continue;

                if (Vector2.Distance(cEnemy.POS, m_cDecoy.POS) > cEnemy.HIT_RANGE * m_cGrid.CELL_SIZE)
                    continue;

                m_cDecoy.Expire();
                m_cDecoy = null;
                return;
            }
        }
        #endregion 분신 (260921)

        #region 필드 아이템 (260920)
        public void Set_FieldItemTable(CCSVData_FieldItemInfo cTable, bool bEnabled)
        {
            m_cFieldItemTable   = cTable;
            m_bFieldItemEnabled = bEnabled;
        }

        /// <summary>
        /// 맵 위 아이템 하나를 미점령 칸에 놓는다. 무엇이 나올지는 표의 가중치가 정한다.
        /// 세 경로(웨이브 시작 · 시간마다 · 몬스터 처치)가 전부 이 함수를 지난다 — 상한도 여기 하나로 걸린다(1-1).
        /// </summary>
        /// <param name="vNearCell"> 이 칸 근처에 놓는다. null이면 맵 전체에서 무작위 </param>
        public bool Spawn_FieldItem(Vector2Int? vNearCell = null)
        {
            if (m_bFieldItemEnabled == false || m_cFieldItemTable == null || m_cMapInfo == null)
                return false;

            if (m_lstFieldItem.Count >= MAX_FIELD_ITEM || Has_Prefab(PREFAB_FIELD_ITEM) == false)
                return false;

            CFieldItemInfo cInfo = m_cFieldItemTable.Pick_Random();
            if (cInfo == null)
                return false;

            Vector2Int vDesired = vNearCell ?? new Vector2Int(UnityEngine.Random.Range(0, m_cMapInfo.iGridWidth),
                                                              UnityEngine.Random.Range(0, m_cMapInfo.iGridHeight));
            if (m_cGrid.Try_Find_NearestCell(vDesired, CELL_STATE.EMPTY, SPAWN_SEARCH_RADIUS,
                                             out Vector2Int vCell) == false)
                return false;

            CFieldItemDesc cDesc = new CFieldItemDesc
            {
                eObjectType   = OBJECT_TYPE.ENEMY_EFFECT,
                strPrefabName = PREFAB_FIELD_ITEM,
                cGrid         = m_cGrid,
                vCell         = vCell,
                fLifeTime     = cInfo.fLifeTime,
                iItemID       = cInfo.iItemID,
                eType         = cInfo.eType,
            };

            GameObject goItem = CGameInstance.Instance.Reuse_Object(cDesc);
            if (goItem == null)
                return false;

            CFieldItem cItem = goItem.GetComponent<CFieldItem>();
            if (cItem == null)
                return false;

            m_lstFieldItem.Add(cItem);
            return true;
        }

        // 웨이브를 시작할 때 몇 개 깔아 둔다 — 판이 다시 깔리므로(2-5) 매 웨이브 새로 뿌린다.
        private void Spawn_WaveFieldItems()
        {
            if (m_cMapInfo == null)
                return;

            for (int i = 0; i < m_cMapInfo.iFieldItemOnWave; ++i)
                Spawn_FieldItem();
        }

        // 시간마다 하나씩. 아무것도 안 하고 버티기만 해도 판이 조금씩 바뀐다.
        private void Tick_FieldItemSpawn(float fDeltaTime)
        {
            if (m_cMapInfo == null || m_cMapInfo.fFieldItemCool <= 0f)
                return;

            m_fFieldItemTimer += fDeltaTime;
            if (m_fFieldItemTimer < m_cMapInfo.fFieldItemCool)
                return;

            m_fFieldItemTimer = 0f;
            Spawn_FieldItem();
        }

        // 몬스터를 잡은 자리에 확률로 떨어뜨린다 — 런 스킬 무기를 고를 이유가 하나 더 생긴다.
        private void Drop_FieldItem(Vector2 vWorldPos)
        {
            if (m_cMapInfo == null || m_cMapInfo.fFieldItemDropRate <= 0f)
                return;

            // 260923_노획 본능(HUNT_LOOT) — 맵이 드롭을 아예 안 주는 경우(위에서 걸러짐)까지 만들어 내지는
            // 않는다. 이미 나오는 드롭의 확률만 올린다.
            if (UnityEngine.Random.value > m_cMapInfo.fFieldItemDropRate + m_fLootCardBonus)
                return;

            Spawn_FieldItem(m_cGrid.World_ToCell(vWorldPos));
        }

        /// <summary>
        /// 줍는 판정. 가까이 가면 줍고, **점령한 땅 안에 들어간 아이템도 주운 것으로 본다**(260920) —
        /// 내 땅으로 덮었는데 아이템이 조용히 사라지면 크게 먹을수록 손해가 된다.
        /// </summary>
        private void Tick_FieldItem()
        {
            if (m_cPlayer == null)
                return;

            Vector2 vPlayerPos   = m_cPlayer.transform.position;
            float   fPickupRange = (SOUL_PICKUP_RADIUS_BASE + m_cPlayer.PICKUP_RADIUS) * m_cGrid.CELL_SIZE;

            for (int i = m_lstFieldItem.Count - 1; i >= 0; --i)
            {
                CFieldItem cItem = m_lstFieldItem[i];

                if (cItem == null || cItem.IS_EXPIRED == true)
                {
                    m_lstFieldItem.RemoveAt(i);
                    continue;
                }

                bool bInOwned = m_cGrid.Is_OwnedPoint(m_cGrid.World_ToGrid(cItem.POS));
                if (bInOwned == false && Vector2.Distance(cItem.POS, vPlayerPos) > fPickupRange)
                    continue;

                cItem.Expire();
                Apply_FieldItem(cItem.TYPE, cItem.ITEM_ID);
            }
        }

        /// <summary>
        /// 아이템 효과. 세 종류 전부 이미 있는 길을 그대로 탄다 —
        /// 마비는 Stun_AllEnemies(화면 연출까지 OnMassStun), 회복은 CPlayer.Add_Life, 자석은 아래 Collect_AllPickup.
        /// </summary>
        private void Apply_FieldItem(FIELD_ITEM_TYPE eType, int iItemID)
        {
            CFieldItemInfo cInfo = m_cFieldItemTable?.Find(iItemID);

            switch (eType)
            {
                case FIELD_ITEM_TYPE.MASS_STUN:
                    Stun_AllEnemies(cInfo != null ? cInfo.fDuration : 2f);
                    break;

                case FIELD_ITEM_TYPE.HEAL_LIFE:
                    m_cPlayer?.Add_Life(cInfo != null ? Mathf.Max(1, Mathf.RoundToInt(cInfo.fValue)) : 1);
                    break;

                case FIELD_ITEM_TYPE.MAGNET_ALL:
                    Collect_AllPickup();
                    break;
            }

            OnFieldItemUsed?.Invoke(eType);
        }

        /// <summary> 전체 자석 — 맵 위 픽업을 전부 그 자리에서 먹는다. 다른 아이템도 같이 발동한다. </summary>
        private void Collect_AllPickup()
        {
            for (int i = m_lstSoul.Count - 1; i >= 0; --i)
            {
                CSoul cSoul = m_lstSoul[i];
                if (cSoul == null || cSoul.IS_EXPIRED == true)
                    continue;

                cSoul.Expire();
                m_cPlayer?.On_SoulCollected();
            }

            // 자석이 자석을 다시 부르지 않게 목록을 먼저 떠 둔다(먹는 도중에 새 아이템이 끼어들 수 있다).
            for (int i = m_lstFieldItem.Count - 1; i >= 0; --i)
            {
                CFieldItem cItem = m_lstFieldItem[i];
                if (cItem == null || cItem.IS_EXPIRED == true || cItem.TYPE == FIELD_ITEM_TYPE.MAGNET_ALL)
                    continue;

                cItem.Expire();
                Apply_FieldItem(cItem.TYPE, cItem.ITEM_ID);
            }
        }
        #endregion 필드 아이템 (260920)

        private void Tick_Soul()
        {
            if (m_cPlayer == null)
                return;

            Vector2 vPlayerPos = m_cPlayer.transform.position;
            float   fPickupRange = (SOUL_PICKUP_RADIUS_BASE + m_cPlayer.PICKUP_RADIUS) * m_cGrid.CELL_SIZE;

            for (int i = m_lstSoul.Count - 1; i >= 0; --i)
            {
                CSoul cSoul = m_lstSoul[i];

                if (cSoul == null || cSoul.IS_EXPIRED == true)
                {
                    m_lstSoul.RemoveAt(i);
                    continue;
                }

                // 260920_가까이 갔거나, 그 칸을 점령해 내 땅이 됐으면 주운 것이다.
                bool bInOwned = m_cGrid.Is_OwnedPoint(m_cGrid.World_ToGrid(cSoul.POS));
                if (bInOwned == true || Vector2.Distance(cSoul.POS, vPlayerPos) <= fPickupRange)
                {
                    cSoul.Expire();
                    m_cPlayer.On_SoulCollected();
                }
            }
        }
        #endregion 런 스킬 소환물 (IRunSkillHost)

        // 260917_회전탄 · 몽둥이는 투사체(ProjectileInfo 20 · 22)로 옮겼다. 둘 다 그려지지 않았고,
        // 회전탄은 반경 안의 몬스터를 매 프레임 때려 즉사시켰다. 피해 · 넉백 · 적탄 지우기는 이제 Tick_Projectile이 본다.

        #region 콜백
        private void On_PlayerCapture(int iCapturedCount)
        {
            if (m_eState != STAGE_STATE.PLAYING)
                return;

            // 260920_가둔 몬스터는 죽는다(2-3). 보상보다 먼저 처리해 둔다 —
            // 죽은 자리에 떨어지는 아이템이 이번 점령의 결과로 같이 읽힌다.
            int iTrapped = Kill_EnemiesInOwned();
            Notify_CaptureStyle(iCapturedCount, iTrapped);

            Grant_CaptureReward(iCapturedCount);

            // 260912_웨이브를 넘기기 전에 카드부터 본다.
            // 순서가 반대면 판이 넘어가며 점령률이 0으로 돌아가 카드를 영영 못 받는다(2-10-1).
            Check_CardReady();

            CWaveInfo cWave = m_cMapInfo.Get_Wave(m_iWave);
            if (cWave != null && m_cGrid.OWNED_RATIO >= cWave.fClearRatio)
                Next_Wave();
        }

        // 260922_점령에서 나오는 현란한 동작 — 가둬 잡기 · 대형 점령 · 연속 점령. 한 번에 여럿 뜰 수 있다(화면이 쌓아 보여 준다)
        private void Notify_CaptureStyle(int iCapturedCount, int iTrapped)
        {
            int iChain = m_cStyle.On_Capture();

            if (iTrapped > 0)
                OnStylish?.Invoke(STYLE_ACTION.TRAP, iTrapped);

            float fRatio = m_cGrid.PLAYABLE_COUNT > 0 ? (float)iCapturedCount / m_cGrid.PLAYABLE_COUNT : 0f;
            if (CStyleTracker.Is_BigCapture(fRatio) == true)
                OnStylish?.Invoke(STYLE_ACTION.BIG_CAPTURE, 1);

            if (iChain >= 2)
                OnStylish?.Invoke(STYLE_ACTION.CHAIN, iChain);
        }

        // 260918_점령 리스크·보상 연결 — 한 번에 닫은 도형이 맵 전체에서 차지하는 비율이 클수록
        // 배율이 커진다(CaptureRewardInfo.csv). "안전하게 조금씩"과 "위험을 감수하고 크게 한 방" 사이에
        // 실제 이득 차이를 만들기 위해서다 — 지금까지는 칸 수만 그대로 더해 차이가 전혀 없었다.
        private void Grant_CaptureReward(int iCapturedCount)
        {
            if (iCapturedCount <= 0 || m_cMapInfo == null || m_cMapInfo.iCoinPerCell <= 0)
                return;

            float fRatio      = m_cGrid.PLAYABLE_COUNT > 0 ? (float)iCapturedCount / m_cGrid.PLAYABLE_COUNT : 0f;
            float fMultiplier = m_cCaptureRewardTable != null ? m_cCaptureRewardTable.Get_Multiplier(fRatio) : 1f;
            int   iCoin       = Mathf.RoundToInt(iCapturedCount * m_cMapInfo.iCoinPerCell * fMultiplier);

            if (iCoin <= 0)
                return;

            m_iStageCoin += iCoin;

            // 260918_파티클이 날아가기 시작할 자리 — 점령한 영역의 정확한 중심 대신 플레이어 위치를 쓴다.
            // 도형을 닫는 순간 플레이어가 그 영역 경계에 있으므로 근사치로 충분하다.
            Vector2 vFrom = m_cPlayer != null ? (Vector2)m_cPlayer.transform.position : m_cGrid.WORLD_CENTER;
            OnCoinGained?.Invoke(iCoin, vFrom);
        }

        private void On_PlayerDead()
        {
            Set_State(STAGE_STATE.FAIL);
        }

        // 260904_스테이지가 끝나면 액터를 세운다.
        // Tick()은 PLAYING이 아니면 빠져나가지만 플레이어/몬스터의 Tick은 Engine 레이어가 직접 돌린다.
        // 그대로 두면 CLEAR/FAIL 이후에도 계속 움직이며 땅을 먹고 목숨이 더 깎인다.
        // Engine이 이미 레이어별 타임스케일을 갖고 있으므로 별도의 정지 플래그를 만들지 않는다.
        private static void Set_ActorTimeScale(float fTimeScale)
        {
            CGameInstance.Instance.Set_LayerTimeScale(OBJECT_TYPE.PLAYER, fTimeScale);
            CGameInstance.Instance.Set_LayerTimeScale(OBJECT_TYPE.ENEMY, fTimeScale);
            // 260904_투사체·거미줄은 ENEMY_EFFECT 레이어에 올라간다. 같이 세우지 않으면 탄만 계속 날아간다.
            CGameInstance.Instance.Set_LayerTimeScale(OBJECT_TYPE.ENEMY_EFFECT, fTimeScale);
        }

        private void Set_State(STAGE_STATE eState)
        {
            if (m_eState == eState)
                return;

            m_eState = eState;

            if (eState == STAGE_STATE.CLEAR || eState == STAGE_STATE.FAIL)
                Set_ActorTimeScale(0f);

            Debug.Log($"[CStage_Manager] STAGE {eState} — {m_iWave}웨이브, 점령률 {m_cGrid.OWNED_RATIO:P1}");
            OnStateChanged?.Invoke(eState);
        }
        #endregion 콜백
    }
}
