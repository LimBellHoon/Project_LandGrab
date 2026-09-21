using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260904_스테이지 선택 화면
    // 260921_목록형에서 '한 장씩 넘기는 카드'로 다시 짰다 (2-23)
    /// <summary>
    /// 한 번에 스테이지 하나만 크게 보여 준다 — 흐린 보상 그림(A) · 좌우 넘김(B) · 진입 버튼(C).
    /// 목록은 여전히 MapInfo.csv를 그대로 훑는다 — 맵이 늘어도 UI 코드는 손대지 않는다.
    ///
    /// 들어가는 판정(해금 · 하트)은 이 화면이 하지 않는다. 버튼은 고른 맵 ID만 올려보내고,
    /// 하트를 쓰고 스테이지를 까는 것은 CGameManager가 한다(2-7 — 화면 전환은 한곳에서).
    /// 이 화면은 '지금 들어가면 얼마가 드는지'와 '들어갈 수 있는지'를 보여 주기만 한다.
    /// </summary>
    public class CUI_StageSelect : CUI
    {
        [SerializeField] private Text       m_txtTitle;         // "스테이지 3 / 10"
        [SerializeField] private Text       m_txtStageName;     // 맵 이름
        [SerializeField] private Text       m_txtStageInfo;     // 별 · 해금 상태
        [SerializeField] private RawImage   m_imgStage;         // 흐린 보상 그림
        [SerializeField] private Button     m_btnPrev;
        [SerializeField] private Button     m_btnNext;
        [SerializeField] private Button     m_btnEnter;
        [SerializeField] private Text       m_txtEnter;         // "진입   ♥ -5"

        private static readonly Color COLOR_IMAGE_LOCK = new Color(0.35f, 0.35f, 0.42f);

        private CCSVData_MapInfo    m_cMapTable;
        private CProgress_Manager   m_cProgress;
        private Action<int>         m_OnSelect;
        private int                 m_iIndex;

        #region Engine.CUI
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CUI_StageSelectDesc cDesc) == false)
            {
                Debug.LogError("[CUI_StageSelect] CUI_StageSelectDesc가 아닙니다.");
                return false;
            }

            if (m_btnEnter == null || m_imgStage == null)
            {
                Debug.LogError("[CUI_StageSelect] 프리팹에 진입 버튼 / 그림 자리가 연결돼 있지 않습니다. "
                             + "Tools/LandGrab/Setup Assets 를 실행하세요.");
                return false;
            }

            m_cMapTable = cDesc.cMapTable;
            m_cProgress = cDesc.cProgress;
            m_OnSelect  = cDesc.OnSelect;

            Bind(m_btnPrev,  () => Move(-1));
            Bind(m_btnNext,  () => Move(+1));
            Bind(m_btnEnter, On_ClickEnter);

            // 처음에는 **가장 최근에 열린 스테이지**를 보여 준다 — 매번 1번부터 넘겨 오게 하면 번거롭다.
            m_iIndex = Find_LatestUnlocked();
            Refresh();
            return true;
        }

        public override void Hide()
        {
            Unbind(m_btnPrev);
            Unbind(m_btnNext);
            Unbind(m_btnEnter);

            m_cMapTable = null;
            m_cProgress = null;
            m_OnSelect  = null;

            base.Hide();
        }
        #endregion Engine.CUI

        /// <summary> 스테이지를 끝내고 돌아왔을 때 해금 · 별 · 하트를 다시 반영한다. </summary>
        public void Refresh_List() => Refresh();

        private void Move(int iStep)
        {
            if (m_cMapTable == null)
                return;

            m_iIndex = Clamp_Index(m_iIndex + iStep, m_cMapTable.COUNT);
            Refresh();
        }

        /// <summary> 끝에서 반대쪽으로 돌아가지 않는다 — 1번에서 왼쪽을 눌러 마지막으로 튀면 길을 잃는다. </summary>
        public static int Clamp_Index(int iIndex, int iCount) => iCount <= 0 ? 0 : Mathf.Clamp(iIndex, 0, iCount - 1);

        private int Find_LatestUnlocked()
        {
            if (m_cMapTable == null || m_cProgress == null)
                return 0;

            int iLatest = 0;
            IReadOnlyList<CMapInfo> lstMap = m_cMapTable.ALL;
            for (int i = 0; i < lstMap.Count; ++i)
            {
                if (m_cProgress.Is_Unlocked(lstMap[i].iMapID) == true)
                    iLatest = i;
            }

            return iLatest;
        }

        private void Refresh()
        {
            if (m_cMapTable == null || m_cProgress == null || m_cMapTable.COUNT <= 0)
                return;

            m_iIndex = Clamp_Index(m_iIndex, m_cMapTable.COUNT);
            CMapInfo cMapInfo = m_cMapTable.ALL[m_iIndex];
            bool bUnlocked = m_cProgress.Is_Unlocked(cMapInfo.iMapID);

            Set_Text(m_txtTitle, $"스테이지  {m_iIndex + 1} / {m_cMapTable.COUNT}");
            Set_Text(m_txtStageName, bUnlocked == true ? cMapInfo.strMapName : $"🔒  {cMapInfo.strMapName}");

            int iStar = m_cProgress.Get_Star(cMapInfo.iMapID);
            Set_Text(m_txtStageInfo, bUnlocked == true
                                   ? $"{CStar_Utility.Get_Text(iStar, cMapInfo.iWaveCount)}    경험치 최대 {cMapInfo.iExpTotal}"
                                   : "앞 스테이지를 깨면 열린다");

            // A — 이 스테이지의 최종 보상 그림을 흐리게. 잠긴 판은 더 어둡게 눌러 둔다.
            string strTex = cMapInfo.Get_RevealTex(cMapInfo.iWaveCount);
            Texture texSource = string.IsNullOrEmpty(strTex) == false ? CGameInstance.Instance.Get_Texture(strTex) : null;
            m_imgStage.texture = CBlur_Utility.Get_Blurred(strTex, texSource);
            m_imgStage.color   = bUnlocked == true ? Color.white : COLOR_IMAGE_LOCK;

            // B — 끝에서는 그쪽 화살표를 끈다
            if (m_btnPrev != null)
                m_btnPrev.interactable = m_iIndex > 0;
            if (m_btnNext != null)
                m_btnNext.interactable = m_iIndex < m_cMapTable.COUNT - 1;

            // C — 잠겼으면 못 누른다. 하트가 모자라도 누를 수는 있다(누르면 왜 못 들어가는지 알려 준다).
            m_btnEnter.interactable = bUnlocked;
            Set_Text(m_txtEnter, bUnlocked == true ? $"진입     ♥ -{cMapInfo.iStaminaCost}" : "잠김");
        }

        private void On_ClickEnter()
        {
            if (m_cMapTable == null || m_cMapTable.COUNT <= 0)
                return;

            m_OnSelect?.Invoke(m_cMapTable.ALL[m_iIndex].iMapID);
        }

        private static void Set_Text(Text cText, string strValue)
        {
            if (cText != null)
                cText.text = strValue;
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
    }
}
