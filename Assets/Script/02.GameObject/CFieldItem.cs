using UnityEngine;

using Engine;

namespace Client
{
    // 260920_맵 위 상호작용 아이템 — 지나가면서 주우면 그 자리에서 효과가 터진다(2-20).
    /// <summary>
    /// CSoul과 같은 픽업(CPickup)이지만 목적이 다르다 — 영혼은 '주울수록 빨라지는 누적 보상'이고,
    /// 이쪽은 '지금 이 순간을 뒤집는 한 방'이다. 무엇을 하는지는 이 클래스가 모른다(CStage_Manager.Apply_FieldItem).
    /// 종류별로 프리팹을 만들지 않고 색만 바꾼다 — CEnemy가 기믹별 색을 쓰는 것과 같은 자리(2-6).
    /// </summary>
    public class CFieldItem : CPickup
    {
        private Color m_cColor = Color.white;

        public FIELD_ITEM_TYPE TYPE    { get; private set; }
        public int             ITEM_ID { get; private set; }

        // 꼭 주워야 하는 것이라 영혼보다 크게, 그리고 은은하게 깜빡여 눈에 띈다.
        protected override float SCALE => 1.6f;
        protected override Color COLOR => m_cColor * (0.8f + 0.2f * Mathf.Sin(Time.time * 6f));

        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CFieldItemDesc cDesc) == false)
            {
                Debug.LogError("[CFieldItem] CFieldItemDesc가 아닙니다.");
                return false;
            }

            TYPE     = cDesc.eType;
            ITEM_ID  = cDesc.iItemID;
            m_cColor = Get_Color(cDesc.eType);

            return Setup_Pickup(cDesc.cGrid, cDesc.vCell, cDesc.fLifeTime);
        }

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
    }
}
