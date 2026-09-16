using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260917_피격 효과 — Project_GYM의 CImpact / CImpactFactory를 이식
    /// <summary>
    /// 대상(플레이어 · 몬스터) 하나가 지금 받고 있는 효과를 전부 들고 있다.
    /// 대상은 매 프레임 Tick을 부르고, 결과(기절 중인가 · 속도 배율 · 번쩍이는가)만 읽어 자기 움직임에 반영한다.
    ///
    /// GYM과 달라진 점
    ///  · 코루틴을 쓰지 않는다. GYM은 효과마다 대상에게 코루틴을 걸었는데(CImpact.Apply),
    ///    대상이 풀로 돌아가도 전역 코루틴(CCoroutine_Excution)은 계속 돌아 다른 몬스터의 HP를 깎았다.
    ///    여기서는 타이머만 들고 Clear 한 번에 전부 끊긴다 — 화면 없이 검증할 수도 있다.
    ///  · 효과가 대상의 상태 기계를 직접 바꾸지 않는다(GYM은 Change_State(FSM.STUN)).
    ///    기절이 둘 겹쳤다가 하나만 풀리면 GYM은 MOVE로 돌아가 버렸다 — 여기선 남은 것이 하나라도 있으면 계속 기절이다.
    ///  · 같은 효과를 다시 맞으면 새로 거는 대신 시간을 늘린다(짧은 쪽으로 덮어쓰지 않는다).
    /// </summary>
    public class CImpactHandler
    {
        private enum ENTRY_KIND { STUN, SLOW, DOT, WHITE }

        private class CEntry
        {
            public ENTRY_KIND   eKind;
            public float        fRemain;        // 음수면 닿아 있는 동안 유지 (Remove가 끊는다)
            public float        fValue;         // SLOW 배율 / DOT 초당 피해
            public float        fDotTimer;      // 다음 도트까지 남은 시간
            // 남은 도트 횟수. 시간으로 끝내면 3초짜리가 float 오차로 네 번 들어가기도 해서 횟수로 센다. 음수면 무한(닿아 있는 동안)
            public int          iDotLeft;

            public bool IS_CONTACT => fRemain < 0f;
        }

        private const float DOT_INTERVAL = 1f;      // GYM과 같이 1초마다 (CImpact_DamageOverTime)

        // 키는 효과 표의 행(CImpactInfo) 또는 탄 특성 모듈 인스턴스다 — 같은 출처의 효과는 겹치지 않고 갱신된다.
        private readonly Dictionary<object, CEntry> m_dicEntry = new Dictionary<object, CEntry>();
        private readonly List<object> m_lstExpired = new List<object>();

        private bool  m_bStunned;
        private bool  m_bWhiteOut;
        private float m_fSpeedScale = 1f;

        public bool  IS_STUNNED   => m_bStunned;
        public bool  IS_WHITE_OUT => m_bWhiteOut;
        /// <summary> 겹친 감속 중 가장 센 것. 없으면 1. </summary>
        public float SPEED_SCALE  => m_fSpeedScale;
        public int   COUNT        => m_dicEntry.Count;

        #region 효과 표(ImpactInfo.csv)로 거는 효과
        /// <summary>
        /// 탄에 맞았을 때 부른다. 넉백 · 폭발은 남는 효과가 아니라 그 자리에서 한 번 일어나고 끝난다.
        /// </summary>
        /// <param name="vSource"> 효과를 건 탄의 위치 — 넉백 방향을 정한다 </param>
        /// <param name="fCellSize"> 표의 거리 단위(셀)를 월드로 바꾸는 값 </param>
        public void Apply(CImpactInfo cInfo, IImpactTarget cTarget, Vector2 vSource, float fCellSize,
                          IProjectileHost cHost, PROJECTILE_SIDE eSide)
        {
            if (cInfo == null || cTarget == null || cTarget.IS_ALIVE == false)
                return;

            switch (cInfo.eType)
            {
                case IMPACT_TYPE.STUN:
                    Set(cInfo, ENTRY_KIND.STUN, cInfo.fTime, 0f);
                    break;

                case IMPACT_TYPE.SLOW:
                    Set(cInfo, ENTRY_KIND.SLOW, cInfo.fTime, cInfo.fValue);
                    break;

                case IMPACT_TYPE.DOT:
                    Set(cInfo, ENTRY_KIND.DOT, cInfo.fTime, cInfo.fValue);
                    break;

                case IMPACT_TYPE.KNOCKBACK:
                {
                    // GYM은 플레이어로부터 먼 쪽으로 밀었다. 적탄도 있으므로 '맞힌 탄에서 먼 쪽'으로 바꿨다.
                    Vector2 vDir = cTarget.POS - vSource;
                    if (vDir.sqrMagnitude <= Mathf.Epsilon)
                        vDir = Vector2.down;

                    cTarget.Push(vDir.normalized, cInfo.fValue * fCellSize, Mathf.Max(0.01f, cInfo.fTime));
                    break;
                }

                case IMPACT_TYPE.EXPLODE:
                    // GYM은 폭발 탄 ID를 201로 박아 두었다. 표의 참조 칸으로 옮겼다.
                    if (cHost != null && cInfo.iRefID > 0)
                        cHost.Spawn_Projectile(cInfo.iRefID, cTarget.POS, Vector2.up, eSide);
                    break;
            }
        }

        /// <summary> 탄에서 떨어졌을 때 '닿아 있는 동안만' 효과를 끊는다. 시간제 효과는 그대로 둔다. </summary>
        public void Remove_Contact(CImpactInfo cInfo)
        {
            if (cInfo != null && m_dicEntry.TryGetValue(cInfo, out CEntry cEntry) == true && cEntry.IS_CONTACT == true)
                Remove(cInfo);
        }
        #endregion 효과 표(ImpactInfo.csv)로 거는 효과

        #region 탄 특성이 직접 거는 효과
        public void Set_Stun(object cKey, float fTime)          => Set(cKey, ENTRY_KIND.STUN, fTime, 0f);
        public void Set_Slow(object cKey, float fTime, float fScale) => Set(cKey, ENTRY_KIND.SLOW, fTime, fScale);
        public void Set_WhiteOut(object cKey, float fTime)      => Set(cKey, ENTRY_KIND.WHITE, fTime, 0f);
        #endregion 탄 특성이 직접 거는 효과

        public void Remove(object cKey)
        {
            if (cKey != null && m_dicEntry.Remove(cKey) == true)
                Refresh_Result();
        }

        /// <summary> 대상이 죽거나 풀로 돌아갈 때. </summary>
        public void Clear()
        {
            m_dicEntry.Clear();
            Refresh_Result();
        }

        /// <returns> 이번 프레임에 들어갈 도트 피해 합 </returns>
        public int Tick(float fDeltaTime)
        {
            int iDamage = 0;
            m_lstExpired.Clear();

            foreach (KeyValuePair<object, CEntry> cPair in m_dicEntry)
            {
                CEntry cEntry = cPair.Value;

                if (cEntry.eKind == ENTRY_KIND.DOT)
                {
                    cEntry.fDotTimer -= fDeltaTime;
                    if (cEntry.fDotTimer <= 0f && cEntry.iDotLeft != 0)
                    {
                        iDamage += Mathf.Max(0, Mathf.RoundToInt(cEntry.fValue));
                        cEntry.fDotTimer += DOT_INTERVAL;
                        if (cEntry.iDotLeft > 0)
                            --cEntry.iDotLeft;
                    }

                    if (cEntry.iDotLeft == 0)
                        m_lstExpired.Add(cPair.Key);
                    continue;
                }

                if (cEntry.IS_CONTACT == true)
                    continue;

                cEntry.fRemain -= fDeltaTime;
                if (cEntry.fRemain <= 0f)
                    m_lstExpired.Add(cPair.Key);
            }

            for (int i = 0; i < m_lstExpired.Count; ++i)
                m_dicEntry.Remove(m_lstExpired[i]);

            if (m_lstExpired.Count > 0)
                Refresh_Result();

            return iDamage;
        }

        private void Set(object cKey, ENTRY_KIND eKind, float fTime, float fValue)
        {
            if (cKey == null)
                return;

            // 시간 0짜리는 걸 이유가 없다(표 오타). 음수는 '닿아 있는 동안'이라 그대로 받는다.
            if (fTime == 0f)
                return;

            if (m_dicEntry.TryGetValue(cKey, out CEntry cEntry) == true)
            {
                // 다시 맞았다 — 남은 시간이 더 길어지는 쪽으로만 갱신한다.
                if (cEntry.IS_CONTACT == false && fTime > 0f)
                {
                    cEntry.fRemain  = Mathf.Max(cEntry.fRemain, fTime);
                    cEntry.iDotLeft = Mathf.Max(cEntry.iDotLeft, Get_DotCount(fTime));
                }
                cEntry.fValue = fValue;
            }
            else
            {
                // 도트는 맞은 순간이 아니라 1초 뒤부터 들어간다 — 3초짜리면 1 · 2 · 3초에 세 번.
                m_dicEntry.Add(cKey, new CEntry { eKind = eKind, fRemain = fTime, fValue = fValue,
                                                  fDotTimer = DOT_INTERVAL, iDotLeft = Get_DotCount(fTime) });
            }

            Refresh_Result();
        }

        private static int Get_DotCount(float fTime)
            => fTime < 0f ? -1 : Mathf.Max(1, Mathf.RoundToInt(fTime / DOT_INTERVAL));

        private void Refresh_Result()
        {
            m_bStunned    = false;
            m_bWhiteOut   = false;
            m_fSpeedScale = 1f;

            foreach (CEntry cEntry in m_dicEntry.Values)
            {
                switch (cEntry.eKind)
                {
                    case ENTRY_KIND.STUN:  m_bStunned  = true; break;
                    case ENTRY_KIND.WHITE: m_bWhiteOut = true; break;
                    case ENTRY_KIND.SLOW:
                        m_fSpeedScale = Mathf.Min(m_fSpeedScale, Mathf.Clamp(cEntry.fValue, 0.05f, 1f));
                        break;
                }
            }
        }
    }
}
