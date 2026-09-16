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
        /// <summary> 런 스킬을 고르면 오를 레벨. 1이면 새로 얻는다 </summary>
        public int              iNextLevel;

        public string   NAME    => eKind == PICK_KIND.CARD ? cCard.strName : cRunSkill.strName;
        public string   DESC    => eKind == PICK_KIND.CARD ? cCard.strDesc : cRunSkill.strDesc;
        public int      WEIGHT  => eKind == PICK_KIND.CARD ? cCard.iWeight : cRunSkill.iWeight;
        public bool     IS_NEW  => eKind == PICK_KIND.RUN_SKILL && iNextLevel <= 1;

        public static CPickOption From_Card(CCardInfo cInfo)
            => new CPickOption { eKind = PICK_KIND.CARD, cCard = cInfo };

        public static CPickOption From_RunSkill(CRunSkillInfo cInfo, int iNextLevel)
            => new CPickOption { eKind = PICK_KIND.RUN_SKILL, cRunSkill = cInfo, iNextLevel = iNextLevel };
    }

    // 260917_카드와 런 스킬을 한 풀에 넣고 가중치로 뽑는다 (Docs/Design_RunSkill_Awaken.md 3장 '3지선다 풀')
    public static class CPickOption_Utility
    {
        /// <param name="cCardTable"> 없으면 런 스킬만 </param>
        /// <param name="cRunSkillTable"> 없으면 카드만 </param>
        /// <param name="fnGetLevel"> 그 런 스킬을 지금 몇 레벨 들고 있는지 (안 가졌으면 0) </param>
        /// <param name="fnIsMapCleared"> 런 스킬 해금 조건 </param>
        public static List<CPickOption> Pick(CCSVData_CardInfo cCardTable, CCSVData_RunSkillInfo cRunSkillTable,
                                             Func<RUN_SKILL_TYPE, int> fnGetLevel, Func<int, bool> fnIsMapCleared,
                                             int iCount)
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

            return CWeightedPick_Utility.Pick(lstCandidate, cOption => cOption.WEIGHT, iCount);
        }
    }
}
