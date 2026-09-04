using System;
using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 플레이어
    /// <summary>
    /// Engine.CGameObject를 상속해 오브젝트 풀/레이어 Tick에 그대로 올라탄다.
    /// 규칙 판정은 '셀에 도착한 순간'에만 수행한다 (CMoveHandler.Tick의 반환값).
    /// </summary>
    public class CPlayer : CGameObject
    {
        private const float INVINCIBLE_TIME = 1.2f;     // 피격 후 무적 시간

        private readonly CInputHandler m_cInputHandler = new CInputHandler();
        private readonly CMoveHandler  m_cMoveHandler  = new CMoveHandler();

        [SerializeField] private SpriteRenderer m_srBody;

        private CTerritoryGrid  m_cGrid;
        private Vector2Int      m_vLastSafeCell;        // 안전 지대를 벗어나기 직전 셀 — 사망 시 복귀 지점
        private int             m_iLife;
        private float           m_fInvincibleTimer;
        private float           m_fBaseSpeed;       // 260904_거미줄 감속의 기준이 되는 원래 속도

        public int          LIFE            => m_iLife;
        public Vector2Int   CUR_CELL        => m_cMoveHandler.CUR_CELL;
        public bool         IS_INVINCIBLE   => m_fInvincibleTimer > 0f;
        /// <summary> 260904_UI가 조이스틱을 그리려고 읽는다. </summary>
        public CVirtualJoystick JOYSTICK    => m_cInputHandler.JOYSTICK;

        /// <summary> 새로 점령한 셀 개수를 전달 </summary>
        public event Action<int> OnCapture;
        /// <summary> 남은 목숨을 전달 </summary>
        public event Action<int> OnLifeChanged;
        /// <summary> 목숨을 전부 잃음 </summary>
        public event Action OnDead;
        /// <summary> 점령 판정에 쓸 몬스터 셀 목록 공급자 (없으면 null) </summary>
        public Func<IReadOnlyList<Vector2Int>> GetEnemyCells;

        #region Engine.CGameObject
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CPlayerDesc cDesc) == false)
            {
                Debug.LogError("[CPlayer] CPlayerDesc가 아닙니다.");
                return false;
            }

            m_cGrid             = cDesc.cGrid;
            m_iLife             = cDesc.iLife;
            m_fInvincibleTimer  = 0f;
            m_vLastSafeCell     = cDesc.vStartCell;
            m_fBaseSpeed        = cDesc.fMoveSpeed;

            if (m_cMoveHandler.Initialize(m_cGrid, cDesc.vStartCell, cDesc.fMoveSpeed) == false)
                return false;

            transform.position = m_cGrid.Cell_ToWorld(cDesc.vStartCell);
            // 바디 스프라이트는 1 월드 유닛 크기로 제작되어 있으므로, 셀 크기의 1.6배로 맞춘다.
            transform.localScale = Vector3.one * m_cGrid.CELL_SIZE * 1.6f;

            m_cInputHandler.Initialize();
            m_cInputHandler.Clear();
            return true;
        }

        public override void Tick(float fDeltaTime)
        {
            if (m_cGrid == null)
                return;

            if (m_fInvincibleTimer > 0f)
            {
                m_fInvincibleTimer -= fDeltaTime;
                Refresh_InvincibleBlink();
            }

            m_cInputHandler.Tick();

            if (m_cMoveHandler.Tick(fDeltaTime, m_cInputHandler.DESIRED_DIR, out Vector2Int vArrivedCell) == true)
                Handle_ArriveCell(vArrivedCell);

            transform.position = m_cMoveHandler.WORLD_POS;
        }

        public override void Hide()
        {
            // 풀에 반납되므로 외부 구독을 끊어 다음 재사용에 새지 않게 한다.
            OnCapture       = null;
            OnLifeChanged   = null;
            OnDead          = null;
            GetEnemyCells   = null;
            m_cGrid         = null;

            base.Hide();
        }
        #endregion Engine.CGameObject

        #region 규칙 판정
        private void Handle_ArriveCell(Vector2Int vCell)
        {
            // 규칙 판정 자체는 그리드가 소유한다. 플레이어는 결과에 반응만 한다.
            STEP_RESULT eResult = m_cGrid.Step_To(vCell, GetEnemyCells != null ? GetEnemyCells() : null,
                                                  out int iCapturedCount);

            switch (eResult)
            {
                case STEP_RESULT.DEAD:
                    Damage();
                    break;

                case STEP_RESULT.CAPTURE:
                    m_vLastSafeCell = vCell;
                    OnCapture?.Invoke(iCapturedCount);
                    break;

                case STEP_RESULT.SAFE:
                    m_vLastSafeCell = vCell;
                    break;
            }
        }

        // 260904_거미줄 감속. 스테이지가 매 프레임 '지금 밟고 있는 칸'을 보고 넣어 준다.
        /// <param name="fScale"> 원래 속도에 곱할 값. 1이면 감속 없음. </param>
        public void Set_SpeedScale(float fScale)
        {
            m_cMoveHandler.SPEED = m_fBaseSpeed * Mathf.Max(0.1f, fScale);
        }

        // 260904_웨이브가 넘어가면 판을 새로 깔기 때문에 플레이어도 새 시작 칸으로 옮겨야 한다.
        // 목숨과 무적 상태는 웨이브를 넘어가도 이어진다.
        /// <summary> 지정한 칸으로 옮기고 이동/입력 상태를 리셋한다. </summary>
        public void Respawn(Vector2Int vCell)
        {
            if (m_cGrid == null)
                return;

            m_vLastSafeCell = vCell;
            m_cMoveHandler.Teleport(vCell);
            m_cInputHandler.Clear();
            transform.position = m_cGrid.Cell_ToWorld(vCell);
        }

        // 260904_이미 죽었거나 풀에 반납된 뒤의 호출을 막는다.
        // 같은 프레임에 여러 몬스터가 겹치거나 스테이지가 끝난 뒤에도 판정이 한 번 더 들어올 수 있어,
        // 목숨이 음수로 내려가거나 m_cGrid가 null인 채로 Clear_Trail을 부를 여지가 있었다.
        /// <summary> 몬스터/탄 피격, 자기 선 밟기 등으로 목숨 1 감소. </summary>
        public void Damage()
        {
            if (m_cGrid == null || m_iLife <= 0 || IS_INVINCIBLE == true)
                return;

            m_cGrid.Clear_Trail();

            --m_iLife;
            OnLifeChanged?.Invoke(m_iLife);

            if (m_iLife <= 0)
            {
                OnDead?.Invoke();
                return;
            }

            m_cMoveHandler.Teleport(m_vLastSafeCell);
            m_cInputHandler.Clear();
            m_fInvincibleTimer = INVINCIBLE_TIME;
        }
        #endregion 규칙 판정

        private void Refresh_InvincibleBlink()
        {
            if (m_srBody == null)
                return;

            Color cColor = m_srBody.color;
            cColor.a = m_fInvincibleTimer > 0f && Mathf.Repeat(m_fInvincibleTimer, 0.2f) < 0.1f ? 0.25f : 1f;
            m_srBody.color = cColor;
        }
    }
}
