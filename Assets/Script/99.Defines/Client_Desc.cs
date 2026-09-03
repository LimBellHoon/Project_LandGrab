using System;

using UnityEngine;

using Engine;

namespace Client
{
    // 260901_땅따먹기 프로토타입 정의
    /// <summary> 스테이지 1개의 규칙 정의 (추후 CSV/SO로 이관) </summary>
    [Serializable]
    public class CStageDesc
    {
        [Header("Grid")]
        public int      iGridWidth      = 60;       // 가로 셀 개수
        public int      iGridHeight     = 100;      // 세로 셀 개수
        public float    fCellSize       = 0.12f;    // 셀 1칸의 월드 크기
        public int      iBorderThick    = 2;        // 시작 시 점령되어 있는 외곽 테두리 두께(셀)

        [Header("Rule")]
        public float    fClearRatio     = 0.7f;     // 클리어에 필요한 점령률
        public float    fTimeLimit      = 180f;     // 제한 시간(초)
        public int      iLife           = 3;        // 목숨

        [Header("Player")]
        public float    fMoveSpeed      = 9f;       // 초당 이동 셀 수

        // 260902_몬스터
        [Header("Enemy")]
        public int      iEnemyCount     = 3;
        public float    fEnemySpeed     = 5f;       // 배회 속도 (초당 셀)
        public float    fEnemyChaseSpeed= 7f;       // 추적 속도 (초당 셀)
        public float    fEnemyTurnRate  = 5f;       // 추적 시 선회 속도 (초당 라디안)
        public float    fEnemyHitRange  = 1.2f;     // 플레이어와의 충돌 반경 (셀)

        // 260904_몬스터 기믹
        // 기믹은 플레이어가 땅을 먹으러 나와 있을 때만 발동한다 (선 위에서는 완전히 안전).
        // 몬스터 i번째가 가질 기믹. 배열이 짧으면 나머지는 NONE.
        // 웨이브 시스템이 들어오면 웨이브별로 이 배열을 갈아끼운다.
        public ENEMY_GIMMICK[] arrEnemyGimmick = { ENEMY_GIMMICK.PROJECTILE, ENEMY_GIMMICK.WEB, ENEMY_GIMMICK.SUMMON };

        [Header("Gimmick - Projectile")]
        public float    fProjectileCool  = 2.5f;    // 발사 쿨타임(초)
        public float    fProjectileSpeed = 8f;      // 초당 셀 — 플레이어(9)보다 느려야 피할 수 있다
        public float    fProjectileLife  = 5f;      // 최대 생존 시간(초)

        [Header("Gimmick - Web")]
        public float    fWebCool        = 5f;       // 거미줄 투사체 쿨타임(초)
        public float    fWebRadius      = 2f;       // 거미줄 반경(셀)
        public float    fWebDuration    = 6f;       // 거미줄 지속 시간(초)
        public float    fWebSlowRate    = 0.5f;     // 밟았을 때 이동 속도 배율
        public float    fWebSlowTime    = 1.5f;     // 감속 지속 시간(초)

        [Header("Gimmick - Summon")]
        public float    fSummonCool     = 6f;       // 소환 쿨타임(초)
        public int      iSummonMax      = 2;        // 소환자 1마리당 동시 생존 수
        public float    fMinionScale    = 0.6f;     // 미니 몬스터 크기 배율
        public float    fMinionSpeedRate= 1.4f;     // 미니 몬스터 속도 배율
    }

    /// <summary> CPlayer 생성 Desc — Engine 오브젝트 풀에 그대로 전달된다 </summary>
    public class CPlayerDesc : CGameObjectDesc
    {
        public CTerritoryGrid   cGrid       { get; set; }
        public Vector2Int       vStartCell  { get; set; }
        public float            fMoveSpeed  { get; set; }
        public int              iLife       { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cGrid = null;
        }
    }

    // 260902_몬스터
    /// <summary> CEnemy 생성 Desc </summary>
    public class CEnemyDesc : CGameObjectDesc
    {
        public CTerritoryGrid   cGrid       { get; set; }
        public Vector2Int       vStartCell  { get; set; }
        public Vector2          vStartDir   { get; set; }
        public float            fSpeed      { get; set; }   // 초당 셀
        public float            fChaseSpeed { get; set; }   // 초당 셀
        public float            fTurnRate   { get; set; }

        // 260904_몬스터 기믹
        public ENEMY_GIMMICK    eGimmick        { get; set; }
        public float            fGimmickCool    { get; set; }   // 기믹 쿨타임(초)
        public float            fScale          { get; set; }   // 크기 배율 (미니 몬스터는 작다). 0이면 1로 취급

        public override void OnReturn()
        {
            base.OnReturn();
            cGrid = null;
        }
    }

    // 260904_몬스터 기믹
    /// <summary> CProjectile 생성 Desc. bLeaveWeb이 true면 소멸 자리에 거미줄을 남긴다. </summary>
    public class CProjectileDesc : CGameObjectDesc
    {
        public CTerritoryGrid   cGrid       { get; set; }
        public Vector2          vStartPos   { get; set; }
        public Vector2          vDir        { get; set; }
        public float            fSpeed      { get; set; }   // 초당 셀
        public float            fLifeTime   { get; set; }
        public bool             bLeaveWeb   { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cGrid = null;
        }
    }

    /// <summary> CWeb 생성 Desc </summary>
    public class CWebDesc : CGameObjectDesc
    {
        public CTerritoryGrid   cGrid       { get; set; }
        public Vector2          vPos        { get; set; }
        public float            fRadius     { get; set; }   // 셀
        public float            fDuration   { get; set; }
        public float            fSlowRate   { get; set; }
        public float            fSlowTime   { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cGrid = null;
        }
    }
}
