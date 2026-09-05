using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260905_능력치 강화 화면 (로비의 업그레이드 탭)
    /// <summary>
    /// 목록은 UpgradeInfo.csv를 그대로 훑어 만든다 — 항목이 늘어도 UI 코드는 손대지 않는다.
    /// 버튼은 프리팹의 비활성 템플릿을 복제해 쓴다(CUI_StageSelect와 같은 방식).
    /// </summary>
    public class CUI_Upgrade : CUI
    {
        [SerializeField] private Transform  m_trContent;
        [SerializeField] private Button     m_btnTemplate;      // 복제 원본 (항상 비활성)
        [SerializeField] private Text       m_txtTitle;

        private readonly List<Button> m_lstButton = new List<Button>();

        private CCSVData_UpgradeInfo    m_cTable;
        private CProgress_Manager       m_cProgress;
        private Action                  m_OnPurchased;

        #region Engine.CUI
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CUI_UpgradeDesc cDesc) == false)
            {
                Debug.LogError("[CUI_Upgrade] CUI_UpgradeDesc가 아닙니다.");
                return false;
            }

            if (m_trContent == null || m_btnTemplate == null)
            {
                Debug.LogError("[CUI_Upgrade] 프리팹에 Content / 버튼 템플릿이 연결돼 있지 않습니다. "
                             + "Tools/LandGrab/Setup Assets 를 실행하세요.");
                return false;
            }

            m_cTable      = cDesc.cUpgradeTable;
            m_cProgress   = cDesc.cProgress;
            m_OnPurchased = cDesc.OnPurchased;

            m_btnTemplate.gameObject.SetActive(false);
            Build_List();
            return true;
        }

        public override void Hide()
        {
            Clear_List();
            m_cTable      = null;
            m_cProgress   = null;
            m_OnPurchased = null;

            base.Hide();
        }
        #endregion Engine.CUI

        private void Build_List()
        {
            Clear_List();

            if (m_txtTitle != null)
                m_txtTitle.text = $"능력치 강화      코인 {m_cProgress.COIN}";

            // 표가 없으면(라벨 누락 등) 빈 화면 대신 이유를 보여 준다.
            if (m_cTable == null || m_cTable.COUNT == 0)
            {
                if (m_txtTitle != null)
                    m_txtTitle.text = "UpgradeInfo.csv를 읽지 못했습니다";
                return;
            }

            IReadOnlyList<CUpgradeInfo> lstInfo = m_cTable.ALL;

            for (int i = 0; i < lstInfo.Count; ++i)
            {
                CUpgradeInfo cInfo = lstInfo[i];

                GameObject goButton = Instantiate(m_btnTemplate.gameObject, m_trContent);
                goButton.name = $"Btn_Upgrade_{cInfo.eType}";
                goButton.SetActive(true);

                Button cButton = goButton.GetComponent<Button>();

                bool bMax      = m_cProgress.Is_UpgradeMax(m_cTable, cInfo.eType);
                int  iCost     = m_cProgress.Get_UpgradeCost(m_cTable, cInfo.eType);
                bool bAfford   = bMax == false && m_cProgress.COIN >= iCost;

                cButton.interactable = bAfford;
                Set_Label(goButton, cInfo, bMax, iCost);

                UPGRADE_TYPE eType = cInfo.eType;   // 클로저 대비 지역 복사
                cButton.onClick.AddListener(() => On_ClickUpgrade(eType));

                m_lstButton.Add(cButton);
            }
        }

        private void Set_Label(GameObject goButton, CUpgradeInfo cInfo, bool bMax, int iCost)
        {
            Text txtLabel = goButton.GetComponentInChildren<Text>();
            if (txtLabel == null)
                return;

            int iLevel = m_cProgress.Get_UpgradeLevel(cInfo.eType);

            txtLabel.text = bMax == true
                          ? $"{cInfo.strName}   Lv.{iLevel}  (MAX)\n{cInfo.strDesc}"
                          : $"{cInfo.strName}   Lv.{iLevel}  →  Lv.{iLevel + 1}   {iCost} 코인\n{cInfo.strDesc}";
        }

        private void On_ClickUpgrade(UPGRADE_TYPE eType)
        {
            if (m_cProgress.Try_Upgrade(m_cTable, eType) == false)
                return;

            // 코인과 레벨이 동시에 바뀌므로 목록을 통째로 다시 그린다 (항목이 몇 개뿐이라 부담이 없다).
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
