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
        private const float INVINCIBLE_TIME  = 1.2f;    // 피격 후 무적 시간
        private const float EVADE_GRACE_TIME = 0.4f;    // 260905_회피 성공 후 빠져나갈 틈
        // 260912_마감 스킬이 점령지를 찾아 나가는 한계. 맵을 통째로 훑지 않게 막는다.
        private const int   SEAL_MAX_RADIUS  = 40;      // 셀
        private const int   SEAL_MAX_STEP    = 120;     // 셀

        private readonly CInputHandler m_cInputHandler = new CInputHandler();
        private readonly CMoveHandler  m_cMoveHandler  = new CMoveHandler();
        // 260905_액티브 스킬. 쿨타임은 핸들러가, 실제 효과는 CSkillEffect가 맡는다.
        private readonly CSkillHandler m_cSkillHandler = new CSkillHandler();

        [SerializeField] private SpriteRenderer m_srBody;

        private CTerritoryGrid  m_cGrid;
        private Vector2Int      m_vLastSafeCell;        // 안전 지대를 벗어나기 직전 셀 — 사망 시 복귀 지점
        private int             m_iLife;
        private float           m_fInvincibleTimer;
        private float           m_fBaseSpeed;       // 260904_거미줄 감속의 기준이 되는 원래 속도
        private float           m_fEvasion;         // 260905_피격 회피 확률 0~1
        private bool            m_bShield;          // 260905_소모품 보호막. 다음 피격 1회를 막는다

        // 260912_스킬 효과. 종류별 모듈이라 CPlayer에는 분기가 없다.
        private CSkillEffect    m_cSkillEffect;
        private ISkillHost      m_cSkillHost;

        // 260912_속도는 두 갈래로 곱해진다 — 거미줄(환경)과 질주(스킬).
        // 스테이지가 매 프레임 환경 배율을 넣어 주므로, 스킬 배율을 따로 두지 않으면
        // 질주를 걸어도 다음 프레임에 덮어써져 아무 일도 일어나지 않는다.
        private float           m_fEnvSpeedScale   = 1f;
        private float           m_fSkillSpeedScale = 1f;
        private float           m_fSkillSpeedTimer;
        // 260912_카드로 얹은 속도. 판이 끝날 때까지 유지되므로 타이머가 없다.
        private float           m_fCardSpeedScale = 1f;

        public int          LIFE            => m_iLife;
        public Vector2Int   CUR_CELL        => m_cMoveHandler.CUR_CELL;
        public bool         IS_INVINCIBLE   => m_fInvincibleTimer > 0f;
        /// <summary> 260905_보호막을 들고 있는가. UI가 표시에 쓴다. </summary>
        public bool         HAS_SHIELD      => m_bShield;
        /// <summary> 260904_UI가 조이스틱을 그리려고 읽는다. </summary>
        public CVirtualJoystick JOYSTICK    => m_cInputHandler.JOYSTICK;
        /// <summary> 260905_UI가 쿨타임 게이지를 그리려고 읽는다. </summary>
        public CSkillHandler    SKILL       => m_cSkillHandler;

        /// <summary> 새로 점령한 셀 개수를 전달 </summary>
        public event Action<int> OnCapture;
        /// <summary> 남은 목숨을 전달 </summary>
        public event Action<int> OnLifeChanged;
        /// <summary> 목숨을 전부 잃음 </summary>
        public event Action OnDead;
        /// <summary> 260905_회피 성공. 연출/사운드를 붙일 자리. </summary>
        public event Action OnEvade;
        // 260916_OnLifeChanged는 Heal에도 불려 '맞았다'만 골라 듣기 어렵다.
        /// <summary> 실제로 맞아 목숨이 줄었을 때만. 카메라 흔들림 같은 피격 연출은 이걸 들을 것. </summary>
        public event Action OnDamaged;
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
            m_fEvasion          = Mathf.Clamp01(cDesc.fEvasion);
            m_bShield           = false;
            m_cSkillHandler.Initialize(cDesc.cSkillInfo, cDesc.iSkillLevel);

            // 풀에서 재사용되므로 지난 판의 효과가 남지 않게 전부 되돌린다.
            m_fEnvSpeedScale   = 1f;
            m_fSkillSpeedScale = 1f;
            m_fSkillSpeedTimer = 0f;
            m_fCardSpeedScale  = 1f;

            m_cSkillEffect = CSkillEffect.Create(cDesc.cSkillInfo != null ? cDesc.cSkillInfo.eType
                                                                         : SKILL_TYPE.NONE);
            if (m_cSkillEffect != null && m_cSkillEffect.Initialize(this) == true)
                m_cSkillEffect.Set_Host(m_cSkillHost);

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

            Tick_SkillSpeed(fDeltaTime);
            m_cSkillHandler.Tick(fDeltaTime);

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
            OnEvade         = null;
            OnDamaged       = null;
            GetEnemyCells   = null;
            m_cGrid         = null;

            base.Hide();
        }
        #endregion Engine.CGameObject

        #region 규칙 판정
        private STEP_RESULT Handle_ArriveCell(Vector2Int vCell)
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

            return eResult;
        }

        // 260904_거미줄 감속. 스테이지가 매 프레임 '지금 밟고 있는 칸'을 보고 넣어 준다.
        /// <param name="fScale"> 원래 속도에 곱할 값. 1이면 감속 없음. </param>
        public void Set_SpeedScale(float fScale)
        {
            m_fEnvSpeedScale = Mathf.Max(0.1f, fScale);
            Apply_Speed();
        }

        // 260912_질주. 환경 감속과 곱해지므로 거미줄 위에서 써도 둘 다 살아 있다.
        /// <param name="fScale"> 원래 속도에 곱할 값. 1.8이면 1.8배 </param>
        public void Add_SkillSpeed(float fScale, float fDuration)
        {
            if (fScale <= 0f || fDuration <= 0f)
                return;

            m_fSkillSpeedScale = fScale;
            m_fSkillSpeedTimer = fDuration;
            Apply_Speed();
        }

        // 260912_카드 효과. 판이 끝날 때까지 유지된다.
        /// <param name="fRatio"> 더할 비율. 0.15면 15% 빨라진다 </param>
        public void Add_CardSpeed(float fRatio)
        {
            if (fRatio <= 0f)
                return;

            m_fCardSpeedScale += fRatio;
            Apply_Speed();
        }

        /// <param name="fRatio"> 더할 회피 확률. 1을 넘지 않는다 </param>
        public void Add_Evasion(float fRatio)
        {
            if (fRatio <= 0f)
                return;

            m_fEvasion = Mathf.Clamp01(m_fEvasion + fRatio);
        }

        /// <summary> 잠깐 무적. 이미 더 길게 걸려 있으면 줄이지 않는다. </summary>
        public void Add_Invincible(float fSeconds)
        {
            if (fSeconds <= 0f)
                return;

            m_fInvincibleTimer = Mathf.Max(m_fInvincibleTimer, fSeconds);
        }

        /// <summary> 스킬이 몬스터처럼 플레이어 밖을 건드릴 때 쓰는 창구. 스테이지가 꽂아 준다. </summary>
        public void Set_SkillHost(ISkillHost cHost)
        {
            m_cSkillHost = cHost;
            m_cSkillEffect?.Set_Host(cHost);
        }

        private void Tick_SkillSpeed(float fDeltaTime)
        {
            if (m_fSkillSpeedTimer <= 0f)
                return;

            m_fSkillSpeedTimer -= fDeltaTime;
            if (m_fSkillSpeedTimer > 0f)
                return;

            m_fSkillSpeedScale = 1f;
            Apply_Speed();
        }

        private void Apply_Speed()
        {
            m_cMoveHandler.SPEED = m_fBaseSpeed * m_fEnvSpeedScale * m_fSkillSpeedScale * m_fCardSpeedScale;
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

            // 260905_보호막이 있으면 확정으로 한 번 막는다. 확률인 회피보다 먼저 쓴다 —
            // 회피가 먼저 터지면 아껴 둔 보호막이 그대로 남아 손해처럼 느껴진다.
            if (m_bShield == true)
            {
                m_bShield = false;
                m_fInvincibleTimer = EVADE_GRACE_TIME;
                OnEvade?.Invoke();
                return;
            }


            // 260905_회피(능력치 강화). 성공하면 짧은 무적을 함께 준다 —
            // 몬스터와 겹쳐 있는 동안 매 프레임 판정하면 확률이 아무리 높아도 결국 죽는다.
            if (m_fEvasion > 0f && UnityEngine.Random.value < m_fEvasion)
            {
                m_fInvincibleTimer = EVADE_GRACE_TIME;
                OnEvade?.Invoke();
                return;
            }

            m_cGrid.Clear_Trail();

            --m_iLife;
            OnDamaged?.Invoke();
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

        // 260905_액티브 스킬 — 워프
        /// <summary> 스킬 버튼이 눌렸을 때. 쿨타임이 남았거나 멈춰 있으면 아무 일도 없다. </summary>
        // 260912_무엇을 하는지는 CSkillEffect가 안다. 스킬이 늘어도 여기는 그대로다.
        // 효과가 실패하면 쿨타임을 돌리지 않는다 — 멈춘 채로 점멸을 눌러 쿨만 날리면 억울하다.
        public bool Try_UseSkill()
        {
            if (m_cGrid == null || m_iLife <= 0 || m_cSkillEffect == null)
                return false;

            if (m_cSkillHandler.IS_READY == false)
                return false;

            if (m_cSkillEffect.Try_Apply(m_cSkillHandler.VALUE, m_cSkillHandler.DURATION) == false)
                return false;

            m_cSkillHandler.Try_Use();
            return true;
        }

        // 한 칸씩 나아가며 평소 이동과 똑같은 규칙을 적용한다.
        // 한 번에 건너뛰지 않는 이유 — 지나간 칸이 선으로 남지 않으면
        // 도형이 끊겨 점령 판정이 깨진다.
        public bool Warp(int iCellCount)
        {
            MOVE_DIR eDir = m_cMoveHandler.CUR_DIR;
            if (eDir == MOVE_DIR.NONE || iCellCount <= 0)
                return false;   // 멈춰 있으면 어디로 갈지 알 수 없다

            Vector2Int vOffset = CTerritoryGrid.Dir_ToOffset(eDir);
            Vector2Int vCell   = m_cMoveHandler.CUR_CELL;
            bool bMoved = false;

            for (int i = 0; i < iCellCount; ++i)
            {
                Vector2Int vNext = vCell + vOffset;

                if (m_cGrid.Is_InBounds(vNext.x, vNext.y) == false || m_cGrid.Is_Blocked(vNext) == true)
                    break;

                vCell  = vNext;
                bMoved = true;

                m_cMoveHandler.Teleport(vCell, eDir);

                STEP_RESULT eResult = Handle_ArriveCell(vCell);
                if (eResult == STEP_RESULT.DEAD || eResult == STEP_RESULT.CAPTURE)
                    break;
            }

            if (bMoved == true)
                transform.position = m_cMoveHandler.WORLD_POS;

            return bMoved;
        }

        // 260912_마감 — 그은 선을 가장 가까운 점령지까지 이어 붙여 도형을 닫는다.
        // 점령 판정을 새로 만들지 않는다. 평소 이동과 똑같이 한 칸씩 밟아 Step_To를 지나게 해서,
        // 규칙이 한 군데(CTerritoryGrid.Step_To)에만 있도록 유지한다.
        public bool Seal()
        {
            if (m_cGrid == null || m_cGrid.IS_DRAWING == false)
                return false;   // 선을 긋고 있지 않으면 마감할 것이 없다

            Vector2Int vCur = m_cMoveHandler.CUR_CELL;
            if (m_cGrid.Try_Find_NearestCell(vCur, CELL_STATE.OWNED, SEAL_MAX_RADIUS,
                                             out Vector2Int vTarget) == false)
                return false;

            bool bMoved = false;

            // ㄱ자로 붙인다 — 가로를 먼저 맞추고 세로를 맞춘다.
            // 최단 경로를 찾지 않는 이유는, 막히면 그 자리에서 멈추고 실패로 돌려 주면 되기 때문이다.
            for (int i = 0; i < SEAL_MAX_STEP; ++i)
            {
                if (vCur == vTarget)
                    break;

                MOVE_DIR eDir = Pick_SealDir(vCur, vTarget);
                if (eDir == MOVE_DIR.NONE)
                    break;

                Vector2Int vNext = vCur + CTerritoryGrid.Dir_ToOffset(eDir);
                if (m_cGrid.Is_InBounds(vNext.x, vNext.y) == false || m_cGrid.Is_Blocked(vNext) == true)
                    break;

                vCur   = vNext;
                bMoved = true;

                m_cMoveHandler.Teleport(vCur, eDir);

                STEP_RESULT eResult = Handle_ArriveCell(vCur);
                if (eResult == STEP_RESULT.DEAD || eResult == STEP_RESULT.CAPTURE)
                    break;
            }

            if (bMoved == true)
                transform.position = m_cMoveHandler.WORLD_POS;

            return bMoved;
        }

        private static MOVE_DIR Pick_SealDir(Vector2Int vFrom, Vector2Int vTo)
        {
            if (vFrom.x != vTo.x)
                return vTo.x > vFrom.x ? MOVE_DIR.RIGHT : MOVE_DIR.LEFT;

            if (vFrom.y != vTo.y)
                return vTo.y > vFrom.y ? MOVE_DIR.UP : MOVE_DIR.DOWN;

            return MOVE_DIR.NONE;
        }


        // 260905_소모품 효과
        /// <summary> 보호막을 얻는다. 이미 있으면 그대로 둔다(중첩하지 않는다). </summary>
        public void Add_Shield() => m_bShield = true;

        /// <summary> 목숨을 회복한다. </summary>
        public void Heal(int iAmount)
        {
            if (iAmount <= 0 || m_iLife <= 0)
                return;

            m_iLife += iAmount;
            OnLifeChanged?.Invoke(m_iLife);
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
