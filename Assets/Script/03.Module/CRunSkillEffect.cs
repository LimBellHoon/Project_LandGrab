using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260916_런 전용 스킬 효과 — CSkillEffect(2-11)와 같은 조합 구조다.
    /// <summary>
    /// 다른 점은 하나뿐이다 — CSkillEffect는 CPlayer에 딱 하나만 붙지만,
    /// 이건 CPlayer가 <see cref="System.Collections.Generic.List{T}"/>로 여러 개를 동시에 들고 있는다
    /// (뱀서라이크처럼 스킬을 계속 쌓아 가는 방식이라 배타적 선택이 아니다).
    /// 레벨이 오를 때마다(처음 획득 포함) <see cref="On_LevelChanged"/> 한 번만 불린다 —
    /// 매 프레임 다시 계산할 값이 아니면 여기서 한 번 걸어 두고 <see cref="Release"/>에서 되돌린다.
    /// </summary>
    public abstract class CRunSkillEffect
    {
        protected CPlayer m_cOwner;
        protected int      m_iLevel;
        // 260917_각성. null이면 각성 전이다.
        protected CAwakenInfo m_cAwaken;

        public bool IS_AWAKENED => m_cAwaken != null;

        /// <summary> 260917_지금 분노가 터져 있는가. 분노조절못해만 true를 낸다(광란의 칼바람이 읽는다). </summary>
        public virtual bool IS_FEVER => false;

        /// <summary> 260916_CPlayer가 이미 들고 있는 효과인지 찾을 때 쓴다(런타임 타입 비교 대신). </summary>
        public RUN_SKILL_TYPE TYPE { get; private set; }

        public static CRunSkillEffect Create(RUN_SKILL_TYPE eType)
        {
            CRunSkillEffect cEffect;

            switch (eType)
            {
                case RUN_SKILL_TYPE.MOONWALK:  cEffect = new CRunSkillEffect_Moonwalk();  break;
                case RUN_SKILL_TYPE.EDGE_WRAP: cEffect = new CRunSkillEffect_EdgeWrap();  break;
                case RUN_SKILL_TYPE.MAGNET:    cEffect = new CRunSkillEffect_Magnet();    break;
                case RUN_SKILL_TYPE.EVASION:   cEffect = new CRunSkillEffect_Evasion();   break;
                case RUN_SKILL_TYPE.RAGE:      cEffect = new CRunSkillEffect_Rage();      break;
                case RUN_SKILL_TYPE.SOUL_COLLECTOR: cEffect = new CRunSkillEffect_SoulCollector(); break;
                case RUN_SKILL_TYPE.ORBIT:     cEffect = new CRunSkillEffect_Orbit();     break;
                case RUN_SKILL_TYPE.CLUB:      cEffect = new CRunSkillEffect_Club();      break;

                // 260917_투사체 무기는 전부 같은 모듈이다 — 무엇을 쏠지는 표가 정한다.
                case RUN_SKILL_TYPE.MAGIC_BOLT:
                case RUN_SKILL_TYPE.LASER_BEAM:
                case RUN_SKILL_TYPE.BOOMERANG:
                case RUN_SKILL_TYPE.BOUNCE_SHOT:
                case RUN_SKILL_TYPE.STUN_SHOT:   cEffect = new CRunSkillEffect_Weapon(); break;     // 260918_마비탄도 쏘는 무기다


                default: return null;
            }

            cEffect.TYPE = eType;
            return cEffect;
        }

        public void Initialize(CPlayer cOwner)
        {
            m_cOwner = cOwner;
        }

        /// <summary> 레벨이 바뀔 때(1레벨 최초 획득 포함) 불린다. </summary>
        public virtual void On_LevelChanged(CRunSkillInfo cInfo, int iLevel) => m_iLevel = iLevel;

        // 260917_각성 — 새 스킬이 아니라 그 스킬의 강화된 형태라 같은 모듈 안에서 분기한다(문서 3장).
        /// <summary> 각성할 때 한 번 불린다. 레벨 수치를 다시 계산해야 하는 효과는 여기서 다시 건다. </summary>
        public virtual void On_Awaken(CAwakenInfo cInfo) => m_cAwaken = cInfo;

        public virtual void Tick(float fDeltaTime) { }

        /// <summary> 스킬을 잃을 때(스테이지 종료) — 걸어 둔 플래그·구독을 되돌린다. </summary>
        public virtual void Release() { }

        // 260916_맵 위에 무언가를 놓아야 하는 스킬(영혼 수집가)만 쓴다. 나머지는 그냥 무시한다.
        public virtual void Set_Host(IRunSkillHost cHost) { }

        /// <summary> 영혼을 주웠을 때 CPlayer가 모든 효과에 알린다 — 관심 없는 효과는 무시한다. </summary>
        public virtual void On_SoulCollected() { }

        /// <summary> 회전탄/몽둥이가 몬스터를 때렸을 때 CPlayer가 모든 효과에 알린다(분노 게이지용). </summary>
        public virtual void On_MonsterHit() { }

    }

    /// <summary> 월보 — 점령지 내부(이미지 위)도 통과할 수 있게 된다. 레벨 개념 없이 on/off뿐이다. </summary>
    public class CRunSkillEffect_Moonwalk : CRunSkillEffect
    {
        public override void On_LevelChanged(CRunSkillInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cOwner.Set_MoveFlag_AllowOwnedInterior(true);
        }

        public override void Release() => m_cOwner.Set_MoveFlag_AllowOwnedInterior(false);
    }

    /// <summary> 어디로든 신발 — 맵 좌우 끝을 잇는다. 레벨 개념 없이 on/off뿐이다. </summary>
    public class CRunSkillEffect_EdgeWrap : CRunSkillEffect
    {
        public override void On_LevelChanged(CRunSkillInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cOwner.Set_MoveFlag_EdgeWrap(true);
        }

        public override void Release() => m_cOwner.Set_MoveFlag_EdgeWrap(false);
    }

    /// <summary> 자석 — 습득 범위(셀). CPlayer에 값만 걸어 두고, 실제 습득은 픽업 쪽(다음 단계)이 읽는다. </summary>
    public class CRunSkillEffect_Magnet : CRunSkillEffect
    {
        public override void On_LevelChanged(CRunSkillInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cOwner.Set_PickupRadius(cInfo.Get_Value(iLevel));
        }

        public override void Release() => m_cOwner.Set_PickupRadius(0f);
    }

    /// <summary>
    /// 회피 — 회피율을 올린다. CPlayer.Add_Evasion은 누적만 하는 API라, 레벨업 때는
    /// '이전 레벨과의 차이'만 더한다 — 총량을 다시 더하면 두 배로 올라간다.
    /// </summary>
    public class CRunSkillEffect_Evasion : CRunSkillEffect
    {
        private float m_fApplied;

        public override void On_LevelChanged(CRunSkillInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);

            float fValue = cInfo.Get_Value(iLevel);
            m_cOwner.Add_Evasion(fValue - m_fApplied);
            m_fApplied = fValue;
        }

        // 260916_회피는 되돌리는 API가 없다(음수 방지) — 하지만 스테이지가 끝나면 어차피
        // CPlayer.Initialize가 회피를 CSV 기준값으로 새로 세팅하므로 다음 판에 새지 않는다.
    }

    /// <summary>
    /// 분노조절못해 — 달리거나 맞거나 몬스터를 때릴 때마다 게이지가 차고, 가득 차면
    /// 짧게 매우 빨라진다. "맞을 때"는 <see cref="On_PlayerDamaged"/>(OnDamaged 구독),
    /// "때릴 때"는 <see cref="On_MonsterHit"/>(회전탄/몽둥이가 명중했을 때 CPlayer가 불러 준다).
    /// </summary>
    public class CRunSkillEffect_Rage : CRunSkillEffect
    {
        private const float GAUGE_PER_SECOND_MOVING = 0.12f;  // 기준값. 레벨 배율이 곱해진다
        private const float GAUGE_PER_HIT           = 0.25f;  // 맞을 때/때릴 때 공통
        private const float FEVER_SPEED_BONUS       = 1.2f;   // Add_SkillSpeed 인자 — 2.2배
        private const float FEVER_DURATION          = 2.5f;   // 초

        private float m_fGaugeRateScale = 1f;
        private float m_fGauge;
        private float m_fFeverTimer;    // 260917_피버가 남은 시간 — 광란의 칼바람이 읽는다

        public override bool IS_FEVER => m_fFeverTimer > 0f;

        public override void On_LevelChanged(CRunSkillInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);

            if (m_cOwner != null && iLevel == 1)
                m_cOwner.OnDamaged += On_PlayerDamaged;

            m_fGaugeRateScale = cInfo.Get_Value(iLevel);
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_fFeverTimer > 0f)
                m_fFeverTimer -= fDeltaTime;

            if (m_cOwner.IS_MOVING == true)
                Add_Gauge(GAUGE_PER_SECOND_MOVING * m_fGaugeRateScale * fDeltaTime);
        }

        public override void On_MonsterHit() => Add_Gauge(GAUGE_PER_HIT * m_fGaugeRateScale);

        private void On_PlayerDamaged() => Add_Gauge(GAUGE_PER_HIT * m_fGaugeRateScale);

        private void Add_Gauge(float fAmount)
        {
            m_fGauge = Mathf.Clamp01(m_fGauge + fAmount);
            if (m_fGauge < 1f)
                return;

            m_fGauge = 0f;
            m_fFeverTimer = FEVER_DURATION;
            m_cOwner.Add_SkillSpeed(FEVER_SPEED_BONUS, FEVER_DURATION);
        }

        public override void Release()
        {
            if (m_cOwner != null)
                m_cOwner.OnDamaged -= On_PlayerDamaged;
        }
    }

    /// <summary>
    /// 영혼 수집가 — 주기적으로 맵에 영혼을 떨어뜨리고, 주울 때마다 이번 판 한정으로
    /// 영구히 빨라진다. 발동 주기는 기믹(CEnemyGimmick)과 같은 자리에서 스스로 재고,
    /// 실제 습득 판정은 CStage_Manager가 한다(2-6과 같은 구조 — 화면 없이 검증 못 할 부분만 넘긴다).
    /// </summary>
    public class CRunSkillEffect_SoulCollector : CRunSkillEffect
    {
        private const float SPAWN_INTERVAL = 4f;   // 초

        private IRunSkillHost m_cHost;
        private float         m_fSpawnTimer = SPAWN_INTERVAL;
        private float         m_fSpeedGainPerSoul;

        public override void Set_Host(IRunSkillHost cHost) => m_cHost = cHost;

        public override void On_LevelChanged(CRunSkillInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_fSpeedGainPerSoul = cInfo.Get_Value(iLevel);
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cHost == null)
                return;

            m_fSpawnTimer -= fDeltaTime;
            if (m_fSpawnTimer > 0f)
                return;

            m_fSpawnTimer = SPAWN_INTERVAL;
            m_cHost.Spawn_Soul();
        }

        public override void On_SoulCollected()
        {
            // 260912_카드 속도 배율을 그대로 쓴다 — "판이 끝날 때까지 유지되는 영구 가산"이
            // 이미 CPlayer.Add_CardSpeed가 하는 일과 정확히 같다(중복 필드를 만들지 않는다).
            m_cOwner.Add_CardSpeed(m_fSpeedGainPerSoul);
        }
    }

    /// <summary>
    /// 회전탄 — 주위를 도는 탄이 적탄을 없애고 몬스터에게 피해를 준다. 레벨업마다 탄이 1개씩 늘어난다(1레벨=1개).
    ///
    /// 260917_투사체로 옮겼다. 예전에는 좌표만 계산해 스테이지가 판정했는데 **아무것도 그려지지 않았고**,
    /// 반경 안의 몬스터를 매 프레임 때려 닿자마자 죽였다. 이제 ProjectileInfo의 회전 궤도탄(ORBIT 이동)을
    /// 필요한 수만큼 띄워 두고 붙잡는다 — 그리기 · 닿는 순간만 피해 · 적탄 지우기(CANCEL_SHOT)가 탄 쪽에 다 있다.
    ///
    /// 수가 바뀌면(레벨업 · 각성 · 분노) 전부 거두고 같은 간격으로 다시 띄운다. 궤도가 어긋나지 않게 하는 가장 간단한 길이다.
    /// </summary>
    public class CRunSkillEffect_Orbit : CRunSkillEffect
    {
        private readonly List<CProjectileCore> m_lstCore   = new List<CProjectileCore>();
        private readonly List<int>             m_lstSerial = new List<int>();

        private IRunSkillHost m_cHost;
        private CRunSkillInfo m_cInfo;
        private int           m_iBaseCount;     // 레벨이 정한 수. 각성 보너스와 피버 배율은 매번 따로 곱한다
        private int           m_iSpawnedID;     // 지금 떠 있는 탄의 종류(피버 동안 바뀐다)

        public override void Set_Host(IRunSkillHost cHost) => m_cHost = cHost;

        public override void On_LevelChanged(CRunSkillInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo      = cInfo;
            m_iBaseCount = Mathf.RoundToInt(cInfo.Get_Value(iLevel));
        }

        // 260917_광란의 칼바람(회전탄 + 분노). 분노가 터져 있는 동안만 수가 불어나고 빠른 탄으로 바뀐다 —
        // 두 스킬이 서로를 모른 채 CPlayer.IS_FEVER 하나로만 겹친다(2-10-2의 흔들림/펀치와 같은 결).
        public int COUNT
        {
            get
            {
                if (m_iBaseCount <= 0)
                    return 0;

                if (IS_AWAKENED == false)
                    return m_iBaseCount;

                int iCount = m_iBaseCount + Mathf.RoundToInt(m_cAwaken.Get_Param("COUNT_BONUS", 0f));
                return Is_Frenzy() == true
                     ? Mathf.RoundToInt(iCount * m_cAwaken.Get_Param("FEVER_COUNT_RATE", 1f)) : iCount;
            }
        }

        public int PROJECTILE_ID
        {
            get
            {
                if (Is_Frenzy() == true)
                {
                    int iFever = Mathf.RoundToInt(m_cAwaken.Get_Param("FEVER_PROJECTILE_ID", 0f));
                    if (iFever > 0)
                        return iFever;
                }
                return m_cInfo != null ? m_cInfo.iProjectileID : 0;
            }
        }

        /// <summary> 지금 떠 있는(아직 살아 있는) 탄 수 </summary>
        public int ALIVE_COUNT
        {
            get
            {
                int iAlive = 0;
                for (int i = 0; i < m_lstCore.Count; ++i)
                {
                    if (Is_Mine(i) == true)
                        ++iAlive;
                }
                return iAlive;
            }
        }

        private bool Is_Frenzy() => IS_AWAKENED == true && m_cOwner != null && m_cOwner.IS_FEVER == true;

        // 붙잡아 둔 탄이 아직 내가 띄운 그 탄인가 — 풀에서 다른 탄으로 재사용됐으면 번호가 다르다.
        private bool Is_Mine(int iIndex)
            => m_lstCore[iIndex] != null && m_lstCore[iIndex].SERIAL == m_lstSerial[iIndex]
               && m_lstCore[iIndex].IS_EXPIRED == false;

        public override void Tick(float fDeltaTime)
        {
            if (m_cHost == null || m_cOwner == null)
                return;

            int iWant = COUNT;
            int iID   = PROJECTILE_ID;
            if (iID <= 0)
                return;

            if (ALIVE_COUNT == iWant && m_lstCore.Count == iWant && iID == m_iSpawnedID)
                return;

            Collect_All();

            for (int i = 0; i < iWant; ++i)
            {
                // 궤도 이동은 쏜 방향을 시작 각도로 쓴다 — 같은 간격으로 흩어 놓는다.
                float fRad = 360f / iWant * i * Mathf.Deg2Rad;
                CProjectileCore cCore = m_cHost.Spawn_PlayerShot(iID, m_cOwner.POS, new Vector2(Mathf.Cos(fRad), Mathf.Sin(fRad)));
                if (cCore == null)
                    continue;

                m_lstCore.Add(cCore);
                m_lstSerial.Add(cCore.SERIAL);
            }

            m_iSpawnedID = iID;
        }

        private void Collect_All()
        {
            for (int i = 0; i < m_lstCore.Count; ++i)
            {
                if (Is_Mine(i) == true)
                    m_lstCore[i].Expire();
            }

            m_lstCore.Clear();
            m_lstSerial.Clear();
        }

        public override void Release() => Collect_All();
    }

    /// <summary>
    /// 몽둥이 — 바라보는 방향으로 주기적으로 휘둘러 맞은 몬스터를 넉백시킨다. 레벨업마다
    /// 판정 반경이 커진다. 뱀서라이크의 다른 무기들처럼 버튼 없이 자동으로 발동한다 —
    /// "액티브"는 버튼 여부가 아니라 쿨타임을 가진 효과라는 뜻이다(패시브는 상시 적용).
    ///
    /// 260917_판정을 투사체로 옮겼다(회전탄과 같은 이유 — 아무것도 그려지지 않았다). 휘두르면
    /// ProjectileInfo 22(짧게 커지는 원)를 레벨 반경만큼 키워 띄우고, 피해 · 넉백(ImpactInfo 7)은 그 탄이 넣는다.
    /// 여기는 '지금 휘두를 차례인가'와 반경만 안다.
    ///
    /// 260918_뱀서라이크가 아니라 베기 스킬이 꼭 필요하진 않지만 지우지 않고, 크게(반경 3배, RunSkillInfo) ·
    /// **좌우 양쪽에 한 번에** 휘두르게 바꿨다. 바라보는 방향을 따르지 않으므로 멈춰 있어도 휘두른다.
    /// </summary>
    public class CRunSkillEffect_Club : CRunSkillEffect
    {
        private const float SWING_INTERVAL = 1.2f;     // 초


        private IRunSkillHost m_cHost;
        private CRunSkillInfo m_cInfo;
        private float m_fSwingTimer;
        private float m_fRadiusCells;

        public override void Set_Host(IRunSkillHost cHost) => m_cHost = cHost;

        public override void On_LevelChanged(CRunSkillInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo        = cInfo;
            m_fRadiusCells = cInfo.Get_Value(iLevel);
        }

        // 260917_반격의 몽둥이(몽둥이 + 회피). 회피하는 순간 쿨을 비워 다음 프레임에 곧바로 휘두르게 한다.
        public override void On_Awaken(CAwakenInfo cInfo)
        {
            base.On_Awaken(cInfo);

            if (m_cOwner != null)
                m_cOwner.OnEvade += On_OwnerEvade;
        }

        public int   PROJECTILE_ID => m_cInfo != null ? m_cInfo.iProjectileID : 0;
        public float RADIUS_CELLS  => m_fRadiusCells * (IS_AWAKENED == true ? m_cAwaken.Get_Param("RADIUS_RATE", 1f) : 1f);
        public bool  IS_READY      => m_fSwingTimer <= 0f;

        private void On_OwnerEvade() => m_fSwingTimer = 0f;

        public override void Tick(float fDeltaTime)
        {
            if (m_fSwingTimer > 0f)
            {
                m_fSwingTimer -= fDeltaTime;
                return;
            }

            if (m_cHost == null || m_cOwner == null || PROJECTILE_ID <= 0)
                return;

            if (Consume_Swing(out float fRadiusCells) == false)
                return;

            // 원의 가장자리가 몸에 닿게 반경만큼 옆으로 띄운다 — 몸에 겹치면 뒤쪽까지 맞아 '좌우'가 흐려진다.
            m_cHost.Spawn_PlayerShot(PROJECTILE_ID, m_cOwner.Get_OffsetPoint(Vector2.left * fRadiusCells), Vector2.left, fRadiusCells);
            m_cHost.Spawn_PlayerShot(PROJECTILE_ID, m_cOwner.Get_OffsetPoint(Vector2.right * fRadiusCells), Vector2.right, fRadiusCells);
        }

        /// <returns> 휘둘렀으면 true(그 즉시 쿨이 다시 찬다). fRadiusCells에 판정 반경(셀) — 탄 크기에 곱한다 </returns>
        public bool Consume_Swing(out float fRadiusCells)
        {
            fRadiusCells = RADIUS_CELLS;

            if (m_fSwingTimer > 0f)
                return false;

            m_fSwingTimer = SWING_INTERVAL * (IS_AWAKENED == true ? m_cAwaken.Get_Param("COOL_RATE", 1f) : 1f);
            return true;
        }

        public override void Release()
        {
            if (m_cOwner != null)
                m_cOwner.OnEvade -= On_OwnerEvade;
        }
    }

    // 260917_투사체 무기 — 일정 시간마다 저절로 쏜다(뱀서라이크). 마법탄 · 레이저 · 부메랑 · 튕기는 탄이 전부 이 모듈이다.
    /// <summary>
    /// 무엇을(iProjectileID) · 얼마마다(fCool) · 어떤 모양으로(eFirePattern) · 누구에게(eTargetFind)는 RunSkillInfo.csv가,
    /// 레벨은 한 번에 쏘는 발 수를 정한다. 각성하면 AwakenInfo.csv가 탄 · 패턴 · 수 · 쿨을 덮어쓴다.
    ///
    /// 몬스터가 하나도 없으면 쏘지 않고 쿨을 찬 채로 기다린다 — 허공에 쏘면 몬스터가 들어오는 순간 쿨이 돌고 있어 억울하다.
    /// 판정 · 탄 생성은 스테이지가 한다(IRunSkillHost) — 몬스터 목록과 탄 풀이 거기 있다.
    /// </summary>
    public class CRunSkillEffect_Weapon : CRunSkillEffect
    {
        private readonly CProjectileFirer m_cFirer = new CProjectileFirer();

        private IRunSkillHost   m_cHost;
        private CRunSkillInfo   m_cInfo;
        private IImpactTarget   m_cTarget;
        private float           m_fCoolTimer;

        public int      PROJECTILE_ID   => IS_AWAKENED == true && m_cAwaken.iProjectileID > 0 ? m_cAwaken.iProjectileID
                                         : m_cInfo != null ? m_cInfo.iProjectileID : 0;
        public float    COOL            => m_cInfo == null ? 0f
                                         : m_cInfo.fCool * (IS_AWAKENED == true ? m_cAwaken.Get_Param("COOL_RATE", 1f) : 1f);
        public int      COUNT           => m_cFirer.COUNT;
        public float    COOL_REMAIN     => m_fCoolTimer;

        public override void Set_Host(IRunSkillHost cHost) => m_cHost = cHost;

        public override void On_LevelChanged(CRunSkillInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;
            Refresh_Firer();
        }

        public override void On_Awaken(CAwakenInfo cInfo)
        {
            base.On_Awaken(cInfo);
            Refresh_Firer();
        }

        private void Refresh_Firer()
        {
            if (m_cInfo == null)
                return;

            int iCount = Mathf.RoundToInt(m_cInfo.Get_Value(m_iLevel));
            FIRE_PATTERN ePattern = m_cInfo.eFirePattern;
            float fAngle = m_cInfo.fFireAngle;

            if (IS_AWAKENED == true)
            {
                iCount += Mathf.RoundToInt(m_cAwaken.Get_Param("COUNT_BONUS", 0f));
                iCount  = Mathf.RoundToInt(m_cAwaken.Get_Param("COUNT_OVERRIDE", iCount));
                fAngle  = m_cAwaken.Get_Param("FIRE_ANGLE", fAngle);
                if (m_cAwaken.bOverridePattern == true)
                    ePattern = m_cAwaken.eFirePattern;
            }

            m_cFirer.Setup(ePattern, iCount, fAngle, 0f, Spawn_Shot);
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cHost == null || m_cInfo == null || m_cOwner == null)
                return;

            Vector2 vFrom = m_cOwner.POS;

            // 연발 중에는 처음 노린 대상을 계속 노린다. 죽었으면 새로 찾는다.
            if (m_cFirer.IS_BURSTING == true)
            {
                if (m_cTarget == null || m_cTarget.IS_ALIVE == false)
                    m_cTarget = m_cHost.Find_Enemy(vFrom, m_cInfo.eTargetFind);

                if (m_cTarget != null)
                    m_cFirer.Tick(fDeltaTime, m_cTarget.POS - vFrom);
                return;
            }

            if (m_fCoolTimer > 0f)
            {
                m_fCoolTimer -= fDeltaTime;
                return;
            }

            m_cTarget = m_cHost.Find_Enemy(vFrom, m_cInfo.eTargetFind);
            if (m_cTarget == null)
                return;     // 쏠 대상이 없다 — 쿨을 찬 채로 기다린다

            m_fCoolTimer = COOL;
            m_cFirer.Fire(m_cTarget.POS - vFrom);
        }

        private void Spawn_Shot(Vector2 vDir) => m_cHost?.Spawn_PlayerShot(PROJECTILE_ID, m_cOwner.POS, vDir);
    }
}
