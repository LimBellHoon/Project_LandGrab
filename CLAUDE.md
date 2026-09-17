# Project_LandGrab

2D 땅따먹기(Qix / 볼피드형) 모바일 게임. Unity **6000.3.8f1** / 2D URP.
서버 비용이 들지 않는 구조가 전제이며, 뒤에 깔린 보상 이미지(카드=스킨)를 점령해서 드러내는 것이 핵심 재미.

---

## 1. 작업 원칙 (반드시 지킬 것)

### 1-1. 재사용 우선 / 중복 금지
새 코드를 짜기 전에 **기존 코드에서 재사용 가능한 함수·헬퍼·패턴을 먼저 검색**한다 (Grep/Glob).

- 같은 로직이 두 곳 이상 생기면 공통 메서드로 추출한다.
- 그리드 규칙은 반드시 `CTerritoryGrid`를 거친다. 셀 배열을 바깥에서 직접 만지지 말 것.
- 대체된 옛 코드(데드 코드, 중복 가드)는 남기지 말고 정리한다.

### 1-2. 코드 주석 컨벤션
새로 작성하거나 수정하는 코드 앞에 `YYMMDD_<작업 카테고리>` 형식의 주석을 단다.

```csharp
// 260902_몬스터
```

**큰 단위로** 달 것 — 줄마다·메서드마다 쪼개지 말고 하나의 기능 블록에 대표 주석 하나.
날짜는 작업 시점 기준. 기존 코드의 `260901_`, `260902_` 주석과 동일 컨벤션.

### 1-3. 네이밍 / 파일 규칙
자매 프로젝트 `Portfolio_SoloLeveling`의 컨벤션을 그대로 따른다.
**애매하면 Engine이 실제로 쓰는 표기가 기준이다** (아래 1-5에 뽑아 두었다).

| 대상 | 규칙 | 예 |
|---|---|---|
| 클래스 | `C` 접두사 | `CTerritoryGrid`, `CMoveHandler` |
| 매니저 | `C<이름>_Manager` | `CStage_Manager` (Engine: `CData_Manager`) |
| 정적 유틸 | `C<이름>_Utility` | `CCSV_Utility` (Engine: `Math_Utility`) |
| enum | 전부 대문자 + 언더바 | `CELL_STATE`, `ENEMY_GIMMICK` |
| 프로퍼티(공개 상태) | 전부 대문자 | `OWNED_RATIO`, `IS_DRAWING` |
| 상수 | 전부 대문자 | `PIXEL_PER_CELL`, `PREFAB_PLAYER` |
| 멤버 변수 | `m_` + 타입 접두사 | `m_arrCell`, `m_iWidth`, `m_lstTrail` |
| 정적 멤버 | `s_` + 타입 접두사 | `s_iPass`, `s_sbLog` |
| 지역/인자 | 타입 접두사 | `iWidth`, `fCellSize`, `vCell`, `eDir` |
| 메서드 | `동사_명사` | `Step_To`, `Cell_ToWorld`, `Try_Find_NearestCell` |

**타입 접두사**
`i`=int, `f`=float, `b`=bool, `str`=string, `v`=Vector, `e`=enum, `c`=클래스 인스턴스,
`arr`=배열, `lst`=List, `dic`=Dictionary, `hs`=HashSet, `stk`=Stack, `q`=Queue,
`go`=GameObject, `tr`=Transform, `sr`=SpriteRenderer, `tex`=Texture, `sp`=Sprite, `ch`=char.

**예외 — 이건 규칙 위반이 아니다**
- **Desc 클래스의 멤버**는 대문자가 아니라 타입 접두사 lowerCamel이다.
  Engine의 `CGameObjectDesc`(`eObjectType`, `strPrefabName`, `vPosition`)를 그대로 따른다.
  데이터 홀더(`CMapInfo`, `CEnemyInfo`)도 같다 — `iMapID`, `fCellSize`, `bIsValid`.
- **수명주기 메서드는 단일 동사**로 둔다. `Tick`, `Hide`, `Show`, `Release`, `Initialize`, `Respawn`.
  Engine이 그렇게 쓰고 있으므로 억지로 `동사_명사`로 쪼개지 말 것.
- **CSV 파싱 클래스 이름은 Engine이 강제**한다 → `CCSVData_<파일명>` (2-5 참고).

**축약 금지.** `Util`, `Mgr`, `Info2` 같은 임의 축약은 쓰지 않는다.
(`CSV`, `UI`, `ID`처럼 이미 통용되는 대문자 약어는 예외)

### 1-4. Engine 표기를 다시 확인해야 할 때
Engine은 DLL이라 소스를 볼 수 없지만 **메타데이터는 읽을 수 있다.**
`Assets/Plugins/Engine/Engine.dll`을 PE → CLI 헤더 → `#~`/`#Strings` 스트림 순으로 파싱하면
타입·필드·메서드·인자 이름과 시그니처가 전부 나온다. IL까지 읽으면 동작도 역추적된다.
(260904에 CSV 규약을 이렇게 확정했다 — 추측으로 짜다 틀리는 것보다 이 쪽이 훨씬 빠르다.)

### 1-5. 인코딩
소스는 **UTF-8 BOM**으로 정규화되어 있다. 새 파일도 동일하게 유지할 것.
(자매 프로젝트에서 CP949 파일을 그냥 편집했다가 한글 주석이 영구 손상된 적이 있음.)

### 1-6. 옵션 UI에 새 항목을 올릴지는 매번 물어볼 것 (260916)
새 기능에 켬/끔 스위치가 필요하면 **개발 스위치는 항상 만든다** — 지금 패턴대로
`CGameConfig`에 필드 하나, 단일 진입점(`Set_Enabled`류) 하나. 옵션 UI 존재 여부와 상관없이
QA를 위해 항상 있어야 한다. 이건 기계적인 작업이라 매번 확인받지 않는다.

**그 스위치를 옵션 UI(유저가 보는 설정 화면)에 노출할지는 다르다.** 이건 화면에
뭘 보여줄지 정하는 기획 판단이라, 기능을 하나 끝낼 때마다 **"이거 옵션에 넣을지" 한 줄로
먼저 확인받은 뒤에 넣는다.** 항목이 쌓일수록 화면이 복잡해지고, 뺐다 넣었다 하면
유저 세이브에 남은 설정값과도 어긋날 수 있다.

옵션 UI를 실제로 만들 때는 카테고리(화면 효과 / 사운드 / 조작 등)를 먼저 잡아 둘 것 —
그래야 새 항목이 생겨도 옵션 화면 자체를 다시 설계하지 않고 해당 카테고리 밑에 줄 하나만 늘면 된다.

지금 후보로 나와 있는 것: `CCameraShake`의 흔들림 켬/끔(2-10-2). 아직 확정은 아니다.

---

## 2. 아키텍처

### 2-1. 레이어
| 레이어 | 위치 | 비고 |
|---|---|---|
| **Engine** | `Assets/Plugins/Engine/Engine.dll` | **소스 없음 (프리빌드 DLL)** — 이 리포에서 수정 불가 |
| **Client** | `Assets/Script/` | 게임 로직 전부 |

Engine은 별도 저장소(`Engine_6000.3.8f1`)의 **`2d-core` 브랜치** 빌드 산출물이다.
`CGameObject`, `CGameObjectDesc`, `CData_Manager` 등을 제공한다.
DLL이라 내부를 읽을 수 없으니 **호출부에서 역추적**할 것.

### 2-2. 스크립트 구조
```
Assets/
├── Editor/                 CProtoSetup(씬·프리팹·Addressable 자동 생성), CProtoTest(코어 테스트)
└── Script/
    ├── 00.GameManager/     CGameManager
    ├── 01.UI/              CDebugHUD, CUI_StageSelect, CUI_InGame, CUI_Popup (Engine.CUI 상속), CSafeArea
    ├── 02.GameObject/      CPlayer, CEnemy, CProjectile, CWeb  (Engine.CGameObject 상속)
    ├── 03.Module/          CTerritoryGrid, CGridRenderer, CMoveHandler, CEnemyMoveHandler
    │                       CInputHandler, CVirtualJoystick, CCameraFitter
    │                       CCameraShake, CCameraPunch, CFlashEffect, CCameraFeel_Utility
    │                       CEnemyGimmick(+_Projectile/_Web/_Spawn), CSound_Utility
    │                       CSkillHandler, CSkillEffect(+_Warp/_Shield/_Dash/_Slow/_Seal)
    ├── 97.Data/            CCSVData_EnemyInfo, CCSVData_MapInfo, CCSV_Utility, CStageProgress, CGameConfig
    ├── 98.Manager/         CStage_Manager, CProgress_Manager, CAudio_Manager, CHaptic_Manager
    └── 99.Defines/         Client_Enum, Client_Desc, Client_Interface

Assets/Data/                EnemyInfo.csv, MapInfo.csv  ← 기획 데이터
```

### 2-3. 핵심 — 영토 표현
**그리드 마스크 + 플러드필** 방식이다. (폴리곤 방식 아님 — 확정된 기술 결정)

- `CTerritoryGrid`가 셀 상태 배열(`CELL_STATE[]`)과 트레일을 소유한다.
- **`Step_To`가 이동 규칙의 단일 진입점**이다. 한 칸 이동의 판정(안전/선긋기/점령/사망)이 전부 여기서 나오고
  결과를 `STEP_RESULT`로 돌려준다. 규칙을 바꿀 일이 생기면 이 함수를 고칠 것.
- `Capture`는 닫힌 도형이 생겼을 때 영역 라벨링 후 **몬스터가 없는 영역만** 점령한다.
  몬스터가 하나도 없으면 가장 큰 영역만 남긴다.
- 플레이어는 점령지 **경계선 위에서만** 안전하다. 점령지 내부는 통과 불가(260902 결정).
- `CGridRenderer`는 셀을 픽셀로 찍는 마스크 텍스처로 그린다. **셰이더 없음.**
  `IS_DIRTY`가 설 때만 갱신하고, 한두 칸만 바뀐 경우 `DIRTY_CELLS`로 그 칸만 다시 올린다
  (점령처럼 한 번에 많이 바뀔 때는 `IS_FULL_DIRTY`로 전체 갱신). 자세한 건 2-5 참고.

### 2-4. enum (`99.Defines/Client_Enum.cs`)
```csharp
CELL_STATE : byte   EMPTY=0(미점령/위험), OWNED=1(점령/안전), TRAIL=2(선분), BLOCK=3(맵 밖)
MOVE_DIR            NONE=-1, UP=0, DOWN, LEFT, RIGHT
STEP_RESULT         SAFE, DRAW, CAPTURE, DEAD
STAGE_STATE         READY, PLAYING, CLEAR, FAIL
ENEMY_GIMMICK       NONE, WEB(거미줄), PROJECTILE(투사체), SPAWN(부하 소환)
CAddressableLabel   PREFAB="Prefabs", TEXTURE="Images", CSV="CSV"
```

### 2-5. 스테이지 규칙 값 — CSV (260904 이관 완료)
규칙 숫자는 전부 `Assets/Data/*.csv`에 있다. **코드에 같은 숫자를 다시 적지 말 것.**
`CStageDesc`에는 이제 '어떤 맵을 띄울지'(`iMapID`)만 남아 있다.

| 파일 | 파싱 클래스 | 내용 |
|---|---|---|
| `EnemyInfo.csv` | `CCSVData_EnemyInfo` | 몬스터 종류별 기믹·속도·충돌반경 |
| `MapInfo.csv` | `CCSVData_MapInfo` | 맵 크기·플레이어 속도·모양 마스크·이미지 스택·웨이브 구성·카드 지급 지점 |
| `CardInfo.csv` | `CCSVData_CardInfo` | 점령률 보상 카드 (3지선다) |
| `ProjectileInfo.csv` | `CCSVData_ProjectileInfo` | 탄 종류 — 모양 · 이동 · 특성 · 수치 (2-15) |
| `ImpactInfo.csv` | `CCSVData_ImpactInfo` | 맞은 대상에게 남는 효과 (2-15) |
| `RunSkillInfo.csv` | `CCSVData_RunSkillInfo` | 런 스킬 — 해금 · 레벨 · 투사체 무기 열 (2-11-1, 2-11-2) |
| `AwakenInfo.csv` | `CCSVData_AwakenInfo` | 런 스킬 각성 — 액티브 + 짝 패시브 (2-11-2) |
| `CharacterInfo.csv` | `CCSVData_CharacterInfo` | 캐릭터 — 스킨 프리팹 · 레벨별 스탯 배율 (2-17) |

> 표를 추가하면 `CProtoSetup`의 **`ARR_CSV`와 `ARR_CSV_TYPE` 두 곳 모두**에 넣을 것.
> 한쪽만 넣으면 검증이 배열 밖을 짚어 예외로 죽는다(260912에 가드를 넣어 이제는 이름을 대고 멈춘다).

