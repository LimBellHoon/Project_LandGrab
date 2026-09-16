using System;
using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260917_발사 패턴 — GYM CBulletFactory(Fire_Stay / Fire_Single / Fire_Spread / Fire_Ring)
    /// <summary>
    /// 한 번 쏠 때 나갈 방향들만 계산한다. 실제로 만드는 것은 창구(IGimmickHost / IProjectileHost)다.
    /// GYM은 방향 계산과 생성이 한 함수에 섞여 있어 화면 없이 확인할 수 없었다.
    /// </summary>
    public static class CProjectileFire_Utility
    {
        /// <param name="vAim"> 조준 방향 (보통 대상 쪽) </param>
        /// <param name="iCount"> SPREAD · RING의 발 수. SINGLE · BURST는 한 번에 1발 </param>
        /// <param name="fAngle"> SPREAD 부채꼴 전체 각도(도). RING · SPIN에선 쓰지 않는다 </param>
        /// <param name="fSpinOffset"> SPIN이 지금까지 돌아간 각도(도) </param>
        public static void Get_Directions(FIRE_PATTERN ePattern, Vector2 vAim, int iCount, float fAngle,
                                          float fSpinOffset, List<Vector2> lstOut)
        {
            lstOut.Clear();

            if (vAim.sqrMagnitude <= Mathf.Epsilon)
                vAim = Vector2.down;
            vAim.Normalize();

            iCount = Mathf.Max(1, iCount);

            switch (ePattern)
            {
                case FIRE_PATTERN.SPREAD:
                {
                    if (iCount == 1)
                    {
                        lstOut.Add(vAim);
                        break;
                    }

                    // 가운데가 조준 방향에 오도록 양 끝을 ±절반에 둔다.
                    float fStep = fAngle / (iCount - 1);
                    for (int i = 0; i < iCount; ++i)
                        lstOut.Add(Rotate(vAim, -fAngle * 0.5f + fStep * i));
                    break;
                }

                case FIRE_PATTERN.RING:
                case FIRE_PATTERN.SPIN:
                {
                    // RING은 조준과 상관없이 고정 방위, SPIN은 거기에 누적 회전을 얹는다.
                    float fStep  = 360f / iCount;
                    float fStart = ePattern == FIRE_PATTERN.SPIN ? fSpinOffset : 0f;
                    for (int i = 0; i < iCount; ++i)
                        lstOut.Add(Rotate(Vector2.right, fStart + fStep * i));
                    break;
                }

                default:    // SINGLE · BURST
                    lstOut.Add(vAim);
                    break;
            }
        }

        public static Vector2 Rotate(Vector2 vDir, float fDegree)
        {
            float fRad = fDegree * Mathf.Deg2Rad;
            float fCos = Mathf.Cos(fRad);
            float fSin = Mathf.Sin(fRad);
            return new Vector2(vDir.x * fCos - vDir.y * fSin, vDir.x * fSin + vDir.y * fCos);
        }
    }

    // 260917_발사 패턴을 실제로 굴리는 쪽 — 포수(CEnemyGimmick_Projectile)와 플레이어 무기(CRunSkillEffect_Weapon)가 같이 쓴다
    /// <summary>
    /// 한 번 쏘라고 하면 패턴대로 방향을 뽑아 fnSpawn을 부른다. 연발(BURST)은 남은 발을 들고 Tick마다 흘려보내고,
    /// 회전 링(SPIN)은 쏠 때마다 각도를 누적한다. 둘 다 '쏘는 쪽의 상태'라 표(탄)에 두지 않고 여기 둔다.
    /// </summary>
    public class CProjectileFirer
    {
        private const float DEFAULT_BURST_INTERVAL = 0.12f;

        private readonly List<Vector2> m_lstDir = new List<Vector2>();

        private FIRE_PATTERN    m_ePattern;
        private int             m_iCount = 1;
        private float           m_fAngle;
        private float           m_fInterval = DEFAULT_BURST_INTERVAL;
        private Action<Vector2> m_fnSpawn;

        private float m_fSpinOffset;
        private int   m_iBurstRemain;
        private float m_fBurstTimer;

        /// <summary> 연발이 아직 남았다 — 끝나기 전에 새로 쏘지 않는다. </summary>
        public bool IS_BURSTING => m_iBurstRemain > 0;
        public int  COUNT       => m_iCount;

        /// <param name="fnSpawn"> 방향 하나마다 불린다. 한 번만 넘겨 두어 매 발사마다 대리자를 새로 만들지 않는다 </param>
        public void Setup(FIRE_PATTERN ePattern, int iCount, float fAngle, float fInterval, Action<Vector2> fnSpawn)
        {
            m_ePattern  = ePattern;
            m_iCount    = Mathf.Max(1, iCount);
            m_fAngle    = fAngle;
            m_fInterval = fInterval > 0f ? fInterval : DEFAULT_BURST_INTERVAL;
            m_fnSpawn   = fnSpawn;
        }

        public void Reset()
        {
            m_fSpinOffset  = 0f;
            m_iBurstRemain = 0;
            m_fBurstTimer  = 0f;
        }

        public void Fire(Vector2 vAim)
        {
            if (m_fnSpawn == null)
                return;

            if (m_ePattern == FIRE_PATTERN.BURST)
            {
                m_iBurstRemain = m_iCount;
                m_fBurstTimer  = 0f;
                Tick(0f, vAim);
                return;
            }

            CProjectileFire_Utility.Get_Directions(m_ePattern, vAim, m_iCount, m_fAngle, m_fSpinOffset, m_lstDir);
            for (int i = 0; i < m_lstDir.Count; ++i)
                m_fnSpawn(m_lstDir[i]);

            if (m_ePattern == FIRE_PATTERN.SPIN)
                m_fSpinOffset = Mathf.Repeat(m_fSpinOffset + m_fAngle, 360f);
        }

        // 연발은 쏘는 순간마다 다시 조준한다 — 한 방향에 몰아 쏘면 한 번 비키는 것으로 다 피해진다.
        public void Tick(float fDeltaTime, Vector2 vAim)
        {
            if (m_iBurstRemain <= 0 || m_fnSpawn == null)
                return;

            m_fBurstTimer -= fDeltaTime;
            if (m_fBurstTimer > 0f)
                return;

            m_fBurstTimer = m_fInterval;
            --m_iBurstRemain;
            m_fnSpawn(vAim.sqrMagnitude > Mathf.Epsilon ? vAim.normalized : Vector2.up);
        }
    }

    // 260917_조준 대상 고르기 — GYM CSkillHandler의 ExploreType (가까운 적 / 체력 많은 적 / 몰린 곳)
    public static class CTargetFinder_Utility
    {
        /// <param name="fCrowdRadius"> CROWDED에서 '몰려 있다'로 볼 반경 (월드) </param>
        /// <returns> 살아 있는 대상이 없으면 null </returns>
        public static IImpactTarget Find<T>(IReadOnlyList<T> lstTarget, Vector2 vFrom, TARGET_FIND eFind,
                                            float fCrowdRadius = 2f) where T : IImpactTarget
        {
            if (lstTarget == null)
                return null;

            IImpactTarget cBest = null;
            float fBestScore = float.MinValue;

            for (int i = 0; i < lstTarget.Count; ++i)
            {
                IImpactTarget cTarget = lstTarget[i];
                if (cTarget == null || cTarget.IS_ALIVE == false)
                    continue;

                float fDistance = Vector2.Distance(vFrom, cTarget.POS);
                float fScore;

                switch (eFind)
                {
                    case TARGET_FIND.HIGHEST_HP:
                        // 체력이 같으면 가까운 쪽 — 거리를 아주 작게 빼서 동점만 가른다.
                        fScore = cTarget.HP - fDistance * 0.0001f;
                        break;

                    case TARGET_FIND.CROWDED:
                        fScore = Count_Near(lstTarget, cTarget.POS, fCrowdRadius) - fDistance * 0.0001f;
                        break;

                    default:
                        fScore = -fDistance;
                        break;
                }

                if (fScore > fBestScore)
                {
                    fBestScore = fScore;
                    cBest = cTarget;
                }
            }

            return cBest;
        }

        private static int Count_Near<T>(IReadOnlyList<T> lstTarget, Vector2 vCenter, float fRadius) where T : IImpactTarget
        {
            int iCount = 0;
            for (int i = 0; i < lstTarget.Count; ++i)
            {
                if (lstTarget[i] != null && lstTarget[i].IS_ALIVE == true
                    && Vector2.Distance(vCenter, lstTarget[i].POS) <= fRadius)
                    ++iCount;
            }
            return iCount;
        }
    }
}
