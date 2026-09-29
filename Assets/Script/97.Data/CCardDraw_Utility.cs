using System;
using System.Collections.Generic;

namespace Client
{
    // 260929_카드 뽑기 알고리즘(Docs/Design_Card_Balance_Spec.md §4.1 · §6.5). 표 · 보유 레벨 · 스테이지 번호만 받는
    // 순수 함수라 화면 없이 CProtoTest가 확률 조정표를 그대로 검증한다(CWeightedPick_Utility와 같은 자리).
    /// <summary>
    /// 첫 선택은 고정 풀(★), 그 뒤는 티어 해금 → 보유 계열 ×2.5 → 확률 조정표 순으로 가중치를 매기고
    /// 보유 계열에서 최소 한 장을 보장해 세 장을 뽑는다.
    /// </summary>
    public static class CCardDraw_Utility
    {
        public const int   PICK_COUNT           = 3;
        public const float OWNED_FAMILY_WEIGHT  = 2.5f;

        // 가중치는 소수(×0.1 · ×2.5)라 CWeightedPick_Utility(int)에 넘길 때 이 배율로 키운다.
        private const int  WEIGHT_SCALE         = 1000;

        /// <summary> 스펙 §7.1 티어 해금 — 스테이지 1~3은 T1, 4~6은 T1+T2, 7~10은 전부. </summary>
        public static int Get_MaxTier(int iStage) => iStage <= 3 ? 1 : (iStage <= 6 ? 2 : 3);

        /// <param name="iPickIndex"> 이번 스테이지에서 지금까지 고른 횟수. 0이면 첫 선택(고정 풀) </param>
        /// <param name="iStage"> 스테이지 번호(1부터) — 이 리포에서는 웨이브 번호다 </param>
        /// <param name="fnGetLevel"> 그 카드를 지금 몇 레벨 들고 있는지(안 가졌으면 0) </param>
        /// <param name="fnExclude"> 버린 카드 · 지금 화면에 떠 있는 카드처럼 호출부가 빼는 것(3지선다 재사용 확장점, 2-10-1) </param>
        public static List<CCardInfo> Draw(IReadOnlyList<CCardInfo> lstAll, int iPickIndex, int iStage,
                                           Func<CARD_TYPE, int> fnGetLevel, Func<CCardInfo, bool> fnExclude = null,
                                           int iCount = PICK_COUNT)
        {
            List<CCardInfo> lstResult = new List<CCardInfo>();
            if (lstAll == null || iCount <= 0)
                return lstResult;

            if (fnGetLevel == null)
                fnGetLevel = eType => 0;

            // 1) 첫 선택은 고정 풀 — ★(bFirstPickOnly)가 붙은 카드를 표 순서대로. 모자라면 아래로 이어서 채운다
            if (iPickIndex == 0)
            {
                for (int i = 0; i < lstAll.Count && lstResult.Count < iCount; ++i)
                {
                    CCardInfo cInfo = lstAll[i];
                    if (cInfo.bFirstPickOnly == true && Is_Enabled(cInfo) == true && fnGetLevel(cInfo.eType) < cInfo.iMaxLevel
                        && (fnExclude == null || fnExclude(cInfo) == false))
                        lstResult.Add(cInfo);
                }

                if (lstResult.Count >= iCount)
                    return lstResult;
            }

            // 2) 후보 필터 — 티어 · 만렙 · 하드 배타(§6.5 M04↔K08) · 호출부가 빼는 것
            int iMaxTier = Get_MaxTier(iStage);
            List<CCardInfo> lstPool = new List<CCardInfo>();
            for (int i = 0; i < lstAll.Count; ++i)
            {
                CCardInfo cInfo = lstAll[i];
                if (Is_Enabled(cInfo) == false || cInfo.iTier > iMaxTier || lstResult.Contains(cInfo) == true)
                    continue;

                if (fnGetLevel(cInfo.eType) >= cInfo.iMaxLevel || Is_HardExcluded(cInfo.eType, fnGetLevel) == true)
                    continue;

                if (fnExclude != null && fnExclude(cInfo) == true)
                    continue;

                lstPool.Add(cInfo);
            }

            // 3) 가중치 — 기본 1.0, 보유 계열 ×2.5, 확률 조정표 배율
            HashSet<CARD_FAMILY> hsOwnedFamily = Collect_OwnedFamily(lstAll, fnGetLevel);
            Dictionary<CCardInfo, float> dicWeight = new Dictionary<CCardInfo, float>();
            for (int i = 0; i < lstPool.Count; ++i)
                dicWeight[lstPool[i]] = Calc_Weight(lstPool[i], hsOwnedFamily, lstAll, fnGetLevel);

            Func<CCardInfo, int> fnIntWeight = cInfo => (int)Math.Round(dicWeight[cInfo] * WEIGHT_SCALE);

            // 4) 보유 계열에서 최소 한 장을 보장한다 — 완전 무작위면 7장이 네 계열로 흩어져 시너지가 안 터진다
            if (hsOwnedFamily.Count > 0)
            {
                List<CCardInfo> lstOwnedPool = lstPool.FindAll(cInfo => hsOwnedFamily.Contains(cInfo.eFamily));
                List<CCardInfo> lstGuaranteed = CWeightedPick_Utility.Pick(lstOwnedPool, fnIntWeight, 1);
                if (lstGuaranteed.Count > 0)
                {
                    lstResult.Add(lstGuaranteed[0]);
                    lstPool.Remove(lstGuaranteed[0]);
                }
            }

            List<CCardInfo> lstRest = CWeightedPick_Utility.Pick(lstPool, fnIntWeight, iCount - lstResult.Count);
            lstResult.AddRange(lstRest);
            return lstResult;
        }

