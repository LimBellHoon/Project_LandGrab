using System.Collections.Generic;
using UnityEngine;

namespace Client
{
    // 260923_긋는 중인 선을 띠 메시로 만든다
    // 260924_스타일을 입힐 수 있게 — 그라디언트(꼭짓점 색) · UV · 굵기를 CTrailStyle로 받는다
    /// <summary>
    /// 폴리라인 하나를 굵기가 일정한 띠로 바꾼다.
    ///
    /// **`LineRenderer` · `TrailRenderer`를 쓰지 않는 이유.** 둘 다 같은 띠 생성기를 쓰는데,
    /// **구간이 굵기보다 짧으면 이음매가 무너진다.** 이 게임은 한 프레임에 0.15칸씩 긋기 때문에
    /// 방향을 꺾은 직후 서너 프레임이 늘 그 상태라, 꺾을 때마다 선이 한순간 일그러져 보였다.
    /// `TrailRenderer`는 그 위에 세 가지가 더 걸린다 — 점이 시간으로 사라져 "점령할 때까지 유지"와 맞지 않고,
    /// **도화선이 태운 중간 구간만 빼는 것**이 아예 불가능하며(2-14-1), 어디로든 신발로 반대편에 나타나면
    /// 맵을 가로지르는 선이 그어진다.
    ///
    /// 여기서는 **구간마다 사각형 하나를 따로 만들고, 꺾인 자리에 삼각형 하나를 덧대** 벌어진 바깥쪽을
    /// 메운다. 사각형은 자기 구간만 보고 만들어지므로 구간이 아무리 짧아도 모양이 무너지지 않는다.
    /// 안쪽에서 조금 겹치는 것은 단색이라 눈에 띄지 않는다.
    ///
    /// **스타일은 전부 여기서 열려 있다** — 꼭짓점 색(그라디언트), UV(셰이더가 흘릴 무늬), 굵기.
    /// 화면을 모른다(Vector 계산뿐) — CProtoTest가 꺾인 각 · 짧은 구간을 넣어 검증한다.
    /// </summary>
    public static class CTrailMesh_Utility
    {
        private const float MIN_SEGMENT = 1e-6f;    // 이보다 짧은 구간 · 납작한 삼각형은 만들지 않는다

        /// <summary> 260924_띠 하나를 어떻게 그릴지. 색은 선 전체에서의 위치(0~1)로 섞는다 </summary>
        public struct CTrailStyle
        {
            public float fWidth;        // 띠 굵기(월드)
            public Color cTail;         // 선이 시작된 쪽(꼬리) 색
            public Color cHead;         // 플레이어 쪽(머리) 색
            public float fArcFrom;      // 이 조각이 선 전체에서 차지하는 구간 (0~1)
            public float fArcTo;
            public float fUVPerWorld;   // u = 지나온 거리 * 이 값. 0이면 0~1로 정규화한다(셰이더가 무늬를 흘릴 때 쓴다)
        }

        /// <summary> 폴리라인을 띠로 만들어 목록 끝에 덧붙인다. 여러 번 불러 여러 조각(선 · 불머리 · 글로우)을 한 메시에 담는다. </summary>
        public static void Append(IReadOnlyList<Vector3> lstPoint, CTrailStyle cStyle,
                                  List<Vector3> lstVertex, List<int> lstIndex, List<Color> lstColor, List<Vector2> lstUV)
        {
            if (lstPoint == null || lstPoint.Count < 2 || cStyle.fWidth <= 0f)
                return;

            float fHalf    = cStyle.fWidth * 0.5f;
            float fTotal   = Get_Length(lstPoint);
            float fWalked  = 0f;
            Vector2 vPrevDir = Vector2.zero;
            Vector3 vPrevEnd = Vector3.zero;
            float   fPrevArc = 0f;

            for (int i = 0; i + 1 < lstPoint.Count; ++i)
            {
                Vector3 vFrom = lstPoint[i];
                Vector3 vTo   = lstPoint[i + 1];

                Vector2 vDir = new Vector2(vTo.x - vFrom.x, vTo.y - vFrom.y);
                float   fLen = vDir.magnitude;
                if (fLen < MIN_SEGMENT)
                    continue;

                vDir /= fLen;
                Vector2 vSide = new Vector2(-vDir.y, vDir.x) * fHalf;

                float fArcFrom = fWalked;
                float fArcTo   = fWalked + fLen;
                fWalked = fArcTo;

                // 구간 하나 = 사각형 하나. 자기 구간만 보므로 짧아도 정상이다
                CVertex vA = Make(vFrom,  vSide, fArcFrom, 0f, cStyle, fTotal);
                CVertex vB = Make(vFrom, -vSide, fArcFrom, 1f, cStyle, fTotal);
                CVertex vC = Make(vTo,    vSide, fArcTo,   0f, cStyle, fTotal);
                CVertex vD = Make(vTo,   -vSide, fArcTo,   1f, cStyle, fTotal);

                Add_Triangle(lstVertex, lstIndex, lstColor, lstUV, vA, vC, vB);
                Add_Triangle(lstVertex, lstIndex, lstColor, lstUV, vC, vD, vB);

                // 꺾인 자리 — 바깥쪽에 벌어진 쐐기를 삼각형 하나로 메운다
                if (vPrevDir != Vector2.zero)
                    Add_Join(lstVertex, lstIndex, lstColor, lstUV, vPrevEnd, vPrevDir, vDir, fHalf, fPrevArc, cStyle, fTotal);

                vPrevDir = vDir;
                vPrevEnd = vTo;
                fPrevArc = fArcTo;
            }
        }