#### Engine이 강제하는 CSV 규약 (어기면 표가 조용히 비어 버린다)
- **구분자는 탭(`\t`).** 쉼표가 아니다. `Engine.CCSVData`가 `Split('\t')`로만 쪼갠다.
- **0번 줄이 헤더.** 헤더 위에는 주석도 넣을 수 없다.
  **Engine은 헤더 줄도 `Parse_CSVData`에 그대로 넘긴다.** 파서 첫 줄에서
  `CCSV_Utility.Is_HeaderRow(arrField, "<첫 열 이름>")`으로 걸러야 한다 —
  안 걸면 실행할 때마다 표 개수만큼 에러가 찍혀 진짜 데이터 오류가 묻힌다(260905).
- 헤더가 `NONE`인 열은 통째로 버려진다 → 맨 끝 '메모' 칸이 그 용도.
- 첫 칸이 `;`로 시작하거나 비면 그 줄은 무시된다.
- 클래스 이름이 **`Client.CCSVData_<파일명>`** 이어야 한다. `MapInfo.csv` ↔ `CCSVData_MapInfo`.
  Engine이 파일명으로 타입을 찾아 `Activator`로 만든다 — 이름이 어긋나면 경고만 남기고 끝난다.
- 조회는 `Get_CSVData("CCSVData_MapInfo")` (내부 키가 `"Client." + 인자`).
- Addressable 라벨 `CSV`가 붙어 있어야 로드된다.

#### 웨이브와 이미지 스택
`MapInfo.csv`의 `strLayerTex`는 겹쳐 깔리는 이미지 목록이며 **(웨이브 수 + 1)장**이다.

```
[0] 마스크      ← 1웨이브의 가림막
[1] 보상1       ← 1웨이브를 깨면 드러남 / 동시에 2웨이브의 가림막
[2] 보상2       ← 2웨이브를 깨면 드러남 / 동시에 3웨이브의 가림막
[3] 보상3       ← 3웨이브를 깨면 드러나는 최종 보상
```

즉 **N웨이브의 가림막은 `[N-1]`, 다 걷어내면 `[N]`이 나온다.**
웨이브를 넘길 때 **판은 그대로 두고** 두 장만 갈아 끼운다(아래 "그 자리에서 이어서 한다" 참고).

`strWaveEnemy`는 웨이브를 `|`, 웨이브 안의 몬스터를 `,`, 개수를 `*`로 적는다
— `101*3|101*2,102*2|102*2,103*1,104*1`.

#### 렌더링
`CGridRenderer`는 두 SpriteRenderer를 쓴다 — 아래에 드러날 이미지(reveal), 위에 가림막(cover).
가림막은 원본을 셀 격자에 맞춰 다시 찍은 마스크 텍스처이고 점령한 칸만 알파 0으로 뚫는다.
그래서 **가림막·모양 마스크로 쓰는 텍스처는 임포트 설정에서 Read/Write Enabled가 켜져 있어야 한다.**

#### 가림막 사본은 원본과 같은 해상도로 찍는다 (260912)
**한 장이 이번 웨이브에는 보상(reveal)이었다가 다음 웨이브에는 가림막(cover)이 된다.**
예전에는 사본을 셀 격자 해상도(`WIDTH * PIXEL_PER_CELL`)로 줄여 찍었다.
맵 1이면 540x960 원본이 240x400으로 줄어드는데, 그러면 **같은 그림이 역할만 바뀌었는데
갑자기 거칠어져 크기가 달라진 것처럼 보인다.** 연출의 전제가 깨지는 지점이다.

그래서 `Fill_Cover`가 원본 크기를 보고 필요하면 마스크 텍스처를 그 크기로 다시 만든다
(`Build_Mask`). `PIXEL_PER_CELL`은 **가림막 원본이 없을 때만 쓰는 기본값**으로 남았다.

텍스처 크기가 칸 수로 나누어떨어지지 않아도 되게, 칸 하나의 픽셀 범위는
`Cell_ToPixelX(x) ~ Cell_ToPixelX(x+1)`로 잡는다 — **다음 칸의 시작을 끝으로 삼아야**
빈틈이나 겹침이 안 생긴다. (960 / 100칸 = 9.6이라 칸 높이가 9와 10을 오간다.)

두 장은 `Fit_ToGrid` 하나로 같은 자리·같은 크기에 놓는다. 한쪽만 원본 비율을 지키면
역할이 바뀔 때 그림이 어긋난다.

**원본 비율은 그리드 비율과 같아야 한다**(260912). 두 장 다 그리드 크기로 늘려 깔리므로
비율이 다르면 그림이 눌린다. 둘이 똑같이 눌리기 때문에 '한쪽만 이상하다'로는 안 보이고
그냥 어색해질 뿐이라 눈으로 찾기 어렵다 — `Validate Assets`의 `Validate_LayerAspect`가 숫자로 잡는다.

**임포트 설정에서 스프라이트 영역을 텍스처 전체로 못 박는다**(260912).
Unity 기본값은 `spriteMeshType: Tight` + `spriteExtrude: 1`이라 **스프라이트가 텍스처보다 작아지고
가장자리가 한 칸 떠 있다**(Sprite Editor에서 보인다). 가림막과 보상은 둘 다 `bounds`를 기준으로
그리드에 맞춰 깔리므로, 한쪽만 잘려 있으면 같은 그리드에 맞춰도 크기가 다르게 보인다.
`CProtoSetup.Import_AsSprite`가 `FullRect` / `extrude 0` / `border 0`으로 고정한다.
**아트를 손으로 넣을 때도 이 세 가지를 확인할 것.**

**배치 이미지의 동심원은 눈으로 쓰는 검사 도구다**(260912). 화면에서 정원으로 보이도록
가로를 비율만큼 늘려 그려 둔다 — **타원으로 보이면 어딘가에서 늘어난 것**이다.

배치 이미지는 `CProtoSetup.TEX_PIXEL_PER_CELL`(현재 9)로 그리드에서 만들어 낸다.
맵 1이면 60x100칸 → **540x900**이고, 칸 하나가 정확히 9x9 픽셀이다.
실제 아트를 발주할 때도 **이 비율(가로:세로 = 칸 수 비율)만 지키면 된다.**

> 원본 해상도로 올리면서 마스크 관련 메모리가 맵 1 기준 약 1MB에서 6MB로 늘었고,
> 점령 때 도는 전체 갱신 비용도 같은 비율로 는다. 성능이 문제가 되면
> **`PIXEL_PER_CELL`을 되살리는 게 아니라 원본 이미지 해상도를 낮추는 쪽으로** 잡을 것.

#### 웨이브가 넘어가도 그 자리에서 이어서 한다 (260912)
판을 다시 깔지 않고, **플레이어도 몬스터도 있던 자리에 그대로 둔다.**
바뀌는 것은 가림막 한 장과 '이번 웨이브에 새로 들어오는 몬스터'뿐이다.

- `Enter_Wave`는 `CTerritoryGrid.Reset`도 `Respawn_Player`도 부르지 않는다 → 점령한 칸과 위치가 유지된다
- `Spawn_Enemies`는 **모자란 종류만 그 차이만큼** 넣는다(`Count_Enemy`).
  전부 회수했다가 다시 뿌리면 플레이어 코앞에 몬스터가 순간이동하는 셈이 된다
- 표에 적힌 수보다 많아도 **줄이지 않는다.** 웨이브는 심해지기만 하는 것이 기획이고,
  방금까지 피하던 몬스터가 증발하면 오히려 혼란스럽다

점령한 칸이 유지되므로 `strWaveClearRatio`는 **누적 비율**이다(0.6 / 0.65 / 0.7).

#### 보상 공개 연출 (260904)
웨이브를 넘길 때 바로 갈아 끼우지 않는다. **드러나는 순간이 이 게임 재미의 전부**라
그냥 툭 바꾸면 남는 게 없기 때문이다. `CStage_Manager`가 3단계로 돌린다.

```
REVEAL(0.5초)  가림막 알파 1→0   보상이 전부 드러남
HOLD  (0.9초)  그대로 유지        감상할 틈
COVER (0.5초)  알파 0→1          다음 가림막이 덮임 (그 사이 텍스처만 교체, 판은 그대로)
```

마지막 웨이브면 COVER 없이 **드러난 채로 CLEAR**로 끝난다.
연출 중에는 액터를 세우고 제한 시간도 멈춘다 — 연출 때문에 시간을 잃으면 억울하다.
연출이 끝나면 **액터는 멈췄던 그 자리에서** 다시 움직인다.

알파는 마스크 텍스처를 다시 찍지 않고 **`SpriteRenderer.color`만** 건드린다
(`CGridRenderer.Set_CoverAlpha`). 매 프레임 픽셀을 다시 올리면 모바일에서 감당이 안 된다.

#### 맵 모양 마스크
> 260912_예시로 쓰던 2번 맵(`좁은 맵`)과 `Tex_Shape_02`를 지웠다. **지금 맵은 하나뿐이다.**
> 기능 자체는 그대로 남아 있으니 쓰려면 텍스처를 만들어 `strShapeMask`에 적으면 된다.
`strShapeMask` 텍스처의 밝은 픽셀만 플레이 가능한 칸이 되고, 나머지는 `CELL_STATE.BLOCK`이 된다.
BLOCK은 플레이어·몬스터 모두 못 들어가고 점령률 분모에서도 빠진다. `-`이면 직사각형 전체.

### 2-6. 몬스터 기믹 (260904)
기믹은 **상속이 아니라 조합**이다. `CEnemy`가 `CEnemyGimmick` 모듈을 하나 들고 있고,
`EnemyInfo.csv`의 `eGimmick` 한 칸으로 무엇을 붙일지 정한다.
그래서 몬스터 종류가 늘어도 **프리팹은 `Prefab_Enemy` 하나면 된다.**

```
CEnemy ── CEnemyMoveHandler   (배회 / 추적 / 벽 튕김)
       └─ CEnemyGimmick       (쿨타임 → 발동)   ← eGimmick으로 결정, NONE이면 null
```

기믹은 소환물을 **직접 만들지 않는다.** `IGimmickHost`(`99.Defines/Client_Interface.cs`)로
스테이지에 요청만 하고, 실제 생성·수명·플레이어 충돌은 `CStage_Manager`가 한곳에서 본다.
그래야 웨이브가 넘어갈 때 통째로 회수할 수 있다.

| 기믹 | Cool | Value | Range | Duration | RefID |
|---|---|---|---|---|---|
| `WEB` | 설치주기 | 플레이어 속도배율 | — | 거미줄 지속 | — |
| `PROJECTILE` | 발사주기 | — | 쏘기 시작하는 거리(셀) | — | `ProjectileInfo.csv` 탄 ID |
| `SPAWN` | 소환주기 | 소환 마리수 | — | — | 소환할 몬스터 ID |

- 투사체·거미줄은 `OBJECT_TYPE.ENEMY_EFFECT` 레이어에 올라간다.
  스테이지가 끝날 때 이 레이어도 함께 세워야 탄이 계속 날아가지 않는다(2-7).
- 거미줄만 플레이어가 안전 지대에 있어도 계속 깔린다. 나머지는 플레이어가 나와 있을 때만 발동한다.
- `SPAWN`의 `RefID`가 다시 `SPAWN` 몬스터를 가리키면 무한히 늘어나므로
  `CStage_Manager.MAX_ENEMY`(32)로 총량을 막는다.
- 260917_`PROJECTILE`의 탄속 · 수명 · 사거리는 **탄 표(2-15)로 옮겼다.** 몇 발을 어떻게 뿌릴지는
  `EnemyInfo.csv`의 `eFirePattern` · `iFireCount` · `fFireAngle` · `fFireInterval`이 정한다 —
  탄 자체가 아니라 쏘는 쪽의 성질이기 때문이다.
- 260918_**몬스터별 체력/공격력도 CSV로 뺐다.** `EnemyInfo.csv`의 `iHp`가 그 몬스터의 최대 체력,
  `iAttack`이 플레이어와 몸이 부딪혔을 때 주는 피해다. 이 몬스터가 쏘는 탄의 피해는 여기가 아니라
  `ProjectileInfo.csv`의 `iDamage`를 따로 쓴다(2-15) — 몸통 박치기와 원거리 공격력은 서로 다른 값이다.
  값이 0 이하면(표를 안 채웠거나 `CProtoTest`처럼 손으로 만든 Desc) `CEnemy`의 고정값(체력3/공격력1)으로
  대체된다 — 예전엔 이 고정값을 전 몬스터가 같이 썼다(2-11-1, 2-14).
  `CStage_Manager.Tick_Enemy`가 같은 프레임에 여러 몬스터가 동시에 닿으면 **가장 센 공격력**을 쓴다.

### 2-6-1. 입력 — 키보드 + 가상 조이스틱 (260904)
`CInputHandler`가 입력을 4방향 하나로 정리해 내보낸다. 어디서 왔는지는 바깥이 몰라도 된다.

```
CInputHandler ── CVirtualJoystick   (터치/마우스 → 4방향)
              └─ WASD / 방향키       (조이스틱을 안 잡고 있을 때만)
```

- **조이스틱은 EventSystem을 쓰지 않고 구 `Input`을 직접 읽는다.**
  이 프로젝트엔 Input System 패키지가 들어 있지만 코드는 구 `Input`을 쓰고 있어
  어느 입력 모듈이 살아 있는지 확실하지 않다. 조작이 통째로 죽는 위험을 피한 선택이다.
  (반대로 스테이지 선택 **버튼은 EventSystem을 탄다** — 여기가 안 눌리면 그 문제다.)
