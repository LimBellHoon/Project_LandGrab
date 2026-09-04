using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260904_맵 / 웨이브 테이블 (Assets/Data/MapInfo.csv)
    /// <summary> 웨이브 하나가 소환할 몬스터 한 종류. </summary>
    public struct CWaveEnemy
    {
        public int iEnemyID;
        public int iCount;
    }

    /// <summary> 웨이브 하나의 규칙. </summary>
    public class CWaveInfo
    {
        public float                fClearRatio;    // 이 웨이브를 넘기는 데 필요한 점령률
        public float                fTimeLimit;     // 이 웨이브의 제한 시간(초)
        public List<CWaveEnemy>     lstEnemy = new List<CWaveEnemy>();

        public int TOTAL_ENEMY
        {
            get
            {
                int iCount = 0;
                for (int i = 0; i < lstEnemy.Count; ++i)
                    iCount += lstEnemy[i].iCount;

                return iCount;
            }
        }
    }

    /// <summary>
    /// MapInfo.csv 한 행 = 맵 하나.
    /// 이미지 스택(lstLayerTex)은 (웨이브 수 + 1)장이다. N웨이브의 가림막이 [N-1]이고,
    /// 그걸 다 걷어내면 [N]이 드러난다 — 마지막 웨이브를 깨면 [웨이브 수]가 최종 보상으로 남는다.
    /// </summary>
    public class CMapInfo
    {
        public int      iMapID;
        public string   strMapName;

        public int      iGridWidth;
        public int      iGridHeight;
        public float    fCellSize;
        public int      iBorderThick;
        public int      iLife;
        public float    fPlayerSpeed;               // 초당 셀
        public int      iWaveCount;

        public string   strShapeMask;               // 맵 모양 텍스처. 비어 있으면 직사각형 전체
        public List<string>     lstLayerTex = new List<string>();
        public List<CWaveInfo>  lstWave     = new List<CWaveInfo>();

        public bool bIsValid;

        /// <param name="iWave"> 1부터 시작 </param>
        public CWaveInfo Get_Wave(int iWave)
        {
            int iIndex = iWave - 1;
            return iIndex >= 0 && iIndex < lstWave.Count ? lstWave[iIndex] : null;
        }

        /// <summary> iWave를 진행하는 동안 화면을 덮고 있는 이미지. </summary>
        public string Get_CoverTex(int iWave) => Get_LayerTex(iWave - 1);

        /// <summary> iWave를 다 점령했을 때 드러나는 이미지. </summary>
        public string Get_RevealTex(int iWave) => Get_LayerTex(iWave);

        private string Get_LayerTex(int iIndex)
            => iIndex >= 0 && iIndex < lstLayerTex.Count ? lstLayerTex[iIndex] : string.Empty;
    }

    /// <summary>
    /// 클래스 이름은 Engine이 강제한다 (CCSVData_EnemyInfo의 설명 참고).
    /// MapInfo.csv ↔ CCSVData_MapInfo.
    /// </summary>
    public class CCSVData_MapInfo : CCSVData
    {
        private const string TABLE_NAME = "MapInfo";
        public  const string CSV_KEY    = "CCSVData_MapInfo";

        private readonly Dictionary<int, CMapInfo>  m_dicInfo = new Dictionary<int, CMapInfo>();
        private readonly List<CMapInfo>             m_lstInfo = new List<CMapInfo>();

        public IReadOnlyList<CMapInfo> ALL      => m_lstInfo;
        public int                     COUNT    => m_lstInfo.Count;

        public CMapInfo Get_Info(int iMapID)
        {
            if (m_dicInfo.TryGetValue(iMapID, out CMapInfo cInfo) == true)
                return cInfo;

            Debug.LogError($"[{TABLE_NAME}] ID {iMapID}인 맵이 표에 없다.");
            return null;
        }

        // 헤더 순서와 1:1로 맞춘다.
        protected override void Parse_CSVData(string[] arrField)
        {
            CMapInfo cInfo = new CMapInfo
            {
                iMapID          = CCSV_Utility.To_Int(arrField, 0),
                strMapName      = CCSV_Utility.To_String(arrField, 1),
                iGridWidth      = CCSV_Utility.To_Int(arrField, 2, 60),
                iGridHeight     = CCSV_Utility.To_Int(arrField, 3, 100),
                fCellSize       = CCSV_Utility.To_Float(arrField, 4, 0.12f),
                iBorderThick    = CCSV_Utility.To_Int(arrField, 5, 2),
                iLife           = CCSV_Utility.To_Int(arrField, 6, 3),
                fPlayerSpeed    = CCSV_Utility.To_Float(arrField, 7, 9f),
                iWaveCount      = CCSV_Utility.To_Int(arrField, 8, 1),
                strShapeMask    = CCSV_Utility.To_String(arrField, 9),
            };

            if (cInfo.iMapID <= 0)
            {
                Debug.LogError($"[{TABLE_NAME}] iMapID가 없는 행을 건너뛴다.");
                return;
            }

            if (m_dicInfo.ContainsKey(cInfo.iMapID) == true)
            {
                Debug.LogError($"[{TABLE_NAME}] iMapID {cInfo.iMapID}가 중복이다. 뒤의 행을 버린다.");
                return;
            }

            cInfo.iWaveCount  = Mathf.Max(1, cInfo.iWaveCount);
            cInfo.lstLayerTex = CCSV_Utility.To_List(arrField, 10);

            Parse_Wave(cInfo, arrField);

            // 표가 조용히 어긋나면 스테이지가 통째로 이상해지므로 여기서 전부 잡는다.
            cInfo.bIsValid =
                CCSV_Utility.Check_Count(TABLE_NAME, cInfo.iMapID, "strLayerTex",
                                     cInfo.lstLayerTex.Count, cInfo.iWaveCount + 1)
              & CCSV_Utility.Check_Count(TABLE_NAME, cInfo.iMapID, "strWaveEnemy",
                                     cInfo.lstWave.Count, cInfo.iWaveCount);

            m_dicInfo.Add(cInfo.iMapID, cInfo);
            m_lstInfo.Add(cInfo);
        }

        /// <summary>
        /// 웨이브 3열(몬스터 / 점령률 / 제한시간)을 한 번에 엮는다.
        /// 몬스터 열이 웨이브 개수의 기준이고, 나머지는 모자라면 마지막 값을 이어 쓴다.
        /// </summary>
        private static void Parse_Wave(CMapInfo cInfo, string[] arrField)
        {
            List<string> lstWaveEnemy = CCSV_Utility.To_List(arrField, 11);
            List<float>  lstRatio     = CCSV_Utility.To_FloatList(arrField, 12);
            List<float>  lstTime      = CCSV_Utility.To_FloatList(arrField, 13);

            for (int i = 0; i < lstWaveEnemy.Count; ++i)
            {
                CWaveInfo cWave = new CWaveInfo
                {
                    fClearRatio = Get_ValueOrLast(lstRatio, i, 0.7f),
                    fTimeLimit  = Get_ValueOrLast(lstTime, i, 180f),
                };

                Parse_WaveEnemy(cInfo, i + 1, lstWaveEnemy[i], cWave.lstEnemy);
                cInfo.lstWave.Add(cWave);
            }
        }

        /// <summary> "101*2,102*1" → [(101,2), (102,1)] </summary>
        private static void Parse_WaveEnemy(CMapInfo cInfo, int iWave, string strWave, List<CWaveEnemy> lstOut)
        {
            string[] arrToken = strWave.Split(CCSV_Utility.SPLIT_ITEM);

            for (int i = 0; i < arrToken.Length; ++i)
            {
                string strToken = arrToken[i].Trim();
                if (strToken.Length == 0)
                    continue;

                string[] arrPair = strToken.Split(CCSV_Utility.SPLIT_COUNT);
                CWaveEnemy cEnemy = new CWaveEnemy
                {
                    iEnemyID = CCSV_Utility.To_Int(arrPair, 0),
                    iCount   = arrPair.Length > 1 ? CCSV_Utility.To_Int(arrPair, 1, 1) : 1,
                };

                if (cEnemy.iEnemyID <= 0 || cEnemy.iCount <= 0)
                {
                    Debug.LogError($"[{TABLE_NAME}] 맵 {cInfo.iMapID} / {iWave}웨이브 — "
                                 + $"'{strToken}'을 [ID]*[마리수]로 읽을 수 없다.");
                    continue;
                }

                lstOut.Add(cEnemy);
            }
        }

        private static float Get_ValueOrLast(List<float> lstValue, int iIndex, float fDefault)
        {
            if (lstValue.Count == 0)
                return fDefault;

            return lstValue[Mathf.Min(iIndex, lstValue.Count - 1)];
        }
    }
}
