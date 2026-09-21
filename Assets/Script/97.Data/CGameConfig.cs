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

        // 260920_맵 위 상호작용 아이템(2-20). 꺼두면 아이템이 아예 나오지 않는다 — 아이템 없이 도는지 볼 때 쓴다.
        [Header("필드 아이템")]
        [Tooltip("끄면 맵 위 아이템이 한 개도 나오지 않는다. 등장 빈도는 MapInfo.csv가 정한다.")]
        [SerializeField] private bool m_bFieldItemEnabled = true;

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

        // 260918_전체 마비(런 스킬)가 터질 때. '모든 적에게 걸렸다'가 한눈에 읽혀야 해서 피격보다 세게 잡았다.
        [Tooltip("전체 마비 때 쌓는 트라우마 0~1.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_fTraumaOnMassStun = 0.55f;

        // 260920_판이 커질수록 시야를 넓힌다(2-10). 선 길이와 점령률 둘 다 "화면에 담아야 할 것이 늘었다"는 뜻이다.
        [Header("줌아웃 (선 길이 · 점령률)")]
        [Tooltip("끄면 선을 길게 긋든 땅을 많이 먹든 시야가 그대로다.")]
        [SerializeField] private bool m_bTrailZoomEnabled = true;

        [Tooltip("그리는 선 한 칸마다 넓어지는 비율. 0.012면 50칸에 60% 넓어진다.")]
        [Range(0f, 0.05f)]
        [SerializeField] private float m_fTrailZoomPerCell = 0.012f;

        [Tooltip("점령률에 곱해 더하는 비율. 0.5면 100% 점령에서 50% 넓어진다.")]
        [Range(0f, 1.5f)]
        [SerializeField] private float m_fZoomPerOwnedRatio = 0.5f;

        [Tooltip("둘을 더해도 이 배율을 넘지 않는다.")]
        [Range(1f, 2.5f)]
        [SerializeField] private float m_fTrailZoomMax = 1.8f;

        [Tooltip("목표 배율까지 따라붙는 시간(초). 클수록 느긋하게 물러난다.")]
        [Range(0.05f, 1.5f)]
        [SerializeField] private float m_fTrailZoomFollowTime = 0.45f;

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

        [Tooltip("전체 마비 때 쌓는 펀치 0~1.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_fPunchOnMassStun = 0.8f;

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

        [Tooltip("전체 마비 플래시가 다 사라지기까지 걸리는 시간(초).")]
        [Range(0.05f, 1.5f)]
        [SerializeField] private float m_fFlashMassStunDuration = 0.45f;

        [Tooltip("전체 마비 플래시의 최대 알파.")]
        [Range(0f, 1f)]
        [SerializeField] private float m_fFlashMassStunAlpha = 0.5f;

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
        // 260920_캐릭터를 전부 가진 것으로 친다 — 가방 캐릭터 탭에서 바로 갈아 끼우며 테스트할 때 쓴다.
        [Tooltip("켜면 CharacterInfo.csv의 캐릭터를 전부 1레벨로 갖고 시작한다. 이동 방식 테스트용.")]
        [SerializeField] private bool m_bUnlockAllCharacter;

        // 260921_좌상단 디버그 글자(CDebugHUD)를 처음부터 띄울지. 켜고 끄는 것은 플레이 중 F1로 한다.
        [Tooltip("켜면 시작할 때부터 좌상단 디버그 정보가 보인다. 플레이 중에는 F1로 켜고 끈다.")]
        [SerializeField] private bool m_bDebugHudVisible;

        // 260920_캐릭터를 바꾸지 않고 이동 방식만 바꿔 본다(2-22). NONE이면 캐릭터 표를 따른다.
        [Tooltip("FOUR_WAY / EIGHT_WAY를 고르면 장착한 캐릭터와 상관없이 그 방식으로 움직인다.")]
        [SerializeField] private DEV_MOVE_STYLE m_eDevMoveStyle = DEV_MOVE_STYLE.NONE;

        [Tooltip("켜면 코인을 쓰지 않고 구매 / 강화 / 스킬 강화가 된다. 코인은 줄지 않는다.")]
        [SerializeField] private bool m_bFreeSpend;

        [Tooltip("코인이 0일 때 처음 한 번만 넣어 준다. 0이면 넣지 않는다. 가격과 '코인 부족'까지 그대로 확인하고 싶을 때 쓴다.")]
        [Min(0)]
        [SerializeField] private int m_iStartCoin;

        // 260917_투사체(Project_GYM 이식) 확인용. 플레이어 탄을 쏘는 스킬이 아직 없고,
        // 포수는 일반탄 하나만 쏘므로 나머지 14종은 이 스위치 없이는 화면에서 볼 길이 없다.
        [Header("디버그 — 투사체")]
        [Tooltip("0이 아니면 플레이어가 이 ProjectileInfo ID의 탄을 가장 가까운 몬스터에게 저절로 쏜다.")]
        [Min(0)]
        [SerializeField] private int m_iDevAutoFireProjectileID;

        [Tooltip("위 자동 발사의 간격(초).")]
        [Range(0.05f, 5f)]
        [SerializeField] private float m_fDevAutoFireCool = 0.8f;

        [Tooltip("0이 아니면 포수(PROJECTILE 기믹 몬스터)가 표에 적힌 탄 대신 이 ProjectileInfo ID의 탄을 쏜다.")]
        [Min(0)]
        [SerializeField] private int m_iDevEnemyShotID;

        public float PLAYER_SPEED_SCALE => Mathf.Max(0.01f, m_fPlayerSpeedScale);
        public float UI_RESERVE_TOP     => Mathf.Clamp(m_fUIReserveTop, 0f, 0.4f);
        public float UI_RESERVE_BOTTOM  => Mathf.Clamp(m_fUIReserveBottom, 0f, 0.5f);
        public float CAMERA_MARGIN      => Mathf.Max(0f, m_fCameraMargin);
        public float VIEW_CELL_HEIGHT   => Mathf.Max(0f, m_fViewCellHeight);
        public float CAMERA_FOLLOW_TIME => Mathf.Clamp(m_fCameraFollowTime, 0f, 0.6f);
        public bool  FIELD_ITEM_ENABLED      => m_bFieldItemEnabled;
        public bool  CAMERA_SHAKE_ENABLED    => m_bCameraShakeEnabled;
        public float SHAKE_MAX_OFFSET        => Mathf.Max(0f, m_fShakeMaxOffset);
        public float SHAKE_DECAY_PER_SECOND  => Mathf.Max(0.01f, m_fShakeDecayPerSecond);
        public float TRAUMA_ON_HIT           => Mathf.Clamp01(m_fTraumaOnHit);
        public float TRAUMA_ON_DEATH         => Mathf.Clamp01(m_fTraumaOnDeath);
        public float TRAUMA_ON_MASS_STUN     => Mathf.Clamp01(m_fTraumaOnMassStun);
        public float PUNCH_ON_MASS_STUN      => Mathf.Clamp01(m_fPunchOnMassStun);
        public float FLASH_MASS_STUN_DURATION => Mathf.Max(0.01f, m_fFlashMassStunDuration);
        public float FLASH_MASS_STUN_ALPHA   => Mathf.Clamp01(m_fFlashMassStunAlpha);
        public bool  TRAIL_ZOOM_ENABLED      => m_bTrailZoomEnabled;
        public float TRAIL_ZOOM_PER_CELL     => Mathf.Max(0f, m_fTrailZoomPerCell);
        public float ZOOM_PER_OWNED_RATIO    => Mathf.Max(0f, m_fZoomPerOwnedRatio);
        public float TRAIL_ZOOM_MAX          => Mathf.Max(1f, m_fTrailZoomMax);
        public float TRAIL_ZOOM_FOLLOW_TIME  => Mathf.Max(0.01f, m_fTrailZoomFollowTime);
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
        public bool  UNLOCK_ALL_CHARACTER => m_bUnlockAllCharacter;
        public bool  DEBUG_HUD_VISIBLE    => m_bDebugHudVisible;
        /// <summary> 260920_개발용 이동 방식 덮어쓰기. null이면 캐릭터를 따른다(2-22). </summary>
        public MOVE_STYLE? DEV_MOVE_STYLE_OVERRIDE
            => m_eDevMoveStyle == DEV_MOVE_STYLE.NONE ? (MOVE_STYLE?)null
             : m_eDevMoveStyle == DEV_MOVE_STYLE.EIGHT_WAY ? MOVE_STYLE.EIGHT_WAY : MOVE_STYLE.FOUR_WAY;
        public bool  FREE_SPEND         => m_bFreeSpend;
        public int   START_COIN         => Mathf.Max(0, m_iStartCoin);
        public int   DEV_AUTO_FIRE_ID   => Mathf.Max(0, m_iDevAutoFireProjectileID);
        public float DEV_AUTO_FIRE_COOL => Mathf.Max(0.05f, m_fDevAutoFireCool);
        public int   DEV_ENEMY_SHOT_ID  => Mathf.Max(0, m_iDevEnemyShotID);

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
