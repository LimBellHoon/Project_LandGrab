using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260912_카드 한 장
    /// <summary>
    /// 점령률을 넘길 때마다 셋 중 하나를 고르는 카드. 고른 판에서만 유지된다.
    /// 수치의 뜻은 종류마다 다르다 — CardInfo.csv 머리의 주석이 기준이다.
    /// </summary>
    public class CCardInfo
    {
        public int          iCardID;
        public CARD_TYPE    eType;
        public string       strName;
        public string       strDesc;
        public float        fValue;
        public int          iWeight;    // 뽑힐 가중치. 0이면 안 나온다
        // 260920_3지선다 카드의 색 테마(2-10-1). 레이아웃은 같고 색만 다르다
        public PICK_THEME   eTheme;

        // 260928_카드 30종(태스크 #22) — 레벨링 카드가 쓰는 열. iMaxLevel이 1 이하면 옛 카드처럼
        // 즉시효과 1회권이다(fValue 하나만 본다) — 새 열을 안 채워도 옛 카드가 그대로 동작한다.
        public CARD_FAMILY  eFamily;       // 제어(K)/기동(M)/수호(G)/도화선(F)
        public int          iTier;         // 원본 스펙의 T(계열 안에서의 단계 1~3) — 뽑기 확률에 아직 안 쓴다(태스크 #23)
        public int          iMaxLevel;
        public float        fValueLv1;
        public float        fValueLv2;
        public float        fValueLv3;
        // 260928_두 번째 수치가 필요한 카드(G08의 확률+깊이 등)를 위한 조율값 — ProjectileInfo.csv와
        // 같은 규약(KEY:VALUE|KEY:VALUE, CCSV_Utility.To_ParamMap). 레벨별로 셋(_LV1/_LV2/_LV3)을 적는다.
        public Dictionary<string, float> dicParam;
        public bool         bFirstPickOnly; // ★ — 원본 스펙의 "첫 선택 고정 풀". 실제 뽑기 배선은 태스크 #23

        /// <summary> dicParam에서 "KEY_LV{iLevel}" 키를 읽는다(없으면 fDefault). 레벨별 2번째 수치용. </summary>
        public float Get_Param(string strKey, int iLevel, float fDefault = 0f)
        {
            if (dicParam == null)
                return fDefault;

            string strKeyFull = $"{strKey}_LV{Mathf.Clamp(iLevel, 1, 3)}".ToUpperInvariant();
            return dicParam.TryGetValue(strKeyFull, out float fValue) ? fValue : fDefault;
        }

        /// <summary> 그 레벨에서의 fValueLv1~3. 레벨 0이거나 범위를 벗어나면 0. </summary>
        public float Get_Value(int iLevel)
        {
            switch (Mathf.Clamp(iLevel, 0, 3))
            {
                case 1: return fValueLv1;
                case 2: return fValueLv2;
                case 3: return fValueLv3;
                default: return 0f;
            }
        }
    }

    public class CCSVData_CardInfo : CCSVData
    {
        public const string CSV_KEY = "CCSVData_CardInfo";

        private readonly List<CCardInfo> m_lstInfo = new List<CCardInfo>();

        public IReadOnlyList<CCardInfo> ALL => m_lstInfo;
        public int COUNT => m_lstInfo.Count;

        /// <summary> 260929_표를 순서대로 훑는 창구(검증 · 테스트용). 범위 밖이면 null </summary>
        public CCardInfo Get_ByIndex(int iIndex)
            => iIndex >= 0 && iIndex < m_lstInfo.Count ? m_lstInfo[iIndex] : null;

        public CCardInfo Get_Info(int iCardID)
        {
            for (int i = 0; i < m_lstInfo.Count; ++i)
            {
                if (m_lstInfo[i].iCardID == iCardID)
                    return m_lstInfo[i];
            }

            return null;
        }

        // 260912_가중치로 뽑되 같은 카드가 한 번에 두 장 나오지 않게 한다.
        // 셋 중 하나를 고르는 재미는 '서로 다른 선택지'에서 나온다.
        /// <summary> 서로 다른 카드를 iCount장 뽑는다. 표에 그만큼 없으면 있는 만큼만 준다. </summary>
        public List<CCardInfo> Pick_Random(int iCount, List<CCardInfo> lstResult = null)
            => CWeightedPick_Utility.Pick(m_lstInfo, cInfo => cInfo.iWeight, iCount, lstResult);

        // 260928_레벨링 카드(iMaxLevel > 1, 태스크 #22)만 후보로 거른다 — CCSVData_RunSkillInfo.Collect_Candidates와
        // 같은 자리(카드·런 스킬 둘 다 "만렙이 아닌 것만"이 후보 규칙이다, CPickOption_Utility가 한 풀에 합친다).
        // 옛 즉시효과 카드(iMaxLevel <= 1)는 Pick_Random이 그대로 맡는다 — 레벨이라는 개념이 없어 다시 나와도 된다.
        /// <param name="fnGetLevel"> 그 카드를 지금 몇 레벨 들고 있는지(안 가졌으면 0) </param>
        public List<CCardInfo> Collect_Candidates(System.Func<CARD_TYPE, int> fnGetLevel)
        {
            List<CCardInfo> lstCandidate = new List<CCardInfo>();

            for (int i = 0; i < m_lstInfo.Count; ++i)
            {
                CCardInfo cInfo = m_lstInfo[i];
                if (cInfo.iWeight <= 0 || cInfo.iMaxLevel <= 1)
                    continue;

                int iLevel = fnGetLevel != null ? fnGetLevel(cInfo.eType) : 0;
                if (iLevel >= cInfo.iMaxLevel)
                    continue;

                lstCandidate.Add(cInfo);
            }

            return lstCandidate;
        }

        protected override void Parse_CSVData(string[] arrField)
        {
            // 260905_헤더 줄은 조용히 넘긴다 (CCSV_Utility.Is_HeaderRow 주석 참고).
            if (CCSV_Utility.Is_HeaderRow(arrField, "iCardID") == true)
                return;

            CCardInfo cInfo = new CCardInfo
            {
                iCardID = CCSV_Utility.To_Int(arrField, 0),
                eType   = CCSV_Utility.To_Enum(arrField, 1, CARD_TYPE.NONE),
                strName = CCSV_Utility.To_String(arrField, 2),
                strDesc = CCSV_Utility.To_String(arrField, 3),
                fValue  = CCSV_Utility.To_Float(arrField, 4),
                iWeight = CCSV_Utility.To_Int(arrField, 5, 10),
                // 260920_카드 색 테마(2-10-1). 안 적으면 전투로 본다
                eTheme  = CCSV_Utility.To_Enum(arrField, 6, PICK_THEME.COMBAT),
                // 260928_카드 30종(태스크 #22)이 쓰는 레벨링 열 — 옛 카드는 안 채워도 기본값(가족 CONTROL·
                // 만렙 0)으로 남아 Collect_Candidates가 안 골라 간다(iMaxLevel <= 1 조건)
                eFamily        = CCSV_Utility.To_Enum(arrField, 7, CARD_FAMILY.CONTROL),
                iTier          = CCSV_Utility.To_Int(arrField, 8, 1),
                iMaxLevel      = CCSV_Utility.To_Int(arrField, 9, 0),
                fValueLv1      = CCSV_Utility.To_Float(arrField, 10),
                fValueLv2      = CCSV_Utility.To_Float(arrField, 11),
                fValueLv3      = CCSV_Utility.To_Float(arrField, 12),
                dicParam       = CCSV_Utility.To_ParamMap(arrField, 13),
                bFirstPickOnly = CCSV_Utility.To_Int(arrField, 14) != 0,
            };

            if (cInfo.iCardID <= 0 || cInfo.eType == CARD_TYPE.NONE)
            {
                Debug.LogError("[CardInfo] iCardID 또는 eType이 없는 행을 건너뛴다. "
                             + "eType은 CARD_TYPE 이름과 철자가 같아야 한다.");
                return;
            }

            m_lstInfo.Add(cInfo);
        }
    }
}
