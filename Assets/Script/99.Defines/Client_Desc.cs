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
        // 260905_능력치 강화 — 피격을 무시할 확률 0~1
        public float            fEvasion    { get; set; }
        // 260905_장착한 액티브 스킬. null이면 스킬 없음.
        public CSkillInfo       cSkillInfo  { get; set; }

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

    // 260905_로비 (하단 탭바)
    /// <summary> 탭을 고르면 OnTabChanged로 알리고, 무엇을 띄울지는 CGameManager가 정한다. </summary>
    public class CUI_LobbyDesc : CUIDesc
    {
        public CProgress_Manager    cProgress       { get; set; }
        public LOBBY_TAB            eStartTab       { get; set; }
        public Action<LOBBY_TAB>    OnTabChanged    { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cProgress    = null;
            OnTabChanged = null;
        }
    }

    /// <summary> 능력치 강화 화면. 목록은 UpgradeInfo.csv를 훑어 UI가 직접 만든다. </summary>
    public class CUI_UpgradeDesc : CUIDesc
    {
        public CCSVData_UpgradeInfo cUpgradeTable   { get; set; }
        public CProgress_Manager    cProgress       { get; set; }
        /// <summary> 강화를 샀을 때 — 로비의 재화 표시를 갱신하려고 쓴다. </summary>
        public Action               OnPurchased     { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cUpgradeTable = null;
            cProgress     = null;
            OnPurchased   = null;
        }
    }

    // 260904_공용 팝업 — 일시정지와 결과 화면이 같은 프리팹을 쓴다
    /// <summary>
    /// 제목 · 본문 · 버튼 두 개가 전부인 팝업. 무엇을 보여줄지는 전부 이 Desc가 정한다.
    /// 화면마다 클래스를 새로 만들지 않으려는 것 — 팝업이 늘어도 프리팹은 하나면 된다.
    /// </summary>
    public class CUI_PopupDesc : CUIDesc
    {
        public string   strTitle        { get; set; }
        public string   strBody         { get; set; }
        /// <summary> 오른쪽(주) 버튼. 비우면 '확인'이 들어간다. </summary>
        public string   strPrimary      { get; set; }
        /// <summary> 왼쪽(보조) 버튼. 비우면 버튼 자체를 숨긴다. </summary>
        public string   strSecondary    { get; set; }

        public Action   OnPrimary       { get; set; }
        public Action   OnSecondary     { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            OnPrimary   = null;
            OnSecondary = null;
        }
    }

    /// <summary>
    /// 인게임 HUD. 조이스틱과 진행 상황을 그리는 데 필요한 것만 넘긴다 —
    /// 싱글턴을 타고 들어가면 스테이지가 없는 순간에 터지기 때문이다.
    /// </summary>
    public class CUI_InGameDesc : CUIDesc
    {
        public CPlayer          cPlayer { get; set; }
        public CStage_Manager   cStage  { get; set; }
        /// <summary> 일시정지 버튼을 눌렀을 때 </summary>
        public Action           OnPause { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cPlayer = null;
            cStage  = null;
            OnPause = null;
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
