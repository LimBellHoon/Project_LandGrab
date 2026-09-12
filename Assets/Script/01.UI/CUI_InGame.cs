using System;

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
        // 260912_점령률 게이지. 숫자만으로는 얼마나 남았는지 한눈에 안 들어온다.
        [SerializeField] private Image          m_imgProgress;
        [SerializeField] private Text           m_txtTime;
        [SerializeField] private Button         m_btnPause;
        // 260905_액티브 스킬 버튼
        [SerializeField] private Button         m_btnSkill;
        [SerializeField] private Image          m_imgSkillCool;
        [SerializeField] private Text           m_txtSkill;
        // 260905_소모품 버튼
        [SerializeField] private Button         m_btnItem;
        [SerializeField] private Text           m_txtItem;

        private CPlayer         m_cPlayer;
        private CStage_Manager  m_cStage;
        private CProgress_Manager  m_cProgress;
        private CCSVData_EquipInfo m_cEquipTable;
        private Action             m_OnUseItem;
        private Canvas          m_cCanvas;
        private Action          m_OnPause;

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
            m_cProgress   = cDesc.cProgress;
            m_cEquipTable = cDesc.cEquipTable;
            m_OnUseItem   = cDesc.OnUseItem;
            m_OnPause = cDesc.OnPause;
            m_cCanvas = GetComponentInParent<Canvas>();

            // 260904_일시정지 버튼. 조이스틱이 화면 아래 60%만 잡으므로 위쪽은 버튼 자리다.
            if (m_btnPause != null)
            {
                m_btnPause.onClick.RemoveAllListeners();
                m_btnPause.onClick.AddListener(() => m_OnPause?.Invoke());
            }

            // 260905_스킬 / 소모품 버튼도 여기서 연결한다.
            // 풀에서 재사용될 때 Initialize가 다시 불리므로 RemoveAllListeners를 먼저 해야
            // 같은 리스너가 쌓여 한 번 눌러도 여러 번 발동하는 일이 없다.
            if (m_btnSkill != null)
            {
                m_btnSkill.onClick.RemoveAllListeners();
                m_btnSkill.onClick.AddListener(On_ClickSkill);
            }

            if (m_btnItem != null)
            {
                m_btnItem.onClick.RemoveAllListeners();
                m_btnItem.onClick.AddListener(On_ClickItem);
            }

            Show_Joystick(false);
            return true;
        }

        public override void Hide()
        {
            m_btnPause?.onClick.RemoveAllListeners();
            m_btnSkill?.onClick.RemoveAllListeners();
            m_btnItem?.onClick.RemoveAllListeners();

            m_cPlayer = null;
            m_cStage  = null;
            m_cProgress   = null;
            m_cEquipTable = null;
            m_OnUseItem   = null;
            m_OnPause = null;
            base.Hide();
        }
        #endregion Engine.CUI

        private void Update()
        {
            Refresh_Joystick();
            Refresh_Status();
            Refresh_Progress();
            Refresh_Skill();
            Refresh_Item();
        }

        /// <summary> 일시정지 중에는 조이스틱을 감춘다 (입력도 어차피 멈춰 있다). </summary>
        public void Set_Interactable(bool bInteractable)
        {
            if (m_btnPause != null)
                m_btnPause.interactable = bInteractable;

            // 260905_일시정지 중에는 스킬도 막는다.
            if (m_btnSkill != null)
                m_btnSkill.interactable = bInteractable;

            if (m_btnItem != null)
                m_btnItem.interactable = bInteractable;

            if (bInteractable == false)
                Show_Joystick(false);
        }

        private void Refresh_Joystick()
        {
            CVirtualJoystick cJoystick = m_cPlayer != null ? m_cPlayer.JOYSTICK : null;
            bool bPaused = m_cStage != null && m_cStage.IS_PAUSED;

            if (cJoystick == null || cJoystick.IS_ACTIVE == false || bPaused == true)
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

        // 260905_스킬 버튼
        private void On_ClickSkill()
        {
            m_cStage?.Try_UseSkill();
        }

        // 쿼타임 덮개를 채워 언제 다시 쓸 수 있는지 보여 준다.
        // 스킬이 없는 상태면 버튼 자체를 숨긴다 — 누를 수 없는 버튼은 혼란만 준다.
        private void Refresh_Skill()
        {
            if (m_btnSkill == null)
                return;

            CSkillHandler cSkill = m_cPlayer != null ? m_cPlayer.SKILL : null;
            bool bHas = cSkill != null && cSkill.HAS_SKILL == true;

            if (m_btnSkill.gameObject.activeSelf != bHas)
                m_btnSkill.gameObject.SetActive(bHas);

            if (bHas == false)
                return;

            if (m_imgSkillCool != null)
                m_imgSkillCool.fillAmount = cSkill.COOL_RATIO;

            if (m_txtSkill != null)
                m_txtSkill.text = cSkill.IS_READY == true
                                ? cSkill.INFO.strName
                                : $"{cSkill.REMAIN:F1}";
        }

        // 260905_소모품
        private void On_ClickItem()
        {
            m_OnUseItem?.Invoke();
        }

        // 갖고 있는 소모품이 없으면 버튼을 숨긴다 — 누를 수 없는 버튼은 혼란만 준다.
        // 장착 여부가 아니라 보유 기준이다(CProgress_Manager.Get_BattleConsumable).
        private void Refresh_Item()
        {
            if (m_btnItem == null)
                return;

            CEquipInfo cInfo = m_cProgress != null ? m_cProgress.Get_BattleConsumable() : null;
            int iCount = cInfo != null ? m_cProgress.Get_ItemCount(cInfo.iEquipID) : 0;
            bool bShow = cInfo != null && iCount > 0;

            if (m_btnItem.gameObject.activeSelf != bShow)
                m_btnItem.gameObject.SetActive(bShow);

            if (bShow == false || m_txtItem == null)
                return;

            m_txtItem.text = $"{cInfo.strName}\n{iCount}";
        }


        // 260912_게이지는 '목표 대비 얼마나 왔나'로 채운다.
        // 점령률 그대로 채우면 목표가 70%일 때 다 찬 것처럼 보이지 않아 답답하다.
        private void Refresh_Progress()
        {
            if (m_cStage == null)
                return;

            if (m_imgProgress != null)
            {
                float fClear = Mathf.Max(0.01f, m_cStage.CLEAR_RATIO);
                m_imgProgress.fillAmount = Mathf.Clamp01(m_cStage.OWNED_RATIO / fClear);
            }

            if (m_txtTime != null)
                m_txtTime.text = Format_Time(m_cStage.REMAIN_TIME);
        }

        /// <summary> 남은 시간. 분:초가 초 단위 숫자보다 남은 양이 빨리 읽힌다. </summary>
        public static string Format_Time(float fSeconds)
        {
            int iTotal = Mathf.Max(0, Mathf.CeilToInt(fSeconds));
            return $"{iTotal / 60:00}:{iTotal % 60:00}";
        }


        private void Refresh_Status()
        {
            if (m_txtStatus == null || m_cStage == null)
                return;

            m_txtStatus.text = $"{m_cStage.WAVE}/{m_cStage.WAVE_COUNT} 웨이브"
                             + $"   {m_cStage.OWNED_RATIO:P0} / {m_cStage.CLEAR_RATIO:P0}"
                             + $"   ♥{m_cStage.LIFE}";
        }
    }
}
