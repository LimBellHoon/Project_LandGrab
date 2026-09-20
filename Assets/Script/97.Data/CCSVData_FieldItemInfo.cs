using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260920_맵 위에 놓이는 상호작용 아이템 (Assets/Data/FieldItemInfo.csv)
    /// <summary>
    /// 한 종류 = 한 줄. 무엇을 하는지는 eType이 정하고, 수치(fValue) · 지속(fDuration)의 뜻은 종류마다 다르다.
    /// 어디서 얼마나 나올지는 여기가 아니라 MapInfo.csv가 정한다 — 같은 아이템이라도 맵마다 자주/드물게
    /// 나와야 하기 때문이다(몬스터 종류와 웨이브 구성을 나눠 둔 것과 같은 자리, 2-5).
    /// </summary>
    public class CFieldItemInfo
    {
        public int              iItemID;
        public FIELD_ITEM_TYPE  eType;
        public string           strName;
        public string           strDesc;
        public int              iWeight;        // 뽑기 가중치. 0이면 안 나온다 — 표에서 지우지 않고 잠글 때 쓴다
        public float            fValue;         // MASS_STUN: 안 씀 / HEAL_LIFE: 회복할 목숨 수 / MAGNET_ALL: 안 씀
        public float            fDuration;      // MASS_STUN: 기절 시간(초) / 나머지: 안 씀
        public float            fLifeTime;      // 맵에 놓인 뒤 사라지기까지(초)
    }

    public class CCSVData_FieldItemInfo : CCSVData
    {
        private const string TABLE_NAME = "FieldItemInfo";
        public  const string CSV_KEY    = "CCSVData_FieldItemInfo";

        private readonly List<CFieldItemInfo> m_lstInfo = new List<CFieldItemInfo>();

        public IReadOnlyList<CFieldItemInfo> ALL   => m_lstInfo;
        public int                           COUNT => m_lstInfo.Count;

        protected override void Parse_CSVData(string[] arrField)
        {
            if (CCSV_Utility.Is_HeaderRow(arrField, "iItemID") == true)
                return;

            CFieldItemInfo cInfo = new CFieldItemInfo
            {
                iItemID   = CCSV_Utility.To_Int(arrField, 0),
                eType     = CCSV_Utility.To_Enum(arrField, 1, FIELD_ITEM_TYPE.NONE),
                strName   = CCSV_Utility.To_String(arrField, 2),
                strDesc   = CCSV_Utility.To_String(arrField, 3),
                iWeight   = CCSV_Utility.To_Int(arrField, 4),
                fValue    = CCSV_Utility.To_Float(arrField, 5),
                fDuration = CCSV_Utility.To_Float(arrField, 6),
                fLifeTime = CCSV_Utility.To_Float(arrField, 7, 10f),
            };

            if (cInfo.iItemID <= 0 || cInfo.eType == FIELD_ITEM_TYPE.NONE)
            {
                Debug.LogError($"[{TABLE_NAME}] iItemID가 없거나 eType을 못 읽은 행을 건너뛴다.");
                return;
            }

            m_lstInfo.Add(cInfo);
        }

        public CFieldItemInfo Find(int iItemID)
        {
            for (int i = 0; i < m_lstInfo.Count; ++i)
            {
                if (m_lstInfo[i].iItemID == iItemID)
                    return m_lstInfo[i];
            }

            return null;
        }

        public CFieldItemInfo Find_ByType(FIELD_ITEM_TYPE eType)
        {
            for (int i = 0; i < m_lstInfo.Count; ++i)
            {
                if (m_lstInfo[i].eType == eType)
                    return m_lstInfo[i];
            }

            return null;
        }

        /// <summary> 가중치로 하나 고른다. 나올 것이 없으면 null. </summary>
        public CFieldItemInfo Pick_Random()
        {
            List<CFieldItemInfo> lstPick = CWeightedPick_Utility.Pick(m_lstInfo, cInfo => cInfo.iWeight, 1);
            return lstPick.Count > 0 ? lstPick[0] : null;
        }
    }
}
