using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260918_비헤이비어 트리(2-16) 첫 연결 — 포수류(EnemyInfo.ENEMY_GIMMICK.PROJECTILE) 전용 "사거리 유지" 패턴.
    /// <summary>
    /// 예전엔 모든 몬스터가 CStage_Manager가 넘기는 노출 여부를 그대로 배회/추적으로 썼다 —
    /// 포수도 몸통 박치기 거리까지 붙었다가 쐈다. 이제는 사거리(EnemyInfo.fGimmickRange) 밖이면
    /// 다가서고, 안이면 자리를 지킨다 — 이동 규칙(CEnemyMoveHandler)은 그대로 두고
    /// '언제 쫓을지'만 트리가 정한다.
    ///
    /// 노드는 소유자를 모르는 게 원칙(CNode.cs 참고)이지만, 여기서는 CEnemy가 트리를 직접 소유하므로
    /// 빌더가 CEnemy.Set_MoveState를 대리자로 그대로 꽂는다 — CEnemyGimmick이 IGimmickHost로
    /// 창구만 받는 것과 같은 결이다.
    /// </summary>
    public static class CEnemyBehaviorTree_Utility
    {
        public static CNode Build_Kite(CEnemy cOwner, CBlackboard cBlackboard, float fCellSize)
        {
            CNode cChase = new CNode_Action(fDeltaTime =>
            {
                cOwner.Set_MoveState(true, cBlackboard.Get_Vector(BLACKBOARD_KEY.TARGET_POS));
                return NODE_STATE.SUCCESS;
            });

            // 사거리 안이거나 플레이어가 안전 지대에 있으면 굳이 다가서지 않는다 — 기믹은 노출 여부와
            // 사거리를 스스로 다시 확인하므로(CEnemyGimmick.Can_Fire) 여기서 쏠지 말지까지 정할 필요는 없다.
            CNode cHold = new CNode_Action(fDeltaTime =>
            {
                cOwner.Set_MoveState(false, cBlackboard.Get_Vector(BLACKBOARD_KEY.TARGET_POS));
                return NODE_STATE.SUCCESS;
            });

            CNode cChaseIfFar = new CNode_Sequence(new List<CNode>
            {
                new CNode_Condition(() =>
                {
                    if (cBlackboard.Get_Bool(BLACKBOARD_KEY.IS_TARGET_EXPOSED) == false)
                        return false;

                    float fDistance = Vector2.Distance(cOwner.POS, cBlackboard.Get_Vector(BLACKBOARD_KEY.TARGET_POS));
                    return fDistance > cBlackboard.Get_Float(BLACKBOARD_KEY.ATTACK_RANGE) * fCellSize;
                }),
                cChase,
            });

            return new CNode_Selector(new List<CNode> { cChaseIfFar, cHold });
        }
    }
}