- **누른 자리가 중심이 되는 플로팅 방식.** 세로 화면에서 엄지가 닿는 자리가 매번 다르다.
- 화면 **아래 60%** 에서 눌러야 잡힌다. 위쪽은 나중에 붙을 버튼용으로 비워 둔다.
- 반경·데드존은 픽셀이 아니라 **화면 높이 비율**(12% / 그 25%)이라 해상도가 달라도 같은 느낌이 난다.
- 판정(`CVirtualJoystick`)과 그리기(`CUI_InGame`)를 나눠서, 화면 없이도 방향 양자화를 테스트한다.

### 2-7. 화면 흐름 / 진행도 (260904)
```
선택 화면(CUI_StageSelect) ──고름──> 스테이지 ──CLEAR/FAIL──> 1.5초 뒤 선택 화면
```
갈아타는 지점은 `CGameManager`에만 있다. 스테이지도 UI도 서로를 모른다.
`CStage_Manager.OnStateChanged`로 결과만 올려보내고, 기록 저장과 화면 전환은 매니저가 한다.

**해금은 순차** — `MapInfo.csv`에 적힌 **순서**로 바로 앞 맵을 깨야 다음이 열린다.
맵이 하나뿐이어도 규칙은 계속 검증된다 — `CProtoTest.Make_TwoMapTable`이 두 줄짜리 표를 따로 만들어 쓴다.
ID 산술이 아니라 표의 순서를 본다. 기획이 중간에 맵을 끼워 넣어도 ID를 다시 매기지 않아도 된다.
`GameConfig.asset`의 `m_bUnlockAllStage`를 켜면 규칙을 무시하고 전부 열린다(2-9).

**저장은 `IStageProgress`로 추상화**되어 있다 (`99.Defines/Client_Interface.cs`).
지금 구현은 `CStageProgress_Local` 하나뿐 — 진행도를 JSON으로 만들어 PlayerPrefs에 넣는다.
저장하는 알맹이가 JSON이라 **뒤끝·Firebase를 붙일 때 이 인터페이스만 새로 구현하면 되고
스테이지/UI 코드는 손대지 않는다.** 백엔드를 붙여도 로컬 구현은 남는다 —
통신이 끊겼다고 진행이 막히면 안 되므로 로컬을 먼저 읽고 나중에 동기화하는 형태가 된다.

UI는 Engine의 `CUI`를 상속해 오브젝트 풀과 캔버스 관리에 그대로 올라탄다.
목록은 `MapInfo.csv`를 훑어 런타임에 만든다 — 맵이 늘어도 UI 코드는 고치지 않는다.
버튼은 프리팹에 넣어 둔 비활성 템플릿을 복제해 쓰고, 겉모습은 프리팹에서 정한다.
`CGameInstance.Set_UICanvas(Field/Main/Popup)`를 먼저 불러야 UI가 붙을 자리가 생긴다.

#### Engine이 강제하는 UI 규약 (260904 — 컴파일 에러로 확인)
UI를 여는 함수는 **제네릭**이다. `T`가 인자에 안 나오므로 **타입을 반드시 적어야 한다.**

```csharp
CUI Open_UI<T>(CUIDesc cUIDesc, Transform trParent = null)   // 반환은 T가 아니라 CUI
```

`Open_UI(cDesc, tr)`처럼 쓰면 `CS0411`(타입 인자 추론 실패)이 난다 → `Open_UI<CUI_Popup>(cDesc, tr)`.

**더 중요한 건 프리팹 이름을 Engine이 덮어쓴다는 점이다.** `CUI_Manager.Open<T>`의 첫 줄이

```csharp
cUIDesc.strPrefabName = "Prefab_" + Engine_Utility.Convert_TypeToString<T>();
```

이고 `Convert_TypeToString<T>()`는 `typeof(T).Name`에서 **맨 앞 `C` 하나만** 떼어낸다.
따라서 Desc에 무슨 이름을 적든 무시되고 **Addressable 주소가 `Prefab_<C 뗀 클래스명>`이어야** 로드된다.

| 클래스 | 강제되는 프리팹 주소 |
|---|---|
| `CUI_StageSelect` | `Prefab_UI_StageSelect` |
| `CUI_InGame` | `Prefab_UI_InGame` |
| `CUI_Popup` | `Prefab_UI_Popup` |

이름이 어긋나면 컴파일은 통과하고 **런타임에 조용히 UI가 안 뜬다.**
그래서 `CGameManager`의 `PREFAB_UI_*` 상수와 `CProtoSetup`의 주소는 이 규칙을 그대로 따른다.

액터(`Reuse_Object`)는 이 규칙을 타지 않는다 — `strPrefabName`을 적은 대로 쓴다(`Prefab_Player` 등).

곁들여 확인한 것들:
- `trParent`는 **선택 인자**다. 안 넘기면 `eObjectType`으로 캔버스를 알아서 찾는다.
- `CLayer.Reuse_GameObject`는 **풀에서 꺼낸 경우에도 `Initialize(desc)`를 부른다.**
  그래서 `Hide()`에서 참조를 끊어도 다음 재사용 때 되살아난다.
- `Close_UI`는 오버로드가 둘이다 — 제네릭 `Close_UI<T>()`와 인스턴스 `Close_UI(CUI)`.
  우리는 연 것을 그대로 닫으므로 후자를 쓴다.

**일시정지 / 결과 화면은 `CUI_Popup` 하나를 돌려쓴다** (Popup 캔버스).
제목·본문·버튼 두 개가 전부이고 무엇을 보여줄지는 `CUI_PopupDesc`가 정한다 —
팝업이 늘어도 클래스와 프리팹을 새로 만들지 않기 위해서다.

- 일시정지는 `CStage_Manager.Set_Pause` → `Set_ActorTimeScale(0)`. 새 정지 플래그를 만들지 말 것.
  연출 중에 일시정지를 풀어도 타임스케일은 0을 유지한다(연출이 끝나야 풀린다).
- **앱이 백그라운드로 가면 자동 일시정지**된다(`CGameManager.OnApplicationPause`).
  돌아올 때 자동으로 풀지는 않는다 — 갑자기 움직이면 그대로 죽는다.

인게임 HUD(`CUI_InGame`, Field 캔버스)는 스테이지가 있는 동안만 떠서 조이스틱과 진행 상황을 그린다.
Engine 레이어가 UI까지 Tick하는지 확실하지 않아 이 클래스만 Unity `Update`를 쓴다 —
조이스틱이 안 그려지면 조작 자체가 불가능해지기 때문이다.
`CDebugHUD`(OnGUI)는 아직 남겨 두었다. 겹치는 정보가 있으므로 정식 UI가 자리 잡으면 지울 것.

### 2-8. 스테이지 수명 주기
`CStage_Manager.Tick`은 `STAGE_STATE.PLAYING`일 때만 규칙을 돌리지만,
**플레이어·몬스터의 `Tick`은 Engine의 레이어가 직접 돌린다.** 스테이지 상태만 바꿔서는 액터가 멈추지 않는다.
그래서 `Set_State`가 CLEAR/FAIL로 넘어갈 때 `Set_LayerTimeScale(PLAYER/ENEMY, 0)`으로 두 레이어를 세우고,
`Start_Stage`/`Release`에서 1로 되돌린다 (260904). 액터를 멈춰야 하는 기능은 이 경로를 쓸 것 — 별도 정지 플래그를 만들지 말 것.
기믹 소환물이 올라가는 `ENEMY_EFFECT` 레이어도 같은 함수(`Set_ActorTimeScale`)가 함께 처리한다.

### 2-9. 튜닝 / 디버그 옵션 — ScriptableObject (260905)
자주 만지는 값과 개발용 스위치는 `Assets/Resources/GameConfig.asset` 하나에 모여 있다
(`97.Data/CGameConfig.cs`). 인스펙터가 아니라 에셋인 이유는 **씬을 열지 않고 고칠 수 있고,
씬을 다시 만들어도 값이 날아가지 않기 때문**이다. `CGameManager`의 인스펙터 플래그는 여기로 옮겼다.

| 항목 | 뜻 |
|---|---|
| `m_fPlayerSpeedScale` | `MapInfo.csv`의 `fPlayerSpeed`에 **곱하는 배율**. 1이면 표 그대로 |
| `m_bUnlockAllStage` | 해금 규칙 무시 |
| `m_bFreeSpend` | 코인을 쓰지 않고 구매 / 강화 / 스킬 강화 |
| `m_iStartCoin` | 코인이 0일 때 처음 한 번만 지급 (0이면 안 함) |

**규칙 숫자는 여전히 CSV가 원본이다.** 여기 두는 것은 '그 값을 얼마나 비틀지'와
'규칙을 건너뛸지'뿐이다 — 같은 숫자를 두 군데 적으면 어느 쪽이 진짜인지 알 수 없게 된다.
그래서 플레이어 기본 속도는 `MapInfo.csv`의 `fPlayerSpeed`에서, 전체적인 빠르기는 배율에서 잡는다.

에셋이 없어도 게임은 기본값으로 돈다(`CGameConfig.Load`). 값을 바꿨는데 반영이 안 되는 상황은
알아채기 어려우므로 `Validate Assets`가 현재 값을 한 줄로 찍어 준다.

**전투에서 쓸 소모품은 `Get_BattleConsumable` 하나로 정한다**(260912).
장착한 것이 있으면 그것을, 없으면 갖고 있는 소모품 중 하나를 고른다.
슬롯은 '여러 개 중 무엇을 들고 갈지' 고르라고 둔 것인데 **장착을 요구하면 사 놓고도 버튼이 안 뜬다** —
산 물건이 가방에서 잠자는 셈이라 보유 기준으로 바꿨다. 버튼 표시(`CUI_InGame`)와
실제 사용(`CGameManager`)이 같은 함수를 보므로 둘이 어긋나지 않는다.

**코인을 쓰는 곳은 강화 / 구매 / 스킬 강화 셋뿐이고 전부 `CProgress_Manager.Pay`를 지난다.**
무료 스위치도 UI가 버튼을 켤지 정하는 `Can_Pay`도 여기 하나에 걸려 있다 —
새로 코인을 쓰는 기능을 붙일 때 `Use_Coin`을 직접 부르지 말 것. 한 곳을 빠뜨리면 그 화면만 조용히 막힌다.

### 2-10. 세로 화면 (9:16) — 260912
모바일 출시 기준을 **세로 고정**으로 잡았다. `defaultScreenOrientation: 0`(Portrait),
기본 해상도 1080x1920, 가로 자동회전은 껐다. UI 캔버스 기준 해상도도 1080x1920이다.

#### 카메라는 플레이어를 따라간다 (260912)
맵 전체를 한 화면에 보여주지 않는다. `CGameConfig.m_fViewCellHeight`(현재 44칸)만큼만 보고
나머지는 플레이어를 따라 움직인다.

**맵이 커져도 보이는 칸 수는 그대로다.** 그래서 스테이지가 올라가 맵이 넓어지면
플레이어가 실제로 좁게 느끼고, 이동 속도 강화가 값어치를 갖는다 —
BM(능력치 강화)과 스테이지 확장이 맞물리는 지점이라 **이 값을 맵 크기에 연동하지 말 것.**

- `Clamp_Center`가 **맵 밖이 보이지 않게 가둔다.** 한 축이 시야보다 좁으면 그 축은 가운데 고정이다
  (가둘 여유가 없는데 억지로 밀면 맵이 흔들린다)
- 맵이 시야보다 작으면 자동으로 전체 모드로 떨어진다. `m_fViewCellHeight`가 0이어도 전체 모드다
- 따라붙는 것은 `SmoothDamp`(`m_fCameraFollowTime`, 현재 0.12초).
  스테이지를 깔 때는 연출 없이 바로 그 자리에 둔다 — 시작하자마자 미끄러지면 어지럽다
- `CGameManager.Tick_Camera`가 액터가 움직인 뒤에 부른다. 먼저 부르면 한 프레임 밀린다

UI 띠는 여전히 빠진다. 따라가는 기준점은 **가운데 띠의 중심**이지 화면 중심이 아니다.

#### 전체 모드일 때는 두 축 중 모자란 쪽으로 맞춘다
`CCameraFitter`(`03.Module`)가 런타임에 맞춘다. 기기마다 비율이 달라 에디터에서 정해 둘 수 없다.

높이로만 맞추면 **9:16에서 맵 좌우가 잘린다.** 맵 1은 월드 7.2 x 12.0인데
높이 기준 카메라(size 6.0)에서는 가로가 7.09밖에 안 보인다. 그래서 가로 기준과 세로 기준을
각각 구해 **큰 쪽**을 쓴다.

맵마다 크기가 다르므로 **스테이지를 깔 때마다 다시 맞춘다**(`CGameManager`).
에디터의 `Setup_Camera`가 잡아 두는 값은 맵 1 기준이라 크기가 다른 맵이 어긋난다.

#### 화면을 세 띠로 나눈다
`CGameConfig`의 `m_fUIReserveTop`(0.10) / `m_fUIReserveBottom`(0.22)만큼은 맵이 쓰지 않는다.
카메라는 남은 가운데 띠에 맵을 넣고 그 띠의 한가운데로 내려간다 —
화면 정중앙에 두면 조이스틱이 맵 아래쪽을 가린다.

