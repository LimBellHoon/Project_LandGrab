using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260904_몬스터 기믹 테이블 (Assets/Data/EnemyInfo.csv)
    /// <summary> EnemyInfo.csv 한 행 = 몬스터 한 종류. </summary>
    public class CEnemyInfo
    {
        public int              iEnemyID;
        public string           strName;
        public string           strPrefabName;
        public ENEMY_GIMMICK    eGimmick;

        public float            fSpeed;         // 배회 속도 (초당 셀)
        public float            fChaseSpeed;    // 추적 속도 (초당 셀)
        public float            fTurnRate;      // 선회 속도 (초당 라디안)
        public float            fHitRange;      // 플레이어와의 충돌 반경 (셀)

        // 기믹마다 의미가 다르다 — EnemyInfo.csv 머리의 주석을 볼 것.
        public float            fGimmickCool;       // 발동 주기 (초)
        public float            fGimmickValue;      // 주 수치 (탄속 / 속도배율 / 소환 마리수)
        public float            fGimmickRange;      // 사거리 (셀)
        public float            fGimmickDuration;   // 지속 시간 (초)
        public int              iGimmickRefID;      // 참조할 몬스터 ID (SPAWN의 소환 대상)
    }

    /// <summary>
    /// 클래스 이름은 Engine이 강제한다 — CCSVDataHolder가 TextAsset 파일명을 보고
    /// "Client.CCSVData_&lt;파일명&gt;" 타입을 찾아 Activator로 만든다.
    /// 따라서 EnemyInfo.csv ↔ CCSVData_EnemyInfo 이름은 절대 어긋나면 안 된다.
    /// 조회는 CGameInstance.Instance.Get_CSVData&lt;CCSVData_EnemyInfo&gt;("CCSVData_EnemyInfo").
    /// </summary>
    public class CCSVData_EnemyInfo : CCSVData
    {
        private const string TABLE_NAME = "EnemyInfo";
        public  const string CSV_KEY    = "CCSVData_EnemyInfo";

        private readonly Dictionary<int, CEnemyInfo>    m_dicInfo = new Dictionary<int, CEnemyInfo>();
        private readonly List<CEnemyInfo>               m_lstInfo = new List<CEnemyInfo>();

        public IReadOnlyList<CEnemyInfo> ALL    => m_lstInfo;
        public int                       COUNT  => m_lstInfo.Count;

        public CEnemyInfo Get_Info(int iEnemyID)
        {
            if (m_dicInfo.TryGetValue(iEnemyID, out CEnemyInfo cInfo) == true)
                return cInfo;

            Debug.LogError($"[{TABLE_NAME}] ID {iEnemyID}인 몬스터가 표에 없다.");
            return null;
        }

        // 헤더 순서와 1:1로 맞춘다. CSV의 열 순서를 바꾸면 여기도 같이 바꿔야 한다.
        protected override void Parse_CSVData(string[] arrField)
        {
            // 260905_헤더 줄은 조용히 넘긴다 (CCSV_Utility.Is_HeaderRow 주석 참고).
            if (CCSV_Utility.Is_HeaderRow(arrField, "iEnemyID") == true)
                return;

            CEnemyInfo cInfo = new CEnemyInfo
            {
                iEnemyID        = CCSV_Utility.To_Int(arrField, 0),
                strName         = CCSV_Utility.To_String(arrField, 1),
                strPrefabName   = CCSV_Utility.To_String(arrField, 2, "Prefab_Enemy"),
                eGimmick        = CCSV_Utility.To_Enum(arrField, 3, ENEMY_GIMMICK.NONE),
                fSpeed          = CCSV_Utility.To_Float(arrField, 4, 5f),
                fChaseSpeed     = CCSV_Utility.To_Float(arrField, 5, 7f),
                fTurnRate       = CCSV_Utility.To_Float(arrField, 6, 5f),
                fHitRange       = CCSV_Utility.To_Float(arrField, 7, 1.2f),
                fGimmickCool    = CCSV_Utility.To_Float(arrField, 8),
                fGimmickValue   = CCSV_Utility.To_Float(arrField, 9),
                fGimmickRange   = CCSV_Utility.To_Float(arrField, 10),
                fGimmickDuration= CCSV_Utility.To_Float(arrField, 11),
                iGimmickRefID   = CCSV_Utility.To_Int(arrField, 12),
            };

            if (cInfo.iEnemyID <= 0)
            {
                Debug.LogError($"[{TABLE_NAME}] iEnemyID가 없는 행을 건너뛴다.");
                return;
            }

            if (m_dicInfo.ContainsKey(cInfo.iEnemyID) == true)
            {
                Debug.LogError($"[{TABLE_NAME}] iEnemyID {cInfo.iEnemyID}가 중복이다. 뒤의 행을 버린다.");
                return;
            }

            m_dicInfo.Add(cInfo.iEnemyID, cInfo);
            m_lstInfo.Add(cInfo);
        }
    }
}
