using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260912_카드 3지선다 — 점령률을 넘길 때마다 한 번
    /// <summary>
    /// 셋 중 하나를 고르면 그 판이 끝날 때까지 효과가 유지된다.
    ///
    /// 무엇을 뽑을지는 표(`CCSVData_CardInfo.Pick_Random`)가, 무엇을 거는지는 스테이지가 한다.
    /// 여기는 보여 주고 고른 결과를 돌려주기만 한다 — 그래야 카드가 늘어도 이 클래스는 그대로다.
    ///
    /// 고르기 전에는 닫히지 않는다. 뒤를 눌러 넘길 수 있으면 '고르는 순간'이 사라진다.
    /// </summary>
    public class CUI_CardPick : CUI
    {
        [SerializeField] private Text           m_txtTitle;
        [SerializeField] private Button         m_btnTemplate;      // 비활성 템플릿. 복제해서 쓴다
        [SerializeField] private RectTransform  m_trContent;

        private readonly List<GameObject> m_lstSpawned = new List<GameObject>();

        private Action<CCardInfo> m_OnPick;

        #region Engine.CUI
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CUI_CardPickDesc cDesc) == false)
            {
                Debug.LogError("[CUI_CardPick] CUI_CardPickDesc가 아닙니다.");
                return false;
            }

            m_OnPick = cDesc.OnPick;

            if (m_txtTitle != null)
                m_txtTitle.text = cDesc.strTitle;

            Build_Cards(cDesc.lstCard);
            return true;
        }

        public override void Hide()
        {
            Clear_Cards();
            m_OnPick = null;
            base.Hide();
        }
        #endregion Engine.CUI

        private void Build_Cards(IReadOnlyList<CCardInfo> lstCard)
        {
            Clear_Cards();

            if (m_btnTemplate == null || m_trContent == null || lstCard == null)
                return;

            for (int i = 0; i < lstCard.Count; ++i)
            {
                CCardInfo cInfo = lstCard[i];
                if (cInfo == null)
                    continue;

                GameObject goCard = Instantiate(m_btnTemplate.gameObject, m_trContent);
                goCard.SetActive(true);
                m_lstSpawned.Add(goCard);

                Text[] arrText = goCard.GetComponentsInChildren<Text>(true);
                if (arrText.Length > 0)
                    arrText[0].text = cInfo.strName;
                if (arrText.Length > 1)
                    arrText[1].text = cInfo.strDesc;

                Button cButton = goCard.GetComponent<Button>();
                if (cButton == null)
                    continue;

                // 260912_루프 변수를 그대로 넘기면 마지막 카드만 잡힌다. 지역 변수로 묶어 둔다.
                CCardInfo cPicked = cInfo;
                cButton.onClick.RemoveAllListeners();
                cButton.onClick.AddListener(() => On_Click(cPicked));
            }
        }

        private void On_Click(CCardInfo cInfo)
        {
            // 두 번 눌러 두 장을 먹는 일을 막는다 — 누른 순간 더는 못 누르게 한다.
            for (int i = 0; i < m_lstSpawned.Count; ++i)
            {
                Button cButton = m_lstSpawned[i] != null ? m_lstSpawned[i].GetComponent<Button>() : null;
                if (cButton != null)
                    cButton.interactable = false;
            }

            m_OnPick?.Invoke(cInfo);
        }

        private void Clear_Cards()
        {
            for (int i = 0; i < m_lstSpawned.Count; ++i)
            {
                if (m_lstSpawned[i] != null)
                    Destroy(m_lstSpawned[i]);
            }

            m_lstSpawned.Clear();
        }
    }
}
