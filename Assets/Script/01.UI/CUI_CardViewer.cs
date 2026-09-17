using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260918_카드 크게 보기 — 갤러리(CUI_Card)에서 한 장을 누르면 화면 전체로 띄우고 좌우로 넘긴다
    /// <summary>
    /// 이 게임의 재미가 '드러난 그림'이라(2-5 보상 공개 연출) 갤러리 썸네일만으로는 모자라다 — 크게 볼 자리를 둔다.
    ///
    /// 넘기기는 두 길이다 — 좌우로 밀기(드래그)와 양옆 버튼. 버튼은 밀기를 모르는 사람을 위해 남겨 둔다.
    /// 끝에서 더 밀면 반대쪽으로 돌아가지 않는다 — 갤러리 순서(맵 · 웨이브)가 의미를 가져서 처음으로 튀면 헷갈린다.
    ///
    /// 여닫기는 CGameManager가 한다(2-7). 이 화면은 닫아 달라는 신호만 올린다.
    /// </summary>
    public class CUI_CardViewer : CUI, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary> 화면 너비의 이 비율보다 길게 밀어야 넘어간다. 짧게 스친 터치로 넘어가지 않게 </summary>
        public const float SWIPE_THRESHOLD_RATE = 0.08f;

        [SerializeField] private RawImage           m_imgPhoto;
        [SerializeField] private AspectRatioFitter  m_cFitter;      // 그림 비율을 지킨 채 화면에 맞춘다
        [SerializeField] private Text               m_txtCaption;
        [SerializeField] private Button             m_btnPrev;
        [SerializeField] private Button             m_btnNext;
        [SerializeField] private Button             m_btnClose;

        private readonly List<CCardViewEntry> m_lstEntry = new List<CCardViewEntry>();
        private int     m_iIndex;
        private Action  m_OnClose;
        private float   m_fDragStartX;

        public int INDEX => m_iIndex;
        public int COUNT => m_lstEntry.Count;

        #region Engine.CUI
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CUI_CardViewerDesc cDesc) == false)
            {
                Debug.LogError("[CUI_CardViewer] CUI_CardViewerDesc가 아닙니다.");
                return false;
            }

            m_lstEntry.Clear();
            if (cDesc.lstEntry != null)
                m_lstEntry.AddRange(cDesc.lstEntry);

            m_OnClose = cDesc.OnClose;

            Bind(m_btnPrev,  () => Show_Index(m_iIndex - 1));
            Bind(m_btnNext,  () => Show_Index(m_iIndex + 1));
            Bind(m_btnClose, () => m_OnClose?.Invoke());

            m_iIndex = -1;
            Show_Index(cDesc.iStartIndex);
            return true;
        }

        public override void Hide()
        {
            Unbind(m_btnPrev);
            Unbind(m_btnNext);
            Unbind(m_btnClose);

            if (m_imgPhoto != null)
                m_imgPhoto.texture = null;

            m_lstEntry.Clear();
            m_OnClose = null;
            base.Hide();
        }
        #endregion Engine.CUI

        #region 넘기기
        /// <summary> 범위를 벗어나면 끝에 붙는다. 같은 자리면 아무 일도 없다. </summary>
        public void Show_Index(int iIndex)
        {
            int iClamped = Clamp_Index(iIndex, m_lstEntry.Count);
            if (iClamped < 0 || iClamped == m_iIndex)
                return;

            m_iIndex = iClamped;
            CCardViewEntry cEntry = m_lstEntry[m_iIndex];

            Texture texture = CGameInstance.Instance != null ? CGameInstance.Instance.Get_Texture(cEntry.strTexName) : null;
            if (m_imgPhoto != null)
                m_imgPhoto.texture = texture;

            if (m_cFitter != null && texture != null && texture.height > 0)
                m_cFitter.aspectRatio = (float)texture.width / texture.height;

            if (m_txtCaption != null)
                m_txtCaption.text = $"{cEntry.strCaption}\n{m_iIndex + 1} / {m_lstEntry.Count}";

            // 끝에서는 그쪽 버튼을 감춘다 — 눌러도 아무 일이 없는 버튼은 고장으로 보인다.
            if (m_btnPrev != null)
                m_btnPrev.gameObject.SetActive(m_iIndex > 0);
            if (m_btnNext != null)
                m_btnNext.gameObject.SetActive(m_iIndex < m_lstEntry.Count - 1);
        }

        public void OnBeginDrag(PointerEventData cEventData) => m_fDragStartX = cEventData.position.x;

        // 끝났을 때만 판정한다. IDragHandler가 없으면 OnBeginDrag/OnEndDrag가 불리지 않아 비워 둔다.
        public void OnDrag(PointerEventData cEventData) { }

        public void OnEndDrag(PointerEventData cEventData)
        {
            int iStep = Get_SwipeStep(cEventData.position.x - m_fDragStartX, Screen.width);
            if (iStep != 0)
                Show_Index(m_iIndex + iStep);
        }
        #endregion 넘기기

        #region 판정 (화면 없이 테스트)
        /// <summary> 목록이 비었으면 -1, 아니면 0 ~ 개수-1로 붙인다. </summary>
        public static int Clamp_Index(int iIndex, int iCount)
            => iCount <= 0 ? -1 : Mathf.Clamp(iIndex, 0, iCount - 1);

        /// <summary>
        /// 민 거리(픽셀)로 넘길 방향을 정한다. 왼쪽으로 밀면(음수) 다음 장(+1), 오른쪽으로 밀면 이전 장(-1) —
        /// 사진 앱과 같은 방향이다. 문턱보다 짧으면 0.
        /// </summary>
        public static int Get_SwipeStep(float fDeltaX, float fScreenWidth)
        {
            float fThreshold = Mathf.Max(1f, fScreenWidth) * SWIPE_THRESHOLD_RATE;
            if (Mathf.Abs(fDeltaX) < fThreshold)
                return 0;

            return fDeltaX < 0f ? 1 : -1;
        }
        #endregion 판정 (화면 없이 테스트)

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
