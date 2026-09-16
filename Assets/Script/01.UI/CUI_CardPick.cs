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

        private readonly List<GameObject> m_lstSpawned = new List<GameObject>();

        private Action<CPickOption> m_OnPick;

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

            Build_Cards(cDesc.lstOption);
            return true;
        }

        public override void Hide()
        {
            Clear_Cards();
            m_OnPick = null;
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

                Text[] arrText = goCard.GetComponentsInChildren<Text>(true);
                if (arrText.Length > 0)
                    arrText[0].text = Get_Title(cInfo);
                if (arrText.Length > 1)
                    arrText[1].text = cInfo.DESC;

                Color cTint = Get_Tint(cInfo);
                Paint_Part(goCard, "Img_Icon", Get_Icon(cInfo), cTint);
                Paint_Part(goCard, "Img_Glow", null, cTint);

                Button cButton = goCard.GetComponent<Button>();
                if (cButton == null)
                    continue;

                // 260912_루프 변수를 그대로 넘기면 마지막 카드만 잡힌다. 지역 변수로 묶어 둔다.
                CPickOption cPicked = cInfo;
                cButton.onClick.RemoveAllListeners();
                cButton.onClick.AddListener(() => On_Click(cPicked));
            }
        }

        // 260917_런 스킬은 이름 옆에 '새로 얻는가 / 몇 레벨이 되는가'를 붙인다. 레벨이 하나뿐인 스킬은 붙이지 않는다.
        public static string Get_Title(CPickOption cOption)
        {
            if (cOption.eKind == PICK_KIND.CARD)
                return cOption.NAME;

            // 260917_각성은 어느 스킬이 바뀌는지가 먼저 읽혀야 한다
            if (cOption.eKind == PICK_KIND.AWAKEN)
                return $"각성  {cOption.NAME}";

            if (cOption.IS_NEW == true)
                return $"{cOption.NAME}  NEW";

            return cOption.cRunSkill.iMaxLevel <= 1 ? cOption.NAME : $"{cOption.NAME}  Lv.{cOption.iNextLevel}";
        }

        // 260917_런 스킬은 분류로 색을 가른다 — 액티브는 공격적인 주황, 패시브는 청록.
        // 카드 다섯 색과 겹치지 않게 골랐다(카드는 종류마다 색이 따로 있다).
        private static readonly Color TINT_RUN_ACTIVE  = new Color(1.00f, 0.50f, 0.20f);
        private static readonly Color TINT_RUN_PASSIVE = new Color(0.30f, 0.95f, 0.90f);
        // 260917_각성은 금색 — 언제 떠도 놓치면 아쉬운 선택이라 일반 픽과 한눈에 달라 보여야 한다(문서 3장)
        private static readonly Color TINT_AWAKEN      = new Color(1.00f, 0.84f, 0.25f);

        private static Color Get_Tint(CPickOption cOption)
        {
            if (cOption.eKind == PICK_KIND.AWAKEN)
                return TINT_AWAKEN;

            if (cOption.eKind == PICK_KIND.RUN_SKILL)
                return cOption.cRunSkill.IS_PASSIVE == true ? TINT_RUN_PASSIVE : TINT_RUN_ACTIVE;

            return Get_Tint(cOption.cCard.eType);
        }

        // 260912_종류마다 색을 달리해 셋을 한눈에 가르게 한다.
        // 글자만으로는 순간적으로 고르기 어렵다 — 색과 아이콘이 먼저 읽힌다.
        private static Color Get_Tint(CARD_TYPE eType)
        {
            switch (eType)
            {
                case CARD_TYPE.SHIELD:  return new Color(0.45f, 0.80f, 1.00f);
                case CARD_TYPE.HEAL:    return new Color(0.45f, 1.00f, 0.60f);
                case CARD_TYPE.SPEED:   return new Color(1.00f, 0.85f, 0.35f);
                case CARD_TYPE.EVASION: return new Color(0.80f, 0.60f, 1.00f);
                case CARD_TYPE.SLOW:    return new Color(1.00f, 0.55f, 0.55f);
                default:                return Color.white;
            }
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

        private void On_Click(CPickOption cInfo)
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
