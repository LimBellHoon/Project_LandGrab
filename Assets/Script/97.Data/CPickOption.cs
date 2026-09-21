using System;
using System.Collections.Generic;

namespace Client
{
    // 260917_3지선다 한 칸 — 카드(CardInfo)와 런 스킬(RunSkillInfo)을 같은 자리에 섞어 보여 주려고 묶었다
    /// <summary>
    /// 화면(CUI_CardPick)은 이것만 알면 된다. 무엇을 거는지는 고른 쪽(CGameManager → CStage_Manager)이 종류를 보고 정한다.
    /// </summary>
    public class CPickOption
    {
        public PICK_KIND        eKind;
        public CCardInfo        cCard;
        public CRunSkillInfo    cRunSkill;
        public CAwakenInfo      cAwaken;        // 260917_각성. cRunSkill에는 그 액티브를 넣어 둔다(아이콘 · 이름 표시)
        /// <summary> 런 스킬을 고르면 오를 레벨. 1이면 새로 얻는다 </summary>
        public int              iNextLevel;

        public string   NAME    => eKind == PICK_KIND.CARD ? cCard.strName
                                 : eKind == PICK_KIND.AWAKEN ? cAwaken.strName : cRunSkill.strName;
        public string   DESC    => eKind == PICK_KIND.CARD ? cCard.strDesc
                                 : eKind == PICK_KIND.AWAKEN ? cAwaken.strDesc : cRunSkill.strDesc;
        public int      WEIGHT  => eKind == PICK_KIND.CARD ? cCard.iWeight
                                 : eKind == PICK_KIND.AWAKEN ? cAwaken.iWeight : cRunSkill.iWeight;
        public bool     IS_NEW  => eKind == PICK_KIND.RUN_SKILL && iNextLevel <= 1;

        /// <summary>
        /// 260921_같은 선택지인지 가리는 이름. 레벨은 넣지 않는다 — 버린 스킬은 몇 레벨이 되든 다시 안 나와야 한다.
        /// </summary>
        public string   KEY     => eKind == PICK_KIND.CARD ? $"C{cCard.iCardID}"
                                 : eKind == PICK_KIND.AWAKEN ? $"A{cAwaken.iAwakenID}" : $"R{(int)cRunSkill.eType}";

        // 260920_카드 색 테마(2-10-1). 각성은 카드 전체가 보라색이라 테마를 보지 않는다 —
        // 화면이 eKind를 먼저 보고, 각성이 아닐 때만 이 값을 쓴다.
        public PICK_THEME THEME => eKind == PICK_KIND.CARD ? cCard.eTheme
                                 : cRunSkill != null ? cRunSkill.eTheme : PICK_THEME.COMBAT;

        /// <summary> 고르면 되는 레벨. 레벨 개념이 없으면(카드 · 각성) 0. </summary>
        public int      LEVEL   => eKind == PICK_KIND.RUN_SKILL && cRunSkill != null && cRunSkill.iMaxLevel > 1
                                 ? iNextLevel : 0;

        public static CPickOption From_Card(CCardInfo cInfo)
            => new CPickOption { eKind = PICK_KIND.CARD, cCard = cInfo };

        public static CPickOption From_RunSkill(CRunSkillInfo cInfo, int iNextLevel)
            => new CPickOption { eKind = PICK_KIND.RUN_SKILL, cRunSkill = cInfo, iNextLevel = iNextLevel };

        public static CPickOption From_Awaken(CAwakenInfo cInfo, CRunSkillInfo cActive)
            => new CPickOption { eKind = PICK_KIND.AWAKEN, cAwaken = cInfo, cRunSkill = cActive };
    }

    // 260917_카드와 런 스킬을 한 풀에 넣고 가중치로 뽑는다 (Docs/Design_RunSkill_Awaken.md 3장 '3지선다 풀')
    public static class CPickOption_Utility
    {
        /// <param name="cCardTable"> 없으면 런 스킬만 </param>
        /// <param name="cRunSkillTable"> 없으면 카드만 </param>
        /// <param name="fnGetLevel"> 그 런 스킬을 지금 몇 레벨 들고 있는지 (안 가졌으면 0) </param>
        /// <param name="fnIsMapCleared"> 런 스킬 해금 조건 </param>
        /// <param name="cAwakenTable"> 260917_없으면 각성 후보가 없다 </param>
        /// <param name="fnIsAwakened"> 그 액티브가 이미 각성했는가 </param>
        public static List<CPickOption> Pick(CCSVData_CardInfo cCardTable, CCSVData_RunSkillInfo cRunSkillTable,
                                             Func<RUN_SKILL_TYPE, int> fnGetLevel, Func<int, bool> fnIsMapCleared,
                                             int iCount, CCSVData_AwakenInfo cAwakenTable = null,
                                             Func<RUN_SKILL_TYPE, bool> fnIsAwakened = null,
                                             Func<CPickOption, bool> fnExclude = null)
        {
            List<CPickOption> lstCandidate = new List<CPickOption>();

            if (cCardTable != null)
            {
                for (int i = 0; i < cCardTable.ALL.Count; ++i)
                    lstCandidate.Add(CPickOption.From_Card(cCardTable.ALL[i]));
            }

            if (cRunSkillTable != null)
            {
                List<CRunSkillInfo> lstSkill = cRunSkillTable.Collect_Candidates(fnGetLevel, fnIsMapCleared);
                for (int i = 0; i < lstSkill.Count; ++i)
                {
                    int iLevel = fnGetLevel != null ? fnGetLevel(lstSkill[i].eType) : 0;
                    lstCandidate.Add(CPickOption.From_RunSkill(lstSkill[i], iLevel + 1));
                }
            }

            // 260917_각성 — 만렙 액티브 + 짝 패시브. 가중치는 표에서 높게 잡아 두었다(놓치면 아쉬운 선택이라)
            if (cAwakenTable != null && cRunSkillTable != null)
            {
                List<CAwakenInfo> lstAwaken = cAwakenTable.Collect_Candidates(cRunSkillTable, fnGetLevel, fnIsAwakened);
                for (int i = 0; i < lstAwaken.Count; ++i)
                    lstCandidate.Add(CPickOption.From_Awaken(lstAwaken[i], cRunSkillTable.Find_ByType(lstAwaken[i].eActiveType)));
            }

            // 260921_버린 것 · 이미 화면에 떠 있는 것은 뺀다(다시 뽑기 · 버리기, 2-10-1)
            if (fnExclude != null)
                lstCandidate.RemoveAll(cOption => fnExclude(cOption));

            return CWeightedPick_Utility.Pick(lstCandidate, cOption => cOption.WEIGHT, iCount);
        }
    }
}
