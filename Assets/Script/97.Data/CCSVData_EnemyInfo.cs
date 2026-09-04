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

        public float            fGimmickCool;   // 기믹 재사용 대기 (초)
        public float            fGimmickValue;  // 기믹별 의미가 다른 수치 — CSV 메모 칸 참고
        public float            fGimmickRange;  // 기믹 사거리 (셀)
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
        protected override void Parse_CSVData(string[] strField)
        {
            CEnemyInfo cInfo = new CEnemyInfo
            {
                iEnemyID        = CCsvUtil.To_Int(strField, 0),
                strName         = CCsvUtil.To_String(strField, 1),
                strPrefabName   = CCsvUtil.To_String(strField, 2, "Prefab_Enemy"),
                eGimmick        = CCsvUtil.To_Enum(strField, 3, ENEMY_GIMMICK.NONE),
                fSpeed          = CCsvUtil.To_Float(strField, 4, 5f),
                fChaseSpeed     = CCsvUtil.To_Float(strField, 5, 7f),
                fTurnRate       = CCsvUtil.To_Float(strField, 6, 5f),
                fHitRange       = CCsvUtil.To_Float(strField, 7, 1.2f),
                fGimmickCool    = CCsvUtil.To_Float(strField, 8),
                fGimmickValue   = CCsvUtil.To_Float(strField, 9),
                fGimmickRange   = CCsvUtil.To_Float(strField, 10),
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
