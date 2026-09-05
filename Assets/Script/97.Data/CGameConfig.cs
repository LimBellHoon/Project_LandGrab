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

        [Header("디버그")]
        [Tooltip("켜면 해금 규칙을 무시하고 모든 맵을 고를 수 있다.")]
        [SerializeField] private bool m_bUnlockAllStage;

        [Tooltip("켜면 코인을 쓰지 않고 구매 / 강화 / 스킬 강화가 된다. 코인은 줄지 않는다.")]
        [SerializeField] private bool m_bFreeSpend;

        [Tooltip("코인이 0일 때 처음 한 번만 넣어 준다. 0이면 넣지 않는다. 가격과 '코인 부족'까지 그대로 확인하고 싶을 때 쓴다.")]
        [Min(0)]
        [SerializeField] private int m_iStartCoin;

        public float PLAYER_SPEED_SCALE => Mathf.Max(0.01f, m_fPlayerSpeedScale);
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
