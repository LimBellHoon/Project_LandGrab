using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260921_스테이지 카드의 흐린 미리보기 (2-23)
    /// <summary>
    /// 로비에서 스테이지 보상 그림을 **흐리게** 보여 준다 — 무엇이 걸려 있는지는 짐작되지만
    /// 다 보이지는 않아야 들어가 보고 싶어진다(이 게임의 재미가 '드러내는 순간'이다, 2-5).
    ///
    /// 셰이더를 쓰지 않는다(2-3과 같은 결정 — 모바일에서 셰이더 호환을 따로 챙기지 않으려고).
    /// 작게 줄여 상자 블러를 두 번 건 뒤 양선형 필터로 늘려 그린다 — 줄이는 것만으로도 대부분의
    /// 흐림이 나오고, 상자 블러는 줄였을 때 남는 계단을 지운다. 한 번 만든 결과는 이름으로 캐시한다.
    ///
    /// 원본은 Read/Write가 켜져 있어야 한다 — 보상 그림은 가림막으로도 쓰이므로 이미 켜져 있다(2-5).
    /// 읽을 수 없으면 원본을 그대로 돌려준다(흐리지 않을 뿐 화면은 나온다).
    /// </summary>
    public static class CBlur_Utility
    {
        private const int WORK_WIDTH = 48;     // 이 폭으로 줄여서 흐린다. 작을수록 더 흐리다

        private static readonly Dictionary<string, Texture2D> s_dicCache = new Dictionary<string, Texture2D>();

        public static Texture Get_Blurred(string strName, Texture texSource)
        {
            if (texSource == null || string.IsNullOrEmpty(strName) == true)
                return texSource;

            if (s_dicCache.TryGetValue(strName, out Texture2D texCached) == true && texCached != null)
                return texCached;

            Texture2D texReadable = texSource as Texture2D;
            if (texReadable == null || texReadable.isReadable == false)
                return texSource;

            Texture2D texBlur = Make_Blurred(texReadable, WORK_WIDTH, 2);
            s_dicCache[strName] = texBlur;
            return texBlur;
        }

        /// <summary> 줄이고 → 상자 블러 → 결과 텍스처. 화면 없이 테스트할 수 있게 공개해 둔다. </summary>
        public static Texture2D Make_Blurred(Texture2D texSource, int iWorkWidth, int iPass)
        {
            int iW = Mathf.Max(2, iWorkWidth);
            int iH = Mathf.Max(2, Mathf.RoundToInt((float)texSource.height / texSource.width * iW));

            Color[] arrPixel = new Color[iW * iH];
            for (int y = 0; y < iH; ++y)
            {
                for (int x = 0; x < iW; ++x)
                    arrPixel[y * iW + x] = texSource.GetPixelBilinear((x + 0.5f) / iW, (y + 0.5f) / iH);
            }

            for (int i = 0; i < iPass; ++i)
                arrPixel = Box_Blur(arrPixel, iW, iH);

            Texture2D texResult = new Texture2D(iW, iH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
            };
            texResult.SetPixels(arrPixel);
            texResult.Apply();
            return texResult;
        }

        // 3x3 평균. 가장자리는 안쪽 칸만 센다(바깥을 검게 보지 않게).
        private static Color[] Box_Blur(Color[] arrSource, int iW, int iH)
        {
            Color[] arrResult = new Color[arrSource.Length];

            for (int y = 0; y < iH; ++y)
            {
                for (int x = 0; x < iW; ++x)
                {
                    Color cSum = Color.clear;
                    int iCount = 0;

                    for (int dy = -1; dy <= 1; ++dy)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= iH)
                            continue;

                        for (int dx = -1; dx <= 1; ++dx)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= iW)
                                continue;

                            cSum += arrSource[ny * iW + nx];
                            ++iCount;
                        }
                    }

                    arrResult[y * iW + x] = cSum / Mathf.Max(1, iCount);
                }
            }

            return arrResult;
        }
    }
}