```
상단 10%   정보 바 (웨이브 · 점령률 게이지 · 남은 시간 · 일시정지)
가운데     맵
하단 22%   왼쪽 조이스틱 / 오른쪽 스킬 · 아이템
```

#### 조이스틱과 버튼은 영역으로 나눈다
조이스틱은 EventSystem이 아니라 구 `Input`을 직접 읽으므로(2-6-1),
**오른쪽 버튼을 눌러도 그 터치가 조이스틱까지 잡아 버린다.**
그래서 `CInputHandler.ACTIVE_WIDTH`(0.55)로 **왼쪽에서만** 잡히게 했다.
오른쪽에 버튼을 더 놓을 때 이 값을 넘지 않게 할 것.

#### 노치 / 홈 인디케이터
`CSafeArea`(`01.UI`)를 UI 루트에 달면 자식이 전부 `Screen.safeArea` 안으로 들어온다.
로비는 **배경을 일부러 밖에 두고** 내용만 안전 영역에 넣는다(`SafeArea` 오브젝트) —
배경까지 줄이면 노치 옆에 칠하지 않은 띠가 생긴다.

에디터 Game 뷰에서는 `safeArea`가 화면 전체라 아무 일도 일어나지 않는다.
**여기서 하는 일이 없다고 해서 잘못된 것이 아니다.**

계산(`Calc_Size` · `Calc_PositionY` · `Calc_AnchorMin/Max`)은 전부 static이라
화면 없이 `CProtoTest`에서 검증한다.

### 2-10-1. 점령률 보상 카드 — 3지선다 (260912)
`MapInfo.csv`의 `strCardRatio`(현재 `0.2|0.4|0.6`)를 넘을 때마다 카드 세 장 중 하나를 고른다.
고른 효과는 **그 판이 끝날 때까지만** 유지된다 — 스테이지를 나가면 사라진다.

```
CStage_Manager.Check_CardReady  점령률이 지점을 넘었나 → OnCardReady
CGameManager.On_CardReady       판을 세우고 CUI_CardPick을 연다
CUI_CardPick                    셋을 보여 주고 고른 것을 돌려준다
CStage_Manager.Apply_Card       효과를 건다
```

- **화면을 여는 것은 `CGameManager`다.** 스테이지가 UI를 직접 열면 화면 전환이 두 군데로 갈라진다(2-7)
- **고르는 동안 판을 세운다.** 카드를 보는 사이에 맞으면 고르는 재미가 아니라 벌이 된다
- **웨이브 판정보다 카드 판정이 먼저다.** 순서가 반대면 판이 넘어가며 점령률이 0으로 돌아가 카드를 영영 못 받는다
- 이미 지나친 지점은 다시 주지 않는다(`m_iCardGiven`)
- 같은 카드가 한 번에 두 장 나오지 않는다(`Pick_Random`). 고르는 재미는 서로 다른 선택지에서 나온다
- 가중치(`iWeight`)가 0이면 안 나온다 — 카드를 지우지 않고 잠글 때 쓴다

#### 런 스킬도 같은 자리에 섞여 나온다 (260917)
3지선다 한 칸은 이제 `CPickOption`이다 — 카드(`CardInfo.csv`)이거나 런 스킬(`RunSkillInfo.csv`, 2-11-1)이다.
**두 표를 한 풀에 넣고 `iWeight`로 뽑는다**(`CPickOption_Utility.Pick`). 어느 쪽이 자주 나올지는 두 표의 가중치로 조절한다.

```
CGameManager.On_CardReady   카드 표 + 런 스킬 표 + 이번 판 레벨(CPlayer.RUN_SKILL) + 해금(CProgress_Manager.Is_Cleared)
CPickOption_Utility.Pick    한 풀에 넣고 가중치로 셋 (CWeightedPick_Utility — 카드 · 런 스킬 표가 같이 쓴다)
CStage_Manager.Apply_Pick   카드면 Apply_Card, 런 스킬이면 CPlayer.Add_RunSkill (새로 얻거나 레벨업)
```

런 스킬이 후보에 오르는 조건은 `CCSVData_RunSkillInfo.Collect_Candidates` 한곳이다.
- 해금됐다(`iUnlockMapID` 맵을 깼거나 0) · 만렙이 아니다 · 가중치가 0이 아니다
- **새로 얻는 스킬이면 그 분류(액티브/패시브)를 5개 미만으로 들고 있다**(`CRunSkillHandler.SLOT_PER_CATEGORY`).
  이미 가진 스킬의 레벨업은 슬롯을 새로 먹지 않으므로 상한과 상관없다 (Docs/Design_RunSkill_Awaken.md 2장 규칙 1)

화면은 런 스킬 이름 옆에 **`NEW` 또는 `Lv.N`**(고르면 될 레벨)을 붙인다. 레벨이 하나뿐인 스킬(월보 · 신발)은 `NEW`만 붙는다.
색은 분류로 가른다 — **액티브 주황, 패시브 청록**. 아이콘(`Tex_RunSkill_<RUN_SKILL_TYPE>`)은 흰색으로 구워 이 색을 곱한다.
**런 스킬 종류를 더하면 `CProtoSetup.ARR_RUN_SKILL_ICON`과 `Is_RunSkillInk`에 `RUN_SKILL_TYPE` 순서대로 넣을 것.**

260917_각성도 같은 자리에 금색으로 섞여 나온다(2-11-2).

카드는 **색과 아이콘이 먼저 읽히게** 만든다. 글자만으로는 순간적으로 고르기 어렵다.
종류마다 아이콘(`Tex_Card_<CARD_TYPE>`)과 색이 정해져 있고, 테두리 발광도 같은 색으로 칠한다.
발광은 9슬라이스(`Tex_CardGlow`)라 카드 크기가 달라져도 두께가 유지된다.
**종류를 더하면 `CProtoSetup.ARR_CARD_ICON`과 `CUI_CardPick.Get_Tint` 두 곳에 같은 순서로 넣을 것.**

속도·회피·감속은 **누적**된다. 플레이어의 속도는 이제 세 갈래가 곱해진다 —
거미줄(환경) × 질주(스킬) × 카드. 몬스터 감속도 스킬 × 카드다(2-11).

### 2-10-2. 카메라 흔들림 — 트라우마 기반 (260916)
`CCameraShake`(`03.Module`)는 **"얼마나 세게 흔들지"만 안다.** 무엇이 흔들 자격이 있는지는 모른다.
호출부가 `Add_Trauma(양)`을 부르면 그 양이 누적(최대 1)되고, 시간이 지나면 스스로 줄어든다.
화면에 보이는 세기는 트라우마의 **제곱**이라 — 조금 쌓였을 땐 거의 안 보이다가 많이 쌓이면
급격히 커진다(Squirrel Eiserloh, GDC "Juicing Your Cameras With Math" 방식을 그대로 따랐다).

```
CPlayer.OnDamaged  ──> CGameManager.On_PlayerDamaged   ──> Add_Trauma(TRAUMA_ON_HIT)
CPlayer.OnDead     ──> CGameManager.On_PlayerDeadShake ──> Add_Trauma(TRAUMA_ON_DEATH)
CGameManager.Tick_Camera가 매 프레임 오프셋을 읽어 카메라 Transform에 더한다
```

**어디를 볼지(`CCameraFitter`)와 얼마나 흔들지(`CCameraShake`)는 서로 모른다.** Fitter가 먼저
그 프레임의 카메라 위치를 다 계산해 박아 두면(`Apply`), 그 위에 흔들림 오프셋을 얹는다 —
다음 프레임에도 Fitter가 같은 기준점에서 다시 계산하므로 흔들림이 누적되어 표류하지 않는다.

**새 흔들림 원인을 추가해도 `CCameraShake`는 고치지 않는다.** `CGameConfig`에 트라우마 양
하나(`m_fTraumaOn<원인>`)를 늘리고, 그 이벤트가 일어나는 곳에서 `Add_Trauma(그 값)` 한 줄만
부르면 된다. 원인별로 진폭·지속시간 쌍을 따로 관리하지 않는 게 핵심이다 — 쌍이 늘어날수록
관리할 조합이 늘어난다.

`CPlayer`는 `OnLifeChanged`(목숨 변화 전체, 회복 포함)와 `OnDamaged`(실제로 맞았을 때만)를
따로 둔다. 흔들림처럼 "맞았다"만 골라 들어야 하는 연출이 `OnLifeChanged`를 들으면
포션으로 회복할 때도 흔들리는 사고가 난다.

**옵션창은 아직 없다.** 대신 끄는 자리 하나(`CCameraShake.Set_Enabled`)는 미리 못박아 뒀다 —
`CGameConfig.m_bCameraShakeEnabled`를 그대로 흘려보내고 있을 뿐이라, 옵션창이 생기면
이 값을 유저 설정으로 바꿔치기만 하면 된다. `CProgress_Manager.Set_FreeSpend`와 같은 자리다.
꺼져 있으면 트라우마가 아예 안 쌓이고, 쌓여 있던 것도 즉시 지운다.

판정(`CCameraShake`)과 적용(카메라 `Transform`에 더하기)을 나눠서 화면 없이 `CProtoTest`에서
파형과 감쇠를 검증한다 — `CCameraFitter`와 같은 이유다.

### 2-10-3. 점령 펀치줌 / 피격·회피 화면 플래시 (260916)
카메라 흔들림과 같은 날 함께 만든 손맛 폴리시 2종이다. 셋 다 같은 모양(0~1이 쌓였다가
스스로 줄어든다)이라 감쇠·세기 곡선 계산은 `CCameraFeel_Utility`(03.Module) 하나로 합쳤다
(1-1) — `CCameraShake`도 260916에 이걸 쓰도록 다시 정리했다.

**펀치줌**(`CCameraPunch`)은 점령하는 순간 카메라를 살짝 당겼다가 되돌린다.
`CPlayer.OnCapture ─▶ CGameManager.On_PlayerCaptured ─▶ Add_Punch(PUNCH_ON_CAPTURE)`.
`CCameraShake`가 위치를 흔드는 것과 똑같은 자리(`CGameManager.Tick_Camera`)에서
Fitter가 정한 크기 위에 배율을 곱한다 — 흔들림·줌이 서로의 존재를 몰라도 같은 프레임에
자연스럽게 겹친다.

**화면 플래시**(`CFlashEffect`)는 피격(빨강) · 회피(하양) 순간 화면 전체를 잠깐 물들인다.
흔들림·펀치와 달리 **카메라가 아니라 화면(UI)**에 적용되므로 `CGameManager`가 아니라
`CUI_InGame`이 들고 있다 — 세계는 GameManager, 화면은 UI 클래스가 맡는다는 구분을 그대로
따른다. `CPlayer.OnDamaged`/`OnEvade`를 `CUI_InGame`이 직접 구독한다(이미 `m_cPlayer`를
들고 있으므로 GameManager를 거칠 필요가 없다). 색은 `CGameConfig`가 아니라
`CUI_InGame`의 상수로 둔다 — 어떤 색을 쓸지는 시각 정체성이지 흔들 만큼의 세기 값이
아니다(`CEnemy`의 기믹별 색과 같은 자리). 겹치면 **나중에 들어온 색이 이긴다** — 섞으면
무슨 일이 일어났는지 못 읽는다.

렌더 대상 `m_imgFlash`는 프리팹의 **맨 마지막 자식**이라 조이스틱·버튼 위에 그려지지만,
`raycastTarget`이 꺼져 있어 그 아래 버튼의 터치를 가로채지 않는다.

셋 다 옵션창 후보다(1-6) — `CGameConfig`에 `m_bCameraPunchEnabled` / `m_bScreenFlashEnabled`가
이미 있고, 꺼지면 즉시 지워진다. 아직 옵션 UI에 올리지는 않았다.

### 2-11. 플레이어 스킬 — 효과 모듈 (260912)
스킬은 **기믹과 같은 조합 구조**다(2-6). `CPlayer`가 `CSkillEffect` 모듈을 하나 들고 있고,
`SkillInfo.csv`의 `eType` 한 칸이 무엇을 붙일지 정한다.

```
CPlayer ── CSkillHandler   (쿨타임만 잰다)
        └─ CSkillEffect    (실제 효과)   ← eType으로 결정, 패시브/NONE이면 null
```

예전에는 `Try_UseSkill`이 WARP 하나를 직접 처리했다. 스킬이 늘 때마다 `CPlayer`에 분기가
쌓이는 모양이라 효과를 떼어 냈다. **`CPlayer`는 '무엇을 할 수 있는가'만 공개하고,
'무엇을 할지'는 표가 정한다.**

| 스킬 | `fValue` | `fDuration` | 쓰는 것 |
|---|---|---|---|
| `WARP` 점멸 | 이동 칸 수 | — | `Warp` |
| `SHIELD` 보호막 | 무적 초 | — | `Add_Invincible` |
| `DASH` 질주 | 추가 속도 비율(0.8=1.8배) | 지속 | `Add_SkillSpeed` |
| `SLOW` 감속 | 감속 비율(0.55=몬스터 0.45배) | 지속 | `ISkillHost.Slow_Enemies` |
| `SEAL` 마감 | — | — | `Seal` |

