using UnityEngine;

namespace Client
{
    // 260917_투사체 모양 — GYM은 모양마다 CBullet 하위 클래스와 프리팹을 따로 뒀다
    // (CBullet_Laser / CBullet_Horizontal / CBullet_Explosion). 여기선 판정과 겉모습 값만 가진 모듈이다.
    /// <summary>
    /// 판정(Is_Overlap)과, 화면이 그대로 옮겨 그릴 값(VIEW_*)을 함께 낸다.
    /// 그래서 CProjectile은 모양을 몰라도 스프라이트를 늘리고 돌리기만 하면 된다.
    /// </summary>
    public abstract class CProjectileShape
    {
        protected CProjectileCore m_cCore;

        /// <summary> 지금 새로 닿은 대상을 맞힐 수 있는가 (레이저 예고 중 · 다 커진 폭발은 못 맞힌다). </summary>
        public virtual bool     CAN_HIT     => true;
        /// <summary> 사거리(fMaxRange)를 날아간 거리로 쓰는가. 레이저는 빔 길이로 쓴다. </summary>
        public virtual bool     USES_RANGE  => true;

        /// <summary> 그릴 자리 (월드) </summary>
        public virtual Vector2  VIEW_CENTER => m_cCore.POS;
        /// <summary> 그릴 크기 (월드). 스프라이트 1유닛 기준 </summary>
        public abstract Vector2 VIEW_SIZE { get; }
        /// <summary> 그릴 회전 (도) </summary>
        public virtual float    VIEW_ANGLE  => 0f;
        /// <summary> 겉모습 알파. 예고선은 흐리게 </summary>
        public virtual float    VIEW_ALPHA  => 1f;
        /// <summary> 사각형(빔)으로 그리는가, 원으로 그리는가 </summary>
        public virtual bool     IS_RECT     => false;

        public static CProjectileShape Create(PROJECTILE_SHAPE eShape)
        {
            switch (eShape)
            {
                case PROJECTILE_SHAPE.LASER: return new CProjectileShape_Laser();
                case PROJECTILE_SHAPE.SWEEP: return new CProjectileShape_Sweep();
                case PROJECTILE_SHAPE.BLAST: return new CProjectileShape_Blast();
                default:                     return new CProjectileShape_Point();
            }
        }

        public virtual void Initialize(CProjectileCore cCore) => m_cCore = cCore;

        public virtual void Tick(float fDeltaTime) { }

        public abstract bool Is_Overlap(Vector2 vPos, float fRadius);

        /// <summary> 표의 반경(셀) × 크기 배율 → 월드 </summary>
        protected float HIT_RADIUS => m_cCore.INFO.fHitRange * m_cCore.SCALE * m_cCore.CELL_SIZE;
    }

    /// <summary> 원. 일반 탄. </summary>
    public class CProjectileShape_Point : CProjectileShape
    {
        // 판정보다 조금 작게 그린다 — 예전 탄(반경 0.7칸 · 지름 0.9칸 그림)과 같은 비율이다.
        private const float VIEW_RATE = 0.65f;

        public override Vector2 VIEW_SIZE => Vector2.one * HIT_RADIUS * 2f * VIEW_RATE;

        public override bool Is_Overlap(Vector2 vPos, float fRadius)
            => Vector2.Distance(vPos, m_cCore.POS) <= HIT_RADIUS + fRadius;
    }

    /// <summary>
    /// 레이저 (GYM CBullet_Laser). 가는 예고선 → 두꺼워짐 → 켜짐 → 사라짐.
    /// 맞는 것은 켜져 있는 동안뿐이다 — 예고선이 보이는 동안 피할 수 있어야 한다.
    ///
    /// GYM은 늘 화면 끝까지 뻗었다. 적 레이저는 점령지에서 멈춘다 — 땅을 먹은 만큼 방패가 되는 것이 이 게임의 규칙이다.
    /// </summary>
    public class CProjectileShape_Laser : CProjectileShape
    {
        public enum PHASE { TELEGRAPH, THICKEN, ACTIVE, FADE }

