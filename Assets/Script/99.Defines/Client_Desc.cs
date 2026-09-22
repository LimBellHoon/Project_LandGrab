using System;
using System.Collections.Generic;

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
        // 260923_시작 자리(그리드 공간). 땅이 다각형이라 칸이 아니라 점이다 — 시작 섬 경계 위
        public Vector2          vStartPos   { get; set; }
        public float            fMoveSpeed  { get; set; }
        // 260918_시작 목숨 수 (다시 목숨제, 2-14)
        public int              iLife       { get; set; }
        // 260905_능력치 강화 — 피격을 무시할 확률 0~1
        public float            fEvasion    { get; set; }
        // 260905_장착한 액티브 스킬. null이면 스킬 없음.
        public CSkillInfo       cSkillInfo  { get; set; }
        public int              iSkillLevel { get; set; }
        // 260920_캐릭터별 이동 방식(2-22)
        public MOVE_STYLE       eMoveStyle  { get; set; }

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

        // 260912_웨이브가 넘어갈 때 '이 종류가 몇 마리 있나'를 세려면 ID가 필요하다.
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
        // 260917_발사 패턴 (EnemyInfo.csv)
        public FIRE_PATTERN     eFirePattern    { get; set; }
        public int              iFireCount      { get; set; }
        public float            fFireAngle      { get; set; }
        public float            fFireInterval   { get; set; }
        // 260918_몬스터별 체력/공격력(EnemyInfo.csv). 0 이하로 두면 CEnemy가 기존 고정값으로 대체한다.
        public int              iHp             { get; set; }
        // 260923_다시 HP 풀이라 필요해진 몬스터별 공격력(2-14).
        public int              iAttack         { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cGrid = null;
        }
    }

    // 260904_몬스터 기믹이 소환하는 것들
    /// <summary> CProjectile 생성 Desc. 수치는 표(ProjectileInfo)에 있고, 셀 크기로 월드 환산은 본체가 한다. </summary>
    public class CProjectileDesc : CGameObjectDesc
    {
        // 260917_탄 한 종류(ProjectileInfo.csv 한 줄)를 통째로 넘긴다. 속도 · 사거리를 낱개로 넘기던 것을 대체한다.
        public CProjectileInfo                  cInfo       { get; set; }
        public IReadOnlyList<CImpactInfo>       lstImpact   { get; set; }
        public IProjectileHost                  cHost       { get; set; }
        public float                            fCellSize   { get; set; }
        public Vector2                          vStartPos   { get; set; }
        public Vector2                          vDir        { get; set; }
        public PROJECTILE_SIDE                  eSide       { get; set; }
        public IImpactTarget                    cOwner      { get; set; }
        // 260917_표의 크기에 곱하는 배율. 1이면 표 그대로
        public float                            fScale      { get; set; } = 1f;

        public override void OnReturn()
        {
            base.OnReturn();
            fScale    = 1f;
            cInfo     = null;
            lstImpact = null;
            cHost     = null;
            cOwner    = null;
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
        // 260921_상단 바의 캐릭터 그림 · 이름(2-23)
        public CCSVData_CharacterInfo cCharacterTable { get; set; }
        public LOBBY_TAB            eStartTab       { get; set; }
        public Action<LOBBY_TAB>    OnTabChanged    { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cProgress       = null;
            cCharacterTable = null;
            OnTabChanged    = null;
        }
    }

    /// <summary> 인벤토리. 안쪽 탭(장비/스킬/캐릭터)은 UI가 스스로 관리한다. </summary>
    public class CUI_InventoryDesc : CUIDesc
    {
        public CCSVData_EquipInfo     cEquipTable     { get; set; }
        // 260918_캐릭터 탭(스킨/레벨업 — 스테이지 진입 캐릭터도 여기서 바꾼다)이 쓴다.
        public CCSVData_CharacterInfo cCharacterTable { get; set; }
        public CProgress_Manager      cProgress       { get; set; }
        /// <summary> 장착 · 강화로 코인이나 장착 상태가 바뀌었을 때 </summary>
        public Action                 OnChanged       { get; set; }
        /// <summary> 260918_상세 · 뽑기 결과 팝업을 띄워 달라 — 화면을 여는 것은 CGameManager다(2-7) </summary>
        public Action<CUI_PopupDesc>  OnRequestPopup  { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cEquipTable     = null;
            cCharacterTable = null;
            cProgress       = null;
            OnChanged       = null;
            OnRequestPopup  = null;
        }
    }


    // 260918_카드 갤러리(로비의 별도 탭 — LOBBY_TAB.CARD). 장착 개념이 없어 가방과 따로 뺐다.
    /// <summary> 지금까지 웨이브를 깨서 드러낸 보상 이미지를 훑어보는 화면. 새 저장 데이터 없이 별 기록 + MapInfo를 읽는다. </summary>
    public class CUI_CardDesc : CUIDesc
    {
        // 260920_카드는 캐릭터에 딸려 있다(CharacterInfo.csv의 strCardTex, 2-17-2)
        public CCSVData_CharacterInfo cCharacterTable { get; set; }
        public CProgress_Manager cProgress { get; set; }
        // 260918_한 장을 누르면 크게 보기를 연다 — 화면을 여는 것은 CGameManager다(2-7)
        public Action<IReadOnlyList<CCardViewEntry>, int> OnOpenViewer { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cCharacterTable = null;
            cProgress       = null;
            OnOpenViewer    = null;
        }
    }

    // 260918_카드 크게 보기 한 장 — 어떤 그림을 무슨 이름으로 보여 줄지
    public class CCardViewEntry
    {
        public string strTexName;
        public string strCaption;
    }

    public class CUI_CardViewerDesc : CUIDesc
    {
        public IReadOnlyList<CCardViewEntry> lstEntry    { get; set; }
        public int                           iStartIndex { get; set; }
        public Action                        OnClose     { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            lstEntry = null;
            OnClose  = null;
        }
    }


    /// <summary> 상점. 목록은 EquipInfo.csv를 훑어 UI가 직접 만든다. </summary>
    public class CUI_ShopDesc : CUIDesc
    {
        public CCSVData_EquipInfo   cEquipTable { get; set; }
        // 260918_장비 뽑기(BM)는 상점에서만 한다. 없으면 뽑기 줄이 안 나온다.
        public CCSVData_GachaInfo   cGachaTable { get; set; }
        public CProgress_Manager    cProgress   { get; set; }
        /// <summary> 구매했을 때 — 로비의 재화 표시를 갱신하려고 쓴다. </summary>
        public Action               OnPurchased { get; set; }
        /// <summary> 260918_뽑기 결과 팝업을 띄워 달라 — 화면을 여는 것은 CGameManager다(2-7) </summary>
        public Action<CUI_PopupDesc> OnRequestPopup { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cEquipTable    = null;
            cGachaTable    = null;
            cProgress      = null;
            OnPurchased    = null;
            OnRequestPopup = null;
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
    // 260912_카드 3지선다
    public class CUI_CardPickDesc : CUIDesc
    {
        public string                   strTitle { get; set; }
        // 260917_카드와 런 스킬이 섞여 들어온다
        public IReadOnlyList<CPickOption> lstOption { get; set; }
        public Action<CPickOption>        OnPick    { get; set; }
        // 260921_다시 뽑기 · 버리기(2-10-1). 남은 횟수는 스테이지가 들고 있어 매번 물어본다
        /// <summary> 새 목록을 돌려준다. 횟수가 없으면 null </summary>
        public Func<IReadOnlyList<CPickOption>>                         OnReroll    { get; set; }
        /// <summary> (버릴 것, 지금 떠 있는 것) → 그 자리를 채울 새 선택지. 버리지 못했으면 null, 채울 것이 없으면 버린 것 자체 </summary>
        public Func<CPickOption, IReadOnlyList<CPickOption>, CPickOption> OnBanish  { get; set; }
        public Func<int>                                                  fnRerollLeft { get; set; }
        public Func<int>                                                  fnBanishLeft { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            lstOption = null;
            OnReroll  = null;
            OnBanish  = null;
            fnRerollLeft = null;
            fnBanishLeft = null;
            OnPick    = null;
        }
    }

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
        // 260905_소모품. 개수와 종류는 UI가 직접 읽고, 실제 사용은 CGameManager가 처리한다.
        public CProgress_Manager    cProgress   { get; set; }
        public CCSVData_EquipInfo   cEquipTable { get; set; }
        public Action               OnUseItem   { get; set; }
        // 260916_피격/회피 플래시 세기·지속시간을 읽으려고 넘긴다.
        public CGameConfig          cConfig     { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cPlayer = null;
            cStage  = null;
            cProgress   = null;
            cEquipTable = null;
            OnUseItem   = null;
            OnPause = null;
            cConfig = null;
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

    // 260916_런 스킬 '영혼 수집가'가 떨어뜨리는 픽업.
    /// <summary> CSoul 생성 Desc. 주우면 사라지고, 안 주워도 fLifeTime이 지나면 사라진다. </summary>
    public class CSoulDesc : CGameObjectDesc
    {
        public CTerritoryGrid   cGrid       { get; set; }
        public Vector2Int       vCell       { get; set; }
        public float            fLifeTime   { get; set; }   // 초

        public override void OnReturn()
        {
            base.OnReturn();
            cGrid = null;
        }
    }

    // 260921_분신(CDecoy) — 어그로만 끄는 허수아비. 그리드 규칙을 타지 않아 셀이 아니라 월드 좌표로 움직인다.
    public class CDecoyDesc : CGameObjectDesc
    {
        public CTerritoryGrid   cGrid       { get; set; }
        public Vector2          vStartPos   { get; set; }
        public Vector2          vDir        { get; set; }
        public float            fSpeed      { get; set; }   // 초당 셀
        public float            fLifeTime   { get; set; }   // 초

        public override void OnReturn()
        {
            base.OnReturn();
            cGrid = null;
        }
    }

    // 260920_맵 위 상호작용 아이템(CFieldItem). 영혼과 같은 모양에 '무엇인가'만 더 붙는다.
    public class CFieldItemDesc : CGameObjectDesc
    {
        public CTerritoryGrid   cGrid       { get; set; }
        public Vector2Int       vCell       { get; set; }
        public float            fLifeTime   { get; set; }   // 초
        public int              iItemID     { get; set; }
        public FIELD_ITEM_TYPE  eType       { get; set; }

        public override void OnReturn()
        {
            base.OnReturn();
            cGrid = null;
        }
    }
}
