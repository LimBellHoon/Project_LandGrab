using System;
using System.Collections.Generic;

namespace Client
{
    // 260917_비헤이비어 트리 — Portfolio_SoloLeveling에서 이식
    /// <summary>
    /// 트리의 한 노드. 들어갈 때(OnEnter) · 머무는 동안(OnUpdate) · 나올 때(OnExit)를 나눠 둔다.
    ///
    /// 원본과 달라진 점
    ///  · Animator / CActor 의존을 뺐다. 원본은 3D 액션 캐릭터 전용이라 노드가 소유자 타입을 알았는데,
    ///    여기서는 노드가 소유자를 모르고 필요한 것은 생성자에서 받는다(조건 · 행동을 대리자로 넘기는 식).
    ///  · Evaluate가 deltaTime을 받는다. 원본은 Time.deltaTime을 직접 읽어 화면 없이 검증할 수 없었다 —
    ///    이 프로젝트의 다른 모듈(CEnemyGimmick, CCameraShake)이 전부 Tick(dt)로 도는 것과 맞췄다.
    ///  · 복합 노드가 중단될 때 진행 중이던 자식까지 같이 중단한다(원본 Selector는 자신만 멈춰
    ///    진행 중이던 자식의 OnExit가 불리지 않았다).
    /// </summary>
    public abstract class CNode
    {
        private bool m_bRunning;

        public bool IS_RUNNING => m_bRunning;

        protected virtual void       OnEnter() { }
        protected virtual NODE_STATE OnUpdate(float fDeltaTime) => NODE_STATE.SUCCESS;
        protected virtual void       OnExit() { }

        public virtual NODE_STATE Evaluate(float fDeltaTime)
        {
            if (m_bRunning == false)
            {
                OnEnter();
                m_bRunning = true;
            }

            NODE_STATE eState = OnUpdate(fDeltaTime);

            // 끝났으면(성공/실패) 나오는 처리를 하고 다음 평가 때 처음부터 다시 들어오게 한다.
            if (eState != NODE_STATE.RUNNING)
            {
                OnExit();
                m_bRunning = false;
            }

            return eState;
        }

        /// <summary> 진행 중이던 노드를 밖에서 끊는다. 진행 중이 아니면 아무 일도 없다. </summary>
        public virtual void Abort()
        {
            if (m_bRunning == false)
                return;

            OnExit();
            m_bRunning = false;
        }
    }

    /// <summary> 자식을 앞에서부터 평가해, 하나라도 실패하지 않으면(성공/진행 중) 그 결과를 돌려준다. OR. </summary>
    public class CNode_Selector : CNode
    {
        private readonly List<CNode> m_lstChild;
        private CNode m_cRunningChild;      // 지난 평가에서 RUNNING이었던 자식

        public CNode_Selector(List<CNode> lstChild)
        {
            m_lstChild = lstChild ?? new List<CNode>();
        }

        // 원본과 같이 매 평가마다 앞에서부터 다시 본다 — 우선순위가 높은 자식이 끼어들 수 있어야 한다.
        public override NODE_STATE Evaluate(float fDeltaTime)
        {
            for (int i = 0; i < m_lstChild.Count; ++i)
            {
                CNode cChild = m_lstChild[i];
                NODE_STATE eResult = cChild.Evaluate(fDeltaTime);

                if (eResult == NODE_STATE.FAILURE)
                    continue;

                // 다른 자식이 끼어들었으면 하던 자식을 끊는다.
                if (m_cRunningChild != null && m_cRunningChild != cChild)
                    m_cRunningChild.Abort();

                m_cRunningChild = eResult == NODE_STATE.RUNNING ? cChild : null;
                return eResult;
            }

            m_cRunningChild = null;
            return NODE_STATE.FAILURE;
        }

        public override void Abort()
        {
            m_cRunningChild?.Abort();
            m_cRunningChild = null;
        }
    }

    /// <summary> 자식을 순서대로 끝까지 성공시켜야 성공한다. 하나라도 실패하면 즉시 실패. AND. </summary>
    public class CNode_Sequence : CNode
    {
        private readonly List<CNode> m_lstChild;
        private int m_iChildIndex;      // 지금 진행 중인 자식

        public CNode_Sequence(List<CNode> lstChild)
        {
            m_lstChild = lstChild ?? new List<CNode>();
        }

        protected override void OnEnter() => m_iChildIndex = 0;

        protected override NODE_STATE OnUpdate(float fDeltaTime)
        {
            while (m_iChildIndex < m_lstChild.Count)
            {
                NODE_STATE eResult = m_lstChild[m_iChildIndex].Evaluate(fDeltaTime);

                if (eResult == NODE_STATE.SUCCESS)
                {
                    ++m_iChildIndex;
                    continue;
                }

                return eResult;     // FAILURE 또는 RUNNING
            }

            return NODE_STATE.SUCCESS;
        }

        protected override void OnExit()
        {
            // 끊겨서 나오는 경우 진행 중이던 자식도 같이 끊는다.
            if (m_iChildIndex < m_lstChild.Count)
                m_lstChild[m_iChildIndex].Abort();
        }
    }

    // 원본은 조건 하나마다 클래스를 만들었다(CNode_Condition_IsDashInput 등). 이 게임의 몬스터 패턴은
    // 표(CSV)에서 조립될 예정이라 클래스를 늘리기보다 대리자를 받는 편이 맞다.
    /// <summary> 조건이 참이면 성공, 거짓이면 실패. 진행 중(RUNNING)은 돌려주지 않는다. </summary>
    public class CNode_Condition : CNode
    {
        private readonly Func<bool> m_fnCheck;

        public CNode_Condition(Func<bool> fnCheck) => m_fnCheck = fnCheck;

        protected override NODE_STATE OnUpdate(float fDeltaTime)
        {
            return m_fnCheck != null && m_fnCheck() == true ? NODE_STATE.SUCCESS : NODE_STATE.FAILURE;
        }
    }

    /// <summary> 행동을 대리자로 받는다. 들어갈 때 / 나올 때 처리도 따로 넘길 수 있다. </summary>
    public class CNode_Action : CNode
    {
        private readonly Func<float, NODE_STATE> m_fnUpdate;
        private readonly Action m_fnEnter;
        private readonly Action m_fnExit;

        public CNode_Action(Func<float, NODE_STATE> fnUpdate, Action fnEnter = null, Action fnExit = null)
        {
            m_fnUpdate = fnUpdate;
            m_fnEnter  = fnEnter;
            m_fnExit   = fnExit;
        }

        protected override void OnEnter() => m_fnEnter?.Invoke();
        protected override void OnExit()  => m_fnExit?.Invoke();

        protected override NODE_STATE OnUpdate(float fDeltaTime)
        {
            return m_fnUpdate != null ? m_fnUpdate(fDeltaTime) : NODE_STATE.SUCCESS;
        }
    }

    /// <summary> 정해 둔 시간만큼 RUNNING을 돌려준 뒤 성공한다. 패턴 사이 쉬는 틈에 쓴다. </summary>
    public class CNode_Wait : CNode
    {
        private readonly float m_fDuration;
        private float m_fElapsed;

        public CNode_Wait(float fDuration) => m_fDuration = fDuration;

        protected override void OnEnter() => m_fElapsed = 0f;

        protected override NODE_STATE OnUpdate(float fDeltaTime)
        {
            m_fElapsed += fDeltaTime;
            return m_fElapsed >= m_fDuration ? NODE_STATE.SUCCESS : NODE_STATE.RUNNING;
        }
    }
}
