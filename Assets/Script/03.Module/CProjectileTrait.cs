using UnityEngine;

namespace Client
{
    // 260917_투사체 특성 — GYM BulletLogic_Trait(ScriptableObject)을 탄마다 새로 만드는 모듈로 옮겼다
    /// <summary>
    /// 자기 동작만 한다. 피해 · 효과(ImpactInfo) · 내구도는 본체가 한 번만 처리한다.
    ///
    /// GYM에서 고친 것
    ///  · Rebound.Initialize가 'base가 성공하면 실패'로 뒤집혀 있어 튕기는 탄이 초기화되지 않았다.
    ///  · StayStun · WhiteOut의 Exit가 base.CollisionEnter를 불러 떨어지는 순간 피해가 한 번 더 들어갔다.
    ///  · ScaleOverTime이 '5 - 남은 수명'으로 시간을 구해 수명이 5초가 아니면 크기가 튀었다.
    ///  · 특성마다 피해를 넣어 특성을 겹치면 피해도 겹쳤다.
    /// </summary>
    public abstract class CProjectileTrait
    {
        protected CProjectileCore m_cCore;

        public static CProjectileTrait Create(PROJECTILE_TRAIT eTrait)
        {
            switch (eTrait)
            {
                case PROJECTILE_TRAIT.REBOUND:          return new CProjectileTrait_Rebound();
                case PROJECTILE_TRAIT.GRAVITY_PULL:     return new CProjectileTrait_Gravity(true);
                case PROJECTILE_TRAIT.GRAVITY_PUSH:     return new CProjectileTrait_Gravity(false);
                case PROJECTILE_TRAIT.KNOCKBACK_PULL:   return new CProjectileTrait_Knockback(true);
                case PROJECTILE_TRAIT.KNOCKBACK_PUSH:   return new CProjectileTrait_Knockback(false);
                case PROJECTILE_TRAIT.ENTER_STUN:       return new CProjectileTrait_EnterStun();
                case PROJECTILE_TRAIT.STAY_STUN:        return new CProjectileTrait_StayStun();
                case PROJECTILE_TRAIT.HIT_STOP:         return new CProjectileTrait_HitStop();
                case PROJECTILE_TRAIT.SCALE_OVER_TIME:  return new CProjectileTrait_ScaleOverTime();
                case PROJECTILE_TRAIT.STAY_STOP:        return new CProjectileTrait_StayStop();
                case PROJECTILE_TRAIT.WHITE_OUT:        return new CProjectileTrait_WhiteOut();
                default:                                return null;    // NONE · RANDOM(본체가 고른다)
            }
        }

        public virtual void Initialize(CProjectileCore cCore) => m_cCore = cCore;

        public virtual void Tick(float fDeltaTime) { }
        public virtual void On_Enter(IImpactTarget cTarget) { }
        public virtual void On_Stay(IImpactTarget cTarget, float fDeltaTime) { }
        public virtual void On_Exit(IImpactTarget cTarget) { }

        /// <summary> 벽에 막혔을 때. 처리했으면(튕김) true. </summary>
        public virtual bool On_Wall(Vector2 vBlockedPos) => false;
    }

    /// <summary>
    /// 벽에 튕긴다. GYM은 화면 끝에서 튕겼다 — 여기선 맵 끝, 그리고 적탄에게는 점령지 가장자리가 벽이다.
    /// 어느 축이 막혔는지 따로 봐서 그 축만 뒤집는다(모서리면 둘 다).
    /// </summary>
    public class CProjectileTrait_Rebound : CProjectileTrait
    {
        public override bool On_Wall(Vector2 vBlockedPos)
        {
            Vector2 vPos = m_cCore.POS;
            Vector2 vDir = m_cCore.DIR;

            bool bBlockX = m_cCore.HOST.Is_Wall(new Vector2(vBlockedPos.x, vPos.y), m_cCore.SIDE);
            bool bBlockY = m_cCore.HOST.Is_Wall(new Vector2(vPos.x, vBlockedPos.y), m_cCore.SIDE);

            // 대각선 끝(모서리 칸)만 막힌 경우 — 두 축 다 뒤집어 왔던 길로 돌려보낸다.
            if (bBlockX == false && bBlockY == false)
                bBlockX = bBlockY = true;

            if (bBlockX == true) vDir.x = -vDir.x;
            if (bBlockY == true) vDir.y = -vDir.y;

            m_cCore.Set_Dir(vDir);
            return true;
        }
    }

    /// <summary> 닿아 있는 동안 끌어당기거나(인력) 밀어낸다(척력). GRAVITY_POWER 셀/초. </summary>
    public class CProjectileTrait_Gravity : CProjectileTrait
    {
        private readonly bool m_bPull;
        private float m_fPower;

