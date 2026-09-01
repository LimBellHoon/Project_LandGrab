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
        [SerializeField] private CStageDesc     m_cStageDesc = new CStageDesc();
        [SerializeField] private SpriteRenderer m_srBackground;     // 뒤에 깔리는 보상 이미지
        [SerializeField] private SpriteRenderer m_srOverlay;        // 미점령 영역을 가리는 마스크

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

        private async void GameLogic_Async()
        {
            try
            {
                // 에셋 메모리 로드 (Addressable 라벨)
                await m_cGameInstance.LoadAssetAsync(CAddressableLabel.PREFAB);

                if (m_cStageManager.Initialize(m_cStageDesc, m_srBackground, m_srOverlay) == false)
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
    }
}
