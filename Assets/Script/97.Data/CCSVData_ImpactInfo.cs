using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260917_맞은 대상에게 남는 효과 — Project_GYM SkillImpactInfo.csv
    public class CImpactInfo
    {
        public int          iImpactID;
        public string       strName;
        public IMPACT_TYPE  eType;
        // 지속 시간(초). -1이면 탄에 닿아 있는 동안만 (GYM과 같은 규칙)
        public float        fTime;
        // STUN 미사용 / SLOW 속도 배율(0.5면 절반) / DOT 초당 피해 / KNOCKBACK 밀리는 거리(셀)
        public float        fValue;
        // EXPLODE가 터뜨릴 ProjectileInfo ID. GYM은 201로 박아 두었다
        public int          iRefID;

        public bool IS_WHILE_CONTACT => fTime < 0f;
    }

    public class CCSVData_ImpactInfo : CCSVData
    {
        public const string CSV_KEY = "CCSVData_ImpactInfo";
        private const string TABLE_NAME = "ImpactInfo";

        private readonly Dictionary<int, CImpactInfo> m_dicInfo = new Dictionary<int, CImpactInfo>();
        private readonly List<CImpactInfo> m_lstInfo = new List<CImpactInfo>();

        public IReadOnlyList<CImpactInfo> ALL => m_lstInfo;
        public int COUNT => m_lstInfo.Count;

        public CImpactInfo Get_Info(int iImpactID)
            => m_dicInfo.TryGetValue(iImpactID, out CImpactInfo cInfo) ? cInfo : null;

        protected override void Parse_CSVData(string[] arrField)
        {
            if (CCSV_Utility.Is_HeaderRow(arrField, "iImpactID") == true)
                return;

            CImpactInfo cInfo = new CImpactInfo
            {
                iImpactID   = CCSV_Utility.To_Int(arrField, 0),
                strName     = CCSV_Utility.To_String(arrField, 1),
                eType       = CCSV_Utility.To_Enum(arrField, 2, IMPACT_TYPE.NONE),
                fTime       = CCSV_Utility.To_Float(arrField, 3),
                fValue      = CCSV_Utility.To_Float(arrField, 4),
                iRefID      = CCSV_Utility.To_Int(arrField, 5),
            };

            if (cInfo.iImpactID <= 0 || cInfo.eType == IMPACT_TYPE.NONE)
            {
                Debug.LogError($"[{TABLE_NAME}] iImpactID 또는 eType이 없는 행을 건너뛴다. "
                             + "eType은 IMPACT_TYPE 이름과 철자가 같아야 한다.");
                return;
            }

            if (m_dicInfo.ContainsKey(cInfo.iImpactID) == true)
            {
                Debug.LogError($"[{TABLE_NAME}] iImpactID {cInfo.iImpactID}가 중복이다. 뒤의 행을 버린다.");
                return;
            }

            m_dicInfo.Add(cInfo.iImpactID, cInfo);
            m_lstInfo.Add(cInfo);
        }
    }
}
