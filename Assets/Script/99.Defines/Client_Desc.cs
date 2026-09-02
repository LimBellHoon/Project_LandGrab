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

        public override void OnReturn()
        {
            base.OnReturn();
            cGrid = null;
        }
    }
}
