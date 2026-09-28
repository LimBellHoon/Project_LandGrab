using UnityEngine;

namespace Client
{
    // 260928_카드 시스템 재작성(Docs/Design_Roguelite_Rewrite.md 6장) — CRunSkillEffect와 같은 조합
    // 구조다(1-1). CPlayer가 List로 여러 개를 동시에 들고, 레벨이 바뀔 때(처음 획득 포함)
    // On_LevelChanged가 한 번 불린다.
    /// <summary>
    /// 몬스터 · 그리드를 직접 건드리는 카드(K02/K03/K04/K05/K06/K07/G08/F02/F04/F06/F07)는
    /// 여기 자식 클래스를 만들지 않는다 — "그리드는 칸만 알고 몬스터는 스테이지가 본다"(2-3)와 같은
    /// 구분으로, <c>CStage_Manager.Apply_Card</c>가 직접 다룬다(<see cref="Create"/>가 그 카드들엔
    /// null을 돌려준다 — 버그가 아니라 "플레이어 혼자 해결할 수 없다"는 뜻이다). 레벨 추적 자체는
    /// <c>CCardHandler</c>가 CCardEffect 존재 여부와 무관하게 항상 한다(<c>CPlayer.Add_Card</c> 참고).
    /// </summary>
    public abstract class CCardEffect
    {
        protected CPlayer m_cOwner;
        protected int      m_iLevel;

        public CARD_TYPE TYPE { get; private set; }

        public static CCardEffect Create(CARD_TYPE eType)
        {
            CCardEffect cEffect;

            switch (eType)
            {
                case CARD_TYPE.K01_ELECTRIC_LINE:     cEffect = new CCardEffect_ElectricLine();    break;
                case CARD_TYPE.M01_SPRINTER:          cEffect = new CCardEffect_Sprinter();        break;
                case CARD_TYPE.M02_MOMENTUM:          cEffect = new CCardEffect_Momentum();        break;
                case CARD_TYPE.M03_CORNERING:         cEffect = new CCardEffect_Cornering();       break;
                case CARD_TYPE.M05_GHOST_STEP:        cEffect = new CCardEffect_GhostStep();       break;
                case CARD_TYPE.M06_NEARMISS_MASTER:   cEffect = new CCardEffect_NearMissMaster();  break;
                case CARD_TYPE.M07_SIZE_SHIFT:        cEffect = new CCardEffect_SizeShift();       break;
                case CARD_TYPE.G01_EXTINGUISHER:      cEffect = new CCardEffect_Extinguisher();    break;
                case CARD_TYPE.G02_INVINCIBLE_STAR:   cEffect = new CCardEffect_InvincibleStar();  break;
                case CARD_TYPE.G03_LAST_SANCTUARY:    cEffect = new CCardEffect_LastSanctuary();   break;
                case CARD_TYPE.G04_MASS_SHIELD:        cEffect = new CCardEffect_MassShield();      break;
                case CARD_TYPE.G05_STURDY:            cEffect = new CCardEffect_Sturdy();          break;
                case CARD_TYPE.G06_BANSHEE_VEIL:      cEffect = new CCardEffect_BansheeVeil();     break;
                case CARD_TYPE.G07_LAST_STAND:        cEffect = new CCardEffect_LastStand();       break;
                case CARD_TYPE.F01_BURNING_HASTE:     cEffect = new CCardEffect_BurningHaste();    break;
                case CARD_TYPE.F03_FIREBREAK:         cEffect = new CCardEffect_Firebreak();       break;
                case CARD_TYPE.F05_FUSE_BOMB:         cEffect = new CCardEffect_FuseBomb();        break;

                // 260928_나머지(K02~K08 일부·G08·F02/F04/F06/F07)는 몬스터·그리드를 직접 건드려야 해서
                // CStage_Manager.Apply_Card가 처리한다 — 여기서는 만들지 않는다.
                default: return null;
            }

            cEffect.TYPE = eType;
            return cEffect;
        }

        public void Initialize(CPlayer cOwner) => m_cOwner = cOwner;

