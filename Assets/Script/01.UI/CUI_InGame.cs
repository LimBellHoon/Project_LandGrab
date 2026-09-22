using System;
using System.Collections.Generic;

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
        // 260922_조이스틱은 반투명 — 누른 자리에 뜨므로 그 아래 맵(몬스터 · 선)을 가리면 안 된다
        private const float JOYSTICK_BASE_ALPHA   = 0.35f;
        private const float JOYSTICK_HANDLE_ALPHA = 0.6f;

        // 260916_화면 플래시 색. 피격/회피는 항상 같은 색이라 CGameConfig가 아니라 여기 상수로 둔다
        // (CEnemy의 기믹별 색과 같은 자리 — 세기·지속시간만 CGameConfig에서 조절한다).
        private static readonly Color COLOR_FLASH_HIT   = new Color(0.9f, 0.15f, 0.15f);
        private static readonly Color COLOR_FLASH_EVADE = Color.white;
        // 260918_전체 마비 — 번개 빛. 피격(빨강) · 회피(하양)와 섞이지 않는 색이다
        private static readonly Color COLOR_FLASH_MASS_STUN = new Color(1f, 0.92f, 0.35f);

        [SerializeField] private RectTransform  m_trJoystickBase;
        [SerializeField] private RectTransform  m_trJoystickHandle;
        [SerializeField] private Text           m_txtStatus;
        // 260912_점령률 게이지. 숫자만으로는 얼마나 남았는지 한눈에 안 들어온다.
        [SerializeField] private Image          m_imgProgress;
        [SerializeField] private Text           m_txtTime;
        // 260918_이번 판에서 점령으로 번 코인. 실제 보유 코인 반영은 스테이지가 끝날 때 CGameManager가 한다.
        [SerializeField] private Text           m_txtCoin;
        [SerializeField] private Button         m_btnPause;
        // 260905_액티브 스킬 버튼
        [SerializeField] private Button         m_btnSkill;
        [SerializeField] private Image          m_imgSkillCool;
        [SerializeField] private Text           m_txtSkill;
        // 260905_소모품 버튼
        [SerializeField] private Button         m_btnItem;
        [SerializeField] private Text           m_txtItem;
        // 260916_화면 전체를 덮는 플래시. 맨 위에 그려져야 하므로 계층상 맨 마지막 자식이다.
        [SerializeField] private Image          m_imgFlash;
        // 260921_시작 위치 슬롯이 도는 동안 깜빡이는 안내(2-3)
        [SerializeField] private Text           m_txtTapToStart;
        private const float TAP_BLINK_SPEED = 5f;

        // 260922_현란한 동작 글자("NEAR MISS!" 등). 꺼 둔 템플릿을 복제해 쓰고, 여럿이 뜨면 위로 쌓는다
        [SerializeField] private Text           m_txtCallout;
        private const int   CALLOUT_MAX       = 3;
        private const float CALLOUT_LIFE      = 1.0f;
        private const float CALLOUT_POP_TIME  = 0.14f;
        private const float CALLOUT_POP_SCALE = 1.7f;
        private const float CALLOUT_FADE_FROM = 0.65f;     // 이 시각부터 옅어진다
        private const float CALLOUT_GAP       = 96f;       // 쌓일 때 한 줄 간격
        private const float CALLOUT_RISE      = 40f;       // 사는 동안 떠오르는 거리
        // 색은 시각 정체성이라 설정이 아니라 여기 둔다(플래시 색과 같은 자리, 2-10-3)
        private static readonly Color COLOR_CALLOUT_NEAR  = new Color(0.45f, 0.95f, 1.00f);
        private static readonly Color COLOR_CALLOUT_CHAIN = new Color(1.00f, 0.70f, 0.20f);
        private static readonly Color COLOR_CALLOUT_TRAP  = new Color(1.00f, 0.35f, 0.45f);
        private static readonly Color COLOR_CALLOUT_BIG   = new Color(1.00f, 0.90f, 0.30f);

        private class CCallout
        {
            public Text  txt;
            public float fAge;
            public int   iSlot;
        }
        private readonly List<CCallout> m_lstCallout = new List<CCallout>();

        private CPlayer         m_cPlayer;
        private CStage_Manager  m_cStage;
        private CProgress_Manager  m_cProgress;
        private CCSVData_EquipInfo m_cEquipTable;
        private Action             m_OnUseItem;
        private Canvas          m_cCanvas;
        private Action          m_OnPause;
        // 260916_판정(CFlashEffect)과 적용(m_imgFlash)을 나눈다 — CCameraShake와 같은 이유다.
        private readonly CFlashEffect m_cFlash = new CFlashEffect();
        private CGameConfig            m_cConfig;

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
            m_cConfig = cDesc.cConfig;
            m_cCanvas = GetComponentInParent<Canvas>();
            Set_Alpha(m_trJoystickBase, JOYSTICK_BASE_ALPHA);       // 260922_반투명 조이스틱
            Set_Alpha(m_trJoystickHandle, JOYSTICK_HANDLE_ALPHA);

            // 260916_피격/회피 화면 플래시. 풀에서 재사용돼도 이전 판의 리스너가 남지 않도록
            // 먼저 끊고 다시 건다(스킬/소모품 버튼과 같은 이유).
            m_cFlash.Clear();
            if (m_cPlayer != null)
            {
                m_cPlayer.OnDamaged -= On_PlayerDamagedFlash;
                m_cPlayer.OnDamaged += On_PlayerDamagedFlash;
                m_cPlayer.OnEvade   -= On_PlayerEvadeFlash;
                m_cPlayer.OnEvade   += On_PlayerEvadeFlash;
            }

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

            // 260916_CPlayer가 아직 살아있는 채로 이 UI가 먼저 닫힐 수 있다(결과 화면 흐름) —
            // 여기서도 명시적으로 끊어야 숨겨진 UI가 다음 피격에 반응하는 사고가 안 난다.
            if (m_cPlayer != null)
            {
                m_cPlayer.OnDamaged -= On_PlayerDamagedFlash;
                m_cPlayer.OnEvade   -= On_PlayerEvadeFlash;
            }

            m_cPlayer = null;
            m_cStage  = null;
            m_cProgress   = null;
            m_cEquipTable = null;
            m_OnUseItem   = null;
            m_OnPause = null;
            m_cConfig = null;
            Clear_Callout();     // 260922_떠 있던 동작 글자를 지운다 — 풀로 돌아간 뒤 다음 판에 남지 않게
            base.Hide();
        }
        #endregion Engine.CUI

        private void Update()
        {
            Refresh_Joystick();
            Refresh_Status();
            Refresh_Progress();
            Refresh_Coin();
            Refresh_Skill();
            Refresh_Item();
            Refresh_Flash();
            Refresh_TapToStart();
            Refresh_Callout();
        }

        #region 260922_현란한 동작 글자
        /// <summary> 띄울 글자. 화면 없이 테스트할 수 있게 공개해 둔다. </summary>
        public static string Get_CalloutText(STYLE_ACTION eAction, int iCount)
        {
            switch (eAction)
            {
                case STYLE_ACTION.NEAR_MISS:   return "NEAR MISS!";
                case STYLE_ACTION.CHAIN:       return $"CHAIN ×{iCount}";
                case STYLE_ACTION.BIG_CAPTURE: return "HUGE!";
                case STYLE_ACTION.TRAP:
                    if (iCount <= 1) return "TRAP!";
                    if (iCount == 2) return "DOUBLE TRAP!";
                    if (iCount == 3) return "TRIPLE TRAP!";
                    return $"MULTI TRAP ×{iCount}";
                default: return string.Empty;
            }
        }

        private static Color Get_CalloutColor(STYLE_ACTION eAction)
        {
            switch (eAction)
            {
                case STYLE_ACTION.NEAR_MISS:   return COLOR_CALLOUT_NEAR;
                case STYLE_ACTION.CHAIN:       return COLOR_CALLOUT_CHAIN;
                case STYLE_ACTION.TRAP:        return COLOR_CALLOUT_TRAP;
                default:                       return COLOR_CALLOUT_BIG;
            }
        }

        /// <summary> 새 글자는 맨 아래 자리에 뜨고, 먼저 뜬 것들은 한 줄씩 위로 밀린다. 넘치면 가장 오래된 것을 지운다. </summary>
        public void Show_Callout(STYLE_ACTION eAction, int iCount)
        {
            if (m_txtCallout == null)
                return;

            if (m_lstCallout.Count >= CALLOUT_MAX)
            {
                Destroy(m_lstCallout[0].txt.gameObject);
                m_lstCallout.RemoveAt(0);
            }

            for (int i = 0; i < m_lstCallout.Count; ++i)
                ++m_lstCallout[i].iSlot;

            Text txt = Instantiate(m_txtCallout, m_txtCallout.transform.parent);
            txt.gameObject.SetActive(true);
            txt.text  = Get_CalloutText(eAction, iCount);
            txt.color = Get_CalloutColor(eAction);

            m_lstCallout.Add(new CCallout { txt = txt, fAge = 0f, iSlot = 0 });
        }

        // 톡 튀어나왔다가(크게 → 제 크기) 조금 떠오르며 옅어진다. 멈춤 · 카드 고르기 중에도 끝까지 보이게 unscaled 시간을 쓴다
        private void Refresh_Callout()
        {
            if (m_txtCallout == null)
                return;

            Vector2 vBase = m_txtCallout.rectTransform.anchoredPosition;

            for (int i = m_lstCallout.Count - 1; i >= 0; --i)
            {
                CCallout cCallout = m_lstCallout[i];
                cCallout.fAge += Time.unscaledDeltaTime;

                if (cCallout.fAge >= CALLOUT_LIFE || cCallout.txt == null)
                {
                    if (cCallout.txt != null)
                        Destroy(cCallout.txt.gameObject);
                    m_lstCallout.RemoveAt(i);
                    continue;
                }

                float fPop   = Mathf.Clamp01(cCallout.fAge / CALLOUT_POP_TIME);
                float fScale = Mathf.Lerp(CALLOUT_POP_SCALE, 1f, 1f - (1f - fPop) * (1f - fPop));
                cCallout.txt.rectTransform.localScale = Vector3.one * fScale;

                float fLife = cCallout.fAge / CALLOUT_LIFE;
                cCallout.txt.rectTransform.anchoredPosition = vBase + new Vector2(0f, cCallout.iSlot * CALLOUT_GAP + fLife * CALLOUT_RISE);

                Color cColor = cCallout.txt.color;
                cColor.a = cCallout.fAge < CALLOUT_FADE_FROM
                         ? 1f : 1f - Mathf.InverseLerp(CALLOUT_FADE_FROM, CALLOUT_LIFE, cCallout.fAge);
                cCallout.txt.color = cColor;
            }
        }

        private void Clear_Callout()
        {
            for (int i = 0; i < m_lstCallout.Count; ++i)
            {
                if (m_lstCallout[i].txt != null)
                    Destroy(m_lstCallout[i].txt.gameObject);
            }

            m_lstCallout.Clear();
        }
        #endregion 현란한 동작 글자

        // 260921_누르기를 기다리는 동안만 깜빡인다. 누른 뒤 멈추는 동안에는 지운다 — 이미 눌렀는데 또 누르라고 하면 헷갈린다
        private void Refresh_TapToStart()
        {
            if (m_txtTapToStart == null)
                return;

            bool bShow = m_cStage != null && m_cStage.IS_START_WAITING == true;
            if (m_txtTapToStart.gameObject.activeSelf != bShow)
                m_txtTapToStart.gameObject.SetActive(bShow);

            if (bShow == false)
                return;

            Color cColor = m_txtTapToStart.color;
            cColor.a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * TAP_BLINK_SPEED));
            m_txtTapToStart.color = cColor;
        }

        // 260916_피격/회피 화면 플래시
        private void On_PlayerDamagedFlash()
        {
            if (m_cConfig == null || m_cConfig.SCREEN_FLASH_ENABLED == false)
                return;

            Color cColor = COLOR_FLASH_HIT;
            cColor.a = m_cConfig.FLASH_HIT_ALPHA;
            m_cFlash.Add_Flash(cColor, m_cConfig.FLASH_HIT_DURATION);
        }

        private void On_PlayerEvadeFlash()
        {
            if (m_cConfig == null || m_cConfig.SCREEN_FLASH_ENABLED == false)
                return;

            Color cColor = COLOR_FLASH_EVADE;
            cColor.a = m_cConfig.FLASH_EVADE_ALPHA;
            m_cFlash.Add_Flash(cColor, m_cConfig.FLASH_EVADE_DURATION);
        }

        // 260918_전체 마비가 터졌다 — CGameManager가 흔들림 · 펀치와 함께 부른다(세계는 매니저, 화면은 UI, 2-10-3).
        public void Play_MassStunFlash()
        {
            if (m_cConfig == null || m_cConfig.SCREEN_FLASH_ENABLED == false)
                return;

            Color cColor = COLOR_FLASH_MASS_STUN;
            cColor.a = m_cConfig.FLASH_MASS_STUN_ALPHA;
            m_cFlash.Add_Flash(cColor, m_cConfig.FLASH_MASS_STUN_DURATION);
        }

        // 260918_목숨 표시 — 찬 칸이 남은 목숨, 빈 칸이 잃은 목숨
        // 260921_하트(♥)는 로비의 입장 재화가 됐다(2-23). 둘이 같은 모양이면 목숨이 하트를 깎는 것처럼 읽혀 ●/○로 바꿨다
        public static string Get_LifeText(int iLife, int iMaxLife)
        {
            int iMax = Mathf.Max(0, iMaxLife);
            int iCur = Mathf.Clamp(iLife, 0, iMax);
            return new string('●', iCur) + new string('○', iMax - iCur);
        }

        private void Refresh_Flash()
        {
            if (m_imgFlash == null)
                return;

            m_cFlash.Tick(Time.deltaTime);

            Color cColor = m_cFlash.COLOR;
            cColor.a *= m_cFlash.ALPHA;

            bool bShow = cColor.a > 0.001f;
            if (m_imgFlash.gameObject.activeSelf != bShow)
                m_imgFlash.gameObject.SetActive(bShow);

            if (bShow == true)
                m_imgFlash.color = cColor;
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

        private static void Set_Alpha(RectTransform trPart, float fAlpha)
        {
            Image cImage = trPart != null ? trPart.GetComponent<Image>() : null;
            if (cImage == null)
                return;

            Color cColor = cImage.color;
            cColor.a = fAlpha;
            cImage.color = cColor;
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
            m_cStage?.Request_Skill();
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

        // 260918_점령 재화. 매 프레임 갱신 외에 점령하는 그 순간에도 Play_CoinGainEffect가 즉시 불러 준다.
        private void Refresh_Coin()
        {
            if (m_txtCoin == null || m_cStage == null)
                return;

            m_txtCoin.text = m_cStage.STAGE_COIN.ToString();
        }

        // 260918_CGameManager가 점령마다 부른다. 파티클이 이 텍스트 쪽으로 촤르륵 날아가는 연출은
        // 프리팹이 있어야 해서 클라우드 세션에서는 못 만든다 — 숫자 갱신만 지금 넣고, 연출은
        // 로컬에서 파티클 프리팹을 만들어 이 함수 안에 이어 붙일 것(2-17의 캐릭터 프리팹 폴백과 같은 사정).
        public void Play_CoinGainEffect(int iAmount, Vector2 vWorldPos)
        {
            Refresh_Coin();
        }


        private void Refresh_Status()
        {
            if (m_txtStatus == null || m_cStage == null)
                return;

            // 260922_3지선다 트리거가 점유율 5% 단위로 되돌아가며(2-21) 조각 게이지 표시를 없앴다.
            // 점령률 자체가 다음 선택지가 열리는 기준이라 이 한 줄로 충분하다.
            m_txtStatus.text = $"{m_cStage.WAVE}/{m_cStage.WAVE_COUNT} 웨이브"
                             + $"   {m_cStage.OWNED_RATIO:P0} / {m_cStage.CLEAR_RATIO:P0}"
                             + $"   {CUI_InGame.Get_LifeText(m_cStage.LIFE, m_cStage.MAX_LIFE)}";
        }
    }
}
