using UnityEngine;

using Engine;

namespace Client
{
    // 260904_몬스터 기믹: 거미줄
    /// <summary>
    /// 투사체가 터진 자리에 일정 시간 남는 장판. 밟은 플레이어를 감속시킨다.
    /// 즉사가 아니라 감속인 이유 — 즉사 장판은 미점령 지대를 통째로 봉쇄해서
    /// 플레이어가 나갈 길을 없애버린다. 감속은 "돌아갈까 뚫을까"의 선택을 만든다.
    /// </summary>
    public class CWeb : CGameObject
    {
        [SerializeField] private SpriteRenderer m_srBody;

        private CTerritoryGrid  m_cGrid;
        private Vector2         m_vPos;
        private float           m_fRadius;      // 월드 유닛
        private float           m_fDuration;
        private float           m_fLifeTime;

        public Vector2  POS         => m_vPos;
        public float    RADIUS      => m_fRadius;
        public float    SLOW_RATE   { get; private set; }
        public float    SLOW_TIME   { get; private set; }

        #region Engine.CGameObject
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CWebDesc cDesc) == false)
            {
                Debug.LogError("[CWeb] CWebDesc가 아닙니다.");
                return false;
            }

            m_cGrid     = cDesc.cGrid;
            m_vPos      = cDesc.vPos;
            m_fRadius   = cDesc.fRadius * m_cGrid.CELL_SIZE;    // Desc는 '셀' 단위
            m_fDuration = Mathf.Max(0.01f, cDesc.fDuration);
            m_fLifeTime = m_fDuration;
            SLOW_RATE   = cDesc.fSlowRate;
            SLOW_TIME   = cDesc.fSlowTime;

            transform.position   = m_vPos;
            transform.localScale = Vector3.one * m_fRadius * 2f;

            Refresh_Alpha();
            return true;
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cGrid == null)
                return;

            m_fLifeTime -= fDeltaTime;

            if (m_fLifeTime <= 0f)
            {
                bCollect = true;
                return;
            }

            Refresh_Alpha();
        }

        public override void Hide()
        {
            m_cGrid = null;
            base.Hide();
        }
        #endregion Engine.CGameObject

        /// <summary> 이 위치가 거미줄 안인가. </summary>
        public bool Is_Inside(Vector2 vWorldPos) => Vector2.Distance(vWorldPos, m_vPos) <= m_fRadius;

        // 사라지기 전에 옅어져서 언제 풀리는지 보이게 한다
        private void Refresh_Alpha()
        {
            if (m_srBody == null)
                return;

            Color cColor = m_srBody.color;
            cColor.a = Mathf.Lerp(0.15f, 0.75f, m_fLifeTime / m_fDuration);
            m_srBody.color = cColor;
        }
    }
}
