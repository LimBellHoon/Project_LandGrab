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
