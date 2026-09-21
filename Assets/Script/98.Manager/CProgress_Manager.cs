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
        private bool                m_bFreeSpend;   // 260905_코인 없이 사고 강화한다 (CGameConfig)

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
            bool bChanged = m_cProgress.Migrate_Legacy();

            // 260918_캐릭터 시스템이 생기기 전에 이미 깬 맵의 캐릭터를 챙겨 준다 —
            // 얻는 순간이 '클리어'뿐이라, 예전에 깬 맵은 다시 깨기 전까지 캐릭터가 영영 안 들어왔다.
            bChanged |= Grant_ClearedCharacters();

            if (bChanged == true)
                m_cRepository.Save(m_cProgress);

            return true;
        }

        /// <summary> 디버그용 — 켜면 해금 규칙을 무시하고 전부 열린 것으로 본다. </summary>
        public void Set_UnlockAll(bool bUnlockAll) => m_bUnlockAll = bUnlockAll;

        /// <summary>
        /// 260920_디버그용 — CharacterInfo.csv의 캐릭터를 전부 1레벨로 가진 것으로 만든다.
        /// 캐릭터마다 이동 방식이 다르므로(2-22) 하나씩 깨지 않고 갈아 끼우며 확인하려고 만들었다.
        /// 이미 가진 캐릭터의 레벨은 건드리지 않는다.
        /// </summary>
        /// <returns> 새로 넣어 준 캐릭터 수 </returns>
        public int Grant_AllCharacters(CCSVData_CharacterInfo cTable)
        {
            if (cTable == null)
                return 0;

            int iGranted = 0;
            for (int i = 0; i < cTable.ALL.Count; ++i)
            {
                CCharacterInfo cInfo = cTable.ALL[i];
                if (cInfo == null || m_cProgress.Has_Character(cInfo.iCharacterID) == true)
                    continue;

                m_cProgress.Set_CharacterLevel(cInfo.iCharacterID, 1);
                ++iGranted;
            }

            if (iGranted <= 0)
                return 0;

            if (m_cProgress.iEquippedCharacterID <= 0 && cTable.ALL.Count > 0)
                m_cProgress.iEquippedCharacterID = cTable.ALL[0].iCharacterID;

            m_cRepository.Save(m_cProgress);
            return iGranted;
        }

        /// <summary> 디버그용 — 켜면 코인을 쓰지 않고 구매 / 강화가 된다. </summary>
        public void Set_FreeSpend(bool bFreeSpend) => m_bFreeSpend = bFreeSpend;

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
        #region 계정 레벨 · 하트 · 다이아 (260921, 2-23)
        private CCSVData_AccountLevelInfo m_cAccountTable;
        private bool                      m_bFreeStamina;     // 개발용 — 하트를 쓰지 않고 들어간다(1-6)

        /// <summary> 테스트가 시각을 밀어 넣을 수 있게 한다. null이면 실제 시계를 쓴다. </summary>
        public System.Func<long> fnNow;

        private long NOW => fnNow != null ? fnNow() : System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        public void Set_AccountTable(CCSVData_AccountLevelInfo cTable) => m_cAccountTable = cTable;
        public void Set_FreeStamina(bool bFree) => m_bFreeStamina = bFree;

        public int ACCOUNT_LEVEL => Mathf.Max(1, m_cProgress.iAccountLevel);
        public int ACCOUNT_EXP   => Mathf.Max(0, m_cProgress.iAccountExp);
        public int DIAMOND       => Mathf.Max(0, m_cProgress.iDiamond);

        public CAccountLevelInfo ACCOUNT_INFO => m_cAccountTable?.Get_Info(ACCOUNT_LEVEL);

        /// <summary> 다음 레벨까지 필요한 경험치. 만렙이거나 표가 없으면 0. </summary>
        public int ACCOUNT_NEED_EXP => ACCOUNT_INFO != null ? Mathf.Max(0, ACCOUNT_INFO.iNeedExp) : 0;

        public int MAX_STAMINA => ACCOUNT_INFO != null ? Mathf.Max(1, ACCOUNT_INFO.iMaxStamina) : 30;
        private float STAMINA_REGEN_SEC => ACCOUNT_INFO != null ? ACCOUNT_INFO.fStaminaRegenSec : 300f;

        /// <summary> 지금 하트. 읽을 때마다 지난 시간만큼 채운다(저장은 쓸 때 한다 — 읽기만으로 디스크를 쓰지 않게). </summary>
        public int STAMINA
        {
            get
            {
                Refresh_Stamina(out int iStamina, out long _);
                return iStamina;
            }
        }

        /// <summary> 다음 한 칸까지 남은 초. 가득 찼으면 0. </summary>
        public int STAMINA_REMAIN_SEC
        {
            get
            {
                Refresh_Stamina(out int iStamina, out long lAnchor);
                return CAccount_Utility.Get_StaminaRemainSec(iStamina, MAX_STAMINA, lAnchor, NOW, STAMINA_REGEN_SEC);
            }
        }

        public bool Can_UseStamina(int iCost) => m_bFreeStamina == true || iCost <= 0 || STAMINA >= iCost;

        /// <summary> 스테이지에 들어갈 때 하트를 쓴다. 모자라면 쓰지 않고 false. </summary>
        public bool Try_UseStamina(int iCost)
        {
            if (m_bFreeStamina == true || iCost <= 0)
                return true;

            Refresh_Stamina(out int iStamina, out long lAnchor);
            if (iStamina < iCost)
                return false;

            // 가득 찬 상태에서 쓰면 그 순간부터 회복을 센다(Regen_Stamina가 기준을 지금으로 끌고 왔다)
            m_cProgress.iStamina       = iStamina - iCost;
            m_cProgress.lStaminaAnchor = lAnchor;
            m_cRepository.Save(m_cProgress);
            return true;
        }

        /// <summary>
        /// 260921_하트를 더한다 — 개발용 F2(CGameManager.Tick_DebugKey). **최대치를 넘겨도 된다** —
        /// 넘친 동안은 회복이 멈추고(Regen_Stamina), 쓰는 만큼만 줄어든다. 음수를 넣어도 0 밑으로 내려가지 않는다.
        /// </summary>
        public void Add_Stamina(int iAmount)
        {
            if (iAmount == 0)
                return;

            Refresh_Stamina(out int iStamina, out long lAnchor);
            m_cProgress.iStamina       = Mathf.Max(0, iStamina + iAmount);
            m_cProgress.lStaminaAnchor = lAnchor;
            m_cRepository.Save(m_cProgress);
        }

        private void Refresh_Stamina(out int iStamina, out long lAnchor)
        {
            // 한 번도 안 쓴 저장본(-1)은 가득 찬 것으로 본다
            int iCur = m_cProgress.iStamina < 0 ? MAX_STAMINA : m_cProgress.iStamina;
            long lStart = m_cProgress.lStaminaAnchor > 0 ? m_cProgress.lStaminaAnchor : NOW;

            CAccount_Utility.Regen_Stamina(iCur, MAX_STAMINA, lStart, NOW, STAMINA_REGEN_SEC,
                                           out iStamina, out lAnchor);
        }

        /// <summary> 계정 경험치를 더한다. 레벨이 오르면 하트를 가득 채워 준다(오른 만큼 한 판 더 하라는 보상). </summary>
        /// <returns> 오른 레벨 수 </returns>
        public int Add_AccountExp(int iGain)
        {
            if (iGain <= 0 || m_cAccountTable == null)
                return 0;

            int iLevel = ACCOUNT_LEVEL;
            int iExp   = ACCOUNT_EXP;
            int iUp    = m_cAccountTable.Apply_Exp(ref iLevel, ref iExp, iGain);

            m_cProgress.iAccountLevel = iLevel;
            m_cProgress.iAccountExp   = iExp;

            if (iUp > 0)
            {
                m_cProgress.iStamina       = Mathf.Max(STAMINA, MAX_STAMINA);
                m_cProgress.lStaminaAnchor = NOW;
            }

            m_cRepository.Save(m_cProgress);
            return iUp;
        }

        public void Add_Diamond(int iAmount)
        {
            if (iAmount <= 0)
                return;

            m_cProgress.iDiamond = DIAMOND + iAmount;
            m_cRepository.Save(m_cProgress);
        }

        /// <summary> 계정 레벨이 주는 속도 배율(곱한다). 표가 없으면 1. </summary>
        public float ACCOUNT_SPEED_RATE   => ACCOUNT_INFO != null ? Mathf.Max(0.1f, ACCOUNT_INFO.fSpeedRate) : 1f;
        /// <summary> 계정 레벨이 주는 회피 보너스(더한다). 표가 없으면 0. </summary>
        public float ACCOUNT_EVASION      => ACCOUNT_INFO != null ? Mathf.Max(0f, ACCOUNT_INFO.fEvasionBonus) : 0f;
        #endregion 계정 레벨 · 하트 · 다이아 (260921, 2-23)

        public int COIN => m_cProgress.iCoin;

        public void Add_Coin(int iAmount)
        {
            if (iAmount <= 0)
                return;

            m_cProgress.Add_Coin(iAmount);
            m_cRepository.Save(m_cProgress);
        }

        // 260905_코인을 쓰는 곳은 강화 / 구매 / 스킬 강화 셋뿐이다. 전부 이 함수를 지나게 해서
        // '무료' 스위치 하나로 세 곳이 한꺼번에 열리게 했다 — 곳곳에 플래그를 뿌리면
        // 어느 하나를 빠뜨렸을 때 그 화면만 조용히 막힌다.
        /// <summary> 비용을 치른다. 무료 모드면 코인을 건드리지 않고 통과시킨다. </summary>
        private bool Pay(int iCost)
        {
            if (m_bFreeSpend == true)
                return true;

            return m_cProgress.Use_Coin(iCost);
        }

        /// <summary> 그 비용을 낼 수 있는가. UI가 버튼을 켤지 정할 때 쓴다. </summary>
        public bool Can_Pay(int iCost) => m_bFreeSpend == true || COIN >= iCost;


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

            if (Pay(cInfo.Get_Cost(iLevel)) == false)
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

            if (Pay(cInfo.iPrice) == false)
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

            return Can_Pay(cInfo.iPrice);
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

        // 260912_전투에서 쓸 소모품. 장착한 것이 없으면 갖고 있는 것 중 하나를 대신 고른다.
        //
        // 슬롯은 '여러 개 중 무엇을 들고 갈지' 고르라고 둔 것인데, 하나밖에 없을 때도
        // 장착을 요구하면 사 놓고도 전투에서 버튼이 안 뜬다. 산 물건이 가방에서 잠자는 셈이다.
        // 그래서 슬롯은 그대로 두되, 비어 있으면 갖고 있는 것으로 채워 준다.
        /// <summary> 전투에서 쓸 소모품. 없으면 null. </summary>
        public CEquipInfo Get_BattleConsumable()
        {
            CEquipInfo cEquipped = Get_Equipped(EQUIP_SLOT.CONSUMABLE);
            if (cEquipped != null && Get_ItemCount(cEquipped.iEquipID) > 0)
                return cEquipped;

            if (m_cEquipTable == null)
                return null;

            IReadOnlyList<CEquipInfo> lstAll = m_cEquipTable.ALL;
            for (int i = 0; i < lstAll.Count; ++i)
            {
                CEquipInfo cInfo = lstAll[i];
                if (cInfo.IS_CONSUMABLE == true && Get_ItemCount(cInfo.iEquipID) > 0)
                    return cInfo;
            }

            return null;
        }

        #region 260917_캐릭터 (스킨 + 스탯 배율, 노션 "캐릭터 시스템" 카드 4장)
        public int  EQUIPPED_CHARACTER_ID              => m_cProgress.iEquippedCharacterID;
        public bool Has_Character(int iCharacterID)     => m_cProgress.Has_Character(iCharacterID);
        public int  Get_CharacterLevel(int iCharacterID)    => m_cProgress.Get_CharacterLevel(iCharacterID);
        public int  Get_CharacterFragment(int iCharacterID) => m_cProgress.Get_CharacterFragment(iCharacterID);

        // 260918_각성 스테이지(MapInfo.iCharacterID > 0) 클리어마다 부른다.
        // 처음 클리어면 1레벨로 얻고 곧바로 장착한다. 이미 있으면 곧바로 레벨업하지 않고
        // 조각만 쌓는다 — 레벨업은 가방에서 조각을 모아 버튼을 눌러야 한다(Try_LevelUpCharacter).
        /// <returns> 새로 얻었거나 조각을 받았으면 true. 표에 없으면 false. </returns>
        /// <summary> 260918_이 캐릭터를 주는 맵(MapInfo.csv의 iCharacterID). 없으면 null. </summary>
        public CMapInfo Find_CharacterMap(int iCharacterID)
        {
            if (m_cMapTable == null || iCharacterID <= 0)
                return null;

            for (int i = 0; i < m_cMapTable.ALL.Count; ++i)
            {
                if (m_cMapTable.ALL[i].iCharacterID == iCharacterID)
                    return m_cMapTable.ALL[i];
            }
            return null;
        }

        /// <returns> 하나라도 새로 챙겼으면 true </returns>
        private bool Grant_ClearedCharacters()
        {
            bool bGranted = false;

            for (int i = 0; i < m_cMapTable.ALL.Count; ++i)
            {
                CMapInfo cMap = m_cMapTable.ALL[i];
                if (cMap.iCharacterID <= 0 || m_cProgress.Is_Cleared(cMap.iMapID) == false
                    || m_cProgress.Has_Character(cMap.iCharacterID) == true)
                    continue;

                // 조각은 주지 않는다 — 처음 클리어 보상(1레벨 획득)만 늦게 받는 것이다.
                m_cProgress.Set_CharacterLevel(cMap.iCharacterID, 1);
                if (m_cProgress.iEquippedCharacterID <= 0)
                    m_cProgress.iEquippedCharacterID = cMap.iCharacterID;

                bGranted = true;
            }

            return bGranted;
        }

        public bool On_CharacterMapCleared(CCSVData_CharacterInfo cTable, int iCharacterID)
        {
            CCharacterInfo cInfo = cTable != null ? cTable.Get_Info(iCharacterID) : null;
            if (cInfo == null)
                return false;

            if (m_cProgress.Has_Character(iCharacterID) == false)
            {
                m_cProgress.Set_CharacterLevel(iCharacterID, 1);
                // 260917_처음 얻은 캐릭터는 곧바로 장착한다 — 안 그러면 그 판을 나가도 여전히 이전 캐릭터로 던전에 들어간다.
                m_cProgress.iEquippedCharacterID = iCharacterID;
            }
            else
            {
                m_cProgress.Add_CharacterFragment(iCharacterID, cInfo.iFragmentPerClear);
            }

            m_cRepository.Save(m_cProgress);
            return true;
        }

        /// <summary> 다음 레벨에 드는 조각 수. 표에 없거나 아직 없는 캐릭터거나 만렙이면 0. </summary>
        public int Get_CharacterLevelUpCost(CCSVData_CharacterInfo cTable, int iCharacterID)
        {
            CCharacterInfo cInfo  = cTable != null ? cTable.Get_Info(iCharacterID) : null;
            int            iLevel = m_cProgress.Get_CharacterLevel(iCharacterID);
            if (cInfo == null || iLevel <= 0 || iLevel >= cInfo.iMaxLevel)
                return 0;

            return cInfo.Get_FragmentCost(iLevel);
        }

        /// <summary> 조각이 충분한가. UI가 레벨업 버튼을 켤지 정할 때 쓴다. </summary>
        public bool Can_LevelUpCharacter(CCSVData_CharacterInfo cTable, int iCharacterID)
        {
            int iCost = Get_CharacterLevelUpCost(cTable, iCharacterID);
            return iCost > 0 && m_cProgress.Get_CharacterFragment(iCharacterID) >= iCost;
        }

        /// <summary> 가방의 레벨업 버튼. 조각이 모자라거나 만렙이면 아무 일도 없다. </summary>
        public bool Try_LevelUpCharacter(CCSVData_CharacterInfo cTable, int iCharacterID)
        {
            int iCost = Get_CharacterLevelUpCost(cTable, iCharacterID);
            if (iCost <= 0 || m_cProgress.Use_CharacterFragment(iCharacterID, iCost) == false)
                return false;

            m_cProgress.Set_CharacterLevel(iCharacterID, m_cProgress.Get_CharacterLevel(iCharacterID) + 1);
            m_cRepository.Save(m_cProgress);
            return true;
        }

        /// <summary> 가방에서 캐릭터를 갈아 낀다. 보유하지 않은 캐릭터는 무시한다. </summary>
        public bool Try_EquipCharacter(int iCharacterID)
        {
            if (m_cProgress.Has_Character(iCharacterID) == false)
                return false;

            if (m_cProgress.iEquippedCharacterID == iCharacterID)
                return true;

            m_cProgress.iEquippedCharacterID = iCharacterID;
            m_cRepository.Save(m_cProgress);
            return true;
        }
        #endregion 260917_캐릭터

        public int Get_SkillLevel(SKILL_TYPE eType) => m_cProgress.Get_SkillLevel(eType);

        /// <summary> 스킬 강화. 코인이 모자라거나 만렙이면 아무 일도 없다. </summary>
        public bool Try_UpgradeSkill(CSkillInfo cInfo)
        {
            if (cInfo == null)
                return false;

            int iLevel = Get_SkillLevel(cInfo.eType);
            if (iLevel >= cInfo.iMaxLevel)
                return false;

            if (Pay(cInfo.Get_Cost(iLevel)) == false)
                return false;

            m_cProgress.Set_SkillLevel(cInfo.eType, iLevel + 1);
            m_cRepository.Save(m_cProgress);
            return true;
        }

        // 260905_패시브 스킬은 장착만 해도 능력치를 올린다. 액티브는 여기 끼어들지 않는다.
        /// <summary> 장착한 패시브 스킬이 주는 능력치. </summary>
        public float Get_PassiveStat(CCSVData_SkillInfo cSkillTable, STAT_TYPE eStat)
        {
            if (cSkillTable == null || eStat == STAT_TYPE.NONE || m_cProgress.iEquippedSkillID <= 0)
                return 0f;

            CSkillInfo cInfo = cSkillTable.Get_Info(m_cProgress.iEquippedSkillID);
            if (cInfo == null || cInfo.IS_PASSIVE == false || cInfo.eStat != eStat)
                return 0f;

            return cInfo.Get_StatValue(Get_SkillLevel(cInfo.eType));
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
                    fSum += cInfo.Get_StatValue(m_cProgress.Get_EquipLevel(cInfo.iEquipID));
            }

            return fSum;
        }

        #region 260918_장비 강화 (가방 — CUpgradeInfo.Get_Cost와 같은 형태, 코인을 쓴다)
        public int Get_EquipLevel(int iEquipID) => m_cProgress.Get_EquipLevel(iEquipID);

        /// <summary> 다음 레벨에 드는 코인. 소모품이거나 표에 없거나 만렙이면 0. </summary>
        public int Get_EquipUpgradeCost(int iEquipID)
        {
            CEquipInfo cInfo = Get_EquipInfo(iEquipID);
            if (cInfo == null || cInfo.IS_CONSUMABLE == true)
                return 0;

            return cInfo.Get_Cost(m_cProgress.Get_EquipLevel(iEquipID));
        }

        /// <summary> 강화 버튼을 켤지 정할 때 쓴다. </summary>
        public bool Can_UpgradeEquip(int iEquipID)
        {
            if (m_cProgress.Has_Item(iEquipID) == false)
                return false;

            int iCost = Get_EquipUpgradeCost(iEquipID);
            return iCost > 0 && Can_Pay(iCost);
        }

        // 260918_장비 뽑기 (가방의 BM 자리). 코인을 내고 EquipInfo.iGachaWeight로 하나를 얻는다.
        // 이미 가진 장비가 나오면 버리지 않는다 — 만렙 전이면 강화 1레벨, 만렙이면 코인을 일부 돌려준다.
        /// <param name="cEquip"> 뽑힌 장비. 실패하면 null </param>
        /// <param name="iRefund"> 돌려받은 코인 (REFUND일 때만) </param>
        public GACHA_RESULT Try_Gacha(CGachaInfo cGacha, out CEquipInfo cEquip, out int iRefund)
        {
            cEquip  = null;
            iRefund = 0;

            if (cGacha == null || m_cEquipTable == null)
                return GACHA_RESULT.FAIL;

            List<CEquipInfo> lstPick = CWeightedPick_Utility.Pick(m_cEquipTable.ALL, cInfo => cInfo.iGachaWeight, 1);
            if (lstPick.Count == 0 || Pay(cGacha.iCost) == false)
                return GACHA_RESULT.FAIL;       // 뽑을 것이 없으면 돈을 받지 않는다

            cEquip = lstPick[0];
            GACHA_RESULT eResult;

            if (m_cProgress.Has_Item(cEquip.iEquipID) == false || cEquip.IS_CONSUMABLE == true)
            {
                m_cProgress.Add_Item(cEquip.iEquipID, 1);
                eResult = GACHA_RESULT.NEW;
            }
            else if (m_cProgress.Get_EquipLevel(cEquip.iEquipID) < cEquip.iMaxLevel)
            {
                m_cProgress.Set_EquipLevel(cEquip.iEquipID, m_cProgress.Get_EquipLevel(cEquip.iEquipID) + 1);
                eResult = GACHA_RESULT.LEVEL_UP;
            }
            else
            {
                iRefund = cGacha.REFUND;
                m_cProgress.Add_Coin(iRefund);
                eResult = GACHA_RESULT.REFUND;
            }

            m_cRepository.Save(m_cProgress);
            return eResult;
        }

        /// <summary> 코인이 모자라거나 소모품이거나 만렙이면 아무 일도 없다. </summary>
        public bool Try_UpgradeEquip(int iEquipID)
        {
            CEquipInfo cInfo = Get_EquipInfo(iEquipID);
            if (cInfo == null || cInfo.IS_CONSUMABLE == true || m_cProgress.Has_Item(iEquipID) == false)
                return false;

            int iLevel = m_cProgress.Get_EquipLevel(iEquipID);
            int iCost  = cInfo.Get_Cost(iLevel);
            if (iCost <= 0 || Pay(iCost) == false)
                return false;

            m_cProgress.Set_EquipLevel(iEquipID, iLevel + 1);
            m_cRepository.Save(m_cProgress);
            return true;
        }
        #endregion 260918_장비 강화

        /// <summary> 강화 + 장비 + 패시브 스킬을 합친 최종 수치. 스테이지에 넣을 값은 이것 하나뿐이다. </summary>
        public float Get_TotalStat(CCSVData_UpgradeInfo cUpgradeTable, STAT_TYPE eStat,
                                   CCSVData_SkillInfo cSkillTable = null)
        {
            return Get_UpgradeValue(cUpgradeTable, eStat) + Get_EquipStat(eStat)
                 + Get_PassiveStat(cSkillTable, eStat);
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
