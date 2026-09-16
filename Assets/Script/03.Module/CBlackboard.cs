using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260917_비헤이비어 트리 — Portfolio_SoloLeveling에서 이식
    /// <summary>
    /// 노드들이 공유하는 작업 메모리. 노드가 소유자를 구체 타입으로 캐스팅하지 않고도 상태를 읽고 쓴다.
    ///
    /// 원본과 달라진 점
    ///  · Has / Remove / Clear가 참조형 칸만 보고 있어, bool/float로 넣은 값은 지워지지 않았다.
    ///    세 칸을 모두 보도록 고쳤다 — 풀에서 재사용되는 몬스터가 지난 판의 값을 들고 나오면 안 된다.
    ///  · Vector2 칸을 따로 뒀다. 이 게임의 대상은 Transform이 아니라 좌표다(몬스터·탄이 그리드 좌표로 움직인다).
    /// </summary>
    public class CBlackboard
    {
        private readonly Dictionary<BLACKBOARD_KEY, bool>    m_dicBool    = new Dictionary<BLACKBOARD_KEY, bool>();
        private readonly Dictionary<BLACKBOARD_KEY, float>   m_dicFloat   = new Dictionary<BLACKBOARD_KEY, float>();
        private readonly Dictionary<BLACKBOARD_KEY, Vector2> m_dicVector  = new Dictionary<BLACKBOARD_KEY, Vector2>();
        private readonly Dictionary<BLACKBOARD_KEY, object>  m_dicRef     = new Dictionary<BLACKBOARD_KEY, object>();

        public void Set(BLACKBOARD_KEY eKey, bool bValue)    => m_dicBool[eKey]   = bValue;
        public void Set(BLACKBOARD_KEY eKey, float fValue)   => m_dicFloat[eKey]  = fValue;
        public void Set(BLACKBOARD_KEY eKey, Vector2 vValue) => m_dicVector[eKey] = vValue;
        public void Set_Ref<T>(BLACKBOARD_KEY eKey, T cValue) where T : class => m_dicRef[eKey] = cValue;

        public bool    Get_Bool(BLACKBOARD_KEY eKey)   => m_dicBool.TryGetValue(eKey, out bool bValue) && bValue;
        public float   Get_Float(BLACKBOARD_KEY eKey)  => m_dicFloat.TryGetValue(eKey, out float fValue) ? fValue : 0f;
        public Vector2 Get_Vector(BLACKBOARD_KEY eKey) => m_dicVector.TryGetValue(eKey, out Vector2 vValue) ? vValue : Vector2.zero;
        public T       Get_Ref<T>(BLACKBOARD_KEY eKey) where T : class
            => m_dicRef.TryGetValue(eKey, out object cValue) ? cValue as T : null;

        public bool Has(BLACKBOARD_KEY eKey)
        {
            return m_dicBool.ContainsKey(eKey) || m_dicFloat.ContainsKey(eKey)
                || m_dicVector.ContainsKey(eKey) || m_dicRef.ContainsKey(eKey);
        }

        public void Remove(BLACKBOARD_KEY eKey)
        {
            m_dicBool.Remove(eKey);
            m_dicFloat.Remove(eKey);
            m_dicVector.Remove(eKey);
            m_dicRef.Remove(eKey);
        }

        public void Clear()
        {
            m_dicBool.Clear();
            m_dicFloat.Clear();
            m_dicVector.Clear();
            m_dicRef.Clear();
        }
    }

    // 260917_트리 하나를 들고 매 프레임 평가한다.
    /// <summary> 원본과 같이 루트 하나만 들고 있다. 소유자가 Tick을 불러 준다. </summary>
    public class CBehaviorTreeHandler
    {
        private CNode m_cRoot;
        private readonly CBlackboard m_cBlackboard = new CBlackboard();

        public CBlackboard BLACKBOARD => m_cBlackboard;
        public bool        HAS_TREE   => m_cRoot != null;

        /// <summary> 트리를 갈아 끼운다. 하던 노드는 끊고, 블랙보드는 비운다. </summary>
        public void Set_Tree(CNode cRoot)
        {
            Release();
            m_cRoot = cRoot;
        }

        public NODE_STATE Tick(float fDeltaTime)
        {
            return m_cRoot != null ? m_cRoot.Evaluate(fDeltaTime) : NODE_STATE.FAILURE;
        }

        /// <summary> 풀로 돌아갈 때 부른다. 지난 판의 진행 상태와 메모리가 남지 않게 한다. </summary>
        public void Release()
        {
            m_cRoot?.Abort();
            m_cRoot = null;
            m_cBlackboard.Clear();
        }
    }
}
