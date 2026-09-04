using UnityEngine;

using Engine;

namespace Client
{
    // 260904_거미줄 — 밟은 플레이어를 느리게 만드는 덫
    /// <summary>
    /// 제자리에 머무르다 수명이 다하면 사라진다.
    /// 플레이어가 그 칸을 점령해 버리면 남아 있을 이유가 없으므로 그때도 사라진다.
    /// 감속 적용은 CStage_Manager가 한다 — 플레이어를 만지는 곳을 한군데로 모으기 위해서다.
    /// </summary>
    public class CWeb : CGameObject
    {
        [SerializeField] private SpriteRenderer m_srBody;

        private CTerritoryGrid  m_cGrid;
        private Vector2Int      m_vCell;
        private float           m_fLifeTime;
        private float           m_fMaxLifeTime;     // 옅어지는 정도를 계산할 기준
        private float           m_fSlowRatio;

        public Vector2Int   CELL        => m_vCell;
        /// <summary> 플레이어 속도에 곱할 값. 1이면 감속 없음. </summary>
        public float        SLOW_RATIO  => m_fSlowRatio;
        /// <summary> Engine이 bCollect가 선 오브젝트를 알아서 풀로 돌려준다 (CProjectile 설명 참고). </summary>
        public bool         IS_EXPIRED  => bCollect;

        #region Engine.CGameObject
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CWebDesc cDesc) == false)
            {
                Debug.LogError("[CWeb] CWebDesc가 아닙니다.");
                return false;
            }

            m_cGrid = cDesc.cGrid;
            if (m_cGrid == null)
            {
                Debug.LogError("[CWeb] Grid가 null 입니다.");
                return false;
            }

            m_vCell      = cDesc.vCell;
            m_fLifeTime    = cDesc.fLifeTime;
            m_fMaxLifeTime = Mathf.Max(0.01f, cDesc.fLifeTime);
            m_fSlowRatio   = Mathf.Clamp(cDesc.fSlowRatio, 0.1f, 1f);
            bCollect       = false;     // 풀에서 재사용되므로 반드시 내려 둔다

            Refresh_Alpha();

            transform.position   = m_cGrid.Cell_ToWorld(m_vCell);
            transform.localScale = Vector3.one * m_cGrid.CELL_SIZE * 2.2f;
            return true;
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cGrid == null || bCollect == true)
                return;

            m_fLifeTime -= fDeltaTime;

            // 플레이어가 그 칸을 점령해 버리면 남아 있을 이유가 없다.
            if (m_fLifeTime <= 0f || m_cGrid.Get_Cell(m_vCell) != CELL_STATE.EMPTY)
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

        // 사라지기 직전 옅어져서 '곧 없어진다'가 보이게 한다.
        private void Refresh_Alpha()
        {
            if (m_srBody == null)
                return;

            Color cColor = m_srBody.color;
            cColor.a = Mathf.Clamp01(m_fLifeTime / m_fMaxLifeTime * 2f);
            m_srBody.color = cColor;
        }
    }
}
