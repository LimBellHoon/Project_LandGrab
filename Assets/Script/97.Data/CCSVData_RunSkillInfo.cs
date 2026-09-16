using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260916_런 전용 스킬 표 (Assets/Data/RunSkillInfo.csv)
    /// <summary>
    /// 장비/강화 스킬(CSkillInfo)과는 다른 표다 — 이쪽은 스테이지 클리어로 해금 풀에 들어가고,
    /// 판 안에서 <see cref="CRunSkillHandler"/>가 여러 개를 동시에 들고 레벨업시킨다.
    /// 저장하지 않는다 — 스테이지를 나가면 보유·레벨이 전부 사라지는 런 전용 데이터다.
    /// </summary>
    public class CRunSkillInfo
    {
        public int              iSkillID;
        public RUN_SKILL_TYPE   eType;
        public SKILL_CATEGORY   eCategory;
        public string           strName;
        public string           strDesc;

        // 260916_이 맵을 클리어해야 해금 풀에 들어간다. 0이면 처음부터 해금.
        public int              iUnlockMapID;
        public int              iMaxLevel;

        public float            fValueBase;
        public float            fValuePerLevel;
        public int              iWeight;    // 3지선다 가중치. 0이면 안 나온다

        public bool IS_PASSIVE => eCategory == SKILL_CATEGORY.PASSIVE;

        /// <summary> 그 레벨에서의 수치. 레벨 1이 fValueBase, 레벨이 오를 때마다 fValuePerLevel씩 붙는다. </summary>
        public float Get_Value(int iLevel)
        {
            int iClamped = Mathf.Clamp(iLevel, 0, iMaxLevel);
            return iClamped <= 0 ? 0f : fValueBase + fValuePerLevel * (iClamped - 1);
        }
    }

    /// <summary>
    /// 클래스 이름은 Engine이 강제한다 — RunSkillInfo.csv ↔ CCSVData_RunSkillInfo.
    /// (자세한 규약은 CCSVData_EnemyInfo 설명 참고)
    /// </summary>
    public class CCSVData_RunSkillInfo : CCSVData
    {
        private const string TABLE_NAME = "RunSkillInfo";
        public  const string CSV_KEY    = "CCSVData_RunSkillInfo";

        private readonly List<CRunSkillInfo> m_lstInfo = new List<CRunSkillInfo>();

        public IReadOnlyList<CRunSkillInfo> ALL   => m_lstInfo;
        public int                          COUNT => m_lstInfo.Count;

        public CRunSkillInfo Find_ByType(RUN_SKILL_TYPE eType)
        {
            for (int i = 0; i < m_lstInfo.Count; ++i)
            {
                if (m_lstInfo[i].eType == eType)
                    return m_lstInfo[i];
            }

            return null;
        }

        // 260916_해금 + 만렙이 아닌 스킬만 골라 3지선다 후보로 뽑는다.
        // 이미 보유한 스킬은 다음 레벨로, 안 가진 스킬은 새로 얻는 쪽이 된다 — 레벨 판단은 호출부(CRunSkillHandler)가 한다.
        /// <param name="fnGetLevel"> 그 스킬을 지금 몇 레벨 들고 있는지 (안 가졌으면 0) </param>
        public List<CRunSkillInfo> Pick_Random(int iCount, System.Func<RUN_SKILL_TYPE, int> fnGetLevel,
                                               System.Func<int, bool> fnIsMapCleared, List<CRunSkillInfo> lstResult = null)
            => CWeightedPick_Utility.Pick(Collect_Candidates(fnGetLevel, fnIsMapCleared), cInfo => cInfo.iWeight,
                                          iCount, lstResult);

        // 260917_3지선다에 오를 수 있는 스킬 — 카드와 섞어 뽑기 위해 뽑기와 떼어 냈다(CPickOption_Utility).
        /// <summary>
        /// 가중치가 있고 · 해금됐고 · 만렙이 아니고 · 새로 얻는 거라면 그 분류(액티브/패시브)의 슬롯이 남아 있는 스킬.
        /// 이미 가진 스킬의 레벨업은 슬롯을 새로 먹지 않으므로 상한에 걸리지 않는다(Design_RunSkill_Awaken 2장 규칙 1).
        /// </summary>
        public List<CRunSkillInfo> Collect_Candidates(System.Func<RUN_SKILL_TYPE, int> fnGetLevel,
                                                      System.Func<int, bool> fnIsMapCleared)
        {
            int iOwnedActive  = Count_Owned(SKILL_CATEGORY.ACTIVE, fnGetLevel);
            int iOwnedPassive = Count_Owned(SKILL_CATEGORY.PASSIVE, fnGetLevel);

            List<CRunSkillInfo> lstCandidate = new List<CRunSkillInfo>();

            for (int i = 0; i < m_lstInfo.Count; ++i)
            {
                CRunSkillInfo cInfo = m_lstInfo[i];
                if (cInfo.iWeight <= 0)
                    continue;

                if (cInfo.iUnlockMapID > 0 && (fnIsMapCleared == null || fnIsMapCleared(cInfo.iUnlockMapID) == false))
                    continue;

                int iLevel = fnGetLevel != null ? fnGetLevel(cInfo.eType) : 0;
                if (iLevel >= cInfo.iMaxLevel)
                    continue;

                int iOwned = cInfo.IS_PASSIVE == true ? iOwnedPassive : iOwnedActive;
                if (iLevel <= 0 && iOwned >= CRunSkillHandler.SLOT_PER_CATEGORY)
                    continue;

                lstCandidate.Add(cInfo);
            }

            return lstCandidate;
        }

        /// <summary> 그 분류의 스킬을 지금 몇 개 들고 있는가. </summary>
        public int Count_Owned(SKILL_CATEGORY eCategory, System.Func<RUN_SKILL_TYPE, int> fnGetLevel)
        {
            if (fnGetLevel == null)
                return 0;

            int iCount = 0;
            for (int i = 0; i < m_lstInfo.Count; ++i)
            {
                if (m_lstInfo[i].eCategory == eCategory && fnGetLevel(m_lstInfo[i].eType) > 0)
                    ++iCount;
            }
            return iCount;
        }

        // 헤더 순서와 1:1로 맞춘다.
        protected override void Parse_CSVData(string[] arrField)
        {
            // 260916_헤더 줄은 조용히 넘긴다 (CCSV_Utility.Is_HeaderRow 주석 참고).
            if (CCSV_Utility.Is_HeaderRow(arrField, "iSkillID") == true)
                return;

            CRunSkillInfo cInfo = new CRunSkillInfo
            {
                iSkillID        = CCSV_Utility.To_Int(arrField, 0),
                eType           = CCSV_Utility.To_Enum(arrField, 1, RUN_SKILL_TYPE.NONE),
                eCategory       = CCSV_Utility.To_Enum(arrField, 2, SKILL_CATEGORY.PASSIVE),
                strName         = CCSV_Utility.To_String(arrField, 3),
                strDesc         = CCSV_Utility.To_String(arrField, 4),
                iUnlockMapID    = CCSV_Utility.To_Int(arrField, 5),
                iMaxLevel       = CCSV_Utility.To_Int(arrField, 6, 1),
                fValueBase      = CCSV_Utility.To_Float(arrField, 7),
                fValuePerLevel  = CCSV_Utility.To_Float(arrField, 8),
                iWeight         = CCSV_Utility.To_Int(arrField, 9, 10),
            };

            if (cInfo.iSkillID <= 0 || cInfo.eType == RUN_SKILL_TYPE.NONE)
            {
                Debug.LogError($"[{TABLE_NAME}] iSkillID 또는 eType이 없는 행을 건너뛴다. "
                             + "eType은 RUN_SKILL_TYPE 이름과 철자가 같아야 한다.");
                return;
            }

            m_lstInfo.Add(cInfo);
        }
    }
}
