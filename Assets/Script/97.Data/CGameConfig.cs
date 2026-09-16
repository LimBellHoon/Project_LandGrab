using UnityEngine;

namespace Client
{
    // 260905_튜닝 / 디버그 옵션
    /// <summary>
    /// 손으로 자주 만지는 값과 개발용 스위치를 한곳에 모아 둔 에셋이다.
    /// Assets/Resources/GameConfig.asset 하나만 쓰고, 없으면 기본값으로 돈다.
    ///
    /// 규칙 숫자(맵 크기·속도·비용)는 여전히 Assets/Data/*.csv가 원본이다.
    /// 여기에는 그 값을 '얼마나 비틀지'와 '규칙을 건너뛸지'만 둔다 —
    /// 같은 숫자를 두 군데 적어 두면 어느 쪽이 진짜인지 알 수 없게 된다.
    /// </summary>
    [CreateAssetMenu(fileName = ASSET_NAME, menuName = "LandGrab/Game Config")]
    public class CGameConfig : ScriptableObject
    {
        public const string ASSET_NAME = "GameConfig";

        [Header("조작감")]
        [Tooltip("MapInfo.csv의 fPlayerSpeed에 곱한다. 1이면 표에 적힌 그대로. 맵마다 다른 속도는 CSV에서, 전체적인 빠르기는 여기서 잡는다.")]
        [Range(0.2f, 3f)]
        [SerializeField] private float m_fPlayerSpeedScale = 1f;

        [Header("화면")]
        [Tooltip("맵 위로 비워 둘 화면 비율. 상단 정보 바가 앉는 자리다.")]
        [Range(0f, 0.4f)]
        [SerializeField] private float m_fUIReserveTop = 0.10f;

