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
        public int  CLEARED_COUNT => m_cProgress.Get_ClearedCount();
        // 260905_재화·강화에서 쓸 총 별 개수
        public int  TOTAL_STAR    => m_cProgress.Get_TotalStar();

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

            // 260905_별이 없던 시절의 저장본이면 별 1개짜리 기록으로 옮긴다.
            if (m_cProgress.Migrate_Legacy() == true)
                m_cRepository.Save(m_cProgress);

            return true;
        }

        /// <summary> 디버그용 — 켜면 해금 규칙을 무시하고 전부 열린 것으로 본다. </summary>
        public void Set_UnlockAll(bool bUnlockAll) => m_bUnlockAll = bUnlockAll;

        public bool Is_Cleared(int iMapID) => m_cProgress.Is_Cleared(iMapID);
        // 260905_별 = 달성한 웨이브 수
        public int  Get_Star(int iMapID)  => m_cProgress.Get_Star(iMapID);

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

        // 260905_별 하나라도 얻으면 그 맵은 클리어다(웨이브 하나만 달성해도 클리어).
        /// <summary> 최고 기록을 갱신하고 저장한다. 기록이 나아지지 않으면 저장까지 가지 않는다. </summary>
        /// <returns>
        /// 늘어난 별 개수. 0이면 갱신 없음.
        /// 개수를 돌려주는 이유 — 재화를 '새로 딴 별만큼' 줘야 같은 판을 반복해 무한히 벌 수 없다.
        /// </returns>
        public int Set_Star(int iMapID, int iStar)
        {
            int iPrev = m_cProgress.Get_Star(iMapID);

            if (m_cProgress.Set_Star(iMapID, iStar) == false)
                return 0;

            m_cRepository.Save(m_cProgress);
            return m_cProgress.Get_Star(iMapID) - iPrev;
        }

        // 260905_재화
        public int COIN => m_cProgress.iCoin;

        public void Add_Coin(int iAmount)
        {
            if (iAmount <= 0)
                return;

            m_cProgress.Add_Coin(iAmount);
            m_cRepository.Save(m_cProgress);
        }

        // 260905_능력치 강화
        public int Get_UpgradeLevel(UPGRADE_TYPE eType) => m_cProgress.Get_UpgradeLevel(eType);

        /// <summary> 지금 레벨에서 적용될 수치. 표가 없으면 0(강화 없음)으로 본다. </summary>
        public float Get_UpgradeValue(CCSVData_UpgradeInfo cTable, UPGRADE_TYPE eType)
        {
            CUpgradeInfo cInfo = cTable != null ? cTable.Get_Info(eType) : null;
            return cInfo != null ? cInfo.Get_Value(Get_UpgradeLevel(eType)) : 0f;
        }

        /// <summary> 다음 레벨 비용. 만렙이면 0. </summary>
        public int Get_UpgradeCost(CCSVData_UpgradeInfo cTable, UPGRADE_TYPE eType)
        {
            CUpgradeInfo cInfo = cTable != null ? cTable.Get_Info(eType) : null;
            return cInfo != null ? cInfo.Get_Cost(Get_UpgradeLevel(eType)) : 0;
        }

        public bool Is_UpgradeMax(CCSVData_UpgradeInfo cTable, UPGRADE_TYPE eType)
        {
            CUpgradeInfo cInfo = cTable != null ? cTable.Get_Info(eType) : null;
            return cInfo != null && Get_UpgradeLevel(eType) >= cInfo.iMaxLevel;
        }

        /// <summary> 코인이 모자라거나 만렙이면 아무 일도 일어나지 않는다. </summary>
        public bool Try_Upgrade(CCSVData_UpgradeInfo cTable, UPGRADE_TYPE eType)
        {
            CUpgradeInfo cInfo = cTable != null ? cTable.Get_Info(eType) : null;
            if (cInfo == null)
                return false;

            int iLevel = Get_UpgradeLevel(eType);
            if (iLevel >= cInfo.iMaxLevel)
                return false;

            if (m_cProgress.Use_Coin(cInfo.Get_Cost(iLevel)) == false)
                return false;

            m_cProgress.Set_UpgradeLevel(eType, iLevel + 1);
            m_cRepository.Save(m_cProgress);
            return true;
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
