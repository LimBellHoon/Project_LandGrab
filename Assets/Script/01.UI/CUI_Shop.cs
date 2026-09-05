using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260905_상점 (로비의 상점 탭)
    /// <summary>
    /// 목록은 EquipInfo.csv를 그대로 훑어 만든다 — 상품이 늘어도 UI 코드는 손대지 않는다.
    /// 장비는 한 번만 살 수 있고, 소모품은 여러 번 살 수 있다.
    /// 버튼 템플릿을 복제해 쓰는 방식은 CUI_StageSelect / CUI_Upgrade와 같다.
    /// </summary>
    public class CUI_Shop : CUI
    {
        [SerializeField] private Transform  m_trContent;
        [SerializeField] private Button     m_btnTemplate;      // 복제 원본 (항상 비활성)
        [SerializeField] private Text       m_txtTitle;

        private readonly List<Button> m_lstButton = new List<Button>();

        private CCSVData_EquipInfo  m_cEquipTable;
        private CProgress_Manager   m_cProgress;
        private Action              m_OnPurchased;

        #region Engine.CUI
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CUI_ShopDesc cDesc) == false)
            {
                Debug.LogError("[CUI_Shop] CUI_ShopDesc가 아닙니다.");
                return false;
            }

            if (m_trContent == null || m_btnTemplate == null)
            {
                Debug.LogError("[CUI_Shop] 프리팹에 Content / 버튼 템플릿이 연결돼 있지 않습니다. "
                             + "Tools/LandGrab/Setup Assets 를 실행하세요.");
                return false;
            }

            m_cEquipTable = cDesc.cEquipTable;
            m_cProgress   = cDesc.cProgress;
            m_OnPurchased = cDesc.OnPurchased;

            m_btnTemplate.gameObject.SetActive(false);
            Build_List();
            return true;
        }

        public override void Hide()
        {
            Clear_List();
            m_cEquipTable = null;
            m_cProgress   = null;
            m_OnPurchased = null;

            base.Hide();
        }
        #endregion Engine.CUI

        private void Build_List()
        {
            Clear_List();

            // 표가 없으면(라벨 누락 등) 빈 화면 대신 이유를 보여 준다.
            if (m_cEquipTable == null || m_cEquipTable.COUNT == 0)
            {
                if (m_txtTitle != null)
                    m_txtTitle.text = "EquipInfo.csv를 읽지 못했습니다";
                return;
            }

            if (m_txtTitle != null)
                m_txtTitle.text = $"상점      코인 {m_cProgress.COIN}";

            IReadOnlyList<CEquipInfo> lstInfo = m_cEquipTable.ALL;

            for (int i = 0; i < lstInfo.Count; ++i)
            {
                CEquipInfo cInfo = lstInfo[i];
                if (cInfo.iPrice <= 0)
                    continue;   // 가격이 없는 것은 상점에 올리지 않는다

                GameObject goButton = Instantiate(m_btnTemplate.gameObject, m_trContent);
                goButton.name = $"Btn_Shop_{cInfo.iEquipID}";
                goButton.SetActive(true);

                Button cButton = goButton.GetComponent<Button>();
                cButton.interactable = m_cProgress.Can_Buy(cInfo.iEquipID);
                Set_Label(goButton, cInfo);

                int iEquipID = cInfo.iEquipID;      // 클로저 대비 지역 복사
                cButton.onClick.AddListener(() => On_ClickBuy(iEquipID));

                m_lstButton.Add(cButton);
            }
        }

        private void Set_Label(GameObject goButton, CEquipInfo cInfo)
        {
            Text txtLabel = goButton.GetComponentInChildren<Text>();
            if (txtLabel == null)
                return;

            // 소모품은 몇 개 갖고 있는지, 장비는 이미 샀는지를 보여 준다.
            string strState;
            if (cInfo.IS_CONSUMABLE == true)
            {
                strState = $"보유 {m_cProgress.Get_ItemCount(cInfo.iEquipID)}";
            }
            else
            {
                strState = m_cProgress.Has_Item(cInfo.iEquipID) == true ? "보유 중" : $"{cInfo.iPrice} 코인";
            }

            txtLabel.text = $"{cInfo.strName}   {strState}\n{cInfo.strDesc}";
        }

        private void On_ClickBuy(int iEquipID)
        {
            if (m_cProgress.Try_Buy(iEquipID) == false)
                return;

            // 코인과 보유 상태가 동시에 바뀌므로 목록을 통째로 다시 그린다.
            Build_List();
            m_OnPurchased?.Invoke();
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
    }
}
