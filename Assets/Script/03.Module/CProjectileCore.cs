using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260917_투사체 본체 — Project_GYM의 CBullet + BulletLogic(ScriptableObject)을 이식
    /// <summary>
    /// 탄 하나의 규칙 전부. 화면(CProjectile)과 떼어 두었으므로 CProtoTest에서 그대로 돌린다.
    ///
    /// 조합 구조(2-6)다.
    /// <code>
    /// CProjectileCore ── CProjectileShape    모양 · 판정 (POINT / LASER / SWEEP / BLAST)   ← 하나
    ///                 ├─ CProjectileMove     이동 (STRAIGHT / TRACE / SPIRAL ...)         ← 하나
    ///                 └─ CProjectileTrait    특성 (REBOUND / 기절 / 넉백 ...)              ← 여러 개
    /// </code>
    ///
    /// GYM과 달라진 점
    ///  · 모듈은 탄마다 새로 만든다. GYM은 ScriptableObject 하나를 모든 탄이 나눠 써서
    ///    한 탄의 상태(부메랑 단계, 커지는 비율)가 다른 탄에 섞였다.
    ///  · 닿기 시작 / 닿아 있음 / 떨어짐을 여기서 한 번만 가린다. GYM은 Unity 물리 콜백에 기대
    ///    특성마다 피해를 다시 넣어(Rebound · KnockBack · Stun 각각 Damage 호출) 특성을 겹치면 피해도 겹쳤다.
    ///    여기선 피해 · 효과 · 내구도 소모는 본체가 한 번, 특성은 자기 동작만 한다.
    ///  · 물리를 쓰지 않는다. 몬스터 · 플레이어 충돌을 이미 거리로 보고 있어(2-6) 같은 방식으로 맞췄다.
    /// </summary>
    public class CProjectileCore
    {
        // 260917_풀에서 다시 쓰일 때마다 새 번호. 탄을 붙잡아 두는 쪽(회전탄)이 '아직 내가 쏜 그 탄인가'를 가린다 —
        // 없으면 거둔 탄이 다른 탄으로 재사용된 뒤에 그 탄을 끄는 사고가 난다.
        private static int s_iSerial;

        private CProjectileInfo         m_cInfo;
        private IProjectileHost         m_cHost;
        private IImpactTarget           m_cOwner;       // 쏜 쪽. ORBIT · SYNC · BOOMERANG이 따라간다. 없어도 된다
        private PROJECTILE_SIDE         m_eSide;
        private float                   m_fCellSize;

        private CProjectileShape                m_cShape;
        private CProjectileMove                 m_cMove;
        private readonly List<CProjectileTrait> m_lstTrait  = new List<CProjectileTrait>();
        private readonly List<CImpactInfo>      m_lstImpact = new List<CImpactInfo>();

        private Vector2 m_vStartPos;
        private Vector2 m_vPos;
        private Vector2 m_vDir;
        private float   m_fElapsed;
        private float   m_fTravelled;       // 월드
        private float   m_fHitStopTimer;
        private float   m_fScaleRate = 1f;  // 특성(SCALE_OVER_TIME)이 곱하는 크기
        private int     m_iDurability;
        private int     m_iHitCount;        // 260917_새로 맞힌 횟수. 플레이어 탄이 몬스터를 때렸는지 스테이지가 센다(분노 게이지)
        private bool    m_bExpired;
        private int     m_iSerial;
        private bool    m_bCancelShot;      // 260917_닿은 적탄을 지운다 (조율값 CANCEL_SHOT)

        // 지금 닿아 있는 대상. 이번 프레임 판정과 비교해 들어옴 / 머무름 / 나감을 가린다.
        private readonly HashSet<IImpactTarget> m_hsContact = new HashSet<IImpactTarget>();
        private readonly HashSet<IImpactTarget> m_hsFrame   = new HashSet<IImpactTarget>();
        private readonly List<IImpactTarget>    m_lstLeave  = new List<IImpactTarget>();

        public CProjectileInfo  INFO            => m_cInfo;
        public PROJECTILE_SIDE  SIDE            => m_eSide;
        public IProjectileHost  HOST            => m_cHost;
        public IImpactTarget    OWNER           => m_cOwner != null && m_cOwner.IS_ALIVE == true ? m_cOwner : null;
        public CProjectileShape SHAPE           => m_cShape;
        public float            CELL_SIZE       => m_fCellSize;
        public Vector2          START_POS       => m_vStartPos;
        public Vector2          POS             => m_vPos;
        public Vector2          DIR             => m_vDir;
        /// <summary> 월드 단위/초 </summary>
        public float            SPEED           => m_cInfo.fSpeed * m_fCellSize;
        public float            ELAPSED         => m_fElapsed;
        public float            LIFE_TIME       => m_cInfo.fLifeTime;
        public float            SCALE           => m_cInfo.fScale * m_fScaleRate;
        public int              DURABILITY      => m_iDurability;
        public int              HIT_COUNT       => m_iHitCount;
        public int              SERIAL          => m_iSerial;
        public bool             CAN_CANCEL_SHOT => m_bCancelShot;
        /// <summary> 판정 반경(월드) — 표의 반경 × 크기 배율. 적탄 지우기처럼 원끼리 볼 때 쓴다 </summary>
        public float            HIT_RADIUS      => m_cInfo != null ? m_cInfo.fHitRange * SCALE * m_fCellSize : 0f;
        public bool             IS_EXPIRED      => m_bExpired;
        public bool             IS_HIT_STOP     => m_fHitStopTimer > 0f;
        public int              TRAIT_COUNT     => m_lstTrait.Count;
        public int              CONTACT_COUNT   => m_hsContact.Count;
        /// <summary> 회전탄이 지울 수 있는 탄인가 — 레이저 · 충격파 · 폭발은 지워지지 않는다. </summary>
        public bool             IS_CANCELABLE   => m_cInfo != null && m_cInfo.eShape == PROJECTILE_SHAPE.POINT;

        /// <param name="lstImpact"> 표에서 찾아 둔 효과. 탄이 표를 직접 알면 화면 없이 검증하기 어렵다 </param>
        public bool Initialize(CProjectileInfo cInfo, IReadOnlyList<CImpactInfo> lstImpact, IProjectileHost cHost,
                               float fCellSize, Vector2 vPos, Vector2 vDir, PROJECTILE_SIDE eSide, IImpactTarget cOwner)
        {
            if (cInfo == null || cHost == null || fCellSize <= 0f)
            {
                Debug.LogError("[CProjectileCore] 탄 정보 / 창구 / 셀 크기가 비어 있습니다.");
                return false;
            }

            m_cInfo     = cInfo;
            m_cHost     = cHost;
            m_cOwner    = cOwner;
            m_eSide     = eSide;
            m_fCellSize = fCellSize;

            m_vStartPos     = vPos;
            m_vPos          = vPos;
            m_vDir          = vDir.sqrMagnitude > Mathf.Epsilon ? vDir.normalized : Vector2.up;
            m_fElapsed      = 0f;
            m_fTravelled    = 0f;
            m_fHitStopTimer = 0f;
            m_fScaleRate    = 1f;
            m_iDurability   = cInfo.iDurability == 0 ? 1 : cInfo.iDurability;
            m_iHitCount     = 0;
            m_iSerial       = ++s_iSerial;
            m_bCancelShot   = cInfo.Get_Param("CANCEL_SHOT", 0f) > 0f;
            m_bExpired      = false;
            m_hsContact.Clear();

            m_lstImpact.Clear();
            if (lstImpact != null)
            {
                for (int i = 0; i < lstImpact.Count; ++i)
                {
                    if (lstImpact[i] != null)
                        m_lstImpact.Add(lstImpact[i]);
                }
            }

            m_cShape = CProjectileShape.Create(cInfo.eShape);
            m_cShape.Initialize(this);

            m_cMove = CProjectileMove.Create(cInfo.eMove);
            m_cMove.Initialize(this);

            Build_Trait(cInfo);
            return true;
        }

        // RANDOM이 섞여 있으면 함께 적힌 특성 중 하나만 남긴다 (GYM CBulletLogic_Trait_RandomTrait).
        private void Build_Trait(CProjectileInfo cInfo)
        {
            m_lstTrait.Clear();

            List<PROJECTILE_TRAIT> lstPick = cInfo.lstTrait;
            if (cInfo.Has_Trait(PROJECTILE_TRAIT.RANDOM) == true)
            {
                List<PROJECTILE_TRAIT> lstCandidate = new List<PROJECTILE_TRAIT>();
                for (int i = 0; i < cInfo.lstTrait.Count; ++i)
                {
                    if (cInfo.lstTrait[i] != PROJECTILE_TRAIT.RANDOM)
                        lstCandidate.Add(cInfo.lstTrait[i]);
                }

                lstPick = new List<PROJECTILE_TRAIT>();
                if (lstCandidate.Count > 0)
                    lstPick.Add(lstCandidate[Random.Range(0, lstCandidate.Count)]);
            }

            for (int i = 0; i < lstPick.Count; ++i)
            {
                CProjectileTrait cTrait = CProjectileTrait.Create(lstPick[i]);
                if (cTrait == null)
                    continue;

                cTrait.Initialize(this);
                m_lstTrait.Add(cTrait);
            }
        }

        public bool Has_Trait<T>() where T : CProjectileTrait
        {
            for (int i = 0; i < m_lstTrait.Count; ++i)
            {
                if (m_lstTrait[i] is T)
                    return true;
            }
            return false;
        }

        #region 매 프레임
        public void Tick(float fDeltaTime)
        {
            if (m_bExpired == true)
                return;

            // 타격 정지 — 탄만 잠깐 멈춘다. 수명도 같이 멈춰야 멈춘 만큼 손해를 보지 않는다.
            if (m_fHitStopTimer > 0f)
            {
                m_fHitStopTimer -= fDeltaTime;
                return;
            }

            m_fElapsed += fDeltaTime;
            if (m_cInfo.fLifeTime > 0f && m_fElapsed >= m_cInfo.fLifeTime)
            {
                Expire();
                return;
            }

            for (int i = 0; i < m_lstTrait.Count; ++i)
                m_lstTrait[i].Tick(fDeltaTime);

            Vector2 vNext = m_cMove.Get_NextPos(fDeltaTime);
            if (m_bExpired == true)     // 부메랑이 돌아와 끝난 경우
                return;

            if (m_cMove.USES_WALL == true && m_cHost.Is_Wall(vNext, m_eSide) == true)
            {
                if (Handle_Wall(vNext) == false)
                {
                    Expire();
                    return;
                }
            }
            else
            {
                m_fTravelled += Vector2.Distance(m_vPos, vNext);
                m_vPos = vNext;
            }

            if (m_cInfo.fMaxRange > 0f && m_cShape.USES_RANGE == true
                && m_fTravelled >= m_cInfo.fMaxRange * m_fCellSize)
            {
                Expire();
                return;
            }

            m_cShape.Tick(fDeltaTime);
        }

        /// <returns> 특성이 벽을 처리했으면(튕김) true, 아니면 탄이 사라진다 </returns>
        private bool Handle_Wall(Vector2 vNext)
        {
            for (int i = 0; i < m_lstTrait.Count; ++i)
            {
                if (m_lstTrait[i].On_Wall(vNext) == false)
                    continue;

                // 튕긴 것도 한 번 쓴 것이다 — 내구도가 다하면 거기서 끝난다 (GYM '죽는 조건 상세').
                Consume_Durability();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 스테이지가 매 프레임 부른다. 맞을 수 있는 대상을 넘기면 닿기 시작 · 닿아 있음 · 떨어짐을 가린다.
        /// 넘기지 않은 대상(죽었거나 안전 지대로 돌아간 플레이어)은 떨어진 것으로 본다.
        /// </summary>
        public void Update_Contact(IReadOnlyList<IImpactTarget> lstCandidate, float fDeltaTime)
        {
            m_hsFrame.Clear();

            if (m_bExpired == false && lstCandidate != null)
            {
                for (int i = 0; i < lstCandidate.Count; ++i)
                {
                    IImpactTarget cTarget = lstCandidate[i];
                    if (cTarget == null || cTarget.IS_ALIVE == false)
                        continue;

                    // 닿는 순간만 판정 시간을 본다(레이저 ACTIVE · 폭발이 커지는 동안).
                    // 이미 닿아 있던 대상은 모양만 보고 붙잡아 둔다 — 레이저가 사라지는 동안 속박이 풀렸다 걸렸다 하지 않게.
                    bool bAlready = m_hsContact.Contains(cTarget);
                    if (bAlready == false && m_cShape.CAN_HIT == false)
                        continue;

                    if (m_cShape.Is_Overlap(cTarget.POS, cTarget.HIT_RADIUS) == false)
                        continue;

                    m_hsFrame.Add(cTarget);

                    if (bAlready == true)
                    {
                        On_Stay(cTarget, fDeltaTime);
                        continue;
                    }

                    m_hsContact.Add(cTarget);
                    On_Enter(cTarget);

                    // 내구도가 다하면 같은 프레임에 겹친 다음 대상은 맞지 않는다.
                    if (m_bExpired == true)
                        break;
                }
            }

            m_lstLeave.Clear();
            foreach (IImpactTarget cTarget in m_hsContact)
            {
                if (m_hsFrame.Contains(cTarget) == false)
                    m_lstLeave.Add(cTarget);
            }

            for (int i = 0; i < m_lstLeave.Count; ++i)
            {
                m_hsContact.Remove(m_lstLeave[i]);
                On_Exit(m_lstLeave[i]);
            }
        }
        #endregion 매 프레임

        #region 닿음
        private void On_Enter(IImpactTarget cTarget)
        {
            ++m_iHitCount;

            if (m_cInfo.iDamage > 0)
                cTarget.Take_Damage(m_cInfo.iDamage);

            if (cTarget.IS_ALIVE == true && cTarget.IMPACT != null)
            {
                for (int i = 0; i < m_lstImpact.Count; ++i)
                    cTarget.IMPACT.Apply(m_lstImpact[i], cTarget, m_vPos, m_fCellSize, m_cHost, m_eSide);
            }

            if (cTarget.IS_ALIVE == true)
            {
                for (int i = 0; i < m_lstTrait.Count; ++i)
                    m_lstTrait[i].On_Enter(cTarget);
            }

            Consume_Durability();
        }

        private void On_Stay(IImpactTarget cTarget, float fDeltaTime)
        {
            for (int i = 0; i < m_lstTrait.Count; ++i)
                m_lstTrait[i].On_Stay(cTarget, fDeltaTime);
        }

        private void On_Exit(IImpactTarget cTarget)
        {
            for (int i = 0; i < m_lstTrait.Count; ++i)
                m_lstTrait[i].On_Exit(cTarget);

            if (cTarget.IMPACT == null)
                return;

            for (int i = 0; i < m_lstImpact.Count; ++i)
                cTarget.IMPACT.Remove_Contact(m_lstImpact[i]);
        }
        #endregion 닿음

        #region 모듈이 부르는 것
        public void Set_Pos(Vector2 vPos) => m_vPos = vPos;

        public void Set_Dir(Vector2 vDir)
        {
            if (vDir.sqrMagnitude > Mathf.Epsilon)
                m_vDir = vDir.normalized;
        }

        public void Set_ScaleRate(float fRate) => m_fScaleRate = Mathf.Max(0.01f, fRate);

        public void Hit_Stop(float fTime) => m_fHitStopTimer = Mathf.Max(m_fHitStopTimer, fTime);

        public float Get_Param(string strKey, float fDefault) => m_cInfo.Get_Param(strKey, fDefault);

        /// <summary> 이동 · 모양이 조준할 대상. 적탄이면 플레이어, 플레이어 탄이면 몬스터 중에서. </summary>
        public IImpactTarget Find_Target() => m_cHost.Find_Target(m_vPos, m_eSide);

        private void Consume_Durability()
        {
            if (m_iDurability < 0)
                return;     // 무한

            --m_iDurability;
            if (m_iDurability <= 0)
                Expire();
        }

        /// <summary>
        /// 끝낸다. 닿아 있던 대상에게는 떨어짐을 알린다 — 안 그러면 속박 · 감속이 영영 안 풀린다
        /// (GYM은 탄이 풀로 돌아갈 때 Exit가 불리지 않아 몬스터가 굳은 채로 남을 수 있었다).
        /// </summary>
        public void Expire()
        {
            if (m_bExpired == true)
                return;

            m_bExpired = true;

            foreach (IImpactTarget cTarget in m_hsContact)
                On_Exit(cTarget);

            m_hsContact.Clear();
        }
        #endregion 모듈이 부르는 것
    }
}
