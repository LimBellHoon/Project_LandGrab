using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 영토 그리드 (상태 + 트레일 + 플러드필 점령)
    /// <summary>
    /// 맵을 셀 격자로 관리한다. MonoBehaviour가 아닌 순수 클래스이므로 테스트/재사용이 쉽다.
    /// 좌표계: 셀 (0,0)이 좌하단. 인덱스 = y * W + x.
    /// </summary>
    public class CTerritoryGrid
    {
        private const int DIR_COUNT = 4;        // 플러드필이 쓰는 4방향 연결. 대각선을 여기 넣으면 점령 판정이 새 나간다
        private const int DIR_COUNT_ALL = 8;    // 260920_이동 입력용 — 뒤 넷은 대각선(2-22)
        // MOVE_DIR(UP, DOWN, LEFT, RIGHT) 순서와 인덱스를 맞춘다.
        private static readonly int[] ARR_DIR_X = { 0, 0, -1, 1, -1,  1, -1, 1 };
        private static readonly int[] ARR_DIR_Y = { 1, -1, 0, 0,  1,  1, -1, -1 };

        // 260902_경계 판정은 8방향. 4방향만 보면 테두리의 모서리 칸이 경계에서 빠져 길이 끊긴다.
        private const int DIR8_COUNT = 8;
        private static readonly int[] ARR_DIR8_X = { 0, 0, -1, 1, -1, 1, -1, 1 };
        private static readonly int[] ARR_DIR8_Y = { 1, -1, 0, 0, 1, 1, -1, -1 };

        private int         m_iWidth;
        private int         m_iHeight;
        private float       m_fCellSize;
        private Vector2     m_vOrigin;          // 셀 (0,0)의 좌하단 월드 좌표

        private CELL_STATE[] m_arrCell;

        // 플러드필 스크래치 버퍼 — 매 점령마다 재할당하지 않고 재사용해 GC를 막는다.
        private int[]           m_arrRegion;        // -1: EMPTY가 아님, 0 이상: 영역 ID
        private Queue<int>      m_qFill         = new Queue<int>();
        private List<int>       m_lstRegionSize = new List<int>();
        private List<bool>      m_lstRegionSafe = new List<bool>();   // 몬스터가 들어있는 영역 = 점령 불가

        private List<int>       m_lstTrail      = new List<int>();    // 현재 그리는 중인 트레일 셀 인덱스(순서 보존)

        private int             m_iOwnedCount;
        private int             m_iPlayableCount;   // BLOCK을 뺀 칸 수 = 점령률의 분모

        // 260904_맵 모양 마스크. Reset이 BLOCK을 되살려야 하므로 셀 상태와 따로 들고 있는다.
        private bool[]          m_arrBlocked;

        // 260904_바뀐 칸만 다시 그리기 위한 목록.
        // 그리는 중에는 매 프레임 한두 칸만 바뀌는데 전체를 다시 찍으면 모바일에서 낭비가 크다.
        // 점령처럼 한 번에 많이 바뀔 때는 목록 대신 IS_FULL_DIRTY로 전체 갱신을 요청한다.
        private readonly List<int> m_lstDirtyCell = new List<int>();

        public int      WIDTH           => m_iWidth;
        public int      HEIGHT          => m_iHeight;
        public float    CELL_SIZE       => m_fCellSize;
        public Vector2  ORIGIN          => m_vOrigin;
        public bool     IS_DRAWING      => m_lstTrail.Count > 0;
        public int      TRAIL_COUNT     => m_lstTrail.Count;
        public float    OWNED_RATIO     => m_iPlayableCount > 0 ? (float)m_iOwnedCount / m_iPlayableCount : 0f;
        public int      PLAYABLE_COUNT  => m_iPlayableCount;
        /// <summary> 렌더러가 다시 그려야 하는지 여부. 렌더 후 Clear_Dirty()로 내린다. </summary>
        public bool     IS_DIRTY        { get; private set; }
        /// <summary> true면 DIRTY_CELLS를 무시하고 전부 다시 그려야 한다. </summary>
        public bool     IS_FULL_DIRTY   { get; private set; }
        /// <summary> 마지막 렌더 이후 바뀐 칸 목록 (IS_FULL_DIRTY일 때는 비어 있다). </summary>
        public IReadOnlyList<int> DIRTY_CELLS => m_lstDirtyCell;

        public Vector2  WORLD_SIZE      => new Vector2(m_iWidth * m_fCellSize, m_iHeight * m_fCellSize);
        public Vector2  WORLD_CENTER    => m_vOrigin + WORLD_SIZE * 0.5f;

        #region Initialize
        /// <param name="vOrigin"> 셀 (0,0)의 좌하단 월드 좌표 </param>
        /// <param name="iBorderThick"> 시작 시 점령된 외곽 테두리 두께(셀). 0이면 테두리를 주지 않는다. </param>
        /// <param name="iStartRadius">
        /// 260920_맵 한가운데에 깔아 줄 '시작 섬'의 반경(셀). 중심에서 상하좌우로 이만큼씩 점령된 채 시작한다.
        /// 0이면 만들지 않는다.
        /// </param>
        /// <param name="arrPlayable">
        /// 260904_맵 모양 마스크. 길이 iWidth*iHeight, false인 칸은 BLOCK이 된다.
        /// null이면 직사각형 전체를 쓴다.
        /// </param>
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
                Debug.LogError($"[CTerritoryGrid] 모양 마스크 길이가 맞지 않는다 : "
                             + $"{arrPlayable.Length}, 기대 {iCellCount}");
                return false;
            }

            m_iWidth        = iWidth;
            m_iHeight       = iHeight;
            m_fCellSize     = fCellSize;
            m_vOrigin       = vOrigin;

            if (m_arrCell == null || m_arrCell.Length != iCellCount)
            {
                m_arrCell    = new CELL_STATE[iCellCount];
                m_arrRegion  = new int[iCellCount];
                m_arrBlocked = new bool[iCellCount];
            }

            for (int i = 0; i < iCellCount; ++i)
                m_arrBlocked[i] = arrPlayable != null && arrPlayable[i] == false;

            Reset(iBorderThick, iStartRadius);
            return true;
        }

        /// <summary>
        /// 전 셀을 EMPTY로 되돌리고 시작 안전 지대(외곽 테두리 · 가운데 시작 섬)만 OWNED로 채운다.
        /// 260904_모양 마스크로 잘라낸 BLOCK 칸은 그대로 두고 점령률 분모에서도 뺀다.
        /// 웨이브가 넘어갈 때마다 이 함수로 판을 다시 깐다.
        /// </summary>
        public void Reset(int iBorderThick, int iStartRadius = 0)
        {
            m_lstTrail.Clear();
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
            Set_FullDirty();
        }

        /// <summary>
        /// 260920_맵 한가운데를 정사각형으로 점령해 둔다 — 플레이어가 여기서 시작한다(2-3).
        /// 외벽에서 시작하면 벽을 따라 한 번에 크게 그어 판이 순식간에 끝났다.
        /// 가운데에서 시작하면 어느 방향으로 나가든 **돌아올 거리가 생긴다.**
        /// </summary>
        public Vector2Int START_CENTER => new Vector2Int(m_iWidth / 2, m_iHeight / 2);

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
        public int To_Index(int x, int y) => y * m_iWidth + x;
        public bool Is_InBounds(int x, int y) => x >= 0 && x < m_iWidth && y >= 0 && y < m_iHeight;

        public Vector3 Cell_ToWorld(int x, int y)
        {
            // 셀의 '중심' 월드 좌표
            return new Vector3(m_vOrigin.x + (x + 0.5f) * m_fCellSize,
                               m_vOrigin.y + (y + 0.5f) * m_fCellSize, 0f);
        }
        public Vector3 Cell_ToWorld(Vector2Int vCell) => Cell_ToWorld(vCell.x, vCell.y);

        public Vector2Int World_ToCell(Vector3 vWorld)
        {
            int x = Mathf.FloorToInt((vWorld.x - m_vOrigin.x) / m_fCellSize);
            int y = Mathf.FloorToInt((vWorld.y - m_vOrigin.y) / m_fCellSize);
            return new Vector2Int(Mathf.Clamp(x, 0, m_iWidth - 1), Mathf.Clamp(y, 0, m_iHeight - 1));
        }

        public static Vector2Int Dir_ToOffset(MOVE_DIR eDir)
        {
            int i = (int)eDir;
            if (i < 0 || i >= DIR_COUNT_ALL)
                return Vector2Int.zero;

            return new Vector2Int(ARR_DIR_X[i], ARR_DIR_Y[i]);
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

        /// <summary>
        /// 대각선을 가로 · 세로 두 방향으로 쪼갠다. 대각선 한 번은 이 두 칸을 잇따라 밟는 것이다 —
        /// 진짜로 비스듬히 한 칸 가면 트레일이 대각선으로만 이어져 **4방향 플러드필이 그 틈으로 새어 나간다**
        /// (점령이 엉뚱하게 터진다). 규칙(Step_To)을 건드리지 않으려면 이 방법뿐이다.
        /// </summary>
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

        #region 셀 조회
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
        public bool Is_Blocked(int x, int y) => Get_Cell(x, y) == CELL_STATE.BLOCK;
        public bool Is_Blocked(Vector2Int vCell) => Is_Blocked(vCell.x, vCell.y);

        // 260902_영토의 '선'만 따라 이동
        /// <summary>
        /// 점령지의 경계('선')인가 — 점령지이면서 이웃 8칸 중 하나라도 점령지가 아닌 칸.
        ///
        /// 260920_**맵 밖도 '점령지가 아닌 것'으로 센다.** 예전에는 Get_Cell이 맵 밖을 OWNED(벽)로
        /// 돌려주는 것을 그대로 써서, 내 땅이 맵 가장자리에 닿으면 그 줄이 통째로 '내부'가 되어
        /// **가장자리를 따라 걸을 수 없었다**(점령 직후 가장자리에 서면 움직일 곳이 없어 갇혔다).
        /// 옛 규칙은 외곽 테두리가 통째로 점령지이던 시절의 것인데, 그 테두리는 260920에 없앴다(2-3).
        /// </summary>
        public bool Is_Boundary(int x, int y)
        {
            if (Get_Cell(x, y) != CELL_STATE.OWNED)
                return false;

            for (int d = 0; d < DIR8_COUNT; ++d)
            {
                int nx = x + ARR_DIR8_X[d];
                int ny = y + ARR_DIR8_Y[d];

                if (Is_InBounds(nx, ny) == false)
                    return true;    // 맵 끝 = 더 먹을 것이 없는 쪽. 여기도 내 땅의 '선'이다

                if (m_arrCell[To_Index(nx, ny)] != CELL_STATE.OWNED)
                    return true;
            }

            return false;
        }
        public bool Is_Boundary(Vector2Int vCell) => Is_Boundary(vCell.x, vCell.y);

        /// <summary>
        /// 260920_월드 좌표에서 반경 안에 그 상태인 칸이 있는가. **몸 크기로 판정해야 하는 것**이 쓴다 —
        /// 몬스터가 선에 닿았는지를 중심 한 점으로만 보면, 화면에서는 몸이 선을 덮고 있는데도
        /// 중심이 그 칸에 들어가기 전까지 아무 일도 일어나지 않아 '왜 안 죽지'가 된다.
        /// </summary>
        /// <param name="fRadiusCells"> 반경(셀) </param>
        public bool Is_StateWithin(Vector2 vWorldPos, float fRadiusCells, CELL_STATE eState)
        {
            Vector2Int vCenter = World_ToCell(vWorldPos);
            int iRange = Mathf.Max(0, Mathf.CeilToInt(fRadiusCells));
            float fRadiusSq = (fRadiusCells * m_fCellSize) * (fRadiusCells * m_fCellSize);

            for (int dy = -iRange; dy <= iRange; ++dy)
            {
                for (int dx = -iRange; dx <= iRange; ++dx)
                {
                    int x = vCenter.x + dx;
                    int y = vCenter.y + dy;

                    if (Is_InBounds(x, y) == false || m_arrCell[To_Index(x, y)] != eState)
                        continue;

                    // 칸의 중심이 아니라 칸 '면'까지의 거리로 본다 — 닿았으면 닿은 것이다.
                    Vector2 vCellCenter = Cell_ToWorld(x, y);
                    float fDx = Mathf.Max(0f, Mathf.Abs(vWorldPos.x - vCellCenter.x) - m_fCellSize * 0.5f);
                    float fDy = Mathf.Max(0f, Mathf.Abs(vWorldPos.y - vCellCenter.y) - m_fCellSize * 0.5f);

                    if (fDx * fDx + fDy * fDy <= fRadiusSq)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 260920_가장 가까운 점령지 경계 칸을 찾는다. 점령 직후 플레이어가 '내부'에 남았을 때
        /// 선 위로 되돌려 놓는 데 쓴다(2-3) — 안전한 곳은 경계선 위뿐이라는 규칙을 지키기 위해서다.
        /// </summary>
        public bool Try_Find_NearestBoundary(Vector2Int vFrom, int iMaxRadius, out Vector2Int vFound)
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
                        // 이미 살펴본 안쪽은 건너뛴다 — 테두리만 본다.
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

        public void Clear_Dirty()
        {
            IS_DIRTY      = false;
            IS_FULL_DIRTY = false;
            m_lstDirtyCell.Clear();
        }

        private void Set_CellDirty(int iIndex)
        {
            IS_DIRTY = true;

            // 이미 전체 갱신이 예약돼 있으면 목록을 쌓아 봐야 버려진다.
            if (IS_FULL_DIRTY == false)
                m_lstDirtyCell.Add(iIndex);
        }

        private void Set_FullDirty()
        {
            IS_DIRTY      = true;
            IS_FULL_DIRTY = true;
            m_lstDirtyCell.Clear();
        }

        // 260902_몬스터가 점령지 안에 갇혔을 때 빠져나올 곳을 찾는 용도
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
        #endregion 셀 조회

        #region 트레일
        /// <summary> 미점령 셀을 밟았을 때 선분을 남긴다. </summary>
        public void Add_Trail(Vector2Int vCell)
        {
            if (Is_InBounds(vCell.x, vCell.y) == false)
                return;

            int iIndex = To_Index(vCell.x, vCell.y);
            if (m_arrCell[iIndex] != CELL_STATE.EMPTY)
                return;

            m_arrCell[iIndex] = CELL_STATE.TRAIL;
            m_lstTrail.Add(iIndex);
            Set_CellDirty(iIndex);
        }

        /// <summary> 사망 등으로 점령에 실패했을 때 그리던 선분을 되돌린다. </summary>
        public void Clear_Trail()
        {
            for (int i = 0; i < m_lstTrail.Count; ++i)
            {
                m_arrCell[m_lstTrail[i]] = CELL_STATE.EMPTY;
                Set_CellDirty(m_lstTrail[i]);
            }

            m_lstTrail.Clear();
        }

        /// <summary> 트레일의 마지막에서 두 번째 셀 — 180도 반전 입력을 막는 데 쓴다. </summary>
        public bool Try_Get_PrevTrailCell(out Vector2Int vCell)
        {
            vCell = Vector2Int.zero;
            if (m_lstTrail.Count < 2)
                return false;

            int iIndex = m_lstTrail[m_lstTrail.Count - 2];
            vCell = new Vector2Int(iIndex % m_iWidth, iIndex / m_iWidth);
            return true;
        }
        #endregion 트레일

        #region 상태 전이
        /// <summary>
        /// 플레이어가 한 셀에 '도착'했을 때의 상태 전이를 처리한다.
        /// 땅따먹기 규칙의 단일 진입점 — 플레이어/테스트 모두 이 함수만 호출한다.
        /// </summary>
        /// <param name="lstEnemyCell"> 점령 판정에 쓸 몬스터 셀 목록 (없으면 null) </param>
        /// <param name="iCapturedCount"> CAPTURE일 때 새로 점령한 셀 개수 </param>
        public STEP_RESULT Step_To(Vector2Int vCell, out int iCapturedCount)
        {
            iCapturedCount = 0;

            switch (Get_Cell(vCell))
            {
                // 260904_맵 밖으로 잘라낸 칸. 이동 판정(CMoveHandler.Can_Move)이 이미 막으므로
                // 여기까지 오지 않지만, 혹시 오더라도 점령 판정으로 새지 않게 명시해 둔다.
                case CELL_STATE.BLOCK:
                    return STEP_RESULT.SAFE;

                // 자기가 그리던 선을 밟았다
                case CELL_STATE.TRAIL:
                    return STEP_RESULT.DEAD;

                // 미점령 지대 — 선분을 남기며 전진 (이 상태에서 몬스터/탄에 피격된다)
                case CELL_STATE.EMPTY:
                    Add_Trail(vCell);
                    return STEP_RESULT.DRAW;

                // 안전 지대 — 선을 그리던 중이었다면 도형이 닫힌 것이므로 점령한다
                default:
                    if (IS_DRAWING == false)
                        return STEP_RESULT.SAFE;

                    iCapturedCount = Capture();
                    return STEP_RESULT.CAPTURE;
            }
        }
        #endregion 상태 전이

        #region 점령 (플러드필)
        /// <summary>
        /// 트레일이 안전 지대에 닿아 도형이 닫혔을 때 호출한다.
        /// 트레일을 점령지로 승격시킨 뒤, **가장 넓은 영역 하나만 남기고 나머지를 전부 점령한다.**
        ///
        /// 260920_예전에는 몬스터가 서 있는 영역을 점령에서 뺐다. 그런데 몬스터는 계속 돌아다니므로
        /// **애써 가둔 도형이 아무 설명 없이 점령되지 않는 일**이 잦았다(가둔 것이 오히려 손해였다).
        /// 이제 가두면 무조건 먹고, **그 안에 있던 몬스터는 죽는다** — 죽이는 것은 몬스터를 들고 있는
        /// CStage_Manager가 한다(여기는 칸만 안다). 점령이 곧 공격 수단이 됐다.
        /// </summary>
        /// <returns> 이번에 새로 점령한 셀 개수 </returns>
        public int Capture()
        {
            if (m_lstTrail.Count == 0)
                return 0;

            // 1. 트레일 → 점령지
            for (int i = 0; i < m_lstTrail.Count; ++i)
            {
                m_arrCell[m_lstTrail[i]] = CELL_STATE.OWNED;
                ++m_iOwnedCount;
            }
            int iCapturedCount = m_lstTrail.Count;
            m_lstTrail.Clear();

            // 2. 남은 EMPTY 영역들을 라벨링
            int iRegionCount = Label_EmptyRegions();
            if (iRegionCount == 0)
            {
                Set_FullDirty();
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

            Set_FullDirty();
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
