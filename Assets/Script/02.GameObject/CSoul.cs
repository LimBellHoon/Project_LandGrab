using UnityEngine;

using Engine;

namespace Client
{
    // 260916_런 스킬 '영혼 수집가'가 떨어뜨리는 픽업 — 주울 때마다 그 판 한정으로 빨라진다.
    /// <summary> 260920_위치 · 수명 · 옅어짐은 CPickup이 맡는다. 여기 남은 것은 겉모습뿐이다. </summary>
    public class CSoul : CPickup
    {
        protected override float SCALE => 1.2f;

        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CSoulDesc cDesc) == false)
            {
                Debug.LogError("[CSoul] CSoulDesc가 아닙니다.");
                return false;
            }

            return Setup_Pickup(cDesc.cGrid, cDesc.vCell, cDesc.fLifeTime);
        }
    }
}
