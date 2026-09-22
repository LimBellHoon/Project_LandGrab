using System;
using System.Collections.Generic;

using Clipper2Lib;

using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 영토 그리드 (상태 + 트레일 + 플러드필 점령)
    // 260923_땅을 칸이 아니라 **다각형**으로 든다 (2-3). 선은 선분, 점령은 빈 땅을 선으로 잘라 가장 큰 조각만 남긴다.
    /// <summary>
    /// 땅의 진짜 모양은 다각형(m_pOwned)이다. 칸으로 들면 사선 · 원이 계단이 되어 그 모양으로 점령할 수 없었다.
    /// 자르기 · 합치기는 Clipper2가 하고, 가벼운 판정(안에 있나 · 가장 가까운 경계 · 선분 교차)은 CPolygon_Utility가 한다.
    ///
    /// 칸 격자(m_arrCell)는 **다각형을 다시 찍은 조회용 사본**으로 남는다 — 스폰 자리 찾기처럼 칸 하나 정밀도면 되는 곳이
    /// 그대로 쓰게 하려는 것이다. 규칙(점령 · 선 · 점령률)은 전부 다각형으로 판정한다. 사본은 점령지가 바뀔 때만 다시 찍는다.
    ///
    /// 좌표는 두 가지다.
    ///   · 칸 (x, y)        — Vector2Int. 칸 (0,0)이 좌하단
    ///   · 그리드 공간 (gx, gy) — Vector2. 칸 하나가 1이고 칸 (x, y)는 [x, x+1) × [y, y+1)을 차지한다. 다각형은 이 공간에 있다
    /// MonoBehaviour가 아닌 순수 클래스라 화면 없이 테스트한다.
    /// </summary>
    public class CTerritoryGrid
    {
        private const int DIR_COUNT_ALL = 8;    // 260920_이동 입력용 — 뒤 넷은 대각선(2-22)
        // MOVE_DIR(UP, DOWN, LEFT, RIGHT, 대각 넷) 순서와 인덱스를 맞춘다.
        private static readonly int[] ARR_DIR_X = { 0, 0, -1, 1, -1,  1, -1, 1 };
        private static readonly int[] ARR_DIR_Y = { 1, -1, 0, 0,  1,  1, -1, -1 };

        // 260923_다각형 계산 정밀도와 여유값. 전부 그리드 공간(칸 하나 = 1) 기준이다
        private const int    CLIP_PRECISION    = 3;         // 0.001칸까지 본다
        private const double TRAIL_STRIP_HALF  = 0.01;      // 점령할 때 선을 이만큼 두께로 부풀려 빈 땅을 가른다
        private const float  TRAIL_END_BITE    = 0.05f;     // 선 양 끝을 점령지 안쪽으로 이만큼 늘린다 — 끝이 딱 맞닿으면 틈으로 샌다
        private const float  ENTRY_PROBE       = 0.005f;    // 경계를 넘은 직후 이만큼 들어간 점이 점령지 안이어야 '들어갔다'로 본다
        private const float  MERGE_COS         = 0.9999f;   // 같은 방향으로 이어지는 선은 점을 늘리지 않고 끝점만 옮긴다
        private const int    ERODE_CIRCLE_STEP = 20;

        private int         m_iWidth;
        private int         m_iHeight;
        private float       m_fCellSize;
        private Vector2     m_vOrigin;          // 칸 (0,0)의 좌하단 월드 좌표

        // 조회용 칸 사본 — OWNED · EMPTY · BLOCK. 다각형이 바뀔 때마다 다시 찍는다(Rasterize)
        private CELL_STATE[] m_arrCell;
        // 260904_맵 모양 마스크. Reset이 BLOCK을 되살려야 하므로 셀 상태와 따로 들고 있는다.
        private bool[]       m_arrBlocked;

        // 260923_진짜 모양
        private PathsD              m_pPlayable = new PathsD();     // 맵 전체에서 잘라낸 칸(BLOCK)을 뺀 것
        private PathsD              m_pOwned    = new PathsD();
        private double              m_dPlayableArea;
        private double              m_dOwnedArea;
        private readonly List<Vector2[]> m_lstRing  = new List<Vector2[]>();   // 점령지 경계 고리 — 걷기 · 판정용
        private readonly List<Rect>      m_lstBound = new List<Rect>();

        // 260923_선 — 조각 여러 개일 수 있다(어디로든 신발로 좌우 끝을 넘으면 거기서 끊기고 반대편에서 이어진다)
        private readonly List<List<Vector2>> m_lstTrailPiece = new List<List<Vector2>>();
        private float m_fTrailLength;

        private readonly List<float> m_lstRowCross = new List<float>();

        public int      WIDTH           => m_iWidth;
        public int      HEIGHT          => m_iHeight;
        public float    CELL_SIZE       => m_fCellSize;
        public Vector2  ORIGIN          => m_vOrigin;
        public bool     IS_DRAWING      => m_lstTrailPiece.Count > 0;
        /// <summary> 260923_지금 긋고 있는 선의 길이(칸). 예전의 '선 칸 수'와 같은 뜻이다 </summary>
        public float    TRAIL_LENGTH    => m_fTrailLength;
        public float    OWNED_RATIO     => m_dPlayableArea > 0.0 ? (float)(m_dOwnedArea / m_dPlayableArea) : 0f;
        /// <summary> 점령률의 분모 — 잘라낸 칸을 뺀 넓이(칸²). 한 번에 먹은 넓이와 같은 단위다 </summary>
        public int      PLAYABLE_COUNT  => Mathf.RoundToInt((float)m_dPlayableArea);
        /// <summary> 점령지가 바뀔 때마다 오른다 — 경계를 걷는 쪽이 자기 자리를 다시 잡을 때를 안다 </summary>
        public int      OWNED_VERSION   { get; private set; }
        /// <summary> 렌더러가 가림막을 다시 뚫어야 하는지. 렌더 후 Clear_Dirty()로 내린다. </summary>
        public bool     IS_DIRTY        { get; private set; }

        public IReadOnlyList<Vector2[]>       RINGS       => m_lstRing;
        public IReadOnlyList<List<Vector2>>   TRAIL_PIECES => m_lstTrailPiece;

        public Vector2  WORLD_SIZE      => new Vector2(m_iWidth * m_fCellSize, m_iHeight * m_fCellSize);
        public Vector2  WORLD_CENTER    => m_vOrigin + WORLD_SIZE * 0.5f;

        #region Initialize
        /// <param name="vOrigin"> 칸 (0,0)의 좌하단 월드 좌표 </param>
        /// <param name="iBorderThick"> 시작 시 점령된 외곽 테두리 두께(칸). 0이면 테두리를 주지 않는다. </param>
        /// <param name="arrPlayable"> 260904_맵 모양 마스크. 길이 iWidth*iHeight, false인 칸은 BLOCK이 된다. null이면 직사각형 전체 </param>
        /// <param name="iStartRadius"> 260920_'시작 섬'의 반경(칸). 중심에서 상하좌우로 이만큼씩 점령된 채 시작한다. 0이면 없다 </param>
        public bool Initialize(int iWidth, int iHeight, float fCellSize, Vector2 vOrigin, int iBorderThick,
                               bool[] arrPlayable = null, int iStartRadius = 0)
        {
            if (iWidth <= 0 || iHeight <= 0 || fCellSize <= 0f)
            {
                Debug.LogError($"[CTerritoryGrid] 잘못된 그리드 크기 : {iWidth}x{iHeight}, cell {fCellSize}");
                return false;
            }

            int iCellCount = iWidth * iHeight;

            if (arrPlayable != null && arrPlayable.Length != iCellCount)
            {
                Debug.LogError($"[CTerritoryGrid] 모양 마스크 길이가 맞지 않는다 : {arrPlayable.Length}, 기대 {iCellCount}");
                return false;
            }

            m_iWidth    = iWidth;
            m_iHeight   = iHeight;
            m_fCellSize = fCellSize;
            m_vOrigin   = vOrigin;

            if (m_arrCell == null || m_arrCell.Length != iCellCount)
            {
                m_arrCell    = new CELL_STATE[iCellCount];
                m_arrBlocked = new bool[iCellCount];
            }

            for (int i = 0; i < iCellCount; ++i)
                m_arrBlocked[i] = arrPlayable != null && arrPlayable[i] == false;

            Build_Playable();

            m_vStartCenter = new Vector2Int(m_iWidth / 2, m_iHeight / 2);
            Reset(iBorderThick, iStartRadius);
            return true;
        }

        // 맵 직사각형에서 잘라낸 칸을 뺀 모양. 잘라낸 칸이 없으면 직사각형 그대로다.
        private void Build_Playable()
        {
            PathsD pRect = new PathsD { Make_Rect(0f, 0f, m_iWidth, m_iHeight) };

            PathsD pBlocked = new PathsD();
            for (int y = 0; y < m_iHeight; ++y)
            {
                for (int x = 0; x < m_iWidth; ++x)
                {
                    if (m_arrBlocked[To_Index(x, y)] == true)
                        pBlocked.Add(Make_Rect(x, y, x + 1, y + 1));
                }
            }

            m_pPlayable = pBlocked.Count > 0
                        ? Clipper.Difference(pRect, Clipper.Union(pBlocked, FillRule.NonZero), FillRule.NonZero, CLIP_PRECISION)
                        : pRect;
            m_dPlayableArea = Math.Abs(Clipper.Area(m_pPlayable));
        }

        /// <summary>
        /// 점령지를 시작 안전 지대(외곽 테두리 · 시작 섬)만으로 되돌리고 선을 지운다.
        /// 웨이브가 넘어갈 때마다 이 함수로 판을 다시 깐다.
        /// </summary>
        public void Reset(int iBorderThick, int iStartRadius = 0)
        {
            m_lstTrailPiece.Clear();
            m_fTrailLength   = 0f;
            IS_TRAIL_BURNING = false;    // 260924_판을 다시 깔면 타던 도화선도 같이 꺼진다
            Clear_Burn();

            // 260920_0을 허용한다 — 외벽이 점령지가 아니어야 '벽을 찍어서 점령'이 막힌다(2-3).
            iBorderThick = Mathf.Clamp(iBorderThick, 0, Mathf.Min(m_iWidth, m_iHeight) / 2);

            PathsD pStart = new PathsD();
            if (iBorderThick > 0)
            {
                PathsD pRing = Clipper.Difference(new PathsD { Make_Rect(0f, 0f, m_iWidth, m_iHeight) },
                                                  new PathsD { Make_Rect(iBorderThick, iBorderThick,
                                                                         m_iWidth - iBorderThick, m_iHeight - iBorderThick) },
                                                  FillRule.NonZero, CLIP_PRECISION);
                pStart.AddRange(pRing);
            }

            if (iStartRadius > 0)
            {
                Vector2Int vCenter = START_CENTER;
                pStart.Add(Make_Rect(vCenter.x - iStartRadius, vCenter.y - iStartRadius,
                                     vCenter.x + iStartRadius + 1, vCenter.y + iStartRadius + 1));
            }

            PathsD pOwned = pStart.Count > 0
                          ? Clipper.Intersect(Clipper.Union(pStart, FillRule.NonZero), m_pPlayable, FillRule.NonZero, CLIP_PRECISION)
                          : new PathsD();
            Set_Owned(pOwned);
        }

        /// <summary>
        /// 260920_시작 섬의 가운데 칸 — 플레이어가 여기서 시작한다(2-3).
        /// 외벽에서 시작하면 벽을 따라 한 번에 크게 그어 판이 순식간에 끝났다.
        /// 가운데에서 시작하면 어느 방향으로 나가든 **돌아올 거리가 생긴다.**
        /// </summary>
        public Vector2Int START_CENTER => m_vStartCenter;

        // 260921_시작 섬의 가운데. 웨이브마다 슬롯으로 고른 자리에 섬을 깐다(2-3) — 처음 값은 맵 한가운데
        private Vector2Int m_vStartCenter;

        /// <summary> 260921_시작 섬을 vCenter에 깔고 판을 다시 깐다. </summary>
        public void Reset(int iBorderThick, int iStartRadius, Vector2Int vCenter)
        {
            m_vStartCenter = vCenter;
            Reset(iBorderThick, iStartRadius);
        }

        /// <summary>
        /// 260921_vCenter에 반지름 iRadius인 시작 섬을 깔 수 있는가 — 섬 전체와 그 둘레 한 칸이 맵 안이고
        /// 잘린 칸(BLOCK)이 없어야 한다. 둘레 한 칸을 남기는 것은 섬 바깥으로 나갈 길이 있어야 해서다.
        /// </summary>
        public bool Can_PlaceStartArea(Vector2Int vCenter, int iRadius)
        {
            int iReach = Mathf.Max(0, iRadius) + 1;

            for (int y = vCenter.y - iReach; y <= vCenter.y + iReach; ++y)
            {
                for (int x = vCenter.x - iReach; x <= vCenter.x + iReach; ++x)
                {
                    if (Is_InBounds(x, y) == false || m_arrBlocked[To_Index(x, y)] == true)
                        return false;
                }
            }

            return true;
        }
        #endregion Initialize

        #region 좌표 변환
        public int To_Index(int x, int y) => y * m_iWidth + x;
        public bool Is_InBounds(int x, int y) => x >= 0 && x < m_iWidth && y >= 0 && y < m_iHeight;

        /// <summary> 칸의 '중심' 월드 좌표 </summary>
        public Vector3 Cell_ToWorld(int x, int y)
            => new Vector3(m_vOrigin.x + (x + 0.5f) * m_fCellSize, m_vOrigin.y + (y + 0.5f) * m_fCellSize, 0f);
        public Vector3 Cell_ToWorld(Vector2Int vCell) => Cell_ToWorld(vCell.x, vCell.y);

        // 260923_그리드 공간 ↔ 월드
        public Vector3 Grid_ToWorld(Vector2 vGrid)
            => new Vector3(m_vOrigin.x + vGrid.x * m_fCellSize, m_vOrigin.y + vGrid.y * m_fCellSize, 0f);
        public Vector2 World_ToGrid(Vector2 vWorld)
            => new Vector2((vWorld.x - m_vOrigin.x) / m_fCellSize, (vWorld.y - m_vOrigin.y) / m_fCellSize);
        /// <summary> 칸 가운데의 그리드 좌표 </summary>
        public static Vector2 Cell_ToGrid(Vector2Int vCell) => new Vector2(vCell.x + 0.5f, vCell.y + 0.5f);
        /// <summary> 그 점이 들어 있는 칸(맵 밖이면 가장자리 칸으로 끌어당긴다) </summary>
        public Vector2Int Grid_ToCell(Vector2 vGrid)
            => new Vector2Int(Mathf.Clamp(Mathf.FloorToInt(vGrid.x), 0, m_iWidth - 1),
                              Mathf.Clamp(Mathf.FloorToInt(vGrid.y), 0, m_iHeight - 1));

        /// <summary>
        /// 260922_월드 좌표가 맵 안인가. World_ToCell은 맵 밖을 가장자리 칸으로 끌어당기므로 그것만 보면
        /// 맵 밖에 있어도 가장자리 칸의 상태가 나온다 — 외벽을 점령지에서 뺀 뒤로 몬스터가 이 틈으로 맵 밖에 나갔다.
        /// </summary>
        public bool Is_WorldInside(Vector2 vWorld)
        {
            Vector2 vGrid = World_ToGrid(vWorld);
            return vGrid.x >= 0f && vGrid.y >= 0f && vGrid.x < m_iWidth && vGrid.y < m_iHeight;
        }

        public Vector2Int World_ToCell(Vector3 vWorld) => Grid_ToCell(World_ToGrid(vWorld));

        public static Vector2Int Dir_ToOffset(MOVE_DIR eDir)
        {
            int i = (int)eDir;
            if (i < 0 || i >= DIR_COUNT_ALL)
                return Vector2Int.zero;

            return new Vector2Int(ARR_DIR_X[i], ARR_DIR_Y[i]);
        }

        /// <summary> 260923_방향의 단위 벡터. NONE이면 0 </summary>
        public static Vector2 Dir_ToVector(MOVE_DIR eDir) => ((Vector2)Dir_ToOffset(eDir)).normalized;

        /// <summary> 260923_움직인 방향을 가장 가까운 상하좌우 하나로. 거의 멈췄으면 NONE </summary>
        public static MOVE_DIR Vector_ToDir4(Vector2 vDir)
        {
            if (vDir.sqrMagnitude < 1e-8f)
                return MOVE_DIR.NONE;

            if (Mathf.Abs(vDir.x) >= Mathf.Abs(vDir.y))
                return vDir.x > 0f ? MOVE_DIR.RIGHT : MOVE_DIR.LEFT;

            return vDir.y > 0f ? MOVE_DIR.UP : MOVE_DIR.DOWN;
        }

        public static MOVE_DIR Dir_Reverse(MOVE_DIR eDir)
        {
            switch (eDir)
            {
                case MOVE_DIR.UP:    return MOVE_DIR.DOWN;
                case MOVE_DIR.DOWN:  return MOVE_DIR.UP;
                case MOVE_DIR.LEFT:  return MOVE_DIR.RIGHT;
                case MOVE_DIR.RIGHT: return MOVE_DIR.LEFT;
                default:             return MOVE_DIR.NONE;
            }
        }

        // 260920_캐릭터별 이동 방식(2-22)
        public static bool Is_Diagonal(MOVE_DIR eDir) => eDir >= MOVE_DIR.UP_LEFT;

        /// <summary> 대각선을 가로 · 세로 두 방향으로 쪼갠다(점멸이 대각선 입력에서 한 축을 고를 때 쓴다). </summary>
        public static void Dir_Split(MOVE_DIR eDir, out MOVE_DIR eHorizontal, out MOVE_DIR eVertical)
        {
            eHorizontal = MOVE_DIR.NONE;
            eVertical   = MOVE_DIR.NONE;

            if (Is_Diagonal(eDir) == false)
                return;

            Vector2Int vOffset = Dir_ToOffset(eDir);
            eHorizontal = vOffset.x > 0 ? MOVE_DIR.RIGHT : MOVE_DIR.LEFT;
            eVertical   = vOffset.y > 0 ? MOVE_DIR.UP    : MOVE_DIR.DOWN;
        }
        #endregion 좌표 변환

        #region 칸 조회 (다각형을 다시 찍은 사본)
        public CELL_STATE Get_Cell(int x, int y)
        {
            if (Is_InBounds(x, y) == false)
                return CELL_STATE.OWNED;    // 맵 밖은 벽 취급 — 진입 판정에서 걸러진다.

            return m_arrCell[To_Index(x, y)];
        }
        public CELL_STATE Get_Cell(Vector2Int vCell) => Get_Cell(vCell.x, vCell.y);
        /// <summary> 셀 인덱스로 바로 읽는다 </summary>
        public CELL_STATE Get_Cell(int iIndex) => m_arrCell[iIndex];

        // 260904_맵 모양 마스크로 잘라낸 칸 — 플레이어도 몬스터도 못 들어간다.
        public bool Is_Blocked(int x, int y) => Is_InBounds(x, y) == false || m_arrBlocked[To_Index(x, y)] == true;
        public bool Is_Blocked(Vector2Int vCell) => Is_Blocked(vCell.x, vCell.y);

        // 260902_몬스터가 점령지 안에 갇혔을 때 빠져나올 곳 · 스폰 자리를 찾는 용도
        /// <summary> vFrom에서 가장 가까운 eState 칸을 링 탐색으로 찾는다. </summary>
        public bool Try_Find_NearestCell(Vector2Int vFrom, CELL_STATE eState, int iMaxRadius, out Vector2Int vFound)
        {
            vFound = vFrom;

            if (Get_Cell(vFrom) == eState)
                return true;

            for (int r = 1; r <= iMaxRadius; ++r)
            {
                for (int dy = -r; dy <= r; ++dy)
                {
                    for (int dx = -r; dx <= r; ++dx)
                    {
                        // 링의 테두리만 검사 (안쪽은 이전 반복에서 이미 봤다)
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r)
                            continue;

                        int x = vFrom.x + dx;
                        int y = vFrom.y + dy;

                        if (Is_InBounds(x, y) == false || m_arrCell[To_Index(x, y)] != eState)
                            continue;

                        vFound = new Vector2Int(x, y);
                        return true;
                    }
                }
            }

            return false;
        }

        // 칸 가운데가 점령지 안이면 OWNED. 가로줄마다 경계와 만나는 x를 구해 그 사이를 채운다.
        private void Rasterize()
        {
            for (int y = 0; y < m_iHeight; ++y)
            {
                int iRow = y * m_iWidth;
                for (int x = 0; x < m_iWidth; ++x)
                    m_arrCell[iRow + x] = m_arrBlocked[iRow + x] == true ? CELL_STATE.BLOCK : CELL_STATE.EMPTY;

                CPolygon_Utility.Collect_RowCrossings(m_lstRing, y + 0.5f, m_lstRowCross);

                for (int k = 0; k + 1 < m_lstRowCross.Count; k += 2)
                {
                    int x0 = Mathf.Max(0, Mathf.CeilToInt(m_lstRowCross[k] - 0.5f));
                    int x1 = Mathf.Min(m_iWidth - 1, Mathf.CeilToInt(m_lstRowCross[k + 1] - 0.5f) - 1);

                    for (int x = x0; x <= x1; ++x)
                    {
                        if (m_arrCell[iRow + x] == CELL_STATE.EMPTY)
                            m_arrCell[iRow + x] = CELL_STATE.OWNED;
                    }
                }
            }
        }

        public void Clear_Dirty() => IS_DIRTY = false;
        #endregion 칸 조회

        #region 260923_다각형 조회
        /// <summary> 점(그리드 공간)이 점령지 안인가. 경계 바로 위는 어느 쪽으로든 나올 수 있다 </summary>
        public bool Is_OwnedPoint(Vector2 vGrid) => CPolygon_Utility.Is_Inside(m_lstRing, m_lstBound, vGrid);

        /// <summary> 점이 맵 안이고 잘라낸 칸이 아닌가 </summary>
        public bool Is_PlayablePoint(Vector2 vGrid)
        {
            if (vGrid.x < 0f || vGrid.y < 0f || vGrid.x >= m_iWidth || vGrid.y >= m_iHeight)
                return false;

            return m_arrBlocked[To_Index(Mathf.FloorToInt(vGrid.x), Mathf.FloorToInt(vGrid.y))] == false;
        }

        /// <summary> 빈 땅(맵 안이고 점령지가 아닌 곳)인가 </summary>
        public bool Is_EmptyPoint(Vector2 vGrid) => Is_PlayablePoint(vGrid) == true && Is_OwnedPoint(vGrid) == false;

        /// <summary> 점령지 경계에서 가장 가까운 곳. 점령지가 없으면 false </summary>
        public bool Try_Find_NearestBoundary(Vector2 vGrid, out Vector2 vNearest)
            => CPolygon_Utility.Try_Find_Nearest(m_lstRing, vGrid, out vNearest, out int _, out int _, out float _);

        /// <summary> 경계에서 가장 가까운 곳과 그 자리(몇 번째 고리 · 몇 번째 변 · 변 위 위치) — 경계를 걷는 쪽이 쓴다 </summary>
        public bool Try_Find_BoundaryLocation(Vector2 vGrid, out Vector2 vNearest, out int iRing, out int iSeg, out float fT)
            => CPolygon_Utility.Try_Find_Nearest(m_lstRing, vGrid, out vNearest, out iRing, out iSeg, out fT);

        /// <summary> 점령지 경계까지의 거리(칸). 점령지가 없으면 아주 큰 값 </summary>
        public float Distance_ToBoundary(Vector2 vGrid)
            => Try_Find_NearestBoundary(vGrid, out Vector2 vNearest) == true ? Vector2.Distance(vNearest, vGrid) : float.MaxValue;
        #endregion 다각형 조회

        #region 트레일
        /// <summary> 경계 위 vStart에서 선을 긋기 시작한다. </summary>
        public void Begin_Trail(Vector2 vStart)
        {
            m_lstTrailPiece.Clear();
            m_lstTrailPiece.Add(new List<Vector2> { vStart });
            m_fTrailLength = 0f;
            IS_TRAIL_BURNING = false;
            Clear_Burn();
        }

        /// <summary> 260923_선을 여기서 끊고 vStart에서 새 조각을 시작한다 — 어디로든 신발로 반대편에 나타날 때 </summary>
        public void Break_Trail(Vector2 vStart)
        {
            if (IS_DRAWING == false)
                return;

            m_lstTrailPiece.Add(new List<Vector2> { vStart });
        }

        // 같은 방향으로 계속 가면 끝점만 옮긴다 — 점이 프레임마다 늘면 판정 · 그리기가 선 길이만큼 무거워진다
        private void Extend_Trail(Vector2 vPoint)
        {
            List<Vector2> lstPiece = m_lstTrailPiece[m_lstTrailPiece.Count - 1];
            Vector2 vLast = lstPiece[lstPiece.Count - 1];
            float fAdd = Vector2.Distance(vLast, vPoint);
            if (fAdd < 1e-6f)
                return;

            m_fTrailLength += fAdd;

            if (lstPiece.Count >= 2)
            {
                Vector2 vPrev = lstPiece[lstPiece.Count - 2];
                Vector2 vA = (vLast - vPrev).normalized;
                Vector2 vB = (vPoint - vLast).normalized;
                if (Vector2.Dot(vA, vB) >= MERGE_COS)
                {
                    lstPiece[lstPiece.Count - 1] = vPoint;
                    return;
                }
            }

            lstPiece.Add(vPoint);
        }

        /// <summary> 사망 등으로 점령에 실패했을 때 그리던 선을 지운다. </summary>
        public void Clear_Trail()
        {
            m_lstTrailPiece.Clear();
            m_fTrailLength   = 0f;
            IS_TRAIL_BURNING = false;
            Clear_Burn();
        }

        /// <summary> 선의 끝(플레이어가 있는 곳). 선이 없으면 false </summary>
        public bool Try_Get_TrailTip(out Vector2 vTip)
        {
            vTip = Vector2.zero;
            if (IS_DRAWING == false)
                return false;

            List<Vector2> lstPiece = m_lstTrailPiece[m_lstTrailPiece.Count - 1];
            vTip = lstPiece[lstPiece.Count - 1];
            return true;
        }

        /// <summary>
        /// 260923_선에 반경 fRadius(칸) 안으로 닿았는가. 닿았으면 선 시작점부터 잰 그 자리의 길이(fArc)를 돌려준다 —
        /// 도화선(2-3)이 어디서 불붙었는지 · 아슬아슬(2-24)이 얼마나 붙었는지를 같은 판정으로 본다(1-1).
        /// </summary>
        public bool Try_Find_TrailTouch(Vector2 vGrid, float fRadius, out float fArc)
        {
            fArc = 0f;
            float fOffset = 0f;
            float fBest   = float.MaxValue;

            for (int p = 0; p < m_lstTrailPiece.Count; ++p)
            {
                List<Vector2> lstPiece = m_lstTrailPiece[p];
                float fDist = CPolygon_Utility.Distance_ToPolyline(lstPiece, vGrid, out float fLocal);
                if (fDist < fBest)
                {
                    fBest = fDist;
                    fArc  = fOffset + fLocal;
                }

                fOffset += CPolygon_Utility.Get_Length(lstPiece);
            }

            return fBest <= fRadius;
        }

        /// <summary> 선 시작점부터 fArc만큼 간 자리 </summary>
        public Vector2 Get_TrailPoint(float fArc)
        {
            Vector2 vLast = Vector2.zero;

            for (int p = 0; p < m_lstTrailPiece.Count; ++p)
            {
                List<Vector2> lstPiece = m_lstTrailPiece[p];
                vLast = lstPiece[0];

                for (int i = 0; i + 1 < lstPiece.Count; ++i)
                {
                    float fLen = Vector2.Distance(lstPiece[i], lstPiece[i + 1]);
                    if (fArc <= fLen)
                        return Vector2.Lerp(lstPiece[i], lstPiece[i + 1], fLen > 0f ? fArc / fLen : 0f);

                    fArc -= fLen;
                    vLast = lstPiece[i + 1];
                }
            }

            return vLast;
        }

        // 260924_도화선(2-3) — 몬스터가 선에 닿으면 그 지점에서 불이 붙어 선 끝(플레이어)을 향해 타들어온다.
        // 언제 · 얼마나 태울지(타이머 · 속도)는 CStage_Manager가 잰다. 그리드는 "어디부터 어디까지 탔는지"만 들고,
        // 그려질 때 그 구간을 빼는 것은 렌더러가 한다(누가 · 왜 태우는지는 모른다 — 2-3과 같은 이유).
        /// <summary> 불이 붙은 자리(선 길이). 안 타고 있으면 음수 </summary>
        public float BURN_FROM { get; private set; } = -1f;
        /// <summary> 불이 지금 닿은 자리(선 길이) </summary>
        public float BURN_TO   { get; private set; } = -1f;

        public void Set_Burn(float fFrom, float fTo)
        {
            BURN_FROM = fFrom;
            BURN_TO   = Mathf.Max(fFrom, fTo);
        }

        private void Clear_Burn()
        {
            BURN_FROM = -1f;
            BURN_TO   = -1f;
        }

        /// <summary>
        /// 260924_도화선이 타는 동안인가. 켜져 있으면 Step_To가 안전 지대로 돌아와도 점령하지 않고
        /// 트레일만 지운다 — "불보다 먼저 내 땅에 닿으면 선만 잃는다"는 규칙을 여기서 지킨다.
        /// CStage_Manager가 발화 · 소화 시점에 이 값을 켜고 끈다.
        /// </summary>
        public bool IS_TRAIL_BURNING { get; set; }
        #endregion 트레일

        #region 260921_잠식 — 땅 갉는 자
        /// <summary>
        /// vCenter에서 fRange(칸) 안의 가장 가까운 점령지 가장자리를 동그랗게 도로 빈 땅으로 되돌린다.
        /// 한 번에 갉는 넓이가 대략 iCount칸이 되게 반지름을 잡는다(가장자리에 반원만 걸리므로 원 넓이의 절반).
        /// vProtect 둘레 fProtectRadius(칸)에 걸리면 갉지 않는다 — 플레이어 발밑이 사라지면
        /// 선을 긋지도 않았는데 빈 땅 위에 서 버린다. 점령 규칙이 땅을 바꾸는 곳은 여기와 Step_To뿐이다(2-3).
        /// </summary>
        /// <returns> 실제로 갉은 넓이(칸, 반올림). 갉지 못했으면 0 </returns>
        public int Erode_Near(Vector2 vCenter, float fRange, int iCount, Vector2 vProtect, float fProtectRadius)
        {
            if (iCount <= 0 || fRange <= 0f || Try_Find_NearestBoundary(vCenter, out Vector2 vBite) == false)
                return 0;

            if (Vector2.Distance(vBite, vCenter) > fRange)
                return 0;

            float fRadius = Mathf.Sqrt(2f * iCount / Mathf.PI);
            if (Vector2.Distance(vBite, vProtect) < fProtectRadius + fRadius)
                return 0;

            PathD pCircle = Clipper.Ellipse(new PointD(vBite.x, vBite.y), fRadius, fRadius, ERODE_CIRCLE_STEP);
            double dBefore = m_dOwnedArea;
            Set_Owned(Clipper.Difference(m_pOwned, new PathsD { pCircle }, FillRule.NonZero, CLIP_PRECISION));

            return Mathf.RoundToInt((float)(dBefore - m_dOwnedArea));
        }
        #endregion 잠식

        #region 상태 전이
        /// <summary>
        /// 260923_선을 긋는 중 vFrom → vTo로 한 걸음 옮길 때의 판정. **땅따먹기 규칙의 단일 진입점**이다(2-3).
        ///   · 가던 길에 자기 선을 가로지르면  DEAD (그 자리가 vEnd)
        ///   · 점령지로 들어가면              CAPTURE (도화선이 타는 중이면 선만 지우고 SAFE)
        ///   · 그 외                        DRAW — 선을 vTo까지 늘린다
        /// 둘이 한 걸음 안에 다 일어나면 먼저 닿은 쪽이다. 선을 긋기 시작하는 것은 Begin_Trail이다.
        /// </summary>
        /// <param name="iCapturedCount"> CAPTURE일 때 새로 점령한 넓이(칸, 반올림) </param>
        public STEP_RESULT Step_To(Vector2 vFrom, Vector2 vTo, out Vector2 vEnd, out int iCapturedCount)
        {
            iCapturedCount = 0;
            vEnd = vFrom;

            if (IS_DRAWING == false)
                return STEP_RESULT.SAFE;

            float fSelf  = Find_SelfCross(vFrom, vTo);
            float fEntry = Find_OwnedEntry(vFrom, vTo);

            if (fSelf <= 1f && fSelf <= fEntry)
            {
                vEnd = Vector2.Lerp(vFrom, vTo, fSelf);
                return STEP_RESULT.DEAD;
            }

            if (fEntry <= 1f)
            {
                vEnd = Vector2.Lerp(vFrom, vTo, fEntry);
                Extend_Trail(vEnd);

                // 260924_도화선이 타는 동안 돌아왔다 — 불보다 먼저 왔으니 살지만, 점령은 안 된다(2-3).
                if (IS_TRAIL_BURNING == true)
                {
                    Clear_Trail();
                    return STEP_RESULT.SAFE;
                }

                iCapturedCount = Capture();
                return STEP_RESULT.CAPTURE;
            }

            Extend_Trail(vTo);
            vEnd = vTo;
            return STEP_RESULT.DRAW;
        }

        // 자기 선과 처음 만나는 곳(0~1). 없으면 2. 방금 그은 마지막 변은 지금 자리와 붙어 있으니 뺀다.
        private float Find_SelfCross(Vector2 vFrom, Vector2 vTo)
        {
            float fBest = 2f;
            int iLastPiece = m_lstTrailPiece.Count - 1;

            for (int p = 0; p <= iLastPiece; ++p)
            {
                List<Vector2> lstPiece = m_lstTrailPiece[p];
                int iSegCount = lstPiece.Count - 1;
                if (p == iLastPiece)
                    --iSegCount;

                for (int i = 0; i < iSegCount; ++i)
                {
                    if (CPolygon_Utility.Try_Intersect(vFrom, vTo, lstPiece[i], lstPiece[i + 1], out float fT, out float _) == true
                     && fT > 1e-5f && fT < fBest)
                        fBest = fT;
                }
            }

            return fBest;
        }

        // 점령지로 '들어가는' 곳(0~1). 없으면 2. 경계를 스치기만 하거나 경계에서 떠나는 것은 들어간 것이 아니다 —
        // 넘은 직후 조금 더 간 점이 점령지 안이어야 한다(선을 막 긋기 시작한 첫 걸음이 경계 위에서 출발하므로).
        private float Find_OwnedEntry(Vector2 vFrom, Vector2 vTo)
        {
            float fLen = Vector2.Distance(vFrom, vTo);
            if (fLen < 1e-7f)
                return 2f;

            Vector2 vDir = (vTo - vFrom) / fLen;
            float fBest = 2f;

            for (int r = 0; r < m_lstRing.Count; ++r)
            {
                Vector2[] arrRing = m_lstRing[r];
                int iCount = arrRing.Length;

                for (int i = 0; i < iCount; ++i)
                {
                    if (CPolygon_Utility.Try_Intersect(vFrom, vTo, arrRing[i], arrRing[(i + 1) % iCount], out float fT, out float _) == false
                     || fT >= fBest)
                        continue;

                    Vector2 vProbe = vFrom + vDir * (fT * fLen + ENTRY_PROBE);
                    if (Is_OwnedPoint(vProbe) == true)
                        fBest = fT;
                }
            }

            // 변과 만나지 않았는데 끝점이 안이면(경계 바로 위에서 출발해 교차가 0으로 잡힌 경우) 끝점을 들어간 곳으로 본다
            if (fBest > 1f && Is_OwnedPoint(vTo) == true && Is_OwnedPoint(vFrom) == false)
                fBest = 1f;

            return fBest;
        }
        #endregion 상태 전이

        #region 점령
        /// <summary>
        /// 선이 점령지에 닿아 도형이 닫혔을 때 부른다. 빈 땅을 선으로 갈라 **가장 넓은 조각 하나만 남기고 나머지를 전부 점령한다.**
        /// 선을 아주 얇은 띠로 부풀려 빈 땅에서 빼면 빈 땅이 여러 조각으로 나뉜다 — 칸 시절의 플러드필과 같은 규칙을
        /// 다각형으로 한 것이라, 선이 어떤 모양이든(사선 · 원 · 여러 번 꺾임) 같은 방식으로 닫힌다.
        ///
        /// 260920_가두면 무조건 먹고, **그 안에 있던 몬스터는 죽는다** — 죽이는 것은 몬스터를 들고 있는
        /// CStage_Manager가 한다(여기는 땅만 안다). 점령이 곧 공격 수단이다.
        /// </summary>
        /// <returns> 이번에 새로 점령한 넓이(칸, 반올림) </returns>
        public int Capture()
        {
            if (IS_DRAWING == false)
                return 0;

            PathsD pLine = new PathsD();
            for (int p = 0; p < m_lstTrailPiece.Count; ++p)
                pLine.Add(To_BittenPath(m_lstTrailPiece[p]));

            PathsD pStrip = Clipper.InflatePaths(pLine, TRAIL_STRIP_HALF, JoinType.Miter, EndType.Butt, 2.0, CLIP_PRECISION);
            PathsD pWall  = Clipper.Union(m_pOwned, pStrip, FillRule.NonZero, CLIP_PRECISION);

            PolyTreeD cTree = new PolyTreeD();
            Clipper.BooleanOp(ClipType.Difference, m_pPlayable, pWall, cTree, FillRule.NonZero, CLIP_PRECISION);

            // 빈 땅 조각마다 (바깥 고리 + 구멍) 넓이를 재어 가장 넓은 것 하나만 남긴다
            s_lstComponent.Clear();
            s_lstComponentArea.Clear();
            for (int i = 0; i < cTree.Count; ++i)
                Collect_Component(cTree[i]);

            int iLargest = -1;
            for (int i = 0; i < s_lstComponent.Count; ++i)
            {
                if (iLargest < 0 || s_lstComponentArea[i] > s_lstComponentArea[iLargest])
                    iLargest = i;
            }

            double dBefore = m_dOwnedArea;
            PathsD pNewOwned = iLargest >= 0
                             ? Clipper.Difference(m_pPlayable, s_lstComponent[iLargest], FillRule.NonZero, CLIP_PRECISION)
                             : new PathsD(m_pPlayable);

            Clear_Trail();
            Set_Owned(pNewOwned);

            return Mathf.Max(0, Mathf.RoundToInt((float)(m_dOwnedArea - dBefore)));
        }

        private static readonly List<PathsD> s_lstComponent     = new List<PathsD>();
        private static readonly List<double> s_lstComponentArea = new List<double>();

        // 바깥 고리 하나 = 조각 하나. 그 구멍들을 같이 담고, 구멍 안에 또 있는 바깥 고리는 따로 조각으로 센다.
        private static void Collect_Component(PolyPathD cOuter)
        {
            if (cOuter.Polygon == null)
                return;

            PathsD pPaths = new PathsD { cOuter.Polygon };
            double dArea  = Math.Abs(Clipper.Area(cOuter.Polygon));

            for (int h = 0; h < cOuter.Count; ++h)
            {
                PolyPathD cHole = cOuter[h];
                if (cHole.Polygon != null)
                {
                    pPaths.Add(cHole.Polygon);
                    dArea -= Math.Abs(Clipper.Area(cHole.Polygon));
                }

                for (int o = 0; o < cHole.Count; ++o)
                    Collect_Component(cHole[o]);
            }

            s_lstComponent.Add(pPaths);
            s_lstComponentArea.Add(dArea);
        }

        // 선 조각을 다각형 경로로 — 양 끝을 가던 방향으로 조금씩 더 늘린다(점령지 안쪽으로 파고들게)
        private static PathD To_BittenPath(List<Vector2> lstPiece)
        {
            PathD pPath = new PathD(lstPiece.Count);
            for (int i = 0; i < lstPiece.Count; ++i)
            {
                Vector2 v = lstPiece[i];

                if (lstPiece.Count >= 2 && i == 0)
                    v -= (lstPiece[1] - lstPiece[0]).normalized * TRAIL_END_BITE;
                else if (lstPiece.Count >= 2 && i == lstPiece.Count - 1)
                    v += (lstPiece[i] - lstPiece[i - 1]).normalized * TRAIL_END_BITE;

                pPath.Add(new PointD(v.x, v.y));
            }

            return pPath;
        }

        // 점령지를 바꾸는 유일한 자리 — 넓이 · 경계 고리 · 칸 사본을 한꺼번에 맞춘다
        private void Set_Owned(PathsD pOwned)
        {
            m_pOwned     = pOwned ?? new PathsD();
            m_dOwnedArea = Math.Abs(Clipper.Area(m_pOwned));

            m_lstRing.Clear();
            m_lstBound.Clear();
            for (int i = 0; i < m_pOwned.Count; ++i)
            {
                Vector2[] arrRing = To_Ring(m_pOwned[i]);
                if (arrRing.Length < 3)
                    continue;

                m_lstRing.Add(arrRing);
                m_lstBound.Add(CPolygon_Utility.Get_Bound(arrRing));
            }

            Rasterize();
            ++OWNED_VERSION;
            IS_DIRTY = true;
        }

        private static Vector2[] To_Ring(PathD pPath)
        {
            List<Vector2> lstPoint = new List<Vector2>(pPath.Count);
            for (int i = 0; i < pPath.Count; ++i)
            {
                Vector2 v = new Vector2((float)pPath[i].x, (float)pPath[i].y);
                if (lstPoint.Count == 0 || (lstPoint[lstPoint.Count - 1] - v).sqrMagnitude > 1e-10f)
                    lstPoint.Add(v);
            }

            if (lstPoint.Count > 1 && (lstPoint[0] - lstPoint[lstPoint.Count - 1]).sqrMagnitude <= 1e-10f)
                lstPoint.RemoveAt(lstPoint.Count - 1);

            return lstPoint.ToArray();
        }

        private static PathD Make_Rect(float x0, float y0, float x1, float y1)
            => new PathD { new PointD(x0, y0), new PointD(x1, y0), new PointD(x1, y1), new PointD(x0, y1) };
        #endregion 점령
    }
}
