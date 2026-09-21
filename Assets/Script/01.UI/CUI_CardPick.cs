using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260912_카드 3지선다 — 점령률을 넘길 때마다 한 번
    // 260917_런 스킬도 같은 자리에 섞여 나온다(CPickOption). 새로 얻는지 · 몇 레벨이 되는지를 이름 옆에 적는다
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
        // 260912_CARD_TYPE 순서대로 넣어 둔다. NONE은 0번이라 한 칸 당겨 쓴다.
        [SerializeField] private Sprite[]       m_arrIcon;
        // 260917_RUN_SKILL_TYPE 순서대로. 흰색으로 구워 두고 분류(액티브/패시브) 색을 칠한다
        [SerializeField] private Sprite[]       m_arrRunSkillIcon;

        // 260921_다시 뽑기 · 버리기(2-10-1). 버리기는 누르면 '버릴 카드 고르기' 상태가 되고, 카드를 누르면 버린다
        [SerializeField] private Button         m_btnReroll;
        [SerializeField] private Button         m_btnBanish;

        private readonly List<GameObject>  m_lstSpawned = new List<GameObject>();
        private readonly List<CPickOption> m_lstOption  = new List<CPickOption>();

        private Action<CPickOption> m_OnPick;
        private Func<IReadOnlyList<CPickOption>>                          m_OnReroll;
        private Func<CPickOption, IReadOnlyList<CPickOption>, CPickOption> m_OnBanish;
        private Func<int>   m_fnRerollLeft;
        private Func<int>   m_fnBanishLeft;
        private bool        m_bBanishMode;
        private string      m_strTitle;

        private static readonly Color COLOR_BANISH_ON  = new Color(0.85f, 0.25f, 0.25f, 1f);
        private static readonly Color COLOR_BUTTON_OFF = new Color(0.20f, 0.26f, 0.44f, 0.9f);

        #region Engine.CUI
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CUI_CardPickDesc cDesc) == false)
            {
                Debug.LogError("[CUI_CardPick] CUI_CardPickDesc가 아닙니다.");
                return false;
            }

            m_OnPick       = cDesc.OnPick;
            m_OnReroll     = cDesc.OnReroll;
            m_OnBanish     = cDesc.OnBanish;
            m_fnRerollLeft = cDesc.fnRerollLeft;
            m_fnBanishLeft = cDesc.fnBanishLeft;
            m_strTitle     = cDesc.strTitle;
            m_bBanishMode  = false;

            m_lstOption.Clear();
            if (cDesc.lstOption != null)
                m_lstOption.AddRange(cDesc.lstOption);

            Bind(m_btnReroll, On_ClickReroll);
            Bind(m_btnBanish, On_ClickBanish);

            Build_Cards(m_lstOption);
            Refresh_Tools();
            return true;
        }

        public override void Hide()
        {
            Clear_Cards();
            m_OnPick   = null;
            m_OnReroll = null;
            m_OnBanish = null;
            m_fnRerollLeft = null;
            m_fnBanishLeft = null;
            m_lstOption.Clear();
            Unbind(m_btnReroll);
            Unbind(m_btnBanish);
            base.Hide();
        }
        #endregion Engine.CUI

        private void Build_Cards(IReadOnlyList<CPickOption> lstOption)
        {
            Clear_Cards();

            if (m_btnTemplate == null || m_trContent == null || lstOption == null)
                return;

            for (int i = 0; i < lstOption.Count; ++i)
            {
                CPickOption cInfo = lstOption[i];
                if (cInfo == null)
                    continue;

                GameObject goCard = Instantiate(m_btnTemplate.gameObject, m_trContent);
                goCard.SetActive(true);
                m_lstSpawned.Add(goCard);

                Paint_Card(goCard, cInfo);

                Button cButton = goCard.GetComponent<Button>();
                if (cButton == null)
                    continue;

                // 260912_루프 변수를 그대로 넘기면 마지막 카드만 잡힌다. 지역 변수로 묶어 둔다.
                CPickOption cPicked = cInfo;
                cButton.onClick.RemoveAllListeners();
                cButton.onClick.AddListener(() => On_Click(cPicked));
            }
        }

        // 260920_카드 세 종류(각성 / 액티브 / 패시브)의 **레이아웃은 전부 같다**. 다른 것은 색뿐이다.
        //   각성  — 카드 전체가 보라색(머리띠도 본문도). 일반 카드와 한눈에 달라 보여야 한다
        //   전투  — 파랑 (COMBAT)
        //   이동  — 주황 (MOVE)
        // 무엇이 전투이고 무엇이 이동인지는 표의 eTheme 열이 정한다 — 화면은 색만 고른다.
        private static readonly Color THEME_COMBAT      = new Color(0.25f, 0.55f, 0.95f);
        private static readonly Color THEME_MOVE        = new Color(1.00f, 0.58f, 0.15f);
        private static readonly Color THEME_AWAKEN      = new Color(0.62f, 0.32f, 0.88f);
        // 일반 카드의 본문은 밝은 아이보리, 각성만 짙은 보라 — 그래서 '카드 전체 색이 다르다'가 된다.
        private static readonly Color BODY_NORMAL       = new Color(0.97f, 0.96f, 0.93f);
        private static readonly Color BODY_AWAKEN       = new Color(0.20f, 0.10f, 0.30f);
        private static readonly Color TEXT_ON_NORMAL    = new Color(0.15f, 0.15f, 0.18f);
        private static readonly Color TEXT_ON_AWAKEN    = new Color(0.98f, 0.94f, 1.00f);

        /// <summary> 머리띠 · 테두리에 쓰는 색. 각성이면 테마와 무관하게 보라다. </summary>
        public static Color Get_ThemeColor(CPickOption cOption)
        {
            if (cOption == null)
                return Color.white;

            if (cOption.eKind == PICK_KIND.AWAKEN)
                return THEME_AWAKEN;

            return cOption.THEME == PICK_THEME.MOVE ? THEME_MOVE : THEME_COMBAT;
        }

        /// <summary> 이름. 각성만 무엇이 바뀌는지 앞에 붙인다 — 어느 스킬이 각성하는지가 먼저 읽혀야 한다. </summary>
        public static string Get_Title(CPickOption cOption)
            => cOption.eKind == PICK_KIND.AWAKEN ? $"각성  {cOption.NAME}" : cOption.NAME;

        /// <summary> 현재 레벨. 새로 얻는 스킬은 NEW, 레벨이 없는 카드 · 각성은 빈 칸이다. </summary>
        public static string Get_LevelText(CPickOption cOption)
        {
            if (cOption == null || cOption.eKind != PICK_KIND.RUN_SKILL)
                return string.Empty;

            if (cOption.IS_NEW == true)
                return "NEW";

            return cOption.LEVEL > 0 ? $"Lv.{cOption.LEVEL}" : string.Empty;
        }

        // 이름 · 아이콘 · 설명 · 레벨 넷만 칠한다. 카드가 늘어도 여기 규칙은 그대로다.
        private void Paint_Card(GameObject goCard, CPickOption cOption)
        {
            bool  bAwaken = cOption.eKind == PICK_KIND.AWAKEN;
            Color cTheme  = Get_ThemeColor(cOption);
            Color cText   = bAwaken ? TEXT_ON_AWAKEN : TEXT_ON_NORMAL;

            Paint_Part(goCard, "Img_Header", null, cTheme);
            Paint_Part(goCard, "Img_Body",   null, bAwaken ? BODY_AWAKEN : BODY_NORMAL);
            Paint_Part(goCard, "Img_Glow",   null, cTheme);
            Paint_Part(goCard, "Img_Body/Img_Icon", Get_Icon(cOption), bAwaken ? THEME_AWAKEN : cTheme);

            Set_Text(goCard, "Img_Header/Txt_Name", Get_Title(cOption), Color.white);
            Set_Text(goCard, "Img_Body/Txt_Desc",   cOption.DESC,       cText);
            Set_Text(goCard, "Img_Body/Txt_Level",  Get_LevelText(cOption), cTheme);
        }

        private static void Set_Text(GameObject goCard, string strPath, string strValue, Color cColor)
        {
            Transform trPart = goCard.transform.Find(strPath);
            Text cText = trPart != null ? trPart.GetComponent<Text>() : null;
            if (cText == null)
                return;

            cText.text  = strValue;
            cText.color = cColor;
        }

        private Sprite Get_Icon(CPickOption cOption)
        {
            // NONE이 0번이라 한 칸 당긴다. 표에 종류를 더하면 배열에도 같은 순서로 넣어야 한다.
            // 각성은 바뀌는 액티브의 아이콘을 금색으로 쓴다
            if (cOption.eKind == PICK_KIND.CARD)
                return Get_ArrayItem(m_arrIcon, (int)cOption.cCard.eType - 1);

            return cOption.cRunSkill != null ? Get_ArrayItem(m_arrRunSkillIcon, (int)cOption.cRunSkill.eType - 1) : null;
        }

        private static Sprite Get_ArrayItem(Sprite[] arrSprite, int iIndex)
        {
            if (arrSprite == null || iIndex < 0 || iIndex >= arrSprite.Length)
                return null;

            return arrSprite[iIndex];
        }

        private static void Paint_Part(GameObject goCard, string strName, Sprite spSprite, Color cTint)
        {
            Transform trPart = goCard.transform.Find(strName);
            Image cImage = trPart != null ? trPart.GetComponent<Image>() : null;
            if (cImage == null)
                return;

            if (spSprite != null)
                cImage.sprite = spSprite;

            cImage.color = cTint;
        }

        #region 260921_다시 뽑기 · 버리기
        private void On_ClickReroll()
        {
            IReadOnlyList<CPickOption> lstNew = m_OnReroll?.Invoke();
            if (lstNew == null || lstNew.Count == 0)
                return;

            m_bBanishMode = false;
            m_lstOption.Clear();
            m_lstOption.AddRange(lstNew);
            Build_Cards(m_lstOption);
            Refresh_Tools();
        }

        // 한 번 누르면 '버릴 카드를 고르세요', 다시 누르면 취소
        private void On_ClickBanish()
        {
            if (m_bBanishMode == false && Get_Left(m_fnBanishLeft) <= 0)
                return;

            m_bBanishMode = !m_bBanishMode;
            Refresh_Tools();
        }

        private void Banish(CPickOption cOption)
        {
            m_bBanishMode = false;

            CPickOption cReplace = m_OnBanish?.Invoke(cOption, m_lstOption);
            int iIndex = m_lstOption.IndexOf(cOption);
            if (cReplace == null || iIndex < 0)
            {
                Refresh_Tools();
                return;
            }

            // 채울 것이 없으면 그 칸은 빠진다 — 마지막 한 장은 남겨 둔다(고를 것이 없으면 창이 안 닫힌다)
            if (cReplace == cOption)
            {
                if (m_lstOption.Count > 1)
                    m_lstOption.RemoveAt(iIndex);
            }
            else
            {
                m_lstOption[iIndex] = cReplace;
            }

            Build_Cards(m_lstOption);
            Refresh_Tools();
        }

        private void Refresh_Tools()
        {
            int iReroll = Get_Left(m_fnRerollLeft);
            int iBanish = Get_Left(m_fnBanishLeft);

            Set_Button(m_btnReroll, $"다시 뽑기  {iReroll}", iReroll > 0, COLOR_BUTTON_OFF);
            Set_Button(m_btnBanish, m_bBanishMode == true ? "취소" : $"버리기  {iBanish}",
                       m_bBanishMode == true || iBanish > 0, m_bBanishMode == true ? COLOR_BANISH_ON : COLOR_BUTTON_OFF);

            if (m_txtTitle != null)
                m_txtTitle.text = m_bBanishMode == true ? "버릴 카드를 고르세요 — 이번 판에 다시 안 나온다" : m_strTitle;
        }

        private static int Get_Left(Func<int> fnLeft) => fnLeft != null ? fnLeft() : 0;

        private static void Set_Button(Button cButton, string strLabel, bool bInteractable, Color cColor)
        {
            if (cButton == null)
                return;

            cButton.interactable = bInteractable;
            Image cImage = cButton.GetComponent<Image>();
            if (cImage != null)
                cImage.color = cColor;

            Text cText = cButton.GetComponentInChildren<Text>();
            if (cText != null)
                cText.text = strLabel;
        }

        private static void Bind(Button cButton, UnityEngine.Events.UnityAction fnClick)
        {
            if (cButton == null)
                return;

            cButton.onClick.RemoveAllListeners();
            cButton.onClick.AddListener(fnClick);
        }

        private static void Unbind(Button cButton)
        {
            if (cButton != null)
                cButton.onClick.RemoveAllListeners();
        }
        #endregion 다시 뽑기 · 버리기

        private void On_Click(CPickOption cInfo)
        {
            if (m_bBanishMode == true)
            {
                Banish(cInfo);
                return;
            }

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