        [Tooltip("맵 아래로 비워 둘 화면 비율. 조이스틱과 버튼이 앉는 자리다.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float m_fUIReserveBottom = 0.22f;

        [Tooltip("맵 둘레에 남기는 여백. 월드 단위다.")]
        [Range(0f, 2f)]
        [SerializeField] private float m_fCameraMargin = 0.3f;

        [Header("카메라 추적")]
        [Tooltip("화면에 보여 줄 세로 칸 수. 0이면 맵 전체를 한 화면에 맞춘다. 맵이 커져도 이 값은 그대로라, 스테이지가 오를수록 좁게 느껴지고 속도 강화가 필요해진다.")]
        [Min(0f)]
        [SerializeField] private float m_fViewCellHeight = 44f;

        [Tooltip("카메라가 플레이어를 따라붙는 데 걸리는 시간(초). 0이면 딱 붙어 따라간다.")]
        [Range(0f, 0.6f)]
        [SerializeField] private float m_fCameraFollowTime = 0.12f;

        [Header("카메라 흔들림")]
        [Tooltip("끄면 아예 흔들리지 않는다. 옵션창이 생기면 이 값 하나만 토글하면 된다.")]
        [SerializeField] private bool m_bCameraShakeEnabled = true;

        [Tooltip("트라우마가 가득 찼을 때(1) 카메라가 밀리는 최대 거리. 월드 단위다.")]
        [Range(0f, 0.5f)]
        [SerializeField] private float m_fShakeMaxOffset = 0.18f;

        [Tooltip("트라우마가 초당 줄어드는 양. 클수록 흔들림이 빨리 잦아든다.")]
        [Range(0.5f, 10f)]
        [SerializeField] private float m_fShakeDecayPerSecond = 2.5f;

        // 260916_원인마다 값을 하나씩 늘리는 자리다. CCameraShake는 안 고치고 여기에
        // 필드 하나, Add_Trauma 호출 한 줄만 추가하면 새 흔들림 원인이 생긴다(2-10-2).
        [Tooltip("피격 한 번에 쌓는 트라우마 0~1.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_fTraumaOnHit = 0.35f;

        [Tooltip("사망 시 쌓는 트라우마 0~1. 피격 트라우마와 더해지므로 죽는 순간은 항상 더 세다.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_fTraumaOnDeath = 0.8f;

        [Header("카메라 펀치줌")]
        [Tooltip("끄면 점령해도 확대되지 않는다.")]
        [SerializeField] private bool m_bCameraPunchEnabled = true;

        [Tooltip("펀치가 가득 찼을 때 카메라가 확대되어 보이는 비율. 0.06이면 6% 확대.")]
        [Range(0f, 0.3f)]
        [SerializeField] private float m_fPunchMaxZoomRatio = 0.06f;

        [Tooltip("펀치가 초당 줄어드는 양. 클수록 빨리 원래 크기로 돌아온다.")]
        [Range(0.5f, 10f)]
        [SerializeField] private float m_fPunchDecayPerSecond = 4f;

        [Tooltip("한 번 점령할 때마다 쌓는 펀치 0~1. 점령 칸 수와 상관없이 고정값이다.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_fPunchOnCapture = 0.5f;

        [Header("화면 플래시")]
        [Tooltip("끄면 피격/회피해도 화면이 물들지 않는다.")]
        [SerializeField] private bool m_bScreenFlashEnabled = true;

        [Tooltip("피격 플래시가 다 사라지기까지 걸리는 시간(초).")]
        [Range(0.05f, 1f)]
        [SerializeField] private float m_fFlashHitDuration = 0.25f;

        [Tooltip("피격 플래시의 최대 알파. 화면을 얼마나 덮을지.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_fFlashHitAlpha = 0.35f;

        [Tooltip("회피 플래시가 다 사라지기까지 걸리는 시간(초).")]
        [Range(0.05f, 1f)]
        [SerializeField] private float m_fFlashEvadeDuration = 0.2f;

        [Tooltip("회피 플래시의 최대 알파.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_fFlashEvadeAlpha = 0.25f;

        [Header("사운드")]
        [Tooltip("끄면 효과음이 전혀 나지 않는다.")]
        [SerializeField] private bool m_bSfxEnabled = true;

        [Tooltip("효과음 전체 볼륨. 종류별 상대 크기는 CAudio_Manager 표에 따로 있다.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_fSfxVolume = 0.8f;

        [Header("햅틱")]
        [Tooltip("끄면 진동이 전혀 울리지 않는다. 세기 조절은 없다 — 몇 번 · 얼마나 간격으로 울릴지는 CHaptic_Manager 표에 있다.")]
        [SerializeField] private bool m_bHapticEnabled = true;

        [Header("디버그")]
        [Tooltip("켜면 해금 규칙을 무시하고 모든 맵을 고를 수 있다.")]
        [SerializeField] private bool m_bUnlockAllStage;

        [Tooltip("켜면 코인을 쓰지 않고 구매 / 강화 / 스킬 강화가 된다. 코인은 줄지 않는다.")]
        [SerializeField] private bool m_bFreeSpend;

        [Tooltip("코인이 0일 때 처음 한 번만 넣어 준다. 0이면 넣지 않는다. 가격과 '코인 부족'까지 그대로 확인하고 싶을 때 쓴다.")]
        [Min(0)]
        [SerializeField] private int m_iStartCoin;

        public float PLAYER_SPEED_SCALE => Mathf.Max(0.01f, m_fPlayerSpeedScale);
        public float UI_RESERVE_TOP     => Mathf.Clamp(m_fUIReserveTop, 0f, 0.4f);
        public float UI_RESERVE_BOTTOM  => Mathf.Clamp(m_fUIReserveBottom, 0f, 0.5f);
        public float CAMERA_MARGIN      => Mathf.Max(0f, m_fCameraMargin);
        public float VIEW_CELL_HEIGHT   => Mathf.Max(0f, m_fViewCellHeight);
        public float CAMERA_FOLLOW_TIME => Mathf.Clamp(m_fCameraFollowTime, 0f, 0.6f);
        public bool  CAMERA_SHAKE_ENABLED    => m_bCameraShakeEnabled;
        public float SHAKE_MAX_OFFSET        => Mathf.Max(0f, m_fShakeMaxOffset);
        public float SHAKE_DECAY_PER_SECOND  => Mathf.Max(0.01f, m_fShakeDecayPerSecond);
        public float TRAUMA_ON_HIT           => Mathf.Clamp01(m_fTraumaOnHit);
        public float TRAUMA_ON_DEATH         => Mathf.Clamp01(m_fTraumaOnDeath);
        public bool  CAMERA_PUNCH_ENABLED    => m_bCameraPunchEnabled;
        public float PUNCH_MAX_ZOOM_RATIO    => Mathf.Clamp01(m_fPunchMaxZoomRatio);
        public float PUNCH_DECAY_PER_SECOND  => Mathf.Max(0.01f, m_fPunchDecayPerSecond);
        public float PUNCH_ON_CAPTURE        => Mathf.Clamp01(m_fPunchOnCapture);
        public bool  SCREEN_FLASH_ENABLED    => m_bScreenFlashEnabled;
        public float FLASH_HIT_DURATION      => Mathf.Max(0.01f, m_fFlashHitDuration);
        public float FLASH_HIT_ALPHA         => Mathf.Clamp01(m_fFlashHitAlpha);
        public float FLASH_EVADE_DURATION    => Mathf.Max(0.01f, m_fFlashEvadeDuration);
        public float FLASH_EVADE_ALPHA       => Mathf.Clamp01(m_fFlashEvadeAlpha);
        public bool  SFX_ENABLED             => m_bSfxEnabled;
        public float SFX_VOLUME              => Mathf.Clamp01(m_fSfxVolume);
        public bool  HAPTIC_ENABLED           => m_bHapticEnabled;
        public bool  UNLOCK_ALL_STAGE   => m_bUnlockAllStage;
        public bool  FREE_SPEND         => m_bFreeSpend;
        public int   START_COIN         => Mathf.Max(0, m_iStartCoin);

        /// <summary>
        /// 에셋이 없어도 게임은 돌아야 하므로 기본값 인스턴스를 만들어 돌려준다.
        /// (클론 직후처럼 에셋이 빠진 상태에서 Play를 눌러도 멈추지 않게 하기 위함)
        /// </summary>
        public static CGameConfig Load()
        {
            CGameConfig cConfig = Resources.Load<CGameConfig>(ASSET_NAME);
            if (cConfig != null)
                return cConfig;

            Debug.LogWarning($"[CGameConfig] Resources/{ASSET_NAME}.asset이 없어 기본값으로 돕니다.");
            return CreateInstance<CGameConfig>();
        }
    }
}
