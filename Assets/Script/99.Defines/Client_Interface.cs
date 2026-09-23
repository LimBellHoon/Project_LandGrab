using System.Collections.Generic;

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
        // 260917_탄의 속도 · 사거리 · 수명은 이제 ProjectileInfo.csv 한 줄이 정한다.
        /// <param name="iProjectileID"> ProjectileInfo.csv의 ID </param>
        /// <param name="cOwner"> 쏜 몬스터. 궤도 · 부메랑 탄이 따라간다 </param>
        void Spawn_EnemyShot(int iProjectileID, Vector2 vPos, Vector2 vDir, IImpactTarget cOwner);

        void Spawn_Web(Vector2Int vCell, float fLifeTime, float fSlowRatio);

        /// <param name="iEnemyID"> EnemyInfo.csv의 ID </param>
        void Spawn_Minion(int iEnemyID, int iCount, Vector2 vPos);

        /// <summary> 260921_땅 갉는 자 — vCell 둘레의 내 땅 가장자리를 갉는다. 플레이어 발밑은 스테이지가 지켜 준다. </summary>
        /// <returns> 실제로 갉은 칸 수 </returns>
        int Gnaw_Territory(Vector2Int vCell, float fRange, int iCount);

        /// <summary> 기믹이 발동 조건을 판단할 때 쓴다 (플레이어가 안전 지대 밖인가). </summary>
        bool IS_PLAYER_EXPOSED { get; }
    }

    // 260912_스킬이 플레이어 밖을 건드려야 할 때 쓰는 창구
    /// <summary>
    /// 스킬 효과는 플레이어가 들고 있지만, 몬스터처럼 플레이어 밖에 있는 것을 건드려야 할 때가 있다.
    /// 기믹이 IGimmickHost로 스테이지에 요청만 하는 것과 같은 구조다 —
    /// 효과 모듈이 스테이지를 직접 알면 화면 없이 검증할 수 없어진다.
    /// </summary>
    public interface ISkillHost
    {
        /// <summary> 살아 있는 몬스터를 한동안 느리게 한다. </summary>
        /// <param name="fScale"> 원래 속도에 곱할 값. 1이면 감속 없음 </param>
        /// <param name="fDuration"> 초 </param>
        void Slow_Enemies(float fScale, float fDuration);

        // 260923_쾌속 돌진(RUSH) — 몬스터는 CPlayer/CMoveHandler가 모르는 대상이라 스테이지에 거리를 묻는다.
        // 맵 끝 · 내 점령지에서 서는 것은 기존 Warp(Step_To) 경로가 이미 하므로 여기서는 몬스터까지만 본다.
        /// <param name="vFromWorld"> 출발점(월드) </param>
        /// <param name="vDirWorld"> 정규화된 방향(월드) </param>
        /// <param name="fMaxDistWorld"> 스킬 수치가 정한 최대 거리(월드) </param>
        /// <returns> 길 위에 몬스터가 없으면 fMaxDistWorld 그대로. 있으면 처음 닿는 지점까지의 거리 </returns>
        float Get_RushDistance(Vector2 vFromWorld, Vector2 vDirWorld, float fMaxDistWorld);
    }

    // 260916_런 전용 스킬(2-11-1)이 플레이어 밖(맵 위)에 무언가를 놓아야 할 때 쓰는 창구.
    /// <summary>
    /// IGimmickHost/ISkillHost와 같은 이유다 — 소환물의 생성·수명·플레이어 충돌은
    /// CStage_Manager가 한곳에서 봐야 웨이브가 넘어갈 때 통째로 회수할 수 있다.
    /// </summary>
    public interface IRunSkillHost
    {
        /// <summary> 무작위 미점령 칸에 영혼 하나를 떨어뜨린다. </summary>
        void Spawn_Soul();

        // 260921_분신(CRunSkillEffect_Decoy) — 어그로만 끄는 허수아비. 생성 · 수명 · 어그로는 스테이지가 본다.
        /// <param name="vDir"> 달려 나갈 방향 </param>
        /// <param name="fDuration"> 살아 있는 시간(초) </param>
        /// <returns> 내보냈으면 true (프리팹이 없거나 이미 있으면 false) </returns>
        bool Spawn_Decoy(Vector2 vPos, Vector2 vDir, float fDuration);

        // 260917_투사체 무기(CRunSkillEffect_Weapon). 몬스터 목록과 탄 풀은 스테이지가 들고 있다.
        /// <summary> 조준할 몬스터. 살아 있는 몬스터가 없으면 null. </summary>
        IImpactTarget Find_Enemy(Vector2 vFrom, TARGET_FIND eFind);

        /// <summary> 플레이어 탄을 쏜다. 쏜 쪽은 플레이어다(부메랑이 돌아오고 궤도탄이 따라온다). </summary>
        /// <returns> 260917_만든 탄의 본체. 회전탄처럼 스킬이 직접 거둬야 하는 탄이 붙잡아 둔다. 실패하면 null </returns>
        /// <param name="fScale"> 260917_표의 크기에 곱한다. 몽둥이처럼 레벨이 판정 크기를 키우는 스킬이 쓴다 </param>
        CProjectileCore Spawn_PlayerShot(int iProjectileID, Vector2 vPos, Vector2 vDir, float fScale = 1f);

        // 260918_전체 마비 — 살아 있는 몬스터 전부를 fDuration초 세우고 화면 연출을 올린다.
        /// <returns> 세운 몬스터 수. 0이면 아무 일도 없었다(연출도 없다) </returns>
        int Stun_AllEnemies(float fDuration);
    }


    // 260917_투사체에 맞을 수 있는 대상 (플레이어 / 몬스터)
    /// <summary>
    /// GYM은 CMonster로 캐스팅해 효과를 걸었다(몬스터만 맞을 수 있었다).
    /// 여기선 적탄이 플레이어를, 플레이어 탄이 몬스터를 맞히므로 둘 다 이 창구로 받는다.
    /// </summary>
    public interface IImpactTarget
    {
        Vector2 POS { get; }
        /// <summary> 월드 단위 충돌 반경 </summary>
        float   HIT_RADIUS { get; }
        bool    IS_ALIVE { get; }
        /// <summary> 조준 대상 고르기(체력 많은 적)에 쓴다 </summary>
        int     HP { get; }
        /// <summary> 기절 · 감속 · 도트 · 번쩍임 타이머를 들고 있는 곳 </summary>
        CImpactHandler IMPACT { get; }

        void Take_Damage(int iAmount);

        /// <summary> 밀어낸다(월드 거리). 그리드를 따라 움직이는 플레이어처럼 밀릴 수 없는 대상은 무시한다. </summary>
        void Push(Vector2 vDir, float fDistance, float fDuration);
    }

    // 260917_탄이 자기 밖(맵 · 다른 탄)을 알아야 할 때 쓰는 창구
    /// <summary>
    /// 기믹 · 스킬 창구와 같은 이유다 — 탄이 스테이지를 직접 알면 화면 없이 검증할 수 없고,
    /// 새로 생긴 탄(폭발)을 스테이지가 회수할 수 없게 된다.
    /// </summary>
    public interface IProjectileHost
    {
        /// <summary> 이 자리가 탄에게 벽인가. 맵 밖은 항상 벽이고, 적탄에게는 점령지도 벽이다. </summary>
        bool Is_Wall(Vector2 vWorldPos, PROJECTILE_SIDE eSide);

        /// <summary> 맵 전체의 월드 영역. SWEEP이 끝까지 뻗는 길이를 잰다. </summary>
        Rect WORLD_BOUNDS { get; }

        /// <summary> 폭발 효과처럼 탄이 다른 탄을 부를 때. </summary>
        void Spawn_Projectile(int iProjectileID, Vector2 vPos, Vector2 vDir, PROJECTILE_SIDE eSide);

        /// <summary> 추적탄이 쫓을 대상. 적탄이면 플레이어, 플레이어 탄이면 가장 가까운 몬스터. 없으면 null. </summary>
        IImpactTarget Find_Target(Vector2 vFrom, PROJECTILE_SIDE eSide);

        // 260917_CHAIN 특성이 다음으로 튈 대상을 찾을 때 쓴다. Find_Target과 달리 반경 안으로 한정하고,
        // 이미 맞은 대상(hsExclude)은 다시 고르지 않는다 — 안 그러면 둘 사이를 왕복한다.
        /// <returns> 반경 안에서 hsExclude에 없는 가장 가까운 대상. 없으면 null. </returns>
        IImpactTarget Find_ChainTarget(Vector2 vFrom, PROJECTILE_SIDE eSide, float fRadius,
                                       ICollection<IImpactTarget> hsExclude);
    }

    // 260904_진행도 저장소
    /// <summary>
    /// 클리어 기록을 어디에 두는지를 게임 로직에서 떼어 놓는다.
    /// 지금은 로컬(CStageProgress_Local)뿐이지만, 뒤끝·Firebase 같은 백엔드를 붙일 때
    /// 이 인터페이스만 새로 구현하면 되고 스테이지/UI 코드는 손대지 않는다.
    ///
    /// 백엔드를 붙여도 로컬 구현은 남는다 — 모바일에서 통신이 끊겼다고 진행이 막히면 안 되므로
    /// 로컬을 먼저 읽고 나중에 동기화하는 형태가 된다.
    /// </summary>
    public interface IStageProgress
    {
        /// <summary> 저장된 기록을 읽어 온다. 없으면 빈 기록을 돌려준다. </summary>
        CStageProgress Load();

        void Save(CStageProgress cProgress);
    }
}
