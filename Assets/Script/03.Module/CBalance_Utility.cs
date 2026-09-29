using UnityEngine;

namespace Client
{
    // 260929_카드 밸런스 스펙(Docs/Design_Card_Balance_Spec.md §6) — 수치 계산식·상한을 한곳에 모았다(1-1).
    // 화면·저장소·시계를 몰라서 CProtoTest가 숫자만으로 검증한다.
    /// <summary>
    /// 이동속도 = BASE × (1 + B) × D × S. B는 카드 증가분 합(+100% 상한), D는 감속 곱(하한 0.30), S는 상태 배율(상한 없음).
    /// 도화선 속도 = BASE × clamp(stageFuseMul + 카드 보정 합, 0.40, 1.50) — 플레이어 속도와 무관하다.
    /// </summary>
    public static class CBalance_Utility
    {
        /// <summary> 스펙 BASE_SPEED(칸/초). 도화선 속도의 기준이다 — 플레이어 실제 속도는 맵 · 계정 배율을 거친 CPlayer의 값을 쓴다. </summary>
        public const float BASE_SPEED         = 6f;

        public const float SPEED_BONUS_MAX    = 1f;      // B 상한 +100%
        public const float SLOW_FACTOR_MIN    = 0.30f;   // D 하한 — 완전 정지 방지

        public const float FUSE_MUL_MIN       = 0.40f;
        public const float FUSE_MUL_MAX       = 1.50f;

        public const int   GROGGY_RESIST_LINEAR_COUNT = 5;      // 여기까지는 기절마다 +25%, 그 뒤는 +100%
        public const float GROGGY_RESIST_STEP         = 0.25f;
        public const float GROGGY_RESIST_STEP_HEAVY   = 1.00f;
        public const float GROGGY_DECAY_PER_SEC       = 0.02f;  // 총량 대비

        /// <summary> B — 카드 · 영웅으로 얻은 이동속도 증가율 합. 0~+1.0으로 자른다(감소는 여기 없다 — 감속은 D). </summary>
        public static float Clamp_SpeedBonus(float fSum) => Mathf.Clamp(fSum, 0f, SPEED_BONUS_MAX);

        /// <summary> D — 감속 배율(곱)의 하한. 1이면 감속 없음. </summary>
        public static float Clamp_SlowFactor(float fProduct) => Mathf.Max(fProduct, SLOW_FACTOR_MIN);

        /// <summary> 플레이어 속도 배율(BASE 제외) = (1 + B) × D × S. </summary>
        public static float Calc_SpeedMultiplier(float fBonusSum, float fSlowProduct, float fStateScale)
            => (1f + Clamp_SpeedBonus(fBonusSum)) * Clamp_SlowFactor(fSlowProduct) * fStateScale;

        public static float Clamp_FuseMul(float fStageMul, float fCardDeltaSum)
            => Mathf.Clamp(fStageMul + fCardDeltaSum, FUSE_MUL_MIN, FUSE_MUL_MAX);

        /// <summary> 도화선 전파 속도(칸/초). 플레이어 속도 버프로는 느려지지 않는다. </summary>
        public static float Calc_FuseSpeed(float fStageMul, float fCardDeltaSum)
            => BASE_SPEED * Clamp_FuseMul(fStageMul, fCardDeltaSum);

        /// <summary> 스펙 §6.3 — n번째(0부터) 기절에 필요한 그로기. n은 이번 판 누적 기절 횟수. </summary>
        public static float Calc_RequiredGroggy(float fTotal, int iStunCount)
        {
            int iN = Mathf.Max(0, iStunCount);
            float fMul = iN < GROGGY_RESIST_LINEAR_COUNT
                ? 1f + GROGGY_RESIST_STEP * iN
                : 1f + GROGGY_RESIST_STEP * GROGGY_RESIST_LINEAR_COUNT + GROGGY_RESIST_STEP_HEAVY * (iN - GROGGY_RESIST_LINEAR_COUNT);
            return fTotal * fMul;
        }
    }
}