        /// <summary> 레벨이 바뀔 때(1레벨 최초 획득 포함) 불린다. </summary>
        public virtual void On_LevelChanged(CCardInfo cInfo, int iLevel) => m_iLevel = iLevel;

        public virtual void Tick(float fDeltaTime) { }

        /// <summary> 카드를 잃을 때(스테이지 종료) — 걸어 둔 플래그 · 구독을 되돌린다. </summary>
        public virtual void Release() { }

        /// <summary> F03/F07/G07 등이 도화선 전파 속도에 곱하는 배율. 1이면 영향 없음(매 프레임 다시 곱해서 합친다). </summary>
        public virtual float Get_FuseSpeedScale() => 1f;
        /// <summary> M07(축소)이 몸 충돌 판정 거리에 곱하는 배율. 1이면 영향 없음. </summary>
        public virtual float Get_HitboxScale() => 1f;
        /// <summary> G07/F01처럼 "조건이 맞는 동안만" 곱하는 이동속도 배율. 1이면 영향 없음. </summary>
        public virtual float Get_ConditionalSpeedScale() => 1f;
    }

    // ==================== 제어형(K) — 플레이어 혼자 해결되는 것만 ====================

    /// <summary> K01_ELECTRIC_LINE — 3번째 선 긋기마다 전기화된다. 실제 판정(기절)은 CStage_Manager가 한다. </summary>
    public class CCardEffect_ElectricLine : CCardEffect
    {
        private CCardInfo m_cInfo;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;

            if (m_cOwner != null && iLevel == 1)
                m_cOwner.OnDrawStart += On_DrawStart;
        }

        public override void Release()
        {
            m_cOwner?.Set_TrailElectrified(false, 0f);
            if (m_cOwner != null)
                m_cOwner.OnDrawStart -= On_DrawStart;
        }

