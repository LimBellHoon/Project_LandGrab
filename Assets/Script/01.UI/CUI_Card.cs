using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260918_카드 갤러리 (로비의 별도 탭 — LOBBY_TAB.CARD)
    // 260920_캐릭터 수집 화면으로 다시 짰다 — 위 서브 탭 둘 + 격자(2-17-2)
    /// <summary>
    /// [캐릭터] 탭은 캐릭터를 그림 + 이름으로 늘어놓는다. **못 가진 캐릭터도 어둡게 보여 준다** —
    /// 무엇을 모을 수 있는지 보여야 모으고 싶어진다(가방 캐릭터 탭과 같은 결, 2-17).
    /// [갤러리] 탭은 지금까지 연 카드를 전부 격자로 편다.
    ///
    /// **카드는 캐릭터에 딸려 있다**(`CharacterInfo.csv`의 `strCardTex`). 표에 캐릭터를 한 줄 더하면
    /// 두 탭 모두에 그대로 따라 붙는다 — 이 클래스는 고치지 않는다.
    /// 무엇을 보여 줄지는 표가, 크게 보기는 `CUI_CardViewer`가 맡는다(2-7).
    /// </summary>
    public class CUI_Card : CUI
    {
        [SerializeField] private Transform  m_trContent;
        [SerializeField] private Button     m_btnTemplate;      // 격자 칸 템플릿 (항상 비활성)
        [SerializeField] private Text       m_txtTitle;
        // 260920_위 서브 탭 둘. 눌린 쪽만 밝게 칠한다.
        [SerializeField] private Button     m_btnTabCharacter;
        [SerializeField] private Button     m_btnTabGallery;

        // 260920_고른 탭은 밝은 하늘색 + 흰 글자, 나머지는 어둡게 가라앉힌다 —
        // 색만 살짝 다르면 지금 어느 탭인지 한눈에 안 읽힌다(레퍼런스 화면과 같은 대비).
        private static readonly Color COLOR_TAB_ON      = new Color(0.25f, 0.80f, 0.95f);
        private static readonly Color COLOR_TAB_OFF     = new Color(0.13f, 0.20f, 0.27f);
        private static readonly Color COLOR_TAB_TEXT_ON  = new Color(1f, 1f, 1f);
        private static readonly Color COLOR_TAB_TEXT_OFF = new Color(0.62f, 0.70f, 0.78f);
        // 못 가졌거나 아직 안 열린 칸 — 지우지 않고 어둡게만 둔다
        private static readonly Color COLOR_CELL_LOCK = new Color(0.35f, 0.38f, 0.45f);
        private static readonly Color COLOR_CELL_OPEN = new Color(1f, 1f, 1f);

        private readonly List<Button> m_lstButton = new List<Button>();
        // 크게 보기에 넘길 목록. 화면에 보이는 순서 그대로다
        private readonly List<CCardViewEntry> m_lstEntry = new List<CCardViewEntry>();
        private System.Action<IReadOnlyList<CCardViewEntry>, int> m_OnOpenViewer;

        private CCSVData_CharacterInfo m_cCharacterTable;
        private CProgress_Manager      m_cProgress;
        private CARD_TAB               m_eTab = CARD_TAB.CHARACTER;

        #region Engine.CUI
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CUI_CardDesc cDesc) == false)
            {
                Debug.LogError("[CUI_Card] CUI_CardDesc가 아닙니다.");
                return false;
            }

            if (m_trContent == null || m_btnTemplate == null)
            {
                Debug.LogError("[CUI_Card] 프리팹에 Content / 칸 템플릿이 연결돼 있지 않습니다. "
                             + "Tools/LandGrab/Setup Assets 를 실행하세요.");
                return false;
            }

            m_cCharacterTable = cDesc.cCharacterTable;
            m_cProgress       = cDesc.cProgress;
            m_OnOpenViewer    = cDesc.OnOpenViewer;

            m_btnTemplate.gameObject.SetActive(false);

            Bind_Tab(m_btnTabCharacter, CARD_TAB.CHARACTER);
            Bind_Tab(m_btnTabGallery,   CARD_TAB.GALLERY);

            Set_Tab(CARD_TAB.CHARACTER);
            return true;
        }

        public override void Hide()
        {
            Clear_List();
            m_cCharacterTable = null;
            m_cProgress       = null;
            m_OnOpenViewer    = null;

            base.Hide();
        }
        #endregion Engine.CUI

        private void Bind_Tab(Button cButton, CARD_TAB eTab)
        {
            if (cButton == null)
                return;

            cButton.onClick.RemoveAllListeners();
            cButton.onClick.AddListener(() => Set_Tab(eTab));
        }

        private void Set_Tab(CARD_TAB eTab)
        {
            m_eTab = eTab;

            Paint_Tab(m_btnTabCharacter, eTab == CARD_TAB.CHARACTER);
            Paint_Tab(m_btnTabGallery,   eTab == CARD_TAB.GALLERY);

            Build_List();
        }

        private static void Paint_Tab(Button cButton, bool bOn)
        {
            if (cButton == null)
                return;

            Image cImage = cButton.GetComponent<Image>();
            if (cImage != null)
                cImage.color = bOn == true ? COLOR_TAB_ON : COLOR_TAB_OFF;

            Text cText = cButton.GetComponentInChildren<Text>(true);
            if (cText != null)
                cText.color = bOn == true ? COLOR_TAB_TEXT_ON : COLOR_TAB_TEXT_OFF;
        }

        private void Build_List()
        {
            Clear_List();

            if (m_cCharacterTable == null || m_cProgress == null)
            {
                Set_Title("CharacterInfo.csv를 읽지 못했습니다");
                return;
            }

            if (m_eTab == CARD_TAB.CHARACTER)
                Build_Characters();
            else
                Build_Gallery();
        }

        // ── [캐릭터] 탭 — 못 가진 캐릭터도 어둡게 보여 준다
        private void Build_Characters()
        {
            IReadOnlyList<CCharacterInfo> lstInfo = m_cCharacterTable.ALL;
            int iOwned = 0;

            for (int i = 0; i < lstInfo.Count; ++i)
            {
                CCharacterInfo cInfo = lstInfo[i];
                if (cInfo == null)
                    continue;

                int  iLevel = m_cProgress.Get_CharacterLevel(cInfo.iCharacterID);
                bool bOwned = iLevel > 0;
                if (bOwned == true)
                    ++iOwned;

                // 대표 그림은 그 캐릭터의 첫 카드다. 못 가졌으면 그림은 그대로 두고 어둡게만 칠한다
                // (실루엣처럼 보여야 '무엇을 모을 수 있는지'가 읽힌다).
                int iOpen = Count_OpenCard(cInfo, iLevel);
                GameObject goCell = Make_Cell(cInfo.strName, cInfo.Get_CardTex(0), bOwned,
                                              bOwned == true ? $"{iOpen}/{cInfo.CARD_COUNT}" : "미보유");

                Button cButton = goCell.GetComponent<Button>();
                CCharacterInfo cPicked = cInfo;
                cButton.onClick.AddListener(() => Open_CharacterCards(cPicked));
            }

            Set_Title($"카드   (캐릭터 {iOwned}/{lstInfo.Count})");
        }

        // ── [갤러리] 탭 — 지금까지 연 카드를 전부 편다
        private void Build_Gallery()
        {
            IReadOnlyList<CCharacterInfo> lstInfo = m_cCharacterTable.ALL;
            int iOpen  = 0;
            int iTotal = 0;

            for (int i = 0; i < lstInfo.Count; ++i)
            {
                CCharacterInfo cInfo = lstInfo[i];
                if (cInfo == null)
                    continue;

                int iLevel = m_cProgress.Get_CharacterLevel(cInfo.iCharacterID);

                for (int iCard = 0; iCard < cInfo.CARD_COUNT; ++iCard)
                {
                    ++iTotal;

                    bool   bOpen   = cInfo.Is_CardUnlocked(iLevel, iCard);
                    string strTex  = cInfo.Get_CardTex(iCard);
                    string strName = $"{cInfo.strName}  {iCard + 1}";

                    if (bOpen == true)
                        ++iOpen;

                    GameObject goCell = Make_Cell(bOpen == true ? strName : "???", strTex, bOpen,
                                                  bOpen == true ? string.Empty : $"Lv.{cInfo.Get_CardUnlockLevel(iCard)}");

                    if (bOpen == false)
                        continue;

                    // 누르면 크게 보기 — 갤러리 순서 그대로 넘겨 좌우로 넘길 수 있게 한다
                    int iIndex = m_lstEntry.Count;
                    m_lstEntry.Add(new CCardViewEntry { strTexName = strTex, strCaption = strName });
                    goCell.GetComponent<Button>().onClick.AddListener(() => m_OnOpenViewer?.Invoke(m_lstEntry, iIndex));
                }
            }

            Set_Title($"카드   (모은 카드 {iOpen}/{iTotal}장)");
        }

        /// <summary> 캐릭터 한 명의 카드만 크게 보기로 연다. 아직 안 열린 장은 넘기지 않는다. </summary>
        private void Open_CharacterCards(CCharacterInfo cInfo)
        {
            if (cInfo == null || m_OnOpenViewer == null)
                return;

            int iLevel = m_cProgress.Get_CharacterLevel(cInfo.iCharacterID);
            List<CCardViewEntry> lstEntry = new List<CCardViewEntry>();

            for (int i = 0; i < cInfo.CARD_COUNT; ++i)
            {
                if (cInfo.Is_CardUnlocked(iLevel, i) == false)
                    continue;

                lstEntry.Add(new CCardViewEntry
                {
                    strTexName = cInfo.Get_CardTex(i),
                    strCaption = $"{cInfo.strName}  {i + 1}",
                });
            }

            if (lstEntry.Count <= 0)
                return;

            m_OnOpenViewer.Invoke(lstEntry, 0);
        }

        public static int Count_OpenCard(CCharacterInfo cInfo, int iLevel)
        {
            if (cInfo == null)
                return 0;

            int iCount = 0;
            for (int i = 0; i < cInfo.CARD_COUNT; ++i)
            {
                if (cInfo.Is_CardUnlocked(iLevel, i) == true)
                    ++iCount;
            }

            return iCount;
        }

        // 칸 하나 — 그림 · 이름 · 오른쪽 아래 상태. 두 탭이 같은 모양을 쓴다.
        private GameObject Make_Cell(string strName, string strTexName, bool bOpen, string strBadge)
        {
            GameObject goCell = Instantiate(m_btnTemplate.gameObject, m_trContent);
            goCell.name = $"Cell_{strName}";
            goCell.SetActive(true);
            m_lstButton.Add(goCell.GetComponent<Button>());

            Set_Text(goCell, "Txt_Name",  strName);
            Set_Text(goCell, "Txt_Badge", strBadge);

            Transform trImage = goCell.transform.Find("Img_Portrait");
            RawImage cImage = trImage != null ? trImage.GetComponent<RawImage>() : null;
            if (cImage != null)
            {
                cImage.texture = CGameInstance.Instance.Get_Texture(strTexName);
                cImage.color   = bOpen == true ? COLOR_CELL_OPEN : COLOR_CELL_LOCK;
            }

            return goCell;
        }

        private static void Set_Text(GameObject goCell, string strPath, string strValue)
        {
            Transform trPart = goCell.transform.Find(strPath);
            Text cText = trPart != null ? trPart.GetComponent<Text>() : null;
            if (cText != null)
                cText.text = strValue;
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

                m_lstButton[i].onClick.RemoveAllListeners();
                Destroy(m_lstButton[i].gameObject);
            }

            m_lstButton.Clear();
            m_lstEntry.Clear();
        }
    }
}
