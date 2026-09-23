using System.Collections.Generic;
using UnityEngine;

namespace Client
{
    // 260923_긋는 중인 선을 띠 메시로 만든다
    /// <summary>
    /// 폴리라인 하나를 굵기가 일정한 띠로 바꾼다.
    ///
    /// **LineRenderer를 쓰지 않는 이유.** 유니티의 LineRenderer는 꺾인 자리를 둥글리거나 이어 붙이는데,
    /// **구간이 굵기보다 짧으면 그 이음매가 무너진다.** 이 게임은 한 프레임에 0.15칸씩 긋고 선 굵기가
    /// 0.55칸이라, 방향을 꺾은 직후 서너 프레임은 항상 그 상태가 된다 — 꺾을 때마다 선이 한순간
    /// 일그러져 보였다.
    ///
    /// 여기서는 **구간마다 사각형 하나를 따로 만들고, 꺾인 자리에 삼각형 하나를 덧대** 벌어진 바깥쪽을
    /// 메운다. 사각형은 자기 구간만 보고 만들어지므로 구간이 아무리 짧아도 모양이 무너지지 않는다
    /// (이어진 띠 하나로 만들면 짧은 구간에서 안쪽이 접혀 삼각형이 뒤집힌다).
    /// 안쪽에서 조금 겹치는 것은 단색이라 눈에 띄지 않는다.
    ///
    /// 화면을 모른다(Vector 계산뿐) — CProtoTest가 꺾인 각 · 짧은 구간을 넣어 검증한다.
    /// </summary>
    public static class CTrailMesh_Utility
    {
        private const float MIN_SEGMENT = 1e-6f;    // 이보다 짧은 구간 · 납작한 삼각형은 만들지 않는다

        /// <summary>
        /// 폴리라인을 띠로 만들어 목록 끝에 덧붙인다. 여러 번 불러 여러 조각(선 · 불머리)을 한 메시에 담는다.
        /// </summary>
        /// <param name="lstPoint"> 이어진 점들(월드). 두 개 미만이면 아무것도 안 한다 </param>
        /// <param name="fWidth"> 띠 전체 굵기 </param>
        public static void Append(IReadOnlyList<Vector3> lstPoint, float fWidth, Color cColor,
                                  List<Vector3> lstVertex, List<int> lstIndex, List<Color> lstColor)
        {
            if (lstPoint == null || lstPoint.Count < 2 || fWidth <= 0f)
                return;

            float   fHalf    = fWidth * 0.5f;
            Vector2 vPrevDir = Vector2.zero;
            Vector3 vPrevEnd = Vector3.zero;

            for (int i = 0; i + 1 < lstPoint.Count; ++i)
            {
                Vector3 vFrom = lstPoint[i];
                Vector3 vTo   = lstPoint[i + 1];

                Vector2 vDir = new Vector2(vTo.x - vFrom.x, vTo.y - vFrom.y);
                if (vDir.sqrMagnitude < MIN_SEGMENT)
                    continue;

                vDir.Normalize();
                Vector2 vSide = new Vector2(-vDir.y, vDir.x) * fHalf;

                // 구간 하나 = 사각형 하나. 자기 구간만 보므로 짧아도 정상이다
                Add_Triangle(lstVertex, lstIndex, lstColor, cColor,
                             Offset(vFrom, vSide), Offset(vTo, vSide), Offset(vFrom, -vSide));
                Add_Triangle(lstVertex, lstIndex, lstColor, cColor,
                             Offset(vTo, vSide), Offset(vTo, -vSide), Offset(vFrom, -vSide));

                // 꺾인 자리 — 바깥쪽에 벌어진 쐐기를 삼각형 하나로 메운다
                if (vPrevDir != Vector2.zero)
                    Add_Join(lstVertex, lstIndex, lstColor, cColor, vPrevEnd, vPrevDir, vDir, fHalf);

                vPrevDir = vDir;
                vPrevEnd = vTo;
            }
        }

        // 두 구간이 만나는 점에서 바깥쪽(꺾이는 반대쪽)으로 벌어진 틈을 메운다
        private static void Add_Join(List<Vector3> lstVertex, List<int> lstIndex, List<Color> lstColor, Color cColor,
                                     Vector3 vAt, Vector2 vPrevDir, Vector2 vNextDir, float fHalf)
        {
            float fCross = vPrevDir.x * vNextDir.y - vPrevDir.y * vNextDir.x;
            if (Mathf.Abs(fCross) < MIN_SEGMENT)
                return;     // 곧게 이어짐 — 메울 틈이 없다

            // 왼쪽으로 꺾으면 바깥은 오른쪽이다
            float fSign = fCross > 0f ? -1f : 1f;
            Vector2 vPrevSide = new Vector2(-vPrevDir.y, vPrevDir.x) * (fHalf * fSign);
            Vector2 vNextSide = new Vector2(-vNextDir.y, vNextDir.x) * (fHalf * fSign);

            Add_Triangle(lstVertex, lstIndex, lstColor, cColor, vAt, Offset(vAt, vPrevSide), Offset(vAt, vNextSide));
        }

        // 삼각형 하나를 넣는다. 유니티는 시계 방향이 앞면이라 그 순서로 맞춰 넣는다 —
        // 반대로 들어가면 컬링을 켠 재질에서 그 조각만 사라져 구멍처럼 보인다.
        private static void Add_Triangle(List<Vector3> lstVertex, List<int> lstIndex, List<Color> lstColor, Color cColor,
                                         Vector3 a, Vector3 b, Vector3 c)
        {
            float fCross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
            if (Mathf.Abs(fCross) < MIN_SEGMENT)
                return;     // 납작한 삼각형은 넣지 않는다

            int iBase = lstVertex.Count;
            lstVertex.Add(a);
            lstVertex.Add(fCross < 0f ? b : c);
            lstVertex.Add(fCross < 0f ? c : b);

            lstColor.Add(cColor);
            lstColor.Add(cColor);
            lstColor.Add(cColor);

            lstIndex.Add(iBase);
            lstIndex.Add(iBase + 1);
            lstIndex.Add(iBase + 2);
        }

        private static Vector3 Offset(Vector3 v, Vector2 vOffset) => new Vector3(v.x + vOffset.x, v.y + vOffset.y, v.z);
    }
}