        private const float TELEGRAPH_WIDTH_RATE = 0.12f;   // 예고선 굵기 (켜졌을 때 대비)
        private const float MARCH_STEP_CELL      = 0.5f;    // 빔 끝을 찾는 간격

        private float m_fTelegraph;
        private float m_fThicken;
        private float m_fFade;
        private float m_fWidthCell;
        private float m_fLength;        // 월드. 벽에서 잘린 실제 길이

        public PHASE    CUR_PHASE
        {
            get
            {
                float fT = m_cCore.ELAPSED;
                if (fT < m_fTelegraph)                        return PHASE.TELEGRAPH;
                if (fT < m_fTelegraph + m_fThicken)           return PHASE.THICKEN;
                if (fT < m_cCore.LIFE_TIME - m_fFade)         return PHASE.ACTIVE;
                return PHASE.FADE;
            }
        }

        public float    LENGTH      => m_fLength;
        public override bool    CAN_HIT     => CUR_PHASE == PHASE.ACTIVE;
        public override bool    USES_RANGE  => false;
        public override bool    IS_RECT     => true;
        public override Vector2 VIEW_CENTER => m_cCore.POS + m_cCore.DIR * (m_fLength * 0.5f);
        public override Vector2 VIEW_SIZE   => new Vector2(m_fLength, Mathf.Max(0.01f, WIDTH));
        public override float   VIEW_ANGLE  => Mathf.Atan2(m_cCore.DIR.y, m_cCore.DIR.x) * Mathf.Rad2Deg;
        public override float   VIEW_ALPHA  => CUR_PHASE == PHASE.TELEGRAPH ? 0.45f : 1f;

        /// <summary> 지금 굵기 (월드) </summary>
        public float WIDTH
        {
            get
            {
                float fFull = m_fWidthCell * m_cCore.SCALE * m_cCore.CELL_SIZE;
                float fThin = fFull * TELEGRAPH_WIDTH_RATE;
                float fT    = m_cCore.ELAPSED;

                switch (CUR_PHASE)
                {
                    case PHASE.TELEGRAPH: return fThin;
                    case PHASE.THICKEN:   return Mathf.Lerp(fThin, fFull, (fT - m_fTelegraph) / Mathf.Max(0.0001f, m_fThicken));
                    case PHASE.ACTIVE:    return fFull;
                    default:
                        float fRemain = m_cCore.LIFE_TIME - fT;
                        return Mathf.Lerp(0f, fFull, fRemain / Mathf.Max(0.0001f, m_fFade));
                }
            }
        }

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);