        public static float Get_Length(IReadOnlyList<Vector3> lstPoint)
        {
            float fLength = 0f;
            for (int i = 0; i + 1 < lstPoint.Count; ++i)
                fLength += Vector3.Distance(lstPoint[i], lstPoint[i + 1]);

            return fLength;
        }

        private struct CVertex
        {
            public Vector3 vPos;
            public Color   cColor;
            public Vector2 vUV;
        }

        // 한 점의 좌우 오프셋 · 색 · UV를 한 번에 만든다. 색은 선 전체에서의 위치로 섞는다
        private static CVertex Make(Vector3 vAt, Vector2 vOffset, float fArc, float fSide, CTrailStyle cStyle, float fTotal)
        {
            float fLocal  = fTotal > MIN_SEGMENT ? Mathf.Clamp01(fArc / fTotal) : 0f;
            float fGlobal = Mathf.Lerp(cStyle.fArcFrom, cStyle.fArcTo, fLocal);
            float fU      = cStyle.fUVPerWorld > 0f ? fArc * cStyle.fUVPerWorld : fLocal;

            return new CVertex
            {
                vPos   = new Vector3(vAt.x + vOffset.x, vAt.y + vOffset.y, vAt.z),
                cColor = Color.Lerp(cStyle.cTail, cStyle.cHead, fGlobal),
                vUV    = new Vector2(fU, fSide),
            };
        }

        // 두 구간이 만나는 점에서 바깥쪽(꺾이는 반대쪽)으로 벌어진 틈을 메운다
        private static void Add_Join(List<Vector3> lstVertex, List<int> lstIndex, List<Color> lstColor, List<Vector2> lstUV,
                                     Vector3 vAt, Vector2 vPrevDir, Vector2 vNextDir, float fHalf, float fArc,
                                     CTrailStyle cStyle, float fTotal)
        {
            float fCross = vPrevDir.x * vNextDir.y - vPrevDir.y * vNextDir.x;
            if (Mathf.Abs(fCross) < MIN_SEGMENT)
                return;     // 곧게 이어짐 — 메울 틈이 없다

            // 왼쪽으로 꺾으면 바깥은 오른쪽이다
            float fSign = fCross > 0f ? -1f : 1f;
            Vector2 vPrevSide = new Vector2(-vPrevDir.y, vPrevDir.x) * (fHalf * fSign);
            Vector2 vNextSide = new Vector2(-vNextDir.y, vNextDir.x) * (fHalf * fSign);
            float   fSide     = fSign > 0f ? 0f : 1f;

            Add_Triangle(lstVertex, lstIndex, lstColor, lstUV,
                         Make(vAt, Vector2.zero, fArc, 0.5f, cStyle, fTotal),
                         Make(vAt, vPrevSide,    fArc, fSide, cStyle, fTotal),
                         Make(vAt, vNextSide,    fArc, fSide, cStyle, fTotal));
        }

        // 삼각형 하나를 넣는다. 유니티는 시계 방향이 앞면이라 그 순서로 맞춰 넣는다 —
        // 반대로 들어가면 컬링을 켠 재질에서 그 조각만 사라져 구멍처럼 보인다.
        private static void Add_Triangle(List<Vector3> lstVertex, List<int> lstIndex, List<Color> lstColor, List<Vector2> lstUV,
                                         CVertex a, CVertex b, CVertex c)
        {
            float fCross = (b.vPos.x - a.vPos.x) * (c.vPos.y - a.vPos.y) - (b.vPos.y - a.vPos.y) * (c.vPos.x - a.vPos.x);
            if (Mathf.Abs(fCross) < MIN_SEGMENT)
                return;     // 납작한 삼각형은 넣지 않는다

            int iBase = lstVertex.Count;
            Add_Vertex(lstVertex, lstColor, lstUV, a);
            Add_Vertex(lstVertex, lstColor, lstUV, fCross < 0f ? b : c);
            Add_Vertex(lstVertex, lstColor, lstUV, fCross < 0f ? c : b);

            lstIndex.Add(iBase);
            lstIndex.Add(iBase + 1);
            lstIndex.Add(iBase + 2);
        }

        private static void Add_Vertex(List<Vector3> lstVertex, List<Color> lstColor, List<Vector2> lstUV, CVertex cVertex)
        {
            lstVertex.Add(cVertex.vPos);
            lstColor.Add(cVertex.cColor);
            lstUV.Add(cVertex.vUV);
        }
    }
}