- **효과가 실패하면 쿨타임을 돌리지 않는다.** 멈춘 채로 점멸을 눌러 쿨만 날리면 억울하다.
- **패시브는 모듈을 만들지 않는다.** 만들면 인게임에 눌러도 아무 일 없는 버튼이 뜬다.
- `SKILL_TYPE`은 **뒤에만 붙일 것.** `CStageProgress`가 스킬 레벨을 이 숫자로 저장해 두어
  순서를 바꾸면 옛 저장본의 레벨이 다른 스킬로 옮겨 간다.

#### 플레이어 밖을 건드리는 스킬
감속은 몬스터를 건드려야 하는데 몬스터는 스테이지가 들고 있다.
기믹이 `IGimmickHost`를 쓰듯 **`ISkillHost`로 요청만 한다** — 효과 모듈이 스테이지를 직접 알면
화면 없이 검증할 수 없어진다. 지속 시간도 스테이지가 재고,
**감속 중에 소환된 몬스터에게도 같은 배율을 건다**(안 그러면 스킬이 반쪽이 된다).

#### 속도는 두 갈래로 곱해진다
거미줄(환경)과 질주(스킬)를 따로 들고 곱한다(`CPlayer.Apply_Speed`).
스테이지가 매 프레임 환경 배율을 넣어 주므로 **한 갈래로 두면 질주가 다음 프레임에 덮어써진다.**
몬스터도 같은 이유로 `m_fSpeedScale`을 따로 든다 — `Set_ChaseState`가 속도를 다시 넣기 때문이다.

#### 마감(SEAL)
그은 선을 가장 가까운 점령지까지 ㄱ자로 이어 도형을 닫는다.
**점령 판정을 새로 만들지 않는다.** 평소 이동과 똑같이 한 칸씩 밟아 `Step_To`를 지나게 해서
규칙이 한 군데에만 있도록 유지한다(2-3). 워프도 같은 이유로 한 칸씩 간다.

### 2-11-1. 런 전용 스킬 — 뱀서라이크 픽업 (260916, 골격만)
위 2-11의 스킬(`SKILL_TYPE`)은 **로비에서 하나만 장착**하는 성장 요소다. 이번에 붙인 건
완전히 다른 결 — **스테이지를 깰 때마다 해금되고, 판 안의 3지선다에서 여러 개를 동시에
얻어 쌓아 가는** 뱀서라이크식 스킬이다. 표·핸들러·효과 모듈을 전부 따로 뒀다 — 저쪽은
"장착 스킬 하나의 쿨타임만 잰다"는 전제가 이미 코드 곳곳에 박혀 있어서(`CSkillHandler`,
`CPlayer`의 `m_cSkillEffect` 필드 하나), 여러 개를 동시에 드는 요구를 거기 욱여넣으면
그 전제부터 다시 뒤집어야 한다.

```
RunSkillInfo.csv          8종 정의 — 해금 조건(iUnlockMapID) · 최대 레벨 · 레벨당 수치 · 뽑기 가중치
CRunSkillHandler          지금 들고 있는 스킬들의 레벨만 센다(Dictionary). 판마다 Clear
CRunSkillEffect(+8종)     실제 효과. CPlayer가 List로 여러 개 동시에 들고 매 프레임 Tick
CPlayer.Add_RunSkill      3지선다가 고른 것을 여기로 넘긴다 — 없으면 1레벨로 붙고, 있으면 레벨업
IRunSkillHost             맵 위에 뭔가를 놓아야 하는 효과(영혼 수집가)가 쓰는 창구.
                          IGimmickHost/ISkillHost와 같은 이유 — 생성·수명·충돌은 CStage_Manager가 한곳에서 본다
```

**저장하지 않는다.** 스테이지를 나가면(`CPlayer.Initialize`가 다시 불릴 때) 보유 스킬과
레벨이 전부 사라진다 — 뱀서라이크가 다 그렇듯 이번 판 한정이다. 해금 자체(어떤 스킬이
3지선다 후보에 오를 수 있는가)만 `CProgress_Manager.Get_Star(iUnlockMapID)`로 계정에
영구히 남는다.

**액티브/패시브 조합으로 액티브를 각성시키는 조합식**은 아직 설계하지 않았다 — 다음 단계.

> 260917_**3지선다에 연결했다**(2-10-1). 이제 카드와 섞여 나와 실제 게임에서 얻을 수 있다.

8종 전부 코드가 붙었다. 마지막 둘(회전탄/몽둥이)은 이 프로젝트에 없던 "몬스터가 전투로
죽는다"는 개념을 처음 요구해서, 최소한의 HP/넉백 골격(`CEnemy.Damage`/`IS_DEAD`,
`CEnemyMoveHandler.Add_Knockback`)을 먼저 만들고 그 위에 얹었다 — EnemyInfo.csv의
기믹/수치 작업(로컬에서 진행 중인 M4 보스 작업)과는 무관한, 순수 전투 배선이다.

| 스킬 | 상태 | 비고 |
|---|---|---|
| 월보 | ✅ | `CMoveHandler.Can_Move`의 "점령지 내부 통과 불가"(260902 결정, 2-3) 조건에 `m_bAllowOwnedInterior` 플래그로 예외를 뚫었다. 그리드 규칙(`Step_To`) 자체는 그대로다 — 이동 가능 여부만 완화한다 |
| 어디로든 신발 | ✅ | `CMoveHandler`가 다음 셀을 계산하는 자리(`Get_NextCell`)를 한 곳으로 모으고, 거기서 x좌표를 폭으로 랩어라운드한다. `Can_Move`/`Can_Follow`/`Try_StartMove` 세 곳이 각자 다음 칸을 계산하고 있어서, 한 곳만 고치면 판정과 실제 이동 결과가 어긋난다 — 셋 다 이 함수를 쓰게 먼저 정리했다 |
| 자석 | ✅(값만) | 레벨별 반경을 `CPlayer.PICKUP_RADIUS`에 걸어 두기만 했다. 실제로 무언가를 끌어당기는 대상(영혼)이 아직 없다 |
| 회피 | ✅ | 기존 `CPlayer.Add_Evasion`(누적 전용 API)에 레벨업 때마다 **직전 레벨과의 차이만** 더한다 — 레벨2를 받았는데 레벨1+레벨2를 둘 다 더하면 두 배가 된다 |
| 분노조절못해 | ✅ | 달릴 때(`CPlayer.IS_MOVING`) · 맞을 때(`OnDamaged`) · 몬스터를 때릴 때(`CPlayer.On_MonsterHit`, 회전탄/몽둥이가 명중하면 `CStage_Manager`가 불러 준다) 셋 다 게이지가 오른다 |
| 영혼 수집가 | ✅ | `CSoul`(신규 픽업 오브젝트, `CWeb`과 같은 자리 — 제자리에 머무르다 수명이 다하면 사라짐)을 `IRunSkillHost.Spawn_Soul()`로 요청하면 `CStage_Manager`가 무작위 미점령 칸에 놓고 수명·습득 판정까지 한곳에서 본다. 습득 범위는 `SOUL_PICKUP_RADIUS_BASE`(기본) + 자석 보너스. 속도 증가는 `CPlayer.Add_CardSpeed`를 그대로 탄다 — "판이 끝날 때까지 유지되는 영구 가산"이 카드 속도가 이미 하는 일과 같아서 필드를 새로 만들지 않았다 |
| 회전탄 | ✅ | 260917_**투사체로 옮겼다.** 예전엔 좌표만 계산해 스테이지가 판정했는데 **아무것도 그려지지 않았고**, 반경 안 몬스터를 매 프레임 때려 즉사시켰다. 이제 `CRunSkillEffect_Orbit`이 `ProjectileInfo` 20(ORBIT 이동 · 수명 0 · `CANCEL_SHOT:1`)을 레벨 수만큼 띄워 붙잡아 둔다. 피해는 닿는 순간만, 적탄 지우기는 `CStage_Manager.Cancel_EnemyShots`가 탄 속성으로 본다. 수가 바뀌면 전부 거두고 같은 간격으로 다시 띄운다. 붙잡은 탄은 `CProjectileCore.SERIAL`로 풀 재사용을 가린다 |
| 몽둥이 | ✅ | 260917_**투사체로 옮겼다**(회전탄과 같은 이유 — 그려지지 않았다). `CRunSkillEffect_Club`은 "지금 휘두를 차례인가"(자체 쿨타임)와 반경만 안다. 바라보는 쪽 앞 좌표는 `CPlayer.Try_Get_FacingPoint`가 주고, 거기에 `ProjectileInfo` 22(짧게 커지는 원)를 **레벨 반경만큼 키워**(`Spawn_PlayerShot`의 `fScale`) 띄운다. 피해 · 넉백(`ImpactInfo` 7)은 그 탄이 넣는다. 멈춰 있으면 휘두르지 않는다. 뱀서라이크의 다른 무기와 마찬가지로 버튼 없이 자동 발동한다 — "액티브"는 쿨타임을 가진 효과라는 뜻이지 버튼 여부가 아니다 |

**회전탄/몽둥이를 위해 처음 생긴 것 — 몬스터 HP와 넉백.** 이전까지 몬스터는 전투로
죽지 않았다(웨이브가 넘어갈 때 회수될 뿐). `CEnemy`에 HP(당시엔 `DEFAULT_HP=3` 고정값)와
`Damage(int)`를 추가했고, 죽으면 Projectile/Web/Soul과 똑같이 **`bCollect`를 세워 Engine이
알아서 풀로 돌려주게 했다** — 새 회수 경로를 만들지 않았다. `CStage_Manager.Tick_Enemy`가
매 프레임 죽은 몬스터를 목록에서 먼저 걷어낸다(안 그러면 다음 프레임엔 Engine이 이미
반납해 다른 몬스터로 바뀌어 있을 수 있다). 넉백은 `CEnemyMoveHandler`에 배회/추적과는
별개인 짧은 강제 이동 구간을 추가하는 방식으로 얹었다.

> 260918_**몬스터별 체력/공격력을 CSV로 뺐다**(2-6 참고). 고정값은 CSV에 값이 없을 때의
> 폴백으로만 남았다.

> 260917_3지선다 UI 일반화는 끝났다(2-10-1 `CPickOption`). 프리팹은 여전히 `Setup_Assets` 산출물이라(3-1)
> 클라우드 세션에서는 화면을 확인할 수 없다 — UI를 바꿨으면 로컬에서 Setup Assets 후 Play로 볼 것.

### 2-11-2. 투사체 무기 · 각성 (260917)

#### 투사체 무기 — 일정 시간마다 저절로 쏜다
뱀서라이크 무기처럼 버튼 없이 쿨마다 쏜다. 2-15의 탄 표를 그대로 쓰고, **효과 모듈은 `CRunSkillEffect_Weapon` 하나**다 —
무기가 늘어도 `RUN_SKILL_TYPE`에 이름 하나, `RunSkillInfo.csv`에 줄 하나면 된다.

| 무기 | 탄 | 쿨 | 패턴 | 조준 | 레벨 수치 |
|---|---|---|---|---|---|
| 마법탄 `MAGIC_BOLT` | 4 유도탄 | 1.4 | BURST | 가장 가까운 적 | 연발 수 |
| 레이저 `LASER_BEAM` | 3 레이저 | 3.5 | SPREAD 40° | 체력이 가장 많은 적 | 줄기 수 |
| 부메랑 `BOOMERANG` | 6 부메랑 | 2.2 | SPREAD 30° | 가장 몰린 곳 | 발 수 |
| 튕기는 탄 `BOUNCE_SHOT` | 2 튕기는 탄 | 2.5 | RING | — | 발 수(1레벨 3발) |

- `RunSkillInfo.csv`에 `iProjectileID` · `fCool` · `eFirePattern` · `fFireAngle` · `eTargetFind` 열이 붙었다. **탄 ID가 0이면 무기가 아니다**
- **몬스터가 없으면 쏘지 않고 쿨을 찬 채로 기다린다.** 허공에 쏘면 몬스터가 들어오는 순간 쿨이 돌고 있어 억울하다
- 연발 · 회전 링의 상태는 `CProjectileFirer`가 든다 — **포수(`CEnemyGimmick_Projectile`)와 같은 클래스**다(1-1)
- 탄 생성과 조준 대상 찾기는 `IRunSkillHost.Spawn_PlayerShot` / `Find_Enemy`로 스테이지에 맡긴다
- 플레이어 탄이 몬스터를 새로 맞히면(`CProjectileCore.HIT_COUNT`) 회전탄 · 몽둥이와 같이 **분노 게이지가 오른다**
- 마법탄만 처음부터 해금(`iUnlockMapID` 0), 나머지 셋은 1번 맵을 깨야 나온다
- 개발용 자동 발사 스위치(2-15)도 같은 `Spawn_PlayerShot`을 지난다

#### 각성 — 만렙 액티브 + 짝 패시브 (Docs/Design_RunSkill_Awaken.md 규칙 3~5)
`AwakenInfo.csv` 한 줄이 각성 하나다. 액티브가 **만렙**이고 짝 패시브를 `iPassiveLevel` 이상 들고 있으면
3지선다에 **금색 "각성" 후보**로 나온다(`PICK_KIND.AWAKEN`). 자동으로 바뀌지 않는다 — 골라야 한다.

