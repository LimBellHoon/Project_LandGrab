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
    ├── 01.UI/              CDebugHUD, CUI_StageSelect, CUI_InGame, CUI_Popup (Engine.CUI 상속)
    ├── 02.GameObject/      CPlayer, CEnemy, CProjectile, CWeb  (Engine.CGameObject 상속)
    ├── 03.Module/          CTerritoryGrid, CGridRenderer, CMoveHandler, CEnemyMoveHandler
    │                       CInputHandler, CVirtualJoystick
    │                       CEnemyGimmick(+_Projectile/_Web/_Spawn)
    ├── 97.Data/            CCSVData_EnemyInfo, CCSVData_MapInfo, CCSV_Utility, CStageProgress
    ├── 98.Manager/         CStage_Manager, CProgress_Manager
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
| `MapInfo.csv` | `CCSVData_MapInfo` | 맵 크기·플레이어 속도·모양 마스크·이미지 스택·웨이브 구성 |

#### Engine이 강제하는 CSV 규약 (어기면 표가 조용히 비어 버린다)
- **구분자는 탭(`\t`).** 쉼표가 아니다. `Engine.CCSVData`가 `Split('\t')`로만 쪼갠다.
- **0번 줄이 헤더.** 헤더 위에는 주석도 넣을 수 없다.
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
웨이브를 넘길 때 `CTerritoryGrid.Reset`으로 판을 새로 깔고 두 장을 갈아 끼운다.

`strWaveEnemy`는 웨이브를 `|`, 웨이브 안의 몬스터를 `,`, 개수를 `*`로 적는다
— `101*3|101*2,102*2|102*2,103*1,104*1`.

#### 렌더링
`CGridRenderer`는 두 SpriteRenderer를 쓴다 — 아래에 드러날 이미지(reveal), 위에 가림막(cover).
가림막은 원본을 셀 격자에 맞춰 다시 찍은 마스크 텍스처이고 점령한 칸만 알파 0으로 뚫는다.
그래서 **가림막·모양 마스크로 쓰는 텍스처는 임포트 설정에서 Read/Write Enabled가 켜져 있어야 한다.**
셀 하나는 `PIXEL_PER_CELL`(현재 4)픽셀로 찍는다.

#### 보상 공개 연출 (260904)
웨이브를 넘길 때 바로 갈아 끼우지 않는다. **드러나는 순간이 이 게임 재미의 전부**라
그냥 툭 바꾸면 남는 게 없기 때문이다. `CStage_Manager`가 3단계로 돌린다.

```
REVEAL(0.5초)  가림막 알파 1→0   보상이 전부 드러남
HOLD  (0.9초)  그대로 유지        감상할 틈
COVER (0.5초)  알파 0→1          다음 가림막이 덮임 (그 사이 Reset + 텍스처 교체)
```

마지막 웨이브면 COVER 없이 **드러난 채로 CLEAR**로 끝난다.
연출 중에는 액터를 세우고 제한 시간도 멈춘다 — 연출 때문에 시간을 잃으면 억울하다.

알파는 마스크 텍스처를 다시 찍지 않고 **`SpriteRenderer.color`만** 건드린다
(`CGridRenderer.Set_CoverAlpha`). 매 프레임 픽셀을 다시 올리면 모바일에서 감당이 안 된다.

#### 맵 모양 마스크
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
| `PROJECTILE` | 발사주기 | 탄속(셀/초) | 사거리(셀) | 탄 수명 | — |
| `SPAWN` | 소환주기 | 소환 마리수 | — | — | 소환할 몬스터 ID |

- 투사체·거미줄은 `OBJECT_TYPE.ENEMY_EFFECT` 레이어에 올라간다.
  스테이지가 끝날 때 이 레이어도 함께 세워야 탄이 계속 날아가지 않는다(2-7).
- 거미줄만 플레이어가 안전 지대에 있어도 계속 깔린다. 나머지는 플레이어가 나와 있을 때만 발동한다.
- `SPAWN`의 `RefID`가 다시 `SPAWN` 몬스터를 가리키면 무한히 늘어나므로
  `CStage_Manager.MAX_ENEMY`(32)로 총량을 막는다.

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
ID 산술이 아니라 표의 순서를 본다. 기획이 중간에 맵을 끼워 넣어도 ID를 다시 매기지 않아도 된다.
`CGameManager`의 인스펙터 `m_bDebugUnlockAll`을 켜면 규칙을 무시하고 전부 열린다.

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
