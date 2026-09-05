namespace Client
{
    // 260901_땅따먹기 프로토타입 정의
    /// <summary> 그리드 한 칸의 상태 </summary>
    public enum CELL_STATE : byte
    {
        EMPTY = 0,      // 미점령 (바깥 = 위험 지대)
        OWNED = 1,      // 점령 완료 (플레이어 안전 지대)
        TRAIL = 2,      // 점령 시도 중 남긴 선분

        // 260904_맵 모양 마스크로 잘라낸 '맵 밖' 칸.
        // 아무도 못 들어가고 점령률 분모에서도 빠진다 — 직사각형이 아닌 맵을 만들기 위한 것.
        BLOCK = 3,
    }

    /// <summary> 그리드 이동 방향 (4방향) </summary>
    public enum MOVE_DIR
    {
        NONE = -1,
        UP = 0,
        DOWN = 1,
        LEFT = 2,
        RIGHT = 3,
    }

    /// <summary> 셀에 도착했을 때 일어난 일 (CTerritoryGrid.Step_To의 결과) </summary>
    public enum STEP_RESULT
    {
        SAFE,       // 안전 지대 위를 이동
        DRAW,       // 미점령 지대에 선분을 남김
        CAPTURE,    // 도형이 닫혀 영토를 점령
        DEAD,       // 자기 선분을 밟음
    }

    // 260905_재화로 올리는 능력치
    /// <summary>
    // 260905_능력치 — 강화와 장비가 같은 것을 올리므로 enum 하나로 묶는다.
    /// <summary> 이름이 UpgradeInfo.csv / EquipInfo.csv의 열과 정확히 같아야 한다. </summary>
    public enum STAT_TYPE
    {
        NONE = 0,
        SPEED,      // 플레이어 이동 속도 배율
        EVASION,    // 피격 회피 확률
        HP,         // 시작 목숨 추가
    }

    // 260905_스킬
    /// <summary> 이름이 SkillInfo.csv의 eType 열과 정확히 같아야 한다. </summary>
    public enum SKILL_TYPE
    {
        NONE = 0,
        WARP,       // 진행 방향으로 순간 이동
    }

    /// <summary> 액티브는 버튼을 눌러야 발동하고, 패시브는 장착만 해도 걸린다. </summary>
    public enum SKILL_CATEGORY
    {
        ACTIVE = 0,
        PASSIVE,
    }

    // 260905_장비
    /// <summary> 슬롯당 하나씩 착용한다. 소모품은 개수로 갖고 있다가 쓴다. </summary>
    public enum EQUIP_SLOT
    {
        NONE = 0,
        SHOES,          // 신발
        BAG,            // 가방
        NECKLACE,       // 목걸이
        CONSUMABLE,     // 1회성 소모품 (방어막 / 포션)
    }

    /// <summary> 인벤토리 창 안의 탭. 스킬은 한 개만 장착한다. </summary>
    public enum INVENTORY_TAB
    {
        EQUIP = 0,
        SKILL,
    }

    // 260905_로비 하단 탭
    /// <summary> 선언 순서가 그대로 탭 버튼 순서다 (CUI_Lobby의 배열 인덱스). </summary>
    public enum LOBBY_TAB
    {
        BATTLE = 0,
        UPGRADE,
        INVENTORY,
        SHOP,
    }

    /// <summary> 스테이지 진행 상태 </summary>
    public enum STAGE_STATE
    {
        READY,
        PLAYING,
        CLEAR,
        FAIL,
    }

    // 260904_몬스터 기믹 (EnemyInfo.csv의 eGimmick 열)
    /// <summary>
    /// 기믹별 동작은 CEnemy를 상속해 붙인다.
    /// 수치는 EnemyInfo.csv의 fGimmickCool / fGimmickValue / fGimmickRange로 들어온다.
    /// </summary>
    public enum ENEMY_GIMMICK
    {
        NONE = 0,       // 기본 — 배회하다 플레이어가 나오면 추적
        WEB,            // 거미줄 — 밟은 플레이어를 느리게 (fGimmickValue = 속도 배율)
        PROJECTILE,     // 투사체 — 플레이어를 향해 발사 (fGimmickValue = 탄속)
        SPAWN,          // 부하 소환 (fGimmickValue = 소환 마리수)
    }

    /// <summary> Addressable 라벨 (Engine.CData_Manager.LoadAssetAsync 인자) </summary>
    public static class CAddressableLabel
    {
        public const string PREFAB = "Prefabs";
        public const string TEXTURE = "Images";
        // 260904_CSV 테이블 라벨. 이 라벨이 붙은 TextAsset은 Engine이 파일명으로
        // Client.CCSVData_<파일명> 클래스를 찾아 자동으로 파싱해 캐싱한다.
        public const string CSV = "CSV";
    }
}
