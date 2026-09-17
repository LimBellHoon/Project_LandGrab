using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260918_장비 뽑기 (Assets/Data/GachaInfo.csv) — 가방 하단의 BM 자리
    /// <summary>
    /// 뽑기 한 종류 = 한 줄. 무엇이 나올지는 EquipInfo.csv의 iGachaWeight가 정한다(0이면 안 나온다).
    /// 줄을 나눠 둔 이유 — 나중에 유료 재화 뽑기 · 기간 한정 뽑기가 붙으면 줄만 늘리면 된다.
    /// </summary>
    public class CGachaInfo
    {
        public int      iGachaID;
        public string   strName;
        public int      iCost;              // 코인
        // 이미 가진 장비가 또 나왔을 때 — 만렙 전이면 강화 1레벨, 만렙이면 비용의 이 비율만큼 코인을 돌려준다
        public float    fDuplicateRefundRate;

        public int REFUND => Mathf.Max(0, Mathf.RoundToInt(iCost * fDuplicateRefundRate));
    }

    public class CCSVData_GachaInfo : CCSVData
    {
        private const string TABLE_NAME = "GachaInfo";
        public  const string CSV_KEY    = "CCSVData_GachaInfo";

        private readonly List<CGachaInfo> m_lstInfo = new List<CGachaInfo>();

        public IReadOnlyList<CGachaInfo> ALL   => m_lstInfo;
        public int                       COUNT => m_lstInfo.Count;

        /// <summary> 표의 첫 뽑기. 가방 버튼은 이것 하나를 쓴다. 없으면 null. </summary>
        public CGachaInfo DEFAULT => m_lstInfo.Count > 0 ? m_lstInfo[0] : null;

        protected override void Parse_CSVData(string[] arrField)
        {
            if (CCSV_Utility.Is_HeaderRow(arrField, "iGachaID") == true)
                return;

            CGachaInfo cInfo = new CGachaInfo
            {
                iGachaID             = CCSV_Utility.To_Int(arrField, 0),
                strName              = CCSV_Utility.To_String(arrField, 1),
                iCost                = CCSV_Utility.To_Int(arrField, 2),
                fDuplicateRefundRate = CCSV_Utility.To_Float(arrField, 3, 0.5f),
            };

            if (cInfo.iGachaID <= 0 || cInfo.iCost < 0)
            {
                Debug.LogError($"[{TABLE_NAME}] iGachaID가 없거나 비용이 음수인 행을 건너뛴다.");
                return;
            }

            m_lstInfo.Add(cInfo);
        }
    }
}