        public CProjectileTrait_Gravity(bool bPull) => m_bPull = bPull;

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);
            m_fPower = Mathf.Max(0f, cCore.Get_Param("GRAVITY_POWER", 2f));
        }

        public override void On_Stay(IImpactTarget cTarget, float fDeltaTime)
        {
            Vector2 vDir = m_bPull == true ? m_cCore.POS - cTarget.POS : cTarget.POS - m_cCore.POS;
            if (vDir.sqrMagnitude <= Mathf.Epsilon || fDeltaTime <= 0f)
                return;

            cTarget.Push(vDir.normalized, m_fPower * m_cCore.CELL_SIZE * fDeltaTime, fDeltaTime);
        }
    }

    /// <summary> 닿는 순간 탄 쪽으로 당기거나(PULL) 진행 방향으로 날린다(PUSH). </summary>
    public class CProjectileTrait_Knockback : CProjectileTrait
    {
        private readonly bool m_bPull;
        private float m_fDistance;
        private float m_fTime;

        public CProjectileTrait_Knockback(bool bPull) => m_bPull = bPull;

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);
            m_fDistance = Mathf.Max(0f, cCore.Get_Param("KNOCKBACK_DISTANCE", 2f));
            m_fTime     = Mathf.Max(0.01f, cCore.Get_Param("KNOCKBACK_TIME", 0.2f));
        }

        public override void On_Enter(IImpactTarget cTarget)
        {
            Vector2 vDir = m_bPull == true ? m_cCore.POS - cTarget.POS : m_cCore.DIR;
            if (vDir.sqrMagnitude <= Mathf.Epsilon)
                return;

            cTarget.Push(vDir.normalized, m_fDistance * m_cCore.CELL_SIZE, m_fTime);
        }
    }

    /// <summary> 닿는 순간 STUN_TIME초 기절. </summary>
    public class CProjectileTrait_EnterStun : CProjectileTrait
    {
        private float m_fTime;

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);
            m_fTime = Mathf.Max(0.01f, cCore.Get_Param("STUN_TIME", 1f));
        }

        public override void On_Enter(IImpactTarget cTarget) => cTarget.IMPACT?.Set_Stun(this, m_fTime);
    }

    /// <summary> 닿아 있는 동안 기절(속박). 떨어지거나 탄이 사라지면 풀린다. </summary>
    public class CProjectileTrait_StayStun : CProjectileTrait
    {
        public override void On_Enter(IImpactTarget cTarget) => cTarget.IMPACT?.Set_Stun(this, -1f);
        public override void On_Exit(IImpactTarget cTarget)  => cTarget.IMPACT?.Remove(this);
    }

    /// <summary> 닿는 순간 탄이 HITSTOP_TIME초 멈춘다. 관통탄의 타격감. </summary>
    public class CProjectileTrait_HitStop : CProjectileTrait
    {
        private float m_fTime;

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);
            m_fTime = Mathf.Max(0f, cCore.Get_Param("HITSTOP_TIME", 0.08f));
        }

        public override void On_Enter(IImpactTarget cTarget) => m_cCore.Hit_Stop(m_fTime);
    }

    /// <summary> GROW_TIME초에 걸쳐 GROW_SCALE배까지 커진다. 판정도 같이 커진다. </summary>
    public class CProjectileTrait_ScaleOverTime : CProjectileTrait
    {
        private float m_fScale;
        private float m_fTime;

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);
            m_fScale = Mathf.Max(0.01f, cCore.Get_Param("GROW_SCALE", 2f));
            m_fTime  = Mathf.Max(0.01f, cCore.Get_Param("GROW_TIME", cCore.LIFE_TIME));
        }

        public override void Tick(float fDeltaTime)
            => m_cCore.Set_ScaleRate(Mathf.Lerp(1f, m_fScale, m_cCore.ELAPSED / m_fTime));
    }

    /// <summary> 닿아 있는 동안 느려진다. STOP_SLOW는 속도 배율(0이면 거의 멈춤). </summary>
    public class CProjectileTrait_StayStop : CProjectileTrait
    {
        private float m_fScale;

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);
            m_fScale = Mathf.Clamp01(cCore.Get_Param("STOP_SLOW", 0f));
        }

        public override void On_Enter(IImpactTarget cTarget) => cTarget.IMPACT?.Set_Slow(this, -1f, m_fScale);
        public override void On_Exit(IImpactTarget cTarget)  => cTarget.IMPACT?.Remove(this);
    }

    /// <summary>
    /// 맞은 대상이 하얗게 번쩍인다. GYM은 닿아 있는 동안만이었는데, 한 번 맞고 사라지는 탄은 번쩍임이
    /// 한 프레임도 안 보였다 — 닿을 때마다 WHITE_TIME초씩 켠다.
    /// </summary>
    public class CProjectileTrait_WhiteOut : CProjectileTrait
    {
        private float m_fTime;

        public override void Initialize(CProjectileCore cCore)
        {
            base.Initialize(cCore);
            m_fTime = Mathf.Max(0.01f, cCore.Get_Param("WHITE_TIME", 0.15f));
        }

        public override void On_Enter(IImpactTarget cTarget)                   => cTarget.IMPACT?.Set_WhiteOut(this, m_fTime);
        public override void On_Stay(IImpactTarget cTarget, float fDeltaTime)  => cTarget.IMPACT?.Set_WhiteOut(this, m_fTime);
    }
}
