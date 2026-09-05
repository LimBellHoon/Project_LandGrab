using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260905_인벤토리 (로비의 가방 탭)
    /// <summary>
    /// 안쪽에 탭이 하나 더 있다 — [장비] / [스킬].
    /// 장비는 슬롯당 하나, 스킬은 통틀어 하나만 장착한다.
    /// 목록은 표를 훑어 만들되 '보유한 것'만 보여 준다 — 상점에서 사야 여기 나타난다.
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

        private CCSVData_EquipInfo  m_cEquipTable;
        private CCSVData_SkillInfo  m_cSkillTable;
        private CProgress_Manager   m_cProgress;
        private Action              m_OnChanged;
        private INVENTORY_TAB       m_eTab;

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

            m_cEquipTable = cDesc.cEquipTable;
            m_cSkillTable = cDesc.cSkillTable;
            m_cProgress   = cDesc.cProgress;
            m_OnChanged   = cDesc.OnChanged;

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

            m_cEquipTable = null;
            m_cSkillTable = null;
            m_cProgress   = null;
            m_OnChanged   = null;

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

            if (m_eTab == INVENTORY_TAB.SKILL)
                Build_SkillList();
            else
                Build_EquipList();
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
                strState = $"{(m_cProgress.Is_Equipped(cInfo.iEquipID) == true ? "[장착 중] " : "")}보유 {m_cProgress.Get_ItemCount(cInfo.iEquipID)}";
            else
                strState = m_cProgress.Is_Equipped(cInfo.iEquipID) == true ? "[장착 중]" : "장착하기";

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

            bool bEquipped = m_cProgress.EQUIPPED_SKILL_ID == cInfo.iSkillID;

            txtLabel.text = $"{cInfo.strName}   {(bEquipped == true ? "[장착 중]" : "장착하기")}"
                          + $"   쿨 {cInfo.fCoolTime:F0}초\n{cInfo.strDesc}";
        }

        private void On_ClickSkill(int iSkillID)
        {
            // 스킬은 하나만 장착한다. 같은 것을 다시 누르면 해제.
            m_cProgress.Set_EquippedSkill(m_cProgress.EQUIPPED_SKILL_ID == iSkillID ? 0 : iSkillID);

            Build_List();
            m_OnChanged?.Invoke();
        }
        #endregion 스킬 탭

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
