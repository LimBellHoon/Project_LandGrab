using System;
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
    public class CStage_Manager : IGimmickHost
    {
        private const string PREFAB_PLAYER      = "Prefab_Player";
        private const string PREFAB_PROJECTILE  = "Prefab_Projectile";
        private const string PREFAB_WEB         = "Prefab_Web";
        private const int    SPAWN_SEARCH_RADIUS = 24;  // 스폰 자리가 막혔을 때 대신 찾아볼 반경(셀)

        // 260904_소환 기믹의 안전장치. RefID가 다시 SPAWN 몬스터를 가리키면 끝없이 늘어난다.
        // 규칙 값이 아니라 사고 방지용 상한이라 CSV로 빼지 않는다.
        private const int    MAX_ENEMY          = 32;

        // 탄의 충돌 반경(셀). 몬스터와 달리 종류가 하나뿐이라 CSV로 뺄 이유가 아직 없다.
        private const float  PROJECTILE_HIT_RANGE = 0.7f;

        // 260904_보상 공개 연출 길이(초). 규칙 값이 아니라 연출 타이밍이라 코드에 둔다.
        private const float  REVEAL_TIME = 0.5f;     // 가림막이 걷히는 시간
        private const float  HOLD_TIME   = 0.9f;     // 드러난 보상을 보여주는 시간
        private const float  COVER_TIME  = 0.5f;     // 다음 가림막이 덮이는 시간

        /// <summary> 웨이브를 넘길 때의 연출 단계. NONE이면 평소대로 게임이 돌아간다. </summary>
        private enum WAVE_PHASE { NONE, REVEAL, HOLD, COVER }

        private readonly CTerritoryGrid m_cGrid         = new CTerritoryGrid();
        private readonly CGridRenderer  m_cGridRenderer = new CGridRenderer();

        private readonly List<CEnemy>       m_lstEnemy      = new List<CEnemy>();
        private readonly List<Vector2Int>   m_lstEnemyCell  = new List<Vector2Int>();  // 점령 판정용 재사용 버퍼

        // 260904_기믹이 소환한 것들. 수명과 충돌을 여기서 한꺼번에 본다.
        private readonly List<CProjectile>  m_lstProjectile = new List<CProjectile>();
        private readonly List<CWeb>         m_lstWeb        = new List<CWeb>();

        private CMapInfo            m_cMapInfo;
        private CCSVData_EnemyInfo  m_cEnemyTable;
        private CPlayer             m_cPlayer;
        private STAGE_STATE         m_eState = STAGE_STATE.READY;
        private int                 m_iWave;
        private int                 m_iStar;    // 260905_이번 판에서 완료한 웨이브 수
        private float               m_fRemainTime;
        private bool                m_bPlayerExposed;   // 기믹 발동 조건 — 매 프레임 Tick_Enemy가 갱신한다
        private bool                m_bPaused;

        // 260904_웨이브 전환 연출
        private WAVE_PHASE          m_eWavePhase;
        private float               m_fPhaseTimer;
        private int                 m_iNextWave;        // 0이면 이번이 마지막 웨이브였다는 뜻

        // 260904_클리어/실패를 밖(CGameManager)이 알아야 진행도를 저장하고 선택 화면으로 돌아갈 수 있다.
        public event Action<STAGE_STATE> OnStateChanged;

        public bool             IS_PAUSED       => m_bPaused;
        public int              MAP_ID          => m_cMapInfo != null ? m_cMapInfo.iMapID : 0;
        public CTerritoryGrid   GRID            => m_cGrid;
        public CPlayer          PLAYER          => m_cPlayer;
        public STAGE_STATE      STATE           => m_eState;
        public float            REMAIN_TIME     => m_fRemainTime;
        public float            OWNED_RATIO     => m_cGrid.OWNED_RATIO;
        public int              LIFE            => m_cPlayer != null ? m_cPlayer.LIFE : 0;
        public int              ENEMY_COUNT     => m_lstEnemy.Count;
        public int              WAVE            => m_iWave;
        // 260905_별 = 이번 판에서 완료한 웨이브 수. 도중에 죽거나 시간이 끝나도 여기까지는 남는다.
        public int              STAR            => m_iStar;

        // 260905_능력치 강화 반영. Start_Stage 전에 넣어 둔다.
        private float           m_fSpeedRate = 1f;      // 이동 속도 배율
        private float           m_fEvasion;             // 피격 회피 확률 0~1
        private int             m_iBonusLife;           // 강화로 늘어난 시작 목숨
        private CSkillInfo      m_cSkillInfo;           // 260905_장착한 액티브 스킬
        private int             m_iSkillLevel;          // 260905_스킬 강화 레벨

        /// <param name="fSpeedRate"> 이동 속도에 곱할 값 (1 = 강화 없음) </param>
        /// <param name="fEvasion"> 피격을 무시할 확률 0~1 </param>
        /// <param name="iBonusLife"> 맵 기본 목숨에 더할 개수 </param>
        public void Set_PlayerUpgrade(float fSpeedRate, float fEvasion, int iBonusLife)
        {
            m_fSpeedRate = Mathf.Max(0.1f, fSpeedRate);
            m_fEvasion   = Mathf.Clamp01(fEvasion);
            m_iBonusLife = Mathf.Max(0, iBonusLife);
        }

        // 260905_장착 시스템이 생기기 전까지는 CGameManager가 표에서 골라 넣어 준다.
        public void Set_PlayerSkill(CSkillInfo cSkillInfo, int iSkillLevel)
        {
            m_cSkillInfo = cSkillInfo;
            m_iSkillLevel = Mathf.Max(0, iSkillLevel);
        }

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

            if (cMapInfo.bIsValid == false)
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

            OnStateChanged = null;
            m_bPaused      = false;
            m_eWavePhase   = WAVE_PHASE.NONE;

            Collect_Player();
            Collect_Enemies();
            m_cGridRenderer.Release();
            m_eState = STAGE_STATE.READY;
        }

        // 260904_스테이지를 다시 고를 수 있으므로 플레이어도 풀에 돌려줘야 한다.
        private void Collect_Player()
        {
            if (m_cPlayer != null)
                CGameInstance.Instance.Collect_Object(m_cPlayer);

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

            m_bPaused    = false;
            m_iStar      = 0;               // 260905_판을 새로 시작하면 별도 처음부터
            m_eWavePhase = WAVE_PHASE.NONE;
            m_cGridRenderer.Set_CoverAlpha(1f);

            m_eState = STAGE_STATE.PLAYING;
            Enter_Wave(1);
            return true;
        }

        public void Tick(float fDeltaTime)
        {
            m_cGridRenderer.Tick();

            if (m_eState != STAGE_STATE.PLAYING || m_bPaused == true)
                return;

            // 260904_연출 중에는 규칙을 멈추고 연출만 돌린다.
            // 제한 시간도 같이 멈춘다 — 연출 때문에 시간을 잃으면 억울하다.
            if (m_eWavePhase != WAVE_PHASE.NONE)
            {
                Tick_WaveTransition(fDeltaTime);
                return;
            }

            Tick_Enemy();
            Tick_Projectile();
            Tick_Web();

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

        // 260904_웨이브를 넘길 때 바로 갈아 끼우지 않는다.
        // 가림막을 걷어 보상을 보여주고, 잠깐 감상할 틈을 준 뒤에 다음 판을 덮는다.
        // 이 게임에서 '드러났다'는 순간이 재미의 전부라 그냥 툭 바꾸면 남는 게 없다.
        private void Next_Wave()
        {
            // 260905_웨이브를 하나 넘길 때마다 별 하나. 이 시점에 확정되므로
            // 뒤 웨이브에서 죽더라도 여기까지의 별은 남는다.
            m_iStar = m_iWave;

            Begin_WaveTransition(m_iWave >= m_cMapInfo.iWaveCount ? 0 : m_iWave + 1);
        }

        private void Begin_WaveTransition(int iNextWave)
        {
            m_iNextWave   = iNextWave;
            m_eWavePhase  = WAVE_PHASE.REVEAL;
            m_fPhaseTimer = 0f;

            Set_ActorTimeScale(0f);     // 연출 동안에는 아무도 움직이지 않는다
        }

        private void Tick_WaveTransition(float fDeltaTime)
        {
            m_fPhaseTimer += fDeltaTime;

            switch (m_eWavePhase)
            {
                case WAVE_PHASE.REVEAL:
                    m_cGridRenderer.Set_CoverAlpha(1f - Mathf.Clamp01(m_fPhaseTimer / REVEAL_TIME));
                    if (m_fPhaseTimer >= REVEAL_TIME)
                        Go_Phase(WAVE_PHASE.HOLD);
                    break;

                case WAVE_PHASE.HOLD:
                    if (m_fPhaseTimer < HOLD_TIME)
                        break;

                    // 마지막 웨이브였다면 최종 보상이 드러난 화면 그대로 끝낸다.
                    if (m_iNextWave <= 0)
                    {
                        m_eWavePhase = WAVE_PHASE.NONE;
                        Set_State(STAGE_STATE.CLEAR);
                        break;
                    }

                    Enter_Wave(m_iNextWave);
                    m_cGridRenderer.Set_CoverAlpha(0f);
                    Go_Phase(WAVE_PHASE.COVER);
                    break;

                case WAVE_PHASE.COVER:
                    m_cGridRenderer.Set_CoverAlpha(Mathf.Clamp01(m_fPhaseTimer / COVER_TIME));
                    if (m_fPhaseTimer < COVER_TIME)
                        break;

                    m_cGridRenderer.Set_CoverAlpha(1f);
                    m_eWavePhase = WAVE_PHASE.NONE;
                    Set_ActorTimeScale(1f);
                    break;
            }
        }

        private void Go_Phase(WAVE_PHASE ePhase)
        {
            m_eWavePhase  = ePhase;
            m_fPhaseTimer = 0f;
        }

        // 260904_일시정지. 액터를 세우는 길은 Set_ActorTimeScale 하나뿐이다(2-8).
        // 260905_소모품은 인벤토리에서 개수를 깎는 쪽(CGameManager)이 먼저 판단하고,
        // 실제 효과만 여기서 플레이어에게 건다.
        /// <returns> 효과를 걸었으면 true </returns>
        public bool Apply_Consumable(CONSUME_EFFECT eEffect)
        {
            if (m_eState != STAGE_STATE.PLAYING || m_bPaused == true || m_cPlayer == null)
                return false;

            switch (eEffect)
            {
                case CONSUME_EFFECT.SHIELD: m_cPlayer.Add_Shield(); return true;
                case CONSUME_EFFECT.HEAL:   m_cPlayer.Heal(1);      return true;
                default:                    return false;
            }
        }


        // 260905_스킬 버튼은 UI에 있고 플레이어는 스테이지가 갖고 있으므로 여기를 거친다.
        /// <summary> 연출 중이거나 멈춰 있을 때는 발동하지 않는다. </summary>
        public bool Try_UseSkill()
        {
            if (m_eState != STAGE_STATE.PLAYING || m_bPaused == true)
                return false;

            if (m_eWavePhase != WAVE_PHASE.NONE || m_cPlayer == null)
                return false;

            return m_cPlayer.Try_UseSkill();
        }

        public void Set_Pause(bool bPause)
        {
            if (m_eState != STAGE_STATE.PLAYING || m_bPaused == bPause)
                return;

            m_bPaused = bPause;

            // 연출 중이면 원래도 멈춰 있어야 하므로 풀어 줄 때도 0을 유지한다.
            bool bResume = bPause == false && m_eWavePhase == WAVE_PHASE.NONE;
            Set_ActorTimeScale(bResume == true ? 1f : 0f);
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
                // 260905_능력치 강화 반영
                fMoveSpeed      = m_cMapInfo.fPlayerSpeed * m_fSpeedRate,
                fEvasion        = m_fEvasion,
                cSkillInfo      = m_cSkillInfo,
                iSkillLevel     = m_iSkillLevel,
                iLife           = m_cMapInfo.iLife + m_iBonusLife,
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
                    Spawn_Enemy(cInfo, Find_EnemySpawnCell(iSpawned, iTotal), Get_EnemySpawnDir(iSpawned));
                    ++iSpawned;
                }
            }
        }

        private void Spawn_Enemy(CEnemyInfo cInfo, Vector2Int vCell, Vector2 vDir)
        {
            if (m_lstEnemy.Count >= MAX_ENEMY)
                return;

            CEnemyDesc cEnemyDesc = new CEnemyDesc
            {
                eObjectType     = OBJECT_TYPE.ENEMY,
                strPrefabName   = cInfo.strPrefabName,
                cGrid           = m_cGrid,
                vStartCell      = vCell,
                vStartDir       = vDir,
                eGimmick        = cInfo.eGimmick,
                fSpeed          = cInfo.fSpeed,
                fChaseSpeed     = cInfo.fChaseSpeed,
                fTurnRate       = cInfo.fTurnRate,
                fHitRange       = cInfo.fHitRange,
                fGimmickCool    = cInfo.fGimmickCool,
                fGimmickValue   = cInfo.fGimmickValue,
                fGimmickRange   = cInfo.fGimmickRange,
                fGimmickDuration= cInfo.fGimmickDuration,
                iGimmickRefID   = cInfo.iGimmickRefID,
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

            cEnemy.Set_GimmickHost(this);
            m_lstEnemy.Add(cEnemy);
        }

        /// <summary> 웨이브가 바뀔 때 몬스터와 기믹 소환물을 전부 풀에 돌려준다. </summary>
        private void Collect_Enemies()
        {
            Collect_All(m_lstEnemy);
            Collect_All(m_lstProjectile);
            Collect_All(m_lstWeb);
        }

        // 목록 세 개가 같은 일을 하므로 하나로 묶는다.
        // 이쪽은 아직 살아 있는 오브젝트를 즉시 걷어내는 길이라 Engine의 자동 회수를 기다리지 않는다.
        private static void Collect_All<T>(List<T> lstObject) where T : CGameObject
        {
            for (int i = 0; i < lstObject.Count; ++i)
            {
                if (lstObject[i] != null)
                    CGameInstance.Instance.Collect_Object(lstObject[i]);
            }

            lstObject.Clear();
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

            m_bPlayerExposed = bExposed;

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

        #region 기믹 소환물 (IGimmickHost)
        // 260904_투사체·거미줄·부하는 전부 여기서 만들고 여기서 회수한다.
        // 기믹 모듈이 직접 풀을 만지면 웨이브가 넘어갈 때 회수할 방법이 없어진다.
        public bool IS_PLAYER_EXPOSED => m_bPlayerExposed;

        public void Spawn_Projectile(Vector2 vPos, Vector2 vDir, float fSpeed, float fRange, float fLifeTime)
        {
            if (Has_Prefab(PREFAB_PROJECTILE) == false)
                return;

            CProjectileDesc cDesc = new CProjectileDesc
            {
                eObjectType     = OBJECT_TYPE.ENEMY_EFFECT,
                strPrefabName   = PREFAB_PROJECTILE,
                cGrid           = m_cGrid,
                vStartPos       = vPos,
                vDir            = vDir,
                fSpeed          = fSpeed,
                fMaxRange       = fRange,
                fLifeTime       = fLifeTime,
                fHitRange       = PROJECTILE_HIT_RANGE,
            };

            GameObject goProjectile = CGameInstance.Instance.Reuse_Object(cDesc);
            if (goProjectile == null)
                return;

            CProjectile cProjectile = goProjectile.GetComponent<CProjectile>();
            if (cProjectile != null)
                m_lstProjectile.Add(cProjectile);
        }

        public void Spawn_Web(Vector2Int vCell, float fLifeTime, float fSlowRatio)
        {
            if (Has_Prefab(PREFAB_WEB) == false)
                return;

            // 같은 칸에 겹쳐 깔아 봐야 효과는 같고 오브젝트만 는다.
            for (int i = 0; i < m_lstWeb.Count; ++i)
            {
                if (m_lstWeb[i].CELL == vCell)
                    return;
            }

            CWebDesc cDesc = new CWebDesc
            {
                eObjectType     = OBJECT_TYPE.ENEMY_EFFECT,
                strPrefabName   = PREFAB_WEB,
                cGrid           = m_cGrid,
                vCell           = vCell,
                fLifeTime       = fLifeTime,
                fSlowRatio      = fSlowRatio,
            };

            GameObject goWeb = CGameInstance.Instance.Reuse_Object(cDesc);
            if (goWeb == null)
                return;

            CWeb cWeb = goWeb.GetComponent<CWeb>();
            if (cWeb != null)
                m_lstWeb.Add(cWeb);
        }

        public void Spawn_Minion(int iEnemyID, int iCount, Vector2 vPos)
        {
            if (m_cEnemyTable == null)
                return;

            CEnemyInfo cInfo = m_cEnemyTable.Get_Info(iEnemyID);
            if (cInfo == null || Has_Prefab(cInfo.strPrefabName) == false)
                return;

            Vector2Int vFrom = m_cGrid.World_ToCell(vPos);

            for (int i = 0; i < iCount; ++i)
            {
                // 소환된 자리가 점령지면 몬스터가 갇힌다 — 가장 가까운 미점령 칸을 찾아 준다.
                if (m_cGrid.Try_Find_NearestCell(vFrom, CELL_STATE.EMPTY, SPAWN_SEARCH_RADIUS,
                                                 out Vector2Int vCell) == false)
                    return;

                Spawn_Enemy(cInfo, vCell, Get_EnemySpawnDir(m_lstEnemy.Count));
            }
        }

        private void Tick_Projectile()
        {
            Vector2 vPlayerPos = m_cPlayer != null ? (Vector2)m_cPlayer.transform.position : Vector2.zero;
            bool bHit = false;

            // 뒤에서부터 지운다 — 앞에서 지우면 인덱스가 밀린다.
            for (int i = m_lstProjectile.Count - 1; i >= 0; --i)
            {
                CProjectile cProjectile = m_lstProjectile[i];

                // 260904_만료된 것은 Engine이 bCollect를 보고 알아서 풀로 돌려준다.
                // 여기서 Collect_Object까지 부르면 같은 오브젝트를 두 번 반납하게 되므로
                // 목록에서 빼기만 한다.
                if (cProjectile == null || cProjectile.IS_EXPIRED == true)
                {
                    m_lstProjectile.RemoveAt(i);
                    continue;
                }

                // 몬스터 충돌과 같은 규칙 — 땅을 먹으러 나와 있을 때만 맞는다.
                // 탄은 점령지에 닿는 순간 사라지므로 안전 지대까지 쫓아오지 못한다.
                if (m_bPlayerExposed == true
                    && Vector2.Distance(cProjectile.POS, vPlayerPos) <= cProjectile.HIT_RANGE * m_cGrid.CELL_SIZE)
                {
                    bHit = true;
                    cProjectile.Expire();
                }
            }

            if (bHit == true && m_cPlayer != null)
                m_cPlayer.Damage();
        }

        private void Tick_Web()
        {
            Vector2Int vPlayerCell = m_cPlayer != null ? m_cPlayer.CUR_CELL : Vector2Int.zero;
            float fSlowRatio = 1f;

            for (int i = m_lstWeb.Count - 1; i >= 0; --i)
            {
                CWeb cWeb = m_lstWeb[i];

                if (cWeb == null || cWeb.IS_EXPIRED == true)
                {
                    m_lstWeb.RemoveAt(i);
                    continue;
                }

                if (cWeb.CELL == vPlayerCell)
                    fSlowRatio = Mathf.Min(fSlowRatio, cWeb.SLOW_RATIO);
            }

            // 밟고 있지 않으면 1이 들어가 원래 속도로 돌아온다.
            m_cPlayer?.Set_SpeedScale(fSlowRatio);
        }
        #endregion 기믹 소환물 (IGimmickHost)

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
            // 260904_투사체·거미줄은 ENEMY_EFFECT 레이어에 올라간다. 같이 세우지 않으면 탄만 계속 날아간다.
            CGameInstance.Instance.Set_LayerTimeScale(OBJECT_TYPE.ENEMY_EFFECT, fTimeScale);
        }

        private void Set_State(STAGE_STATE eState)
        {
            if (m_eState == eState)
                return;

            m_eState = eState;

            if (eState == STAGE_STATE.CLEAR || eState == STAGE_STATE.FAIL)
                Set_ActorTimeScale(0f);

            Debug.Log($"[CStage_Manager] STAGE {eState} — {m_iWave}웨이브, 점령률 {m_cGrid.OWNED_RATIO:P1}");
            OnStateChanged?.Invoke(eState);
        }
        #endregion 콜백
    }
}
