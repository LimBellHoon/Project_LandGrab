using UnityEngine;

using Engine;

namespace Client
{
    // 260920_점령 조각 — 3지선다 게이지를 올리는 유일한 수단이다(2-21).
    /// <summary>
    /// 점령하면 먹은 칸 수만큼 **미점령 지역에** 뿌려지고, 몬스터를 잡아도 그 자리에 떨어진다.
    /// 즉 보상이 늘 '방금 행동한 곳이 아니라 위험한 바깥'에 생기고, 주우러 나가야 성장한다 —
    /// 점령률이 저절로 게이지를 채우던 예전 방식에는 이 왕복이 없었다.
    ///
    /// **수명이 없다.** 그 웨이브 안에는 언제든 주우러 갈 수 있고, 웨이브가 넘어가며 판을 다시 깔 때
    /// 함께 사라진다(2-5) — 타이머 없이도 '이번 판에 주워야 한다'가 생긴다.
    /// </summary>
    public class CShard : CPickup
    {
        private static readonly Color COLOR_SHARD = new Color(0.75f, 1f, 0.55f);

        /// <summary> 이 조각 하나가 채우는 게이지 양. </summary>
        public int VALUE { get; private set; }

        protected override float SCALE => 0.9f;     // 영혼 · 아이템보다 작다. 여러 개가 흩뿌려지는 것이다
        protected override Color COLOR => COLOR_SHARD;

        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CShardDesc cDesc) == false)
            {
                Debug.LogError("[CShard] CShardDesc가 아닙니다.");
                return false;
            }

            VALUE = Mathf.Max(1, cDesc.iValue);

            return Setup_Pickup(cDesc.cGrid, cDesc.vCell, 0f);      // 0 = 수명 없음
        }
    }
}
