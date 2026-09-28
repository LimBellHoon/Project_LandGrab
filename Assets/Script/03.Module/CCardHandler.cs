using System.Collections.Generic;

namespace Client
{
    // 260928_카드 시스템 재작성(Docs/Design_Roguelite_Rewrite.md 6장) — 새 카드는 런 스킬처럼 여러 개를
    // 동시에 들고 레벨업한다(사용자 확인 — 런 스킬 시스템을 이 구조로 흡수한다).
    /// <summary>
    /// 지금 들고 있는 카드들의 레벨만 센다. 실제 효과는 <see cref="CCardEffect"/>가 붙는다(조합 구조, 1-1) —
    /// <see cref="CRunSkillHandler"/>와 거의 같은 모양이다. 저장하지 않는다 — 스테이지가 끝나면
    /// <see cref="Clear"/>로 통째로 비운다.
    ///
    /// 이번(태스크 #21)은 프레임만 만든다 — 실제 카드 30종과 CSV 표는 태스크 #22가 채운다.
    /// 그때까지 이 클래스는 <c>CPlayer</c>에 아직 붙어 있지 않다(아직 만들 카드가 없다). 옛 카드
    /// (<c>CardInfo.csv</c> + <c>CStage_Manager.Apply_Card</c>의 즉시효과 스위치문)와 옛 런 스킬
    /// (<c>RunSkillInfo.csv</c> + <c>CRunSkillHandler</c>)은 그대로 살아 있다 — 새 카드 30종이 이 프레임
    /// 위에서 전부 자리 잡은 뒤에 한 번에 걷어낼 것(지금 지우면 아무것도 대신할 게 없어 화면이 죽는다).
    /// </summary>
    public class CCardHandler
    {
        private readonly Dictionary<CARD_TYPE, int> m_dicLevel = new Dictionary<CARD_TYPE, int>();

        public IReadOnlyDictionary<CARD_TYPE, int> ALL => m_dicLevel;

        public int Get_Level(CARD_TYPE eType)
            => m_dicLevel.TryGetValue(eType, out int iLevel) ? iLevel : 0;

        public bool Has(CARD_TYPE eType) => Get_Level(eType) > 0;

        /// <summary> 새로 얻으면 1레벨, 이미 있으면 다음 레벨로 — iMaxLevel을 넘지 않는다. </summary>
        /// <returns> 적용된 이후 레벨. </returns>
        public int Add_Or_LevelUp(CARD_TYPE eType, int iMaxLevel)
        {
            int iNext = System.Math.Min(iMaxLevel, Get_Level(eType) + 1);
            m_dicLevel[eType] = iNext;
            return iNext;
        }

        public void Clear() => m_dicLevel.Clear();
    }
}
