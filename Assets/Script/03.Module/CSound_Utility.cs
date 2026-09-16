using UnityEngine;

namespace Client
{
    // 260916_효과음이 아직 없어 파형을 코드로 합성한다 — 텍스처를 CProtoSetup이 절차적으로
    // 그려 두는 것과 같은 자리다. 진짜 사운드가 오면 이 파일은 그대로 두고 CAudio_Manager의
    // 재생 소스만 갈아 끼우면 된다.
    /// <summary>
    /// 파형을 숫자 배열로만 만든다(UnityEngine.AudioClip을 몰라도 된다) — 화면 없이
    /// CProtoTest에서 검증하기 위해서다. AudioClip으로 감싸는 건 CAudio_Manager가 한다.
    /// </summary>
    public enum WAVE_SHAPE
    {
        SINE,
        SQUARE,
        TRIANGLE,
        SAWTOOTH,
    }

    public static class CSound_Utility
    {
        public const int SAMPLE_RATE = 22050;      // 효과음이라 44100까지 필요 없다 — 절반이면 용량도 절반이다
        private const float FADE_TIME = 0.006f;    // 시작/끝을 이 시간만큼 무음에서 감아 올려 클릭음을 없앤다

        /// <summary>
        /// 짧은 톤 하나를 샘플 배열로 만든다. 시작음(fStartFreq)에서 끝음(fEndFreq)까지
        /// 선형으로 미끄러지므로, 둘을 같게 두면 일정한 음이고 다르게 두면 삐-용 하는 효과가 난다
        /// (사망=하강, 점령=상승처럼 방향만으로도 좋고 나쁨이 읽힌다).
        /// </summary>
        /// <param name="eShape"> 파형. SINE이 가장 부드럽고 SQUARE/SAWTOOTH가 거칠다 </param>
        /// <param name="fStartFreq"> 시작 음높이(Hz) </param>
        /// <param name="fEndFreq"> 끝 음높이(Hz) </param>
        /// <param name="fDuration"> 길이(초) </param>
        /// <param name="fVolume"> 0~1. 최종 재생 볼륨과 곱해지는 것과 별개로 톤 자체의 상대 크기 </param>
        public static float[] Generate_Tone(WAVE_SHAPE eShape, float fStartFreq, float fEndFreq,
                                            float fDuration, float fVolume = 1f)
        {
            int iSampleCount = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(0.01f, fDuration) * SAMPLE_RATE));
            float[] arrSample = new float[iSampleCount];

            float fVol   = Mathf.Clamp01(fVolume);
            int   iFadeN = Mathf.Clamp(Mathf.RoundToInt(FADE_TIME * SAMPLE_RATE), 1, iSampleCount / 2);

            double dPhase = 0.0;
            for (int i = 0; i < iSampleCount; ++i)
            {
                float fT    = (float)i / SAMPLE_RATE;
                float fFreq = Mathf.Lerp(fStartFreq, fEndFreq, iSampleCount <= 1 ? 0f : (float)i / (iSampleCount - 1));
                dPhase += fFreq / SAMPLE_RATE;

                float fWave = Sample_Wave(eShape, (float)(dPhase % 1.0));

                // 260916_시작·끝을 선형으로 감아 클릭(뚝) 소리를 없앤다.
                float fEnvelope = 1f;
                if (i < iFadeN)
                    fEnvelope = (float)i / iFadeN;
                else if (i >= iSampleCount - iFadeN)
                    fEnvelope = (float)(iSampleCount - 1 - i) / iFadeN;

                arrSample[i] = fWave * fVol * fEnvelope;
            }

            return arrSample;
        }

        // 위상(0~1)을 받아 그 파형의 진폭(-1~1)을 낸다.
        private static float Sample_Wave(WAVE_SHAPE eShape, float fPhase)
        {
            switch (eShape)
            {
                case WAVE_SHAPE.SQUARE:   return fPhase < 0.5f ? 1f : -1f;
                case WAVE_SHAPE.TRIANGLE: return 1f - 4f * Mathf.Abs(Mathf.Repeat(fPhase + 0.75f, 1f) - 0.5f);
                case WAVE_SHAPE.SAWTOOTH: return 2f * fPhase - 1f;
                default:                  return Mathf.Sin(fPhase * Mathf.PI * 2f);   // SINE
            }
        }
    }
}
