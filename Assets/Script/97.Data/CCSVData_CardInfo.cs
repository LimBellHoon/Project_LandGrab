using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260912_카드 한 장
    /// <summary>
    /// 점령률을 넘길 때마다 셋 중 하나를 고르는 카드. 고른 판에서만 유지된다.
    /// 수치의 뜻은 종류마다 다르다 — CardInfo.csv 머리의 주석이 기준이다.
    /// </summary>
    public class CCardInfo
    {
        public int          iCardID;
        public CARD_TYPE    eType;
        public string       strName;
        public string       strDesc;
        public float        fValue;
        public int          iWeight;    // 뽑힐 가중치. 0이면 안 나온다
        // 260920_3지선다 카드의 색 테마(2-10-1). 레이아웃은 같고 색만 다르다
        public PICK_THEME   eTheme;
    }

    public class CCSVData_CardInfo : CCSVData
    {
        public const string CSV_KEY = "CCSVData_CardInfo";

        private readonly List<CCardInfo> m_lstInfo = new List<CCardInfo>();

        public IReadOnlyList<CCardInfo> ALL => m_lstInfo;
        public int COUNT => m_lstInfo.Count;

        public CCardInfo Get_Info(int iCardID)
        {
            for (int i = 0; i < m_lstInfo.Count; ++i)
            {
                if (m_lstInfo[i].iCardID == iCardID)
                    return m_lstInfo[i];
            }

            return null;
        }

        // 260912_가중치로 뽑되 같은 카드가 한 번에 두 장 나오지 않게 한다.
        // 셋 중 하나를 고르는 재미는 '서로 다른 선택지'에서 나온다.
        /// <summary> 서로 다른 카드를 iCount장 뽑는다. 표에 그만큼 없으면 있는 만큼만 준다. </summary>
        public List<CCardInfo> Pick_Random(int iCount, List<CCardInfo> lstResult = null)
            => CWeightedPick_Utility.Pick(m_lstInfo, cInfo => cInfo.iWeight, iCount, lstResult);

        protected override void Parse_CSVData(string[] arrField)
        {
            // 260905_헤더 줄은 조용히 넘긴다 (CCSV_Utility.Is_HeaderRow 주석 참고).
            if (CCSV_Utility.Is_HeaderRow(arrField, "iCardID") == true)
                return;

            CCardInfo cInfo = new CCardInfo
            {
                iCardID = CCSV_Utility.To_Int(arrField, 0),
                eType   = CCSV_Utility.To_Enum(arrField, 1, CARD_TYPE.NONE),
                strName = CCSV_Utility.To_String(arrField, 2),
                strDesc = CCSV_Utility.To_String(arrField, 3),
                fValue  = CCSV_Utility.To_Float(arrField, 4),
                iWeight = CCSV_Utility.To_Int(arrField, 5, 10),
                // 260920_카드 색 테마(2-10-1). 안 적으면 전투로 본다
                eTheme  = CCSV_Utility.To_Enum(arrField, 6, PICK_THEME.COMBAT),
            };

            if (cInfo.iCardID <= 0 || cInfo.eType == CARD_TYPE.NONE)
            {
                Debug.LogError("[CardInfo] iCardID 또는 eType이 없는 행을 건너뛴다. "
                             + "eType은 CARD_TYPE 이름과 철자가 같아야 한다.");
                return;
            }

            m_lstInfo.Add(cInfo);
        }
    }
}
