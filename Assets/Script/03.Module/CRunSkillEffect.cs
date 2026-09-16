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

        public virtual void Tick(float fDeltaTime) { }

        /// <summary> 스킬을 잃을 때(스테이지 종료) — 걸어 둔 플래그·구독을 되돌린다. </summary>
        public virtual void Release() { }

        // 260916_맵 위에 무언가를 놓아야 하는 스킬(영혼 수집가)만 쓴다. 나머지는 그냥 무시한다.
        public virtual void Set_Host(IRunSkillHost cHost) { }

        /// <summary> 영혼을 주웠을 때 CPlayer가 모든 효과에 알린다 — 관심 없는 효과는 무시한다. </summary>
        public virtual void On_SoulCollected() { }

        /// <summary> 회전탄/몽둥이가 몬스터를 때렸을 때 CPlayer가 모든 효과에 알린다(분노 게이지용). </summary>
        public virtual void On_MonsterHit() { }

        // 260916_회전탄 — 플레이어 중심 기준 상대 오프셋(셀)만 돌려준다. 월드 변환·그리드 참조는
        // CPlayer(그리드를 아는 쪽)가 한다 — 효과 쪽을 화면/그리드 없이 계속 테스트하기 위해서다.
        /// <returns> 이 효과가 회전탄을 갖고 있으면 true, lstOffsetCells에 오프셋을 채운다. </returns>
        public virtual bool Try_Get_OrbitOffsets(List<Vector2> lstOffsetCells) => false;

        // 260916_몽둥이 — "지금 휘두를 차례인가"만 판단한다. 실제 위치 계산(플레이어 위치 +
        // 바라보는 방향)은 마찬가지로 CPlayer가 한다.
        /// <returns> 이번 프레임에 휘둘렀으면 true(그 즉시 쿨타임이 다시 찬다). fRadiusCells에 판정 반경(셀). </returns>
        public virtual bool Consume_Swing(out float fRadiusCells)
        {
            fRadiusCells = 0f;
            return false;
        }
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

        public override void On_LevelChanged(CRunSkillInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);

            if (m_cOwner != null && iLevel == 1)
                m_cOwner.OnDamaged += On_PlayerDamaged;

            m_fGaugeRateScale = cInfo.Get_Value(iLevel);
        }

        public override void Tick(float fDeltaTime)
        {
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
    /// 회전탄 — 주위를 도는 탄이 적탄을 없애고 몬스터에게 피해를 준다. 레벨업마다 탄이 1개씩
    /// 늘어난다(1레벨=1개). 실제 충돌 판정은 <see cref="CStage_Manager"/>가 한다 — 적탄/몬스터
    /// 목록을 들고 있는 쪽이 거기라서다(2-6과 같은 이유). 여기서는 "지금 탄이 어디 있는가"만 안다.
    /// </summary>
    public class CRunSkillEffect_Orbit : CRunSkillEffect
    {
        private const float ORBIT_RADIUS_CELLS = 1.5f;     // 플레이어로부터의 거리
        private const float ANGULAR_SPEED_DEG  = 180f;     // 초당 회전각

        /// <summary> 탄 하나의 충돌 반경(셀). CStage_Manager가 세계 좌표로 환산해 쓴다. </summary>
        public const float HIT_RADIUS_CELLS = 0.5f;

        private float m_fAngle;
        private int   m_iCount;

        public override void On_LevelChanged(CRunSkillInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_iCount = Mathf.RoundToInt(cInfo.Get_Value(iLevel));
        }

        public override void Tick(float fDeltaTime)
        {
            m_fAngle = (m_fAngle + ANGULAR_SPEED_DEG * fDeltaTime) % 360f;
        }

        public override bool Try_Get_OrbitOffsets(List<Vector2> lstOffsetCells)
        {
            lstOffsetCells.Clear();
            if (m_iCount <= 0)
                return false;

            float fStepDeg = 360f / m_iCount;

            for (int i = 0; i < m_iCount; ++i)
            {
                float fRad = (m_fAngle + fStepDeg * i) * Mathf.Deg2Rad;
                lstOffsetCells.Add(new Vector2(Mathf.Cos(fRad), Mathf.Sin(fRad)) * ORBIT_RADIUS_CELLS);
            }

            return true;
        }
    }

    /// <summary>
    /// 몽둥이 — 바라보는 방향으로 주기적으로 휘둘러 맞은 몬스터를 넉백시킨다. 레벨업마다
    /// 판정 반경이 커진다. 뱀서라이크의 다른 무기들처럼 버튼 없이 자동으로 발동한다 —
    /// "액티브"는 버튼 여부가 아니라 쿨타임을 가진 효과라는 뜻이다(패시브는 상시 적용).
    /// </summary>
    public class CRunSkillEffect_Club : CRunSkillEffect
    {
        private const float SWING_INTERVAL = 1.2f;     // 초

        /// <summary> 넉백 밀리는 거리(셀). CStage_Manager가 세계 좌표로 환산해 쓴다. </summary>
        public const float KNOCKBACK_DISTANCE_CELLS = 1.5f;
        /// <summary> 넉백 지속 시간(초). </summary>
        public const float KNOCKBACK_DURATION = 0.25f;
        /// <summary> 플레이어 앞으로 얼마나 나가서 판정할지(셀). </summary>
        public const float SWING_OFFSET_CELLS = 1f;

        private float m_fSwingTimer;
        private float m_fRadiusCells;

        public override void On_LevelChanged(CRunSkillInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_fRadiusCells = cInfo.Get_Value(iLevel);
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_fSwingTimer > 0f)
                m_fSwingTimer -= fDeltaTime;
        }

        public override bool Consume_Swing(out float fRadiusCells)
        {
            fRadiusCells = m_fRadiusCells;

            if (m_fSwingTimer > 0f)
                return false;

            m_fSwingTimer = SWING_INTERVAL;
            return true;
        }
    }
}
