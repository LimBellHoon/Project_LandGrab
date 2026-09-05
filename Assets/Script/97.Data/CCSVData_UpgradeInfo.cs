using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260905_능력치 강화 테이블 (Assets/Data/UpgradeInfo.csv)
    /// <summary> UpgradeInfo.csv 한 행 = 강화 항목 하나. </summary>
    public class CUpgradeInfo
    {
        public STAT_TYPE eType;
        public string       strName;
        public string       strDesc;

        public int          iMaxLevel;
        public int          iCostBase;          // 0 -> 1 레벨 비용
        public int          iCostAdd;           // 레벨이 하나 오를 때마다 붙는 추가 비용
        public float        fValuePerLevel;     // 레벨 1당 오르는 수치

        /// <summary> 다음 레벨로 올리는 데 드는 비용. 만렙이면 0. </summary>
        public int Get_Cost(int iCurLevel)
        {
            if (iCurLevel >= iMaxLevel)
                return 0;

            return iCostBase + iCostAdd * Mathf.Max(0, iCurLevel);
        }

        public float Get_Value(int iLevel) => fValuePerLevel * Mathf.Clamp(iLevel, 0, iMaxLevel);
    }

    /// <summary>
    /// 클래스 이름은 Engine이 강제한다 — UpgradeInfo.csv ↔ CCSVData_UpgradeInfo.
    /// (자세한 규약은 CCSVData_EnemyInfo 설명 참고)
    /// </summary>
    public class CCSVData_UpgradeInfo : CCSVData
    {
        private const string TABLE_NAME = "UpgradeInfo";
        public  const string CSV_KEY    = "CCSVData_UpgradeInfo";

        private readonly Dictionary<STAT_TYPE, CUpgradeInfo> m_dicInfo = new Dictionary<STAT_TYPE, CUpgradeInfo>();
        private readonly List<CUpgradeInfo>                     m_lstInfo = new List<CUpgradeInfo>();

        public IReadOnlyList<CUpgradeInfo> ALL   => m_lstInfo;
        public int                         COUNT => m_lstInfo.Count;

        public CUpgradeInfo Get_Info(STAT_TYPE eType)
        {
            if (m_dicInfo.TryGetValue(eType, out CUpgradeInfo cInfo) == true)
                return cInfo;

            Debug.LogError($"[{TABLE_NAME}] {eType} 항목이 표에 없다.");
            return null;
        }

        // 헤더 순서와 1:1로 맞춘다.
        protected override void Parse_CSVData(string[] arrField)
        {
            // 260905_헤더 줄은 조용히 넘긴다 (CCSV_Utility.Is_HeaderRow 주석 참고).
            if (CCSV_Utility.Is_HeaderRow(arrField, "eType") == true)
                return;

            CUpgradeInfo cInfo = new CUpgradeInfo
            {
                eType           = CCSV_Utility.To_Enum(arrField, 0, STAT_TYPE.NONE),
                strName         = CCSV_Utility.To_String(arrField, 1),
                strDesc         = CCSV_Utility.To_String(arrField, 2),
                iMaxLevel       = CCSV_Utility.To_Int(arrField, 3, 1),
                iCostBase       = CCSV_Utility.To_Int(arrField, 4, 100),
                iCostAdd        = CCSV_Utility.To_Int(arrField, 5),
                fValuePerLevel  = CCSV_Utility.To_Float(arrField, 6),
            };

            if (cInfo.eType == STAT_TYPE.NONE)
            {
                Debug.LogError($"[{TABLE_NAME}] eType이 없는 행을 건너뛴다. "
                             + "STAT_TYPE 이름과 철자가 같아야 한다.");
                return;
            }

            if (m_dicInfo.ContainsKey(cInfo.eType) == true)
            {
                Debug.LogError($"[{TABLE_NAME}] {cInfo.eType}가 중복이다. 뒤의 행을 버린다.");
                return;
            }

            m_dicInfo.Add(cInfo.eType, cInfo);
            m_lstInfo.Add(cInfo);
        }
    }
}
