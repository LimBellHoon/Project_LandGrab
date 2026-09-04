using System;
using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260904_진행도 (클리어한 맵 기록)
    // 260905_별 기록으로 확장 — 맵마다 '몇 웨이브까지 갔는가'를 남긴다.
    /// <summary>
    /// 맵 하나의 최고 기록. 별 = 달성한 웨이브 수다.
    /// JsonUtility는 Dictionary를 직렬화하지 못해 리스트로 둔다.
    /// </summary>
    [Serializable]
    public class CMapRecord
    {
        public int iMapID;
        public int iStar;
    }

    /// <summary>
    /// 통째로 JSON이 되는 모양으로 둔다. 나중에 백엔드에 올릴 때 이 덩어리를 그대로 보내면 되고,
    /// 스키마가 늘어도 저장 코드는 손댈 일이 없다.
    /// </summary>
    [Serializable]
    public class CStageProgress
    {
        public List<CMapRecord> lstRecord = new List<CMapRecord>();
        public int              iLastMapID;

        // 260905_구버전(별이 없던 시절) 기록. Migrate_Legacy로 옮기고 비운다.
        // 필드를 지우면 JsonUtility가 옛 저장본을 읽을 때 그냥 버려서 진행도가 날아간다.
        public List<int>        lstClearedMap = new List<int>();

        /// <summary> 별 하나라도 얻었으면 그 맵은 클리어한 것이다(웨이브 하나만 달성해도 클리어). </summary>
        public bool Is_Cleared(int iMapID) => Get_Star(iMapID) >= 1;

        public int Get_Star(int iMapID)
        {
            CMapRecord cRecord = Find(iMapID);
            return cRecord != null ? cRecord.iStar : 0;
        }

        /// <summary> 최고 기록만 남긴다. </summary>
        /// <returns> 기록이 갱신됐으면 true </returns>
        public bool Set_Star(int iMapID, int iStar)
        {
            if (iMapID <= 0 || iStar <= 0)
                return false;

            CMapRecord cRecord = Find(iMapID);
            if (cRecord == null)
            {
                lstRecord.Add(new CMapRecord { iMapID = iMapID, iStar = iStar });
                return true;
            }

            if (cRecord.iStar >= iStar)
                return false;

            cRecord.iStar = iStar;
            return true;
        }

        public int Get_ClearedCount()
        {
            int iCount = 0;
            for (int i = 0; i < lstRecord.Count; ++i)
            {
                if (lstRecord[i].iStar >= 1)
                    ++iCount;
            }
            return iCount;
        }

        public int Get_TotalStar()
        {
            int iTotal = 0;
            for (int i = 0; i < lstRecord.Count; ++i)
                iTotal += lstRecord[i].iStar;

            return iTotal;
        }

        // 260905_별이 없던 시절의 저장본을 별 1개짜리 기록으로 옮긴다.
        // 클리어 여부만 알 뿐 몇 웨이브까지 갔는지는 모르므로, 최소값인 1을 준다.
        /// <returns> 옮긴 게 있으면 true (저장이 필요하다) </returns>
        public bool Migrate_Legacy()
        {
            if (lstClearedMap.Count == 0)
                return false;

            for (int i = 0; i < lstClearedMap.Count; ++i)
                Set_Star(lstClearedMap[i], 1);

            lstClearedMap.Clear();
            return true;
        }

        private CMapRecord Find(int iMapID)
        {
            for (int i = 0; i < lstRecord.Count; ++i)
            {
                if (lstRecord[i].iMapID == iMapID)
                    return lstRecord[i];
            }

            return null;
        }
    }

    /// <summary>
    /// 로컬 저장 구현. JSON 문자열을 PlayerPrefs에 넣는다.
    /// 파일 IO보다 단순하면서도, 저장하는 알맹이가 JSON이라 그대로 백엔드로 옮길 수 있다.
    /// </summary>
    public class CStageProgress_Local : IStageProgress
    {
        private const string SAVE_KEY = "LandGrab_StageProgress";

        public CStageProgress Load()
        {
            string strJson = PlayerPrefs.GetString(SAVE_KEY, string.Empty);
            if (string.IsNullOrEmpty(strJson) == true)
                return new CStageProgress();

            try
            {
                // 저장 포맷이 바뀌거나 파일이 깨져도 게임은 떠야 한다 — 실패하면 빈 기록으로 시작한다.
                CStageProgress cProgress = JsonUtility.FromJson<CStageProgress>(strJson);
                return cProgress ?? new CStageProgress();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CStageProgress_Local] 저장 기록을 읽지 못해 새로 시작합니다 : {e.Message}");
                return new CStageProgress();
            }
        }

        public void Save(CStageProgress cProgress)
        {
            if (cProgress == null)
                return;

            PlayerPrefs.SetString(SAVE_KEY, JsonUtility.ToJson(cProgress));
            PlayerPrefs.Save();
        }
    }
}
