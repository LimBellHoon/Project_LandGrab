using System;
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
        private const int DIR_COUNT = 4;
        // MOVE_DIR(UP, DOWN, LEFT, RIGHT) 순서와 인덱스를 맞춘다.
        private static readonly int[] ARR_DIR_X = { 0, 0, -1, 1 };
        private static readonly int[] ARR_DIR_Y = { 1, -1, 0, 0 };

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
        private int             m_iTotalCount;

        public int      WIDTH           => m_iWidth;
        public int      HEIGHT          => m_iHeight;
        public float    CELL_SIZE       => m_fCellSize;
        public Vector2  ORIGIN          => m_vOrigin;
        public bool     IS_DRAWING      => m_lstTrail.Count > 0;
        public int      TRAIL_COUNT     => m_lstTrail.Count;
        public float    OWNED_RATIO     => m_iTotalCount > 0 ? (float)m_iOwnedCount / m_iTotalCount : 0f;
        /// <summary> 렌더러가 다시 그려야 하는지 여부. 렌더 후 Clear_Dirty()로 내린다. </summary>
        public bool     IS_DIRTY        { get; private set; }

        public Vector2  WORLD_SIZE      => new Vector2(m_iWidth * m_fCellSize, m_iHeight * m_fCellSize);
        public Vector2  WORLD_CENTER    => m_vOrigin + WORLD_SIZE * 0.5f;

        #region Initialize
        /// <param name="vOrigin"> 셀 (0,0)의 좌하단 월드 좌표 </param>
        /// <param name="iBorderThick"> 시작 시 점령된 외곽 테두리 두께(셀). 플레이어의 최초 안전 지대. </param>
        public bool Initialize(int iWidth, int iHeight, float fCellSize, Vector2 vOrigin, int iBorderThick)
        {
            if (iWidth <= 0 || iHeight <= 0 || fCellSize <= 0f)
            {
                Debug.LogError($"[CTerritoryGrid] 잘못된 그리드 크기 : {iWidth}x{iHeight}, cell {fCellSize}");
                return false;
            }

            m_iWidth        = iWidth;
            m_iHeight       = iHeight;
            m_fCellSize     = fCellSize;
            m_vOrigin       = vOrigin;
            m_iTotalCount   = iWidth * iHeight;

            if (m_arrCell == null || m_arrCell.Length != m_iTotalCount)
            {
                m_arrCell   = new CELL_STATE[m_iTotalCount];
                m_arrRegion = new int[m_iTotalCount];
            }

            Reset(iBorderThick);
            return true;
        }

        /// <summary> 전 셀을 EMPTY로 되돌리고 외곽 테두리만 OWNED로 채운다. </summary>
        public void Reset(int iBorderThick)
        {
            Array.Clear(m_arrCell, 0, m_arrCell.Length);
            m_lstTrail.Clear();
            m_iOwnedCount = 0;

            iBorderThick = Mathf.Clamp(iBorderThick, 1, Mathf.Min(m_iWidth, m_iHeight) / 2);

            for (int y = 0; y < m_iHeight; ++y)
            {
                for (int x = 0; x < m_iWidth; ++x)
                {
                    bool bBorder = x < iBorderThick || y < iBorderThick
                                || x >= m_iWidth - iBorderThick || y >= m_iHeight - iBorderThick;

                    if (bBorder == true)
                    {
                        m_arrCell[To_Index(x, y)] = CELL_STATE.OWNED;
                        ++m_iOwnedCount;
                    }
                }
            }

            IS_DIRTY = true;
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
            if (i < 0 || i >= DIR_COUNT)
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
        #endregion 좌표 변환

        #region 셀 조회
        public CELL_STATE Get_Cell(int x, int y)
        {
            if (Is_InBounds(x, y) == false)
                return CELL_STATE.OWNED;    // 맵 밖은 벽 취급 — 진입 판정에서 걸러진다.

            return m_arrCell[To_Index(x, y)];
        }
        public CELL_STATE Get_Cell(Vector2Int vCell) => Get_Cell(vCell.x, vCell.y);

        public bool Is_Safe(Vector2Int vCell) => Get_Cell(vCell) == CELL_STATE.OWNED;

        /// <summary> 이 셀에 발을 들이면 즉사하는가 (자기 트레일 밟기). </summary>
        public bool Is_Deadly(Vector2Int vCell) => Get_Cell(vCell) == CELL_STATE.TRAIL;

        // 260902_영토의 '선'만 따라 이동
        /// <summary>
        /// 점령지의 경계('선')인가 — 점령지이면서 이웃 8칸 중 하나라도 점령지가 아닌 칸.
        /// 맵 밖은 Get_Cell이 OWNED(벽)로 돌려주므로 바깥쪽 테두리는 경계에서 자동 제외된다.
        /// </summary>
        public bool Is_Boundary(int x, int y)
        {
            if (Get_Cell(x, y) != CELL_STATE.OWNED)
                return false;

            for (int d = 0; d < DIR8_COUNT; ++d)
            {
                if (Get_Cell(x + ARR_DIR8_X[d], y + ARR_DIR8_Y[d]) != CELL_STATE.OWNED)
                    return true;
            }

            return false;
        }
        public bool Is_Boundary(Vector2Int vCell) => Is_Boundary(vCell.x, vCell.y);

        public void Clear_Dirty() => IS_DIRTY = false;

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
            IS_DIRTY = true;
        }

        /// <summary> 사망 등으로 점령에 실패했을 때 그리던 선분을 되돌린다. </summary>
        public void Clear_Trail()
        {
            for (int i = 0; i < m_lstTrail.Count; ++i)
                m_arrCell[m_lstTrail[i]] = CELL_STATE.EMPTY;

            m_lstTrail.Clear();
            IS_DIRTY = true;
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
        public STEP_RESULT Step_To(Vector2Int vCell, IReadOnlyList<Vector2Int> lstEnemyCell, out int iCapturedCount)
        {
            iCapturedCount = 0;

            switch (Get_Cell(vCell))
            {
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

                    iCapturedCount = Capture(lstEnemyCell);
                    return STEP_RESULT.CAPTURE;
            }
        }
        #endregion 상태 전이

        #region 점령 (플러드필)
        /// <summary>
        /// 트레일이 안전 지대에 닿아 도형이 닫혔을 때 호출한다.
        /// 트레일을 점령지로 승격시킨 뒤, 몬스터가 없는 미점령 영역을 전부 점령한다.
        /// </summary>
        /// <param name="lstEnemyCell"> 살아있는 몬스터가 서 있는 셀. null이면 가장 큰 영역만 남긴다. </param>
        /// <returns> 이번에 새로 점령한 셀 개수 </returns>
        public int Capture(IReadOnlyList<Vector2Int> lstEnemyCell)
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
                IS_DIRTY = true;
                return iCapturedCount;
            }

            // 3. 몬스터가 서 있는 영역에 '안전' 표시 — 이 영역은 점령되지 않는다.
            bool bAnySafe = Mark_EnemyRegions(lstEnemyCell);

            // 몬스터가 하나도 없다면 가장 넓은 영역만 남기고 나머지를 먹는다.
            // (몬스터 없는 프로토타입/디버그 상황에서도 규칙이 성립하도록)
            if (bAnySafe == false)
                m_lstRegionSafe[Find_LargestRegion()] = true;

            // 4. 안전 표시가 없는 영역 = 플레이어가 가둔 영역 → 전부 점령
            for (int i = 0; i < m_arrCell.Length; ++i)
            {
                int iRegion = m_arrRegion[i];
                if (iRegion < 0 || m_lstRegionSafe[iRegion] == true)
                    continue;

                m_arrCell[i] = CELL_STATE.OWNED;
                ++m_iOwnedCount;
                ++iCapturedCount;
            }

            IS_DIRTY = true;
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

        private bool Mark_EnemyRegions(IReadOnlyList<Vector2Int> lstEnemyCell)
        {
            if (lstEnemyCell == null)
                return false;

            bool bAnySafe = false;

            for (int i = 0; i < lstEnemyCell.Count; ++i)
            {
                Vector2Int vCell = lstEnemyCell[i];
                if (Is_InBounds(vCell.x, vCell.y) == false)
                    continue;

                int iRegion = m_arrRegion[To_Index(vCell.x, vCell.y)];
                if (iRegion < 0)
                    continue;   // 몬스터가 점령지 위에 있는 예외 상황 — 무시

                m_lstRegionSafe[iRegion] = true;
                bAnySafe = true;
            }

            return bAnySafe;
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
