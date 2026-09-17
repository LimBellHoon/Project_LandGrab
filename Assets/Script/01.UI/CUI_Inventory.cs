using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260905_인벤토리 (로비의 가방 탭)
    // 260918_안쪽 탭에 [캐릭터]를 추가했다(INVENTORY_TAB). 카드 갤러리는 장착 개념이 없어
    // 로비의 별도 탭(LOBBY_TAB.CARD, CUI_Card)으로 뺐다.
    /// <summary>
    /// 안쪽에 탭이 셋 있다 — [장비] / [스킬] / [캐릭터].
    /// 장비는 슬롯당 하나, 스킬은 통틀어 하나, 캐릭터도 스테이지 진입용으로 하나만 장착한다.
    /// 목록은 표를 훑어 만들되 '보유한 것'만 보여 준다 — 얻어야(구매/클리어) 여기 나타난다.
    /// </summary>
    public class CUI_Inventory : CUI
    {
        private static readonly Color COLOR_TAB_ON  = new Color(0.24f, 0.52f, 0.86f, 1f);
        private static readonly Color COLOR_TAB_OFF = new Color(0.13f, 0.16f, 0.26f, 1f);

        [SerializeField] private Transform  m_trContent;
        [SerializeField] private Button     m_btnTemplate;      // 복제 원본 (항상 비활성)
        [SerializeField] private Text       m_txtTitle;
        [SerializeField] private Button[]   m_arrTabButton;     // INVENTORY_TAB 순서와 1:1

        private readonly List<Button> m_lstButton = new List<Button>();

        private CCSVData_EquipInfo     m_cEquipTable;
        private CCSVData_SkillInfo     m_cSkillTable;
        // 260918_캐릭터(스킨/레벨업) 탭
        private CCSVData_CharacterInfo m_cCharacterTable;
        private CProgress_Manager      m_cProgress;
        private Action                 m_OnChanged;
        private INVENTORY_TAB          m_eTab;

        #region Engine.CUI
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CUI_InventoryDesc cDesc) == false)
            {
                Debug.LogError("[CUI_Inventory] CUI_InventoryDesc가 아닙니다.");
                return false;
            }

            if (m_trContent == null || m_btnTemplate == null
                || m_arrTabButton == null || m_arrTabButton.Length == 0)
            {
                Debug.LogError("[CUI_Inventory] 프리팹에 Content / 템플릿 / 탭 버튼이 연결돼 있지 않습니다. "
                             + "Tools/LandGrab/Setup Assets 를 실행하세요.");
                return false;
            }

            m_cEquipTable     = cDesc.cEquipTable;
            m_cSkillTable     = cDesc.cSkillTable;
            m_cCharacterTable = cDesc.cCharacterTable;
            m_cProgress       = cDesc.cProgress;
            m_OnChanged       = cDesc.OnChanged;

            m_btnTemplate.gameObject.SetActive(false);
            Bind_TabButtons();
            Select_Tab(INVENTORY_TAB.EQUIP);
            return true;
        }

        public override void Hide()
        {
            Clear_List();

            for (int i = 0; i < m_arrTabButton.Length; ++i)
            {
                if (m_arrTabButton[i] != null)
                    m_arrTabButton[i].onClick.RemoveAllListeners();
            }

            m_cEquipTable     = null;
            m_cSkillTable     = null;
            m_cCharacterTable = null;
            m_cProgress       = null;
            m_OnChanged       = null;

            base.Hide();
        }
        #endregion Engine.CUI

        private void Bind_TabButtons()
        {
            for (int i = 0; i < m_arrTabButton.Length; ++i)
            {
                Button cButton = m_arrTabButton[i];
                if (cButton == null)
                    continue;

                // 클로저가 반복 변수를 잡지 않도록 지역에 복사해 둔다.
                INVENTORY_TAB eTab = (INVENTORY_TAB)i;

                cButton.onClick.RemoveAllListeners();
                cButton.onClick.AddListener(() => Select_Tab(eTab));
            }
        }

        private void Select_Tab(INVENTORY_TAB eTab)
        {
            m_eTab = eTab;

            for (int i = 0; i < m_arrTabButton.Length; ++i)
            {
                if (m_arrTabButton[i] == null)
                    continue;

                Image imgTab = m_arrTabButton[i].GetComponent<Image>();
                if (imgTab != null)
                    imgTab.color = (INVENTORY_TAB)i == m_eTab ? COLOR_TAB_ON : COLOR_TAB_OFF;
            }

            Build_List();
        }

        private void Build_List()
        {
            Clear_List();

            switch (m_eTab)
            {
                case INVENTORY_TAB.SKILL:     Build_SkillList();     break;
                case INVENTORY_TAB.CHARACTER: Build_CharacterList(); break;
                default:                      Build_EquipList();     break;
            }
        }

        #region 장비 탭
        private void Build_EquipList()
        {
            if (m_cEquipTable == null)
            {
                Set_Title("EquipInfo.csv를 읽지 못했습니다");
                return;
            }

            IReadOnlyList<CEquipInfo> lstInfo = m_cEquipTable.ALL;
            int iOwned = 0;

            for (int i = 0; i < lstInfo.Count; ++i)
            {
                CEquipInfo cInfo = lstInfo[i];
                if (m_cProgress.Has_Item(cInfo.iEquipID) == false)
                    continue;       // 보유한 것만 보여 준다

                ++iOwned;

                Button cButton = Make_Row($"Btn_Equip_{cInfo.iEquipID}");
                Set_EquipLabel(cButton.gameObject, cInfo);

                // 260905_소모품도 슬롯에 넣는다 — 전투에 무엇을 들고 갈지 고르는 것이다.
                cButton.interactable = true;

                int iEquipID = cInfo.iEquipID;      // 클로저 대비 지역 복사
                cButton.onClick.AddListener(() => On_ClickEquip(iEquipID));

                // 260918_소모품은 강화 대상이 아니다(EquipInfo.iMaxLevel 0) — 버튼 자체를 안 만든다.
                if (cInfo.IS_CONSUMABLE == false)
                    Add_UpgradeButton(cButton.gameObject, iEquipID);
            }

            Set_Title(iOwned > 0 ? "가방 — 장비" : "가방 — 장비   (상점에서 먼저 구매하세요)");
        }

        private void Set_EquipLabel(GameObject goButton, CEquipInfo cInfo)
        {
            Text txtLabel = goButton.GetComponentInChildren<Text>();
            if (txtLabel == null)
                return;

            string strState;
            if (cInfo.IS_CONSUMABLE == true)
            {
                strState = $"{(m_cProgress.Is_Equipped(cInfo.iEquipID) == true ? "[장착 중] " : "")}보유 {m_cProgress.Get_ItemCount(cInfo.iEquipID)}";
            }
            else
            {
                string strEquip = m_cProgress.Is_Equipped(cInfo.iEquipID) == true ? "[장착 중]" : "장착하기";
                strState = $"{strEquip}   Lv.{m_cProgress.Get_EquipLevel(cInfo.iEquipID)}/{cInfo.iMaxLevel}";
            }

            txtLabel.text = $"{cInfo.strName}   {strState}\n{cInfo.strDesc}";
        }

        private void On_ClickEquip(int iEquipID)
        {
            // 이미 낀 것을 다시 누르면 벗는다 — 버튼 하나로 토글한다.
            if (m_cProgress.Is_Equipped(iEquipID) == true)
                m_cProgress.Unequip(iEquipID);
            else if (m_cProgress.Try_Equip(iEquipID) == false)
                return;

            Build_List();
            m_OnChanged?.Invoke();
        }

        // 260918_장비 강화 — 행 전체를 누르면 장착/해제이므로, 강화는 오른쪽에 작은 버튼을 하나 더 붙인다.
        // 템플릿에 그 자리가 없어 이 탭에서만 런타임에 복제해 붙인다 — 프리팹을 새로 만들지 않기 위해서다.
        // 부모 행이 Clear_List에서 파괴될 때 같이 사라지므로 별도 목록에 담지 않는다.
        private void Add_UpgradeButton(GameObject goRow, int iEquipID)
        {
            GameObject goUpgrade = Instantiate(m_btnTemplate.gameObject, goRow.transform);
            goUpgrade.name = "Btn_Upgrade";
            goUpgrade.SetActive(true);

            RectTransform trUpgrade = goUpgrade.GetComponent<RectTransform>();
            trUpgrade.anchorMin        = new Vector2(1f, 0.5f);
            trUpgrade.anchorMax        = new Vector2(1f, 0.5f);
            trUpgrade.pivot            = new Vector2(1f, 0.5f);
            trUpgrade.anchoredPosition = new Vector2(-8f, 0f);
            trUpgrade.sizeDelta        = new Vector2(160f, 64f);

            Text txtLabel = goUpgrade.GetComponentInChildren<Text>();
            if (txtLabel != null)
            {
                int iCost = m_cProgress.Get_EquipUpgradeCost(iEquipID);
                txtLabel.text = iCost > 0 ? $"강화\n{iCost}" : "MAX";
            }

            Button cButton = goUpgrade.GetComponent<Button>();
            cButton.onClick.RemoveAllListeners();
            cButton.onClick.AddListener(() => On_ClickUpgradeEquip(iEquipID));
        }

        // 260918_코인이 모자라거나 만렙이면 Try_UpgradeEquip이 조용히 실패한다 —
        // On_ClickSkill/On_ClickCharacter처럼 버튼을 따로 잠그지 않고 시도-실패로 막는다.
        private void On_ClickUpgradeEquip(int iEquipID)
        {
            if (m_cProgress.Try_UpgradeEquip(iEquipID) == false)
                return;

            Build_List();
            m_OnChanged?.Invoke();
        }
        #endregion 장비 탭

        #region 스킬 탭
        private void Build_SkillList()
        {
            if (m_cSkillTable == null)
            {
                Set_Title("SkillInfo.csv를 읽지 못했습니다");
                return;
            }

            Set_Title("가방 — 스킬   (하나만 장착)");

            IReadOnlyList<CSkillInfo> lstInfo = m_cSkillTable.ALL;

            for (int i = 0; i < lstInfo.Count; ++i)
            {
                CSkillInfo cInfo = lstInfo[i];

                Button cButton = Make_Row($"Btn_Skill_{cInfo.iSkillID}");
                Set_SkillLabel(cButton.gameObject, cInfo);

                int iSkillID = cInfo.iSkillID;      // 클로저 대비 지역 복사
                cButton.onClick.AddListener(() => On_ClickSkill(iSkillID));
            }
        }

        private void Set_SkillLabel(GameObject goButton, CSkillInfo cInfo)
        {
            Text txtLabel = goButton.GetComponentInChildren<Text>();
            if (txtLabel == null)
                return;

            int  iLevel    = m_cProgress.Get_SkillLevel(cInfo.eType);
            int  iCost     = cInfo.Get_Cost(iLevel);
            bool bMax      = iLevel >= cInfo.iMaxLevel;

            bool bEquipped = m_cProgress.EQUIPPED_SKILL_ID == cInfo.iSkillID;

            // 260905_한 줄에 장착 상태와 강화 정보를 함께 보여 준다.
            // 분류(액티브/패시브)를 적어 줘야 인게임 버튼이 왜 안 뜨는지 헷갈리지 않는다.
            string strKind  = cInfo.IS_PASSIVE == true ? "패시브" : "액티브";
            string strLevel = bMax == true ? $"Lv.{iLevel} (MAX)" : $"Lv.{iLevel} → {iLevel + 1}  {iCost} 코인";

            txtLabel.text = $"{cInfo.strName}   {(bEquipped == true ? "[장착 중]" : "장착하기")}   {strKind}\n"
                          + $"{strLevel}   {cInfo.strDesc}";
        }

        // 260905_장착되지 않은 스킬을 누르면 장착, 이미 장착한 스킬을 누르면 강화한다.
        // 버튼이 하나뿐이라 상태에 따라 뜻을 바꿨다 — 라벨에 다음 동작이 그대로 적혀 있다.
        private void On_ClickSkill(int iSkillID)
        {
            if (m_cProgress.EQUIPPED_SKILL_ID != iSkillID)
            {
                m_cProgress.Set_EquippedSkill(iSkillID);
            }
            else if (m_cProgress.Try_UpgradeSkill(m_cSkillTable.Get_Info(iSkillID)) == false)
            {
                return;     // 코인이 모자라거나 만렙
            }

            Build_List();
            m_OnChanged?.Invoke();
        }
        #endregion 스킬 탭

        #region 캐릭터 탭 (260918_스킨/레벨업 — 스테이지 진입 캐릭터를 여기서 바꾼다)
        private void Build_CharacterList()
        {
            if (m_cCharacterTable == null)
            {
                Set_Title("CharacterInfo.csv를 읽지 못했습니다");
                return;
            }

            IReadOnlyList<CCharacterInfo> lstInfo = m_cCharacterTable.ALL;

            // 260918_못 가진 캐릭터도 목록에 보인다 — 무엇을 모을 수 있는지 보여야 모으고 싶어진다.
            // 못 가진 줄은 어디서 얻는지만 적고 눌리지 않는다.
            for (int i = 0; i < lstInfo.Count; ++i)
            {
                CCharacterInfo cInfo = lstInfo[i];
                bool bOwned = m_cProgress.Has_Character(cInfo.iCharacterID);

                Button cButton = Make_Row($"Btn_Character_{cInfo.iCharacterID}");
                Set_CharacterLabel(cButton.gameObject, cInfo);
                cButton.interactable = bOwned;

                if (bOwned == false)
                    continue;

                int iCharacterID = cInfo.iCharacterID;      // 클로저 대비 지역 복사
                cButton.onClick.AddListener(() => On_ClickCharacter(iCharacterID));
            }

            Set_Title("가방 — 캐릭터   (눌러서 장착 / 장착 중이면 레벨업)");
        }

        // 260918_이 캐릭터를 주는 맵 — MapInfo.csv의 iCharacterID로 찾는다. 없으면 아직 얻을 곳이 없다.
        private string Get_CharacterSource(int iCharacterID)
        {
            CMapInfo cMap = m_cProgress.Find_CharacterMap(iCharacterID);
            return cMap != null ? $"{cMap.strMapName} 클리어 시 획득" : "획득처 준비 중";
        }

        private void Set_CharacterLabel(GameObject goButton, CCharacterInfo cInfo)
        {
            Text txtLabel = goButton.GetComponentInChildren<Text>();
            if (txtLabel == null)
                return;

            // 260918_못 가진 캐릭터 — 이름 · 설명과 어디서 얻는지만 보여 준다
            if (m_cProgress.Has_Character(cInfo.iCharacterID) == false)
            {
                txtLabel.text = $"{cInfo.strName}   [미보유] {Get_CharacterSource(cInfo.iCharacterID)}\n{cInfo.strDesc}";
                return;
            }

            int  iLevel    = m_cProgress.Get_CharacterLevel(cInfo.iCharacterID);
            bool bEquipped = m_cProgress.EQUIPPED_CHARACTER_ID == cInfo.iCharacterID;
            bool bMax      = iLevel >= cInfo.iMaxLevel;

            string strState;
            if (bEquipped == false)
                strState = "장착하기";
            else if (bMax == true)
                strState = "[장착 중] MAX";
            else
                strState = $"[장착 중] 레벨업 {m_cProgress.Get_CharacterFragment(cInfo.iCharacterID)}"
                          + $"/{m_cProgress.Get_CharacterLevelUpCost(m_cCharacterTable, cInfo.iCharacterID)} 조각";

            // 260918_성급은 아직 틀뿐이다 — 화면에 몇 성인지만 보여 주고 실제 효과는 걸지 않는다(CLAUDE.md 2-17).
            int    iTier    = cInfo.Get_StarTier(iLevel);
            string strStar  = new string('★', iTier)
                            + new string('☆', Mathf.Max(0, cInfo.lstStarLevel.Count - iTier));

            txtLabel.text = $"{cInfo.strName}   {strState}\n"
                          + $"Lv.{iLevel}/{cInfo.iMaxLevel}   {strStar} (준비 중)\n{cInfo.strDesc}";
        }

        // 260918_장착되지 않은 캐릭터를 누르면 장착, 이미 장착한 캐릭터를 누르면 조각으로 레벨업한다 —
        // On_ClickSkill과 같은 '버튼 하나가 상태에 따라 뜻을 바꾸는' 패턴이다.
        private void On_ClickCharacter(int iCharacterID)
        {
            if (m_cProgress.EQUIPPED_CHARACTER_ID != iCharacterID)
            {
                m_cProgress.Try_EquipCharacter(iCharacterID);
            }
            else if (m_cProgress.Try_LevelUpCharacter(m_cCharacterTable, iCharacterID) == false)
            {
                return;     // 조각이 모자라거나 만렙
            }

            Build_List();
            m_OnChanged?.Invoke();
        }
        #endregion 캐릭터 탭

        #region 공용
        private Button Make_Row(string strName)
        {
            GameObject goButton = Instantiate(m_btnTemplate.gameObject, m_trContent);
            goButton.name = strName;
            goButton.SetActive(true);

            Button cButton = goButton.GetComponent<Button>();
            m_lstButton.Add(cButton);
            return cButton;
        }

        private void Set_Title(string strTitle)
        {
            if (m_txtTitle != null)
                m_txtTitle.text = strTitle;
        }

        private void Clear_List()
        {
            for (int i = 0; i < m_lstButton.Count; ++i)
            {
                if (m_lstButton[i] == null)
                    continue;

                // 리스너를 끊지 않으면 파괴된 뒤에도 콜백이 남는다.
                m_lstButton[i].onClick.RemoveAllListeners();
                Destroy(m_lstButton[i].gameObject);
            }

            m_lstButton.Clear();
        }
        #endregion 공용
    }
}
