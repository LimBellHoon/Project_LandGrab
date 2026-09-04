using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260904_스테이지 선택 화면
    /// <summary>
    /// Engine.CUI를 상속하므로 오브젝트 풀과 UI 캔버스 관리에 그대로 올라탄다.
    /// 목록은 MapInfo.csv를 그대로 훑어 만든다 — 맵이 늘어도 UI 코드는 손대지 않는다.
    ///
    /// 버튼은 프리팹에 넣어 둔 템플릿(비활성)을 복제해 쓴다.
    /// 폰트·레이아웃 같은 겉모습은 프리팹에서 정해지고(코드로는 CProtoSetup이 만든다),
    /// 이 클래스는 '무엇을 보여줄지'만 결정한다.
    /// </summary>
    public class CUI_StageSelect : CUI
    {
        [SerializeField] private Transform  m_trContent;    // 버튼이 붙을 자리
        [SerializeField] private Button     m_btnTemplate;  // 복제 원본 (항상 비활성)
        [SerializeField] private Text       m_txtTitle;

        private readonly List<Button> m_lstButton = new List<Button>();

        private CCSVData_MapInfo    m_cMapTable;
        private CProgress_Manager   m_cProgress;
        private Action<int>         m_OnSelect;

        #region Engine.CUI
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CUI_StageSelectDesc cDesc) == false)
            {
                Debug.LogError("[CUI_StageSelect] CUI_StageSelectDesc가 아닙니다.");
                return false;
            }

            if (m_trContent == null || m_btnTemplate == null)
            {
                Debug.LogError("[CUI_StageSelect] 프리팹에 Content / 버튼 템플릿이 연결돼 있지 않습니다. "
                             + "Tools/LandGrab/Setup Assets 를 실행하세요.");
                return false;
            }

            m_cMapTable = cDesc.cMapTable;
            m_cProgress = cDesc.cProgress;
            m_OnSelect  = cDesc.OnSelect;

            m_btnTemplate.gameObject.SetActive(false);
            Build_List();
            return true;
        }

        public override void Hide()
        {
            // 풀에 반납되므로 만들어 둔 버튼과 콜백을 정리한다.
            Clear_List();
            m_cMapTable = null;
            m_cProgress = null;
            m_OnSelect  = null;

            base.Hide();
        }
        #endregion Engine.CUI

        /// <summary> 스테이지를 끝내고 돌아왔을 때 해금 상태를 다시 반영한다. </summary>
        public void Refresh_List()
        {
            if (m_cMapTable == null)
                return;

            Build_List();
        }

        private void Build_List()
        {
            Clear_List();

            IReadOnlyList<CMapInfo> lstMap = m_cMapTable.ALL;
            if (m_txtTitle != null)
            {
                // 260905_클리어 수보다 모은 별이 더 정확한 진행도다 — 웨이브를 얼마나 파고들었는지 보인다.
                m_txtTitle.text = $"스테이지 선택   ★ {m_cProgress.TOTAL_STAR} / {Get_TotalWave(lstMap)}"
                                + $"   ({m_cProgress.CLEARED_COUNT} / {lstMap.Count} 클리어)";
            }

            for (int i = 0; i < lstMap.Count; ++i)
            {
                CMapInfo cMapInfo = lstMap[i];

                GameObject goButton = Instantiate(m_btnTemplate.gameObject, m_trContent);
                goButton.name = $"Btn_Map_{cMapInfo.iMapID}";
                goButton.SetActive(true);

                Button cButton = goButton.GetComponent<Button>();
                bool bUnlocked  = m_cProgress.Is_Unlocked(cMapInfo.iMapID);

                cButton.interactable = bUnlocked;
                Set_ButtonLabel(goButton, cMapInfo, bUnlocked);

                // 클로저가 반복 변수를 잡지 않도록 지역에 복사해 둔다.
                int iMapID = cMapInfo.iMapID;
                cButton.onClick.AddListener(() => m_OnSelect?.Invoke(iMapID));

                m_lstButton.Add(cButton);
            }
        }

        private void Set_ButtonLabel(GameObject goButton, CMapInfo cMapInfo, bool bUnlocked)
        {
            Text txtLabel = goButton.GetComponentInChildren<Text>();
            if (txtLabel == null)
                return;

            // 260905_잠긴 맵은 자물쇠, 열린 맵은 별 칸을 웨이브 수만큼 보여준다.
            // 한 번도 안 깬 맵도 빈 별(☆☆☆)이 보여야 '뭘 채우는 건지'가 전달된다.
            if (bUnlocked == false)
            {
                txtLabel.text = $"🔒  {cMapInfo.iMapID}. {cMapInfo.strMapName}";
                return;
            }

            int iStar = m_cProgress.Get_Star(cMapInfo.iMapID);

            txtLabel.text = $"{CStar_Utility.Get_Text(iStar, cMapInfo.iWaveCount)}  "
                          + $"{cMapInfo.iMapID}. {cMapInfo.strMapName}";
        }

        private static int Get_TotalWave(IReadOnlyList<CMapInfo> lstMap)
        {
            int iTotal = 0;
            for (int i = 0; i < lstMap.Count; ++i)
                iTotal += lstMap[i].iWaveCount;

            return iTotal;
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
