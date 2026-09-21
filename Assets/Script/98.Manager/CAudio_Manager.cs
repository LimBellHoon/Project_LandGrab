using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260916_효과음 — 절차적 플레이스홀더
    /// <summary>
    /// 지금은 효과음 파일이 하나도 없어 <see cref="CSound_Utility"/>로 톤을 합성해 쓴다.
    /// 진짜 SFX가 오면 <see cref="Build_Clip"/> 안쪽만 "합성" 대신 "에셋 로드"로 바꾸면 되고,
    /// 호출부(<c>Play(SOUND_ID)</c>)는 그대로다 — Engine이 텍스처/프리팹은 Addressable로
    /// 실어 나르지만 오디오 클립을 위한 홀더는 없어서(1-4로 DLL을 직접 확인했다),
    /// 오디오는 처음부터 클라이언트가 전담한다.
    ///
    /// 어떤 파형·음높이를 쓸지는 소리의 정체성이지 세기 값이 아니므로 CGameConfig가 아니라
    /// 여기 표(<see cref="s_dicDef"/>)에 상수로 둔다 — CUI_InGame의 플래시 색과 같은 자리(2-10-3).
    /// 켬/끔과 전체 볼륨만 CGameConfig에서 조절한다.
    /// </summary>
    public class CAudio_Manager
    {
        // 260916_새 효과음을 늘릴 때는 SOUND_ID에 값 하나, 여기 표에 줄 하나만 추가하면 된다.
        // eWave/시작음/끝음으로 방향(상승=상승감, 하강=실패감)만 잡아도 종류가 잘 읽힌다.
        private readonly struct CSoundDef
        {
            public readonly WAVE_SHAPE eWave;
            public readonly float      fStartFreq;
            public readonly float      fEndFreq;
            public readonly float      fDuration;
            public readonly float      fVolume;    // 톤끼리의 상대 크기 0~1

            public CSoundDef(WAVE_SHAPE eWave, float fStartFreq, float fEndFreq, float fDuration, float fVolume)
            {
                this.eWave      = eWave;
                this.fStartFreq = fStartFreq;
                this.fEndFreq   = fEndFreq;
                this.fDuration  = fDuration;
                this.fVolume    = fVolume;
            }
        }

        private static readonly Dictionary<SOUND_ID, CSoundDef> s_dicDef = new Dictionary<SOUND_ID, CSoundDef>
        {
            { SOUND_ID.HIT,         new CSoundDef(WAVE_SHAPE.SQUARE,   220f,  160f, 0.10f, 0.70f) },
            { SOUND_ID.DEATH,       new CSoundDef(WAVE_SHAPE.SAWTOOTH, 300f,   80f, 0.45f, 0.85f) },
            { SOUND_ID.EVADE,       new CSoundDef(WAVE_SHAPE.SINE,     700f,  900f, 0.12f, 0.55f) },
            { SOUND_ID.CAPTURE,     new CSoundDef(WAVE_SHAPE.SINE,     520f,  900f, 0.15f, 0.60f) },
            { SOUND_ID.CARD_READY,  new CSoundDef(WAVE_SHAPE.TRIANGLE, 600f,  700f, 0.20f, 0.55f) },
            { SOUND_ID.STAGE_CLEAR, new CSoundDef(WAVE_SHAPE.SINE,     500f, 1000f, 0.50f, 0.85f) },
            { SOUND_ID.STAGE_FAIL,  new CSoundDef(WAVE_SHAPE.SAWTOOTH, 260f,  100f, 0.50f, 0.85f) },
            // 260918_전체 마비 — 높은 데서 뚝 떨어지는 사각파. 세상이 멈추는 느낌
            { SOUND_ID.MASS_STUN,   new CSoundDef(WAVE_SHAPE.SQUARE,  1400f,  180f, 0.35f, 0.75f) },
            // 260921_시작 위치 슬롯 — 짧은 딸깍, 멈추면 올라가는 한 음
            { SOUND_ID.SLOT_TICK,   new CSoundDef(WAVE_SHAPE.SQUARE,   900f,  900f, 0.03f, 0.35f) },
            { SOUND_ID.SLOT_STOP,   new CSoundDef(WAVE_SHAPE.TRIANGLE, 500f, 1100f, 0.25f, 0.70f) },
        };

        private readonly Dictionary<SOUND_ID, AudioClip> m_dicClip = new Dictionary<SOUND_ID, AudioClip>();

        private AudioSource m_cSource;
        private GameObject  m_goPlayer;
        private bool        m_bEnabled = true;
        private float       m_fVolume  = 1f;

        /// <summary> 스테이지와 상관없이 앱이 사는 동안 한 번만 부른다(로비 진입 전, GameLogic_Async에서). </summary>
        public void Initialize()
        {
            m_goPlayer = new GameObject("SFX_Player");
            Object.DontDestroyOnLoad(m_goPlayer);

            m_cSource = m_goPlayer.AddComponent<AudioSource>();
            m_cSource.playOnAwake = false;
            m_cSource.spatialBlend = 0f;   // 2D — 카메라 위치와 상관없이 같은 크기로 들린다

            // 260916_씬에 리스너가 있을 확률이 높지만(Setup_Camera가 만든 카메라), 없어도
            // 조용히 안 들리기만 해야지 예외로 죽으면 안 된다 — 여기서 보강해 둔다.
            if (Camera.main != null && Camera.main.GetComponent<AudioListener>() == null)
                Camera.main.gameObject.AddComponent<AudioListener>();

            // 260916_한 번만 합성해 두고 재생 때는 캐시에서 꺼내 쓴다 — 매번 합성하면 끊긴다.
            foreach (KeyValuePair<SOUND_ID, CSoundDef> cPair in s_dicDef)
                m_dicClip[cPair.Key] = Build_Clip(cPair.Key, cPair.Value);
        }

        public void Release()
        {
            if (m_goPlayer != null)
                Object.Destroy(m_goPlayer);

            m_goPlayer = null;
            m_cSource  = null;
            m_dicClip.Clear();
        }

        // 260916_옵션창은 아직 없다. CCameraShake.Set_Enabled와 같은 자리 — 켬/끔·볼륨을
        // 미리 하나로 못박아 두고, 지금은 CGameConfig 값을 그대로 흘려보낸다.
        public void Set_Enabled(bool bEnabled) => m_bEnabled = bEnabled;
        public void Set_Volume(float fVolume)  => m_fVolume = Mathf.Clamp01(fVolume);

        /// <summary> 여러 개가 겹쳐도 서로를 끊지 않는다(AudioSource.PlayOneShot). </summary>
        public void Play(SOUND_ID eSoundID)
        {
            if (m_bEnabled == false || m_cSource == null || eSoundID == SOUND_ID.NONE)
                return;

            if (m_dicClip.TryGetValue(eSoundID, out AudioClip cClip) == false || cClip == null)
                return;

            float fVolume = m_fVolume * (s_dicDef.TryGetValue(eSoundID, out CSoundDef cDef) ? cDef.fVolume : 1f);
            m_cSource.PlayOneShot(cClip, fVolume);
        }

        private static AudioClip Build_Clip(SOUND_ID eSoundID, CSoundDef cDef)
        {
            float[] arrSample = CSound_Utility.Generate_Tone(cDef.eWave, cDef.fStartFreq, cDef.fEndFreq, cDef.fDuration);

            AudioClip cClip = AudioClip.Create($"SFX_{eSoundID}", arrSample.Length, 1,
                                               CSound_Utility.SAMPLE_RATE, false);
            cClip.SetData(arrSample, 0);
            return cClip;
        }
    }
}
