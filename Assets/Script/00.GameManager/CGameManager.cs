using System;

using UnityEngine;

using Engine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 진입점 (Engine 초기화 + Tick 펌프)
    // 260904_스테이지 선택 → 플레이 → 결과 → 선택 흐름
    /// <summary>
    /// Portfolio_SoloLeveling의 CGameManager와 같은 역할.
    /// Engine을 초기화하고 매 프레임 Tick을 흘려보낸 뒤, 클라이언트 매니저를 구동한다.
    ///
    /// 화면 흐름은 여기서만 갈아탄다 — 스테이지도 UI도 서로를 모른다.
    /// </summary>
    public class CGameManager : SingletonBase_MonoBehaviour<CGameManager>
    {
        // 260904_UI 프리팹 이름은 Engine이 정한다 — 우리가 Desc에 적어도 덮어쓴다.
        // Engine.CUI_Manager.Open<T>가 첫 줄에서 이렇게 한다:
        //     cUIDesc.strPrefabName = "Prefab_" + Engine_Utility.Convert_TypeToString<T>();
        // Convert_TypeToString은 typeof(T).Name에서 맨 앞 'C'만 떼어낸다.
        // 따라서 CUI_StageSelect → "Prefab_UI_StageSelect"가 강제된다.
        // 여기 상수는 Has_Prefab 사전 검사에만 쓰므로 그 규칙과 반드시 같아야 한다.
        private const string PREFAB_UI_STAGE_SELECT = "Prefab_UI_StageSelect";
        private const string PREFAB_UI_INGAME       = "Prefab_UI_InGame";
        private const string PREFAB_UI_POPUP        = "Prefab_UI_Popup";

        // 필드 이름을 바꾸면 씬에 저장된 참조가 끊긴다 — 이름은 그대로 두고 역할만 정리했다.
        // m_srBackground = 점령하면 드러날 이미지(reveal), m_srOverlay = 그 위를 덮는 가림막(cover).
        [SerializeField] private CStageDesc     m_cStageDesc = new CStageDesc();
        [SerializeField] private SpriteRenderer m_srBackground;     // 드러날 보상 이미지
        [SerializeField] private SpriteRenderer m_srOverlay;        // 미점령 영역을 덮는 가림막

        // 260904_UI 캔버스. Engine이 UI를 붙일 자리를 알아야 한다.
        [Header("UI Canvas")]
        [SerializeField] private Transform m_trUIField;
        [SerializeField] private Transform m_trUIMain;
        [SerializeField] private Transform m_trUIPopup;

        [Header("Debug")]
        [Tooltip("켜면 해금 규칙을 무시하고 모든 맵을 고를 수 있다.")]
        [SerializeField] private bool m_bDebugUnlockAll;

        private CGameInstance       m_cGameInstance;
        private CStage_Manager      m_cStageManager;
        private CProgress_Manager   m_cProgressManager;

        private CCSVData_MapInfo    m_cMapTable;
        private CCSVData_EnemyInfo  m_cEnemyTable;
        private CCSVData_UpgradeInfo m_cUpgradeTable;   // 260905_능력치 강화 표
        private CCSVData_SkillInfo  m_cSkillTable;      // 260905_스킬 표
        private CUI                 m_cStageSelectUI;
        private CUI                 m_cInGameUI;
        private CUI                 m_cPopupUI;
        private bool                m_bReady;
        private bool                m_bLastCleared;
        // 260905_결과 화면에 넘길 별 정보
        private int                 m_iLastStar;
        private int                 m_iLastMaxStar;
        private bool                m_bLastNewRecord;
        private int                 m_iLastCoin;

        public static CStage_Manager    STAGE_MANAGER    => instance.m_cStageManager;
        public static CProgress_Manager PROGRESS_MANAGER => instance.m_cProgressManager;

        #region Unity
        public void Start()
        {
            /* 엔진 초기화 */
            m_cGameInstance = CGameInstance.Instance;
            if (m_cGameInstance.Initialize_Engine() == false)
            {
                Debug.LogError("[CGameManager] Engine 초기화 실패");
                return;
            }

            /* 클라이언트 매니저 초기화 */
            m_cStageManager    = new CStage_Manager();
            m_cProgressManager = new CProgress_Manager();

            GameLogic_Async();
        }

        public void OnDestroy()
        {
            CancelInvoke();
            m_cStageManager?.Release();
            m_cGameInstance?.Release_Engine();
        }

        public void FixedUpdate()
        {
            if (m_bReady == false)
                return;

            m_cGameInstance.FixedTick();
        }

        public void Update()
        {
            if (m_bReady == false)
                return;

            m_cGameInstance.Tick();
            m_cStageManager.Tick(Get_LayerDeltaTime(OBJECT_TYPE.DEFAULT));
        }

        public void LateUpdate()
        {
            if (m_bReady == false)
                return;

            m_cGameInstance.LateTick();
        }

        // 260904_전화가 오거나 홈으로 나가면 게임이 계속 돌아가면 안 된다.
        // 돌아올 때 자동으로 풀지는 않는다 — 갑자기 움직이면 그대로 죽는다.
        public void OnApplicationPause(bool bPause)
        {
            if (bPause == true && m_bReady == true)
                Pause_Stage();
        }
        #endregion Unity

        #region TIME_MANAGER
        public static float Get_LayerDeltaTime(OBJECT_TYPE eObjectType) => CGameInstance.Instance.Get_LayerDeltaTime(eObjectType);
        public static void Set_LayerTimeScale(OBJECT_TYPE eObjectType, float fLocalTimeScale) => CGameInstance.Instance.Set_LayerTimeScale(eObjectType, fLocalTimeScale);
        #endregion TIME_MANAGER

        #region 초기 구동
        // 260904_스테이지 규칙은 전부 CSV에서 온다.
        // CSV 라벨이 붙은 TextAsset은 Engine이 파일명으로 Client.CCSVData_<파일명> 클래스를 찾아
        // 자동으로 파싱해 캐싱한다 — 그래서 여기서는 라벨만 읽어 오면 된다.
        private async void GameLogic_Async()
        {
            try
            {
                // 에셋 메모리 로드 (Addressable 라벨)
                await m_cGameInstance.LoadAssetAsync(CAddressableLabel.PREFAB);
                await m_cGameInstance.LoadAssetAsync(CAddressableLabel.TEXTURE);
                await m_cGameInstance.LoadAssetAsync(CAddressableLabel.CSV);

                if (Check_AssetsReady() == false)
                    return;

                if (Load_Table() == false)
                    return;

                if (m_cProgressManager.Initialize(m_cMapTable, new CStageProgress_Local()) == false)
                    return;

                m_cProgressManager.Set_UnlockAll(m_bDebugUnlockAll);

                m_cGameInstance.Set_UICanvas(m_trUIField, m_trUIMain, m_trUIPopup);

                m_bReady = true;
                Open_StageSelect();
            }
            catch (Exception e)
            {
                Debug.LogError($"[CGameManager] 초기화 중 예외 : {e}");
            }
        }

        // 260904_Setup Assets를 한 번도 안 돌린 클론에서 Play를 누르면
        // Addressable이 'No Location found for Key=...' 예외를 라벨마다 토해내고,
        // 그 뒤에 CSV를 못 읽었다는 메시지가 따라붙어 원인이 CSV처럼 보인다.
        // 실제 원인은 라벨 자체가 없다는 것이므로, 그 사실을 먼저 한 줄로 말해 준다.
        // 프리팹은 Addressable을 거쳐 들어오므로 하나만 물어봐도 라벨이 살아 있는지 알 수 있다.
        private bool Check_AssetsReady()
        {
            if (m_cGameInstance.Has_Prefab(PREFAB_UI_STAGE_SELECT) == true)
                return true;

            Debug.LogError($"[CGameManager] Addressable에 '{PREFAB_UI_STAGE_SELECT}'가 없습니다 — "
                         + "에셋이 아직 만들어지지 않았습니다. "
                         + "Unity 메뉴 Tools/LandGrab/Setup Assets 를 먼저 실행하세요. "
                         + "(스프라이트 · UI 프리팹 · Addressable 라벨을 한꺼번에 만듭니다)");
            return false;
        }

        /// <summary>
        /// 표가 없으면 스테이지를 띄울 수 없다. 무엇이 없는지 정확히 알려 준다 —
        /// 거의 항상 Addressable 라벨(CSV)이 안 붙었거나 파일명과 클래스명이 어긋난 경우다.
        /// </summary>
        private bool Load_Table()
        {
            m_cMapTable   = m_cGameInstance.Get_CSVData(CCSVData_MapInfo.CSV_KEY) as CCSVData_MapInfo;
            m_cEnemyTable = m_cGameInstance.Get_CSVData(CCSVData_EnemyInfo.CSV_KEY) as CCSVData_EnemyInfo;

            if (m_cMapTable == null)
            {
                Debug.LogError("[CGameManager] MapInfo.csv를 읽지 못했습니다. "
                             + $"Assets/Data/MapInfo.csv에 Addressable 라벨 '{CAddressableLabel.CSV}'가 "
                             + "붙었는지, 클래스 이름이 CCSVData_MapInfo인지 확인하세요.");
                return false;
            }

            // 260905_강화 표는 없어도 게임은 돌아간다(강화가 전부 0레벨일 뿐).
            // 260905_스킬 표도 없으면 스킬 없이 진행한다.
            m_cSkillTable = m_cGameInstance.Get_CSVData(CCSVData_SkillInfo.CSV_KEY) as CCSVData_SkillInfo;

            m_cUpgradeTable = m_cGameInstance.Get_CSVData(CCSVData_UpgradeInfo.CSV_KEY) as CCSVData_UpgradeInfo;
            if (m_cUpgradeTable == null)
            {
                Debug.LogWarning("[CGameManager] UpgradeInfo.csv를 읽지 못해 능력치 강화 없이 진행합니다. "
                               + $"Addressable 라벨 '{CAddressableLabel.CSV}'가 붙었는지 확인하세요.");
            }

            if (m_cEnemyTable == null)
            {
                Debug.LogError("[CGameManager] EnemyInfo.csv를 읽지 못했습니다. "
                             + $"Assets/Data/EnemyInfo.csv에 Addressable 라벨 '{CAddressableLabel.CSV}'가 "
                             + "붙었는지, 클래스 이름이 CCSVData_EnemyInfo인지 확인하세요.");
                return false;
            }

            return true;
        }
        #endregion 초기 구동

        #region 화면 흐름
        // 260904_선택 → 플레이 → 결과 → 선택. 갈아타는 지점을 여기 한곳에 모아 둔다.
        private void Open_StageSelect()
        {
            if (m_cStageSelectUI != null)
            {
                // 이미 떠 있으면 해금 상태만 다시 반영한다.
                (m_cStageSelectUI as CUI_StageSelect)?.Refresh_List();
                return;
            }

            if (m_cGameInstance.Has_Prefab(PREFAB_UI_STAGE_SELECT) == false)
            {
                Debug.LogError($"[CGameManager] '{PREFAB_UI_STAGE_SELECT}' 프리팹이 없습니다. "
                             + "Tools/LandGrab/Setup Assets 를 실행하세요.");
                return;
            }

            // strPrefabName은 Engine이 T로 채우므로 여기서 적지 않는다(위 상수 주석 참고).
            CUI_StageSelectDesc cDesc = new CUI_StageSelectDesc
            {
                eObjectType     = OBJECT_TYPE.UI_MAIN,
                cMapTable       = m_cMapTable,
                cProgress       = m_cProgressManager,
                OnSelect        = Start_Stage,
            };

            m_cStageSelectUI = m_cGameInstance.Open_UI<CUI_StageSelect>(cDesc, m_trUIMain);
        }

        private void Close_StageSelect()
        {
            if (m_cStageSelectUI == null)
                return;

            m_cGameInstance.Close_UI(m_cStageSelectUI);
            m_cStageSelectUI = null;
        }

        private void Start_Stage(int iMapID)
        {
            CMapInfo cMapInfo = m_cMapTable.Get_Info(iMapID);
            if (cMapInfo == null)
                return;

            if (m_cProgressManager.Is_Unlocked(iMapID) == false)
            {
                Debug.LogWarning($"[CGameManager] 맵 {iMapID}는 아직 잠겨 있습니다.");
                return;
            }

            Close_StageSelect();

            // 이전 스테이지가 남아 있을 수 있다 — 완전히 정리하고 새로 깐다.
            m_cStageManager.Release();

            if (m_cStageManager.Initialize(cMapInfo, m_cEnemyTable, m_srOverlay, m_srBackground) == false)
            {
                Open_StageSelect();
                return;
            }

            // 260905_강화 수치를 반영한다. 표가 없으면 전부 0이라 강화 없는 상태가 된다.
            m_cStageManager.Set_PlayerUpgrade(
                1f + m_cProgressManager.Get_UpgradeValue(m_cUpgradeTable, UPGRADE_TYPE.SPEED),
                m_cProgressManager.Get_UpgradeValue(m_cUpgradeTable, UPGRADE_TYPE.EVASION),
                Mathf.RoundToInt(m_cProgressManager.Get_UpgradeValue(m_cUpgradeTable, UPGRADE_TYPE.HP)));

            // 260905_장착 시스템이 없으므로 지금은 표에서 워프를 골라 넣는다.
            m_cStageManager.Set_PlayerSkill(m_cSkillTable != null
                                          ? m_cSkillTable.Find_ByType(SKILL_TYPE.WARP) : null);

            m_cStageManager.OnStateChanged += On_StageStateChanged;

            if (m_cStageManager.Start_Stage() == false)
            {
                Open_StageSelect();
                return;
            }

            m_cProgressManager.Set_LastMap(iMapID);
            m_cStageDesc.iMapID = iMapID;

            Open_InGameUI();
        }

        // 260904_인게임 HUD (가상 조이스틱). 스테이지가 있는 동안만 떠 있다.
        private void Open_InGameUI()
        {
            Close_InGameUI();

            if (m_cGameInstance.Has_Prefab(PREFAB_UI_INGAME) == false)
            {
                Debug.LogWarning($"[CGameManager] '{PREFAB_UI_INGAME}' 프리팹이 없어 조이스틱 없이 진행합니다. "
                               + "Tools/LandGrab/Setup Assets 를 실행하세요.");
                return;
            }

            CUI_InGameDesc cDesc = new CUI_InGameDesc
            {
                eObjectType     = OBJECT_TYPE.UI_FIELD,
                cPlayer         = m_cStageManager.PLAYER,
                cStage          = m_cStageManager,
                OnPause         = Pause_Stage,
            };

            m_cInGameUI = m_cGameInstance.Open_UI<CUI_InGame>(cDesc, m_trUIField);
        }

        private void Close_InGameUI()
        {
            if (m_cInGameUI == null)
                return;

            m_cGameInstance.Close_UI(m_cInGameUI);
            m_cInGameUI = null;
        }

        // 260905_클리어 기준이 '전 웨이브 완주'에서 '웨이브 하나 이상 달성'으로 바뀌었다.
        // STAGE_STATE는 판이 어떻게 끝났는지(완주/시간·목숨 소진)를 말할 뿐이고,
        // 해금과 기록은 별 개수가 정한다 — 2웨이브에서 죽어도 1웨이브를 깼으면 클리어다.
        private void On_StageStateChanged(STAGE_STATE eState)
        {
            if (eState != STAGE_STATE.CLEAR && eState != STAGE_STATE.FAIL)
                return;

            m_iLastStar    = m_cStageManager.STAR;
            m_iLastMaxStar = m_cStageManager.WAVE_COUNT;
            m_bLastCleared = m_iLastStar >= 1;

            // 260905_재화는 '새로 딴 별'만큼만 준다 — 안 그러면 쉬운 판을 반복해 무한히 벌 수 있다.
            int iGainedStar  = m_bLastCleared == true
                             ? m_cProgressManager.Set_Star(m_cStageManager.MAP_ID, m_iLastStar) : 0;
            m_bLastNewRecord = iGainedStar > 0;
            m_iLastCoin      = iGainedStar * Get_CoinPerStar(m_cStageManager.MAP_ID);

            if (m_iLastCoin > 0)
                m_cProgressManager.Add_Coin(m_iLastCoin);

            // 260904_클리어는 드러난 보상을 조금 더 보여준 뒤 결과를 띄운다.
            // 실패는 굳이 끌 이유가 없어 빨리 띄운다.
            Invoke(nameof(Show_Result), m_bLastCleared == true ? RESULT_HOLD_CLEAR : RESULT_HOLD_FAIL);
        }

        private const float RESULT_HOLD_CLEAR = 1.4f;
        private const float RESULT_HOLD_FAIL  = 0.8f;

        // 260904_결과 화면. 일시정지와 같은 팝업 프리팹을 쓴다.
        // 260905_별을 결과의 주인공으로 둔다. 별 0개일 때만 '실패'다.
        // 260905_별당 코인은 맵마다 다르다(MapInfo.csv).
        private int Get_CoinPerStar(int iMapID)
        {
            CMapInfo cMapInfo = m_cMapTable != null ? m_cMapTable.Get_Info(iMapID) : null;
            return cMapInfo != null ? cMapInfo.iCoinPerStar : 0;
        }

        private void Show_Result()
        {
            string strBody = $"{m_cStageManager.MAP_NAME}\n"
                           + $"{CStar_Utility.Get_Text(m_iLastStar, m_iLastMaxStar)}\n"
                           + $"{m_iLastStar} / {m_iLastMaxStar} 웨이브"
                           + $"    점령률 {m_cStageManager.OWNED_RATIO:P0}";

            if (m_bLastNewRecord == true && m_iLastCoin > 0)
                strBody += $"\n신기록!  +{m_iLastCoin} 코인   (보유 {m_cProgressManager.COIN})";

            Open_Popup(new CUI_PopupDesc
            {
                strTitle    = m_bLastCleared == true ? "클리어!" : "실패",
                strBody     = strBody,
                strPrimary  = "확인",
                OnPrimary   = Return_ToStageSelect,
            });
        }

        // 260904_일시정지. 액터를 세우는 것은 스테이지가, 화면은 여기가 맡는다.
        private void Pause_Stage()
        {
            if (m_cStageManager.STATE != STAGE_STATE.PLAYING || m_cStageManager.IS_PAUSED == true)
                return;

            m_cStageManager.Set_Pause(true);
            (m_cInGameUI as CUI_InGame)?.Set_Interactable(false);

            Open_Popup(new CUI_PopupDesc
            {
                strTitle     = "일시정지",
                strBody      = $"{m_cStageManager.MAP_NAME}    "
                             + $"{m_cStageManager.WAVE} / {m_cStageManager.WAVE_COUNT} 웨이브",
                strPrimary   = "계속하기",
                strSecondary = "나가기",
                OnPrimary    = Resume_Stage,
                OnSecondary  = Return_ToStageSelect,
            });
        }

        private void Resume_Stage()
        {
            Close_Popup();
            (m_cInGameUI as CUI_InGame)?.Set_Interactable(true);
            m_cStageManager.Set_Pause(false);
        }

        private void Open_Popup(CUI_PopupDesc cDesc)
        {
            Close_Popup();

            if (m_cGameInstance.Has_Prefab(PREFAB_UI_POPUP) == false)
            {
                Debug.LogError($"[CGameManager] '{PREFAB_UI_POPUP}' 프리팹이 없습니다. "
                             + "Tools/LandGrab/Setup Assets 를 실행하세요.");
                return;
            }

            cDesc.eObjectType = OBJECT_TYPE.UI_POPUP;

            m_cPopupUI = m_cGameInstance.Open_UI<CUI_Popup>(cDesc, m_trUIPopup);
        }

        private void Close_Popup()
        {
            if (m_cPopupUI == null)
                return;

            m_cGameInstance.Close_UI(m_cPopupUI);
            m_cPopupUI = null;
        }

        private void Return_ToStageSelect()
        {
            CancelInvoke();
            Close_Popup();
            Close_InGameUI();
            m_cStageManager.Release();
            Open_StageSelect();
        }
        #endregion 화면 흐름
    }
}