        /// <summary> 스펙 §6.5 확률 조정표를 전부 곱한 배율 × 보유 계열 가중. </summary>
        public static float Calc_Weight(CCardInfo cInfo, HashSet<CARD_FAMILY> hsOwnedFamily,
                                        IReadOnlyList<CCardInfo> lstAll, Func<CARD_TYPE, int> fnGetLevel)
        {
            float fWeight = 1f;
            if (hsOwnedFamily != null && hsOwnedFamily.Contains(cInfo.eFamily) == true)
                fWeight *= OWNED_FAMILY_WEIGHT;

            return fWeight * Calc_WeightModifier(cInfo, lstAll, fnGetLevel);
        }

        /// <summary>
        /// 배타가 아니라 등장 확률만 낮춘다(보유 자체는 가능). 굴절(G08) 빌드와 도화선(F) 빌드 중 한쪽을 자연스럽게 타게 하는 장치다.
        /// </summary>
        public static float Calc_WeightModifier(CCardInfo cInfo, IReadOnlyList<CCardInfo> lstAll, Func<CARD_TYPE, int> fnGetLevel)
        {
            float fMod = 1f;
            CARD_TYPE eType = cInfo.eType;

            // F계열을 이미 2장 이상 들었으면 G08은 거의 안 나온다
            if (eType == CARD_TYPE.G08_REFRACTION && Count_OwnedFamily(lstAll, fnGetLevel, CARD_FAMILY.FUSE) >= 2)
                fMod *= 0.1f;

            // G08을 먼저 들었으면 F계열이 잘 안 나오고, Lv.3에서는 아예 빠진다(굴절 100%면 도화선이 없어 F가 전부 죽은 카드)
            if (cInfo.eFamily == CARD_FAMILY.FUSE)
            {
                int iG08 = fnGetLevel(CARD_TYPE.G08_REFRACTION);
                if (iG08 >= 3)
                    fMod *= 0f;
                else if (iG08 >= 1)
                    fMod *= 0.3f;
            }

            // {G01, F03, F05} 중 두 장을 들었으면 남은 한 장은 잘 안 나온다 — 도화선을 늦추는 카드가 셋 다 몰리지 않게
            if (eType == CARD_TYPE.G01_EXTINGUISHER || eType == CARD_TYPE.F03_FIREBREAK || eType == CARD_TYPE.F05_FUSE_BOMB)
            {
                int iOwnedOfTrio = (fnGetLevel(CARD_TYPE.G01_EXTINGUISHER) > 0 ? 1 : 0)
                                 + (fnGetLevel(CARD_TYPE.F03_FIREBREAK) > 0 ? 1 : 0)
                                 + (fnGetLevel(CARD_TYPE.F05_FUSE_BOMB) > 0 ? 1 : 0);
                if (iOwnedOfTrio == 2 && fnGetLevel(eType) == 0)
                    fMod *= 0.1f;
            }

            // K03 · K04는 한 장을 들었으면 나머지 한 장이 반으로 준다(그로기 보조 둘이 겹치지 않게)
            if (eType == CARD_TYPE.K03_LEVY && fnGetLevel(CARD_TYPE.K04_TURRET) > 0)
                fMod *= 0.5f;
            if (eType == CARD_TYPE.K04_TURRET && fnGetLevel(CARD_TYPE.K03_LEVY) > 0)
                fMod *= 0.5f;

            return fMod;
        }

        /// <summary> 스펙 §6.5 하드 배타 — M04 ↔ K08은 둘 다 안전 귀환을 덮어써서 한쪽을 들면 다른 쪽은 풀에서 빠진다. </summary>
        public static bool Is_HardExcluded(CARD_TYPE eType, Func<CARD_TYPE, int> fnGetLevel)
        {
            if (eType == CARD_TYPE.M04_UNBREAKABLE_RUSH)
                return fnGetLevel(CARD_TYPE.K08_WHIRL) > 0;
            if (eType == CARD_TYPE.K08_WHIRL)
                return fnGetLevel(CARD_TYPE.M04_UNBREAKABLE_RUSH) > 0;

            return false;
        }

        // 레벨링 카드(iMaxLevel > 1)만 새 카드다. iWeight가 0이면 꺼 둔 것(스펙 §5.1의 enabled: false — 안전 귀환이 없는 K08/M04)
        private static bool Is_Enabled(CCardInfo cInfo) => cInfo.iMaxLevel > 1 && cInfo.iWeight > 0;

        private static HashSet<CARD_FAMILY> Collect_OwnedFamily(IReadOnlyList<CCardInfo> lstAll, Func<CARD_TYPE, int> fnGetLevel)
        {
            HashSet<CARD_FAMILY> hsFamily = new HashSet<CARD_FAMILY>();
            for (int i = 0; i < lstAll.Count; ++i)
            {
                if (lstAll[i].iMaxLevel > 1 && fnGetLevel(lstAll[i].eType) > 0)
                    hsFamily.Add(lstAll[i].eFamily);
            }

            return hsFamily;
        }

        private static int Count_OwnedFamily(IReadOnlyList<CCardInfo> lstAll, Func<CARD_TYPE, int> fnGetLevel, CARD_FAMILY eFamily)
        {
            int iCount = 0;
            for (int i = 0; i < lstAll.Count; ++i)
            {
                if (lstAll[i].iMaxLevel > 1 && lstAll[i].eFamily == eFamily && fnGetLevel(lstAll[i].eType) > 0)
                    ++iCount;
            }

            return iCount;
        }
    }
}
