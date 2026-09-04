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
    ├── 01.UI/              CDebugHUD
    ├── 02.GameObject/      CPlayer, CEnemy  (Engine.CGameObject 상속)
    ├── 03.Module/          CTerritoryGrid, CGridRenderer, CMoveHandler, CEnemyMoveHandler, CInputHandler
    ├── 97.Data/            CCSVData_EnemyInfo, CCSVData_MapInfo, CCSV_Utility
    ├── 98.Manager/         CStage_Manager
    └── 99.Defines/         Client_Enum, Client_Desc

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

#### 맵 모양 마스크
`strShapeMask` 텍스처의 밝은 픽셀만 플레이 가능한 칸이 되고, 나머지는 `CELL_STATE.BLOCK`이 된다.
BLOCK은 플레이어·몬스터 모두 못 들어가고 점령률 분모에서도 빠진다. `-`이면 직사각형 전체.

### 2-6. 스테이지 수명 주기
`CStage_Manager.Tick`은 `STAGE_STATE.PLAYING`일 때만 규칙을 돌리지만,
**플레이어·몬스터의 `Tick`은 Engine의 레이어가 직접 돌린다.** 스테이지 상태만 바꿔서는 액터가 멈추지 않는다.
그래서 `Set_State`가 CLEAR/FAIL로 넘어갈 때 `Set_LayerTimeScale(PLAYER/ENEMY, 0)`으로 두 레이어를 세우고,
`Start_Stage`/`Release`에서 1로 되돌린다 (260904). 액터를 멈춰야 하는 기능은 이 경로를 쓸 것 — 별도 정지 플래그를 만들지 말 것.

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
