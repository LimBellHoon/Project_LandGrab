using UnityEngine;

namespace Client
{
    // 260904_몬스터 기믹이 무언가를 소환할 때 쓰는 창구
    /// <summary>
    /// 투사체·거미줄·부하는 전부 스테이지가 소유해야 한다 — 수명 관리와 플레이어 충돌 판정이
    /// 한곳에 모여 있어야 웨이브가 넘어갈 때 통째로 회수할 수 있기 때문이다.
    /// 그래서 기믹은 직접 만들지 않고 이 창구로 요청만 한다. 구현은 CStage_Manager.
    /// </summary>
    public interface IGimmickHost
    {
        /// <param name="fSpeed"> 초당 셀 </param>
        /// <param name="fRange"> 셀. 이 거리를 날아가면 사라진다 </param>
        void Spawn_Projectile(Vector2 vPos, Vector2 vDir, float fSpeed, float fRange, float fLifeTime);

        void Spawn_Web(Vector2Int vCell, float fLifeTime, float fSlowRatio);

        /// <param name="iEnemyID"> EnemyInfo.csv의 ID </param>
        void Spawn_Minion(int iEnemyID, int iCount, Vector2 vPos);

        /// <summary> 기믹이 발동 조건을 판단할 때 쓴다 (플레이어가 안전 지대 밖인가). </summary>
        bool IS_PLAYER_EXPOSED { get; }
    }
}
