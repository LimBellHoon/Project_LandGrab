using UnityEngine;

using Engine;

namespace Client
{
    // 260920_맵 위 상호작용 아이템 — 지나가면서 주우면 그 자리에서 효과가 터진다.
    /// <summary>
    /// CSoul과 같은 자리(제자리에 머무르다 수명이 다하면 사라짐)이지만 목적이 달라 따로 만들었다 —
    /// 영혼은 '주울수록 빨라지는 누적 보상'이고, 이쪽은 '지금 이 순간을 뒤집는 한 방'이다(2-19 설계).
    /// 무엇을 하는지는 이 클래스가 모른다. 줍는 판정도 효과도 CStage_Manager가 한곳에서 본다 —
    /// 몬스터 · 탄 · 거미줄 · 영혼과 같은 규칙이다. 여기서는 위치 · 수명 · 겉모습만 안다.
    /// 종류별로 프리팹을 만들지 않고 색만 바꾼다 — CEnemy가 기믹별 색을 쓰는 것과 같은 자리(2-6).
    /// </summary>
    public class CFieldItem : CGameObject
    {
        [SerializeField] private SpriteRenderer m_srBody;

        private CTerritoryGrid  m_cGrid;
        private Vector2Int      m_vCell;
        private float           m_fLifeTime;
        private float           m_fMaxLifeTime;
        private Color           m_cColor = Color.white;

        public FIELD_ITEM_TYPE TYPE    { get; private set; }
        public int             ITEM_ID { get; private set; }

        public Vector2 POS        => m_cGrid != null ? m_cGrid.Cell_ToWorld(m_vCell) : Vector2.zero;
        /// <summary> Engine이 bCollect가 선 오브젝트를 알아서 풀로 돌려준다 (CSoul과 같다). </summary>
        public bool    IS_EXPIRED => bCollect;

        #region Engine.CGameObject
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CFieldItemDesc cDesc) == false)
            {
                Debug.LogError("[CFieldItem] CFieldItemDesc가 아닙니다.");
                return false;
            }

            m_cGrid = cDesc.cGrid;
            if (m_cGrid == null)
            {
                Debug.LogError("[CFieldItem] Grid가 null 입니다.");
                return false;
            }

            TYPE           = cDesc.eType;
            ITEM_ID        = cDesc.iItemID;
            m_vCell        = cDesc.vCell;
            m_fLifeTime    = cDesc.fLifeTime;
            m_fMaxLifeTime = Mathf.Max(0.01f, cDesc.fLifeTime);
            m_cColor       = Get_Color(cDesc.eType);
            bCollect       = false;     // 풀에서 재사용되므로 반드시 내려 둔다

            Refresh_Color();

            transform.position   = m_cGrid.Cell_ToWorld(m_vCell);
            transform.localScale = Vector3.one * m_cGrid.CELL_SIZE * 1.6f;   // 영혼보다 크게 — 꼭 주워야 하는 것이다
            return true;
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cGrid == null || bCollect == true)
                return;

            m_fLifeTime -= fDeltaTime;

            // 260920_점령된 칸이어도 여기서 지우지 않는다 — 점령한 땅 안의 픽업은 '사라지는 것'이 아니라
            // '먹은 것'이고, 그 판정은 CStage_Manager가 한다(줍는 곳을 한군데로 모은다).
            if (m_fLifeTime <= 0f)
            {
                bCollect = true;
                return;
            }

            Refresh_Color();
        }

        public override void Hide()
        {
            m_cGrid = null;
            base.Hide();
        }
        #endregion Engine.CGameObject

        /// <summary> 주웠을 때처럼 밖에서 끝내야 할 때 부른다. </summary>
        public void Expire() => bCollect = true;

        /// <summary> 종류를 색으로 가른다. 글자를 읽을 틈이 없으므로 색만으로 무엇인지 알아야 한다. </summary>
        public static Color Get_Color(FIELD_ITEM_TYPE eType)
        {
            switch (eType)
            {
                case FIELD_ITEM_TYPE.MASS_STUN:  return new Color(1f, 0.92f, 0.35f);    // 번개 — 노랑
                case FIELD_ITEM_TYPE.HEAL_LIFE:  return new Color(1f, 0.35f, 0.45f);    // 하트 — 빨강
                case FIELD_ITEM_TYPE.MAGNET_ALL: return new Color(0.45f, 0.75f, 1f);    // 자석 — 파랑
                default:                         return Color.white;
            }
        }

        // 사라지기 직전 옅어지고, 평소에는 은은하게 깜빡여 눈에 띈다.
        private void Refresh_Color()
        {
            if (m_srBody == null)
                return;

            float fBlink = 0.8f + 0.2f * Mathf.Sin(Time.time * 6f);
            Color cColor = m_cColor * fBlink;
            cColor.a = Mathf.Clamp01(m_fLifeTime / m_fMaxLifeTime * 2f);
            m_srBody.color = cColor;
        }
    }
}
