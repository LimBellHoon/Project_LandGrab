using System.Collections.Generic;

namespace Client
{
    // 260916_런 전용 스킬(뱀서라이크) — 여러 개를 동시에 들고 레벨업한다.
    /// <summary>
    /// 장비/강화 스킬을 재는 <see cref="CSkillHandler"/>와 짝이지만 성격이 다르다 —
    /// 저쪽은 '장착 스킬 하나의 쿨타임'만 재고, 이쪽은 '지금 들고 있는 런 스킬들의 레벨'만 센다.
    /// 실제 효과는 여기서 만들지 않는다 — <see cref="CRunSkillEffect"/>가 붙는다(같은 조합 구조, 2-6/2-11).
    /// 저장하지 않는다 — 스테이지가 끝나면 <see cref="Clear"/>로 통째로 비운다.
    /// </summary>
    public class CRunSkillHandler
    {
        // 260917_한 판에 얻을 수 있는 수 — 액티브 5개 / 패시브 5개 (Design_RunSkill_Awaken 2장 규칙 1).
        // 이미 가진 스킬의 레벨업은 이 수와 상관없다.
        public const int SLOT_PER_CATEGORY = 5;

        private readonly Dictionary<RUN_SKILL_TYPE, int> m_dicLevel = new Dictionary<RUN_SKILL_TYPE, int>();
        // 260917_각성한 액티브. 액티브 하나는 한 번만 각성한다 — 판마다 Clear로 함께 비운다.
        private readonly HashSet<RUN_SKILL_TYPE> m_hsAwakened = new HashSet<RUN_SKILL_TYPE>();

        public IReadOnlyDictionary<RUN_SKILL_TYPE, int> ALL => m_dicLevel;

        public int Get_Level(RUN_SKILL_TYPE eType)
            => m_dicLevel.TryGetValue(eType, out int iLevel) ? iLevel : 0;

        public bool Has(RUN_SKILL_TYPE eType) => Get_Level(eType) > 0;

        public bool Is_Awakened(RUN_SKILL_TYPE eType) => m_hsAwakened.Contains(eType);

        /// <returns> 들고 있지 않거나 이미 각성했으면 false </returns>
        public bool Awaken(RUN_SKILL_TYPE eType)
        {
            if (Has(eType) == false)
                return false;

            return m_hsAwakened.Add(eType);
        }

        /// <summary> 새로 얻으면 1레벨, 이미 있으면 다음 레벨로 — 만렙이면 더 오르지 않는다. </summary>
        /// <returns> 적용된 이후 레벨. </returns>
        public int Add_Or_LevelUp(CRunSkillInfo cInfo)
        {
            if (cInfo == null)
                return 0;

            int iNext = System.Math.Min(cInfo.iMaxLevel, Get_Level(cInfo.eType) + 1);
            m_dicLevel[cInfo.eType] = iNext;
            return iNext;
        }

        public void Clear()
        {
            m_dicLevel.Clear();
            m_hsAwakened.Clear();
        }
    }
}
