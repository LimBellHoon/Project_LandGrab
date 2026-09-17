using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260918_카드 갤러리 (로비의 별도 탭 — LOBBY_TAB.CARD)
    /// <summary>
    /// 지금까지 웨이브를 깨서 드러낸 보상 이미지를 훑어보기만 하는 화면이다. 장착 개념이 없어
    /// 가방(CUI_Inventory)과 따로 뺐다.
    ///
    /// 새 저장 데이터를 만들지 않는다 — 별(=달성한 웨이브 수, 2-7)과 MapInfo.csv의 이미지 스택(2-5)을
    /// 그대로 읽어, 별 개수만큼의 웨이브 보상이 이미 드러난 것으로 본다.
    /// 목록은 CUI_Upgrade와 같은 방식으로 프리팹의 비활성 템플릿을 복제해 만든다.
    /// </summary>
    public class CUI_Card : CUI
    {
        [SerializeField] private Transform  m_trContent;
        [SerializeField] private Button     m_btnTemplate;      // 복제 원본 (항상 비활성)
        [SerializeField] private Text       m_txtTitle;

        private readonly List<Button> m_lstButton = new List<Button>();
        // 260918_크게 보기에 넘길 목록. 갤러리에 보이는 순서 그대로다
        private readonly List<CCardViewEntry> m_lstEntry = new List<CCardViewEntry>();
        private System.Action<IReadOnlyList<CCardViewEntry>, int> m_OnOpenViewer;

        private CCSVData_MapInfo  m_cMapTable;
        private CProgress_Manager m_cProgress;

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
                Debug.LogError("[CUI_Card] 프리팹에 Content / 버튼 템플릿이 연결돼 있지 않습니다. "
                             + "Tools/LandGrab/Setup Assets 를 실행하세요.");
                return false;
            }

            m_cMapTable    = cDesc.cMapTable;
            m_cProgress    = cDesc.cProgress;
            m_OnOpenViewer = cDesc.OnOpenViewer;

            m_btnTemplate.gameObject.SetActive(false);
            Build_List();
            return true;
        }

        public override void Hide()
        {
            Clear_List();
            m_cMapTable    = null;
            m_cProgress    = null;
            m_OnOpenViewer = null;

            base.Hide();
        }
        #endregion Engine.CUI

        private void Build_List()
        {
            Clear_List();

            if (m_cMapTable == null || m_cProgress == null)
            {
                Set_Title("MapInfo.csv를 읽지 못했습니다");
                return;
            }

            IReadOnlyList<CMapInfo> lstMap = m_cMapTable.ALL;
            int iCount = 0;

            for (int i = 0; i < lstMap.Count; ++i)
            {
                CMapInfo cMapInfo = lstMap[i];
                // 260918_별 = 달성한 웨이브 수(2-7) — 그만큼의 보상 이미지가 이미 드러난 것이다.
                int iStar = m_cProgress.Get_Star(cMapInfo.iMapID);

                for (int iWave = 1; iWave <= iStar; ++iWave)
                {
                    string strTex = cMapInfo.Get_RevealTex(iWave);
                    if (string.IsNullOrEmpty(strTex) == true)
                        continue;

                    ++iCount;
                    Make_Row(cMapInfo, iWave, strTex);
                }
            }

            Set_Title(iCount > 0 ? $"카드   (모은 카드 {iCount}장)"
                                 : "카드   (스테이지에서 웨이브를 깨면 여기 모입니다)");
        }

        private void Make_Row(CMapInfo cMapInfo, int iWave, string strTexName)
        {
            GameObject goButton = Instantiate(m_btnTemplate.gameObject, m_trContent);
            goButton.name = $"Btn_Card_{cMapInfo.iMapID}_{iWave}";
            goButton.SetActive(true);

            Button cButton = goButton.GetComponent<Button>();
            m_lstButton.Add(cButton);

            Text txtLabel = goButton.GetComponentInChildren<Text>();
            string strCaption = $"{cMapInfo.strMapName}   {iWave}웨이브 보상";
            if (txtLabel != null)
                txtLabel.text = strCaption;

            Add_Thumbnail(goButton, strTexName);

            // 260918_누르면 크게 보기 — 갤러리 순서 그대로 넘겨 좌우로 넘길 수 있게 한다
            int iIndex = m_lstEntry.Count;
            m_lstEntry.Add(new CCardViewEntry { strTexName = strTexName, strCaption = strCaption });
            cButton.onClick.AddListener(() => m_OnOpenViewer?.Invoke(m_lstEntry, iIndex));
        }

        // 260918_템플릿에 이미지 자리가 없어 런타임에 하나 붙인다 — 이 화면만을 위해 프리팹을 새로 만들지 않기 위해서다.
        private static void Add_Thumbnail(GameObject goRow, string strTexName)
        {
            Texture texture = CGameInstance.Instance.Get_Texture(strTexName);
            if (texture == null)
                return;

            GameObject goThumb = new GameObject("Thumbnail", typeof(RectTransform));
            goThumb.transform.SetParent(goRow.transform, false);

            RectTransform trThumb = goThumb.GetComponent<RectTransform>();
            trThumb.anchorMin        = new Vector2(1f, 0.5f);
            trThumb.anchorMax        = new Vector2(1f, 0.5f);
            trThumb.pivot            = new Vector2(1f, 0.5f);
            trThumb.anchoredPosition = new Vector2(-8f, 0f);
            trThumb.sizeDelta        = new Vector2(96f, 96f);

            RawImage imgThumb = goThumb.AddComponent<RawImage>();
            imgThumb.texture       = texture;
            imgThumb.raycastTarget = false;
        }

        // 260918_클라우드 작업에서 호출만 있고 정의가 빠져 컴파일되지 않았다 — CUI_Inventory와 같은 모양으로 채웠다.
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
