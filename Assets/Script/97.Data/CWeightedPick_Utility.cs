using System;
using System.Collections.Generic;

namespace Client
{
    // 260917_가중치 무작위 추출 — 카드 표와 런 스킬 표에 똑같은 코드가 따로 있어 하나로 모았다(1-1)
    public static class CWeightedPick_Utility
    {
        /// <summary>
        /// 후보에서 가중치 비율로 iCount개를 뽑는다. 한 번 뽑힌 것은 다시 나오지 않는다 —
        /// 3지선다의 재미는 서로 다른 선택지에서 나온다(2-10-1). 가중치가 0 이하인 후보는 나오지 않는다.
        /// </summary>
        /// <param name="lstCandidate"> 이 목록은 건드리지 않는다 </param>
        public static List<T> Pick<T>(IReadOnlyList<T> lstCandidate, Func<T, int> fnWeight, int iCount,
                                      List<T> lstResult = null)
        {
            if (lstResult == null)
                lstResult = new List<T>();

            lstResult.Clear();
            if (lstCandidate == null || fnWeight == null || iCount <= 0)
                return lstResult;

            List<T> lstPool = new List<T>();
            int iTotal = 0;

            for (int i = 0; i < lstCandidate.Count; ++i)
            {
                int iWeight = fnWeight(lstCandidate[i]);
                if (iWeight <= 0)
                    continue;

                lstPool.Add(lstCandidate[i]);
                iTotal += iWeight;
            }

            while (lstResult.Count < iCount && lstPool.Count > 0)
            {
                int iRoll = UnityEngine.Random.Range(0, iTotal);
                int iPick = lstPool.Count - 1;       // 끝을 넘기는 경우의 보험

                for (int i = 0; i < lstPool.Count; ++i)
                {
                    iRoll -= fnWeight(lstPool[i]);
                    if (iRoll >= 0)
                        continue;

                    iPick = i;
                    break;
                }

                lstResult.Add(lstPool[iPick]);
                iTotal -= fnWeight(lstPool[iPick]);
                lstPool.RemoveAt(iPick);
            }

            return lstResult;
        }
    }
}
