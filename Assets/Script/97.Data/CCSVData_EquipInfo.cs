using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260905_장비 테이블 (Assets/Data/EquipInfo.csv)
    /// <summary> EquipInfo.csv 한 행 = 장비 하나. </summary>
    public class CEquipInfo
    {
        public int          iEquipID;
        public EQUIP_SLOT   eSlot;
        public string       strName;
        public string       strDesc;

        public STAT_TYPE    eStat;          // 올려 주는 능력치. 소모품은 NONE
        public float        fStatValue;     // SPEED는 배율, EVASION은 확률, HP는 목숨 개수
        public int          iPrice;         // 상점 가격 (코인)
        public CONSUME_EFFECT eConsume;     // 소모품이 주는 효과. 장비는 NONE

        public bool IS_CONSUMABLE => eSlot == EQUIP_SLOT.CONSUMABLE;
    }

    /// <summary>
    /// 클래스 이름은 Engine이 강제한다 — EquipInfo.csv ↔ CCSVData_EquipInfo.
    /// (자세한 규약은 CCSVData_EnemyInfo 설명 참고)
    /// </summary>
    public class CCSVData_EquipInfo : CCSVData
    {
        private const string TABLE_NAME = "EquipInfo";
        public  const string CSV_KEY    = "CCSVData_EquipInfo";

        private readonly Dictionary<int, CEquipInfo> m_dicInfo = new Dictionary<int, CEquipInfo>();
        private readonly List<CEquipInfo>            m_lstInfo = new List<CEquipInfo>();

        public IReadOnlyList<CEquipInfo> ALL   => m_lstInfo;
        public int                       COUNT => m_lstInfo.Count;

        public CEquipInfo Get_Info(int iEquipID)
        {
            if (m_dicInfo.TryGetValue(iEquipID, out CEquipInfo cInfo) == true)
                return cInfo;

            Debug.LogError($"[{TABLE_NAME}] ID {iEquipID}인 장비가 표에 없다.");
            return null;
        }

        /// <summary> 해당 슬롯에 낄 수 있는 장비만 모아 돌려준다. UI 목록을 만들 때 쓴다. </summary>
        public void Collect_BySlot(EQUIP_SLOT eSlot, List<CEquipInfo> lstResult)
        {
            if (lstResult == null)
                return;

            lstResult.Clear();

            for (int i = 0; i < m_lstInfo.Count; ++i)
            {
                if (m_lstInfo[i].eSlot == eSlot)
                    lstResult.Add(m_lstInfo[i]);
            }
        }

        // 헤더 순서와 1:1로 맞춘다.
        protected override void Parse_CSVData(string[] arrField)
        {
            CEquipInfo cInfo = new CEquipInfo
            {
                iEquipID    = CCSV_Utility.To_Int(arrField, 0),
                eSlot       = CCSV_Utility.To_Enum(arrField, 1, EQUIP_SLOT.NONE),
                strName     = CCSV_Utility.To_String(arrField, 2),
                strDesc     = CCSV_Utility.To_String(arrField, 3),
                eStat       = CCSV_Utility.To_Enum(arrField, 4, STAT_TYPE.NONE),
                fStatValue  = CCSV_Utility.To_Float(arrField, 5),
                iPrice      = CCSV_Utility.To_Int(arrField, 6),
                eConsume    = CCSV_Utility.To_Enum(arrField, 7, CONSUME_EFFECT.NONE),
            };

            if (cInfo.iEquipID <= 0 || cInfo.eSlot == EQUIP_SLOT.NONE)
            {
                Debug.LogError($"[{TABLE_NAME}] iEquipID 또는 eSlot이 없는 행을 건너뛴다. "
                             + "eSlot은 EQUIP_SLOT 이름과 철자가 같아야 한다.");
                return;
            }

            if (m_dicInfo.ContainsKey(cInfo.iEquipID) == true)
            {
                Debug.LogError($"[{TABLE_NAME}] iEquipID {cInfo.iEquipID}가 중복이다. 뒤의 행을 버린다.");
                return;
            }

            m_dicInfo.Add(cInfo.iEquipID, cInfo);
            m_lstInfo.Add(cInfo);
        }
    }
}
