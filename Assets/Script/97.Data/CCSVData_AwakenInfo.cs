using System;
using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260917_런 스킬 각성 (Assets/Data/AwakenInfo.csv) — Docs/Design_RunSkill_Awaken.md 2장 규칙 3~5
    /// <summary>
    /// 액티브 하나 + 짝 패시브 하나 = 각성 한 줄. 액티브가 만렙이고 짝 패시브를 들고 있으면 3지선다 후보에 오른다.
    /// 각성해도 슬롯을 새로 먹지 않는다 — 그 액티브가 그 자리에서 바뀐다.
    /// </summary>
    public class CAwakenInfo
    {
        public int              iAwakenID;
        public RUN_SKILL_TYPE   eActiveType;
        public RUN_SKILL_TYPE   ePassiveType;
        public int              iPassiveLevel;      // 짝 패시브가 이 레벨 이상이면 된다. 보통 1 — 패시브는 '열쇠'지 '재료'가 아니다
        public string           strName;
        public string           strDesc;
        public int              iWeight;

        // 투사체 무기(CRunSkillEffect_Weapon)용 덮어쓰기. 0 / 비어 있으면 원래 값을 쓴다.
        public int              iProjectileID;
        public bool             bOverridePattern;
        public FIRE_PATTERN     eFirePattern;

        // 액티브마다 뜻이 다른 조율값 — COUNT_BONUS · COOL_RATE · SPEED_RATE · RADIUS_RATE 등 (CLAUDE.md 2-11-2)
        public Dictionary<string, float> dicParam = new Dictionary<string, float>();

        public float Get_Param(string strKey, float fDefault)
            => dicParam.TryGetValue(strKey, out float fValue) ? fValue : fDefault;
    }

    public class CCSVData_AwakenInfo : CCSVData
    {
        private const string TABLE_NAME = "AwakenInfo";
        public  const string CSV_KEY    = "CCSVData_AwakenInfo";

        private readonly List<CAwakenInfo> m_lstInfo = new List<CAwakenInfo>();

        public IReadOnlyList<CAwakenInfo> ALL   => m_lstInfo;
        public int                        COUNT => m_lstInfo.Count;

        public CAwakenInfo Get_Info(int iAwakenID)
        {
            for (int i = 0; i < m_lstInfo.Count; ++i)
            {
                if (m_lstInfo[i].iAwakenID == iAwakenID)
                    return m_lstInfo[i];
            }
            return null;
        }

        /// <summary>
        /// 지금 고를 수 있는 각성. 액티브가 만렙(표의 iMaxLevel)이고 · 짝 패시브가 요구 레벨 이상이고 ·
        /// 그 액티브가 아직 각성하지 않았고 · 가중치가 0이 아니다.
        /// 액티브 요구 레벨을 표에 따로 적지 않는 이유 — '만렙'과 같은 숫자를 두 곳에 적게 된다(문서 3장).
        /// </summary>
        public List<CAwakenInfo> Collect_Candidates(CCSVData_RunSkillInfo cRunSkillTable,
                                                    Func<RUN_SKILL_TYPE, int> fnGetLevel,
                                                    Func<RUN_SKILL_TYPE, bool> fnIsAwakened)
        {
            List<CAwakenInfo> lstCandidate = new List<CAwakenInfo>();
            if (cRunSkillTable == null || fnGetLevel == null)
                return lstCandidate;

            for (int i = 0; i < m_lstInfo.Count; ++i)
            {
                CAwakenInfo cInfo = m_lstInfo[i];
                if (cInfo.iWeight <= 0)
                    continue;

                if (fnIsAwakened != null && fnIsAwakened(cInfo.eActiveType) == true)
                    continue;

                CRunSkillInfo cActive = cRunSkillTable.Find_ByType(cInfo.eActiveType);
                if (cActive == null || fnGetLevel(cInfo.eActiveType) < cActive.iMaxLevel)
                    continue;

                if (fnGetLevel(cInfo.ePassiveType) < Mathf.Max(1, cInfo.iPassiveLevel))
                    continue;

                lstCandidate.Add(cInfo);
            }

            return lstCandidate;
        }

        // 헤더 순서와 1:1로 맞춘다.
        protected override void Parse_CSVData(string[] arrField)
        {
            if (CCSV_Utility.Is_HeaderRow(arrField, "iAwakenID") == true)
                return;

            string strPattern = CCSV_Utility.To_String(arrField, 8);
            bool bOverridePattern = strPattern.Length > 0 && strPattern != "-";

            CAwakenInfo cInfo = new CAwakenInfo
            {
                iAwakenID        = CCSV_Utility.To_Int(arrField, 0),
                eActiveType      = CCSV_Utility.To_Enum(arrField, 1, RUN_SKILL_TYPE.NONE),
                ePassiveType     = CCSV_Utility.To_Enum(arrField, 2, RUN_SKILL_TYPE.NONE),
                iPassiveLevel    = CCSV_Utility.To_Int(arrField, 3, 1),
                strName          = CCSV_Utility.To_String(arrField, 4),
                strDesc          = CCSV_Utility.To_String(arrField, 5),
                iWeight          = CCSV_Utility.To_Int(arrField, 6, 30),
                iProjectileID    = CCSV_Utility.To_Int(arrField, 7),
                bOverridePattern = bOverridePattern,
                eFirePattern     = bOverridePattern == true
                                 ? CCSV_Utility.To_Enum(arrField, 8, FIRE_PATTERN.SINGLE) : FIRE_PATTERN.SINGLE,
                dicParam         = CCSV_Utility.To_ParamMap(arrField, 9),
            };

            if (cInfo.iAwakenID <= 0 || cInfo.eActiveType == RUN_SKILL_TYPE.NONE || cInfo.ePassiveType == RUN_SKILL_TYPE.NONE)
            {
                Debug.LogError($"[{TABLE_NAME}] iAwakenID · eActiveType · ePassiveType이 없는 행을 건너뛴다. "
                             + "타입은 RUN_SKILL_TYPE 이름과 철자가 같아야 한다.");
                return;
            }

            m_lstInfo.Add(cInfo);
        }
    }
}
