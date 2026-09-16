using UnityEngine;

namespace Client
{
    // 260916_카메라 흔들림 / 펀치줌이 같은 모양의 계산을 반복해서 뺐다(1-1).
    /// <summary>
    /// "0~1 값이 쌓였다가 시간이 지나면 스스로 줄어든다"는 모양을 여러 카메라 연출이
    /// 똑같이 쓴다 — <see cref="CCameraShake"/>(위치)와 <see cref="CCameraPunch"/>(줌)가 그렇다.
    /// 둘 다 계산만 여기서 빌리고, 무엇에 적용할지는 각자 안다.
    /// </summary>
    public static class CCameraFeel_Utility
    {
        /// <summary> 초당 fDecayPerSecond만큼 줄인다. 0 밑으로는 안 내려간다. </summary>
        public static float Decay(float fValue, float fDecayPerSecond, float fDeltaTime)
        {
            return Mathf.Max(0f, fValue - fDecayPerSecond * fDeltaTime);
        }

        /// <summary>
        /// 쌓인 양을 화면에 보이는 세기로 바꾼다. 제곱(2)을 쓰면 살짝 쌓였을 땐 거의 안 보이다가
        /// 많이 쌓이면 급격히 커진다 — 트라우마 기반 카메라 연출의 표준적인 변환이다.
        /// </summary>
        public static float Curve(float fValue, float fPower = 2f)
        {
            return Mathf.Pow(Mathf.Clamp01(fValue), fPower);
        }
    }
}
