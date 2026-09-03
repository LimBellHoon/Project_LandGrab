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
        private const string PREFAB_PLAYER     = "Prefab_Player";
        private const string PREFAB_ENEMY      = "Prefab_Enemy";
        // 260904_몬스터 기믹
        private const string PREFAB_PROJECTILE = "Prefab_Projectile";
        private const string PREFAB_WEB        = "Prefab_Web";
        private const int    GIMMICK_SEARCH_RADIUS = 6;    // 거미줄/소환 위치를 찾을 최대 반경(셀)

        private readonly CTerritoryGrid m_cGrid         = new CTerritoryGrid();
        private readonly CGridRenderer  m_cGridRenderer = new CGridRenderer();

        // 260902_몬스터
        private readonly List<CEnemy>       m_lstEnemy      = new List<CEnemy>();
        private readonly List<Vector2Int>   m_lstEnemyCell  = new List<Vector2Int>();  // 점령 판정용 재사용 버퍼

        // 260904_몬스터 기믹
        private readonly List<CProjectile>  m_lstProjectile = new List<CProjectile>();
        private readonly List<CWeb>         m_lstWeb        = new List<CWeb>();

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
            m_cGridRenderer.Release();
            m_lstEnemy.Clear();
            m_lstProjectile.Clear();
            m_lstWeb.Clear();
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
                ENEMY_GIMMICK eGimmick = Get_EnemyGimmick(i);

                Spawn_Enemy(new CEnemyDesc
                {
                    eObjectType     = OBJECT_TYPE.ENEMY,
                    strPrefabName   = PREFAB_ENEMY,
                    cGrid           = m_cGrid,
                    vStartCell      = Get_EnemySpawnCell(i),
                    vStartDir       = Get_EnemySpawnDir(i),
                    fSpeed          = m_cStageDesc.fEnemySpeed,
                    fChaseSpeed     = m_cStageDesc.fEnemyChaseSpeed,
                    fTurnRate       = m_cStageDesc.fEnemyTurnRate,
                    eGimmick        = eGimmick,
                    fGimmickCool    = Get_GimmickCool(eGimmick),
                    fScale          = 1f,
                });
            }
        }

        // 260904_몬스터 기믹: 최초 스폰과 미니 몬스터 소환이 같은 경로를 타도록 분리
        private CEnemy Spawn_Enemy(CEnemyDesc cEnemyDesc)
        {
            GameObject goEnemy = CGameInstance.Instance.Reuse_Object(cEnemyDesc);
            if (goEnemy == null)
            {
                Debug.LogError("[CStage_Manager] Enemy 생성 실패 — Addressable 라벨/프리팹 이름을 확인하세요.");
                return null;
            }

            CEnemy cEnemy = goEnemy.GetComponent<CEnemy>();
            if (cEnemy == null)
            {
                Debug.LogError("[CStage_Manager] 프리팹에 CEnemy 컴포넌트가 없습니다.");
                return null;
            }

            if (cEnemyDesc.eGimmick != ENEMY_GIMMICK.NONE)
                cEnemy.OnGimmick += On_EnemyGimmick;

            m_lstEnemy.Add(cEnemy);
            return cEnemy;
        }

        private ENEMY_GIMMICK Get_EnemyGimmick(int iIndex)
        {
            ENEMY_GIMMICK[] arrGimmick = m_cStageDesc.arrEnemyGimmick;
            if (arrGimmick == null || iIndex >= arrGimmick.Length)
                return ENEMY_GIMMICK.NONE;

            return arrGimmick[iIndex];
        }

        private float Get_GimmickCool(ENEMY_GIMMICK eGimmick)
        {
            switch (eGimmick)
            {
                case ENEMY_GIMMICK.PROJECTILE: return m_cStageDesc.fProjectileCool;
                case ENEMY_GIMMICK.WEB:        return m_cStageDesc.fWebCool;
                case ENEMY_GIMMICK.SUMMON:     return m_cStageDesc.fSummonCool;
                default:                       return 0f;
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

            Prune_Enemies();

            // 플레이어가 안전 지대(선) 위에 있으면 몬스터는 쫓지 않고 배회한다.
            bool bExposed = m_cGrid.Get_Cell(m_cPlayer.CUR_CELL) != CELL_STATE.OWNED;
            Vector2 vPlayerPos = m_cPlayer.transform.position;

            float fHitRange = m_cStageDesc.fEnemyHitRange * m_cGrid.CELL_SIZE;
            bool bHit = false;

            for (int i = 0; i < m_lstEnemy.Count; ++i)
            {
                CEnemy cEnemy = m_lstEnemy[i];
                cEnemy.Set_ChaseState(bExposed, vPlayerPos);

                // 그리는 중인 선분에 몬스터가 닿아도 사망한다.
                if (m_cGrid.Get_Cell(cEnemy.CUR_CELL) == CELL_STATE.TRAIL)
                {
                    bHit = true;
                    break;
                }

                // 플레이어와의 직접 충돌은 땅을 먹으러 나와 있을 때만 판정한다.
                if (bExposed == true && Vector2.Distance(cEnemy.POS, vPlayerPos) <= fHitRange)
                {
                    bHit = true;
                    break;
                }
            }

            // 260904_기믹 판정. 투사체도 몬스터와 같은 규칙(선분 접촉 / 노출된 플레이어 접촉)을 따른다.
            if (bHit == false)
                bHit = Tick_Projectile(bExposed, vPlayerPos, fHitRange);

            Tick_Web(bExposed, vPlayerPos);

            if (bHit == true)
                m_cPlayer.Damage();
        }

        #region 기믹
        private void On_EnemyGimmick(CEnemy cEnemy)
        {
            switch (cEnemy.GIMMICK)
            {
                case ENEMY_GIMMICK.PROJECTILE: Fire_Projectile(cEnemy, false); break;
                case ENEMY_GIMMICK.WEB:        Fire_Projectile(cEnemy, true);  break;
                case ENEMY_GIMMICK.SUMMON:     Summon_Minion(cEnemy);          break;
            }
        }

        private void Fire_Projectile(CEnemy cEnemy, bool bLeaveWeb)
        {
            if (Has_Prefab(PREFAB_PROJECTILE) == false)
                return;

            Vector2 vDir = cEnemy.TARGET_POS - cEnemy.POS;
            if (vDir.sqrMagnitude <= Mathf.Epsilon)
                return;

            CProjectileDesc cDesc = new CProjectileDesc
            {
                eObjectType     = OBJECT_TYPE.ENEMY_EFFECT,
                strPrefabName   = PREFAB_PROJECTILE,
                cGrid           = m_cGrid,
                vStartPos       = cEnemy.POS,
                vDir            = vDir.normalized,
                fSpeed          = m_cStageDesc.fProjectileSpeed,
                fLifeTime       = m_cStageDesc.fProjectileLife,
                bLeaveWeb       = bLeaveWeb,
            };

            GameObject goProjectile = CGameInstance.Instance.Reuse_Object(cDesc);
            CProjectile cProjectile = goProjectile != null ? goProjectile.GetComponent<CProjectile>() : null;
            if (cProjectile == null)
                return;

            cProjectile.OnExpire += On_ProjectileExpire;
            m_lstProjectile.Add(cProjectile);
        }

        private void On_ProjectileExpire(CProjectile cProjectile)
        {
            if (cProjectile.LEAVE_WEB == true)
                Spawn_Web(cProjectile.POS);
        }

        private void Spawn_Web(Vector2 vPos)
        {
            if (Has_Prefab(PREFAB_WEB) == false)
                return;

            // 투사체는 점령지에 닿아 소멸하는 경우가 많다. 거미줄은 미점령 지대에 깔려야
            // 의미가 있으므로 가장 가까운 미점령 칸으로 밀어 넣는다.
            Vector2Int vCell = m_cGrid.World_ToCell(vPos);
            if (m_cGrid.Get_Cell(vCell) != CELL_STATE.EMPTY)
            {
                if (m_cGrid.Try_Find_NearestCell(vCell, CELL_STATE.EMPTY, GIMMICK_SEARCH_RADIUS, out Vector2Int vFound) == false)
                    return;

                vPos = m_cGrid.Cell_ToWorld(vFound);
            }

            CWebDesc cDesc = new CWebDesc
            {
                eObjectType     = OBJECT_TYPE.ENEMY_EFFECT,
                strPrefabName   = PREFAB_WEB,
                cGrid           = m_cGrid,
                vPos            = vPos,
                fRadius         = m_cStageDesc.fWebRadius,
                fDuration       = m_cStageDesc.fWebDuration,
                fSlowRate       = m_cStageDesc.fWebSlowRate,
                fSlowTime       = m_cStageDesc.fWebSlowTime,
            };

            GameObject goWeb = CGameInstance.Instance.Reuse_Object(cDesc);
            CWeb cWeb = goWeb != null ? goWeb.GetComponent<CWeb>() : null;
            if (cWeb == null)
                return;

            m_lstWeb.Add(cWeb);
        }

        private void Summon_Minion(CEnemy cSummoner)
        {
            if (cSummoner.SUMMON_COUNT >= m_cStageDesc.iSummonMax)
                return;

            if (m_cGrid.Try_Find_NearestCell(cSummoner.CUR_CELL, CELL_STATE.EMPTY,
                                             GIMMICK_SEARCH_RADIUS, out Vector2Int vSpawnCell) == false)
                return;

            // 미니 몬스터는 기믹이 없다 — 소환된 놈이 또 소환하면 무한히 불어난다.
            CEnemy cMinion = Spawn_Enemy(new CEnemyDesc
            {
                eObjectType     = OBJECT_TYPE.ENEMY,
                strPrefabName   = PREFAB_ENEMY,
                cGrid           = m_cGrid,
                vStartCell      = vSpawnCell,
                vStartDir       = Get_EnemySpawnDir(cSummoner.SUMMON_COUNT),
                fSpeed          = m_cStageDesc.fEnemySpeed * m_cStageDesc.fMinionSpeedRate,
                fChaseSpeed     = m_cStageDesc.fEnemyChaseSpeed * m_cStageDesc.fMinionSpeedRate,
                fTurnRate       = m_cStageDesc.fEnemyTurnRate,
                eGimmick        = ENEMY_GIMMICK.NONE,
                fScale          = m_cStageDesc.fMinionScale,
            });

            if (cMinion == null)
                return;

            cMinion.SUMMONER = cSummoner;
            ++cSummoner.SUMMON_COUNT;
        }

        /// <returns> 투사체가 플레이어나 선분에 닿았으면 true </returns>
        private bool Tick_Projectile(bool bExposed, Vector2 vPlayerPos, float fHitRange)
        {
            bool bHit = false;

            for (int i = m_lstProjectile.Count - 1; i >= 0; --i)
            {
                CProjectile cProjectile = m_lstProjectile[i];

                if (cProjectile == null || cProjectile.bCollect == true)
                {
                    m_lstProjectile.RemoveAt(i);
                    continue;
                }

                if (m_cGrid.Get_Cell(cProjectile.CUR_CELL) == CELL_STATE.TRAIL)
                    bHit = true;
                else if (bExposed == true && Vector2.Distance(cProjectile.POS, vPlayerPos) <= fHitRange)
                    bHit = true;
            }

            return bHit;
        }

        private void Tick_Web(bool bExposed, Vector2 vPlayerPos)
        {
            for (int i = m_lstWeb.Count - 1; i >= 0; --i)
            {
                CWeb cWeb = m_lstWeb[i];

                if (cWeb == null || cWeb.bCollect == true)
                {
                    m_lstWeb.RemoveAt(i);
                    continue;
                }

                // 선 위에서는 완전히 안전해야 하므로 나와 있을 때만 감속된다.
                if (bExposed == true && cWeb.Is_Inside(vPlayerPos) == true)
                    m_cPlayer.Apply_Slow(cWeb.SLOW_RATE, cWeb.SLOW_TIME);
            }
        }
        #endregion 기믹

        // 260904_풀에 반납된 몬스터를 목록에서 걷어낸다. 미니 몬스터가 사라지면
        // 소환자의 SUMMON_COUNT를 되돌려 다시 소환할 수 있게 한다.
        private void Prune_Enemies()
        {
            for (int i = m_lstEnemy.Count - 1; i >= 0; --i)
            {
                CEnemy cEnemy = m_lstEnemy[i];
                if (cEnemy != null && cEnemy.bCollect == false)
                    continue;

                if (cEnemy != null && cEnemy.SUMMONER != null)
                    --cEnemy.SUMMONER.SUMMON_COUNT;

                m_lstEnemy.RemoveAt(i);
            }
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

        private void Set_State(STAGE_STATE eState)
        {
            if (m_eState == eState)
                return;

            m_eState = eState;
            Debug.Log($"[CStage_Manager] STAGE {eState} — 점령률 {m_cGrid.OWNED_RATIO:P1}");
        }
        #endregion 콜백
    }
}
