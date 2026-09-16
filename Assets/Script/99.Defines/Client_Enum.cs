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

    // 260905_능력치 — 강화와 장비가 같은 것을 올리므로 enum 하나로 묶는다.
    /// <summary> 이름이 UpgradeInfo.csv / EquipInfo.csv의 열과 정확히 같아야 한다. </summary>
    public enum STAT_TYPE
    {
        NONE = 0,
        SPEED,      // 플레이어 이동 속도 배율
        EVASION,    // 피격 회피 확률
        HP,         // 260916_최대 체력 추가
    }

    // 260905_스킬
    /// <summary> 이름이 SkillInfo.csv의 eType 열과 정확히 같아야 한다. </summary>
    public enum SKILL_TYPE
    {
        NONE = 0,
        WARP,       // 진행 방향으로 순간 이동
        SWIFT,      // 패시브 — 이동 속도
        LUCKY,      // 패시브 — 회피

        // 260912_액티브 추가. 값은 뒤에만 붙인다 —
        // CStageProgress가 스킬 레벨을 이 숫자로 저장해 두어 순서를 바꾸면 옛 저장본이 어긋난다.
        SHIELD,     // 일정 시간 무적
        DASH,       // 일정 시간 이동 속도 상승
        SLOW,       // 일정 시간 몬스터 감속
        SEAL,       // 그은 선을 가장 가까운 점령지까지 이어 바로 마감
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
    /// <summary> 소모품이 주는 효과. 장비는 NONE. </summary>
    public enum CONSUME_EFFECT
    {
        NONE = 0,
        SHIELD,     // 다음 피격 1회 무효
        HEAL,       // 목숨 1 회복
    }

    // 260912_점령률을 넘길 때마다 셋 중 하나를 고르는 카드
    /// <summary> 고른 그 판 동안만 유지된다. 스테이지를 나가면 사라진다. </summary>
    public enum CARD_TYPE
    {
        NONE = 0,
        SHIELD,     // 피격 1회 무효 (즉시)
        HEAL,       // 목숨 1 회복 (즉시)
        SPEED,      // 이동 속도 상승
        EVASION,    // 회피 확률 상승
        SLOW,       // 몬스터 감속
    }



    // 260916_런 전용 스킬 — 스테이지 클리어로 해금되고, 스테이지 내 3지선다로 얻어 쌓는다.
    // (뱀서라이크 방식) 장비/강화 스킬(SKILL_TYPE)과는 완전히 별개의 표·핸들러를 쓴다 —
    // 저쪽은 '하나만 장착', 이쪽은 '여러 개를 동시에 들고 레벨업'이라 구조 자체가 다르다.
    /// <summary> 이름이 RunSkillInfo.csv의 eType 열과 정확히 같아야 한다. 뒤에만 추가할 것(저장 슬롯 없음 — 판마다 초기화되므로 순서 걱정은 없다). </summary>
    // 260917_비헤이비어 트리 (Portfolio_SoloLeveling에서 이식)
    /// <summary> 노드 한 번 평가의 결과. </summary>
    public enum NODE_STATE
    {
        RUNNING,    // 아직 진행 중 — 다음 프레임에 같은 노드를 이어서 평가한다
        SUCCESS,
        FAILURE,
    }

    /// <summary>
    /// 노드끼리 공유하는 작업 메모리의 키. 노드가 소유자를 구체 타입으로 캐스팅하지 않고도
    /// 상태를 읽고 쓰게 해 준다. 원본(SoloLeveling)은 3D 액션용 키였으므로 이 게임에 맞게 다시 골랐다.
    /// </summary>
    public enum BLACKBOARD_KEY
    {
        TARGET_POS,         // Vector2 : 노리는 위치 (보통 플레이어)
        DETECT_RANGE,       // float   : 감지 범위(셀)
        ATTACK_RANGE,       // float   : 공격 범위(셀)
        IS_ATTACKING,       // bool    : 공격 패턴 진행 중 — 중간에 끊지 않는다
        PATTERN_INDEX,      // float   : 지금 차례인 패턴 번호
        NEXT_PATTERN_TIME,  // float   : 다음 패턴을 시작해도 되는 누적 시간
        IS_SUPERARMOR,      // bool    : 피격 반응을 무시하는 중
    }

    public enum RUN_SKILL_TYPE
    {
        NONE = 0,
        MOONWALK,       // 월보 — 점령지 내부(이미지 위)도 통과 가능
        EDGE_WRAP,      // 어디로든 신발 — 좌우 끝에서 반대편으로 순간 이동
        SOUL_COLLECTOR, // 영혼 수집가 — 주울 때마다 그 판 한정 영구 스피드 증가
        RAGE,           // 분노조절못해 — 게이지가 차면 피버타임
        MAGNET,         // 자석 — 습득 범위 증가
        EVASION,        // 회피 — 회피율 증가
        ORBIT,          // 회전탄 — 주위를 도는 투사체, 적탄 상쇄 + 몬스터 피해
        CLUB,           // 몽둥이 — 바라보는 방향으로 휘둘러 넉백
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

    // 260916_효과음 종류. CAudio_Manager.Play(SOUND_ID)로 재생한다.
    /// <summary> 새 종류를 추가해도 여기 하나 늘리고 CAudio_Manager에 정의 한 줄만 더하면 된다(2-12). </summary>
    public enum SOUND_ID
    {
        NONE = 0,
        HIT,            // 피격
        DEATH,          // 사망
        EVADE,          // 회피 성공
        CAPTURE,        // 점령
        CARD_READY,     // 카드 3지선다 등장
        STAGE_CLEAR,    // 스테이지 클리어
        STAGE_FAIL,     // 스테이지 실패
    }

    // 260916_햅틱 종류. CHaptic_Manager.Play(HAPTIC_ID)로 재생한다.
    /// <summary> SOUND_ID와 같은 사건을 가리키지만 따로 둔다 — 소리는 나도 햅틱은 없는(또는 그 반대) 조합을 열어 두기 위해서다(2-13). </summary>
    public enum HAPTIC_ID
    {
        NONE = 0,
        HIT,
        DEATH,
        EVADE,
        CAPTURE,
        CARD_READY,
        STAGE_CLEAR,
        STAGE_FAIL,
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
