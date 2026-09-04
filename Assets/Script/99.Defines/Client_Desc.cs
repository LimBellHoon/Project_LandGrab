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
        public float            fGimmickDuration{ get; set; }
        public int              iGimmickRefID   { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cGrid = null;
        }
    }

    // 260904_몬스터 기믹이 소환하는 것들
    /// <summary> CProjectile 생성 Desc. 속도·사거리는 셀 단위로 받아 안에서 월드로 환산한다. </summary>
    public class CProjectileDesc : CGameObjectDesc
    {
        public CTerritoryGrid   cGrid       { get; set; }
        public Vector2          vStartPos   { get; set; }
        public Vector2          vDir        { get; set; }
        public float            fSpeed      { get; set; }   // 초당 셀
        public float            fLifeTime   { get; set; }   // 초
        public float            fMaxRange   { get; set; }   // 셀. 0 이하면 수명까지 날아간다
        public float            fHitRange   { get; set; }   // 셀

        public override void OnReturn()
        {
            base.OnReturn();
            cGrid = null;
        }
    }

    // 260904_스테이지 선택 UI
    /// <summary>
    /// CUIDesc는 Engine에서 CGameObjectDesc를 상속하므로 strPrefabName / eObjectType을 그대로 쓴다.
    /// 표와 진행도를 넘겨 UI가 스스로 목록을 그리게 한다.
    /// </summary>
    public class CUI_StageSelectDesc : CUIDesc
    {
        public CCSVData_MapInfo     cMapTable   { get; set; }
        public CProgress_Manager    cProgress   { get; set; }
        /// <summary> 맵을 고르면 그 iMapID를 넘긴다. </summary>
        public Action<int>          OnSelect    { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cMapTable = null;
            cProgress = null;
            OnSelect  = null;
        }
    }

    /// <summary> CWeb 생성 Desc. 밟은 플레이어를 fSlowRatio 배로 느리게 만든다. </summary>
    public class CWebDesc : CGameObjectDesc
    {
        public CTerritoryGrid   cGrid       { get; set; }
        public Vector2Int       vCell       { get; set; }
        public float            fLifeTime   { get; set; }   // 초
        public float            fSlowRatio  { get; set; }   // 1이면 감속 없음

        public override void OnReturn()
        {
            base.OnReturn();
            cGrid = null;
        }
    }
}
