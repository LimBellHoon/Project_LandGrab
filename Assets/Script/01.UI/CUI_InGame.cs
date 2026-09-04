using UnityEngine;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260904_인게임 HUD — 가상 조이스틱 + 진행 상황
    /// <summary>
    /// 조이스틱의 판정은 CVirtualJoystick이 하고 여기서는 그리기만 한다.
    ///
    /// Engine의 레이어가 UI까지 Tick하는지 확실하지 않아 Unity의 Update를 쓴다.
    /// 조이스틱이 안 그려지면 손가락이 어디를 잡았는지 보이지 않아 조작 자체가 불가능해지므로,
    /// 여기서만은 확실히 도는 쪽을 골랐다.
    /// </summary>
    public class CUI_InGame : CUI
    {
        private const float HANDLE_RATIO = 0.45f;   // 손잡이 지름 / 조이스틱 지름

        [SerializeField] private RectTransform  m_trJoystickBase;
        [SerializeField] private RectTransform  m_trJoystickHandle;
        [SerializeField] private Text           m_txtStatus;

        private CPlayer         m_cPlayer;
        private CStage_Manager  m_cStage;
        private Canvas          m_cCanvas;

        #region Engine.CUI
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CUI_InGameDesc cDesc) == false)
            {
                Debug.LogError("[CUI_InGame] CUI_InGameDesc가 아닙니다.");
                return false;
            }

            if (m_trJoystickBase == null || m_trJoystickHandle == null)
            {
                Debug.LogError("[CUI_InGame] 프리팹에 조이스틱이 연결돼 있지 않습니다. "
                             + "Tools/LandGrab/Setup Assets 를 실행하세요.");
                return false;
            }

            m_cPlayer = cDesc.cPlayer;
            m_cStage  = cDesc.cStage;
            m_cCanvas = GetComponentInParent<Canvas>();

            Show_Joystick(false);
            return true;
        }

        public override void Hide()
        {
            m_cPlayer = null;
            m_cStage  = null;
            base.Hide();
        }
        #endregion Engine.CUI

        private void Update()
        {
            Refresh_Joystick();
            Refresh_Status();
        }

        private void Refresh_Joystick()
        {
            CVirtualJoystick cJoystick = m_cPlayer != null ? m_cPlayer.JOYSTICK : null;

            if (cJoystick == null || cJoystick.IS_ACTIVE == false)
            {
                Show_Joystick(false);
                return;
            }

            Show_Joystick(true);

            // 캔버스가 ScreenSpaceOverlay라 RectTransform.position에 화면 픽셀을 그대로 넣을 수 있다.
            // 반면 크기(sizeDelta)는 캔버스 단위라 스케일로 나눠 줘야 실제 반경과 맞는다.
            float fScale    = m_cCanvas != null && m_cCanvas.scaleFactor > 0f ? m_cCanvas.scaleFactor : 1f;
            float fDiameter = cJoystick.RADIUS * 2f / fScale;

            m_trJoystickBase.position    = cJoystick.ORIGIN;
            m_trJoystickBase.sizeDelta   = Vector2.one * fDiameter;
            m_trJoystickHandle.position  = cJoystick.HANDLE;
            m_trJoystickHandle.sizeDelta = Vector2.one * (fDiameter * HANDLE_RATIO);
        }

        private void Show_Joystick(bool bShow)
        {
            if (m_trJoystickBase.gameObject.activeSelf != bShow)
                m_trJoystickBase.gameObject.SetActive(bShow);

            if (m_trJoystickHandle.gameObject.activeSelf != bShow)
                m_trJoystickHandle.gameObject.SetActive(bShow);
        }

        private void Refresh_Status()
        {
            if (m_txtStatus == null || m_cStage == null)
                return;

            m_txtStatus.text = $"{m_cStage.MAP_NAME}  {m_cStage.WAVE}/{m_cStage.WAVE_COUNT}웨이브"
                             + $"   {m_cStage.OWNED_RATIO:P0} / {m_cStage.CLEAR_RATIO:P0}"
                             + $"   ♥{m_cStage.LIFE}   {m_cStage.REMAIN_TIME:F0}s";
        }
    }
}
