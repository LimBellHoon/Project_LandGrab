using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 스테이지 진행/클리어 판정
    // 260904_MapInfo.csv / EnemyInfo.csv 기반 + 웨이브 진행
    /// <summary>
    /// 그리드 · 렌더러 · 플레이어 · 몬스터를 소유하고 웨이브 규칙을 판정한다.
    /// 규칙 값은 전부 CSV에서 온다 — 이 클래스에 숫자를 다시 적지 말 것.
    ///
    /// 웨이브: N웨이브를 fClearRatio만큼 점령하면 그리드를 다시 깔고 이미지 스택을 한 장 벗긴다.
    /// 마지막 웨이브까지 넘기면 CLEAR.
    /// </summary>
    public class CStage_Manager
    {
        private const int SPAWN_SEARCH_RADIUS = 24;     // 스폰 자리가 막혔을 때 대신 찾아볼 반경(셀)

        private readonly CTerritoryGrid m_cGrid         = new CTerritoryGrid();
        private readonly CGridRenderer  m_cGridRenderer = new CGridRenderer();

        private readonly List<CEnemy>       m_lstEnemy      = new List<CEnemy>();
        private readonly List<Vector2Int>   m_lstEnemyCell  = new List<Vector2Int>();  // 점령 판정용 재사용 버퍼

        private CMapInfo            m_cMapInfo;
        private CCSVData_EnemyInfo  m_cEnemyTable;
        private CPlayer             m_cPlayer;
        private STAGE_STATE         m_eState = STAGE_STATE.READY;
        private int                 m_iWave;
        private float               m_fRemainTime;

        public CTerritoryGrid   GRID            => m_cGrid;
        public CPlayer          PLAYER          => m_cPlayer;
        public STAGE_STATE      STATE           => m_eState;
        public float            REMAIN_TIME     => m_fRemainTime;
        public float            OWNED_RATIO     => m_cGrid.OWNED_RATIO;
        public int              LIFE            => m_cPlayer != null ? m_cPlayer.LIFE : 0;
        public int              ENEMY_COUNT     => m_lstEnemy.Count;
        public int              WAVE            => m_iWave;
        public int              WAVE_COUNT      => m_cMapInfo != null ? m_cMapInfo.iWaveCount : 0;
        public string           MAP_NAME        => m_cMapInfo != null ? m_cMapInfo.strMapName : string.Empty;

        public float CLEAR_RATIO
        {
            get
            {
                CWaveInfo cWave = m_cMapInfo != null ? m_cMapInfo.Get_Wave(m_iWave) : null;
                return cWave != null ? cWave.fClearRatio : 0f;
            }
        }

        #region 초기화
        /// <param name="srCover"> 가림막을 그릴 SpriteRenderer (그리드 마스크) </param>
        /// <param name="srReveal"> 점령하면 드러날 보상 이미지를 깔 SpriteRenderer </param>
        public bool Initialize(CMapInfo cMapInfo, CCSVData_EnemyInfo cEnemyTable,
                               SpriteRenderer srCover, SpriteRenderer srReveal)
        {
            if (cMapInfo == null)
            {
                Debug.LogError("[CStage_Manager] CMapInfo가 null 입니다. MapInfo.csv를 확인하세요.");
                return false;
            }

            if (cMapInfo.IS_VALID == false)
                Debug.LogWarning($"[CStage_Manager] 맵 {cMapInfo.iMapID}의 표가 어긋나 있습니다. "
                               + "웨이브 수와 이미지 스택 장수를 확인하세요.");

            m_cMapInfo    = cMapInfo;
            m_cEnemyTable = cEnemyTable;

            // 그리드를 월드 원점 기준으로 가운데 정렬한다.
            Vector2 vWorldSize = new Vector2(cMapInfo.iGridWidth * cMapInfo.fCellSize,
                                             cMapInfo.iGridHeight * cMapInfo.fCellSize);
            Vector2 vOrigin = -vWorldSize * 0.5f;

            if (m_cGrid.Initialize(cMapInfo.iGridWidth, cMapInfo.iGridHeight, cMapInfo.fCellSize,
                                   vOrigin, cMapInfo.iBorderThick, Load_ShapeMask(cMapInfo)) == false)
                return false;

            return m_cGridRenderer.Initialize(m_cGrid, srCover, srReveal);
        }

        public void Release()
        {
            Set_ActorTimeScale(1f);

            Collect_Enemies();
            m_cGridRenderer.Release();
            m_cPlayer = null;
        }

        // 260904_맵 모양 마스크 — 밝은 픽셀이 플레이 가능한 칸이다.
        /// <summary> strShapeMask 텍스처를 셀 격자로 샘플링해 '플레이 가능' 배열로 만든다. 없으면 null. </summary>
        private static bool[] Load_ShapeMask(CMapInfo cMapInfo)
        {
            if (string.IsNullOrEmpty(cMapInfo.strShapeMask) == true)
                return null;

            Texture2D texShape = Get_Texture(cMapInfo.strShapeMask);
            if (texShape == null)
                return null;

            if (texShape.isReadable == false)
            {
                Debug.LogError($"[CStage_Manager] 모양 마스크 '{cMapInfo.strShapeMask}'는 Read/Write가 꺼져 있습니다. "
                             + "텍스처 임포트 설정에서 Read/Write Enabled를 켜세요.");
                return null;
            }

            Color32[] arrSrc = texShape.GetPixels32();
            bool[] arrPlayable = new bool[cMapInfo.iGridWidth * cMapInfo.iGridHeight];

            for (int y = 0; y < cMapInfo.iGridHeight; ++y)
            {
                int sy = y * texShape.height / cMapInfo.iGridHeight;

                for (int x = 0; x < cMapInfo.iGridWidth; ++x)
                {
                    int sx = x * texShape.width / cMapInfo.iGridWidth;
                    Color32 cColor = arrSrc[sy * texShape.width + sx];

                    // 밝기 절반을 기준으로 자른다. 알파가 0인 칸도 맵 밖으로 본다.
                    int iLuma = (cColor.r + cColor.g + cColor.b) / 3;
                    arrPlayable[y * cMapInfo.iGridWidth + x] = cColor.a > 127 && iLuma > 127;
                }
            }

            return arrPlayable;
        }

        private static Texture2D Get_Texture(string strName)
        {
            // 이미지 스택의 칸이 비어 있을 수 있다 — 그건 잘못이 아니라 '가림막 없음'이다.
            if (string.IsNullOrEmpty(strName) == true)
                return null;

            Texture texture = CGameInstance.Instance.Get_Texture(strName);
            if (texture == null)
            {
                Debug.LogError($"[CStage_Manager] 텍스처 '{strName}'을 찾을 수 없습니다. "
                             + $"Addressable 라벨 '{CAddressableLabel.TEXTURE}'에 등록됐는지 확인하세요.");
                return null;
            }

            return texture as Texture2D;
        }
        #endregion 초기화

        #region 스테이지 / 웨이브 진행
        public bool Start_Stage()
        {
            if (Spawn_Player() == false)
                return false;

            // 이전 스테이지가 CLEAR/FAIL로 끝나며 0으로 내려둔 타임스케일을 되돌린다.
            Set_ActorTimeScale(1f);

            m_eState = STAGE_STATE.PLAYING;
            Enter_Wave(1);
            return true;
        }

        public void Tick(float fDeltaTime)
        {
            m_cGridRenderer.Tick();

            if (m_eState != STAGE_STATE.PLAYING)
                return;

            Tick_Enemy();

            m_fRemainTime -= fDeltaTime;
            if (m_fRemainTime <= 0f)
            {
                m_fRemainTime = 0f;
                Set_State(STAGE_STATE.FAIL);
            }
        }

        // 260904_웨이브 진입 — 판을 새로 깔고 이미지 스택을 한 장 벗긴다.
        /// <param name="iWave"> 1부터 시작 </param>
        private void Enter_Wave(int iWave)
        {
            CWaveInfo cWave = m_cMapInfo.Get_Wave(iWave);
            if (cWave == null)
            {
                Debug.LogError($"[CStage_Manager] {iWave}웨이브 정보가 없습니다.");
                Set_State(STAGE_STATE.CLEAR);
                return;
            }

            m_iWave       = iWave;
            m_fRemainTime = cWave.fTimeLimit;

            m_cGrid.Reset(m_cMapInfo.iBorderThick);
            m_cGridRenderer.Set_WaveTexture(Get_Texture(m_cMapInfo.Get_CoverTex(iWave)),
                                            Get_Texture(m_cMapInfo.Get_RevealTex(iWave)));

            Respawn_Player();
            Spawn_Enemies(cWave);

            Debug.Log($"[CStage_Manager] {m_cMapInfo.strMapName} — {iWave}/{m_cMapInfo.iWaveCount} 웨이브 시작 "
                    + $"(목표 {cWave.fClearRatio:P0}, {cWave.fTimeLimit:F0}초, 몬스터 {cWave.TOTAL_ENEMY}마리)");
        }

        private void Next_Wave()
        {
            if (m_iWave >= m_cMapInfo.iWaveCount)
            {
                Set_State(STAGE_STATE.CLEAR);
                return;
            }

            Enter_Wave(m_iWave + 1);
        }
        #endregion 스테이지 / 웨이브 진행

        #region 플레이어
        // 260903_Engine은 프리팹이 없으면 Instantiate에서 예외를 던진다(반환값 null이 아님).
        // 호출 전에 미리 확인해야 스테이지 전체가 초기화 도중 죽는 것을 막을 수 있다.
        private static bool Has_Prefab(string strPrefabName)
        {
            return CGameInstance.Instance.Has_Prefab(strPrefabName);
        }

        private bool Spawn_Player()
        {
            const string PREFAB_PLAYER = "Prefab_Player";

            if (Has_Prefab(PREFAB_PLAYER) == false)
            {
                Debug.LogError($"[CStage_Manager] '{PREFAB_PLAYER}'를 찾을 수 없습니다. "
                             + "Unity 메뉴 Tools/LandGrab/Setup Assets 를 실행한 뒤 다시 시도하세요.");
                return false;
            }

            CPlayerDesc cPlayerDesc = new CPlayerDesc
            {
                eObjectType     = OBJECT_TYPE.PLAYER,
                strPrefabName   = PREFAB_PLAYER,
                cGrid           = m_cGrid,
                vStartCell      = Find_StartCell(),
                fMoveSpeed      = PLAYER_MOVE_SPEED,
                iLife           = m_cMapInfo.iLife,
            };

            GameObject goPlayer = CGameInstance.Instance.Reuse_Object(cPlayerDesc);
            if (goPlayer == null)
            {
                Debug.LogError("[CStage_Manager] Player 생성 실패 — Addressable 라벨/프리팹 이름을 확인하세요.");
                return false;
            }

            m_cPlayer = goPlayer.GetComponent<CPlayer>();
            if (m_cPlayer == null)
            {
                Debug.LogError("[CStage_Manager] 프리팹에 CPlayer 컴포넌트가 없습니다.");
                return false;
            }

            m_cPlayer.OnCapture     += On_PlayerCapture;
            m_cPlayer.OnDead        += On_PlayerDead;
            m_cPlayer.GetEnemyCells  = Get_EnemyCells;  // 몬스터가 있는 영역은 점령되지 않는다

            return true;
        }

        // 플레이어 이동 속도는 아직 맵별로 나눌 이유가 없어 상수로 둔다.
        // 맵마다 달리 줄 일이 생기면 MapInfo.csv에 열을 하나 늘릴 것.
        private const float PLAYER_MOVE_SPEED = 9f;

        private void Respawn_Player()
        {
            if (m_cPlayer != null)
                m_cPlayer.Respawn(Find_StartCell());
        }

        /// <summary>
        /// 시작 위치는 아래쪽 테두리 가운데. 모양 마스크로 그 자리가 잘려 나갔을 수 있으므로
        /// 가장 가까운 안전 지대를 대신 찾는다.
        /// </summary>
        private Vector2Int Find_StartCell()
        {
            Vector2Int vDesired = new Vector2Int(m_cMapInfo.iGridWidth / 2, m_cMapInfo.iBorderThick - 1);

            if (m_cGrid.Try_Find_NearestCell(vDesired, CELL_STATE.OWNED, SPAWN_SEARCH_RADIUS,
                                             out Vector2Int vStart) == true)
                return vStart;

            Debug.LogError("[CStage_Manager] 플레이어가 설 안전 지대를 찾지 못했습니다. 모양 마스크를 확인하세요.");
            return vDesired;
        }
        #endregion 플레이어

        #region 몬스터
        // 260904_웨이브가 정한 조합대로 소환한다 (MapInfo.csv의 strWaveEnemy).
        private void Spawn_Enemies(CWaveInfo cWave)
        {
            Collect_Enemies();

            if (m_cEnemyTable == null || cWave.lstEnemy.Count == 0)
                return;

            int iSpawned = 0;
            int iTotal   = cWave.TOTAL_ENEMY;

            for (int i = 0; i < cWave.lstEnemy.Count; ++i)
            {
                CWaveEnemy cEntry = cWave.lstEnemy[i];
                CEnemyInfo cInfo  = m_cEnemyTable.Get_Info(cEntry.iEnemyID);
                if (cInfo == null)
                    continue;

                if (Has_Prefab(cInfo.strPrefabName) == false)
                {
                    // 260903_몬스터가 없어도 스테이지는 굴러가야 한다 — 경고만 남기고 넘어간다.
                    Debug.LogWarning($"[CStage_Manager] '{cInfo.strPrefabName}'가 없어 "
                                   + $"{cInfo.strName}을(를) 건너뜁니다. Tools/LandGrab/Setup Assets 를 실행하세요.");
                    continue;
                }

                for (int n = 0; n < cEntry.iCount; ++n)
                {
                    Spawn_Enemy(cInfo, iSpawned, iTotal);
                    ++iSpawned;
                }
            }
        }

        private void Spawn_Enemy(CEnemyInfo cInfo, int iIndex, int iTotal)
        {
            CEnemyDesc cEnemyDesc = new CEnemyDesc
            {
                eObjectType     = OBJECT_TYPE.ENEMY,
                strPrefabName   = cInfo.strPrefabName,
                cGrid           = m_cGrid,
                vStartCell      = Find_EnemySpawnCell(iIndex, iTotal),
                vStartDir       = Get_EnemySpawnDir(iIndex),
                iEnemyID        = cInfo.iEnemyID,
                eGimmick        = cInfo.eGimmick,
                fSpeed          = cInfo.fSpeed,
                fChaseSpeed     = cInfo.fChaseSpeed,
                fTurnRate       = cInfo.fTurnRate,
                fHitRange       = cInfo.fHitRange,
                fGimmickCool    = cInfo.fGimmickCool,
                fGimmickValue   = cInfo.fGimmickValue,
                fGimmickRange   = cInfo.fGimmickRange,
            };

            GameObject goEnemy = CGameInstance.Instance.Reuse_Object(cEnemyDesc);
            if (goEnemy == null)
            {
                Debug.LogError("[CStage_Manager] Enemy 생성 실패 — Addressable 라벨/프리팹 이름을 확인하세요.");
                return;
            }

            CEnemy cEnemy = goEnemy.GetComponent<CEnemy>();
            if (cEnemy == null)
            {
                Debug.LogError("[CStage_Manager] 프리팹에 CEnemy 컴포넌트가 없습니다.");
                return;
            }

            m_lstEnemy.Add(cEnemy);
        }

        /// <summary> 웨이브가 바뀔 때 이전 몬스터를 풀에 돌려준다. </summary>
        private void Collect_Enemies()
        {
            for (int i = 0; i < m_lstEnemy.Count; ++i)
            {
                if (m_lstEnemy[i] != null)
                    CGameInstance.Instance.Collect_Object(m_lstEnemy[i]);
            }

            m_lstEnemy.Clear();
        }

        // 플레이어 시작 지점(아래쪽)에서 멀리 떨어진 가운데~위쪽 대역에 고르게 배치한다.
        // 모양 마스크로 잘린 자리에 걸릴 수 있으므로 가장 가까운 미점령 칸으로 보정한다.
        private Vector2Int Find_EnemySpawnCell(int iIndex, int iTotal)
        {
            float fT = (iIndex + 1f) / (iTotal + 1f);

            int x = Mathf.RoundToInt(Mathf.Lerp(m_cMapInfo.iGridWidth * 0.2f, m_cMapInfo.iGridWidth * 0.8f, fT));
            int y = Mathf.RoundToInt(m_cMapInfo.iGridHeight * (0.4f + 0.15f * (iIndex % 3)));
            Vector2Int vDesired = new Vector2Int(x, y);

            return m_cGrid.Try_Find_NearestCell(vDesired, CELL_STATE.EMPTY, SPAWN_SEARCH_RADIUS,
                                                out Vector2Int vSpawn) ? vSpawn : vDesired;
        }

        private static Vector2 Get_EnemySpawnDir(int iIndex)
        {
            // 대각선으로 출발시켜야 벽에 튕기며 맵 전체를 고르게 돈다.
            float fX = (iIndex % 2 == 0) ? 1f : -1f;
            float fY = ((iIndex / 2) % 2 == 0) ? 1f : -1f;
            return new Vector2(fX, fY).normalized;
        }

        private void Tick_Enemy()
        {
            if (m_cPlayer == null)
                return;

            // 플레이어가 안전 지대(선) 위에 있으면 몬스터는 쫓지 않고 배회한다.
            bool bExposed = m_cGrid.Get_Cell(m_cPlayer.CUR_CELL) != CELL_STATE.OWNED;
            Vector2 vPlayerPos = m_cPlayer.transform.position;
            bool bHit = false;

            // 260904_피격이 확정돼도 루프를 끊지 않는다 — break로 빠지면 뒤쪽 몬스터의 추적 상태가 갱신되지
            // 않아, 플레이어가 안전 지대로 돌아간 뒤에도 한 프레임 더 추적 속도로 달려든다.
            for (int i = 0; i < m_lstEnemy.Count; ++i)
            {
                CEnemy cEnemy = m_lstEnemy[i];
                cEnemy.Set_ChaseState(bExposed, vPlayerPos);

                if (bHit == true)
                    continue;

                // 그리는 중인 선분에 몬스터가 닿아도 사망한다.
                if (m_cGrid.Get_Cell(cEnemy.CUR_CELL) == CELL_STATE.TRAIL)
                {
                    bHit = true;
                    continue;
                }

                // 플레이어와의 직접 충돌은 땅을 먹으러 나와 있을 때만 판정한다.
                if (bExposed == true
                    && Vector2.Distance(cEnemy.POS, vPlayerPos) <= cEnemy.HIT_RANGE * m_cGrid.CELL_SIZE)
                {
                    bHit = true;
                }
            }

            if (bHit == true)
                m_cPlayer.Damage();
        }

        /// <summary> 점령 판정에 넘길 몬스터 셀 목록. 매 호출마다 버퍼를 재사용해 GC를 만들지 않는다. </summary>
        private IReadOnlyList<Vector2Int> Get_EnemyCells()
        {
            m_lstEnemyCell.Clear();

            for (int i = 0; i < m_lstEnemy.Count; ++i)
                m_lstEnemyCell.Add(m_lstEnemy[i].CUR_CELL);

            return m_lstEnemyCell;
        }
        #endregion 몬스터

        #region 콜백
        private void On_PlayerCapture(int iCapturedCount)
        {
            if (m_eState != STAGE_STATE.PLAYING)
                return;

            CWaveInfo cWave = m_cMapInfo.Get_Wave(m_iWave);
            if (cWave != null && m_cGrid.OWNED_RATIO >= cWave.fClearRatio)
                Next_Wave();
        }

        private void On_PlayerDead()
        {
            Set_State(STAGE_STATE.FAIL);
        }

        // 260904_스테이지가 끝나면 액터를 세운다.
        // Tick()은 PLAYING이 아니면 빠져나가지만 플레이어/몬스터의 Tick은 Engine 레이어가 직접 돌린다.
        // 그대로 두면 CLEAR/FAIL 이후에도 계속 움직이며 땅을 먹고 목숨이 더 깎인다.
        // Engine이 이미 레이어별 타임스케일을 갖고 있으므로 별도의 정지 플래그를 만들지 않는다.
        private static void Set_ActorTimeScale(float fTimeScale)
        {
            CGameInstance.Instance.Set_LayerTimeScale(OBJECT_TYPE.PLAYER, fTimeScale);
            CGameInstance.Instance.Set_LayerTimeScale(OBJECT_TYPE.ENEMY, fTimeScale);
        }

        private void Set_State(STAGE_STATE eState)
        {
            if (m_eState == eState)
                return;

            m_eState = eState;

            if (eState == STAGE_STATE.CLEAR || eState == STAGE_STATE.FAIL)
                Set_ActorTimeScale(0f);

            Debug.Log($"[CStage_Manager] STAGE {eState} — {m_iWave}웨이브, 점령률 {m_cGrid.OWNED_RATIO:P1}");
        }
        #endregion 콜백
    }
}
