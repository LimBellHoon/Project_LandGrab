using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260917_캐릭터 표 (Assets/Data/CharacterInfo.csv)
    /// <summary>
    /// 캐릭터는 스킨(외형)과 스탯 배율만 다르다 — 전용 스킬은 없다. 런 스킬 뽑기 풀(2-11-1/2-11-2)은
    /// 전 캐릭터 공통이다. 캐릭터마다 고유 킷을 만들면 캐릭터가 늘 때마다 밸런싱 비용이 같이 늘어
    /// 47명 목표와 정면충돌하기 때문이다(노션 "캐릭터 시스템" 카드 9장).
    /// </summary>
    public class CCharacterInfo
    {
        public int      iCharacterID;
        public string   strName;
        public string   strPrefabName;
        public int      iMaxLevel;

        public float    fSpeedRateBase;
        public float    fSpeedRatePerLevel;
        public float    fMaxHpRateBase;
        public float    fMaxHpRatePerLevel;
        public float    fEvasionBonusBase;
        public float    fEvasionBonusPerLevel;

        public string   strDesc;

        // 260918_레벨업 재화 — 이미 가진 캐릭터를 각성 스테이지에서 다시 클리어하면 조각을 받고,
        // 가방에서 조각을 모아 레벨업 버튼을 눌러야 실제로 레벨이 오른다(CProgress_Manager 참고).
        public int      iFragmentPerClear;
        public int      iFragmentCostBase;
        public int      iFragmentCostAdd;

        // 260918_성급 스켈레톤 — 화면 틀만 만들어 둔다. 임계 레벨과 설명 텍스트뿐이고
        // 실제 스탯/효과는 아직 어디에도 걸려 있지 않다. 나중에 기획이 확정되면
        // Get_SpeedRate 등과 같은 자리에 실제 배율을 추가하면 된다.
        public List<int>    lstStarLevel = new List<int>();
        public List<string> lstStarDesc  = new List<string>();

        // 260917_레벨 0(미보유)이어도 배율이 1(속도·체력) / 0(회피 보너스)이 나와야 안전하게 곱할 수 있다 —
        // CRunSkillInfo.Get_Value(레벨 0이면 0)와 다르게 최소 레벨을 1로 못박는 이유다.
        public float Get_SpeedRate(int iLevel)    => Get_Value(fSpeedRateBase, fSpeedRatePerLevel, iLevel);
        public float Get_MaxHpRate(int iLevel)    => Get_Value(fMaxHpRateBase, fMaxHpRatePerLevel, iLevel);
        public float Get_EvasionBonus(int iLevel) => Get_Value(fEvasionBonusBase, fEvasionBonusPerLevel, iLevel);

        // 260918_레벨1 → 2가 첫 강화이므로 (iCurLevel - 1)을 '지금까지 강화한 횟수'로 본다 —
        // CUpgradeInfo/CSkillInfo의 Get_Cost와 같은 형태이되, 이쪽은 레벨이 0이 아니라 1부터 시작해서다.
        public int Get_FragmentCost(int iCurLevel) => iFragmentCostBase + iFragmentCostAdd * Mathf.Max(0, iCurLevel - 1);

        /// <summary> 지금 몇 성인지(0~lstStarLevel.Count). 화면 표시용 — 실제 효과는 없다. </summary>
        public int Get_StarTier(int iLevel)
        {
            int iTier = 0;
            for (int i = 0; i < lstStarLevel.Count; ++i)
            {
                if (iLevel >= lstStarLevel[i])
                    ++iTier;
            }
            return iTier;
        }

        private float Get_Value(float fBase, float fPerLevel, int iLevel)
        {
            int iClamped = Mathf.Clamp(iLevel, 1, iMaxLevel);
            return fBase + fPerLevel * (iClamped - 1);
        }
    }

    /// <summary>
    /// 클래스 이름은 Engine이 강제한다 (CCSVData_EnemyInfo의 설명 참고).
    /// CharacterInfo.csv ↔ CCSVData_CharacterInfo.
    /// </summary>
    public class CCSVData_CharacterInfo : CCSVData
    {
        private const string TABLE_NAME = "CharacterInfo";
        public  const string CSV_KEY    = "CCSVData_CharacterInfo";

        private readonly Dictionary<int, CCharacterInfo> m_dicInfo = new Dictionary<int, CCharacterInfo>();
        private readonly List<CCharacterInfo>             m_lstInfo = new List<CCharacterInfo>();

        public IReadOnlyList<CCharacterInfo> ALL   => m_lstInfo;
        public int                           COUNT => m_lstInfo.Count;

        public CCharacterInfo Get_Info(int iCharacterID)
        {
            if (m_dicInfo.TryGetValue(iCharacterID, out CCharacterInfo cInfo) == true)
                return cInfo;

            Debug.LogError($"[{TABLE_NAME}] ID {iCharacterID}인 캐릭터가 표에 없다.");
            return null;
        }

        // 헤더 순서와 1:1로 맞춘다.
        protected override void Parse_CSVData(string[] arrField)
        {
            // 260917_헤더 줄은 조용히 넘긴다 (CCSV_Utility.Is_HeaderRow 주석 참고).
            if (CCSV_Utility.Is_HeaderRow(arrField, "iCharacterID") == true)
                return;

            CCharacterInfo cInfo = new CCharacterInfo
            {
                iCharacterID          = CCSV_Utility.To_Int(arrField, 0),
                strName               = CCSV_Utility.To_String(arrField, 1),
                strPrefabName         = CCSV_Utility.To_String(arrField, 2),
                iMaxLevel             = CCSV_Utility.To_Int(arrField, 3, 1),
                fSpeedRateBase        = CCSV_Utility.To_Float(arrField, 4, 1f),
                fSpeedRatePerLevel    = CCSV_Utility.To_Float(arrField, 5),
                fMaxHpRateBase        = CCSV_Utility.To_Float(arrField, 6, 1f),
                fMaxHpRatePerLevel    = CCSV_Utility.To_Float(arrField, 7),
                fEvasionBonusBase     = CCSV_Utility.To_Float(arrField, 8),
                fEvasionBonusPerLevel = CCSV_Utility.To_Float(arrField, 9),
                strDesc               = CCSV_Utility.To_String(arrField, 10),
                iFragmentPerClear     = CCSV_Utility.To_Int(arrField, 11, 10),
                iFragmentCostBase     = CCSV_Utility.To_Int(arrField, 12, 20),
                iFragmentCostAdd      = CCSV_Utility.To_Int(arrField, 13, 5),
                lstStarLevel          = CCSV_Utility.To_IntList(arrField, 14),
                lstStarDesc           = CCSV_Utility.To_List(arrField, 15),
            };

            if (cInfo.iCharacterID <= 0 || string.IsNullOrEmpty(cInfo.strPrefabName) == true)
            {
                Debug.LogError($"[{TABLE_NAME}] iCharacterID 또는 strPrefabName이 없는 행을 건너뛴다.");
                return;
            }

            if (m_dicInfo.ContainsKey(cInfo.iCharacterID) == true)
            {
                Debug.LogError($"[{TABLE_NAME}] iCharacterID {cInfo.iCharacterID}가 중복이다. 뒤의 행을 버린다.");
                return;
            }

            m_dicInfo.Add(cInfo.iCharacterID, cInfo);
            m_lstInfo.Add(cInfo);
        }
    }
}
