using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260904_진행도 / 맵 해금
    /// <summary>
    /// 어떤 맵이 열려 있는지를 판정하고 클리어 기록을 저장한다.
    /// 해금은 순차 — MapInfo.csv에 적힌 순서로 바로 앞 맵을 깨야 다음이 열린다.
    /// ID 산술이 아니라 표의 순서를 기준으로 삼는다. 기획이 중간에 맵을 끼워 넣어도
    /// ID를 다시 매기지 않아도 되기 때문이다.
    ///
    /// 저장소는 IStageProgress로 갈아끼운다 (지금은 로컬, 나중에 백엔드).
    /// </summary>
    public class CProgress_Manager
    {
        private IStageProgress      m_cRepository;
        private CCSVData_MapInfo    m_cMapTable;
        private CStageProgress      m_cProgress = new CStageProgress();
        private bool                m_bUnlockAll;

        /// <summary> 디버그 전체 개방 여부. </summary>
        public bool IS_UNLOCK_ALL => m_bUnlockAll;
        public int  CLEARED_COUNT => m_cProgress.lstClearedMap.Count;

        public bool Initialize(CCSVData_MapInfo cMapTable, IStageProgress cRepository)
        {
            if (cMapTable == null || cRepository == null)
            {
                Debug.LogError("[CProgress_Manager] 맵 표 또는 저장소가 null 입니다.");
                return false;
            }

            m_cMapTable   = cMapTable;
            m_cRepository = cRepository;
            m_cProgress   = cRepository.Load();
            return true;
        }

        /// <summary> 디버그용 — 켜면 해금 규칙을 무시하고 전부 열린 것으로 본다. </summary>
        public void Set_UnlockAll(bool bUnlockAll) => m_bUnlockAll = bUnlockAll;

        public bool Is_Cleared(int iMapID) => m_cProgress.Is_Cleared(iMapID);

        /// <summary> 표의 첫 맵은 항상 열려 있고, 그 뒤는 바로 앞 맵을 깨야 열린다. </summary>
        public bool Is_Unlocked(int iMapID)
        {
            if (m_bUnlockAll == true)
                return true;

            int iIndex = Find_Index(iMapID);
            if (iIndex < 0)
                return false;

            if (iIndex == 0)
                return true;

            return m_cProgress.Is_Cleared(m_cMapTable.ALL[iIndex - 1].iMapID);
        }

        /// <summary> 클리어를 기록하고 저장한다. 이미 깬 맵이면 저장까지 가지 않는다. </summary>
        public void Set_Cleared(int iMapID)
        {
            if (m_cProgress.Set_Cleared(iMapID) == false)
                return;

            m_cRepository.Save(m_cProgress);
        }

        /// <summary> 마지막으로 고른 맵을 기억한다 — 선택 화면을 다시 열 때 그 자리로 돌아간다. </summary>
        public void Set_LastMap(int iMapID)
        {
            if (m_cProgress.iLastMapID == iMapID)
                return;

            m_cProgress.iLastMapID = iMapID;
            m_cRepository.Save(m_cProgress);
        }

        public int Get_LastMapID()
        {
            if (m_cProgress.iLastMapID > 0 && Is_Unlocked(m_cProgress.iLastMapID) == true)
                return m_cProgress.iLastMapID;

            IReadOnlyList<CMapInfo> lstMap = m_cMapTable.ALL;
            return lstMap.Count > 0 ? lstMap[0].iMapID : 0;
        }

        private int Find_Index(int iMapID)
        {
            IReadOnlyList<CMapInfo> lstMap = m_cMapTable.ALL;

            for (int i = 0; i < lstMap.Count; ++i)
            {
                if (lstMap[i].iMapID == iMapID)
                    return i;
            }

            return -1;
        }
    }
}
