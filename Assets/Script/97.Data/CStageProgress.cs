using System;
using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260904_진행도 (클리어한 맵 기록)
    /// <summary>
    /// 통째로 JSON이 되는 모양으로 둔다. 나중에 백엔드에 올릴 때 이 덩어리를 그대로 보내면 되고,
    /// 스키마가 늘어도 저장 코드는 손댈 일이 없다.
    /// </summary>
    [Serializable]
    public class CStageProgress
    {
        public List<int>    lstClearedMap = new List<int>();
        public int          iLastMapID;

        public bool Is_Cleared(int iMapID) => lstClearedMap.Contains(iMapID);

        public bool Set_Cleared(int iMapID)
        {
            if (iMapID <= 0 || Is_Cleared(iMapID) == true)
                return false;

            lstClearedMap.Add(iMapID);
            return true;
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
