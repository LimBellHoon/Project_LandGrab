using UnityEngine;

namespace Client
{
    // 260917_투사체 이동 — GYM BulletLogic_Movement(ScriptableObject)을 탄마다 새로 만드는 모듈로 옮겼다
    /// <summary>
    /// 다음 자리만 계산한다. 벽에 막히는지 · 튕기는지는 본체(CProjectileCore)와 특성이 본다.
    /// </summary>
    public abstract class CProjectileMove
    {
        protected CProjectileCore m_cCore;

        /// <summary> 벽에 닿으면 사라지거나 튕기는가. 쏜 쪽에 붙어 도는 탄은 벽을 무시한다. </summary>
        public virtual bool USES_WALL => true;

        public static CProjectileMove Create(PROJECTILE_MOVE eMove)
        {
            switch (eMove)
            {
                case PROJECTILE_MOVE.STRAIGHT:  return new CProjectileMove_Straight();
                case PROJECTILE_MOVE.TRACE:     return new CProjectileMove_Trace();
                case PROJECTILE_MOVE.SPIRAL:    return new CProjectileMove_Spiral();
                case PROJECTILE_MOVE.BOOMERANG: return new CProjectileMove_Boomerang();
                case PROJECTILE_MOVE.ORBIT:     return new CProjectileMove_Orbit();
                case PROJECTILE_MOVE.SYNC:      return new CProjectileMove_Sync();
                default:                        return new CProjectileMove_None();
            }
        }

        public virtual void Initialize(CProjectileCore cCore) => m_cCore = cCore;

        public abstract Vector2 Get_NextPos(float fDeltaTime);
    }

    /// <summary> 제자리. 레이저 · 충격파 · 폭발 · 장판. </summary>
    public class CProjectileMove_None : CProjectileMove
    {
        public override bool USES_WALL => false;
        public override Vector2 Get_NextPos(float fDeltaTime) => m_cCore.POS;
    }

    /// <summary> 직진 (GYM Normal). </summary>
    public class CProjectileMove_Straight : CProjectileMove
    {
        public override Vector2 Get_NextPos(float fDeltaTime)
            => m_cCore.POS + m_cCore.DIR * m_cCore.SPEED * fDeltaTime;
    }

