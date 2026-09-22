using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260923_다각형 땅 — 점 · 선분 · 고리(닫힌 다각형) 계산만 모았다 (2-3)
    /// <summary>
    /// 땅을 칸이 아니라 다각형으로 들면서 필요해진 기하 계산이다. 자르기 · 합치기 같은 불리언 연산은 Clipper2가 하고,
    /// 여기는 매 프레임 가볍게 부르는 것 — 안에 있나 · 가장 가까운 점 · 선분 교차 · 가로줄이 경계와 만나는 x — 만 한다.
    /// 화면 없이 테스트할 수 있게 전부 static이다(CProtoTest).
    ///
    /// 고리는 끝점을 되풀이하지 않는 닫힌 점 배열이다(마지막 점 다음이 첫 점). 좌표는 '그리드 공간'(칸 하나 = 1)이다.
    /// 여러 고리를 **짝홀 규칙**으로 본다 — Clipper 결과는 겹치지 않는 바깥 고리와 구멍 고리라 짝홀로 읽어도 같다.
    /// </summary>
    public static class CPolygon_Utility
    {
        /// <summary> 점이 고리들 안에 있는가(짝홀). 고리마다 경계 상자로 먼저 거른다. </summary>
        public static bool Is_Inside(IReadOnlyList<Vector2[]> lstRing, IReadOnlyList<Rect> lstBound, Vector2 vPoint)
        {
            bool bInside = false;

            for (int r = 0; r < lstRing.Count; ++r)
            {
                if (lstBound != null && lstBound[r].Contains(vPoint) == false)
                    continue;

                Vector2[] arrRing = lstRing[r];
                int iCount = arrRing.Length;

                for (int i = 0, j = iCount - 1; i < iCount; j = i++)
                {
                    Vector2 a = arrRing[i];
                    Vector2 b = arrRing[j];

                    if ((a.y > vPoint.y) != (b.y > vPoint.y)
                     && vPoint.x < (b.x - a.x) * (vPoint.y - a.y) / (b.y - a.y) + a.x)
                        bInside = !bInside;
                }
            }

            return bInside;
        }

        /// <summary> 선분 위에서 점에 가장 가까운 곳. fT는 a(0) → b(1) 사이 위치. </summary>
        public static Vector2 Closest_OnSegment(Vector2 a, Vector2 b, Vector2 vPoint, out float fT)
        {
            Vector2 vAB = b - a;
            float fLenSq = vAB.sqrMagnitude;
            fT = fLenSq > 1e-12f ? Mathf.Clamp01(Vector2.Dot(vPoint - a, vAB) / fLenSq) : 0f;
            return a + vAB * fT;
        }

        /// <summary>
        /// 고리들의 경계에서 점에 가장 가까운 곳. 어느 고리 · 몇 번째 변(i → i+1) · 변 위 위치까지 돌려준다.
        /// 고리가 하나도 없으면 false.
        /// </summary>
        public static bool Try_Find_Nearest(IReadOnlyList<Vector2[]> lstRing, Vector2 vPoint,
                                            out Vector2 vNearest, out int iRing, out int iSeg, out float fT)
        {
            vNearest = vPoint;
            iRing = -1;
            iSeg  = -1;
            fT    = 0f;
            float fBestSq = float.MaxValue;

            for (int r = 0; r < lstRing.Count; ++r)
            {
                Vector2[] arrRing = lstRing[r];
                int iCount = arrRing.Length;

                for (int i = 0; i < iCount; ++i)
                {
                    Vector2 vOn = Closest_OnSegment(arrRing[i], arrRing[(i + 1) % iCount], vPoint, out float fSegT);
                    float fSq = (vOn - vPoint).sqrMagnitude;
                    if (fSq >= fBestSq)
                        continue;

                    fBestSq  = fSq;
                    vNearest = vOn;
                    iRing    = r;
                    iSeg     = i;
                    fT       = fSegT;
                }
            }

            return iRing >= 0;
        }

        /// <summary>
        /// 두 선분 p1→p2, q1→q2가 만나는가. fT는 p 쪽 위치(0~1), fU는 q 쪽 위치. 평행하면 false.
        /// </summary>
        public static bool Try_Intersect(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2, out float fT, out float fU)
        {
            fT = 0f;
            fU = 0f;

            Vector2 r = p2 - p1;
            Vector2 s = q2 - q1;
            float fDenom = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(fDenom) < 1e-9f)
                return false;

            Vector2 qp = q1 - p1;
            fT = (qp.x * s.y - qp.y * s.x) / fDenom;
            fU = (qp.x * r.y - qp.y * r.x) / fDenom;
            return fT >= 0f && fT <= 1f && fU >= 0f && fU <= 1f;
        }

        /// <summary>
        /// 가로줄 y가 고리들의 변과 만나는 x들을 작은 순으로 모은다. 짝을 지어(0-1, 2-3 …) 채우면 안쪽이다.
        /// 칸 격자를 다시 찍을 때와 가림막을 픽셀로 뚫을 때 같이 쓴다.
        /// </summary>
        public static void Collect_RowCrossings(IReadOnlyList<Vector2[]> lstRing, float fY, List<float> lstOut)
        {
            lstOut.Clear();

            for (int r = 0; r < lstRing.Count; ++r)
            {
                Vector2[] arrRing = lstRing[r];
                int iCount = arrRing.Length;

                for (int i = 0, j = iCount - 1; i < iCount; j = i++)
                {
                    Vector2 a = arrRing[i];
                    Vector2 b = arrRing[j];

                    if ((a.y > fY) == (b.y > fY))
                        continue;

                    lstOut.Add(a.x + (fY - a.y) * (b.x - a.x) / (b.y - a.y));
                }
            }

            lstOut.Sort();
        }

        /// <summary> 고리의 경계 상자. </summary>
        public static Rect Get_Bound(Vector2[] arrRing)
        {
            if (arrRing == null || arrRing.Length == 0)
                return new Rect();

            Vector2 vMin = arrRing[0];
            Vector2 vMax = arrRing[0];
            for (int i = 1; i < arrRing.Length; ++i)
            {
                vMin = Vector2.Min(vMin, arrRing[i]);
                vMax = Vector2.Max(vMax, arrRing[i]);
            }

            // Rect.Contains는 오른쪽 · 위 끝을 빼므로 살짝 넓힌다 — 경계 위의 점이 빠지지 않게
            return Rect.MinMaxRect(vMin.x - 1e-4f, vMin.y - 1e-4f, vMax.x + 1e-4f, vMax.y + 1e-4f);
        }

        /// <summary> 폴리라인 위에서 점에 가장 가까운 거리와 그 자리의 호 길이(시작점부터 잰 길이). </summary>
        public static float Distance_ToPolyline(IReadOnlyList<Vector2> lstLine, Vector2 vPoint, out float fArc)
        {
            fArc = 0f;
            if (lstLine == null || lstLine.Count == 0)
                return float.MaxValue;

            if (lstLine.Count == 1)
                return Vector2.Distance(lstLine[0], vPoint);

            float fBestSq = float.MaxValue;
            float fWalked = 0f;

            for (int i = 0; i + 1 < lstLine.Count; ++i)
            {
                Vector2 vOn = Closest_OnSegment(lstLine[i], lstLine[i + 1], vPoint, out float fT);
                float fLen = Vector2.Distance(lstLine[i], lstLine[i + 1]);
                float fSq  = (vOn - vPoint).sqrMagnitude;

                if (fSq < fBestSq)
                {
                    fBestSq = fSq;
                    fArc    = fWalked + fLen * fT;
                }

                fWalked += fLen;
            }

            return Mathf.Sqrt(fBestSq);
        }

        public static float Get_Length(IReadOnlyList<Vector2> lstLine)
        {
            float fLength = 0f;
            for (int i = 0; lstLine != null && i + 1 < lstLine.Count; ++i)
                fLength += Vector2.Distance(lstLine[i], lstLine[i + 1]);
            return fLength;
        }
    }
}
