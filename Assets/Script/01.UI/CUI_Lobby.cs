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
        [SerializeField] private Button[]   m_arrTabButton;     // LOBBY_TAB 순서와 1:1

        // 260921_상단 바를 레퍼런스 로비처럼 다시 짰다(2-23)
        //   D 왼쪽 — 장착 캐릭터 그림 · 이름 · 계정 레벨 · 경험치 막대
        //   E 오른쪽 — 하트(+회복 시계) · 코인 · 다이아
        [SerializeField] private RawImage   m_imgProfile;
        [SerializeField] private Text       m_txtProfileName;
        [SerializeField] private Text       m_txtLevel;
        [SerializeField] private Image      m_imgExpFill;       // Filled — 스프라이트가 있어야 fillAmount가 먹는다(Tex_White)
        [SerializeField] private Text       m_txtExp;
        [SerializeField] private Text       m_txtStamina;
        [SerializeField] private Text       m_txtStaminaTimer;
        [SerializeField] private Text       m_txtCoin;
        [SerializeField] private Text       m_txtDiamond;

        private CProgress_Manager       m_cProgress;
        private CCSVData_CharacterInfo  m_cCharacterTable;
        private float                   m_fTimerTick;       // 하트 시계를 1초마다만 다시 쓴다
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

            m_cProgress       = cDesc.cProgress;
            m_cCharacterTable = cDesc.cCharacterTable;
            m_OnTabChanged    = cDesc.OnTabChanged;

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

            m_cProgress       = null;
            m_cCharacterTable = null;
            m_OnTabChanged    = null;

            base.Hide();
        }
        #endregion Engine.CUI

        // 260921_하트는 시간이 지나면 저절로 찬다 — 로비에 있는 동안 시계가 흘러가야 한다.
        // Engine 레이어가 UI를 Tick하는지 확실하지 않아 CUI_InGame처럼 Unity Update를 쓴다(2-7).
        private void Update()
        {
            if (m_cProgress == null)
                return;

            m_fTimerTick -= Time.unscaledDeltaTime;
            if (m_fTimerTick > 0f)
                return;

            m_fTimerTick = 1f;
            Refresh_Stamina();
        }

        /// <summary> 강화로 코인을 썼을 때처럼 밖에서 값이 바뀌면 불러 준다. 상단 바 전체를 다시 칠한다. </summary>
        public void Refresh_Currency()
        {
            if (m_cProgress == null)
                return;

            Set_Text(m_txtCoin,    $"{m_cProgress.COIN:N0}");
            Set_Text(m_txtDiamond, $"{m_cProgress.DIAMOND:N0}");
            Refresh_Stamina();
            Refresh_Profile();
        }

        private void Refresh_Stamina()
        {
            Set_Text(m_txtStamina, $"{m_cProgress.STAMINA}/{m_cProgress.MAX_STAMINA}");

            // 가득 찼으면 시계를 지운다 — '0초 남음'이 계속 떠 있으면 무언가 기다려야 하는 줄 안다
            int iRemain = m_cProgress.STAMINA_REMAIN_SEC;
            Set_Text(m_txtStaminaTimer, iRemain > 0 ? $"회복 {CAccount_Utility.Format_Timer(iRemain)}" : "가득");
        }

        // D — 장착 캐릭터 그림(첫 카드) · 이름 · 계정 레벨 · 경험치
        private void Refresh_Profile()
        {
            int iCharacterID = m_cProgress.EQUIPPED_CHARACTER_ID;
            CCharacterInfo cCharacter = iCharacterID > 0 && m_cCharacterTable != null
                                      ? m_cCharacterTable.Get_Info(iCharacterID) : null;

            Set_Text(m_txtProfileName, cCharacter != null ? cCharacter.strName : "캐릭터 없음");

            if (m_imgProfile != null)
            {
                string strTex = cCharacter != null ? cCharacter.Get_CardTex(0) : string.Empty;
                m_imgProfile.texture = string.IsNullOrEmpty(strTex) == false
                                     ? CGameInstance.Instance.Get_Texture(strTex) : null;
            }

            int iNeed = m_cProgress.ACCOUNT_NEED_EXP;
            Set_Text(m_txtLevel, $"Lv.{m_cProgress.ACCOUNT_LEVEL}");
            Set_Text(m_txtExp,   iNeed > 0 ? $"{m_cProgress.ACCOUNT_EXP}/{iNeed}" : "MAX");

            if (m_imgExpFill != null)
                m_imgExpFill.fillAmount = iNeed > 0 ? Mathf.Clamp01((float)m_cProgress.ACCOUNT_EXP / iNeed) : 1f;
        }

        private static void Set_Text(Text cText, string strValue)
        {
            if (cText != null)
                cText.text = strValue;
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