```
CCSVData_AwakenInfo.Collect_Candidates   만렙(RunSkillInfo의 iMaxLevel) · 짝 패시브 · 아직 안 함 · 가중치
CPickOption_Utility.Pick                 카드 · 런 스킬과 한 풀에서 뽑는다 (가중치 30으로 높게)
CStage_Manager.Apply_Pick → CPlayer.Awaken_RunSkill → CRunSkillHandler.Awaken + CRunSkillEffect.On_Awaken
```

- **액티브 요구 레벨 열은 없다** — '만렙'과 같은 숫자를 두 곳에 적게 된다
- **각성해도 슬롯 · 레벨은 그대로다.** 그 액티브가 그 자리에서 바뀐다. 액티브 하나는 한 번만 각성한다
- **새 스킬 클래스를 만들지 않는다.** 각성은 그 스킬의 강화된 형태라 같은 효과 모듈이 `m_cAwaken`을 보고 분기한다
- 판이 끝나면 각성도 레벨과 함께 사라진다(`CRunSkillHandler.Clear`)

| 각성 | 액티브 + 패시브 | 바뀌는 것 |
|---|---|---|
| 광란의 칼바람 | 회전탄 + 분노 | 탄 +1, **분노가 터진 동안** 수 2배 · 빠른 탄 21로 교체 (`CPlayer.IS_FEVER`) |
| 반격의 몽둥이 | 몽둥이 + 회피 | 범위 1.3배, **회피하는 순간 쿨이 비워진다** (`CPlayer.OnEvade`) |
| 블랙홀탄 | 마법탄 + 자석 | 탄 16(관통 + 끌어당김), 쿨 0.8배 |
| 십자 레이저 | 레이저 + 신발 | 탄 17(몸에 붙는 레이저), SPIN 4방향 |
| 영혼 낫 | 부메랑 + 영혼 수집가 | 탄 18(큰 낫 + 넉백 + 도트), +1 |
| 끝없는 튕김 | 튕기는 탄 + 월보 | 탄 19(11번 튕김), SPIN, +2 |

**투사체 무기의 각성은 표만으로 만든다** — `iProjectileID` · `eFirePattern`을 덮어쓰고 `strParam`으로 수를 조율한다
(`COUNT_BONUS` `COUNT_OVERRIDE` `COOL_RATE` `FIRE_ANGLE`). 새 각성탄은 `ProjectileInfo.csv`에 줄만 늘리면 된다.
회전탄 · 몽둥이처럼 동작이 바뀌는 각성만 코드가 필요하다(`FEVER_COUNT_RATE` `FEVER_PROJECTILE_ID` / `RADIUS_RATE` `COOL_RATE`).

**아직 안 만든 문서 후보**: 땅고르기 몽둥이(몽둥이 + 월보 — 때린 자리를 점령지로). 칸을 바깥에서 점령시키는 길이
`CTerritoryGrid`에 없어 규칙 단일 진입점(2-3)을 건드려야 한다 — 따로 설계할 것.

### 2-12. 효과음 — 절차적 플레이스홀더 (260916)
효과음 파일이 하나도 없다. `Engine.dll`을 직접 열어 확인해 보니(1-4) **Engine에는 애초에
오디오용 홀더가 없다** — `CTextureDataHolder`/`CPrefabDataHolder`는 있어도 오디오는 없다.
그래서 사운드는 Addressable/`CGameInstance`를 거치지 않고 **처음부터 클라이언트가 전담**한다.

당장은 `CSound_Utility`(03.Module)가 사인/사각/삼각/톱니파를 코드로 합성해 대신 채운다 —
텍스처를 `CProtoSetup`이 절차적으로 그려 두는 것과 같은 자리다. 시작음과 끝음을 다르게 주면
미끄러지는 톤이 나온다(하강=사망, 상승=점령처럼 **방향만으로 좋고 나쁨이 읽힌다**).
시작·끝 몇 ms를 무음에서 감아올려 클릭(뚝) 소리를 없앤다.

```
CSound_Utility.Generate_Tone   숫자 배열만 만든다 — AudioClip을 몰라도 된다(화면 없이 테스트)
CAudio_Manager.Build_Clip      그 배열을 AudioClip으로 감싸 캐싱한다(앱 수명 동안 한 번)
CAudio_Manager.Play(SOUND_ID)  캐시에서 꺼내 PlayOneShot — 여러 개가 겹쳐도 서로 안 끊는다
```

**진짜 SFX가 오면 `Build_Clip` 안쪽만 "합성"에서 "에셋 로드"로 바꾸면 된다.** 호출부
(`Play(SOUND_ID)`)는 그대로다 — `CGameManager`도, 훅이 걸린 자리들도 손댈 일이 없다.

`CAudio_Manager`는 `CStage_Manager`처럼 `CGameManager`가 들고 있는 순수 C# 클래스이지만,
**스테이지가 아니라 앱 전체 수명**이다 — `GameLogic_Async`에서 한 번만 `Initialize()`하고
`OnDestroy`에서 `Release()`한다. 재생을 위한 `AudioSource` 하나짜리 GameObject를 직접
만든다 — Engine이 프리팹/UI는 풀링해 주지만 오디오는 그 대상이 아니라서, 굳이 Engine의
풀링 규약(2-7)을 흉내 낼 이유가 없다.

**어떤 파형·음높이를 쓸지는 소리의 정체성이지 세기 값이 아니다.** 그래서 `CGameConfig`가
아니라 `CAudio_Manager` 안의 표(`s_dicDef`)에 상수로 둔다 — `CUI_InGame`의 플래시 색과
같은 자리(2-10-3). `CGameConfig`에는 켬/끔과 전체 볼륨만 있다. 새 효과음을 추가할 때는
`SOUND_ID`에 값 하나, 그 표에 줄 하나만 늘리면 된다 — 이미 흔들림/펀치/플래시에서 반복해
온 확장 방식과 같다(2-10-2, 2-10-3, 1-6).

지금 걸려 있는 자리는 전부 **이미 있던 손맛 훅**이다 — 새 이벤트를 뚫지 않았다.

| `SOUND_ID` | 훅 |
|---|---|
| `HIT` | `CPlayer.OnDamaged` |
| `DEATH` | `CPlayer.OnDead` |
| `EVADE` | `CPlayer.OnEvade` |
| `CAPTURE` | `CPlayer.OnCapture` |
| `CARD_READY` | `CStage_Manager.OnCardReady` |
| `STAGE_CLEAR` / `STAGE_FAIL` | `CStage_Manager.OnStateChanged` — **`m_bLastCleared` 기준**(2-7의 결과 화면과 같은 기준). 별을 하나라도 땄으면 `STAGE_STATE.FAIL`이어도 클리어 소리가 난다 |

**웨이브 하나를 깼을 때(중간 REVEAL)는 아직 소리가 없다.** `CStage_Manager`의 웨이브
상태 기계에 새 이벤트를 뚫어야 하는데, 이미 잘 도는 핵심 흐름이라 이번엔 손대지 않았다 —
필요해지면 그때 REVEAL 진입 지점에 이벤트를 하나 추가할 것.

### 2-13. 햅틱 — 절차적 패턴 (260916)
Unity 기본 API(`Handheld.Vibrate`)는 **세기 조절이 없다** — 한 번 울리거나 안 울리거나 둘뿐이다.
그래서 세기 대신 **울리는 횟수와 간격**으로 종류를 구분한다. `CSound_Utility`가 파형(음높이·
파형 종류)으로 소리의 정체성을 냈다면(2-12), `CHaptic_Manager`는 **리듬**으로 낸다 —
피격은 한 번 짧게, 사망은 세 번 끊어서 울리는 식이다.

```
CHaptic_Manager.Play(HAPTIC_ID)   첫 펄스를 즉시 울리고 남은 펄스·간격을 잰다
CGameManager.Update가 매 프레임   CHaptic_Manager.Tick(dt)를 불러 남은 펄스를 흘려보낸다
```

**코루틴을 쓰지 않는다.** `CStage_Manager`·`CAudio_Manager`처럼 `CGameManager`가 매 프레임
`Tick(dt)`을 불러 주는 순수 C# 클래스로 두어야 다른 매니저들과 같은 결로 테스트할 수 있다
(화면 없이 `CProtoTest`에서 `FIRED_COUNT`/`PULSE_REMAIN`으로 펄스가 계획대로 흘러가는지 본다 —
`Handheld.Vibrate` 자체는 **에디터에서 조용히 아무 일도 안 하므로**(`CSafeArea`가 에디터
Game 뷰에서 하는 일이 없는 것과 같은 성격, 2-10) 실기기에서만 확인된다).

**진짜 세기 조절(iOS Core Haptics / Android `VibrationEffect`)이 필요해지면 `Trigger_Pulse`
안쪽만 바꾸면 된다.** 호출부(`Play(HAPTIC_ID)`)는 그대로다 — `CAudio_Manager.Build_Clip`과
같은 자리다.

`HAPTIC_ID`는 `SOUND_ID`와 같은 사건을 가리키지만 **일부러 따로 둔 열거형**이다 — 소리는
나는데 햅틱은 없는(또는 그 반대) 조합을 나중에 열어 두기 위해서다. 지금은 정확히 같은 자리에
같이 걸려 있다(`On_PlayerDamaged` 등, 2-12의 표와 동일).

`CGameConfig`에는 켬/끔(`m_bHapticEnabled`) 하나뿐이다 — 세기 조절 자체가 없으니 볼륨 같은
값을 만들 게 없다. 옵션창 후보로만 남겨 뒀다(1-6).

### 2-14. 플레이어 체력 — 목숨 개수에서 HP 풀로 (260916)
M4(보스 콘텐츠) 설계에서 보스가 몬스터보다 더 아프게 때려야 한다는 요구가 나왔는데,
예전 `CPlayer.Damage()`는 인자가 없어 **항상 목숨 1개**만 깎았다 — 공격력이 다른 두 몬스터를
구분할 방법이 아예 없었다. 그래서 목숨 개수(`m_iLife`, -1 고정)를 HP 풀(`m_iHp`/`m_iMaxHp`,
가변 피해량)로 바꿨다.

```
CPlayer.HP / MAX_HP        남은 체력 / 최대 체력
CPlayer.Damage(iAmount)    iAmount만큼 깎는다. 무적·보호막·회피 판정은 그대로다
CPlayer.Heal(iAmount)      MAX_HP를 넘지 않게 회복한다 (전엔 상한이 아예 없었다)
CPlayer.OnHpChanged        옛 OnLifeChanged와 같은 자리 — 이름만 HP에 맞췄다
```

> 260917_**탄 피해는 `ProjectileInfo.csv`의 `iDamage`를 쓴다**(2-15). 아래는 몬스터 접촉 기준으로 읽을 것.
> 260918_**몬스터 접촉 피해도 `EnemyInfo.csv`의 `iAttack`으로 뺐다**(2-6). 예전엔 전 몬스터가
> `CStage_Manager.DEFAULT_HIT_DAMAGE`(1) 고정값을 같이 썼는데, 이제 몬스터 종류마다 다르게 줄 수 있다 —
> `Damage()` 쪽은 처음부터 가변 피해량을 받게 만들어 둬서 호출부 구조는 손댈 필요가 없었다.

자기 선분을 밟은 즉사(`STEP_RESULT.DEAD`)는 특정 몬스터가 준 피해가 아니므로
`CPlayer.SELF_TRAIL_DAMAGE`(1)라는 별도 고정값을 쓴다. 이건 몬스터 공격력과 무관하게 그대로 남는다.

`MapInfo.csv`의 `iLife` 열은 `iMaxHp`로 이름을 바꿨다 — 값이 의미하는 것이 이제 '시작 목숨
개수'가 아니라 '시작/최대 체력'이기 때문이다. `UpgradeInfo.csv`/`EquipInfo.csv`의 `HP` 스탯은
여전히 같은 자리(`Get_TotalStat(..., STAT_TYPE.HP, ...)`)에서 `CStage_Manager.Set_PlayerUpgrade`의
`iBonusHp`로 들어가 `iMaxHp`에 더해진다 — 강화/장비가 최종 수치를 만드는 흐름 자체는 그대로다.

### 2-15. 투사체 — Project_GYM BulletLogic 이식 (260917)
GYM의 탄 구조(이동 ScriptableObject + 특성 ScriptableObject + 모양별 하위 클래스 + 피격 효과)를 옮겼다.
**프리팹은 `Prefab_Projectile` 하나, 탄 종류는 `ProjectileInfo.csv` 한 줄**이다(2-6과 같은 원칙).

```
CProjectile (풀 · 그리기) ── CProjectileCore (규칙 전부, 화면 없이 테스트)
                              ├─ CProjectileShape   POINT 원 / LASER 예고선→빔 / SWEEP 맵 끝까지 뻗는 띠 / BLAST 커지는 원
                              ├─ CProjectileMove    NONE / STRAIGHT / TRACE / SPIRAL / BOOMERANG / ORBIT / SYNC
                              └─ CProjectileTrait×N REBOUND / GRAVITY_* / KNOCKBACK_* / ENTER_STUN / STAY_STUN /
                                                    HIT_STOP / SCALE_OVER_TIME / STAY_STOP / WHITE_OUT / CHAIN (+RANDOM)
맞은 대상 ── CImpactHandler   기절 · 감속 · 도트 · 번쩍임 타이머 (ImpactInfo.csv: STUN SLOW DOT KNOCKBACK EXPLODE)
```

