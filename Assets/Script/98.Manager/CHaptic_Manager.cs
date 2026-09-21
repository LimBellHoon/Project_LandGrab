using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260916_햅틱 — 절차적 패턴
    /// <summary>
    /// Unity 기본 API(`Handheld.Vibrate`)는 세기 조절이 없다 — 한 번 울리거나 안 울리거나 둘뿐이다.
    /// 세기 대신 **울리는 횟수와 간격**으로 종류를 구분한다. 피격은 한 번 짧게, 사망은 세 번
    /// 끊어서처럼 — <see cref="CSound_Utility"/>가 파형으로 정체성을 냈다면 여기는 리듬으로 낸다.
    ///
    /// 진짜 세기 조절(iOS Core Haptics / Android VibrationEffect)이 필요해지면
    /// <see cref="Trigger_Pulse"/> 안쪽만 바꾸면 된다 — 호출부(`Play(HAPTIC_ID)`)는 그대로다.
    /// `CAudio_Manager`와 같은 자리다(2-12).
    ///
    /// **에디터에서는 `Handheld.Vibrate`가 조용히 아무 일도 안 한다** — 실기기에서만 확인된다
    /// (`CSafeArea`가 에디터 Game 뷰에서 하는 일이 없는 것과 같은 성격, 2-10).
    /// </summary>
    public class CHaptic_Manager
    {
        // 260916_새 종류를 늘릴 때는 HAPTIC_ID에 값 하나, 여기 표에 줄 하나만 추가하면 된다.
        private readonly struct CHapticDef
        {
            public readonly int   iPulseCount;
            public readonly float fPulseGap;   // 초. 펄스 사이 간격

            public CHapticDef(int iPulseCount, float fPulseGap)
            {
                this.iPulseCount = iPulseCount;
                this.fPulseGap   = fPulseGap;
            }
        }

        private static readonly Dictionary<HAPTIC_ID, CHapticDef> s_dicDef = new Dictionary<HAPTIC_ID, CHapticDef>
        {
            { HAPTIC_ID.HIT,         new CHapticDef(1, 0f) },
            { HAPTIC_ID.DEATH,       new CHapticDef(3, 0.15f) },
            { HAPTIC_ID.EVADE,       new CHapticDef(1, 0f) },
            { HAPTIC_ID.CAPTURE,     new CHapticDef(2, 0.10f) },
            { HAPTIC_ID.CARD_READY,  new CHapticDef(1, 0f) },
            { HAPTIC_ID.STAGE_CLEAR, new CHapticDef(3, 0.12f) },
            { HAPTIC_ID.STAGE_FAIL,  new CHapticDef(2, 0.20f) },
            { HAPTIC_ID.MASS_STUN,   new CHapticDef(2, 0.08f) },     // 260918_전체 마비 — 짧게 두 번
            { HAPTIC_ID.STYLISH,     new CHapticDef(1, 0f) },        // 260922_현란한 동작
        };

        private bool  m_bEnabled = true;

        // 260916_남은 펄스를 시간 기반으로 흘려보낸다. CGameManager.Update가 매 프레임 Tick한다 —
        // 코루틴을 안 쓰는 이유는 CStage_Manager 등 다른 순수 C# 매니저와 같은 결로 두기 위해서다.
        private int   m_iPulseRemain;
        private float m_fGap;
        private float m_fTimer;
        private int   m_iFiredCount;   // 260916_실기기 없이도 몇 번 울렸는지 세어 CProtoTest에서 검증한다.

        /// <summary> 아직 남은 펄스 수. 디버그·테스트용. </summary>
        public int PULSE_REMAIN => m_iPulseRemain;
        /// <summary> Play()를 부른 뒤로 실제 Trigger_Pulse가 불린 누적 횟수. </summary>
        public int FIRED_COUNT  => m_iFiredCount;

        public void Set_Enabled(bool bEnabled) => m_bEnabled = bEnabled;

        /// <summary> 이미 재생 중이던 패턴은 새로 들어온 것으로 덮어쓴다(CFlashEffect와 같은 규칙). </summary>
        public void Play(HAPTIC_ID eHapticID)
        {
            if (m_bEnabled == false || eHapticID == HAPTIC_ID.NONE)
                return;

            if (s_dicDef.TryGetValue(eHapticID, out CHapticDef cDef) == false)
                return;

            Trigger_Pulse();
            m_iPulseRemain = cDef.iPulseCount - 1;
            m_fGap         = cDef.fPulseGap;
            m_fTimer       = cDef.fPulseGap;
        }

        public void Tick(float fDeltaTime)
        {
            if (m_iPulseRemain <= 0)
                return;

            m_fTimer -= fDeltaTime;
            if (m_fTimer > 0f)
                return;

            Trigger_Pulse();
            --m_iPulseRemain;
            m_fTimer = m_fGap;
        }

        private void Trigger_Pulse()
        {
            Handheld.Vibrate();
            ++m_iFiredCount;
        }
    }
}