        private void On_DrawStart()
        {
            bool bElectrified = m_cOwner.Count_DrawStart() % 3 == 0;
            m_cOwner.Set_TrailElectrified(bElectrified, m_cInfo.Get_Value(m_iLevel));
        }
    }

    // ==================== 기동형(M) ====================

    /// <summary> M01_SPRINTER — 선을 긋기 시작하면 1초간 빨라진다. </summary>
    public class CCardEffect_Sprinter : CCardEffect
    {
        private const float DURATION = 1f;
        private CCardInfo m_cInfo;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;
            if (m_cOwner != null && iLevel == 1)
                m_cOwner.OnDrawStart += On_DrawStart;
        }

        public override void Release()
        {
            if (m_cOwner != null)
                m_cOwner.OnDrawStart -= On_DrawStart;
        }

        private void On_DrawStart() => m_cOwner?.Add_CardBurstSpeed(m_cInfo.Get_Value(m_iLevel), DURATION);
    }

    /// <summary> M02_MOMENTUM — 직진 1초마다 5%씩 최대치까지 쌓인다. 방향을 바꾸면 리셋된다. </summary>
    public class CCardEffect_Momentum : CCardEffect
    {
        private const float GAIN_PER_SEC = 0.05f;
        private CCardInfo m_cInfo;
        private float      m_fStack;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;
            if (m_cOwner != null && iLevel == 1)
                m_cOwner.OnTurn += On_Turn;
        }

        public override void Release()
        {
            if (m_cOwner != null)
                m_cOwner.OnTurn -= On_Turn;
            m_cOwner?.Set_MomentumSpeedScale(1f);
        }

        private void On_Turn() => m_fStack = 0f;

        public override void Tick(float fDeltaTime)
        {
            if (m_cOwner == null || m_cOwner.IS_MOVING == false)
                return;

            float fMax = m_cInfo.Get_Value(m_iLevel);
            m_fStack = Mathf.Min(fMax, m_fStack + GAIN_PER_SEC * fDeltaTime);
            m_cOwner.Set_MomentumSpeedScale(1f + m_fStack);
        }
    }

    /// <summary> M03_CORNERING — 방향을 바꿀 때마다 잠깐 가속이 유지된다(260928_현재 이동 모델에는 "꺾을 때
    /// 감속"이 없어 원본 스펙의 "감속 없음"을 문자 그대로 옮길 자리가 없다 — 대신 "코너를 빠르게 빠져나간다"는
    /// 취지를 살려 짧은 가속으로 구현했다. 수치가 구체적이지 않아 값은 10% 고정, 지속시간만 레벨을 따른다). </summary>
    public class CCardEffect_Cornering : CCardEffect
    {
        private const float SPEED_BONUS = 0.1f;
        private CCardInfo m_cInfo;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;
            if (m_cOwner != null && iLevel == 1)
                m_cOwner.OnTurn += On_Turn;
        }

        public override void Release()
        {
            if (m_cOwner != null)
                m_cOwner.OnTurn -= On_Turn;
        }

        private void On_Turn() => m_cOwner?.Add_CardBurstSpeed(SPEED_BONUS, m_cInfo.Get_Value(m_iLevel));
    }

    /// <summary> M05_GHOST_STEP — 한 번에 12% 이상 점령하면 잠깐 몸이 통과 상태가 된다(기존 무적 재사용). </summary>
    public class CCardEffect_GhostStep : CCardEffect
    {
        public const float BIG_CAPTURE_RATIO = 0.12f;   // K05_FROST_PRISON과 같은 문턱값(원본 스펙 공통)
        private CCardInfo m_cInfo;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;
            if (m_cOwner != null && iLevel == 1)
                m_cOwner.OnCapture += On_Capture;
        }

        public override void Release()
        {
            if (m_cOwner != null)
                m_cOwner.OnCapture -= On_Capture;
        }

        private void On_Capture(int iCount)
        {
            if (m_cOwner.LAST_CAPTURE_RATIO >= BIG_CAPTURE_RATIO)
                m_cOwner.Add_Invincible(m_cInfo.Get_Value(m_iLevel));
        }
    }

    /// <summary> M06_NEARMISS_MASTER — 아슬아슬하게 피하면 1초간 빨라진다. </summary>
    public class CCardEffect_NearMissMaster : CCardEffect
    {
        private const float DURATION = 1f;
        private CCardInfo m_cInfo;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;
            if (m_cOwner != null && iLevel == 1)
                m_cOwner.OnNearMissSuccess += On_NearMiss;
        }

        public override void Release()
        {
            if (m_cOwner != null)
                m_cOwner.OnNearMissSuccess -= On_NearMiss;
        }

        private void On_NearMiss() => m_cOwner?.Add_CardBurstSpeed(m_cInfo.Get_Value(m_iLevel), DURATION);
    }

    /// <summary>
    /// M07_SIZE_SHIFT — 스테이지 시작(=이 카드를 처음 얻는 순간)에 반반 확률로 갈린다.
    /// 대형: 최대 HP가 늘어난다. 소형: 이동속도가 오르고 몸 충돌 판정이 줄어든다.
    /// 260928_원본 스펙의 "대형은 선이 굵어져 점유 면적 +10%"는 반영하지 않았다 — 선 굵기(GameConfig.m_fTrailWidthCell)는
    /// 순수 시각 값이라 칸 기반 점령 판정과 무관하다는 게 이미 확정된 설계다(2-3, 2-3-1). 점령 넓이 자체를 바꾸려면
    /// 규칙 단일 진입점(CTerritoryGrid.Step_To)을 건드려야 해서 이번 카드 하나 때문에 벌이지 않았다.
    /// </summary>
    public class CCardEffect_SizeShift : CCardEffect
    {
        private bool  m_bGiant;
        private int   m_iAppliedMaxLife;
        private float m_fAppliedSpeed;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            int iPrevLevel = m_iLevel;
            base.On_LevelChanged(cInfo, iLevel);

            if (iPrevLevel == 0)
                m_bGiant = Random.value < 0.5f;

            if (m_bGiant == true)
            {
                int iNewBonus = Mathf.RoundToInt(cInfo.Get_Value(iLevel));
                m_cOwner?.Add_CardMaxLife(iNewBonus - m_iAppliedMaxLife);
                m_iAppliedMaxLife = iNewBonus;
            }
            else
            {
                float fNewBonus = cInfo.Get_Param("SPEED", iLevel) / 100f;
                m_cOwner?.Add_CardSpeed(fNewBonus - m_fAppliedSpeed);
                m_fAppliedSpeed = fNewBonus;
            }
        }

        public override float Get_HitboxScale() => m_bGiant == true ? 1f : 0.7f;   // 소형 — 히트박스 -30% 고정
    }

    // ==================== 수호형(G) ====================

    /// <summary> G01_EXTINGUISHER — 판당 충전을 들고, 점령(ON_CLOSE)마다 그 레벨의 최대치로 다시 채운다.
    /// 실제 소모(도화선이 임박했을 때)는 CStage_Manager.Tick_Fuse가 CPlayer.Try_ConsumeExtinguisher로 한다. </summary>
    public class CCardEffect_Extinguisher : CCardEffect
    {
        private CCardInfo m_cInfo;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;
            if (m_cOwner != null && iLevel == 1)
                m_cOwner.OnCapture += On_Capture;

            m_cOwner?.Refill_ExtinguisherCharge(Mathf.RoundToInt(cInfo.Get_Value(iLevel)));
        }

        public override void Release()
        {
            if (m_cOwner != null)
                m_cOwner.OnCapture -= On_Capture;
        }

        private void On_Capture(int iCount) => m_cOwner.Refill_ExtinguisherCharge(Mathf.RoundToInt(m_cInfo.Get_Value(m_iLevel)));
    }

    /// <summary> G02_INVINCIBLE_STAR — 35초마다 보호막 1 + 무적을 함께 준다. </summary>
    public class CCardEffect_InvincibleStar : CCardEffect
    {
        private const float INTERVAL = 35f;
        private CCardInfo m_cInfo;
        private float      m_fTimer;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;
            if (m_fTimer <= 0f)
                m_fTimer = INTERVAL;
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cInfo == null || m_cOwner == null)
                return;

            m_fTimer -= fDeltaTime;
            if (m_fTimer > 0f)
                return;

            m_fTimer = INTERVAL;
            m_cOwner.Add_Shield();
            m_cOwner.Add_Invincible(m_cInfo.Get_Value(m_iLevel));
        }
    }

    /// <summary> G03_LAST_SANCTUARY — 목숨이 1로 떨어지면 판당 한 번만 완전 무적을 준다. </summary>
    public class CCardEffect_LastSanctuary : CCardEffect
    {
        private CCardInfo m_cInfo;
        private bool       m_bUsed;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;
            if (m_cOwner != null && iLevel == 1)
                m_cOwner.OnLifeChanged += On_LifeChanged;
        }

        public override void Release()
        {
            if (m_cOwner != null)
                m_cOwner.OnLifeChanged -= On_LifeChanged;
        }

        private void On_LifeChanged(int iLife)
        {
            if (m_bUsed == true || iLife != 1)
                return;

            m_bUsed = true;
            m_cOwner.Add_Invincible(m_cInfo.Get_Value(m_iLevel));
        }
    }

    /// <summary> G04_MASS_SHIELD — 한 번에 10% 이상 점령하면 그 레벨만큼 보호막을 준다(SHIELD_MAX까지 쌓인다). </summary>
    public class CCardEffect_MassShield : CCardEffect
    {
        private const float BIG_CAPTURE_RATIO = 0.10f;
        private CCardInfo m_cInfo;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;
            if (m_cOwner != null && iLevel == 1)
                m_cOwner.OnCapture += On_Capture;
        }

        public override void Release()
        {
            if (m_cOwner != null)
                m_cOwner.OnCapture -= On_Capture;
        }

        private void On_Capture(int iCount)
        {
            if (m_cOwner.LAST_CAPTURE_RATIO < BIG_CAPTURE_RATIO)
                return;

            int iCharge = Mathf.RoundToInt(m_cInfo.Get_Value(m_iLevel));
            for (int i = 0; i < iCharge; ++i)
                m_cOwner.Add_Shield();
        }
    }

    /// <summary> G05_STURDY — 최대 HP가 늘어난다(레벨업마다 직전 레벨과의 차이만 더한다). </summary>
    public class CCardEffect_Sturdy : CCardEffect
    {
        private int m_iApplied;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            int iNew = Mathf.RoundToInt(cInfo.Get_Value(iLevel));
            m_cOwner?.Add_CardMaxLife(iNew - m_iApplied);
            m_iApplied = iNew;
        }
    }

    /// <summary> G06_BANSHEE_VEIL — 재충전 쿨타임마다 다음 피격 1회 무효화를 준다(잔영과 같은 자원 재사용). </summary>
    public class CCardEffect_BansheeVeil : CCardEffect
    {
        private CCardInfo m_cInfo;
        private float      m_fTimer;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;
            if (m_fTimer <= 0f)
                m_fTimer = cInfo.Get_Value(iLevel);
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cInfo == null || m_cOwner == null)
                return;

            m_fTimer -= fDeltaTime;
            if (m_fTimer > 0f)
                return;

            m_fTimer = m_cInfo.Get_Value(m_iLevel);
            m_cOwner.Add_FreeHit(1);
        }
    }

    /// <summary> G07_LAST_STAND — 목숨이 1일 때만 이속이 오르고 도화선도 느려진다. </summary>
    public class CCardEffect_LastStand : CCardEffect
    {
        private CCardInfo m_cInfo;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;
        }

        public override float Get_ConditionalSpeedScale()
            => m_cOwner != null && m_cOwner.LIFE == 1 ? 1f + m_cInfo.Get_Value(m_iLevel) / 100f : 1f;

        public override float Get_FuseSpeedScale()
            => m_cOwner != null && m_cOwner.LIFE == 1 ? 1f - m_cInfo.Get_Param("FUSE", m_iLevel) / 100f : 1f;
    }

    // ==================== 도화선형(F) — 플레이어 혼자 해결되는 것만 ====================

    /// <summary> F01_BURNING_HASTE — 도화선이 타는 동안만 이동속도가 오른다. </summary>
    public class CCardEffect_BurningHaste : CCardEffect
    {
        private CCardInfo m_cInfo;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;
        }

        // CardInfo.csv의 F01 fValueLv는 이미 비율(0.40=+40%)이다 — K06/G07/G08의 %표기(35=35%)와
        // 다른 스케일이라 여기서는 100으로 나누지 않는다(M01_SPRINTER와 같은 결).
        public override float Get_ConditionalSpeedScale()
            => m_cOwner != null && m_cOwner.IS_TRAIL_BURNING == true ? 1f + m_cInfo.Get_Value(m_iLevel) : 1f;
    }

    /// <summary> F03_FIREBREAK — 도화선 전파 속도를 늘 줄인다. </summary>
    public class CCardEffect_Firebreak : CCardEffect
    {
        private CCardInfo m_cInfo;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            m_cInfo = cInfo;
        }

        // F03도 F01과 같은 비율 스케일이다(CardInfo.csv fValueLv=0.30 = -30%) — 100으로 나누지 않는다.
        public override float Get_FuseSpeedScale() => 1f - m_cInfo.Get_Value(m_iLevel);
    }

    /// <summary> F05_FUSE_BOMB — 판당 무효화 횟수(레벨업마다 직전 레벨과의 차이만 더한다). </summary>
    public class CCardEffect_FuseBomb : CCardEffect
    {
        private int m_iApplied;

        public override void On_LevelChanged(CCardInfo cInfo, int iLevel)
        {
            base.On_LevelChanged(cInfo, iLevel);
            int iNew = Mathf.RoundToInt(cInfo.Get_Value(iLevel));
            m_cOwner?.Add_FuseBombCharge(iNew - m_iApplied);
            m_iApplied = iNew;
        }
    }
}
