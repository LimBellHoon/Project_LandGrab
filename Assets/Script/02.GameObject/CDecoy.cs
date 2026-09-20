using UnityEngine;

using Engine;

namespace Client
{
    // 260921_분신 — 어그로만 끄는 허수아비 (2-11-3)
    /// <summary>
    /// 플레이어가 가던 방향으로 한 획 달리다 수명이 다하거나 몬스터에게 닿으면 사라진다.
    /// **피해를 주지도, 받지도 않는다** — 하는 일은 몬스터의 시선을 잠깐 가져가는 것뿐이다.
    /// 몬스터가 누구를 쫓을지는 CStage_Manager가 정한다(여기는 위치와 수명만 안다) —
    /// 픽업(CPickup)과 같은 이유다.
    ///
    /// 그리드 규칙을 타지 않는다. 선을 긋지도, 점령하지도 않는다 —
    /// 분신이 점령까지 하는 형태는 트레일을 둘로 나눠야 해서 따로 설계할 것(2-11-3 메모).
    /// </summary>
    public class CDecoy : CGameObject
    {
        [SerializeField] private SpriteRenderer m_srBody;

        private static readonly Color COLOR_BODY = new Color(0.65f, 0.85f, 1f, 0.65f);

        private CTerritoryGrid  m_cGrid;
        private Vector2         m_vPos;
        private Vector2         m_vDir;
        private float           m_fSpeed;       // 초당 셀
        private float           m_fLifeTime;
        private float           m_fMaxLifeTime;

        public Vector2 POS        => m_vPos;
        public bool    IS_EXPIRED => bCollect;

        #region Engine.CGameObject
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CDecoyDesc cDesc) == false)
            {
                Debug.LogError("[CDecoy] CDecoyDesc가 아닙니다.");
                return false;
            }

            m_cGrid = cDesc.cGrid;
            if (m_cGrid == null)
            {
                Debug.LogError("[CDecoy] Grid가 null 입니다.");
                return false;
            }

            m_vPos         = cDesc.vStartPos;
            m_vDir         = cDesc.vDir.sqrMagnitude > 0.0001f ? cDesc.vDir.normalized : Vector2.up;
            m_fSpeed       = Mathf.Max(0.1f, cDesc.fSpeed);
            m_fLifeTime    = Mathf.Max(0.1f, cDesc.fLifeTime);
            m_fMaxLifeTime = m_fLifeTime;
            bCollect       = false;     // 풀에서 재사용되므로 반드시 내려 둔다

            transform.position   = m_vPos;
            transform.localScale = Vector3.one * m_cGrid.CELL_SIZE * 1.2f;
            Refresh_Color();
            return true;
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cGrid == null || bCollect == true)
                return;

            m_fLifeTime -= fDeltaTime;
            if (m_fLifeTime <= 0f)
            {
                bCollect = true;
                return;
            }

            // 한 획 — 방향을 바꾸지 않고 곧장 달린다. 맵 끝에 닿으면 거기서 멈춰 어그로만 계속 끈다.
            m_vPos += m_vDir * (m_fSpeed * m_cGrid.CELL_SIZE * fDeltaTime);
            transform.position = m_vPos;

            Refresh_Color();
        }

        public override void Hide()
        {
            m_cGrid = null;
            base.Hide();
        }
        #endregion Engine.CGameObject

        /// <summary> 몬스터에게 닿았을 때처럼 밖에서 끝낼 때 부른다. </summary>
        public void Expire() => bCollect = true;

        // 사라지기 직전 옅어져 '곧 없어진다'가 보이게 한다(픽업과 같은 규칙).
        private void Refresh_Color()
        {
            if (m_srBody == null)
                return;

            Color cColor = COLOR_BODY;
            cColor.a *= Mathf.Clamp01(m_fLifeTime / m_fMaxLifeTime * 2f);
            m_srBody.color = cColor;
        }
    }
}
