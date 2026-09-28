using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 영토 그리드 (상태 + 트레일 + 플러드필 점령)
    // 260928_로그라이트 전면 재작성 — 다각형(Clipper2, 260923)을 걷어내고 칸 배열 + 플러드필로 되돌린다
    // (Docs/Design_Roguelite_Rewrite.md 3장). Clipper2는 지우지 않고 참조만 끊는다.
    /// <summary>
    /// 맵을 셀 격자로 관리한다. MonoBehaviour가 아닌 순수 클래스라 화면 없이 테스트한다.
    /// 좌표는 두 가지다.
    ///   · 칸 (x, y)          — Vector2Int. 칸 (0,0)이 좌하단. 이동 · 점령 규칙은 이 좌표로 한다
    ///   · 그리드 공간 (gx, gy) — Vector2. 칸 하나가 1이고 칸 (x, y)의 가운데가 (x+0.5, y+0.5)다.
    ///     연속 좌표가 필요한 옛 호출부(CStage_Manager · CPlayer · CEnemyMoveHandler)가 그대로 쓸 수 있게
    ///     Is_OwnedPoint 같은 점 질의 API 이름 · 시그니처는 다각형 시절 그대로 두고 내부만 칸 조회로 바꿨다.
    /// </summary>
    public class CTerritoryGrid
    {
        private const int DIR_COUNT     = 4;    // 플러드필이 쓰는 4방향 연결
        private const int DIR_COUNT_ALL = 8;    // 이동 입력용 — 뒤 넷은 대각선(2-22, 이번 재작성에서는 미사용)
        // MOVE_DIR(UP, DOWN, LEFT, RIGHT, 대각 넷) 순서와 인덱스를 맞춘다. 앞 8개는 8방향 경계 판정에도 쓴다.
        private static readonly int[] ARR_DIR_X = { 0, 0, -1, 1, -1,  1, -1, 1 };
        private static readonly int[] ARR_DIR_Y = { 1, -1, 0, 0,  1,  1, -1, -1 };

        private int         m_iWidth;
        private int         m_iHeight;
        private float       m_fCellSize;
        private Vector2     m_vOrigin;          // 칸 (0,0)의 좌하단 월드 좌표

        private CELL_STATE[] m_arrCell;
        private bool[]        m_arrBlocked;      // 260904_맵 모양 마스크. Reset이 BLOCK을 되살려야 하므로 따로 든다

        // 플러드필 스크래치 버퍼 — 매 점령마다 재할당하지 않고 재사용해 GC를 막는다.
        private int[]           m_arrRegion;
        private readonly Queue<int>  m_qFill         = new Queue<int>();
        private readonly List<int>   m_lstRegionSize = new List<int>();
        private readonly List<bool>  m_lstRegionSafe = new List<bool>();

        // 260928_트레일 — 규칙 판정용 칸 인덱스 목록과, 렌더러 · 도화선이 쓰는 그리드 공간 폴리라인을 같이 든다.
        // 폴리라인은 이어진 칸끼리 한 조각이고, 어디로든 신발로 반대편에 넘어가면(칸 사이가 안 붙어 있으면)
        // 새 조각을 시작한다 — 화면에 맵을 가로지르는 선이 그어지지 않게.
        private readonly List<int>            m_lstTrailCell  = new List<int>();
        private readonly List<List<Vector2>>  m_lstTrailPiece = new List<List<Vector2>>();
        private float                          m_fTrailLength;

        private int         m_iOwnedCount;
        private int         m_iPlayableCount;

        public int      WIDTH           => m_iWidth;
        public int      HEIGHT          => m_iHeight;
        public float    CELL_SIZE       => m_fCellSize;
        public Vector2  ORIGIN          => m_vOrigin;
        public bool     IS_DRAWING      => m_lstTrailCell.Count > 0;
        /// <summary> 지금 긋고 있는 선의 길이(칸). </summary>
        public float    TRAIL_LENGTH    => m_fTrailLength;
        public float    OWNED_RATIO     => m_iPlayableCount > 0 ? (float)m_iOwnedCount / m_iPlayableCount : 0f;
        /// <summary> 점령률의 분모 — 잘라낸 칸을 뺀 칸 수. </summary>
        public int      PLAYABLE_COUNT  => m_iPlayableCount;
        /// <summary> 렌더러가 가림막을 다시 찍어야 하는지. 렌더 후 Clear_Dirty()로 내린다. </summary>
        public bool     IS_DIRTY        { get; private set; }

        /// <summary> 긋는 중인 선의 그리드 공간 폴리라인(조각 여러 개일 수 있다). 렌더러 · 도화선이 읽는다. </summary>
        public IReadOnlyList<List<Vector2>> TRAIL_PIECES => m_lstTrailPiece;

        public Vector2  WORLD_SIZE      => new Vector2(m_iWidth * m_fCellSize, m_iHeight * m_fCellSize);
        public Vector2  WORLD_CENTER    => m_vOrigin + WORLD_SIZE * 0.5f;

        #region Initialize
        /// <param name="vOrigin"> 칸 (0,0)의 좌하단 월드 좌표 </param>
        /// <param name="iBorderThick"> 시작 시 점령된 외곽 테두리 두께(칸). 0이면 테두리를 주지 않는다. </param>
        /// <param name="arrPlayable"> 맵 모양 마스크. 길이 iWidth*iHeight, false인 칸은 BLOCK이 된다. null이면 직사각형 전체 </param>
        /// <param name="iStartRadius"> '시작 섬'의 반경(칸). 중심에서 상하좌우로 이만큼씩 점령된 채 시작한다. 0이면 없다 </param>
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
                m_arrRegion  = new int[iCellCount];
                m_arrBlocked = new bool[iCellCount];
            }

            for (int i = 0; i < iCellCount; ++i)
                m_arrBlocked[i] = arrPlayable != null && arrPlayable[i] == false;

            m_vStartCenter = new Vector2Int(m_iWidth / 2, m_iHeight / 2);
            Reset(iBorderThick, iStartRadius);
            return true;
        }

        /// <summary>
        /// 전 셀을 EMPTY로 되돌리고 시작 안전 지대(외곽 테두리 · 가운데 시작 섬)만 OWNED로 채운다.
        /// 웨이브가 넘어갈 때마다 이 함수로 판을 다시 깐다.
        /// </summary>
        public void Reset(int iBorderThick, int iStartRadius = 0)
        {
            Clear_Trail();
            m_iOwnedCount    = 0;
            m_iPlayableCount = 0;

            // 260920_0을 허용한다 — 외벽이 점령지가 아니어야 '벽을 찍어서 점령'이 막힌다(2-3).
            iBorderThick = Mathf.Clamp(iBorderThick, 0, Mathf.Min(m_iWidth, m_iHeight) / 2);

            for (int y = 0; y < m_iHeight; ++y)
            {
                for (int x = 0; x < m_iWidth; ++x)
                {
                    int iIndex = To_Index(x, y);

                    if (m_arrBlocked[iIndex] == true)
                    {
                        m_arrCell[iIndex] = CELL_STATE.BLOCK;
                        continue;
                    }

                    ++m_iPlayableCount;

                    bool bBorder = x < iBorderThick || y < iBorderThick
                                || x >= m_iWidth - iBorderThick || y >= m_iHeight - iBorderThick;

                    if (bBorder == true)
                    {
                        m_arrCell[iIndex] = CELL_STATE.OWNED;
                        ++m_iOwnedCount;
                        continue;
                    }

                    m_arrCell[iIndex] = CELL_STATE.EMPTY;
                }
            }

            Fill_StartArea(iStartRadius);
            Set_Dirty();
        }

        /// <summary> 시작 섬의 가운데 칸 — 플레이어가 여기서 시작한다(2-3). </summary>
        public Vector2Int START_CENTER => m_vStartCenter;

        // 260921_시작 섬의 가운데. 웨이브마다 슬롯으로 고른 자리에 섬을 깐다(2-3) — 처음 값은 맵 한가운데
        private Vector2Int m_vStartCenter;

        /// <summary> 시작 섬을 vCenter에 깔고 판을 다시 깐다. </summary>
        public void Reset(int iBorderThick, int iStartRadius, Vector2Int vCenter)
        {
            m_vStartCenter = vCenter;
            Reset(iBorderThick, iStartRadius);
        }

        /// <summary>
        /// vCenter에 반지름 iRadius인 시작 섬을 깔 수 있는가 — 섬 전체와 그 둘레 한 칸이 맵 안이고
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

        private void Fill_StartArea(int iStartRadius)
        {
            if (iStartRadius <= 0)
                return;

            Vector2Int vCenter = START_CENTER;

            for (int y = vCenter.y - iStartRadius; y <= vCenter.y + iStartRadius; ++y)
            {
                for (int x = vCenter.x - iStartRadius; x <= vCenter.x + iStartRadius; ++x)
                {
                    if (Is_InBounds(x, y) == false)
                        continue;

                    int iIndex = To_Index(x, y);
                    if (m_arrCell[iIndex] != CELL_STATE.EMPTY)
                        continue;   // BLOCK(맵 밖)은 건드리지 않는다

                    m_arrCell[iIndex] = CELL_STATE.OWNED;
                    ++m_iOwnedCount;
                }
            }
        }
        #endregion Initialize

        #region 좌표 변환
        public int  To_Index(int x, int y) => y * m_iWidth + x;
        public bool Is_InBounds(int x, int y) => x >= 0 && x < m_iWidth && y >= 0 && y < m_iHeight;

        /// <summary> 칸의 '중심' 월드 좌표 </summary>
        public Vector3 Cell_ToWorld(int x, int y)
            => new Vector3(m_vOrigin.x + (x + 0.5f) * m_fCellSize, m_vOrigin.y + (y + 0.5f) * m_fCellSize, 0f);
        public Vector3 Cell_ToWorld(Vector2Int vCell) => Cell_ToWorld(vCell.x, vCell.y);

        // 그리드 공간(칸 하나 = 1) ↔ 월드
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

        /// <summary> 월드 좌표가 맵 안인가(그리드를 벗어난 좌표를 가장자리 칸으로 끌어당기지 않고 그대로 본다). </summary>
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

        /// <summary> 방향의 단위 벡터. NONE이면 0 </summary>
        public static Vector2 Dir_ToVector(MOVE_DIR eDir) => ((Vector2)Dir_ToOffset(eDir)).normalized;

        /// <summary> 움직인 방향을 가장 가까운 상하좌우 하나로. 거의 멈췄으면 NONE </summary>
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

        // 260902_선분 자동 추적: 진행 방향이 막혔을 때 살펴볼 두 방향
        public static void Dir_Perpendicular(MOVE_DIR eDir, out MOVE_DIR eFirst, out MOVE_DIR eSecond)
        {
            if (eDir == MOVE_DIR.LEFT || eDir == MOVE_DIR.RIGHT)
            {
                eFirst  = MOVE_DIR.UP;
                eSecond = MOVE_DIR.DOWN;
                return;
            }

            eFirst  = MOVE_DIR.LEFT;
            eSecond = MOVE_DIR.RIGHT;
        }

        // 260920_캐릭터별 이동 방식(2-22). 이번 재작성 범위에서는 8방향 · 나선형을 굴리지 않지만
        // 판정 유틸은 남겨 둔다(설계 문서 1장 "보류" — 지우지 않는다).
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

        #region 칸 조회
        public CELL_STATE Get_Cell(int x, int y)
        {
            if (Is_InBounds(x, y) == false)
                return CELL_STATE.OWNED;    // 맵 밖은 벽 취급 — 진입 판정에서 걸러진다.

            return m_arrCell[To_Index(x, y)];
        }
        public CELL_STATE Get_Cell(Vector2Int vCell) => Get_Cell(vCell.x, vCell.y);
        /// <summary> 셀 인덱스로 바로 읽는다 — 렌더러가 나눗셈 없이 훑기 위한 것. </summary>
        public CELL_STATE Get_Cell(int iIndex) => m_arrCell[iIndex];

        // 260904_맵 모양 마스크로 잘라낸 칸 — 플레이어도 몬스터도 못 들어간다.
        public bool Is_Blocked(int x, int y) => Is_InBounds(x, y) == false || m_arrBlocked[To_Index(x, y)] == true;
        public bool Is_Blocked(Vector2Int vCell) => Is_Blocked(vCell.x, vCell.y);

        // 260902_영토의 '선'만 따라 이동
        /// <summary>
        /// 점령지의 경계('선')인가 — 점령지이면서 이웃 8칸 중 하나라도 점령지가 아닌 칸.
        /// 맵 밖도 '점령지가 아닌 것'으로 센다 — 안 그러면 내 땅이 맵 가장자리에 닿을 때
        /// 그 줄이 통째로 '내부'가 되어 가장자리를 따라 걸을 수 없다.
        /// </summary>
        public bool Is_Boundary(int x, int y)
        {
            if (Get_Cell(x, y) != CELL_STATE.OWNED)
                return false;

            for (int d = 0; d < DIR_COUNT_ALL; ++d)
            {
                int nx = x + ARR_DIR_X[d];
                int ny = y + ARR_DIR_Y[d];

                if (Is_InBounds(nx, ny) == false)
                    return true;    // 맵 끝 = 더 먹을 것이 없는 쪽. 여기도 내 땅의 '선'이다

                if (m_arrCell[To_Index(nx, ny)] != CELL_STATE.OWNED)
                    return true;
            }

            return false;
        }
        public bool Is_Boundary(Vector2Int vCell) => Is_Boundary(vCell.x, vCell.y);

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
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r)
                            continue;   // 링의 테두리만 검사(안쪽은 이전 반복에서 이미 봤다)

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

        private bool Try_Find_NearestBoundaryCell(Vector2Int vFrom, int iMaxRadius, out Vector2Int vFound)
        {
            vFound = vFrom;

            if (Is_Boundary(vFrom) == true)
                return true;

            for (int r = 1; r <= iMaxRadius; ++r)
            {
                for (int dy = -r; dy <= r; ++dy)
                {
                    for (int dx = -r; dx <= r; ++dx)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r)
                            continue;

                        Vector2Int vCell = new Vector2Int(vFrom.x + dx, vFrom.y + dy);
                        if (Is_InBounds(vCell.x, vCell.y) == false || Is_Boundary(vCell) == false)
                            continue;

                        vFound = vCell;
                        return true;
                    }
                }
            }

            return false;
        }

        public void Clear_Dirty() => IS_DIRTY = false;
        private void Set_Dirty() => IS_DIRTY = true;
        #endregion 칸 조회

        #region 점 질의 (다각형 시절과 같은 이름 · 시그니처 — 내부만 칸 조회로 바뀌었다)
        /// <summary> 점이 맵 안이고 잘라낸 칸이 아닌가 </summary>
        public bool Is_PlayablePoint(Vector2 vGrid)
        {
            if (vGrid.x < 0f || vGrid.y < 0f || vGrid.x >= m_iWidth || vGrid.y >= m_iHeight)
                return false;

            return m_arrBlocked[To_Index(Mathf.FloorToInt(vGrid.x), Mathf.FloorToInt(vGrid.y))] == false;
        }

        /// <summary> 점(그리드 공간)이 점령지 안인가 — 그 칸이 OWNED인가. </summary>
        public bool Is_OwnedPoint(Vector2 vGrid)
            => Is_PlayablePoint(vGrid) == true && Get_Cell(Grid_ToCell(vGrid)) == CELL_STATE.OWNED;

        /// <summary> 빈 땅(맵 안이고 점령지가 아닌 곳)인가 </summary>
        public bool Is_EmptyPoint(Vector2 vGrid) => Is_PlayablePoint(vGrid) == true && Is_OwnedPoint(vGrid) == false;

        /// <summary> 점령지 경계에서 가장 가까운 곳(그 경계 칸의 가운데). 점령지가 없으면 false </summary>
        public bool Try_Find_NearestBoundary(Vector2 vGrid, out Vector2 vNearest)
        {
            Vector2Int vFrom = Grid_ToCell(vGrid);

            if (Try_Find_NearestBoundaryCell(vFrom, m_iWidth + m_iHeight, out Vector2Int vFound) == true)
            {
                vNearest = Cell_ToGrid(vFound);
                return true;
            }

            vNearest = vGrid;
            return false;
        }

        /// <summary> 점령지 경계까지의 거리(칸). 점령지가 없으면 아주 큰 값 </summary>
        public float Distance_ToBoundary(Vector2 vGrid)
            => Try_Find_NearestBoundary(vGrid, out Vector2 vNearest) == true ? Vector2.Distance(vNearest, vGrid) : float.MaxValue;
        #endregion 점 질의

        #region 트레일
        // 미점령 셀을 밟았을 때 선분을 남긴다. vFrom(밟기 전 칸)은 렌더러 · 도화선이 쓰는 그리드 공간
        // 폴리라인을 만드는 데만 쓴다 — 규칙 판정은 vTo(=도착 칸)만 본다.
        private void Add_Trail(Vector2Int vFrom, Vector2Int vTo)
        {
            bool bFirstEver = m_lstTrailCell.Count == 0;

            int iIndex = To_Index(vTo.x, vTo.y);
            m_arrCell[iIndex] = CELL_STATE.TRAIL;
            m_lstTrailCell.Add(iIndex);
            Set_Dirty();

            Vector2 vToPoint  = Cell_ToGrid(vTo);
            bool    bAdjacent = Mathf.Abs(vTo.x - vFrom.x) + Mathf.Abs(vTo.y - vFrom.y) == 1;

            if (bFirstEver == true)
            {
                // 안전 지대를 처음 떠나는 순간 — 나간 자리(vFrom, 내 땅)부터 그어 경계와 이어져 보이게 한다
                m_lstTrailPiece.Add(new List<Vector2> { Cell_ToGrid(vFrom), vToPoint });
            }
            else if (bAdjacent == true)
            {
                m_lstTrailPiece[m_lstTrailPiece.Count - 1].Add(vToPoint);
            }
            else
            {
                // 어디로든 신발로 반대편으로 넘어갔다 — 여기서 선을 끊고 새 조각을 시작한다
                m_lstTrailPiece.Add(new List<Vector2> { vToPoint });
            }

            m_fTrailLength += 1f;
        }

        /// <summary> 사망 등으로 점령에 실패했을 때 그리던 선을 지운다. </summary>
        public void Clear_Trail()
        {
            for (int i = 0; i < m_lstTrailCell.Count; ++i)
                m_arrCell[m_lstTrailCell[i]] = CELL_STATE.EMPTY;

            if (m_lstTrailCell.Count > 0)
                Set_Dirty();

            m_lstTrailCell.Clear();
            m_lstTrailPiece.Clear();
            m_fTrailLength   = 0f;
            IS_TRAIL_BURNING = false;
            Clear_Burn();
        }

        /// <summary>
        /// 선에 반경 fRadius(칸) 안으로 닿았는가. 닿았으면 선 시작점부터 잰 그 자리의 길이(fArc)를 돌려준다 —
        /// 도화선이 어디서 불붙었는지 · 아슬아슬이 얼마나 붙었는지를 같은 판정으로 본다(1-1).
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

        /// <summary> 선 시작점부터 fArc만큼 간 자리(그리드 공간) </summary>
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

        // 260924_도화선(2-3, 2-14-1) — 몬스터가 선에 닿으면 그 지점에서 불이 붙어 선 끝(플레이어)을 향해
        // 타들어온다. 언제 · 얼마나 태울지(타이머 · 속도)는 CStage_Manager가 잰다. 그리드는 "어디부터 어디까지
        // 탔는지"만 들고, 그려질 때 그 구간을 빼는 것은 렌더러가 한다.
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
        /// 도화선이 타는 동안인가. 켜져 있으면 Step_To가 안전 지대로 돌아와도 점령하지 않고
        /// 트레일만 지운다. CStage_Manager가 발화 · 소화 시점에 이 값을 켜고 끈다.
        /// </summary>
        public bool IS_TRAIL_BURNING { get; set; }
        #endregion 트레일

        #region 굴절 (원본 스펙 §2.3, G08 카드 전용 — Docs/Design_Roguelite_Rewrite.md 3-3)
        /// <summary>
        /// 260928_굴절 — 몬스터가 트레일에 닿았을 때(도화선 발화와 같은 판정, Try_Find_TrailTouch) 발화 대신
        /// 쓸 수 있는 대안이다. 닿은 트레일 칸(vTouchCell) 하나를 없애고, 그 자리를 vAwayDir 방향으로
        /// iDepth칸만큼 밀어낸 'ㄷ'자 우회 칸들로 갈아 끼운다 — 다각형 시절이었다면 그 정점을 통째로 옮기고
        /// 교차 검사를 다시 돌려야 했지만, 칸 배열로 돌아온 지금은 그 칸 앞뒤(A·B)만 그대로 두고
        /// 가운데 한 칸을 우회로로 바꿔 끼우는 문제로 줄어든다.
        ///
        /// 우회로는 A(닿은 칸 바로 앞) → vAwayDir로 iDepth칸 → 선 방향으로 두 칸(A·B 사이 간격만큼) →
        /// vAwayDir 반대로 iDepth칸 → B(닿은 칸 바로 뒤)로 이어지는 사각 우회다. 그래서 다음 조건을 전부
        /// 요구한다 — 하나라도 어긋나면 아무것도 바꾸지 않고 false를 돌려준다(호출부가 도화선으로 폴백할 것,
        /// 스펙의 폴백 규칙 그대로).
        ///   · vTouchCell이 지금 트레일의 '중간' 칸이어야 한다(맨 처음·맨 끝 칸은 앞 또는 뒤가 없어 우회를
        ///     다시 이을 수 없다)
        ///   · 그 앞뒤 두 칸(A, B)이 정확히 한 방향으로 곧게 뻗어 있어야 한다(꺾이는 자리는 지원하지 않는다 —
        ///     그 경우도 그대로 도화선으로 떨어진다)
        ///   · vAwayDir은 그 방향과 직각인 상하좌우 단위 벡터여야 한다
        ///   · 우회로가 지나갈 칸이 전부 맵 안이고 빈 땅(EMPTY)이어야 한다(벽 · 점령지 · 이미 그은 선과 겹치면 안 된다)
        /// </summary>
        /// <param name="vTouchCell"> 몬스터가 닿은 트레일 칸(Try_Find_TrailTouch가 돌려준 자리를 Grid_ToCell로 칸으로 바꾼 것) </param>
        /// <param name="vAwayDir"> 밀어낼 방향(보통 몬스터 반대쪽) — 상하좌우 단위 벡터 하나 </param>
        /// <param name="iDepth"> 밀어낼 칸 수(1 이상) </param>
        /// <param name="iInsertedCount"> 성공했을 때 새로 트레일에 들어간 칸 수(참고용 — 트레일 길이가 이만큼 늘었다) </param>
        public bool Try_Insert_Detour(Vector2Int vTouchCell, Vector2Int vAwayDir, int iDepth, out int iInsertedCount)
        {
            iInsertedCount = 0;

            if (iDepth < 1 || Mathf.Abs(vAwayDir.x) + Mathf.Abs(vAwayDir.y) != 1)
                return false;

            int iAt = m_lstTrailCell.IndexOf(To_Index(vTouchCell.x, vTouchCell.y));
            if (iAt <= 0 || iAt >= m_lstTrailCell.Count - 1)
                return false;   // 처음 · 끝 칸이거나 트레일에 없는 칸이다 — 앞뒤를 이을 수 없다

            Vector2Int vA = Index_ToCell(m_lstTrailCell[iAt - 1]);
            Vector2Int vB = Index_ToCell(m_lstTrailCell[iAt + 1]);
            Vector2Int vDiff = vB - vA;

            // A-C-B가 곧게 뻗어 있어야 한다 — 정확히 한 축으로 2칸 떨어져 있어야 그 사이(C)가 가운데다
            if (vDiff.x != 0 && vDiff.y != 0)
                return false;
            if (Mathf.Abs(vDiff.x) + Mathf.Abs(vDiff.y) != 2)
                return false;

            Vector2Int vDir = new Vector2Int(vDiff.x / 2, vDiff.y / 2);
            if (vDir.x * vAwayDir.x + vDir.y * vAwayDir.y != 0)
                return false;   // 밀어낼 방향은 선 방향과 직각이어야 한다(옆으로 비켜서는 것이지 앞뒤로 찌르는 게 아니다)

            s_lstDetour.Clear();
            for (int k = 1; k <= iDepth; ++k)
                s_lstDetour.Add(vA + vAwayDir * k);
            s_lstDetour.Add(vA + vAwayDir * iDepth + vDir);
            s_lstDetour.Add(vB + vAwayDir * iDepth);
            for (int k = iDepth - 1; k >= 1; --k)
                s_lstDetour.Add(vB + vAwayDir * k);

            for (int i = 0; i < s_lstDetour.Count; ++i)
            {
                Vector2Int vCell = s_lstDetour[i];
                if (Is_InBounds(vCell.x, vCell.y) == false || Get_Cell(vCell) != CELL_STATE.EMPTY)
                    return false;
            }

            // 유효성 확인이 끝났다 — 이제 실제로 갈아 끼운다. C는 트레일에서 빠지므로 다시 빈 땅이 된다.
            m_arrCell[To_Index(vTouchCell.x, vTouchCell.y)] = CELL_STATE.EMPTY;

            m_lstTrailCell.RemoveAt(iAt);
            for (int i = 0; i < s_lstDetour.Count; ++i)
            {
                Vector2Int vCell = s_lstDetour[i];
                int iIndex = To_Index(vCell.x, vCell.y);
                m_arrCell[iIndex] = CELL_STATE.TRAIL;
                m_lstTrailCell.Insert(iAt + i, iIndex);
            }

            iInsertedCount   = s_lstDetour.Count;
            m_fTrailLength   = m_lstTrailCell.Count;
            Rebuild_TrailPieces();
            return true;
        }

        private static readonly List<Vector2Int> s_lstDetour = new List<Vector2Int>();

        private Vector2Int Index_ToCell(int iIndex) => new Vector2Int(iIndex % m_iWidth, iIndex / m_iWidth);

        // 굴절처럼 트레일 칸 배열 가운데를 통째로 갈아 끼운 뒤에는, 매 스텝 붙여 나가던 Add_Trail의
        // 증분 방식 대신 m_lstTrailCell 전체를 훑어 조각(m_lstTrailPiece)을 처음부터 다시 만든다 —
        // 드문 호출이라(카드가 있을 때 발화 대신 한 번) 매 프레임 비용을 걱정할 자리가 아니다.
        // 첫 조각의 맨 앞 점(안전 지대를 나간 자리)은 이미 있던 조각에서 그대로 가져온다 — 그 정보는
        // 트레일 칸 배열에 없다(그 칸 자체가 트레일이 아니라 내 땅이었으므로).
        private void Rebuild_TrailPieces()
        {
            if (m_lstTrailCell.Count == 0 || m_lstTrailPiece.Count == 0)
                return;

            Vector2 vEntry = m_lstTrailPiece[0][0];
            m_lstTrailPiece.Clear();

            Vector2Int vPrevCell = Index_ToCell(m_lstTrailCell[0]);
            m_lstTrailPiece.Add(new List<Vector2> { vEntry, Cell_ToGrid(vPrevCell) });

            for (int i = 1; i < m_lstTrailCell.Count; ++i)
            {
                Vector2Int vCell = Index_ToCell(m_lstTrailCell[i]);
                bool bAdjacent = Mathf.Abs(vCell.x - vPrevCell.x) + Mathf.Abs(vCell.y - vPrevCell.y) == 1;

                if (bAdjacent == true)
                    m_lstTrailPiece[m_lstTrailPiece.Count - 1].Add(Cell_ToGrid(vCell));
                else
                    m_lstTrailPiece.Add(new List<Vector2> { Cell_ToGrid(vCell) });

                vPrevCell = vCell;
            }
        }
        #endregion 굴절

        #region 잠식 — 땅 갉는 자
        /// <summary>
        /// vCenter에서 fRange(칸) 안의 가장 가까운 점령지 가장자리를 동그랗게 도로 빈 땅으로 되돌린다.
        /// 한 번에 갉는 넓이가 대략 iCount칸이 되게 반지름을 잡는다. vProtect 둘레 fProtectRadius(칸)에
        /// 걸리면 갉지 않는다 — 플레이어 발밑이 사라지면 선을 긋지도 않았는데 빈 땅 위에 서 버린다.
        /// </summary>
        /// <returns> 실제로 갉은 칸 수 </returns>
        public int Erode_Near(Vector2 vCenter, float fRange, int iCount, Vector2 vProtect, float fProtectRadius)
        {
            if (iCount <= 0 || fRange <= 0f)
                return 0;

            Vector2Int vCenterCell  = Grid_ToCell(vCenter);
            Vector2Int vProtectCell = Grid_ToCell(vProtect);

            s_lstErode.Clear();
            int   iReach   = Mathf.CeilToInt(fRange);
            float fRangeSq = fRange * fRange;

            for (int dy = -iReach; dy <= iReach; ++dy)
            {
                for (int dx = -iReach; dx <= iReach; ++dx)
                {
                    int x = vCenterCell.x + dx;
                    int y = vCenterCell.y + dy;
                    int iDistSq = dx * dx + dy * dy;

                    if (iDistSq > fRangeSq || Can_Erode(x, y) == false)
                        continue;

                    if (Mathf.Abs(x - vProtectCell.x) <= fProtectRadius && Mathf.Abs(y - vProtectCell.y) <= fProtectRadius)
                        continue;

                    s_lstErode.Add(new Vector3Int(x, y, iDistSq));
                }
            }

            s_lstErode.Sort((a, b) => a.z.CompareTo(b.z));

            int iEroded = 0;
            for (int i = 0; i < s_lstErode.Count && iEroded < iCount; ++i)
            {
                if (Erode(new Vector2Int(s_lstErode[i].x, s_lstErode[i].y)) == true)
                    ++iEroded;
            }

            return iEroded;
        }

        private static readonly List<Vector3Int> s_lstErode = new List<Vector3Int>();

        private bool Erode(Vector2Int vCell)
        {
            if (Can_Erode(vCell.x, vCell.y) == false)
                return false;

            int iIndex = To_Index(vCell.x, vCell.y);
            m_arrCell[iIndex] = CELL_STATE.EMPTY;
            --m_iOwnedCount;
            Set_Dirty();
            return true;
        }

        // 점령한 칸이고, 상하좌우 중 하나가 빈 땅이다(맵 끝은 빈 땅이 아니다 — 벽 쪽 가장자리는 안 갉힌다)
        private bool Can_Erode(int x, int y)
        {
            if (Is_InBounds(x, y) == false || m_arrCell[To_Index(x, y)] != CELL_STATE.OWNED)
                return false;

            for (int d = 0; d < DIR_COUNT; ++d)
            {
                int nx = x + ARR_DIR_X[d];
                int ny = y + ARR_DIR_Y[d];
                if (Is_InBounds(nx, ny) == true && m_arrCell[To_Index(nx, ny)] == CELL_STATE.EMPTY)
                    return true;
            }

            return false;
        }
        #endregion 잠식

        #region 상태 전이
        /// <summary>
        /// 플레이어가 한 칸에서 다른 칸으로 '도착'했을 때의 상태 전이. 땅따먹기 규칙의 단일 진입점(2-3) —
        /// CMoveHandler(플레이어) · CProtoTest 모두 이 함수만 호출한다.
        /// </summary>
        /// <param name="iCapturedCount"> CAPTURE일 때 새로 점령한 칸 개수 </param>
        public STEP_RESULT Step_To(Vector2Int vFrom, Vector2Int vTo, out int iCapturedCount)
        {
            iCapturedCount = 0;

            switch (Get_Cell(vTo))
            {
                case CELL_STATE.BLOCK:
                    return STEP_RESULT.SAFE;

                case CELL_STATE.TRAIL:
                    return STEP_RESULT.DEAD;

                case CELL_STATE.EMPTY:
                    Add_Trail(vFrom, vTo);
                    return STEP_RESULT.DRAW;

                // 안전 지대 — 선을 그리던 중이었다면 도형이 닫힌 것이므로 점령한다
                default:
                    if (IS_DRAWING == false)
                        return STEP_RESULT.SAFE;

                    // 260924_도화선이 타는 동안 돌아오면 점령하지 않는다 — "불보다 먼저 오면 선만 잃는다"(2-14-1)
                    if (IS_TRAIL_BURNING == true)
                    {
                        Clear_Trail();
                        return STEP_RESULT.SAFE;
                    }

                    iCapturedCount = Capture();
                    return STEP_RESULT.CAPTURE;
            }
        }
        #endregion 상태 전이

        #region 점령 (플러드필)
        /// <summary>
        /// 트레일이 안전 지대에 닿아 도형이 닫혔을 때 호출한다.
        /// 트레일을 점령지로 승격시킨 뒤, **가장 넓은 영역 하나만 남기고 나머지를 전부 점령한다.**
        /// 가두면 무조건 먹고, 그 안에 있던 몬스터는 죽는다 — 죽이는 것은 몬스터를 들고 있는
        /// CStage_Manager가 한다(여기는 칸만 안다). 점령이 곧 공격 수단이다.
        /// </summary>
        /// <returns> 이번에 새로 점령한 칸 개수 </returns>
        public int Capture()
        {
            if (m_lstTrailCell.Count == 0)
                return 0;

            // 1. 트레일 → 점령지
            for (int i = 0; i < m_lstTrailCell.Count; ++i)
            {
                m_arrCell[m_lstTrailCell[i]] = CELL_STATE.OWNED;
                ++m_iOwnedCount;
            }
            int iCapturedCount = m_lstTrailCell.Count;

            m_lstTrailCell.Clear();
            m_lstTrailPiece.Clear();
            m_fTrailLength   = 0f;
            IS_TRAIL_BURNING = false;
            Clear_Burn();

            // 2. 남은 EMPTY 영역들을 라벨링
            int iRegionCount = Label_EmptyRegions();
            if (iRegionCount == 0)
            {
                Set_Dirty();
                return iCapturedCount;
            }

            // 3. 가장 넓은 영역 하나만 남긴다 — 그게 '아직 안 먹은 바깥'이다.
            m_lstRegionSafe[Find_LargestRegion()] = true;

            // 4. 남기지 않은 영역 = 플레이어가 가둔 영역 → 전부 점령
            for (int i = 0; i < m_arrCell.Length; ++i)
            {
                int iRegion = m_arrRegion[i];
                if (iRegion < 0 || m_lstRegionSafe[iRegion] == true)
                    continue;

                m_arrCell[i] = CELL_STATE.OWNED;
                ++m_iOwnedCount;
                ++iCapturedCount;
            }

            Set_Dirty();
            return iCapturedCount;
        }

        /// <summary> EMPTY 셀들을 4방향 연결 영역으로 묶어 ID를 매긴다. </summary>
        private int Label_EmptyRegions()
        {
            m_lstRegionSize.Clear();
            m_lstRegionSafe.Clear();

            for (int i = 0; i < m_arrRegion.Length; ++i)
                m_arrRegion[i] = -1;

            int iRegionId = 0;

            for (int iStart = 0; iStart < m_arrCell.Length; ++iStart)
            {
                if (m_arrCell[iStart] != CELL_STATE.EMPTY || m_arrRegion[iStart] >= 0)
                    continue;

                int iSize = 0;
                m_qFill.Clear();
                m_qFill.Enqueue(iStart);
                m_arrRegion[iStart] = iRegionId;

                while (m_qFill.Count > 0)
                {
                    int iCur = m_qFill.Dequeue();
                    ++iSize;

                    int cx = iCur % m_iWidth;
                    int cy = iCur / m_iWidth;

                    for (int d = 0; d < DIR_COUNT; ++d)
                    {
                        int nx = cx + ARR_DIR_X[d];
                        int ny = cy + ARR_DIR_Y[d];

                        if (Is_InBounds(nx, ny) == false)
                            continue;

                        int iNext = To_Index(nx, ny);
                        if (m_arrCell[iNext] != CELL_STATE.EMPTY || m_arrRegion[iNext] >= 0)
                            continue;

                        m_arrRegion[iNext] = iRegionId;
                        m_qFill.Enqueue(iNext);
                    }
                }

                m_lstRegionSize.Add(iSize);
                m_lstRegionSafe.Add(false);
                ++iRegionId;
            }

            return iRegionId;
        }

        private int Find_LargestRegion()
        {
            int iBest = 0;
            for (int i = 1; i < m_lstRegionSize.Count; ++i)
            {
                if (m_lstRegionSize[i] > m_lstRegionSize[iBest])
                    iBest = i;
            }
            return iBest;
        }
        #endregion 점령 (플러드필)
    }
}
