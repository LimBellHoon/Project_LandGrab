using System;

using UnityEngine;

using Engine;

namespace Client
{
    // 260904_스테이지 규칙 값은 전부 MapInfo.csv로 옮겼다.
    // 같은 숫자를 인스펙터와 CSV 두 곳에 두면 어느 쪽이 진짜인지 알 수 없게 되므로
    // 여기에는 '어떤 맵을 띄울지'만 남긴다.
    /// <summary> 인스펙터에서 고르는 시작 맵. 실제 규칙은 MapInfo.csv가 갖는다. </summary>
    [Serializable]
    public class CStageDesc
    {
        [Header("MapInfo.csv의 iMapID")]
        public int iMapID = 1;
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

    // 260902_몬스터 / 260904_기믹 수치는 EnemyInfo.csv에서 들어온다
    /// <summary> CEnemy 생성 Desc </summary>
    public class CEnemyDesc : CGameObjectDesc
    {
        public CTerritoryGrid   cGrid       { get; set; }
        public Vector2Int       vStartCell  { get; set; }
        public Vector2          vStartDir   { get; set; }

        public int              iEnemyID        { get; set; }
        public ENEMY_GIMMICK    eGimmick        { get; set; }
        public float            fSpeed          { get; set; }   // 초당 셀
        public float            fChaseSpeed     { get; set; }   // 초당 셀
        public float            fTurnRate       { get; set; }
        public float            fHitRange       { get; set; }   // 셀
        public float            fGimmickCool    { get; set; }
        public float            fGimmickValue   { get; set; }
        public float            fGimmickRange   { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cGrid = null;
        }
    }
}
