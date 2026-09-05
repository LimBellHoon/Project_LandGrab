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
        private CCSVData_EquipInfo  m_cEquipTable;
        private CStageProgress      m_cProgress = new CStageProgress();
        private bool                m_bUnlockAll;

        /// <summary> 디버그 전체 개방 여부. </summary>
        public bool IS_UNLOCK_ALL => m_bUnlockAll;
        public int  CLEARED_COUNT => m_cProgress.Get_ClearedCount();
        // 260905_재화·강화에서 쓸 총 별 개수
        public int  TOTAL_STAR    => m_cProgress.Get_TotalStar();

        // 260905_장비 표는 슬롯 판별과 스탯 합산에 필요하다. 없으면 인벤토리 기능만 꺼진다.
        public bool Initialize(CCSVData_MapInfo cMapTable, IStageProgress cRepository,
                               CCSVData_EquipInfo cEquipTable = null)
        {
            if (cMapTable == null || cRepository == null)
            {
                Debug.LogError("[CProgress_Manager] 맵 표 또는 저장소가 null 입니다.");
                return false;
            }

            m_cMapTable   = cMapTable;
            m_cEquipTable = cEquipTable;
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
        public int Get_UpgradeLevel(STAT_TYPE eType) => m_cProgress.Get_UpgradeLevel(eType);

        /// <summary> 지금 레벨에서 적용될 수치. 표가 없으면 0(강화 없음)으로 본다. </summary>
        public float Get_UpgradeValue(CCSVData_UpgradeInfo cTable, STAT_TYPE eType)
        {
            CUpgradeInfo cInfo = cTable != null ? cTable.Get_Info(eType) : null;
            return cInfo != null ? cInfo.Get_Value(Get_UpgradeLevel(eType)) : 0f;
        }

        /// <summary> 다음 레벨 비용. 만렙이면 0. </summary>
        public int Get_UpgradeCost(CCSVData_UpgradeInfo cTable, STAT_TYPE eType)
        {
            CUpgradeInfo cInfo = cTable != null ? cTable.Get_Info(eType) : null;
            return cInfo != null ? cInfo.Get_Cost(Get_UpgradeLevel(eType)) : 0;
        }

        public bool Is_UpgradeMax(CCSVData_UpgradeInfo cTable, STAT_TYPE eType)
        {
            CUpgradeInfo cInfo = cTable != null ? cTable.Get_Info(eType) : null;
            return cInfo != null && Get_UpgradeLevel(eType) >= cInfo.iMaxLevel;
        }

        /// <summary> 코인이 모자라거나 만렙이면 아무 일도 일어나지 않는다. </summary>
        public bool Try_Upgrade(CCSVData_UpgradeInfo cTable, STAT_TYPE eType)
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

        #region 260905_인벤토리 (장비 · 소모품 · 스킬)
        public int  Get_ItemCount(int iEquipID)  => m_cProgress.Get_ItemCount(iEquipID);
        public bool Has_Item(int iEquipID)       => m_cProgress.Has_Item(iEquipID);
        public bool Is_Equipped(int iEquipID)    => m_cProgress.Is_Equipped(iEquipID);
        public int  EQUIPPED_SKILL_ID            => m_cProgress.iEquippedSkillID;

        public void Add_Item(int iEquipID, int iCount = 1)
        {
            if (iEquipID <= 0 || iCount <= 0)
                return;

            m_cProgress.Add_Item(iEquipID, iCount);
            m_cRepository.Save(m_cProgress);
        }

        /// <summary> 소모품을 쓴다. 없으면 아무 일도 없다. </summary>
        public bool Use_Item(int iEquipID, int iCount = 1)
        {
            if (m_cProgress.Use_Item(iEquipID, iCount) == false)
                return false;

            m_cRepository.Save(m_cProgress);
            return true;
        }

        /// <summary> 갖고 있지 않으면 장착하지 않는다. 소모품도 슬롯 하나를 차지한다. </summary>
        public bool Try_Equip(int iEquipID)
        {
            // 260905_소모품도 슬롯 하나를 차지한다 — 전투에 무엇을 들고 갈지 고르는 것이다.
            CEquipInfo cInfo = Get_EquipInfo(iEquipID);
            if (cInfo == null)
                return false;

            if (m_cProgress.Has_Item(iEquipID) == false)
                return false;

            m_cProgress.Equip(iEquipID, Collect_SlotIDs(cInfo.eSlot));
            m_cRepository.Save(m_cProgress);
            return true;
        }

        // 260905_상점
        /// <summary>
        /// 장비는 한 번만 살 수 있고, 소모품은 여러 번 살 수 있다.
        /// 코인이 모자라면 아무 일도 일어나지 않는다.
        /// </summary>
        public bool Try_Buy(int iEquipID)
        {
            CEquipInfo cInfo = Get_EquipInfo(iEquipID);
            if (cInfo == null || cInfo.iPrice <= 0)
                return false;

            if (cInfo.IS_CONSUMABLE == false && m_cProgress.Has_Item(iEquipID) == true)
                return false;

            if (m_cProgress.Use_Coin(cInfo.iPrice) == false)
                return false;

            m_cProgress.Add_Item(iEquipID, 1);
            m_cRepository.Save(m_cProgress);
            return true;
        }

        /// <summary> 살 수 있는 상태인가 (이미 가졌거나 돈이 모자라면 false). </summary>
        public bool Can_Buy(int iEquipID)
        {
            CEquipInfo cInfo = Get_EquipInfo(iEquipID);
            if (cInfo == null || cInfo.iPrice <= 0)
                return false;

            if (cInfo.IS_CONSUMABLE == false && m_cProgress.Has_Item(iEquipID) == true)
                return false;

            return COIN >= cInfo.iPrice;
        }


        public void Unequip(int iEquipID)
        {
            if (m_cProgress.Is_Equipped(iEquipID) == false)
                return;

            m_cProgress.Unequip(iEquipID);
            m_cRepository.Save(m_cProgress);
        }

        /// <summary> 그 슬롯에 지금 낀 장비. 없으면 null. </summary>
        public CEquipInfo Get_Equipped(EQUIP_SLOT eSlot)
        {
            if (m_cEquipTable == null)
                return null;

            for (int i = 0; i < m_cProgress.lstEquipped.Count; ++i)
            {
                CEquipInfo cInfo = m_cEquipTable.Get_Info(m_cProgress.lstEquipped[i]);
                if (cInfo != null && cInfo.eSlot == eSlot)
                    return cInfo;
            }

            return null;
        }

        // 260905_스킬은 통틀어 하나만 장착한다.
        public void Set_EquippedSkill(int iSkillID)
        {
            if (m_cProgress.iEquippedSkillID == iSkillID)
                return;

            m_cProgress.iEquippedSkillID = iSkillID;
            m_cRepository.Save(m_cProgress);
        }

        /// <summary>
        /// 장착한 장비가 주는 능력치 합. 강화(Get_UpgradeValue)와 더해서 쓴다 —
        /// 둘을 합치는 곳을 한 군데(CGameManager)로 모으기 위해 여기서는 장비만 센다.
        /// </summary>
        public float Get_EquipStat(STAT_TYPE eStat)
        {
            if (m_cEquipTable == null || eStat == STAT_TYPE.NONE)
                return 0f;

            float fSum = 0f;

            for (int i = 0; i < m_cProgress.lstEquipped.Count; ++i)
            {
                CEquipInfo cInfo = m_cEquipTable.Get_Info(m_cProgress.lstEquipped[i]);
                if (cInfo != null && cInfo.eStat == eStat)
                    fSum += cInfo.fStatValue;
            }

            return fSum;
        }

        /// <summary> 강화 + 장비를 합친 최종 수치. 스테이지에 넣을 값은 이것 하나뿐이다. </summary>
        public float Get_TotalStat(CCSVData_UpgradeInfo cUpgradeTable, STAT_TYPE eStat)
        {
            return Get_UpgradeValue(cUpgradeTable, eStat) + Get_EquipStat(eStat);
        }

        private CEquipInfo Get_EquipInfo(int iEquipID)
        {
            return m_cEquipTable != null ? m_cEquipTable.Get_Info(iEquipID) : null;
        }

        // 같은 슬롯의 장비를 벗기려면 그 슬롯에 속한 ID를 전부 알아야 한다.
        private readonly List<CEquipInfo> m_lstSlotBuffer = new List<CEquipInfo>();
        private readonly List<int>        m_lstSlotID     = new List<int>();

        private IReadOnlyList<int> Collect_SlotIDs(EQUIP_SLOT eSlot)
        {
            m_lstSlotID.Clear();

            if (m_cEquipTable == null)
                return m_lstSlotID;

            m_cEquipTable.Collect_BySlot(eSlot, m_lstSlotBuffer);
            for (int i = 0; i < m_lstSlotBuffer.Count; ++i)
                m_lstSlotID.Add(m_lstSlotBuffer[i].iEquipID);

            return m_lstSlotID;
        }
        #endregion 260905_인벤토리 (장비 · 소모품 · 스킬)


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
