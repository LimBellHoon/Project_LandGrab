using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260921_계정 레벨 (Assets/Data/AccountLevelInfo.csv) — 2-23
    /// <summary>
    /// 한 줄 = 한 레벨. 그 레벨에서 다음 레벨까지 필요한 경험치와, 그 레벨이 주는 능력치 · 하트 최대치를 적는다.
    /// 능력치는 **누적값**으로 적는다(5레벨의 속도 배율 = 5레벨 줄의 값). 레벨마다 더해 가면 한 줄을 고칠 때
    /// 그 뒤가 전부 밀려 기획이 표를 읽기 어렵다.
    /// </summary>
    public class CAccountLevelInfo
    {
        public int   iLevel;
        public int   iNeedExp;          // 이 레벨에서 다음 레벨까지. 0이면 만렙
        public int   iMaxStamina;       // 하트 최대치
        public float fStaminaRegenSec;  // 하트 하나가 차는 시간(초)
        public float fSpeedRate;        // 플레이어 속도에 곱한다
        public float fEvasionBonus;     // 회피율에 더한다
    }

    public class CCSVData_AccountLevelInfo : CCSVData
    {
        private const string TABLE_NAME = "AccountLevelInfo";
        public  const string CSV_KEY    = "CCSVData_AccountLevelInfo";

        private readonly List<CAccountLevelInfo> m_lstInfo = new List<CAccountLevelInfo>();

        public IReadOnlyList<CAccountLevelInfo> ALL => m_lstInfo;
        public int MAX_LEVEL => m_lstInfo.Count > 0 ? m_lstInfo[m_lstInfo.Count - 1].iLevel : 1;

        protected override void Parse_CSVData(string[] arrField)
        {
            if (CCSV_Utility.Is_HeaderRow(arrField, "iLevel") == true)
                return;

            CAccountLevelInfo cInfo = new CAccountLevelInfo
            {
                iLevel           = CCSV_Utility.To_Int(arrField, 0),
                iNeedExp         = CCSV_Utility.To_Int(arrField, 1),
                iMaxStamina      = CCSV_Utility.To_Int(arrField, 2, 30),
                fStaminaRegenSec = CCSV_Utility.To_Float(arrField, 3, 300f),
                fSpeedRate       = CCSV_Utility.To_Float(arrField, 4, 1f),
                fEvasionBonus    = CCSV_Utility.To_Float(arrField, 5),
            };

            if (cInfo.iLevel <= 0)
            {
                Debug.LogError($"[{TABLE_NAME}] iLevel이 없는 행을 건너뛴다.");
                return;
            }

            m_lstInfo.Add(cInfo);
        }

        /// <summary> 그 레벨의 줄. 표보다 높으면 마지막 줄, 낮으면 첫 줄. 표가 비었으면 null. </summary>
        public CAccountLevelInfo Get_Info(int iLevel)
        {
            if (m_lstInfo.Count <= 0)
                return null;

            for (int i = 0; i < m_lstInfo.Count; ++i)
            {
                if (m_lstInfo[i].iLevel == iLevel)
                    return m_lstInfo[i];
            }

            return iLevel < m_lstInfo[0].iLevel ? m_lstInfo[0] : m_lstInfo[m_lstInfo.Count - 1];
        }

        /// <summary>
        /// 경험치를 더해 레벨을 올린다 — 한 번에 여러 레벨이 오를 수 있다. 만렙이면 경험치는 더 쌓이지 않는다.
        /// 화면 없이 테스트하려고 저장소를 모르는 순수 계산으로 뒀다(CProgress_Manager가 결과를 저장한다).
        /// </summary>
        /// <returns> 오른 레벨 수 </returns>
        public int Apply_Exp(ref int iLevel, ref int iExp, int iGain)
        {
            if (iGain <= 0 || m_lstInfo.Count <= 0)
                return 0;

            iLevel = Mathf.Max(1, iLevel);
            iExp  += iGain;

            int iUp = 0;
            while (true)
            {
                CAccountLevelInfo cInfo = Get_Info(iLevel);
                if (cInfo == null || cInfo.iNeedExp <= 0 || iLevel >= MAX_LEVEL)
                {
                    iExp = 0;       // 만렙 — 더 쌓아 봐야 쓸 곳이 없다
                    break;
                }

                if (iExp < cInfo.iNeedExp)
                    break;

                iExp -= cInfo.iNeedExp;
                ++iLevel;
                ++iUp;
            }

            return iUp;
        }
    }
}