            m_fTelegraph = Mathf.Max(0f, cCore.Get_Param("LASER_TELEGRAPH", 0.8f));
            m_fThicken   = Mathf.Max(0f, cCore.Get_Param("LASER_THICKEN", 0.2f));
            m_fFade      = Mathf.Max(0f, cCore.Get_Param("LASER_FADE", 0.3f));
            m_fWidthCell = Mathf.Max(0.05f, cCore.Get_Param("LASER_WIDTH", 1f));
            Refresh_Length();
        }

        // 점령지는 빔이 뻗는 도중에도 늘어날 수 있어 매 프레임 다시 잰다.
        public override void Tick(float fDeltaTime) => Refresh_Length();

        private void Refresh_Length()
        {
            float fMax  = Mathf.Max(1f, m_cCore.INFO.fMaxRange) * m_cCore.CELL_SIZE;
            float fStep = MARCH_STEP_CELL * m_cCore.CELL_SIZE;

            m_fLength = fMax;
            for (float fDist = fStep; fDist <= fMax; fDist += fStep)
            {
                if (m_cCore.HOST.Is_Wall(m_cCore.POS + m_cCore.DIR * fDist, m_cCore.SIDE) == true)
                {
                    m_fLength = fDist;
                    break;
                }
            }
        }

        public override bool Is_Overlap(Vector2 vPos, float fRadius)
        {
            Vector2 vToTarget = vPos - m_cCore.POS;
            float   fAlong    = Vector2.Dot(vToTarget, m_cCore.DIR);
            if (fAlong < -fRadius || fAlong > m_fLength + fRadius)
                return false;

            float fAcross = Mathf.Abs(m_cCore.DIR.x * vToTarget.y - m_cCore.DIR.y * vToTarget.x);
            return fAcross <= WIDTH * 0.5f + fRadius;
        }
    }

    /// <summary>
    /// 충격파 (GYM CBullet_Horizontal). 쏜 자리를 지나는 띠가 양옆으로 맵 끝까지 뻗는다.
    /// 쏜 방향이 가로에 가까우면 가로 띠, 세로에 가까우면 세로 띠다.
    /// </summary>
    public class CProjectileShape_Sweep : CProjectileShape
    {
        private bool  m_bHorizontal;
        private float m_fReachTime;     // 맵 끝까지 닿는 데 걸리는 시간
        private float m_fMaxExtent;     // 월드. 가장 먼 맵 끝까지의 거리

        public float    EXTENT => m_fMaxExtent * Mathf.Clamp01(m_cCore.ELAPSED / Mathf.Max(0.0001f, m_fReachTime));
        public override bool    IS_RECT     => true;
        public override bool    USES_RANGE  => false;
        public override Vector2 VIEW_SIZE   => new Vector2(Mathf.Max(0.01f, EXTENT * 2f), HIT_RADIUS * 2f);
        public override float   VIEW_ANGLE  => m_bHorizontal == true ? 0f : 90f;

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);

            m_bHorizontal = Mathf.Abs(cCore.DIR.x) >= Mathf.Abs(cCore.DIR.y);
            m_fReachTime  = Mathf.Max(0.01f, cCore.Get_Param("SWEEP_TIME", cCore.LIFE_TIME * 0.5f));

            Rect rcBounds = cCore.HOST.WORLD_BOUNDS;
            m_fMaxExtent = m_bHorizontal == true
                ? Mathf.Max(cCore.POS.x - rcBounds.xMin, rcBounds.xMax - cCore.POS.x)
                : Mathf.Max(cCore.POS.y - rcBounds.yMin, rcBounds.yMax - cCore.POS.y);
        }

        public override bool Is_Overlap(Vector2 vPos, float fRadius)
        {
            Vector2 vDelta  = vPos - m_cCore.POS;
            float   fAlong  = m_bHorizontal == true ? vDelta.x : vDelta.y;
            float   fAcross = m_bHorizontal == true ? vDelta.y : vDelta.x;

            return Mathf.Abs(fAlong) <= EXTENT + fRadius && Mathf.Abs(fAcross) <= HIT_RADIUS + fRadius;
        }
    }

    /// <summary>
    /// 폭발 (GYM CBullet_Explosion). 반경이 커지는 동안만 맞는다 — 다 커진 뒤에 걸어 들어온 대상은 안 맞는다.
    /// </summary>
    public class CProjectileShape_Blast : CProjectileShape
    {
        private float m_fGrowTime;
        private float m_fMaxRate;

        public float    RADIUS
        {
            get
            {
                float fT = Mathf.Clamp01(m_cCore.ELAPSED / Mathf.Max(0.0001f, m_fGrowTime));
                return HIT_RADIUS * Mathf.Lerp(1f, m_fMaxRate, fT);
            }
        }

        public override bool    CAN_HIT     => m_cCore.ELAPSED <= m_fGrowTime;
        public override Vector2 VIEW_SIZE   => Vector2.one * RADIUS * 2f;
        // 다 커지면 남은 수명 동안 흐려진다.
        public override float   VIEW_ALPHA
            => CAN_HIT == true ? 1f
             : Mathf.Clamp01((m_cCore.LIFE_TIME - m_cCore.ELAPSED) / Mathf.Max(0.0001f, m_cCore.LIFE_TIME - m_fGrowTime));

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);

            m_fGrowTime = Mathf.Max(0.01f, cCore.Get_Param("BLAST_GROW", 0.3f));
            m_fMaxRate  = Mathf.Max(1f, cCore.Get_Param("BLAST_SCALE", 4f));
        }

        public override bool Is_Overlap(Vector2 vPos, float fRadius)
            => Vector2.Distance(vPos, m_cCore.POS) <= RADIUS + fRadius;
    }
}
