using UnityEngine;

namespace Client
{
    // 260921_계정 성장 · 하트 계산 (2-23)
    /// <summary>
    /// 저장소도 시계도 모르는 순수 계산만 모았다 — 지금 시각을 인자로 받으므로 화면 없이,
    /// 기다리지 않고 "30분 뒤" 같은 상황을 그대로 테스트할 수 있다(CProtoTest).
    /// 실제 저장과 시각은 CProgress_Manager가 넣는다.
    /// </summary>
    public static class CAccount_Utility
    {
        /// <summary>
        /// 지난 시간만큼 하트를 채운다. 가득 차 있는 동안은 시각이 흘러도 **쌓아 두지 않는다** —
        /// 가득 찬 채로 하루를 놀다 와도 한 칸 쓰는 순간부터 다시 센다(모바일 스태미나의 흔한 규칙).
        /// </summary>
        /// <param name="lAnchor"> 회복을 세기 시작한 시각(유닉스 초). 다음 한 칸은 여기서 fRegenSec 뒤 </param>
        public static void Regen_Stamina(int iStamina, int iMax, long lAnchor, long lNow, float fRegenSec,
                                         out int iNewStamina, out long lNewAnchor)
        {
            iNewStamina = iStamina;
            lNewAnchor  = lAnchor;

            if (iStamina >= iMax || fRegenSec <= 0f)
            {
                // 가득 찼으면 기준 시각을 지금으로 끌고 온다 — 안 그러면 쓰는 순간 밀린 시간이 한꺼번에 들어온다
                lNewAnchor = lNow;
                return;
            }

            long lElapsed = lNow - lAnchor;
            if (lElapsed <= 0)
                return;

            long lRegenSec = (long)Mathf.Max(1f, fRegenSec);
            long lGain     = lElapsed / lRegenSec;
            if (lGain <= 0)
                return;

            iNewStamina = (int)Mathf.Min(iMax, iStamina + lGain);
            lNewAnchor  = iNewStamina >= iMax ? lNow : lAnchor + lGain * lRegenSec;
        }

        /// <summary> 다음 한 칸이 찰 때까지 남은 초. 가득 찼으면 0. </summary>
        public static int Get_StaminaRemainSec(int iStamina, int iMax, long lAnchor, long lNow, float fRegenSec)
        {
            if (iStamina >= iMax || fRegenSec <= 0f)
                return 0;

            long lRegenSec = (long)Mathf.Max(1f, fRegenSec);
            long lPassed   = (lNow - lAnchor) % lRegenSec;
            return (int)Mathf.Max(0, lRegenSec - lPassed);
        }

        /// <summary>
        /// 스테이지에서 얻는 계정 경험치 — 총 경험치 × 진행도(0~1). **점령률에 비례한다**(2-23).
        /// 진행도는 CStage_Manager.PROGRESS가 낸다(달성한 웨이브 + 지금 웨이브의 점령률 / 웨이브 수).
        /// </summary>
        public static int Calc_StageExp(int iExpTotal, float fProgress)
            => Mathf.Max(0, Mathf.RoundToInt(iExpTotal * Mathf.Clamp01(fProgress)));

        /// <summary>
        /// 스테이지 진행도 0~1. 웨이브를 넘길 때마다 점령률이 0으로 돌아가므로(2-5)
        /// 그냥 점령률을 쓰면 3웨이브에서 죽은 판이 1웨이브에서 죽은 판보다 적게 받는다 —
        /// 그래서 **달성한 웨이브 수 + 지금 웨이브에서 목표 대비 얼마나 먹었는지**로 잰다.
        /// </summary>
        public static float Calc_StageProgress(int iClearedWave, int iWaveCount, float fOwnedRatio, float fClearRatio)
        {
            if (iWaveCount <= 0)
                return 0f;

            if (iClearedWave >= iWaveCount)
                return 1f;

            float fPartial = fClearRatio > 0f ? Mathf.Clamp01(fOwnedRatio / fClearRatio) : 0f;
            return Mathf.Clamp01((iClearedWave + fPartial) / iWaveCount);
        }

        /// <summary> 남은 초를 "04:59"로. 하트 회복 시계에 쓴다. </summary>
        public static string Format_Timer(int iSeconds)
        {
            int iTotal = Mathf.Max(0, iSeconds);
            return $"{iTotal / 60:00}:{iTotal % 60:00}";
        }
    }
}
