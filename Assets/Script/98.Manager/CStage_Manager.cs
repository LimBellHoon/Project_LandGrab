using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 스테이지 진행/클리어 판정
    /// <summary>
    /// 그리드 · 그리드 렌더러 · 플레이어를 소유하고 스테이지 규칙(제한 시간, 점령률)을 판정한다.
    /// </summary>
    public class CStage_Manager
    {
        private const string PREFAB_PLAYER = "Prefab_Player";
        private const string PREFAB_ENEMY  = "Prefab_Enemy";

        private readonly CTerritoryGrid m_cGrid         = new CTerritoryGrid();
        private readonly CGridRenderer  m_cGridRenderer = new CGridRenderer();

        // 260902_몬스터
        private readonly List<CEnemy>       m_lstEnemy      = new List<CEnemy>();
        private readonly List<Vector2Int>   m_lstEnemyCell  = new List<Vector2Int>();  // 점령 판정용 재사용 버퍼

        private CStageDesc      m_cStageDesc;
        private CPlayer         m_cPlayer;
        private STAGE_STATE     m_eState = STAGE_STATE.READY;
        private float           m_fRemainTime;

        public CTerritoryGrid   GRID            => m_cGrid;
        public CPlayer          PLAYER          => m_cPlayer;
        public STAGE_STATE      STATE           => m_eState;
        public float            REMAIN_TIME     => m_fRemainTime;
        public float            OWNED_RATIO     => m_cGrid.OWNED_RATIO;
        public float            CLEAR_RATIO     => m_cStageDesc != null ? m_cStageDesc.fClearRatio : 0f;
        public int              LIFE            => m_cPlayer != null ? m_cPlayer.LIFE : 0;
        public int              ENEMY_COUNT     => m_lstEnemy.Count;

        #region 초기화
        public bool Initialize(CStageDesc cStageDesc, SpriteRenderer srBackground, SpriteRenderer srOverlay)
        {
            if (cStageDesc == null)
            {
                Debug.LogError("[CStage_Manager] CStageDesc가 null 입니다.");
                return false;
            }

            m_cStageDesc = cStageDesc;

            // 그리드를 월드 원점 기준으로 가운데 정렬한다.
            Vector2 vWorldSize = new Vector2(cStageDesc.iGridWidth * cStageDesc.fCellSize,
                                             cStageDesc.iGridHeight * cStageDesc.fCellSize);
            Vector2 vOrigin = -vWorldSize * 0.5f;

            if (m_cGrid.Initialize(cStageDesc.iGridWidth, cStageDesc.iGridHeight,
                                   cStageDesc.fCellSize, vOrigin, cStageDesc.iBorderThick) == false)
                return false;

            if (m_cGridRenderer.Initialize(m_cGrid, srOverlay) == false)
                return false;

            Fit_Background(srBackground, vWorldSize);
            return true;
        }

        public void Release()
        {
            Set_ActorTimeScale(1f);

            m_cGridRenderer.Release();
            m_lstEnemy.Clear();
            m_cPlayer = null;
        }

        /// <summary> 배경(보상 이미지)을 그리드와 정확히 같은 크기로 맞춘다. </summary>
        private void Fit_Background(SpriteRenderer srBackground, Vector2 vWorldSize)
        {
            if (srBackground == null || srBackground.sprite == null)
                return;

            Vector2 vSpriteSize = srBackground.sprite.bounds.size;
            if (vSpriteSize.x <= 0f || vSpriteSize.y <= 0f)
                return;

            srBackground.transform.position   = new Vector3(m_cGrid.WORLD_CENTER.x, m_cGrid.WORLD_CENTER.y, 0f);
            srBackground.transform.localScale = new Vector3(vWorldSize.x / vSpriteSize.x,
                                                            vWorldSize.y / vSpriteSize.y, 1f);
        }
        #endregion 초기화

        #region 스테이지 진행
        public bool Start_Stage()
        {
            if (Spawn_Player() == false)
                return false;

            Spawn_Enemies();

            // 이전 스테이지가 CLEAR/FAIL로 끝나며 0으로 내려둔 타임스케일을 되돌린다.
            Set_ActorTimeScale(1f);

            m_fRemainTime = m_cStageDesc.fTimeLimit;
            m_eState      = STAGE_STATE.PLAYING;
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

        // 260903_Engine은 프리팹이 없으면 Instantiate에서 예외를 던진다(반환값 null이 아님).
        // 호출 전에 미리 확인해야 스테이지 전체가 초기화 도중 죽는 것을 막을 수 있다.
        private static bool Has_Prefab(string strPrefabName)
        {
            return CGameInstance.Instance.Has_Prefab(strPrefabName);
        }

        private bool Spawn_Player()
        {
            if (Has_Prefab(PREFAB_PLAYER) == false)
            {
                Debug.LogError($"[CStage_Manager] '{PREFAB_PLAYER}'를 찾을 수 없습니다. "
                             + "Unity 메뉴 Tools/LandGrab/Setup Assets 를 실행한 뒤 다시 시도하세요.");
                return false;
            }

            // 시작 위치: 아래쪽 테두리(안전 지대)의 가운데
            Vector2Int vStartCell = new Vector2Int(m_cStageDesc.iGridWidth / 2, m_cStageDesc.iBorderThick - 1);

            CPlayerDesc cPlayerDesc = new CPlayerDesc
            {
                eObjectType     = OBJECT_TYPE.PLAYER,
                strPrefabName   = PREFAB_PLAYER,
                cGrid           = m_cGrid,
                vStartCell      = vStartCell,
                fMoveSpeed      = m_cStageDesc.fMoveSpeed,
                iLife           = m_cStageDesc.iLife,
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
        #endregion 스테이지 진행

        #region 몬스터
        private void Spawn_Enemies()
        {
            m_lstEnemy.Clear();

            if (m_cStageDesc.iEnemyCount <= 0)
                return;

            // 260903_몬스터가 없어도 스테이지는 굴러가야 한다 — 경고만 남기고 넘어간다.
            if (Has_Prefab(PREFAB_ENEMY) == false)
            {
                Debug.LogWarning($"[CStage_Manager] '{PREFAB_ENEMY}'가 없어 몬스터 없이 진행합니다. "
                               + "Unity 메뉴 Tools/LandGrab/Setup Assets 를 실행하세요.");
                return;
            }

            for (int i = 0; i < m_cStageDesc.iEnemyCount; ++i)
            {
                CEnemyDesc cEnemyDesc = new CEnemyDesc
                {
                    eObjectType     = OBJECT_TYPE.ENEMY,
                    strPrefabName   = PREFAB_ENEMY,
                    cGrid           = m_cGrid,
                    vStartCell      = Get_EnemySpawnCell(i),
                    vStartDir       = Get_EnemySpawnDir(i),
                    fSpeed          = m_cStageDesc.fEnemySpeed,
                    fChaseSpeed     = m_cStageDesc.fEnemyChaseSpeed,
                    fTurnRate       = m_cStageDesc.fEnemyTurnRate,
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
        }

        // 플레이어 시작 지점(아래쪽)에서 멀리 떨어진 가운데~위쪽 대역에 고르게 배치한다.
        private Vector2Int Get_EnemySpawnCell(int iIndex)
        {
            float fT = (iIndex + 1f) / (m_cStageDesc.iEnemyCount + 1f);

            int x = Mathf.RoundToInt(Mathf.Lerp(m_cStageDesc.iGridWidth * 0.2f, m_cStageDesc.iGridWidth * 0.8f, fT));
            int y = Mathf.RoundToInt(m_cStageDesc.iGridHeight * (0.4f + 0.15f * (iIndex % 3)));

            return new Vector2Int(x, y);
        }

        private Vector2 Get_EnemySpawnDir(int iIndex)
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

            float fHitRange = m_cStageDesc.fEnemyHitRange * m_cGrid.CELL_SIZE;
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
                if (bExposed == true && Vector2.Distance(cEnemy.POS, vPlayerPos) <= fHitRange)
                    bHit = true;
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

            if (m_cGrid.OWNED_RATIO >= m_cStageDesc.fClearRatio)
                Set_State(STAGE_STATE.CLEAR);
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

            Debug.Log($"[CStage_Manager] STAGE {eState} — 점령률 {m_cGrid.OWNED_RATIO:P1}");
        }
        #endregion 콜백
    }
}
