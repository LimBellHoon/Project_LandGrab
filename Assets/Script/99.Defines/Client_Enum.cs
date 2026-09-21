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

    /// <summary>
    /// 그리드 이동 방향. 0~3이 4방향이고, 260920_대각선 넷을 뒤에 붙였다.
    /// **앞 넷의 순서는 바꾸지 말 것** — CTerritoryGrid의 플러드필이 0~3만 돌며 4방향 연결을 본다.
    /// </summary>
    public enum MOVE_DIR
    {
        NONE = -1,
        UP = 0,
        DOWN = 1,
        LEFT = 2,
        RIGHT = 3,

        // 260920_캐릭터별 이동 방식(2-22). 대각선은 가로 한 칸 + 세로 한 칸으로 밟는다 —
        // 그래야 점령 판정(4방향 연결)이 그대로 성립한다.
        UP_LEFT = 4,
        UP_RIGHT = 5,
        DOWN_LEFT = 6,
        DOWN_RIGHT = 7,
    }

    /// <summary> 260920_GameConfig에서 이동 방식을 덮어쓸 때 쓰는 개발용 선택지. NONE이면 캐릭터 표를 따른다(1-6). </summary>
    public enum DEV_MOVE_STYLE
    {
        NONE = 0,
        FOUR_WAY,
        EIGHT_WAY,
        SPIRAL,         // 260921_나선형도 캐릭터와 무관하게 확인할 수 있게
    }

    // 260920_캐릭터마다 다른 이동 방식 (CharacterInfo.csv의 eMoveStyle, 2-22)
    public enum MOVE_STYLE
    {
        FOUR_WAY = 0,   // 기본 — 상하좌우
        EIGHT_WAY,      // 대각선까지. 한 번에 두 칸을 밟으므로 그만큼 빠르다 → 캐릭터 속도 배율을 낮게 잡는다
        // 260921_나선형 — 멈추지 않고 계속 전진하며, 입력이 없으면 일정 칸마다 같은 쪽으로 꺾어 소용돌이를 그린다.
        // 입력은 '어디로 갈지'가 아니라 '언제 꺾을지'가 된다 — 조작 방식 자체가 다른 캐릭터다(2-19 설계).
        SPIRAL,
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
        HP,         // 260916_최대 체력 추가 → 260918_다시 목숨제라 '목숨 수'다(이름은 저장본 호환 때문에 그대로)
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

    // 260920_카드 화면(로비 탭)의 서브 탭 (2-17-2)
    public enum CARD_TAB
    {
        CHARACTER = 0,  // 캐릭터별 모음 — 그림 + 이름. 못 가진 캐릭터도 어둡게 보여 준다
        GALLERY,        // 지금까지 연 카드를 전부 편다
    }

    // 260920_3지선다 카드의 색 테마. 이름이 CardInfo.csv / RunSkillInfo.csv의 eTheme 열과 같아야 한다.
    /// <summary>
    /// 카드 레이아웃은 전부 같고 **색만 다르다**(2-10-1). 각성은 여기 없다 —
    /// 각성은 PICK_KIND로 갈리고 카드 전체가 보라색이 된다.
    /// </summary>
    public enum PICK_THEME
    {
        COMBAT = 0,     // 전투 — 파랑 (무기 · 피해 · 방어처럼 싸움에 쓰는 것)
        MOVE,           // 이동 — 주황 (속도 · 회피 · 이동 규칙처럼 움직임에 쓰는 것)
    }

    // 260917_3지선다 한 칸의 종류. 카드와 런 스킬을 한 화면에 섞는다
    public enum PICK_KIND
    {
        CARD,           // CardInfo.csv — 보호막 · 회복 · 가속 · 회피 · 둔화
        RUN_SKILL,      // RunSkillInfo.csv — 새로 얻거나 레벨업
        AWAKEN,         // AwakenInfo.csv — 만렙 액티브 + 짝 패시브 → 각성 (Docs/Design_RunSkill_Awaken.md)
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
    // 260917_투사체 — Project_GYM의 BulletLogic 구조를 이식
    /// <summary> 누가 쏜 탄인가. 적탄은 플레이어를, 플레이어 탄은 몬스터를 맞힌다. </summary>
    public enum PROJECTILE_SIDE
    {
        ENEMY_SHOT,
        PLAYER_SHOT,
    }

    /// <summary>
    /// 탄의 판정 모양. GYM은 모양마다 프리팹과 하위 클래스(CBullet_Laser 등)를 따로 뒀는데,
    /// 이 프로젝트는 프리팹 하나 + 표 한 줄 원칙(2-6)이라 모양도 모듈로 바꿨다.
    /// </summary>
    public enum PROJECTILE_SHAPE
    {
        POINT,      // 원 — 일반 탄
        LASER,      // 가는 예고선 → 두꺼운 빔 → 사라짐. 사각형 (GYM CBullet_Laser)
        SWEEP,      // 가로로 맵 끝까지 뻗어 나가는 띠 (GYM CBullet_Horizontal)
        BLAST,      // 점점 커지는 원. 커지는 동안만 맞는다 (GYM CBullet_Explosion)
    }

    /// <summary> 탄 하나가 어떻게 움직이는가. 단일 선택. (GYM BulletLogic_Movement) </summary>
    public enum PROJECTILE_MOVE
    {
        NONE,       // 제자리
        STRAIGHT,   // 직진 (GYM Normal)
        TRACE,      // 대상을 향해 휜다 (GYM Trace는 이름만 추적형이고 직진이었다 — 여기선 실제로 휜다)
        SPIRAL,     // 나선
        BOOMERANG,  // 나갔다 멈췄다 쏜 쪽으로 돌아온다
        ORBIT,      // 쏜 쪽 주위를 돈다 (GYM CirclePattern)
        SYNC,       // 쏜 쪽 위치에 붙어 다닌다 (GYM SyncPlayer)
    }

    /// <summary> 탄에 얹는 특성. 복수 선택. (GYM BulletLogic_Trait) </summary>
    public enum PROJECTILE_TRAIT
    {
        NONE,
        REBOUND,            // 벽에 맞으면 튕긴다
        GRAVITY_PULL,       // 닿아 있는 동안 끌어당긴다 (인력)
        GRAVITY_PUSH,       // 닿아 있는 동안 밀어낸다 (척력)
        KNOCKBACK_PULL,     // 닿는 순간 탄 쪽으로 당긴다
        KNOCKBACK_PUSH,     // 닿는 순간 진행 방향으로 날린다
        ENTER_STUN,         // 닿는 순간 잠깐 기절
        STAY_STUN,          // 닿아 있는 동안 기절 (속박)
        HIT_STOP,           // 닿는 순간 탄이 잠깐 멈춘다 (타격감)
        SCALE_OVER_TIME,    // 시간이 지날수록 커진다
        STAY_STOP,          // 닿아 있는 동안 느려진다
        WHITE_OUT,          // 닿아 있는 동안 하얗게 빛난다
        RANDOM,             // 함께 적힌 특성 중 하나만 무작위로 남긴다
        // 260917_체인 라이트닝. 닿으면 기절시키고 반경 안의 아직 안 맞은 대상 쪽으로 방향을 튼다.
        // 몇 번 튈지는 REBOUND와 같은 자리(iDurability)가 정한다 — 새 상수를 만들지 않는다.
        CHAIN,
    }

    /// <summary> 맞은 대상에게 남는 효과. (GYM CImpact) </summary>
    public enum IMPACT_TYPE
    {
        NONE,
        STUN,       // 기절
        SLOW,       // 감속
        DOT,        // 초마다 피해
        KNOCKBACK,  // 탄에서 먼 쪽으로 밀림
        EXPLODE,    // 그 자리에 다른 탄을 터뜨린다 (iRefID)
    }

    /// <summary>
    /// 한 번에 몇 발을 어떤 모양으로 뿌리는가. 탄 자체의 성질이 아니라 쏘는 쪽의 성질이다
    /// (GYM은 SkillInfo 한 줄에 섞여 있었다 — M4 기획서 1-2의 결정대로 나눴다).
    /// </summary>
    public enum FIRE_PATTERN
    {
        SINGLE,     // 조준 방향으로 한 발
        SPREAD,     // 부채꼴로 여러 발 (GYM 방사형)
        RING,       // 360도로 고르게 (8방향 등)
        BURST,      // 한 방향으로 시간차를 두고 여러 발 (연발)
        SPIN,       // RING을 쏠 때마다 각도를 돌린다 (CW/CCW)
    }

    /// <summary> 플레이어 탄이 누구를 노리는가. (GYM ExploreType) </summary>
    public enum TARGET_FIND
    {
        NEAREST,        // 가장 가까운 몬스터
        HIGHEST_HP,     // 체력이 가장 많은 몬스터
        CROWDED,        // 몬스터가 가장 몰려 있는 곳
    }

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
        // 260918_첫 연결(CEnemyBehaviorTree_Utility.Build_Kite, 2-16)에서 추가.
        IS_TARGET_EXPOSED,  // bool    : 노리는 대상(플레이어)이 지금 위협 가능한 상태인가(안전 지대 밖)
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

        // 260917_투사체 무기 — 일정 시간마다 저절로 쏜다(뱀서라이크). 효과는 전부 CRunSkillEffect_Weapon 하나이고
        // 무엇을 어떻게 쏠지는 RunSkillInfo.csv의 탄 ID · 쿨 · 발사 패턴 · 조준 열이 정한다.
        MAGIC_BOLT,     // 마법탄 — 가장 가까운 몬스터에게 유도탄 연발
        LASER_BEAM,     // 레이저 — 체력이 가장 많은 몬스터 쪽으로 레이저
        BOOMERANG,      // 부메랑 — 몬스터가 몰린 쪽으로 던졌다 돌아온다
        BOUNCE_SHOT,    // 튕기는 탄 — 사방으로 뿌리고 맵 끝에서 튕긴다

        // 260918_뱀서라이크가 아니고 목숨제라 '적을 잠깐 세우는' 수단이 핵심이다 — 죽이는 것보다 비키게 한다.
        STUN_SHOT,      // 마비탄 — 가장 가까운 적에게 주기적으로 마비탄 (CRunSkillEffect_Weapon)
        // 260920_전체 마비는 런 스킬에서 뺐다 — 맵 위에서 주워 쓰는 필드 아이템(FIELD_ITEM_TYPE.MASS_STUN)이 됐다.

        // 260921_이동 스킬 네 종(2-11-3). 이 게임 고유의 동사(선 긋기 · 왕복 · 어그로)를 쓴다.
        SPIRAL_RUSH,    // 나선 가속 — 선이 길어질수록 빨라진다
        GHOST_STEP,     // 유령 걸음 — 선을 긋기 시작하면 잠깐 통과 상태
        AFTERIMAGE,     // 잔상 — 방향을 꺾는 순간 짧게 무적
        DECOY,          // 분신 — 어그로를 끄는 허수아비를 내보낸다
    }

    /// <summary> 260921_캐릭터 이동 방식에 '나선형'을 더했다(2-22). </summary>
    // 260920_맵 위에 놓여 지나가며 줍는 아이템 (FieldItemInfo.csv · CFieldItem · 2-20)
    public enum FIELD_ITEM_TYPE
    {
        NONE = 0,
        MASS_STUN,      // 전체 마비 — 살아 있는 몬스터를 전부 잠깐 세운다 + 화면 연출
        HEAL_LIFE,      // 목숨 회복 — 최대치를 넘지 않게 목숨을 되돌린다
        MAGNET_ALL,     // 전체 자석 — 맵 위의 픽업(영혼 · 다른 아이템)을 전부 그 자리에서 먹는다
    }

    /// <summary> 인벤토리 창 안의 탭. 스킬은 한 개만 장착한다. </summary>
    // 260918_캐릭터(스킨/레벨업 — 스테이지 진입 캐릭터도 여기서 바꾼다) 추가.
    // 카드(웨이브 보상 갤러리)는 가방 안이 아니라 로비의 별도 탭(LOBBY_TAB.CARD)으로 뺐다.
    // 260918_가방 하단 탭(D). 스킬은 판 안에서만 얻는 런 스킬이라 가방에서 뺐다. 펫은 자리만 잡아 둔다.
    public enum INVENTORY_TAB
    {
        EQUIP = 0,
        CHARACTER,
        PET,
    }

    // 260918_장비 뽑기 결과
    public enum GACHA_RESULT
    {
        FAIL,           // 코인이 모자라거나 뽑을 것이 없다
        NEW,            // 처음 얻었다
        LEVEL_UP,       // 이미 가진 장비 — 강화 1레벨로 바뀌었다
        REFUND,         // 이미 가진 만렙 장비 — 코인을 돌려받았다
    }

    // 260905_로비 하단 탭
    /// <summary> 선언 순서가 그대로 탭 버튼 순서다 (CUI_Lobby의 배열 인덱스). </summary>
    public enum LOBBY_TAB
    {
        BATTLE = 0,
        UPGRADE,
        INVENTORY,
        SHOP,
        // 260918_수집한 카드(웨이브 보상 이미지) 갤러리. 장착 개념이 없어 가방과 따로 뺐다.
        CARD,
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
        GNAW,           // 260921_땅 갉는 자 — 내 땅 가장자리를 도로 빈 땅으로 (fGimmickValue = 한 번에 갉는 칸 수)
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
        MASS_STUN,      // 260918_전체 마비
        ITEM_GET,       // 260920_맵 위 아이템 획득
        SLOT_TICK,      // 260921_시작 위치 슬롯이 한 칸 넘어갈 때
        SLOT_STOP,      // 260921_시작 위치가 정해졌을 때
        STYLISH,        // 260922_현란한 동작(아슬아슬 · 연속 점령 · 가둬 잡기 · 대형 점령)
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
        MASS_STUN,      // 260918_전체 마비
        ITEM_GET,       // 260920_맵 위 아이템 획득
        STYLISH,        // 260922_현란한 동작
    }

    // 260922_현란한 동작 — 알아보고 알려 주기만 한다(보상 없음). CStyleTracker가 가리고 CUI_InGame이 글자로 띄운다
    public enum STYLE_ACTION
    {
        NEAR_MISS,      // 선을 긋는 중 몬스터가 몸 · 선을 스치고 지나갔다
        CHAIN,          // 몇 초 안에 연달아 점령
        TRAP,           // 점령으로 몬스터를 가둬 잡았다(마리 수)
        BIG_CAPTURE,    // 한 번에 맵의 큰 비율을 먹었다
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
