using System;

using UnityEngine;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260905_로비 — 하단 탭바로 전투/업그레이드/인벤토리/상점을 오간다
    /// <summary>
    /// 탭바와 재화 표시만 갖고, 탭 안에 무엇을 띄울지는 CGameManager가 정한다.
    /// (화면 흐름을 한곳에서만 갈아탄다는 기존 규칙을 그대로 지킨다)
    ///
    /// 전투 중에는 이 UI를 통째로 닫는다 — 조이스틱이 화면 아래를 쓰기 때문에
    /// 탭바가 남아 있으면 조작과 겹친다.
    /// </summary>
    public class CUI_Lobby : CUI
    {
        // 선택된 탭은 밝게, 나머지는 어둡게.
        private static readonly Color COLOR_TAB_ON  = new Color(0.24f, 0.52f, 0.86f, 1f);
        private static readonly Color COLOR_TAB_OFF = new Color(0.13f, 0.16f, 0.26f, 1f);

        [SerializeField] private Transform  m_trContent;        // 탭 화면이 열릴 자리
        [SerializeField] private Text       m_txtCoin;
        [SerializeField] private Text       m_txtStar;
        [SerializeField] private Button[]   m_arrTabButton;     // LOBBY_TAB 순서와 1:1

        private CProgress_Manager   m_cProgress;
        private Action<LOBBY_TAB>   m_OnTabChanged;
        private LOBBY_TAB           m_eTab;

        /// <summary> 탭 화면을 열 부모. CGameManager가 여기에 Open_UI 한다. </summary>
        public Transform    CONTENT => m_trContent;
        public LOBBY_TAB    TAB     => m_eTab;

        #region Engine.CUI
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CUI_LobbyDesc cDesc) == false)
            {
                Debug.LogError("[CUI_Lobby] CUI_LobbyDesc가 아닙니다.");
                return false;
            }

            if (m_trContent == null || m_arrTabButton == null || m_arrTabButton.Length == 0)
            {
                Debug.LogError("[CUI_Lobby] 프리팹에 Content / 탭 버튼이 연결돼 있지 않습니다. "
                             + "Tools/LandGrab/Setup Assets 를 실행하세요.");
                return false;
            }

            m_cProgress    = cDesc.cProgress;
            m_OnTabChanged = cDesc.OnTabChanged;

            Bind_TabButtons();
            Refresh_Currency();

            // 처음 열릴 때는 전투 탭. 실제로 무엇을 띄울지는 콜백을 받은 쪽이 정한다.
            Select_Tab(cDesc.eStartTab);
            return true;
        }

        public override void Hide()
        {
            for (int i = 0; i < m_arrTabButton.Length; ++i)
            {
                if (m_arrTabButton[i] != null)
                    m_arrTabButton[i].onClick.RemoveAllListeners();
            }

            m_cProgress    = null;
            m_OnTabChanged = null;

            base.Hide();
        }
        #endregion Engine.CUI

        /// <summary> 강화로 코인을 썼을 때처럼 밖에서 값이 바뀌면 불러 준다. </summary>
        public void Refresh_Currency()
        {
            if (m_cProgress == null)
                return;

            if (m_txtCoin != null)
                m_txtCoin.text = $"코인 {m_cProgress.COIN}";

            if (m_txtStar != null)
                m_txtStar.text = $"★ {m_cProgress.TOTAL_STAR}";
        }

        public void Select_Tab(LOBBY_TAB eTab)
        {
            m_eTab = eTab;

            Refresh_TabVisual();
            m_OnTabChanged?.Invoke(eTab);
        }

        #region private
        private void Bind_TabButtons()
        {
            for (int i = 0; i < m_arrTabButton.Length; ++i)
            {
                Button cButton = m_arrTabButton[i];
                if (cButton == null)
                    continue;

                // 클로저가 반복 변수를 잡지 않도록 지역에 복사해 둔다.
                LOBBY_TAB eTab = (LOBBY_TAB)i;

                cButton.onClick.RemoveAllListeners();
                cButton.onClick.AddListener(() => Select_Tab(eTab));
            }
        }

        private void Refresh_TabVisual()
        {
            for (int i = 0; i < m_arrTabButton.Length; ++i)
            {
                if (m_arrTabButton[i] == null)
                    continue;

                Image imgTab = m_arrTabButton[i].GetComponent<Image>();
                if (imgTab != null)
                    imgTab.color = (LOBBY_TAB)i == m_eTab ? COLOR_TAB_ON : COLOR_TAB_OFF;
            }
        }
        #endregion private
    }
}
