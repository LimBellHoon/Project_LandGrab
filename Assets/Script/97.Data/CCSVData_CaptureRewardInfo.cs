using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260918_점령 재화 배율 표 (Assets/Data/CaptureRewardInfo.csv)
    /// <summary>
    /// 한 번의 점령이 맵 전체에서 차지하는 비율에 따라 코인 배율을 정한다.
    /// "안전하게 조금씩" 과 "위험을 감수하고 크게 한 방" 사이에 실제 이득 차이를 만들기 위한 표다.
    /// </summary>
    public class CCaptureRewardInfo
    {
        public int      iTier;
        public float    fRatioMin;
        public float    fRatioMax;
        public float    fMultiplier;
    }

    /// <summary>
    /// 클래스 이름은 Engine이 강제한다 (CCSVData_EnemyInfo의 설명 참고).
    /// CaptureRewardInfo.csv ↔ CCSVData_CaptureRewardInfo.
    /// </summary>
    public class CCSVData_CaptureRewardInfo : CCSVData
    {
        private const string TABLE_NAME = "CaptureRewardInfo";
        public  const string CSV_KEY    = "CCSVData_CaptureRewardInfo";

        // 260918_iTier 순서(=표에 적힌 순서)가 비율 오름차순이라고 신뢰한다 — CCSVData_CharacterInfo의
        // lstStarLevel과 같은 전제다. 정렬을 따로 하지 않으므로 표를 작성할 때 오름차순을 지킬 것.
        private readonly List<CCaptureRewardInfo> m_lstInfo = new List<CCaptureRewardInfo>();

        public IReadOnlyList<CCaptureRewardInfo> ALL => m_lstInfo;

        /// <summary> 표가 비어 있으면 배율 없음(1배)으로 본다. </summary>
        public float Get_Multiplier(float fRatio)
        {
            if (m_lstInfo.Count == 0)
                return 1f;

            for (int i = 0; i < m_lstInfo.Count; ++i)
            {
                if (fRatio < m_lstInfo[i].fRatioMax)
                    return m_lstInfo[i].fMultiplier;
            }

            // 표에 없는 구간(더 큰 비율)은 마지막 구간 배율을 그대로 쓴다.
            return m_lstInfo[m_lstInfo.Count - 1].fMultiplier;
        }

        protected override void Parse_CSVData(string[] arrField)
        {
            // 260918_헤더 줄은 조용히 넘긴다 (CCSV_Utility.Is_HeaderRow 주석 참고).
            if (CCSV_Utility.Is_HeaderRow(arrField, "iTier") == true)
                return;

            CCaptureRewardInfo cInfo = new CCaptureRewardInfo
            {
                iTier       = CCSV_Utility.To_Int(arrField, 0),
                fRatioMin   = CCSV_Utility.To_Float(arrField, 1),
                fRatioMax   = CCSV_Utility.To_Float(arrField, 2, 1f),
                fMultiplier = CCSV_Utility.To_Float(arrField, 3, 1f),
            };

            if (cInfo.iTier <= 0)
            {
                Debug.LogError($"[{TABLE_NAME}] iTier가 없는 행을 건너뛴다.");
                return;
            }

            m_lstInfo.Add(cInfo);
        }
    }
}
