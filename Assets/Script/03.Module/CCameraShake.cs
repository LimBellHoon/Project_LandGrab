using UnityEngine;

namespace Client
{
    // 260916_카메라 흔들림 — 트라우마 기반
    /// <summary>
    /// "얼마나 세게 흔들지"만 안다. 무엇이 흔들 자격이 있는지는 모른다 —
    /// 호출부가 <see cref="Add_Trauma"/>로 양만 넘기면 여기서 감쇠와 파형을 계산한다.
    ///
    /// 새 흔들림 원인을 추가할 때 이 클래스는 고칠 필요가 없다. CGameConfig에 트라우마 양
    /// 하나 추가하고, 그 이벤트가 일어나는 곳에서 Add_Trauma(그 값)만 부르면 된다 —
    /// 원인별로 진폭·지속시간 쌍을 따로 관리하지 않는다(2-10-2).
    ///
    /// 트라우마(0~1)는 쌓이고 시간이 지나면 스스로 줄어든다. 화면에 보이는 세기는
    /// 트라우마의 제곱이라 — 살짝 쌓였을 땐 거의 안 보이고 많이 쌓이면 급격히 커진다.
    /// (Squirrel Eiserloh, GDC "Juicing Your Cameras With Math" — 널리 쓰이는 방식을 그대로 따랐다.)
    ///
    /// 판정(여기)과 적용(카메라 Transform에 더하기)을 나눈다 — CVirtualJoystick과 같은 이유로,
    /// 화면 없이 CProtoTest에서 검증하기 위해서다.
    /// </summary>
    public class CCameraShake
    {
        private const float NOISE_SPEED = 25f;     // 펄린 노이즈를 훑는 빠르기 — 흔들림이 떨리는 속도

        private bool  m_bEnabled = true;
        private float m_fMaxOffset;        // 트라우마 1일 때 최대 변위(월드 단위)
        private float m_fDecayPerSecond;   // 트라우마가 초당 줄어드는 양

        private float m_fTrauma;           // 0~1
        private float m_fNoiseTime;
        private float m_fSeedX;
        private float m_fSeedY;

        /// <summary> 디버그 표시용. 흔들림 세기 자체는 Tick의 반환값을 쓸 것. </summary>
        public float TRAUMA => m_fTrauma;

        /// <param name="fMaxOffset"> 트라우마 1일 때 카메라가 밀리는 최대 거리(월드 단위) </param>
        /// <param name="fDecayPerSecond"> 트라우마가 초당 줄어드는 양. 클수록 빨리 잦아든다 </param>
        public void Initialize(float fMaxOffset, float fDecayPerSecond)
        {
            m_fMaxOffset      = Mathf.Max(0f, fMaxOffset);
            m_fDecayPerSecond = Mathf.Max(0.01f, fDecayPerSecond);

            // 260917_같은 프레임에 X/Y가 같은 값을 훑지 않도록 시드를 떼어 둔다.
            // 생성자에서 뽑으면 안 된다 — CGameManager(MonoBehaviour)의 필드 초기화에서 new 되는데,
            // Unity는 그 시점에 Random을 부르면 예외를 던지고 뒤따르는 필드(m_cAudioManager 등)가 null로 남는다.
            m_fSeedX = Random.Range(0f, 1000f);
            m_fSeedY = Random.Range(0f, 1000f);
        }

        // 260916_옵션창은 아직 없지만 끄는 자리는 미리 하나로 못박아 둔다.
        // 나중에 옵션창이 생기면 이 스위치 하나만 토글하면 된다 — CProgress_Manager.Set_FreeSpend와 같은 자리다.
        /// <summary> 꺼져 있으면 트라우마를 안 쌓고, 남아 있던 것도 즉시 지운다. </summary>
        public void Set_Enabled(bool bEnabled)
        {
            m_bEnabled = bEnabled;
            if (bEnabled == false)
                m_fTrauma = 0f;
        }

        /// <summary> 스테이지를 새로 깔 때 부른다. 이전 판의 흔들림이 다음 판으로 넘어가지 않게 한다. </summary>
        public void Reset() => m_fTrauma = 0f;

        /// <summary>
        /// 흔들림 원인이 생겼을 때 부른다. 여러 번 겹치면 누적된다(최대 1) —
        /// 동시에 여러 대 맞으면 더 세게 흔들리는 쪽이 자연스럽다.
        /// </summary>
        /// <param name="fAmount"> 더할 트라우마 양 0~1. CGameConfig의 TRAUMA_ON_* 값을 그대로 넘긴다 </param>
        public void Add_Trauma(float fAmount)
        {
            if (m_bEnabled == false || fAmount <= 0f)
                return;

            m_fTrauma = Mathf.Clamp01(m_fTrauma + fAmount);
        }

        /// <returns> 이번 프레임 카메라 위치에 더할 오프셋(월드 단위). 흔들 게 없으면 (0,0) </returns>
        public Vector2 Tick(float fDeltaTime)
        {
            if (m_fTrauma <= 0f)
                return Vector2.zero;

            m_fTrauma     = CCameraFeel_Utility.Decay(m_fTrauma, m_fDecayPerSecond, fDeltaTime);
            m_fNoiseTime += fDeltaTime * NOISE_SPEED;

            return Calc_Offset(m_fTrauma, m_fNoiseTime, m_fSeedX, m_fSeedY, m_fMaxOffset);
        }

        // 260916_화면 없이 검증하려고 순수 함수로 뺐다(CCameraFitter.Calc_Size와 같은 이유).
        public static Vector2 Calc_Offset(float fTrauma, float fNoiseTime, float fSeedX, float fSeedY, float fMaxOffset)
        {
            float fShake = CCameraFeel_Utility.Curve(fTrauma);

            // 펄린 노이즈로 -1~1을 매끄럽게 오간다. Random.value로 매 프레임 뽑으면
            // 흔들리는 게 아니라 순간이동하는 것처럼 보인다.
            float fX = Mathf.PerlinNoise(fSeedX, fNoiseTime) * 2f - 1f;
            float fY = Mathf.PerlinNoise(fSeedY, fNoiseTime) * 2f - 1f;

            return new Vector2(fX, fY) * fShake * fMaxOffset;
        }
    }
}