    /// <summary>
    /// 추적. GYM Trace는 이름과 달리 직진만 했다 — 여기선 초당 TRACE_TURN 라디안까지 대상 쪽으로 휜다.
    /// 한 번에 확 꺾지 않는 이유는 피할 수 있어야 하기 때문이다(CEnemyGimmick_Projectile의 원칙).
    /// </summary>
    public class CProjectileMove_Trace : CProjectileMove
    {
        private float m_fTurnRate;

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);
            m_fTurnRate = Mathf.Max(0f, cCore.Get_Param("TRACE_TURN", 1.5f));
        }

        public override Vector2 Get_NextPos(float fDeltaTime)
        {
            IImpactTarget cTarget = m_cCore.Find_Target();
            if (cTarget != null)
            {
                Vector2 vWant = cTarget.POS - m_cCore.POS;
                if (vWant.sqrMagnitude > Mathf.Epsilon)
                {
                    float fCur  = Mathf.Atan2(m_cCore.DIR.y, m_cCore.DIR.x);
                    float fWant = Mathf.Atan2(vWant.y, vWant.x);
                    float fNext = Mathf.MoveTowardsAngle(fCur * Mathf.Rad2Deg, fWant * Mathf.Rad2Deg,
                                                         m_fTurnRate * Mathf.Rad2Deg * fDeltaTime) * Mathf.Deg2Rad;
                    m_cCore.Set_Dir(new Vector2(Mathf.Cos(fNext), Mathf.Sin(fNext)));
                }
            }

            return m_cCore.POS + m_cCore.DIR * m_cCore.SPEED * fDeltaTime;
        }
    }

    /// <summary>
    /// 나선. 쏜 자리를 중심으로 돌면서 반경이 커진다. 탄속이 있으면 중심도 쏜 방향으로 흘러간다.
    /// GYM Spiral은 '5 - 남은 수명'으로 경과 시간을 구해 수명이 5초가 아니면 어긋났다 — 경과 시간을 직접 쓴다.
    /// </summary>
    public class CProjectileMove_Spiral : CProjectileMove
    {
        private float m_fRotate;        // 라디안/초
        private float m_fExpand;        // 셀/초
        private float m_fStartAngle;

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);
            m_fRotate     = cCore.Get_Param("SPIRAL_ROTATE", 3f);
            m_fExpand     = cCore.Get_Param("SPIRAL_EXPAND", 2f);
            m_fStartAngle = Mathf.Atan2(cCore.DIR.y, cCore.DIR.x);
        }

        public override Vector2 Get_NextPos(float fDeltaTime)
        {
            float   fTime   = m_cCore.ELAPSED;
            float   fAngle  = m_fStartAngle + m_fRotate * fTime;
            float   fRadius = m_fExpand * m_cCore.CELL_SIZE * fTime;
            Vector2 vCenter = m_cCore.START_POS
                            + new Vector2(Mathf.Cos(m_fStartAngle), Mathf.Sin(m_fStartAngle)) * m_cCore.SPEED * fTime;

            Vector2 vNext = vCenter + new Vector2(Mathf.Cos(fAngle), Mathf.Sin(fAngle)) * fRadius;
            m_cCore.Set_Dir(vNext - m_cCore.POS);   // 튕김 · 넉백 방향이 실제 진행 방향을 따르게
            return vNext;
        }
    }

    /// <summary>
    /// 부메랑. 나감 → 멈춤 → 쏜 쪽으로 돌아옴 → 닿으면 끝.
    /// 쏜 쪽이 이미 죽었으면 쏜 자리로 돌아온다.
    /// </summary>
    public class CProjectileMove_Boomerang : CProjectileMove
    {
        private const float CATCH_RANGE_CELL = 0.6f;

        private float m_fOutTime;
        private float m_fStayTime;

        public bool IS_RETURNING => m_cCore.ELAPSED >= m_fOutTime + m_fStayTime;

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);
            m_fOutTime  = Mathf.Max(0f, cCore.Get_Param("BOOMERANG_OUT", 0.6f));
            m_fStayTime = Mathf.Max(0f, cCore.Get_Param("BOOMERANG_STAY", 0.3f));
        }

        public override Vector2 Get_NextPos(float fDeltaTime)
        {
            float fTime = m_cCore.ELAPSED;

            if (fTime < m_fOutTime)
                return m_cCore.POS + m_cCore.DIR * m_cCore.SPEED * fDeltaTime;

            if (fTime < m_fOutTime + m_fStayTime)
                return m_cCore.POS;

            IImpactTarget cOwner = m_cCore.OWNER;
            Vector2 vHome   = cOwner != null ? cOwner.POS : m_cCore.START_POS;
            Vector2 vToHome = vHome - m_cCore.POS;
            float   fStep   = m_cCore.SPEED * fDeltaTime;

            if (vToHome.magnitude <= Mathf.Max(fStep, CATCH_RANGE_CELL * m_cCore.CELL_SIZE))
            {
                m_cCore.Expire();
                return vHome;
            }

            m_cCore.Set_Dir(vToHome);
            return m_cCore.POS + m_cCore.DIR * fStep;
        }
    }

    /// <summary> 쏜 쪽 주위를 돈다 (GYM CirclePattern). 쏜 쪽이 죽으면 함께 사라진다. </summary>
    public class CProjectileMove_Orbit : CProjectileMove
    {
        private float m_fRadiusCell;
        private float m_fSpeedDeg;
        private float m_fStartAngle;

        public override bool USES_WALL => false;

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);
            m_fRadiusCell = Mathf.Max(0f, cCore.Get_Param("ORBIT_RADIUS", 2f));
            m_fSpeedDeg   = cCore.Get_Param("ORBIT_SPEED", 180f);
            m_fStartAngle = Mathf.Atan2(cCore.DIR.y, cCore.DIR.x) * Mathf.Rad2Deg;
        }

        public override Vector2 Get_NextPos(float fDeltaTime)
        {
            IImpactTarget cOwner = m_cCore.OWNER;
            if (cOwner == null)
            {
                m_cCore.Expire();
                return m_cCore.POS;
            }

            float fAngle = (m_fStartAngle + m_fSpeedDeg * m_cCore.ELAPSED) * Mathf.Deg2Rad;
            Vector2 vOffset = new Vector2(Mathf.Cos(fAngle), Mathf.Sin(fAngle));

            // 접선 방향을 진행 방향으로 둔다 — 넉백이 도는 방향으로 밀어낸다.
            m_cCore.Set_Dir(m_fSpeedDeg >= 0f ? new Vector2(-vOffset.y, vOffset.x) : new Vector2(vOffset.y, -vOffset.x));
            return cOwner.POS + vOffset * m_fRadiusCell * m_cCore.CELL_SIZE;
        }
    }

    /// <summary> 쏜 쪽에 붙어 다닌다 (GYM SyncPlayer). 몬스터 몸에서 뻗는 레이저 등. </summary>
    public class CProjectileMove_Sync : CProjectileMove
    {
        private Vector2 m_vOffset;

        public override bool USES_WALL => false;

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);

            IImpactTarget cOwner = cCore.OWNER;
            m_vOffset = cOwner != null ? cCore.POS - cOwner.POS : Vector2.zero;
        }

        public override Vector2 Get_NextPos(float fDeltaTime)
        {
            IImpactTarget cOwner = m_cCore.OWNER;
            if (cOwner == null)
            {
                m_cCore.Expire();
                return m_cCore.POS;
            }

            return cOwner.POS + m_vOffset;
        }
    }
}
