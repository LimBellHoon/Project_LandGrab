using UnityEngine;

using Engine;

namespace Client
{
    // 260916_런 스킬 '영혼 수집가'가 떨어뜨리는 픽업 — CWeb과 같은 자리(제자리에 머무르다 사라짐)
    /// <summary>
    /// 플레이어와의 충돌 판정(자석 범위 포함)은 CStage_Manager가 한다 — 몬스터/탄/거미줄과
    /// 마찬가지로 플레이어를 만지는 곳을 한군데로 모으기 위해서다. 여기서는 위치와 수명만 안다.
    /// </summary>
    public class CSoul : CGameObject
    {
        [SerializeField] private SpriteRenderer m_srBody;

        private CTerritoryGrid  m_cGrid;
        private Vector2Int      m_vCell;
        private float           m_fLifeTime;
        private float           m_fMaxLifeTime;     // 옅어지는 정도를 계산할 기준

        public Vector2       POS         => m_cGrid != null ? m_cGrid.Cell_ToWorld(m_vCell) : Vector2.zero;
        /// <summary> Engine이 bCollect가 선 오브젝트를 알아서 풀로 돌려준다 (CProjectile 설명 참고). </summary>
        public bool          IS_EXPIRED  => bCollect;

        #region Engine.CGameObject
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CSoulDesc cDesc) == false)
            {
                Debug.LogError("[CSoul] CSoulDesc가 아닙니다.");
                return false;
            }

            m_cGrid = cDesc.cGrid;
            if (m_cGrid == null)
            {
                Debug.LogError("[CSoul] Grid가 null 입니다.");
                return false;
            }

            m_vCell        = cDesc.vCell;
            m_fLifeTime    = cDesc.fLifeTime;
            m_fMaxLifeTime = Mathf.Max(0.01f, cDesc.fLifeTime);
            bCollect       = false;     // 풀에서 재사용되므로 반드시 내려 둔다

            Refresh_Alpha();

            transform.position   = m_cGrid.Cell_ToWorld(m_vCell);
            transform.localScale = Vector3.one * m_cGrid.CELL_SIZE * 1.2f;
            return true;
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cGrid == null || bCollect == true)
                return;

            m_fLifeTime -= fDeltaTime;

            // 그 칸이 점령돼 버리면 더 주울 수 없으므로 남아 있을 이유가 없다.
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

        /// <summary> 플레이어가 주웠을 때처럼 밖에서 끝내야 할 때 부른다. </summary>
        public void Expire() => bCollect = true;

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
