using UnityEngine;

using Engine;

namespace Client
{
    // 260920_맵 위에 놓이는 픽업의 공통 뼈대 — 영혼(CSoul) · 아이템(CFieldItem) · 조각(CShard)이 같이 쓴다.
    /// <summary>
    /// 셋 다 하는 일이 같다 — 한 칸에 놓이고, 수명이 있으면 옅어지다 사라지고, 밖에서 끝낼 수 있다.
    /// 세 클래스에 같은 코드를 세 번 쓰게 되어 여기로 모았다(1-1).
    ///
    /// **플레이어와 닿았는지는 여기서 보지 않는다.** 줍는 판정도 효과도 CStage_Manager가 한곳에서 본다 —
    /// 몬스터 · 탄 · 거미줄과 같은 규칙이다. 파생 클래스는 '무엇인가'(종류 · 값 · 겉모습)만 더한다.
    /// </summary>
    public abstract class CPickup : CGameObject
    {
        [SerializeField] protected SpriteRenderer m_srBody;

        private CTerritoryGrid  m_cGrid;
        private Vector2Int      m_vCell;
        private float           m_fLifeTime;
        private float           m_fMaxLifeTime;     // 옅어지는 정도를 계산할 기준

        public Vector2    POS  => m_cGrid != null ? m_cGrid.Cell_ToWorld(m_vCell) : Vector2.zero;
        public Vector2Int CELL => m_vCell;
        /// <summary> Engine이 bCollect가 선 오브젝트를 알아서 풀로 돌려준다 (CProjectile 설명 참고). </summary>
        public bool       IS_EXPIRED => bCollect;

        /// <summary> 셀 크기에 곱할 배율. 눈에 띄어야 하는 픽업일수록 크게 잡는다. </summary>
        protected virtual float SCALE => 1.2f;
        /// <summary> 스프라이트에 곱할 색. 종류를 색으로 가르는 픽업이 덮어쓴다. </summary>
        protected virtual Color COLOR => Color.white;

        #region Engine.CGameObject
        /// <summary> 파생 클래스는 자기 Desc를 읽은 뒤 이 함수로 공통 부분을 세운다. </summary>
        protected bool Setup_Pickup(CTerritoryGrid cGrid, Vector2Int vCell, float fLifeTime)
        {
            if (cGrid == null)
            {
                Debug.LogError($"[{GetType().Name}] Grid가 null 입니다.");
                return false;
            }

            m_cGrid        = cGrid;
            m_vCell        = vCell;
            m_fLifeTime    = fLifeTime;
            m_fMaxLifeTime = Mathf.Max(0.01f, fLifeTime);
            bCollect       = false;     // 풀에서 재사용되므로 반드시 내려 둔다

            Refresh_Color();

            transform.position   = cGrid.Cell_ToWorld(vCell);
            transform.localScale = Vector3.one * cGrid.CELL_SIZE * SCALE;
            return true;
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cGrid == null || bCollect == true)
                return;

            // 260920_수명 0은 무한이다 — 조각처럼 '언젠가는 주우러 와야 하는' 픽업에 쓴다(탄의 fLifeTime과 같은 규칙, 2-15).
            if (m_fMaxLifeTime > 0f && m_fLifeTime > 0f)
            {
                m_fLifeTime -= fDeltaTime;
                if (m_fLifeTime <= 0f)
                {
                    bCollect = true;
                    return;
                }
            }

            // 260920_점령된 칸이어도 여기서 지우지 않는다 — 점령한 땅 안의 픽업은 '사라지는 것'이 아니라
            // '먹은 것'이고, 그 판정은 CStage_Manager가 한다.
            Refresh_Color();
        }

        public override void Hide()
        {
            m_cGrid = null;
            base.Hide();
        }
        #endregion Engine.CGameObject

        /// <summary> 플레이어가 주웠을 때처럼 밖에서 끝내야 할 때 부른다. </summary>
        public void Expire() => bCollect = true;

        /// <summary> 사라지기 직전 옅어져서 '곧 없어진다'가 보이게 한다. 수명이 무한이면 늘 또렷하다. </summary>
        protected void Refresh_Color()
        {
            if (m_srBody == null)
                return;

            Color cColor = COLOR;
            cColor.a = m_fLifeTime <= 0f ? 1f : Mathf.Clamp01(m_fLifeTime / m_fMaxLifeTime * 2f);
            m_srBody.color = cColor;
        }
    }
}
