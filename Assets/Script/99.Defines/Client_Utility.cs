using System.Text;

namespace Client
{
    // 260905_별 표기
    /// <summary>
    /// 결과 화면과 스테이지 선택이 같은 모양으로 별을 그리도록 한곳에 모아 둔다.
    /// 스프라이트 별로 바꿀 때도 여기만 고치면 된다.
    /// </summary>
    public static class CStar_Utility
    {
        private const char CHAR_FILLED = '★';
        private const char CHAR_EMPTY  = '☆';

        /// <param name="iStar"> 획득한 별 </param>
        /// <param name="iMax"> 그 맵의 웨이브 수 </param>
        public static string Get_Text(int iStar, int iMax)
        {
            if (iMax <= 0)
                return string.Empty;

            StringBuilder sbText = new StringBuilder(iMax);
            for (int i = 0; i < iMax; ++i)
                sbText.Append(i < iStar ? CHAR_FILLED : CHAR_EMPTY);

            return sbText.ToString();
        }
    }
}
