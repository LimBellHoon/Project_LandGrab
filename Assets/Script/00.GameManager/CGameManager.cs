using System;

using UnityEngine;

using Engine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 진입점 (Engine 초기화 + Tick 펌프)
    /// <summary>
    /// Portfolio_SoloLeveling의 CGameManager와 같은 역할.
    /// Engine을 초기화하고 매 프레임 Tick을 흘려보낸 뒤, 클라이언트 매니저를 구동한다.
    /// </summary>
    public class CGameManager : SingletonBase_MonoBehaviour<CGameManager>
    {
        // 필드 이름을 바꾸면 씬에 저장된 참조가 끊긴다 — 이름은 그대로 두고 역할만 정리했다.
        // m_srBackground = 점령하면 드러날 이미지(reveal), m_srOverlay = 그 위를 덮는 가림막(cover).
        [SerializeField] private CStageDesc     m_cStageDesc = new CStageDesc();
        [SerializeField] private SpriteRenderer m_srBackground;     // 드러날 보상 이미지
        [SerializeField] private SpriteRenderer m_srOverlay;        // 미점령 영역을 덮는 가림막

        private CGameInstance   m_cGameInstance;
        private CStage_Manager  m_cStageManager;
        private bool            m_bReady;

        public static CStage_Manager STAGE_MANAGER => instance.m_cStageManager;

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
            m_cStageManager = new CStage_Manager();

            GameLogic_Async();
        }

        public void OnDestroy()
        {
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
        #endregion Unity

        #region TIME_MANAGER
        public static float Get_LayerDeltaTime(OBJECT_TYPE eObjectType) => CGameInstance.Instance.Get_LayerDeltaTime(eObjectType);
        public static void Set_LayerTimeScale(OBJECT_TYPE eObjectType, float fLocalTimeScale) => CGameInstance.Instance.Set_LayerTimeScale(eObjectType, fLocalTimeScale);
        #endregion TIME_MANAGER

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

                if (Load_Table(out CCSVData_MapInfo cMapTable, out CCSVData_EnemyInfo cEnemyTable) == false)
                    return;

                CMapInfo cMapInfo = cMapTable.Get_Info(m_cStageDesc.iMapID);
                if (cMapInfo == null)
                    return;

                if (m_cStageManager.Initialize(cMapInfo, cEnemyTable, m_srOverlay, m_srBackground) == false)
                    return;

                if (m_cStageManager.Start_Stage() == false)
                    return;

                m_bReady = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[CGameManager] 초기화 중 예외 : {e}");
            }
        }

        /// <summary>
        /// 표가 없으면 스테이지를 띄울 수 없다. 무엇이 없는지 정확히 알려 준다 —
        /// 거의 항상 Addressable 라벨(CSV)이 안 붙었거나 파일명과 클래스명이 어긋난 경우다.
        /// </summary>
        private bool Load_Table(out CCSVData_MapInfo cMapTable, out CCSVData_EnemyInfo cEnemyTable)
        {
            cMapTable   = m_cGameInstance.Get_CSVData(CCSVData_MapInfo.CSV_KEY) as CCSVData_MapInfo;
            cEnemyTable = m_cGameInstance.Get_CSVData(CCSVData_EnemyInfo.CSV_KEY) as CCSVData_EnemyInfo;

            if (cMapTable == null)
            {
                Debug.LogError("[CGameManager] MapInfo.csv를 읽지 못했습니다. "
                             + $"Assets/Data/MapInfo.csv에 Addressable 라벨 '{CAddressableLabel.CSV}'가 "
                             + "붙었는지, 클래스 이름이 CCSVData_MapInfo인지 확인하세요.");
                return false;
            }

            if (cEnemyTable == null)
            {
                Debug.LogError("[CGameManager] EnemyInfo.csv를 읽지 못했습니다. "
                             + $"Assets/Data/EnemyInfo.csv에 Addressable 라벨 '{CAddressableLabel.CSV}'가 "
                             + "붙었는지, 클래스 이름이 CCSVData_EnemyInfo인지 확인하세요.");
                return false;
            }

            return true;
        }
    }
}
