using System;
using System.Collections.Generic;
using System.Globalization;

using UnityEngine;

namespace Client
{
    // 260904_CSV 파싱 공용 헬퍼
    /// <summary>
    /// Engine.CCSVData.Parse_CSVData가 넘겨주는 string[]에서 값을 꺼낼 때 쓴다.
    /// 칸이 모자라거나 형식이 틀려도 예외를 던지지 않고 기본값으로 넘어간다 —
    /// Engine이 행 단위로 try/catch를 걸어 두긴 했지만, 한 칸 오타로 행 전체가 날아가면
    /// 기획이 무엇을 잘못 적었는지 알 수 없기 때문이다.
    /// 숫자는 반드시 InvariantCulture로 읽는다. 한국어 로케일에서 소수점이 쉼표로 잡히면
    /// 0.7이 7로 둔갑한다.
    /// </summary>
    public static class CCSV_Utility
    {
        public const char SPLIT_LIST  = '|';    // 웨이브 등 목록 구분
        public const char SPLIT_ITEM  = ',';    // 목록 안의 항목 구분
        public const char SPLIT_COUNT = '*';    // ID*개수
        private const string EMPTY_MARK = "-";  // '값 없음'을 눈에 보이게 적는 표기

        public static string To_String(string[] arrField, int iIndex, string strDefault = "")
        {
            if (arrField == null || iIndex < 0 || iIndex >= arrField.Length)
                return strDefault;

            string strValue = arrField[iIndex].Trim();
            if (strValue.Length == 0 || strValue == EMPTY_MARK)
                return strDefault;

            return strValue;
        }

        // 260905_Engine.CCSVData는 0번 줄(헤더)도 Parse_CSVData에 그대로 넘긴다.
        // 파서마다 이걸 먼저 걸러 내지 않으면 실행할 때마다 표 개수만큼 에러가 찍히고,
        // 정작 기획이 낸 진짜 데이터 오류가 그 사이에 묻힌다.
        /// <summary> 첫 칸이 열 이름과 같으면 헤더 줄이다. </summary>
        public static bool Is_HeaderRow(string[] arrField, string strFirstColumn)
        {
            if (arrField == null || arrField.Length == 0 || string.IsNullOrEmpty(strFirstColumn) == true)
                return false;

            // TextAsset이 BOM을 남기는 경우가 있어 첫 글자를 떼고 비교한다.
            string strValue = arrField[0].Trim().TrimStart('\uFEFF');
            return string.Equals(strValue, strFirstColumn, StringComparison.OrdinalIgnoreCase);
        }

        public static int To_Int(string[] arrField, int iIndex, int iDefault = 0)
        {
            string strValue = To_String(arrField, iIndex);
            if (strValue.Length == 0)
                return iDefault;

            return int.TryParse(strValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int iValue)
                 ? iValue : iDefault;
        }

        public static float To_Float(string[] arrField, int iIndex, float fDefault = 0f)
        {
            string strValue = To_String(arrField, iIndex);
            if (strValue.Length == 0)
                return fDefault;

            return float.TryParse(strValue, NumberStyles.Float, CultureInfo.InvariantCulture, out float fValue)
                 ? fValue : fDefault;
        }

        public static T To_Enum<T>(string[] arrField, int iIndex, T eDefault) where T : struct
        {
            string strValue = To_String(arrField, iIndex);
            if (strValue.Length == 0)
                return eDefault;

            if (Enum.TryParse(strValue, true, out T eValue) == true)
                return eValue;

            Debug.LogWarning($"[CCSV_Utility] '{strValue}'는 {typeof(T).Name}에 없는 값이다. {eDefault}로 대체한다.");
            return eDefault;
        }

        /// <summary> "a|b|c" → [a, b, c]. 빈 칸이면 빈 목록. </summary>
        public static List<string> To_List(string[] arrField, int iIndex, char chSplit = SPLIT_LIST)
        {
            List<string> lstValue = new List<string>();

            string strValue = To_String(arrField, iIndex);
            if (strValue.Length == 0)
                return lstValue;

            string[] arrToken = strValue.Split(chSplit);
            for (int i = 0; i < arrToken.Length; ++i)
            {
                string strToken = arrToken[i].Trim();
                if (strToken.Length > 0)
                    lstValue.Add(strToken);
            }

            return lstValue;
        }

        /// <summary> "0.6|0.65|0.7" → [0.6, 0.65, 0.7] </summary>
        public static List<float> To_FloatList(string[] arrField, int iIndex, char chSplit = SPLIT_LIST)
        {
            List<string> lstToken = To_List(arrField, iIndex, chSplit);
            List<float> lstValue = new List<float>(lstToken.Count);

            for (int i = 0; i < lstToken.Count; ++i)
            {
                lstValue.Add(float.TryParse(lstToken[i], NumberStyles.Float, CultureInfo.InvariantCulture,
                                            out float fValue) ? fValue : 0f);
            }

            return lstValue;
        }

        /// <summary> 목록 길이가 기대와 다르면 경고를 남기고 false. 표가 조용히 어긋나는 것을 막는다. </summary>
        public static bool Check_Count(string strTable, int iKey, string strColumn, int iActual, int iExpect)
        {
            if (iActual == iExpect)
                return true;

            Debug.LogError($"[{strTable}] ID {iKey} — {strColumn}의 항목이 {iActual}개다. {iExpect}개여야 한다.");
            return false;
        }
    }
}
