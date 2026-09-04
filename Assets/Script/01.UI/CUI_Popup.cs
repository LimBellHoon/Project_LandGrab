using System;

using UnityEngine;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260904_공용 팝업 — 일시정지 / 결과 화면이 함께 쓴다
    /// <summary>
    /// 제목 · 본문 · 버튼 두 개가 전부다. 무엇을 보여줄지는 CUI_PopupDesc가 전부 정한다.
    /// 화면마다 클래스를 새로 만들면 프리팹도 같이 늘어나므로 하나로 돌려쓴다.
    ///
    /// 버튼은 EventSystem을 탄다 — 스테이지 선택 화면과 같은 경로다.
    /// (조이스틱만 Input을 직접 읽는다. 2-6-1 참고)
    /// </summary>
    public class CUI_Popup : CUI
    {
        [SerializeField] private Text   m_txtTitle;
        [SerializeField] private Text   m_txtBody;
        [SerializeField] private Button m_btnPrimary;
        [SerializeField] private Button m_btnSecondary;

        private Action m_OnPrimary;
        private Action m_OnSecondary;

        #region Engine.CUI
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CUI_PopupDesc cDesc) == false)
            {
                Debug.LogError("[CUI_Popup] CUI_PopupDesc가 아닙니다.");
                return false;
            }

            if (m_btnPrimary == null || m_btnSecondary == null)
            {
                Debug.LogError("[CUI_Popup] 프리팹에 버튼이 연결돼 있지 않습니다. "
                             + "Tools/LandGrab/Setup Assets 를 실행하세요.");
                return false;
            }

            m_OnPrimary   = cDesc.OnPrimary;
            m_OnSecondary = cDesc.OnSecondary;

            if (m_txtTitle != null)
                m_txtTitle.text = cDesc.strTitle;

            if (m_txtBody != null)
                m_txtBody.text = cDesc.strBody;

            Setup_Button(m_btnPrimary, string.IsNullOrEmpty(cDesc.strPrimary) ? "확인" : cDesc.strPrimary,
                         () => m_OnPrimary?.Invoke());

            // 보조 버튼은 쓸 일이 없으면 감춘다 (결과 화면처럼 버튼이 하나뿐인 경우).
            bool bUseSecondary = string.IsNullOrEmpty(cDesc.strSecondary) == false;
            m_btnSecondary.gameObject.SetActive(bUseSecondary);

            if (bUseSecondary == true)
                Setup_Button(m_btnSecondary, cDesc.strSecondary, () => m_OnSecondary?.Invoke());

            return true;
        }

        public override void Hide()
        {
            // 풀에 반납되므로 리스너와 콜백을 끊는다. 남겨 두면 다음 팝업이 옛 동작을 물고 온다.
            m_btnPrimary?.onClick.RemoveAllListeners();
            m_btnSecondary?.onClick.RemoveAllListeners();
            m_OnPrimary   = null;
            m_OnSecondary = null;

            base.Hide();
        }
        #endregion Engine.CUI

        private static void Setup_Button(Button cButton, string strLabel, UnityEngine.Events.UnityAction dgClick)
        {
            cButton.onClick.RemoveAllListeners();
            cButton.onClick.AddListener(dgClick);

            Text txtLabel = cButton.GetComponentInChildren<Text>();
            if (txtLabel != null)
                txtLabel.text = strLabel;
        }
    }
}