| 파일 | 내용 |
|---|---|
| `ProjectileInfo.csv` | 모양 · 이동 · 특성(`\|`) · 탄속 · 수명 · 사거리 · 판정반경 · 크기 · 피해 · 내구 · 효과 ID(`\|`) · 조율값 |
| `ImpactInfo.csv` | 효과 종류 · 시간(`-1`이면 닿아 있는 동안) · 수치 · 참조(폭발이 부를 탄 ID) |

**조율값(`strParam`)은 `KEY:VALUE|KEY:VALUE`로 이름을 붙여 적는다**(`CCSV_Utility.To_ParamMap`).
GYM은 위치로 읽어 특성 하나를 빼면 값이 엉뚱한 특성으로 밀렸다. 키 목록:
`LASER_TELEGRAPH` `LASER_THICKEN` `LASER_FADE` `LASER_WIDTH` / `SWEEP_TIME` / `BLAST_GROW` `BLAST_SCALE` /
`TRACE_TURN` / `SPIRAL_ROTATE` `SPIRAL_EXPAND` / `BOOMERANG_OUT` `BOOMERANG_STAY` / `ORBIT_RADIUS` `ORBIT_SPEED` /
`GRAVITY_POWER` / `KNOCKBACK_DISTANCE` `KNOCKBACK_TIME` / `STUN_TIME` / `HITSTOP_TIME` / `GROW_SCALE` `GROW_TIME` /
`STOP_SLOW` / `WHITE_TIME` / `CANCEL_SHOT`(1이면 닿은 작은 적탄을 지운다 — 플레이어 탄 전용) /
`CHAIN_STUN_TIME` `CHAIN_RANGE`. 없는 키는 기본값을 쓴다.
**수명(`fLifeTime`) 0은 무한**이다 — 회전탄처럼 스킬이 직접 거두는 탄에 쓴다.

#### CHAIN — 체인 라이트닝 (260917, GYM에 없던 특성)
닿으면 기절시키고, 반경(`CHAIN_RANGE`) 안의 **아직 안 맞은** 대상 쪽으로 방향을 튼다 — `REBOUND`가 벽에서
방향을 뒤집는 것과 같은 자리(`On_Wall` 대신 `On_Enter`에서 `Set_Dir`)라 이동은 그대로 STRAIGHT가 맡는다.
그래서 화면에는 탄이 실제로 옆 적에게 날아가 맞는 것으로 보인다 — TRACE로 쓰면 안 된다. TRACE는 매 틱
`Find_Target()`(가장 가까운 적 하나, 이미 맞았어도 다시 겨눔)으로 재조준해 이 트레잇의 `Set_Dir`을 바로
덮어써 버린다.

**몇 번 튈지는 새 값을 만들지 않고 REBOUND와 같은 자리(`iDurability`)로 정한다** — 맞을 때마다 내구도가
줄어 다하면 사라진다(1-1, 같은 숫자를 두 곳에 두지 않는다).

다음 대상 찾기는 `Find_Target`과 다른 창구(`IProjectileHost.Find_ChainTarget`)를 쓴다 — 반경으로 자르고
**이미 맞은 대상(탄의 `m_hsContact`)을 제외**해야 두 대상 사이를 왕복하지 않는다. `CTargetFinder_Utility.Find`
(가장 가까운 것 하나, 제외 없음)와 `Find_Nearby`(반경 + 제외)를 분리해 둔 이유이기도 하다.

#### 닿음은 본체가 한 번만 가린다
스테이지가 매 프레임 '맞을 수 있는 대상'을 넘기면(`Update_Contact`) 본체가 **닿기 시작 · 닿아 있음 · 떨어짐**을 가린다.
**피해 · 효과 · 내구도 소모는 닿기 시작할 때 본체가 한 번** 넣고, 특성은 자기 동작만 한다.
GYM은 특성마다 피해를 다시 넣어 특성을 겹치면 피해도 겹쳤다.

- 적탄은 플레이어를, 플레이어 탄은 몬스터를 맞힌다(`PROJECTILE_SIDE`). 둘 다 `IImpactTarget`으로 받는다
- **적탄은 플레이어가 나와 있고 무적이 아닐 때만** 맞힌다 — 몬스터 충돌과 같은 규칙(2-6)
- 탄이 사라지면 닿아 있던 대상 전부에 떨어짐을 알린다. 안 그러면 속박 · 장판 감속이 영영 안 풀린다
- 레이저는 켜져 있는 동안, 폭발은 커지는 동안에만 **새로** 맞는다. 이미 닿아 있던 대상은 모양만 보고 붙잡아 둔다

#### 벽 — 화면 끝이 아니라 맵 끝과 점령지
GYM의 '화면에 맞고 튕기는' 탄은 여기서 **맵 끝**에서 튕긴다. **적탄에게는 점령지도 벽**이다 —
땅을 먹은 만큼 막아 주는 것이 이 게임의 규칙이라, 튕기는 탄도 레이저도 점령지 가장자리에서 멈춘다.
플레이어 탄은 점령지 위를 지나간다. 판정은 `CStage_Manager.Is_Wall` 한곳이다.

#### 효과는 타이머다
`CImpactHandler`가 대상마다 효과를 들고 결과만 낸다(`IS_STUNNED` · `SPEED_SCALE` · `IS_WHITE_OUT`).
GYM처럼 코루틴을 걸지 않는다 — 풀로 돌아간 몬스터에게 코루틴이 계속 돌았다.
- 같은 출처를 다시 맞으면 새로 걸지 않고 **긴 쪽으로** 늘린다
- 기절이 둘 겹쳤다가 하나만 풀려도 **남은 게 있으면 계속 기절**이다(GYM은 풀렸다)
- 감속은 겹치면 가장 센 것. 속도는 기존 갈래(환경 × 스킬 × 카드)에 **따로 곱한다**(2-11)
- **플레이어는 밀리지 않는다**(`Push`가 비어 있다). 칸을 따라 움직이므로 밀면 선이 끊겨 점령 규칙이 깨진다
- 도트는 시간이 아니라 **횟수**로 끝낸다 — 3초짜리가 float 오차로 네 번 들어가지 않게

#### GYM에서 고쳐서 옮긴 것
- ScriptableObject를 모든 탄이 공유해 탄끼리 상태가 섞였다 → 모듈을 탄마다 새로 만든다
- `Rebound.Initialize`의 조건이 뒤집혀 튕기는 탄이 초기화되지 않았다
- `StayStun` · `WhiteOut`의 Exit가 Enter를 불러 떨어질 때 피해가 한 번 더 들어갔다
- `Spiral` · `ScaleOverTime`이 '5 - 남은 수명'으로 시간을 구해 수명이 5초가 아니면 어긋났다
- `Trace`가 이름만 추적이고 직진이었다 → 초당 `TRACE_TURN` 라디안까지 휜다
- 폭발 탄 ID가 201로 박혀 있었다 → `ImpactInfo.csv`의 `iRefID`

#### 개발용 스위치 (`GameConfig.asset`, 1-6)
포수는 일반탄만 쏘고 플레이어 무기(2-11-2)는 4종만 쓰므로, 나머지 탄은 **이 스위치 없이는 화면에서 보기 어렵다.**

| 항목 | 뜻 |
|---|---|
| `m_iDevAutoFireProjectileID` | 0이 아니면 플레이어가 그 탄을 가장 가까운 몬스터에게 저절로 쏜다 (정식 무기는 2-11-2) |
| `m_fDevAutoFireCool` | 위 자동 발사 간격 |
| `m_iDevEnemyShotID` | 0이 아니면 포수가 표 대신 그 탄을 쏜다 |

#### 옮기지 않은 것
- GYM 몬스터의 이동 종류 — `MonsterInfo.csv`에 주석으로만 있고 코드는 추적 하나뿐이었다
- ~~플레이어 스킬이 탄을 쏘는 연결~~ → 260917_투사체 무기로 붙였다(2-11-2)

### 2-16. 비헤이비어 트리 — Portfolio_SoloLeveling 이식 (260917)
`03.Module/CNode.cs`(Selector · Sequence · Condition · Action · Wait)와 `CBlackboard.cs`(`CBehaviorTreeHandler` 포함).
M4 보스 패턴용 골격으로 옮겨 왔다.
- 노드가 소유자(Animator · CActor)를 모른다. 조건 · 행동은 대리자로 받는다
- `Evaluate(dt)`로 시간을 주입한다 — 화면 없이 검증한다
- Selector가 우선순위 높은 자식에게 넘어갈 때 **하던 자식을 끊는다**(원본은 OnExit가 안 불렸다)
- 3D 전투 노드(대시 · 콤보 등)는 CharacterController 전용이라 옮기지 않았다

#### 첫 연결 — 포수류(PROJECTILE)의 "사거리 유지" (260918)
`CEnemyBehaviorTree_Utility.Build_Kite`(`03.Module`)가 실제 몬스터에 트리를 붙인 첫 사례다.
`CEnemy`가 `eGimmick == PROJECTILE`일 때만 `CBehaviorTreeHandler`를 만들어 든다 — 그 외(WEB · SPAWN · NONE)는
`m_cBehaviorTree`가 `null`(또는 `Set_Tree(null)`로 트리가 빠진 상태)이라 예전처럼 `CStage_Manager`가
넘기는 노출 여부를 곧바로 배회/추적으로 쓴다.

```
CEnemy.Set_ChaseState(bExposed, vTargetPos)   CStage_Manager가 매 프레임 부른다 — 상태만 갱신
  트리 없음 → Set_MoveState(bExposed, vTargetPos)로 바로 반영(기존 동작 그대로)
  트리 있음 → Tick에서 BLACKBOARD(IS_TARGET_EXPOSED · TARGET_POS)만 채우고 실제 결정은 미룬다
CEnemy.Tick                                   트리가 있으면 여기서 Evaluate → Action 노드가 Set_MoveState를 부른다
CEnemy.Set_MoveState(bChase, vTargetPos)      실제로 배회/추적을 뒤집는 자리. 이동 규칙(CEnemyMoveHandler)은
                                               그대로 두고 '언제 쫓을지'만 갈아 끼운다
```

트리 구조는 Selector 하나다 — **노출 + 사거리(`EnemyInfo.fGimmickRange`) 밖이면 쫓고, 그 외(사거리 안 /
플레이어가 안전 지대)에는 자리를 지킨다.** 기믹(`CEnemyGimmick_Projectile`)은 노출·사거리를 스스로
다시 확인하므로(`Can_Fire`) 트리는 쏠지 말지까지 정하지 않는다 — **이동만 담당한다.**

- **예전엔 포수도 몸통 박치기 거리까지 붙었다가 쐈다.** 사거리 개념이 있는 유일한 기믹인데 이동은
  다른 몬스터와 똑같이 무조건 추적이라, 사거리를 갖고 있는 의미가 없었다. 이제 사거리 밖에서 멈춰
  버티는 "치고 빠지는" 대신 "거리를 두는" 느낌이 난다
- `BLACKBOARD_KEY.IS_TARGET_EXPOSED`를 이번에 추가했다 — 원본 키(`TARGET_POS`/`ATTACK_RANGE`)는
  이미 있었지만 "지금 노릴 수 있는 상태인가"를 담을 자리가 없었다
- 풀에서 재사용된 오브젝트가 이전 판엔 다른 기믹이었을 수 있다 — `Setup_BehaviorTree`가 매 `Initialize`마다
  다시 판단해서 `Set_Tree(null)`로 정리하거나 새 트리를 얹는다. `CBehaviorTreeHandler` 인스턴스 자체는
  `m_cImpact`처럼 재사용하고 내용만 갈아 끼운다(새 `GC.Alloc`을 매 판마다 만들지 않는다)
- `CProtoTest.Test_EnemyKiteBehavior`가 사거리 밖/안/비노출 세 갈래와, PROJECTILE이 아닌 몬스터는
  트리 없이 기존 동작 그대로인지를 화면 없이 검증한다

### 2-17. 캐릭터 시스템 — 스킨 + 스탯 배율 + 조각 레벨업 (260917~260918)
캐릭터는 **전용 스킬이 없다.** 런 스킬 풀(2-11-1/2-11-2)은 전 캐릭터 공통이고, 캐릭터마다 다른 것은
스킨(프리팹)과 스탯 배율(속도 · 체력 · 회피)뿐이다 — 캐릭터가 늘 때마다 밸런싱 비용이 같이 느는
전용 킷 구조를 피했다. 로비 "가방"의 **캐릭터 탭**에서 장착을 바꾸고, 스테이지 진입 후에는 못 바꾼다.

```
CharacterInfo.csv          프리팹 · 최대레벨 · 레벨당 속도/체력 배율 · 회피 보너스 · 조각 경제 · 성급 스켈레톤
CStageProgress             보유 캐릭터 + 레벨 + 조각 + 지금 장착한 캐릭터(스킬 레벨과 같은 자리)
CProgress_Manager          On_CharacterMapCleared / Try_LevelUpCharacter / Try_EquipCharacter
CStage_Manager.Set_Character  CGameManager가 장착 캐릭터 정보를 넘긴다 → Spawn_Player가 반영
CUI_Inventory              가방의 [캐릭터] 탭(장착 · 레벨업)
```

