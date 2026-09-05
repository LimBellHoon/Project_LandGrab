using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260905_스킬 테이블 (Assets/Data/SkillInfo.csv)
    /// <summary> SkillInfo.csv 한 행 = 스킬 하나. </summary>
    public class CSkillInfo
    {
        public int              iSkillID;
        public SKILL_TYPE       eType;
        public SKILL_CATEGORY   eCategory;
        public string           strName;
        public string           strDesc;

        public float            fCoolTime;      // 초
        public float            fValue;         // 스킬마다 의미가 다르다. WARP은 이동할 칸 수
    }

    /// <summary>
    /// 클래스 이름은 Engine이 강제한다 — SkillInfo.csv ↔ CCSVData_SkillInfo.
    /// (자세한 규약은 CCSVData_EnemyInfo 설명 참고)
    /// </summary>
    public class CCSVData_SkillInfo : CCSVData
    {
        private const string TABLE_NAME = "SkillInfo";
        public  const string CSV_KEY    = "CCSVData_SkillInfo";

        private readonly Dictionary<int, CSkillInfo> m_dicInfo = new Dictionary<int, CSkillInfo>();
        private readonly List<CSkillInfo>            m_lstInfo = new List<CSkillInfo>();

        public IReadOnlyList<CSkillInfo> ALL   => m_lstInfo;
        public int                       COUNT => m_lstInfo.Count;

        public CSkillInfo Get_Info(int iSkillID)
        {
            if (m_dicInfo.TryGetValue(iSkillID, out CSkillInfo cInfo) == true)
                return cInfo;

            Debug.LogError($"[{TABLE_NAME}] ID {iSkillID}인 스킬이 표에 없다.");
            return null;
        }

        /// <summary> 타입으로 찾는다. 같은 타입이 여러 개면 첫 행. </summary>
        public CSkillInfo Find_ByType(SKILL_TYPE eType)
        {
            for (int i = 0; i < m_lstInfo.Count; ++i)
            {
                if (m_lstInfo[i].eType == eType)
                    return m_lstInfo[i];
            }

            return null;
        }

        // 헤더 순서와 1:1로 맞춘다.
        protected override void Parse_CSVData(string[] arrField)
        {
            CSkillInfo cInfo = new CSkillInfo
            {
                iSkillID    = CCSV_Utility.To_Int(arrField, 0),
                eType       = CCSV_Utility.To_Enum(arrField, 1, SKILL_TYPE.NONE),
                eCategory   = CCSV_Utility.To_Enum(arrField, 2, SKILL_CATEGORY.ACTIVE),
                strName     = CCSV_Utility.To_String(arrField, 3),
                strDesc     = CCSV_Utility.To_String(arrField, 4),
                fCoolTime   = CCSV_Utility.To_Float(arrField, 5, 5f),
                fValue      = CCSV_Utility.To_Float(arrField, 6),
            };

            if (cInfo.iSkillID <= 0 || cInfo.eType == SKILL_TYPE.NONE)
            {
                Debug.LogError($"[{TABLE_NAME}] iSkillID 또는 eType이 없는 행을 건너뛴다. "
                             + "eType은 SKILL_TYPE 이름과 철자가 같아야 한다.");
                return;
            }

            if (m_dicInfo.ContainsKey(cInfo.iSkillID) == true)
            {
                Debug.LogError($"[{TABLE_NAME}] iSkillID {cInfo.iSkillID}가 중복이다. 뒤의 행을 버린다.");
                return;
            }

            m_dicInfo.Add(cInfo.iSkillID, cInfo);
            m_lstInfo.Add(cInfo);
        }
    }
}
