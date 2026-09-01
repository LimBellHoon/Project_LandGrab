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
        private readonly CTerritoryGrid m_cGrid         = new CTerritoryGrid();
        private readonly CGridRenderer  m_cGridRenderer = new CGridRenderer();

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

            m_fRemainTime = m_cStageDesc.fTimeLimit;
            m_eState      = STAGE_STATE.PLAYING;
            return true;
        }

        public void Tick(float fDeltaTime)
        {
            m_cGridRenderer.Tick();

            if (m_eState != STAGE_STATE.PLAYING)
                return;

            m_fRemainTime -= fDeltaTime;
            if (m_fRemainTime <= 0f)
            {
                m_fRemainTime = 0f;
                Set_State(STAGE_STATE.FAIL);
            }
        }

        private bool Spawn_Player()
        {
            // 시작 위치: 아래쪽 테두리(안전 지대)의 가운데
            Vector2Int vStartCell = new Vector2Int(m_cStageDesc.iGridWidth / 2, m_cStageDesc.iBorderThick - 1);

            CPlayerDesc cPlayerDesc = new CPlayerDesc
            {
                eObjectType     = OBJECT_TYPE.PLAYER,
                strPrefabName   = "Prefab_Player",
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
            m_cPlayer.GetEnemyCells  = null;    // TODO: 몬스터 구현 시 살아있는 몬스터의 셀 목록을 넘긴다

            return true;
        }
        #endregion 스테이지 진행

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