#### 획득 — 각성 스테이지 클리어
`MapInfo.csv`의 `iCharacterID`가 그 맵(각성 스테이지)을 클리어하면 얻거나 강화할 캐릭터다.
0이면 캐릭터를 안 주는 잔향/파밍 스테이지다. **처음 클리어하면 1레벨로 얻고 곧바로 장착한다**
(`CProgress_Manager.On_CharacterMapCleared`) — 안 그러면 그 판을 나가도 이전 캐릭터로 던전에 들어간다.

#### 강화 — 조각 + 가방의 레벨업 버튼 (260918)
**이미 가진 캐릭터를 각성 스테이지에서 다시 클리어해도 레벨이 자동으로 오르지 않는다.**
"잔향 조각"(`iFragmentPerClear`, `CharacterInfo.csv`)만 쌓이고, 가방 캐릭터 탭에서
레벨업 버튼을 눌러야(`Try_LevelUpCharacter`) 조각을 소모해 레벨이 오른다.
- 비용은 `Get_FragmentCost(iCurLevel)` = `iFragmentCostBase + iFragmentCostAdd * (iCurLevel - 1)`.
  레벨 1→2가 첫 강화라 `(iCurLevel - 1)`을 지금까지 강화한 횟수로 본다 —
  `CUpgradeInfo`/`CSkillInfo`의 `Get_Cost`(레벨 0부터 시작)와 형태는 같고 시작점만 다르다
- 만렙을 넘지 않는다. 만렙이면 비용이 0이라 `Try_LevelUpCharacter`도 항상 실패한다
- 캐릭터 탭의 행 버튼은 **`On_ClickSkill`과 같은 한 버튼 두 역할 패턴**이다 — 장착 안 된 캐릭터를
  누르면 장착, 이미 장착한 캐릭터를 누르면 레벨업을 시도한다. 새 버튼을 늘리지 않았다

#### 성급 — 화면 틀만 (260918, 스켈레톤)
`CharacterInfo.csv`의 `strStarLevel`/`strStarDesc`(예: `3|6|10`, `효과 없음|최대 체력 +5%|공격력 +10%`)로
성급 임계 레벨과 설명 텍스트만 표에 둔다. `CCharacterInfo.Get_StarTier(iLevel)`이 지금 몇 성인지
계산해 캐릭터 탭에 `★★☆` 식으로 보여 주지만 **실제 스탯/효과는 어디에도 걸려 있지 않다** — 기획이
확정되면 `Get_SpeedRate` 등과 같은 자리에 실제 배율을 추가할 것.

#### 배율 계산 — 레벨 0도 1로 클램프
`Get_SpeedRate`/`Get_MaxHpRate`/`Get_EvasionBonus`는 **레벨 0(미보유)도 레벨 1로 클램프한다** —
`CRunSkillInfo.Get_Value`(레벨 0이면 0)와 다른 의도적 차이다. 곱셈용 배율이 0이면 스탯이 사라진다.

#### 프리팹 누락 폴백
캐릭터 스킨 프리팹(`Prefab_Character_*`)이 아직 없어도(로컬 `Setup Assets` 전) 게임이 죽지
않는다 — `Spawn_Player`가 `Has_Prefab`으로 먼저 확인하고 없으면 기본 `Prefab_Player`로
대체하며 경고만 남긴다(3장 "프리팹 누락" 결정과 같은 자리). 스탯 배율은 스킨과 무관하게 그대로 적용된다.

#### 가방 — 캐릭터 탭 (260918)
`CUI_Inventory`에 `INVENTORY_TAB.CHARACTER`를 추가했다. 기존 [장비]/[스킬] 탭과 같은
목록형 UI(`Make_Row`)를 그대로 재사용한다 — 이 화면만을 위한 새 프리팹 레이아웃을 만들지 않았다.
캐릭터 탭이 스테이지 진입 캐릭터를 바꾸는 자리다. 보유한 캐릭터만 나열하고, 행 버튼이
장착/레벨업을 겸한다(위 "강화" 참고).

스크린샷 레퍼런스(초상화 + 캐러셀 + 성급 게이지가 있는 전용 캐릭터 화면)와 달리 지금은
**같은 정보를 리스트 한 줄에 욱여넣은 상태**다. 클라우드 세션은 Unity 프리팹을 만들 수 없어
코드만 먼저 준비해 둔 것 — 전용 레이아웃(초상화 패널 + 하단 캐릭터 스트립)은 로컬에서
`Setup Assets`로 새 프리팹 구조를 짤 때 다시 붙일 것.

### 2-17-1. 장비 강화 (260918)
장비도 캐릭터처럼 레벨을 갖는다 — `EquipInfo.csv`에 `iMaxLevel`/`iCostBase`/`iCostAdd`/
`fStatValuePerLevel`을 추가했다. 형태는 `CUpgradeInfo.Get_Cost`/`Get_Value`와 같다(레벨 0부터
시작, 만렙이면 비용 0). 레벨은 `CStageProgress.CItemRecord`에 필드 하나(`iLevel`)만 얹었다 —
이미 그 장비를 보유하고 있다는 레코드가 있으니 새 리스트를 만들 이유가 없었다.

```
CEquipInfo.Get_Cost(iCurLevel)      다음 레벨 코인 비용. 만렙이면 0
CEquipInfo.Get_StatValue(iLevel)    fStatValue + fStatValuePerLevel * 레벨 — 실제 적용 수치
CProgress_Manager.Try_UpgradeEquip  코인을 내고 레벨을 올린다. Get_EquipStat이 이 레벨을 바로 반영한다
```

- **소모품은 강화 대상이 아니다**(`iMaxLevel` 0). `IS_CONSUMABLE`로 한 번 더 막아 표를 잘못 적어도 안전하다
- 가방 장비 탭의 각 줄은 **행 전체(장착/해제) + 오른쪽 작은 강화 버튼**으로 나뉜다. 행 버튼과 강화
  버튼을 하나로 합치면 "장착했다가 또 눌러 강화"처럼 두 가지 뜻이 겹쳐 캐릭터 탭의 패턴을 못 쓴다.
  템플릿에 그 버튼 자리가 없어 `CUI_Card`의 썸네일처럼 런타임에 하나 더 복제해 붙였다 — 프리팹을
  새로 만들지 않기 위해서다

### 2-17-2. 카드 갤러리 — 로비의 별도 탭 (260918)
수집한 카드(웨이브 보상 이미지)는 가방이 아니라 **로비 하단 탭바의 독립된 탭**(`LOBBY_TAB.CARD`,
`CUI_Card`)이다. 장착이라는 개념이 없는 순수 감상용 화면이라 장착/강화를 다루는 가방과 성격이
달라 처음부터 분리했다.

```
LOBBY_TAB.CARD → CGameManager.Open_TabCard → CUI_Card
```

- **새 저장 데이터를 만들지 않았다** — `CProgress_Manager.Get_Star(iMapID)`(별 = 달성한 웨이브 수,
  2-7)와 `CMapInfo.Get_RevealTex(iWave)`(2-5)를 그대로 읽어, 별 개수만큼의 웨이브 보상이 이미
  드러난 것으로 본다
- 썸네일은 `CGameInstance.Get_Texture`로 얻은 텍스처를 `RawImage`로 행마다 런타임에 하나씩
  붙인다(목록 템플릿에 이미지 자리가 없어서) — 감상용이라 버튼에 클릭 동작을 걸지 않는다
- `CUI_Upgrade`/`CUI_Shop`과 같은 단일 목록 화면 구조를 그대로 따른다(안쪽 탭 없음)


---

## 3. 에디터 툴

Unity 메뉴 `Tools/LandGrab/` 아래에 있다.

| 메뉴 | 메서드 | 용도 |
|---|---|---|
| Setup Prototype | `CProtoSetup.Setup_All` | 씬까지 새로 만듦 |
| Setup Assets | `CProtoSetup.Setup_Assets` | 씬은 건드리지 않음 |
| Validate Assets | `CProtoSetup.Validate_Assets` | 프리팹/에셋 누락 검증 |
| Run Core Test | `CProtoTest.Run` | 그리드 코어 테스트 |
| Render Preview PNG | `CProtoTest.Render_Preview` | 프리뷰 PNG 출력 |

배치모드 실행 (에디터 UI 없이):
```bash
Unity.exe -batchmode -nographics -quit -projectPath "D:\Unity Project\Project_LandGrab" -executeMethod Client.CProtoTest.Run
```

> 프리팹 누락이 스테이지 전체를 죽인 적이 있다(260903 수정). 에셋을 건드렸으면 **Validate Assets를 먼저 돌릴 것.**
> Validate Assets는 260904부터 CSV(파일 존재 · 탭 구분 · 짝이 되는 파싱 클래스 · Addressable 라벨)와
> 웨이브 이미지(Read/Write 여부)까지 함께 본다.

### 3-1. 스프라이트·UI 프리팹·Addressable은 커밋되지 않는다 (260904)
`Assets/Art`의 대부분과 `Assets/Prefabs`의 UI 프리팹, 그리고 그 Addressable 등록은
**`Setup Assets`가 만들어 내는 산출물**이라 리포에 들어 있지 않다.
리포에 커밋된 Addressable 설정에는 액터 프리팹 4개와 `Prefabs` 라벨만 있다.

그래서 **clone 직후나 새 PC에서 Play를 누르면 이렇게 터진다.**

```
InvalidKeyException: No Location found for Key=...
Failed to load label: Images
Failed to load label: CSV
[CGameManager] MapInfo.csv를 읽지 못했습니다. ...
```

`Images` / `CSV` 라벨이 **존재하지 않아서** 나는 것이지 CSV 파일이 잘못된 게 아니다.
(마지막 줄이 CSV를 지목해 오해하기 쉽다.)

**Play 전에 `Tools/LandGrab/Setup Assets`를 한 번 돌리면 끝난다.**
스프라이트 생성 → UI 프리팹 생성 → 라벨 등록까지 한꺼번에 한다.

`CGameManager.Check_AssetsReady`가 이 상황을 먼저 잡아 한 줄로 알려 준다 —
Addressable 예외 더미를 읽기 전에 이 메시지부터 볼 것.

### 3-2. 임시 사본에서 프리팹을 만들었으면 `.meta`까지 확인할 것 (260905)
Unity가 열려 있어 배치모드가 막히면 프로젝트를 통째로 복사해 거기서 `Setup Assets`를 돌리게 된다.
이때 **`.cs.meta`를 함께 복사하지 않으면 사본이 GUID를 새로 발급**하고,
거기서 만든 프리팹은 원본 프로젝트에 없는 GUID를 가리킨 채 돌아온다.

증상은 컴파일 에러도, 프리팹 없음도 아니다 — 프리팹은 로드되는데 **루트에 컴포넌트가 없어서**
Engine 안쪽에서 터진다.

```
NullReferenceException
  at Engine.CLayer.Reuse_GameObject   ← cGameObject.Initialize(Desc)
  at Client.CGameManager.Open_Lobby
```

`Has_Prefab`은 통과하므로 우리 쪽 가드에 걸리지 않는다.
`Tools/LandGrab/Validate Assets`는 이걸 잡는다(`FAIL  CUI_Lobby 컴포넌트 없음`) —
**사본에서 만든 산출물을 되돌려 놓았으면 원본 프로젝트에서 한 번 더 검증할 것.**

확인은 프리팹 안의 `m_EditorClassIdentifier`(`Assembly-CSharp::Client.CUI_Lobby`) 바로 위
`m_Script` GUID가 `Assets/Script/01.UI/CUI_Lobby.cs.meta`의 것과 같은지 보면 된다.


---

## 4. Git / 원격 작업

- 원격: `https://github.com/LimBellHoon/Project_LandGrab.git` (private, 기본 브랜치 `main`)
- 추적 파일 156개 / 약 1MB. **Git LFS 불필요**, 대용량 파일 없음 → clone이 빠르다.
- `Library/`는 추적하지 않음 — 새 PC에서 첫 Unity 실행 시 재생성(오래 걸림).
- `.meta` 파일은 **항상 짝으로 커밋**할 것. 빠지면 GUID가 재생성되며 참조가 끊긴다.

### 다른 PC / 클라우드 세션에서 작업할 때
```bash
git clone https://github.com/LimBellHoon/Project_LandGrab.git
```
클라우드 세션이나 GitHub Actions 환경에는 **Unity 에디터가 없다.** 따라서 원격에서 신뢰할 수 있는 것은:

- ✅ `.cs` 로직 수정, 리팩터링, 코드 리뷰, 문서 작업
- ❌ 프리팹/씬 편집, 에디터 툴 실행, 컴파일·플레이 검증

컴파일과 동작 확인은 반드시 Unity가 설치된 PC에서 할 것.

> ⚠️ 과거에 리포 안에 다른 리포(`Project_FG`)가 서브모듈 gitlink로 섞여 들어간 사고가 있었다(260903 제거).
> 프로젝트 폴더 안에서 다른 저장소를 clone하지 말 것.
