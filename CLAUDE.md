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
    │                       CPolygon_Utility(260923_다각형 판정 헬퍼), CTrailMesh_Utility(260923_선 띠 메시)
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
> 260923_**다각형 방식으로 바꿨다**(예전 결정 "그리드 마스크 + 플러드필, 폴리곤 아님"을 뒤집었다).
> 칸 단위로는 사선이 계단, 나선이 네모가 되어 **그은 모양 그대로 먹을 수 없었다.** 이제 선과 점령지가 완전히 매끈하다.

**점령지는 다각형, 선은 꺾은선이다.** 불리언 연산은 `Assets/Plugins/Clipper2`(Clipper2 2.0.0, Boost 라이선스, 소스 그대로)가 한다.

- `CTerritoryGrid`가 점령지 다각형(`RINGS`, 짝홀 규칙)과 트레일 꺾은선(`TRAIL_PIECES`)을 소유한다.
  좌표는 **그리드 공간**(1 = 칸 하나, 칸 (x,y)는 [x,x+1)×[y,y+1)) — 월드와는 `Grid_ToWorld` · `World_ToGrid`로 오간다
- **칸 배열은 이제 사본이다.** 점령지가 바뀔 때마다 칸 가운데 점으로 다시 찍어 둔다(`Get_Cell` · `Try_Find_NearestCell`).
  스폰 자리 찾기처럼 '대충 어느 칸이 빈 땅인가'만 알면 되는 곳이 쓴다. **판정(점령 · 벽 · 픽업 · 가둬 잡기)은 다각형으로 한다**
  (`Is_OwnedPoint` · `Is_EmptyPoint` · `Is_PlayablePoint`). 점령률(`OWNED_RATIO`)도 넓이로 잰다
- **`Step_To(from, to)`가 이동 규칙의 단일 진입점**이다. 한 걸음(선분)의 판정(안전/선긋기/점령/사망)이 전부 여기서 나오고
  결과를 `STEP_RESULT`로 돌려준다. 규칙을 바꿀 일이 생기면 이 함수를 고칠 것.
  `CMoveHandler`가 한 프레임 이동을 선분으로 넘긴다 — 자기 선(마지막 조각 빼고)을 가로지르면 DEAD, 점령지에 들어가면 CAPTURE
- `Capture`는 선을 아주 가는 띠(반폭 0.01칸)로 부풀려 점령지와 합친 뒤, 플레이 영역에서 빼고 남은 조각들 중
  **가장 넓은 조각 하나만 남기고 전부** 점령한다(조각 = 바깥 윤곽 + 그 안의 구멍).
  260920_예전에는 **몬스터가 서 있는 영역을 점령에서 뺐다.** 그런데 몬스터는 계속 돌아다니므로
  애써 가둔 도형이 아무 설명 없이 점령되지 않는 일이 잦았다 — 가두는 것이 오히려 손해였다.
  이제 가두면 무조건 먹고, **그 안에 갇힌 몬스터는 죽는다**(`CStage_Manager.Kill_EnemiesInOwned`).
  점령이 곧 공격 수단이 됐다 — 죽은 자리에는 확률로 아이템이 그대로 떨어진다(2-20).
  **누가 죽는지는 그리드가 모른다** — 그리드는 칸만 알고, 몬스터를 들고 있는 스테이지가 죽인다.
- 플레이어는 점령지 **경계선 위에서만** 안전하다. 점령지 내부는 통과 불가(260902 결정).
- 260924_**몬스터가 '선'에 닿아도 더는 즉시 피해가 아니다 — 도화선에 불이 붙는다**(2-14-1).
  `Step_To`는 도화선이 타는 동안(`IS_TRAIL_BURNING`) 안전 지대로 돌아와도 점령시키지 않는다 —
  트레일만 지우고 `SAFE`로 끝난다("불보다 먼저 오면 선만 잃는다"). 규칙 단일 진입점(`Step_To`)은
  여전히 하나이고, 새 `STEP_RESULT`를 만들지 않았다 — 판정만 갈라 넣었다.
- 260923_**내 땅 위에서는 경계선을 따라 미끄러진다**(`CMoveHandler.Move_Safe`). 경계가 사선 · 곡선이어도 4방향 캐릭터가
  탈 수 있게, 누른 방향과 가장 잘 맞는 변(각도 약 75도 이내)을 따라간다. 변 방향과 직각이라 탈 변이 없으면 선다.
  **누른 방향으로 0.12칸 앞이 확실히 빈 땅이면 거기서 나가 선을 긋는다**(`Can_Exit`) — 볼록한 모서리 끝에서 계속 누르면 그대로 나간다.
  꼭짓점 0.1칸 안에서 꺾으면 옆 변으로 갈아탄다(모서리 코앞에서 걸리지 않게)
- 260923_**누른 방향이 변과 직각이면 돌던 방향으로 계속 간다**(`m_iFollowSign` — 260902_선분 자동 추적을 다각형으로 옮긴 것).
  "아래로 가려고 누르고 있으면 좌우 경계도 선을 따라 알아서 간다"가 이것이다. 다각형으로 갈아탄 뒤 빠져 있어
  안쪽을 누르면 그 자리에 서 버렸는데, 조작이 크게 불편해져 되살렸다. 따라가다 그 방향으로 나갈 수 있게 되면
  `Can_Exit`이 받아 저절로 나간다. 아직 한 번도 안 움직였으면(돌던 방향이 없으면) 예전처럼 선다
- 260923_**넓이가 없다시피 한 도형(1칸 미만)은 점령이 아니다**(`CTerritoryGrid.MIN_CAPTURE_AREA`). 선만 사라지고 `SAFE`로 끝난다 —
  도화선에 쫓겨 돌아온 것과 같은 자리다. 8방향 캐릭터가 나가자마자 살짝 되돌아오면 넓이 0짜리 도형이 계속 닫혀
  **점령 연출 · CHAIN 자막이 도배되고, 그때마다 경계로 되돌려져 "나갈 수가 없는" 것처럼 보였다**
- 260921_**선을 긋는 중에도 손을 떼면 멈춘다.** 예전에는 미점령 지대에서 멈출 수 없어 가던 방향으로 저절로 나아갔다 —
  조작하지 않은 움직임으로 죽는 일이 잦았다. 뒤로 꺾는 입력(나아가던 방향과 120도 넘게 어긋남)은 무시하고 그 자리에 선다
- 260920_**점령 직후에는 가장 가까운 경계로 되돌린다**(`CMoveHandler.Snap_ToBoundary`).
  방금 그은 선이 점령지 '안쪽'이 되는 일이 잦은데, 내부에 갇혔을 때
  빠져나가라고 열어 둔 예외가 있어서 — **내부에 서 있는 동안 월보(런 스킬)를 공짜로 얻은 것처럼**
  마음대로 돌아다닐 수 있었다. 월보를 그냥 주면 그 런 스킬의 값어치가 사라지므로 선으로 되돌리는 쪽을 골랐다.
  그 예외는 안전망으로만 남는다(되돌릴 경계를 못 찾는 극단적인 맵에서 갇히지 않게).
- 260920_**맵 한가운데 '시작 섬'에서 시작한다.** `MapInfo.csv`의 `iStartRadius`(현재 5 → 11x11칸)만큼
  점령된 채로 시작하고, 플레이어는 그 섬의 **아래 경계**에 선다(섬 한가운데는 '내부'라 한 칸도 못 움직인다).
  - **외벽은 이제 점령지가 아니다**(`iBorderThick` 0). 그래서 **벽을 찍어도 도형이 닫히지 않는다** —
    반드시 내 땅(점령지)으로 돌아와야 점령된다. 예전에는 외곽 테두리가 점령지라 벽을 따라 한 번에
    크게 긋고 벽에 붙이기만 하면 끝나서 판이 순식간에 끝났다
  - 가운데에서 시작하면 어느 방향으로 나가든 **돌아올 거리**가 생긴다 — 그 왕복이 이 게임의 긴장이다
  - `iBorderThick`은 0을 허용한다. 둘 다 0인 맵은 안전 지대가 없으니 하나는 반드시 줄 것
- 260921_**시작 섬 자리는 웨이브마다 슬롯으로 고른다**(`CStage_Manager` 시작 위치 슬롯). 늘 가운데에서 시작하면 판이 늘 같다.
  - 웨이브를 시작하기 전(1웨이브는 스테이지 진입 직후, 이후는 COVER 연출 끝)에 후보 10곳을 뽑아 **실제로 섬을 깔아 보이며**
    0.08초마다 옮겨 다닌다. 화면에는 **TAP TO START**가 깜빡이고 카메라는 맵 전체를 본다(`CCameraFitter.Set_Overview`)
  - 아무 데나 누르면(키보드는 스페이스 · 엔터) 다섯 칸 더 넘기며 느려지다 멈추고, 그 자리에서 시작한다. 넘길 때 딸깍, 멈추면 한 음 + 펀치줌
  - 후보는 섬 둘레 한 칸까지 맵 안이고(`CTerritoryGrid.Can_PlaceStartArea`), 몬스터와 섬 가장자리 4칸 이상 떨어진 곳만.
    후보끼리는 섬 하나 넓이만큼 떨어뜨린다 — 옆 칸으로만 옮겨 다니면 도는 느낌이 안 난다
  - 도는 동안 액터 · 제한 시간은 멈춰 있다. 웨이브 시작 아이템은 자리를 정한 **뒤에** 깐다(먼저 깔면 섬 밑에 깔려 공짜로 먹힌다)
  - 시작 섬 자리는 `CTerritoryGrid.START_CENTER`가 기억한다 — `Reset(테두리, 반지름, 가운데)`로 옮긴다
- `CGridRenderer`는 점령지 다각형을 마스크 텍스처에 찍어 가림막을 뚫는다. **셰이더 없음.**
  260923_픽셀 한 줄을 네 줄로 나눠 다각형과 만나는 구간을 재 **가장자리를 반투명으로 부드럽게**(안티앨리어싱) 찍는다.
  점령지가 바뀔 때(`IS_DIRTY`)만 통째로 다시 찍는다 — 선을 긋는 것만으로는 다시 찍지 않는다.
  **선은 텍스처가 아니라 띠 메시로 따로 그린다**(`CTrailMesh_Utility`, 굵기 0.55칸, 타는 구간은 빼고 불꽃 머리를 붙인다).
  260923_처음에는 `LineRenderer`를 썼는데 **구간이 굵기보다 짧으면 이음매가 무너진다** — 한 프레임에 0.15칸씩 긋고
  굵기가 0.55칸이라 꺾은 직후 서너 프레임이 늘 그 상태라, 꺾을 때마다 선이 한순간 일그러져 보였다.
  지금은 **구간마다 사각형 하나 + 꺾인 자리에 삼각형 하나**로 직접 만든다 — 구간이 아무리 짧아도 모양이 그대로다.
  재질도 가림막 재질을 그대로 쓰지 않고 **흰 1x1로 갈아 끼운다**(안 그러면 선에 맵 그림이 늘어나 붙는다).
  기하 계산은 화면을 몰라 `CProtoTest`가 짧은 구간 · 날카로운 각으로 검증한다(뒤집힘 · 납작함 · 삐져나감). 자세한 건 2-5 참고.

### 2-4. enum (`99.Defines/Client_Enum.cs`)
```csharp
CELL_STATE : byte   EMPTY=0(미점령/위험), OWNED=1(점령/안전), TRAIL=2(선분), BLOCK=3(맵 밖)
MOVE_DIR            NONE=-1, UP=0, DOWN, LEFT, RIGHT
STEP_RESULT         SAFE, DRAW, CAPTURE, DEAD
STAGE_STATE         READY, PLAYING, CLEAR, FAIL
ENEMY_GIMMICK       NONE, WEB(거미줄), PROJECTILE(투사체), SPAWN(부하 소환), GNAW(땅 갉기)
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
| `GachaInfo.csv` | `CCSVData_GachaInfo` | 장비 뽑기 — 비용 · 중복 환급 비율 (2-17-3) |
| `CharacterInfo.csv` | `CCSVData_CharacterInfo` | 캐릭터 — 스킨 · 스탯 배율 · 이동 방식 · 카드 목록 (2-17, 2-22, 2-17-2) |
| `CaptureRewardInfo.csv` | `CCSVData_CaptureRewardInfo` | 점령 재화 배율 — 한 번에 닫은 도형의 맵 대비 비율 구간별 (2-18) |
| `FieldItemInfo.csv` | `CCSVData_FieldItemInfo` | 맵 위 상호작용 아이템 — 종류 · 가중치 · 수치 (2-20) |
| `AccountLevelInfo.csv` | `CCSVData_AccountLevelInfo` | 계정 레벨 — 필요 경험치 · 하트 최대치/회복 · 속도 · 회피 (2-23) |

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
웨이브를 넘길 때 가림막 두 장을 갈아 끼우고 **점령한 칸은 비운다**(아래 "웨이브가 넘어가면 판을 다시 깐다" 참고).

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

#### 웨이브가 넘어가면 판을 다시 깐다 (260920, 260912 결정 뒤집음)
`Enter_Wave`가 **`CTerritoryGrid.Reset`으로 점령한 칸을 전부 비우고 플레이어를 시작 칸에 다시 세운다.**
화면에서 바뀌는 것은 '드러난 보상 위에 새 가림막이 덮인다'뿐이고, 그 그림을 처음부터 다시 벗겨 낸다.

260912에는 점령을 유지한 채 이어서 했는데, 그러면 **뒤 웨이브가 '조금만 더 먹으면 끝'이 되어**
새 그림을 드러내는 맛이 없었다 — 누적 0.6 / 0.65 / 0.7이면 2웨이브는 5%만 더 먹으면 끝난다.

- **점령률이 0으로 돌아가므로 `strWaveClearRatio`는 이제 웨이브마다 다시 재는 비율**이다(0.6 / 0.65 / 0.7).
  제한 시간(`strWaveTimeLimit`)도 웨이브마다 다시 채워진다 — 둘을 같이 보고 조절할 것
- **카드 문턱 기록(`m_iRatioStep`)도 같이 지운다** — 안 그러면 점령률이 0으로 돌아가 2·3웨이브에는
  카드가 아예 안 나온다. `s_arrCardThreshold`(2-21)가 웨이브마다 다시 처음부터 돈다
- **몬스터는 거두지 않는다.** `Spawn_Enemies`는 여전히 **모자란 종류만 그 차이만큼** 넣고(`Count_Enemy`),
  표에 적힌 수보다 많아도 줄이지 않는다 — 웨이브는 심해지기만 한다는 것이 기획이고,
  방금까지 피하던 몬스터가 증발하면 오히려 혼란스럽다
- 판을 다시 깔아도 **연출 순서는 그대로**다 — REVEAL·HOLD로 보상을 다 보여 준 뒤 HOLD 끝에서
  `Enter_Wave`가 판을 비우고, 그 위를 COVER가 덮는다. 연출 중에는 액터가 멈춰 있어 재배치가 보이지 않는다

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
| `GNAW` | 갉는 주기 | 한 번에 갉는 칸 수 | 닿는 거리(셀) | — | — |

#### 땅 갉는 자 `GNAW` (260921)
**먹은 땅도 영원하지 않다.** 내 땅 가장자리를 도로 빈 땅으로 되돌려 점령률을 깎는다(웨이브 목표가 멀어진다).
- 플레이어를 쫓지 않고 **가장 가까운 내 땅으로 간다**(`CEnemyGimmick.Try_Get_MoveTarget` — 기믹이 이동 목표를 대신 낸다).
  몸에 닿으면 죽는 것은 다른 몬스터와 같다. 대처법은 **가둬서 죽이기** — 내 땅에 붙어 있으니 가두기 쉽다
- 규칙은 그리드에 있다(`CTerritoryGrid.Erode_Near`). 260923_**닿는 거리 안의 가장 가까운 경계 점을 동그랗게 한 입** 뜯는다
  (넓이 ≈ 한 번에 갉는 칸 수). 가장자리만 먹으므로 안쪽부터 구멍이 나지 않는다
- **플레이어 둘레 2칸은 지킨다**(`CStage_Manager.GNAW_PROTECT_RADIUS`) — 발밑이 사라지면 선을 긋지도 않았는데 빈 땅에 서 버린다.
  나가 있는 사이 돌아갈 자리(`m_vLastSafePos`)가 갉혔으면 죽었을 때 가장 가까운 경계에서 다시 시작한다
- 거미줄처럼 플레이어가 안전 지대에 있어도 갉는다 — 쉬는 동안 땅이 줄어야 압박이 된다

- 260922_**몬스터는 맵 밖을 직접 벽으로 본다**(`CTerritoryGrid.Is_WorldInside`). 예전에는 맵 밖을 테두리 칸으로 끌어당겨
  그 테두리가 점령지라 저절로 막혔는데, 외벽을 점령지에서 뺀 뒤로(2-3) 몬스터가 맵 밖으로 나갔다. 이미 나가 있던 것은 가장 가까운 빈 땅으로 돌아온다
- 투사체·거미줄은 `OBJECT_TYPE.ENEMY_EFFECT` 레이어에 올라간다.
  스테이지가 끝날 때 이 레이어도 함께 세워야 탄이 계속 날아가지 않는다(2-7).
- 거미줄만 플레이어가 안전 지대에 있어도 계속 깔린다. 나머지는 플레이어가 나와 있을 때만 발동한다.
- `SPAWN`의 `RefID`가 다시 `SPAWN` 몬스터를 가리키면 무한히 늘어나므로
  `CStage_Manager.MAX_ENEMY`(32)로 총량을 막는다.
- 260917_`PROJECTILE`의 탄속 · 수명 · 사거리는 **탄 표(2-15)로 옮겼다.** 몇 발을 어떻게 뿌릴지는
  `EnemyInfo.csv`의 `eFirePattern` · `iFireCount` · `fFireAngle` · `fFireInterval`이 정한다 —
  탄 자체가 아니라 쏘는 쪽의 성질이기 때문이다.
- 260918_**몬스터별 체력도 CSV로 뺐다.** `EnemyInfo.csv`의 `iHp`가 그 몬스터의 최대 체력이다.
  값이 0 이하면(표를 안 채웠거나 `CProtoTest`처럼 손으로 만든 Desc) `CEnemy`의 고정값(체력3)으로 대체된다.
  같은 날 넣었던 공격력(`iAttack`)은 목숨제로 돌리며 없앴다 — 무엇에 맞든 목숨 하나다(2-14).

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
- 260923_**WASD/방향키 · E·J(스킬) · 스페이스·엔터(시작 탭 확인, 2-3)는 전부 `#if UNITY_EDITOR`로
  묶여 있다.** 모바일 빌드에는 물리 키보드가 없어 원래도 눌릴 일이 없었지만, "빌드에는 아예 안 들어간다"를
  분명히 하려고 컴파일 단계에서 뺐다 — 화면 조이스틱·버튼은 이 분기와 무관하게 항상 살아 있다
  (`CVirtualJoystick`은 구 `Input`을 직접 읽을 뿐 이 매크로를 타지 않는다). 마우스 클릭은 에디터에서
  터치를 대신하는 용도라 그대로 뒀다 — 빼야 하는 건 "누를 게 없는데 편의로 넣어 둔 키보드 단축키"쪽이다
- **누른 자리가 중심이 되는 플로팅 방식.** 세로 화면에서 엄지가 닿는 자리가 매번 다르다.
- 화면 **아래 60%** 에서 눌러야 잡힌다. 위쪽은 나중에 붙을 버튼용으로 비워 둔다.
- 반경·데드존은 픽셀이 아니라 **화면 높이 비율**(260922_6% / 그 25% — 12%는 화면 대비 너무 컸다)이라 해상도가 달라도 같은 느낌이 난다.
- 260922_조이스틱은 **반투명**이다(바탕 0.35 · 손잡이 0.6, `CUI_InGame`의 상수). 누른 자리에 떠서 그 아래 맵을 가리면 안 된다.
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
| `m_bUnlockAllCharacter` | 260920_캐릭터를 전부 가진 것으로 친다 (이동 방식 비교용, 2-22) |
| `m_eDevMoveStyle` | 260920_장착 캐릭터와 무관하게 이동 방식을 덮어쓴다 (2-22) |
| `m_bDebugHudVisible` | 260921_좌상단 디버그 글자(`CDebugHUD`)를 처음부터 띄울지. 플레이 중에는 **F1**로 켜고 끈다 |
| (단축키) **F2** | 260921_하트 +10(`CGameManager.Tick_DebugKey` → `CProgress_Manager.Add_Stamina`). 최대치를 넘겨도 된다 — 넘친 동안은 회복이 멈추고 쓰는 만큼만 준다 |
| `m_bFreeStamina` | 260921_하트가 모자라도 들어가고 줄지 않는다 (2-23) |

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
- 260920_**판이 커질수록 시야가 넓어진다**(`CCameraZoomOut`). 둘을 **더해서** 목표 배율을 만든다 —
  둘 다 "지금 화면에 담아야 할 것이 늘었다"는 같은 뜻이라 한 모듈에 뒀다.
  - **그리는 선 길이** — 칸당 `m_fTrailZoomPerCell`(1.2%). 멀리 나갔을 때 돌아올 길과 쫓아오는 몬스터를
    같이 봐야 판단이 되는데, 시야가 고정이면 화면 밖에서 잘려 억울하게 죽는다
  - **점령률** — `m_fZoomPerOwnedRatio`(100% 점령에서 +50%). 내 땅이 넓어질수록 '어디를 더 먹을지' 고르는
    판이 커지는데, 좁은 시야로는 남은 땅의 모양이 안 보여 길을 못 고른다
  - 합쳐도 `m_fTrailZoomMax`(1.8배)를 넘지 않는다. 선을 거두면(점령 · 사망) 점령률 몫만 남는다
  - **펀치줌과 같은 자리에서 크기에 곱하므로 둘이 서로를 몰라도 겹친다**(2-10-3).
    한 칸 바뀔 때마다 튀지 않게 `SmoothDamp`로 따라붙는다 — 판정은 모듈, 적용은 `Tick_Camera`다(화면 없이 테스트)

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

### 2-10-1. 보상 카드 — 3지선다 (260912)
> 260920_점령률 대신 조각 게이지로 여는 방식을 잠깐 썼었지만(2-21), **260923_다시 점령률 자동 트리거로
> 되돌렸다.** 아래는 그 기준대로 다시 정확하다 — "점령률이 지점을 넘으면"을 그대로 읽으면 된다.

고른 효과는 **그 판이 끝날 때까지만** 유지된다 — 스테이지를 나가면 사라진다.

```
CStage_Manager.Check_CardReady  점령률이 s_arrCardThreshold의 지점을 하나 더 넘었나 → OnCardReady (2-21)
CGameManager.On_CardReady       판을 세우고 CUI_CardPick을 연다
CUI_CardPick                    셋을 보여 주고 고른 것을 돌려준다
CStage_Manager.Apply_Card       효과를 건다
```

- **화면을 여는 것은 `CGameManager`다.** 스테이지가 UI를 직접 열면 화면 전환이 두 군데로 갈라진다(2-7)
- **고르는 동안 판을 세운다.** 카드를 보는 사이에 맞으면 고르는 재미가 아니라 벌이 된다
- **웨이브 판정보다 카드 판정이 먼저다.** 순서가 반대면 판이 넘어가며 점령률이 0으로 돌아가 카드를 영영 못 받는다
- 이미 지나친 지점은 다시 주지 않는다(`m_iCardGiven`)
- 260921_**다시 뽑기 · 버리기**(`MapInfo.csv`의 `iPickReroll` · `iPickBanish`, 판 전체에서 센다). 카드 아래 버튼 둘.
  다시 뽑기는 세 장을 새로 뽑는다. 버리기는 누르면 '버릴 카드 고르기' 상태가 되고, 고른 카드는 **판이 끝날 때까지 안 나오며**
  그 자리를 화면에 없는 새 선택지로 채운다. 같은 선택지인지는 `CPickOption.KEY`로 가린다(레벨은 안 본다 — 버린 스킬은 몇 레벨이든 안 나온다).
  뽑기는 `CGameManager.Build_PickOptions` 한곳을 지나고, `CPickOption_Utility.Pick`의 `fnExclude`가 버린 것 · 떠 있는 것을 뺀다
- 260921_**한 번에 한 장만 연다**(`CPickQueue`). 큰 도형을 한 번에 닫으면 점령률 문턱을 여러 개 한꺼번에
  넘을 수 있는데, 예전에는 창을 연달아 열어 앞 창이 남아 입력을 전부 막았다. 넘친 만큼 쌓아 두고, 고른 뒤
  `CStage_Manager.Close_Pick`이 다음 창을 연다. 선택지가 없어 못 열었을 때도 `Close_Pick`을 불러야 대기열이 막히지 않는다
- 같은 카드가 한 번에 두 장 나오지 않는다(`Pick_Random`). 고르는 재미는 서로 다른 선택지에서 나온다
- 가중치(`iWeight`)가 0이면 안 나온다 — 카드를 지우지 않고 잠글 때 쓴다
- 260923_**사냥형 8종이 들어갔다**(`Docs/Design_Card_Pool.md` 1장 초안 그대로 반영 — 나머지 세 갈래는
  아직 초안 상태). 몸으로 부딪히는 것을 공격 기회로 바꾸는 카드들이다. 수치를 든 곳이 갈린다 —
  플레이어 쪽 수치(`THORN_DAMAGE` · `KNOCKBACK_SCALE` · `BODY_DAMAGE_SCALE` · `FEAST_HEAL` ·
  `TAUNT_EXTRA_HIT`)는 `CPlayer`가, 몬스터를 직접 건드리는 수치(`HUNT_LOOT`의 드롭 확률 가산,
  `HUNT_EXECUTE`의 스플래시 반경, `HUNT_MARK`의 감속)는 `CStage_Manager`가 든다 —
  "그리드는 칸만 알고 몬스터는 스테이지가 본다"(2-3)와 같은 구분을 카드에도 그대로 적용했다.
  전부 몸 충돌 처리부(`CStage_Manager.Tick_Enemy`의 `bHit` 분기) 한곳에서 갈린다 — 카드가 없으면
  수치가 기본값(0 또는 1)이라 아무 일도 안 하므로 새 분기를 늘리지 않았다. `HUNT_MARK`는 새
  `ImpactInfo.csv` 행을 만들지 않고 `CImpactInfo`를 코드에서 즉석으로 만들어 기존 `CImpactHandler`에
  건다(같은 인스턴스를 계속 재사용해 "같은 출처면 긴 쪽으로 갱신"이 그대로 적용된다).
  카드 아이콘은 `CProtoSetup.Is_IconInk`에 모양을 8개 더했고, 그 뒤에 오던 런 스킬 아이콘 자리
  (`RUN_SKILL_ICON_KIND_START`)도 5 → 13으로 밀었다 — 로컬에서 `Setup Assets`를 다시 돌려야 아이콘이 나온다

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

#### 카드 생김새 — 레이아웃은 하나, 색만 셋 (260920)
카드는 **색과 아이콘이 먼저 읽히게** 만든다. 글자만으로는 순간적으로 고르기 어렵다.
그래서 **모든 카드가 똑같은 레이아웃**을 쓰고 색만 다르다 — 종류마다 다른 판을 만들면 어느 게 어느 건지
매번 다시 읽어야 한다.

```
┌ Img_Header  (테마색 머리띠) ─┐   이름   Txt_Name
│ Img_Body                     │   아이콘 Img_Icon
│   아이콘 / 설명 / 레벨        │   설명   Txt_Desc
└──────────────────────────────┘   레벨   Txt_Level  (NEW · Lv.N, 없으면 빈 칸)
   Img_Glow — 테두리 발광(9슬라이스라 카드 크기가 달라져도 두께가 유지된다)
```

| 종류 | 색 | 무엇이 정하는가 |
|---|---|---|
| **각성** | **보라 — 카드 전체**(머리띠 + 본문 바탕까지 짙은 보라) | `PICK_KIND.AWAKEN` |
| 전투 | 파랑 | `eTheme = COMBAT` |
| 이동 | 주황 | `eTheme = MOVE` |

- **각성만 본문 바탕색까지 바뀐다.** 일반 카드는 밝은 아이보리 바탕에 머리띠만 테마색이다 —
  각성은 놓치면 아쉬운 선택이라(2-11-2) 한눈에 달라 보여야 한다
- **무엇이 전투이고 무엇이 이동인지는 표가 정한다** — `CardInfo.csv` · `RunSkillInfo.csv`의 `eTheme` 열.
  화면(`CUI_CardPick.Get_ThemeColor`)은 색만 고르므로, 분류를 바꾸고 싶으면 표만 고치면 된다
- 액티브/패시브는 **색을 가르지 않는다**(260920에 바꿨다 — 예전엔 액티브 주황 · 패시브 청록이었다).
  고르는 순간 중요한 것은 "지금 싸울 힘이 느는가, 움직임이 느는가"이지 쿨타임 유무가 아니다
- 레벨은 이름 옆이 아니라 **카드 아래 제 칸**에 적는다. 새로 얻으면 `NEW`, 아니면 `Lv.N`(고르면 될 레벨)
- 아이콘(`Tex_Card_<CARD_TYPE>` · `Tex_RunSkill_<RUN_SKILL_TYPE>`)은 흰색으로 구워 테마색을 곱한다.
  **종류를 더하면 `CProtoSetup.ARR_CARD_ICON` · `ARR_RUN_SKILL_ICON`과 `Is_RunSkillInk`에 enum 순서대로 넣을 것**

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
| `RUSH` 쾌속 돌진 | 최대 이동 칸 수 | — | `Rush` |

#### 260923_쾌속 돌진(RUSH) — 일본 애니메이션 검객의 그 한 걸음
"몬스터에 닿을 때까지 아주 빠르게 쭉 뻗어 나가다 멈추는" 대시. **새 이동 규칙을 만들지 않았다** —
점멸(`WARP`)과 완전히 같은 길(`CPlayer.Warp → CMoveHandler.Warp → Step_To`)을 탄다. 그 경로가
이미 하는 일이 그대로 요구사항이 된다.

- **맵 끝에서 서는 것**과 **내 점령지에 닿으면 멈추는 것**(실제로는 점령까지 정리된다)은 `Warp`가
  원래 하던 일이다 — 맵 밖으로는 못 나가고(`Clamp_ToPlayable`), 내 땅에 닿으면 도형이 닫힌다(2-3).
  이 스킬이 새로 하는 일은 **몬스터에 닿기 전에 멈추는 것 하나뿐**이다
- 몬스터는 `CPlayer`/`CMoveHandler`가 모르는 대상이라(그리드는 칸만 안다, 2-3) 스테이지에 거리를
  물어본다 — 감속 스킬이 `ISkillHost.Slow_Enemies`를 쓰는 것과 같은 자리에 `Get_RushDistance`를
  하나 더 얹었다. 원기둥(선분 + 반경) 판정으로 길 위에서 가장 가까운 몬스터까지의 거리를 구하고,
  그 거리로 `Warp`의 이동 거리를 미리 줄인다. 몬스터가 없으면 스킬 수치(`fValue`)의 최대 거리 그대로 간다
- 몸 판정과 같은 반경(`CEnemy.HIT_RANGE`)을 쓴다 — 새 충돌 반경을 만들지 않았다
- **더 발전시키면 "베는" 느낌이 된다** — 사냥형 카드 `HUNT_THORN`(가시 갑옷, 2-10-1 아래 참고)과
  조합하면, 돌진이 멈추는 순간(=몸 충돌)에 그 몬스터가 고정 피해를 입는다. 이동과 전투를 분리해 둔
  덕분에 새 코드 없이 카드 하나로 "돌진하며 벤다"가 만들어진다 — 확정 기획은 아니고, 두 시스템이
  같은 몸 충돌 판정을 공유해서 생기는 자연스러운 결과다

- **효과가 실패하면 쿨타임을 돌리지 않는다.** 멈춘 채로 점멸을 눌러 쿨만 날리면 억울하다.
- 260922_**발동은 오른쪽 화면 버튼 하나로 둔다**(연속 터치 같은 커맨드는 넣지 않았다). 연속 터치는 한 번 누른 것과 가리려고
  입력을 기다려야 해 **늘 늦게 나가고**, 조이스틱을 쥔 손과 겹쳐 오작동하며, 화면에 안 보여 존재를 모른다.
  대신 버튼이 둔하지 않게 두 가지를 더했다
  - **입력 버퍼**(`CPlayer.Request_Skill`) — 쿨이 끝나기 직전이나 칸 사이를 지나는 순간에 누른 것도 0.25초 동안 기억했다가 쓴다
  - **점멸은 지금 누르고 있는 방향으로**(`CPlayer.Get_WarpDir`) — 손을 떼면 멈추게 되면서(2-3) 가던 방향만 보면 멈춘 채 원하는 쪽으로 못 튄다.
    대각선 입력은 가던 축을, 선을 긋는 중 뒤로 튀는 것은 막는다(자기 선을 밟는다)
  - 에디터 · 키보드는 **E 또는 J**가 스킬 버튼이다(`CInputHandler.SKILL_PRESSED`)
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
그은 선을 가장 가까운 점령지까지 이어 도형을 닫는다(260923_ㄱ자가 아니라 곧게 — `CMoveHandler.Draw_To`).
**점령 판정을 새로 만들지 않는다.** 평소 이동과 똑같이 `Step_To`를 지나게 해서
규칙이 한 군데에만 있도록 유지한다(2-3). 워프도 같은 이유로 평소 이동 길(`CMoveHandler.Warp`)을 탄다.

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
| 몽둥이 | ✅ | 260917_**투사체로 옮겼다**(회전탄과 같은 이유 — 그려지지 않았다). `CRunSkillEffect_Club`은 "지금 휘두를 차례인가"(자체 쿨타임)와 반경만 안다. `ProjectileInfo` 22(짧게 커지는 원)를 **레벨 반경만큼 키워**(`Spawn_PlayerShot`의 `fScale`) 띄우고, 피해 · 넉백(`ImpactInfo` 7)은 그 탄이 넣는다. 260918_뱀서라이크가 아니라 꼭 필요한 스킬은 아니지만 지우지 않고 **반경 3배(1레벨 3칸) · 좌우 양쪽에 한 번에** 휘두르게 바꿨다 — 원 가장자리가 몸에 닿게 반경만큼 옆에 띄운다(`CPlayer.Get_OffsetPoint`). 바라보는 방향을 따르지 않아 멈춰 있어도 휘두른다. 뱀서라이크의 다른 무기와 마찬가지로 버튼 없이 자동 발동한다 — "액티브"는 쿨타임을 가진 효과라는 뜻이지 버튼 여부가 아니다 |

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

> **260918_이 게임은 뱀서라이크가 아니다.** 목숨제(2-14)에서 필요한 것은 몬스터를 죽이는 무기보다 **잠깐 세우는** 수단이라
> 투사체 무기 4종(마법탄 · 레이저 · 부메랑 · 튕기는 탄)은 `iWeight` 0으로 3지선다에서 뺐다(코드 · 각성은 남겨 둠).
> 대신 **마비 둘**을 넣었다.
>
> | 스킬 | 동작 | 레벨 수치 |
> |---|---|---|
> | 마비탄 `STUN_SHOT` | 3초마다 가장 가까운 적에게 유도 마비탄(`ProjectileInfo` 23, 피해 0 · 1.5초 기절). `CRunSkillEffect_Weapon` 그대로 | 발 수(부채꼴 40°) |
> | ~~전체 마비 `MASS_STUN`~~ | 260920_**런 스킬에서 뺐다** — 맵 위에서 주워 쓰는 필드 아이템이 됐다(2-20) | — |
>
> **전체 마비는 '모두에게 걸렸다'가 한눈에 읽혀야 한다.** 스테이지가 `OnMassStun`을 올리면 `CGameManager.On_MassStun`이
> 흔들림(`m_fTraumaOnMassStun`) · 펀치줌(`m_fPunchOnMassStun`) · 번개빛 화면 플래시(`CUI_InGame.Play_MassStunFlash`) ·
> 효과음 · 진동을 한 번에 낸다(2-10-2의 확장 방식 그대로 — 설정값 하나 · 호출 한 줄씩). 몬스터마다 잠깐 하얗게 번쩍인다.
> 몬스터가 없으면 둘 다 쏘지 않고 쿨을 찬 채로 기다린다.
>
> 260920_**전체 마비는 쿨타임을 뚝 떼고 3지선다에서 고르는 순간 딱 한 번만 터진다**(`CRunSkillEffect_MassStun.On_LevelChanged`).
> 계속 도는 마비는 몬스터를 사실상 영영 세워 두어 피하는 재미가 사라졌다 — 이제 고르는 순간의 한 방이다.
> 다시 고르면(레벨업) 그때 또 한 번, 더 긴 시간으로 터진다. `RunSkillInfo.csv`의 `fCool`은 이제 안 쓴다(0).
> `Set_Host`가 `On_LevelChanged`보다 먼저 불리므로(`CPlayer.Add_RunSkill`) 붙는 그 자리에서 바로 쓸 수 있다.

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
3지선다에 **보라색 "각성" 후보**로 나온다(`PICK_KIND.AWAKEN`, 260920_금색에서 바꿨다 — 2-10-1).
자동으로 바뀌지 않는다 — 골라야 한다.

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

### 2-11-3. 이동 런 스킬 넷 (260921)
전투 스킬은 "몬스터를 어떻게 할 것인가"를 다루지만, 이 게임에서 실제로 손에 잡히는 긴장은
**선을 긋고 돌아오는 왕복**이다. 그 왕복을 건드리는 스킬 넷을 넣었다 — 전부 `eTheme = MOVE`(주황).

| 스킬 | 분류 | 하는 일 | 레벨이 올리는 것 |
|---|---|---|---|
| 나선 가속 `SPIRAL_RUSH` | 패시브 | 그리는 선이 길수록 빨라진다(최대 2배). 선을 거두면 원래 속도 | 칸당 증가폭 |
| 유령 걸음 `GHOST_STEP` | 액티브 | **선을 긋기 시작하면** 잠깐 몬스터 · 탄을 통과한다 | 통과 시간 |
| 잔상 `AFTERIMAGE` | 패시브 | 방향을 꺾는 순간 0.35초 무적 | **쿨이 줄어든다** |
| 분신 `DECOY` | 액티브 | 어그로를 끄는 분신을 한 획 내보낸다 | 분신 수명 |

- **나선 가속**은 "길게 나가는 도박"에 보상을 준다 — 돌아오는 마지막 구간이 가장 빠르다.
  카메라도 선 길이에 따라 물러나므로(2-10) 빨라지는 것이 화면에서 같이 읽힌다.
  속도는 기존 갈래에 **따로 곱한다**(`CPlayer.Set_TrailSpeedScale`) — 거미줄 · 질주 · 카드에 덮어써지지 않게
- **유령 걸음은 쿨타임이 없다.** '선 긋기 시작'이 곧 발동 조건이다 — 가장 위험한 순간은
  안전 지대를 벗어나는 그 순간이라, 경계에 몬스터가 붙어 있으면 나가자마자 죽어 아무것도 못 했다.
  통과는 기존 무적(`Add_Invincible`)을 그대로 쓴다 — 몬스터 · 적탄 판정이 이미 무적을 본다
- **잔상은 레벨이 올라도 무적 시간이 안 늘어난다.** 늘리면 제자리에서 좌우로 비비는 것만으로
  영구 무적이 된다 — 그래서 **쿨을 줄이는 쪽**으로 성장시킨다(2.4 → 1.2초)
- **분신은 피해를 주지도 받지도 않는다.** 하는 일은 몬스터의 시선을 잠깐 가져가는 것뿐이다 —
  이 게임에서 제일 무서운 것은 '돌아오는 길이 막히는 것'이라, **시간을 사는** 스킬이다.
  선을 긋는 중에만 나간다(안전 지대에서는 끌 어그로가 없다). 한 번에 하나만 둔다 —
  여러 개면 어그로가 흩어져 무엇을 노리는지 안 읽힌다

```
CPlayer.OnDrawStart / OnTurn   260921_이동 스킬이 듣는 새 훅. 기존 훅으로는 '선을 긋기 시작한 순간'과
                               '방향을 꺾은 순간'을 가려낼 수 없었다(Tick_MoveSignal이 상태를 비교해 올린다)
IRunSkillHost.Spawn_Decoy      분신 생성 · 수명 · 어그로는 CStage_Manager가 한곳에서 본다(2-11-1과 같은 이유)
CStage_Manager.Tick_Enemy      분신이 살아 있으면 Set_ChaseState에 분신 위치를 넘긴다 = 어그로가 옮겨간다
```

> **분신 각성(점령까지 하는 분신)은 아직이다.** 분신이 자기 선을 긋고 점령까지 하려면
> `CTerritoryGrid`의 트레일을 **둘로 나눠** 들고(본체 · 분신), "분신이 맞으면 분신 선만 지운다"를
> 지원해야 한다. 지금 트레일은 하나뿐이라 규칙 단일 진입점(2-3)을 크게 건드리는 작업이다 — 따로 설계할 것.

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

### 2-14. 플레이어 목숨 — HP 풀 + 보호막 + 넉백 (260916 HP 풀 → 260918 목숨제 → 260923 다시 HP 풀)

> **260923_다시 HP 풀로 돌렸다(+ 보호막 중첩 · 몬스터 넉백).** "선을 긋는 동안은 약하다"라는 핵심 긴장 자체는
> 그대로 둔다 — 바뀌는 건 **맞았을 때의 반응**이다. 즉사(260918)는 규칙은 명료했지만 맞는 순간이 항상
> 똑같아 밋밋했다. "몇 대는 버티지만 크게 맞으면 위험하다"는 굴곡을 다시 주기 위해 가변 피해 + 보호막
> 중첩 + 피격 시 넉백을 얹었다. **`Damage()`가 처음부터 가변 피해량을 받게 만들어 뒀던 덕에(260916)
> 이번에도 호출부 구조는 거의 그대로 두고 되살릴 수 있었다.**
> - `EnemyInfo.csv`의 `iAttack`을 되살렸다(2-6, 5종 몬스터에 값을 다시 채웠다) — 몬스터마다 접촉 피해가 다시 갈린다
> - `CPlayer.Damage(int iAmount, bool bLineCut = false)` — 순서는: 무적 중이면 무시 → **보호막이 있으면
>   무엇에 맞았든(선 끊김 포함) 한 겹만 깎고 막는다**(`m_iShield`, `Add_Shield()`로 `SHIELD_MAX`(3)까지 쌓인다) →
>   보호막이 없고 `bLineCut == false`일 때만 회피 판정을 본다. **선이 끊긴 것(자기 선 밟기 · 도화선에 따라잡힘)은
>   보호막이 없으면 회피로 흘리지 못한다**(260922 결정 그대로 유지 — "선을 긋는 동안은 약하다"의 핵심이라 이번에도 건드리지 않았다).
>   260924_**"몬스터가 선에 닿음"은 더 이상 이 호출의 직접 트리거가 아니다** — 닿는 순간은 도화선이 붙을 뿐이고,
>   실제로 `Damage(..., true)`가 불리는 건 그 도화선이 트레일 끝까지 다 타들어와 따라잡힌 순간이다(2-14-1)
> - 자기 선 밟기(`STEP_RESULT.DEAD`)는 `SELF_TRAIL_DAMAGE`(9999, 사실상 즉사)로 처리한다 — 최대 체력이
>   가변값(`MapInfo.iLife`)이 됐으므로 목숨제 때 쓰던 고정값 1로는 더 이상 죽음을 보장할 수 없다
> - **몬스터와 부딪히면(몸 · 선) 몬스터도 플레이어 반대 방향으로 밀려난다**(`CStage_Manager.PLAYER_HIT_KNOCKBACK_CELL`(1.5칸) ·
>   `_DURATION`(0.2초) → `CEnemy.Push`, 기존 회전탄/몽둥이용 넉백 골격(2-11-1)을 그대로 재사용했다) —
>   맞는 순간 화면이 멎지 않고 서로 반응하는 게 보인다
> - `CPlayer.LIFE`/`MAX_LIFE`/`OnLifeChanged`/`Add_Life`, `MapInfo.csv`의 `iLife`는 **이름을 그대로 뒀다** —
>   260916의 첫 HP 풀 전환 때는 `HP`/`MAX_HP`/`OnHpChanged`로 이름까지 바꿨었지만, 그새 목숨제(260918) 기간에
>   그 이름들로 저장본·호출부가 다시 쌓여 이번엔 이름을 안 바꾸는 쪽이 손댈 곳이 적었다. **값이 뜻하는 건
>   여전히 '체력'이지 '목숨 개수'가 아니다** — 아래 260916 기록의 `HP`/`MAX_HP`/`OnHpChanged`는 지금은
>   각각 `LIFE`/`MAX_LIFE`/`OnLifeChanged`로 읽을 것
> - `IImpactTarget.HP`(적 조준 · 탄 판정용 공개 인터페이스)는 `LIFE`를 그대로 반환한다 — 새 필드를 안 만들었다
>
> 아래는 260916 HP 풀로 처음 바꿨을 때의 기록이다(이름만 위 설명대로 다시 읽을 것). 260918에 잠깐
> 목숨제로 되돌렸던 기간의 기록은 지웠다 — 지금과 반대 방향이라 남겨 두면 헷갈린다.
M4(보스 콘텐츠) 설계에서 보스가 몬스터보다 더 아프게 때려야 한다는 요구가 나왔는데,
예전 `CPlayer.Damage()`는 인자가 없어 **항상 목숨 1개**만 깎았다 — 공격력이 다른 두 몬스터를
구분할 방법이 아예 없었다. 그래서 목숨 개수를 가변 피해량을 받는 HP 풀로 바꿨다.

```
CPlayer.LIFE / MAX_LIFE     남은 체력 / 최대 체력
CPlayer.Damage(iAmount, bLineCut)   iAmount만큼 깎는다. 무적 → 보호막 → (선 끊김이 아니면) 회피 순으로 먼저 막는다
CPlayer.Add_Life(iAmount)   MAX_LIFE를 넘지 않게 회복한다
CPlayer.OnLifeChanged       체력이 바뀔 때마다(회복 포함). '맞았을 때만'은 OnDamaged를 따로 듣는다
```

> 260917_**탄 피해는 `ProjectileInfo.csv`의 `iDamage`를 쓴다**(2-15). 아래는 몬스터 접촉 기준으로 읽을 것.
> 260923_**몬스터 접촉 피해는 `EnemyInfo.csv`의 `iAttack`에서 온다**(2-6). 몬스터 종류마다 다르게 줄 수 있다 —
> `Damage()` 쪽은 처음부터 가변 피해량을 받게 만들어 둬서 호출부 구조는 손댈 필요가 없었다.

자기 선분을 밟은 즉사(`STEP_RESULT.DEAD`)는 특정 몬스터가 준 피해가 아니므로
`CPlayer.SELF_TRAIL_DAMAGE`(9999)라는 별도 고정값을 쓴다. 이건 몬스터 공격력과 무관하게 그대로 남는다.

`MapInfo.csv`의 `iLife` 열이 시작/최대 체력이다(이름은 저장본 호환 때문에 그대로 뒀다, 위 참고).
`UpgradeInfo.csv`/`EquipInfo.csv`의 `HP` 스탯은 여전히 같은 자리(`Get_TotalStat(..., STAT_TYPE.HP, ...)`)에서
`CStage_Manager.Set_PlayerUpgrade`의 `iBonusLife`로 들어가 `m_cMapInfo.iLife`(최대치)에 더해진다 —
강화/장비가 최종 수치를 만드는 흐름 자체는 그대로다.

### 2-14-1. 도화선 — 선 접촉은 더 이상 즉시 피해가 아니다 (260924)
**"선을 긋는 동안은 약하다"는 핵심 긴장은 그대로 두되, 선에 닿는 순간의 반응을 바꿨다.**
예전(260922~260923)에는 몬스터의 몸이 트레일에 닿으면 그 프레임에 바로 `Damage(..., bLineCut: true)`가
불렸다 — 판정은 명료했지만, "선이 곧 위험하다"는 사실이 오히려 **선을 방패로 만드는 역설**을 낳았다.
몬스터가 선에 닿아도 몬스터만 튕겨 나갈 뿐 선 자체는 멀쩡했으므로, 유저는 HP 몇 대를 감수하고 계속
크게 그으면 됐다 — 욕심과 위험의 긴장이 사라진 것이다. 그래서 **몸**과 **선**의 피격 규칙을 완전히
나눴다.

```
몸 접촉   그대로 즉시 피해 + 넉백 + 무적(2-14, 손 안 댐)
선 접촉   그 지점에서 도화선에 불이 붙어 트레일 끝(플레이어)을 향해 타들어온다 — 아래
```

```
CStage_Manager.Ignite_Fuse   몬스터의 몸이 트레일에 처음 닿은 자리(선 위의 길이)에서 발화. 이미 타는 중이면 새로 안 붙는다
CStage_Manager.Tick_Fuse     매 프레임 불 앞머리를 선 끝 쪽으로 민다(FUSE_SPEED_CELL_PER_SEC, 초당 3칸)
CTerritoryGrid.Set_Burn      260923_탄 구간(BURN_FROM ~ BURN_TO, 선 위의 길이)만 적어 둔다 — 렌더러가 그 구간을 빼고 그린다
CTerritoryGrid.Step_To       IS_TRAIL_BURNING이 켜져 있으면 안전 지대로 돌아와도 점령하지 않는다
```

- **발화는 몸(`HIT_RANGE`)이 트레일에 닿는 순간**(`CTerritoryGrid.Try_Find_TrailTouch` — 닿은 자리를 선 위의 길이로 돌려준다)이다. 몸 판정과 같은 자리라 "화면에서는
  닿았는데 안 붙었다"가 안 생긴다
- **발화 지점의 몬스터 공격력(`EnemyInfo.iAttack`)을 그 자리에서 기억해 둔다**(`m_iFuseDamage`) —
  불이 다 타들어올 때쯤엔 그 몬스터가 어디 있는지, 심지어 살아 있는지조차 알 수 없어서다
- 260923_**도화선은 선 위의 길이(칸)로 센다.** 트레일은 그대로 두고 탄 구간만 적어 두면, 선을 그리는 `LineRenderer`가
  그 구간을 빼고 그린다 — **불이 타들어오는 모습이 "선이 지나온 순서대로 사라지는" 것으로 표현된다**. 앞머리에는 불꽃색 머리가 붙는다
- **불보다 먼저 안전 지대에 닿으면 선만 잃는다.** `Step_To`의 점령 분기(`default` 케이스)에서
  `IS_TRAIL_BURNING`을 보고, 켜져 있으면 `Capture()` 대신 `Clear_Trail()`만 하고 `SAFE`를 돌려준다 —
  **새 `STEP_RESULT`를 만들지 않았다.** "안전하게 서 있다"는 뜻은 원래 `SAFE`가 이미 갖고 있었다.
  마감(SEAL) 스킬도 같은 `Step_To`를 반복 호출해 닫으므로 별도 처리 없이 이 규칙을 그대로 탄다
- **따라잡히면(도화선이 트레일 끝까지 타면) 선은 무조건 사라진다** — 보호막·회피는 HP만 막을 뿐,
  "충돌 자체는 일어난 것"이라는 몸 넉백과 같은 원칙(2-14)을 여기서도 지킨다. HP 차감은
  `bLineCut: true`로 부르므로 회피는 여전히 못 흘린다(자기 선 밟기와 같은 취급)
- **몸 넉백은 없다.** 발화시킨 몬스터가 잡히는 순간엔 어디 있을지 알 수 없어(위 참고) 넉백을 걸 대상이
  마땅치 않다 — 몸 충돌만의 몫으로 남겨 뒀다
- **무적 중(유령 걸음 등, 2-11-3)에는 발화 자체가 안 된다.** 도화선은 몸 충돌과 달리 무적이 끝난 뒤까지
  살아남는 상태라, 무적 중 살짝 스친 것도 그대로 불이 붙으면 "통과했는데 나중에 맞는" 모순이 생긴다 —
  그래서 발화 판정에서만 `CPlayer.IS_INVINCIBLE`을 따로 본다(몸 충돌은 원래부터 `Damage()` 안에서
  막히므로 그대로 뒀다)
- **속도 3칸/초는 튜닝 값**(`CStage_Manager.FUSE_SPEED_CELL_PER_SEC`)이라 CSV로 빼지 않았다
  (`PLAYER_HIT_KNOCKBACK_CELL`과 같은 자리, 2-14). 플레이어가 계속 앞으로 그으면(기본 속도 9칸/초 기준)
  격차가 계속 벌어져 잡히지 않는다 — 멈추거나 감속당하면 거리가 좁혀진다
- **몸에 맞아 죽거나 마감으로 트레일이 먼저 사라지면 도화선도 조용히 꺼진다**(`Tick_Fuse`가 매 프레임
  `IS_DRAWING`을 확인). 웨이브 전환(`Enter_Wave`) · 스테이지 종료(`Release`)에서도 같이 리셋한다
- 발화 지점을 알릴 자리로 `CStage_Manager.OnFuseIgnited`(칸 좌표)를 훅만 뚫어 뒀다 — 스파크 이펙트는
  프리팹이 있어야 해서 클라우드 세션에서는 못 붙인다(2-17의 캐릭터 프리팹 폴백과 같은 사정).
  지금도 선 렌더러가 탄 구간을 지우므로 **연출 없이도 기능은 이미 동작한다**
- 화면 없이 `CProtoTest.Test_Fuse`가 `Try_Find_TrailTouch` · `Set_Burn` · `IS_TRAIL_BURNING` 세 원시
  동작을 검증한다(260923_다각형 전환으로 `Get_TrailIndex`/`Burn_TrailCell`에서 이름·값이 바뀌었다 —
  칸 인덱스 대신 선 위의 길이(`float`, arc)로 잰다). 발화·소화 타이밍(`Ignite_Fuse`/`Tick_Fuse`)은
  `CStage_Manager` 쪽이라 GNAW의 침식 타이머(2-6)처럼 그리드 원시 동작만 단위 테스트하고 호출 타이밍은
  실기기에서 확인할 것

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
> 260920_스킬은 그대로 없지만 **이동 방식은 캐릭터마다 다르다(2-22).**

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

- 260918_**캐릭터 시스템 전에 이미 깬 맵의 캐릭터는 진행도를 읽을 때 챙겨 준다**(`Grant_ClearedCharacters`).
  얻는 순간이 '클리어'뿐이라, 안 그러면 예전에 깬 맵을 다시 깨기 전까지 캐릭터가 영영 안 들어온다. 조각은 주지 않는다
- 260918_**가방 캐릭터 탭은 못 가진 캐릭터도 보여 준다.** 무엇을 모을 수 있는지 보여야 모으고 싶어진다 —
  못 가진 줄은 `[미보유] <맵 이름> 클리어 시 획득`(`CProgress_Manager.Find_CharacterMap`)만 적고 눌리지 않는다

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

### 2-17-3. 가방 화면 — 네 구역 (260918)
레퍼런스 게임의 가방 배치를 따라 `CUI_Inventory`를 다시 짰다. 프리팹은 `CProtoSetup.Create_InventoryUI`가 처음부터 만든다.

```
┌ 위 패널 ───────────────────────────┐  A  가운데 — 장착 캐릭터 그림 + 이름 · 레벨 (누르면 캐릭터 탭)
│ B 신발   A 장착 캐릭터   B 목걸이   │  B  부위별 장착 장비 — 신발 · 가방 / 목걸이 · 소모품
│ B 가방                   B 소모품   │      (비었으면 장비 탭, 끼고 있으면 그 장비 상세)
└────────────────────────────────────┘
┌ C 보유 목록 (5열 격자, 스크롤) ─────┐  C  칸마다 아이콘 · 이름 · Lv. 장착한 칸은 왼쪽 위 'E'
└────────────────────────────────────┘
 D [장비] [캐릭터] [펫]                  D  목록 탭(INVENTORY_TAB). 장비 뽑기는 상점에 있다
```

- **스킬 탭은 뺐다.** 스킬은 판 안에서만 얻는 런 스킬(2-11-1)이라 가방에서 고를 것이 아니다. 펫은 자리만 있다(준비 중)
- **장착 · 강화 · 레벨업은 칸을 눌러 뜨는 상세 팝업에서 한다.** 가방은 `CUI_PopupDesc`만 만들어 `OnRequestPopup`으로 올리고,
  `CGameManager.Open_LobbyPopup`이 띄운다(2-7). 어느 버튼이든 **먼저 닫고 동작한다** — 동작이 없는 버튼은 닫기만 한다
- 장비 칸 아이콘은 부위별 하나(`Tex_Equip_<EQUIP_SLOT>`, 흰색)다. 같은 부위 장비는 이름으로 가른다 — 장비별 아트가 오면 표에 아이콘 열을 늘릴 것
- 캐릭터 그림은 그 캐릭터 스킨 프리팹(`strPrefabName`)의 몸 스프라이트, 없으면 기본 몸(`Tex_PlayerBody`)이다 — 스테이지의 대체 규칙(2-17)과 같다
- 목록 칸과 B 슬롯은 같은 모양(`Img_Icon` · `Txt_Label` · `Badge_Equip`)이라 `Paint_Cell` 하나로 칠한다

#### 장비 뽑기 (`GachaInfo.csv`) — 상점에서만
**BM은 상점 한곳에서만 한다**(260918). 처음엔 가방 하단에 뒀다가 옮겼다 — 상점 목록 맨 위의 금색 줄이다(`CUI_Shop.Add_GachaRow`).
결과 팝업은 가방과 같은 길(`OnRequestPopup` → `CGameManager.Open_LobbyPopup`)로 띄운다.
```
CProgress_Manager.Try_Gacha → Pay(iCost) → EquipInfo.iGachaWeight로 하나 → 결과(GACHA_RESULT)
```
- 무엇이 나올지는 **`EquipInfo.csv`의 `iGachaWeight`** 열이다. 0이면 안 나온다 — 소모품(보호막)은 뺐다
- **이미 가진 장비가 나오면 버리지 않는다** — 만렙 전이면 강화 +1(`LEVEL_UP`), 만렙이면 비용 × `fDuplicateRefundRate` 코인 환급(`REFUND`)
- 뽑을 것이 없으면 코인을 받지 않는다. 코인은 `Pay`를 지나므로 무료 스위치(`m_bFreeSpend`, 2-9)도 걸린다
- 상점은 표의 **첫 줄**을 쓴다. 유료 재화 · 기간 한정 뽑기는 줄을 늘리고 상점에 줄을 붙이면 된다

### 2-17-2. 카드 갤러리 — 로비의 별도 탭 (260918)
수집한 카드(웨이브 보상 이미지)는 가방이 아니라 **로비 하단 탭바의 독립된 탭**(`LOBBY_TAB.CARD`,
`CUI_Card`)이다. 장착이라는 개념이 없는 순수 감상용 화면이라 장착/강화를 다루는 가방과 성격이
달라 처음부터 분리했다.

```
LOBBY_TAB.CARD → CGameManager.Open_TabCard → CUI_Card
```

#### 260920_캐릭터 수집 화면으로 다시 짰다 — 서브 탭 둘 + 격자
레퍼런스 게임의 캐릭터 수집 화면을 따라 목록형에서 격자형으로 바꿨다. 위에 서브 탭이 둘 있다(`CARD_TAB`).

```
[캐릭터]  캐릭터를 그림 + 이름으로 늘어놓는다. 누르면 그 캐릭터의 카드만 크게 보기로 넘긴다
[갤러리]  지금까지 연 카드를 전부 격자로 편다. 누르면 전체 순서 그대로 크게 보기
```

- **카드는 캐릭터에 딸려 있다** — `CharacterInfo.csv`의 `strCardTex`(그림 목록) · `strCardLevel`(각 장이 열리는 레벨).
  **표에 캐릭터를 한 줄 더하면 두 탭에 그대로 따라 붙는다** — `CUI_Card`는 고치지 않는다
- 한 장이 열리는 조건은 `Is_CardUnlocked` 한곳이다 — **그 캐릭터를 갖고 있고(레벨 1 이상), 레벨이 그 장의 해금 레벨 이상**.
  즉 캐릭터를 키우면 카드가 한 장씩 열린다
- **못 가진 캐릭터 · 아직 안 열린 카드도 어둡게 보여 준다**(`COLOR_CELL_LOCK`). 무엇을 모을 수 있는지
  보여야 모으고 싶어진다 — 가방 캐릭터 탭과 같은 결(2-17)
- 칸 모양은 두 탭이 같다(`Img_Portrait` · `Txt_Name` · `Txt_Badge`) — 캐릭터 탭은 뱃지에 `연 장수/전체`,
  갤러리 탭은 잠긴 칸에 `Lv.N`을 적는다
- 격자는 3열이다. 세로 화면이라 넷을 넘기면 얼굴이 안 읽힌다
- 예전에는 맵의 웨이브 보상 이미지(`Get_RevealTex`)를 별 개수만큼 보여 줬다. 카드의 주인이 맵에서
  캐릭터로 바뀌면서 `CUI_CardDesc`도 `cMapTable` 대신 `cCharacterTable`을 받는다
- 260918_**한 장을 누르면 크게 보기**(`CUI_CardViewer`, `Prefab_UI_CardViewer`, Popup 캔버스)가 화면 전체로 뜬다.
  갤러리 순서 그대로 **좌우로 밀거나 양옆 버튼으로 넘긴다**(화면 너비 8% 넘게 밀어야 넘어간다).
  끝에서 반대쪽으로 돌아가지 않는다. 여닫기는 `CGameManager.Open_CardViewer`/`Close_CardViewer`(2-7) —
  갤러리 탭이 닫히면 같이 닫힌다. 판정(`Clamp_Index`/`Get_SwipeStep`)은 static이라 화면 없이 테스트한다
- `CUI_Upgrade`/`CUI_Shop`과 같은 단일 목록 화면 구조를 그대로 따른다(안쪽 탭 없음)

### 2-18. 점령 리스크·보상 연결 (260918)
지금까지는 `Capture()`가 칸 수만 그대로 더해서, **안전하게 조금씩 먹기와 위험을 감수하고 크게
한 방에 먹기 사이에 아무 이득 차이가 없었다.** 그래서 최적 전략이 "구석에서 야금야금"
하나로 수렴해 반복작업처럼 느껴진다는 문제가 있었다 — 이걸 겨냥한 첫 조치다.

```
CPlayer.Handle_ArriveCell(STEP_RESULT.CAPTURE) → OnCapture(iCapturedCount)
CStage_Manager.On_PlayerCapture → Grant_CaptureReward(iCapturedCount)
  fRatio = iCapturedCount / CTerritoryGrid.PLAYABLE_COUNT   (이미 있던 계산을 그대로 재사용)
  fMultiplier = CCSVData_CaptureRewardInfo.Get_Multiplier(fRatio)
  iCoin = iCapturedCount * MapInfo.iCoinPerCell * fMultiplier  →  STAGE_COIN에 누적
  → OnCoinGained(iCoin, vPlayerPos)  →  CGameManager → CUI_InGame.Play_CoinGainEffect
```

- **한 번의 점령이 맵 전체에서 차지하는 비율**로 배율 구간을 나눈다(`CaptureRewardInfo.csv`,
  260918 갱신: 0~10% 1.0배 / 10~20% 1.2배 / 20~30% 1.5배 / 30~40% 1.8배 / 40%~ 2배 — 전부 기획이
  CSV에서 바로 조정한다). 표가 없으면 1배로 지급해 게임이 죽지 않는다
- **점령마다 저장하지 않는다.** `STAGE_COIN`은 스테이지 안에서만 누적되는 순수 숫자이고,
  실제 보유 코인 반영(`CProgress_Manager.Add_Coin`, PlayerPrefs 저장)은 **스테이지가 끝날 때 한 번만**
  한다(`On_StageStateChanged`, 별 코인과 합쳐서) — 점령마다 저장하면 모바일에서 디스크 쓰기가 낭비된다
- **클리어/실패와 무관하게 지급한다.** 별 코인은 신기록을 새로 세워야 나오지만, 점령 재화는
  이미 먹은 땅이 사라지지 않으므로 죽어도 그대로 받는다 — 결과 화면 문구(`Show_Result`)도
  신기록 여부와 코인 유무를 따로 본다
- `CUI_InGame.m_txtCoin`이 이번 판 누적을 실시간으로 보여준다. **파티클이 이 숫자 쪽으로
  촤르륵 날아가는 연출은 프리팹이 있어야 해서 클라우드 세션에서는 못 만들었다** —
  `Play_CoinGainEffect(iAmount, vWorldPos)`가 숫자는 이미 갱신하고 있으니, 로컬에서 파티클
  프리팹을 만들어 그 함수 안에 이어 붙이면 된다(2-17의 캐릭터 프리팹 폴백과 같은 사정)
- `MapInfo.csv`에 `iCoinPerCell` 열이 새로 생겼다(15번째). 기존 맵 행에는 `iCoinPerStar`
  바로 뒤에 끼워 넣었다 — **이 CSV를 참조하는 다른 시트/스프레드시트가 있다면 열 순서가
  하나씩 밀렸으니 다시 맞출 것**

### 2-23. 로비 메인 — 계정 레벨 · 하트 · 스테이지 카드 (260921)
레퍼런스 로비를 따라 메인 화면을 다시 짰다. **상단 바(D · E)는 로비(`CUI_Lobby`)**, **스테이지 카드(A · B · C)는
전투 탭(`CUI_StageSelect`)** 이다 — 상단 바는 어느 탭에서든 남아 있어야 해서 로비에 뒀다.

```
┌ D 프로필 ───────────┐  E  ♥ 24/30   ● 1,250   ◆ 0
│ [그림] 캐릭터 이름   │      회복 04:59
│        Lv.3 ▓▓▓░ 경험치│
└─────────────────────┘
        스테이지 1 / 3
   ◀  ┌ 이름 ───────────┐  ▶        B  좌우로 한 장씩(끝에서 반대쪽으로 돌지 않는다)
      │  흐린 보상 그림  │           A  그 스테이지 최종 보상 그림을 흐리게
      │ ☆☆☆  경험치 최대 300 │
      └─────────────────┘
          [ 진입   ♥ -5 ]          C  하트를 쓰고 들어간다
```

#### D — 계정 레벨 · 경험치 (`AccountLevelInfo.csv`)
- **캐릭터 레벨이 아니라 계정 레벨이다.** 캐릭터 레벨(조각으로 올림, 2-17)과는 따로 간다.
  프로필 칸의 그림 · 이름은 지금 장착한 캐릭터(첫 카드 그림)를 보여 줄 뿐이다
- **경험치는 스테이지가 끝날 때 얻는다** — 그 스테이지의 `MapInfo.iExpTotal` × **진행도**.
  진행도는 `CAccount_Utility.Calc_StageProgress` = (달성한 웨이브 + 지금 웨이브의 점령률/목표) ÷ 웨이브 수.
  웨이브를 넘길 때 점령률이 0으로 돌아가므로(2-5) 그냥 점령률을 쓰면 3웨이브에서 죽은 판이 1웨이브에서 죽은
  판보다 적게 받는다 — 그래서 앞 웨이브 몫을 더한다. **실패해도 먹은 만큼은 받는다**(점령 재화 2-18과 같은 결)
- 레벨이 오르면 **속도 배율 · 회피 보너스**가 오른다(`fSpeedRate` 곱하기 · `fEvasionBonus` 더하기 — 강화 · 장비와 같은 자리,
  `CGameManager.Start_Stage`의 `Set_PlayerUpgrade`). 표에는 **누적값**으로 적는다(한 줄을 고쳐도 뒤가 밀리지 않게)
- 레벨이 오르면 하트를 가득 채워 준다 — 오른 만큼 한 판 더 하라는 보상
- 만렙이면 경험치는 더 쌓이지 않는다(`Apply_Exp`)

#### E — 하트 · 코인 · 다이아
- **하트는 스테이지에 들어갈 때 쓴다**(`MapInfo.iStaminaCost`, 현재 5). 최대치 · 회복 시간은 계정 레벨 표에 있다
  (현재 30 · 5분에 1개 — 레벨이 오르면 최대치를 늘릴 수 있게 레벨 표에 뒀다)
- **시간 계산은 저장소도 시계도 모르는 순수 함수다**(`CAccount_Utility.Regen_Stamina`) — 시각을 인자로 받으므로
  "30분 뒤"를 기다리지 않고 테스트한다. `CProgress_Manager.fnNow`로 시계를 갈아 끼울 수 있다
- **가득 찬 동안에는 시간을 쌓아 두지 않는다.** 가득 찬 채로 하루를 놀다 와도 한 칸 쓰는 순간부터 다시 센다
- 읽기만으로는 저장하지 않는다 — 쓸 때(`Try_UseStamina`) · 레벨이 오를 때만 디스크에 쓴다
- 하트가 모자라면 들어가지 않고 **필요 · 보유 · 다음 하트까지 남은 시간**을 팝업으로 알려 준다.
  진입 버튼은 하트가 모자라도 눌린다 — 눌러야 왜 못 들어가는지 알 수 있다
- 로비는 1초마다 하트 시계를 다시 쓴다(`CUI_Lobby.Update` — CUI_InGame처럼 Unity Update를 쓴다, 2-7)
- **다이아(유료 재화)는 아직 보유량 표시만 있다.** 얻는 곳 · 쓰는 곳은 다음 작업이다(`CProgress_Manager.Add_Diamond`만 뚫어 뒀다)
- 인게임의 목숨(♥ 하트 모양 HUD, 2-14)과 **다른 것**이다 — 이름이 겹치니 기획 문서에서는 "입장 하트"로 구분할 것

#### A — 흐린 보상 그림 (`CBlur_Utility`)
**드러내는 순간이 이 게임의 재미**라(2-5) 로비에서는 다 보여 주지 않는다 — 무엇이 걸려 있는지 짐작만 되게 흐린다.
셰이더 없이 CPU로 만든다: 폭 48로 줄이고 → 3x3 상자 블러 두 번 → 양선형 필터로 늘려 그린다. 이름으로 캐시한다.
보상 그림은 가림막으로도 쓰여 Read/Write가 이미 켜져 있다(2-5). 못 읽으면 흐리지 않은 원본을 그대로 쓴다.
잠긴 스테이지는 그림을 어둡게 누르고 진입 버튼을 끈다.

#### 저장
`CStageProgress`에 `iAccountLevel` · `iAccountExp` · `iStamina`(-1 = 한 번도 안 씀 → 가득) · `lStaminaAnchor`(유닉스 초) ·
`iDiamond`를 더했다. 옛 저장본은 이 필드가 없어 초기값으로 남는다 — '레벨 0은 1', '하트 -1은 가득'으로 읽는 규칙을
`CProgress_Manager` 한곳에서 정한다.

#### 개발용 스위치 (1-6)
`m_bFreeStamina` — 켜면 하트가 모자라도 들어가고, 들어가도 줄지 않는다.

### 2-24. 현란한 동작 피드백 (260922)
**잘한 순간을 게임이 알아봐 준다 — 보상은 없다.** 스타일 게이지 · 랭크 보상은 기획에서 뺐다. 알아보고 알려 주는 것만으로
"과감하게 하면 기분이 좋다"가 된다.

| 동작 | 판정 | 글자 |
|---|---|---|
| 아슬아슬 | 선을 긋는 중 몬스터가 몸 · 선에 **몸 판정 + 1.5칸**까지 붙었다가 **맞지 않고 떨어짐** | `NEAR MISS!` |
| 연속 점령 | 점령 사이가 4초 안 | `CHAIN ×N` |
| 가둬 잡기 | 점령으로 몬스터를 죽임 | `TRAP!` · `DOUBLE TRAP!` · `TRIPLE TRAP!` · `MULTI TRAP ×N` |
| 대형 점령 | 한 번에 맵의 10% 이상 | `HUGE!` |

```
CStyleTracker            판정만(화면 없이 테스트). 시각은 Tick으로 주입한다
CStage_Manager.OnStylish Tick_Enemy(아슬아슬) · Notify_CaptureStyle(점령 셋)이 올린다
CGameManager.On_Stylish  글자(CUI_InGame.Show_Callout) · 소리 · 진동 · 펀치. 가둬 잡기 · 대형 점령만 흔든다
```

- **아슬아슬은 붙는 순간이 아니라 떨어지는 순간에 띄운다** — 붙는 순간 띄우면 그대로 맞아 죽을 때도 떠 우습다.
  맞거나 안전 지대로 돌아오면 붙어 있던 것은 잊는다. 0.6초 안에 여럿이면 한 번만(소음이 된다)
- 글자는 톡 튀어나왔다가(1.7배 → 제 크기) 떠오르며 옅어진다. 여럿이 뜨면 위로 쌓이고 최대 셋이다. 색은 동작마다 다르다
- 판정 수치(4초 · 10% · 1.5칸)는 `CStyleTracker`의 상수다 — 보상이 없는 연출 판정이라 표로 빼지 않았다(연출 타이밍과 같은 자리)
- 개발 스위치 `m_bStylishEnabled` · 세기 `m_fPunchOnStylish` · `m_fTraumaOnStylish`(`GameConfig.asset`, 1-6). 옵션 UI에는 아직 안 올렸다

### 2-22. 캐릭터별 이동 방식 (260920)
캐릭터가 스킨과 스탯 배율만 다른 상태였는데(2-17), **어떻게 움직이는가**를 캐릭터마다 다르게 줬다.
같은 맵도 조작감이 달라지므로 캐릭터를 바꿀 이유가 생긴다.

| 방식 | 조작 | 비고 |
|---|---|---|
| `FOUR_WAY` | 상하좌우 (기본) | 지금까지의 조작 그대로 |
| `EIGHT_WAY` | 대각선까지 8방향. 260921_**선을 긋는 동안은 곧은 사선**으로 간다 | 대각선 모양으로 점령한다 |
| `SPIRAL` | 260921_내 땅 위에서는 4방향 그대로. **나가면 아르키메데스 나선**으로 돌고, 입력은 방향만 바꾼다 | 한 바퀴 돌면 스스로 내 땅으로 돌아가 둥글게 점령한다 |

```
CharacterInfo.csv eMoveStyle → CStage_Manager.Set_Character → CPlayerDesc.eMoveStyle
                                → CMoveHandler.Set_MoveStyle  (실제 이동)
                                → CInputHandler.Set_MoveStyle (조이스틱 · 키보드 입력 해석)
```

#### 이동은 전부 연속이다 (260923, 다각형 전환)
땅이 다각형이 되면서 칸 단위 이동 · 자유 이동(옛 `CMoveHandler_Free`) 구분이 없어졌다. `CMoveHandler` 하나가
한 프레임 이동을 선분으로 `Step_To`에 넘긴다(2-3). **선은 몸이 지나간 그대로 남는다** — 사선은 곧은 사선, 나선은 원이다.

- 내 땅 위에서는 경계를 따라 미끄러진다(2-3). 8방향 캐릭터가 사선 경계를 비스듬히 누르면 그 변을 그대로 탄다
- 선을 긋는 동안: 4방향 · 8방향은 누른 방향으로 곧게, 나선형은 아래 나선으로 방향을 틀며 간다.
  **되돌아가는 입력(154도 넘게 어긋남)은 무시하고 선다** — 자기 선을 밟는 즉사를 막는다.
  260923_예전 기준(120도)은 8방향의 135도 꺾기까지 막아 **셀레스티안이 나갔다가 비스듬히 돌아오지 못했다** —
  135도는 자기 선에서 옆으로 벌어지므로 밟지 않는다
- 맵 끝 · 잘린 칸(BLOCK)에는 들어가지 않고 벽을 따라 미끄러진다
- 점멸(`Warp`)도 같은 길로 한 번에 간다. 마감(`Draw_To`)은 가장 가까운 내 땅까지 **곧게** 잇는다(예전 ㄱ자)
- 260923_**어디로든 신발은 좌우뿐 아니라 위아래 끝도 잇는다**(`CMoveHandler.Find_WrapAxis`). 두 축을 한 걸음에 같이 넘으면 먼저 닿는 쪽부터 넘어간다
- 8방향 캐릭터의 `fSpeedRateBase`는 0.72다. 이동은 방향을 정규화하므로 대각선이 더 빠르지 않다 —
  낮은 배율은 **캐릭터 전체가 느린 것**이다(2-19에서 정해 둔 대로). 밸런스를 보고 조정할 것

#### 입력
`CVirtualJoystick.To_Dir`이 8방향일 때만 45도 부채꼴 여덟 칸으로 자른다(작은 축이 큰 축의 41.4%를
넘으면 대각선). 키보드는 두 축을 같이 누르면 대각선이다. 4방향 캐릭터는 이 분기를 아예 타지 않는다.

#### 개발용 스위치 (`GameConfig.asset`, 1-6)
| 항목 | 뜻 |
|---|---|
| `m_bUnlockAllCharacter` | `CharacterInfo.csv`의 캐릭터를 전부 1레벨로 가진 것으로 만든다(`CProgress_Manager.Grant_AllCharacters`). 가방 캐릭터 탭에서 갈아 끼우며 비교할 때 쓴다 |
| `m_eDevMoveStyle` | `NONE`이 아니면 장착한 캐릭터와 무관하게 그 방식으로 움직인다 |

`MOVE_DIR`에 대각선 넷을 뒤에 붙였다 — **앞 넷(0~3)의 순서는 바꾸지 말 것**(저장 · 입력 해석이 번호를 본다).

#### 나선형 (260921)
**나갈지는 내가 정하고, 나가서는 소용돌이를 그린다.** 처음엔 멈추지 못하고 스스로 꺾는 방식이었는데
내 땅 위에서도 제멋대로 움직여 조작하는 맛이 없었다 — 그래서 내 땅 위에서는 4방향 캐릭터와 똑같다.

- 선을 긋기 시작하면 **아르키메데스 나선**(r = a + bθ, `Get_SpiralRadius`)으로 시계 방향으로 돈다.
  곡률 1/r로 방향을 틀어 가며 적분하므로 어느 방향으로 나가든 그 자리에서 나선이 시작된다.
  시작 반지름 4칸, 한 바퀴에 4칸씩 벌어진다(`SPIRAL_START_RADIUS` · `SPIRAL_GROW_PER_TURN`)
- **입력은 방향만 바꾼다.** 새로 누른 방향으로 곧게 틀고 그 자리에서 나선을 처음부터 다시 그린다.
  누르고 있는 동안은 다시 시작하지 않는다(`m_eSpiralInput`) — 계속 이기면 나선이 곧은 선이 된다
- **한 바퀴 돌면 가장 가까운 내 땅으로 방향을 틀어 스스로 닫는다** — 나선은 선이 스스로 닫히지 않아서다.
  그 전에 내 땅에 닿으면 평소처럼 그 자리에서 점령된다. 틀 때도 처음 반지름보다 급하게 꺾지 않는다
- 260921_손을 떼면 그 자리에 멈추고, 다시 누르면 멈췄던 나선을 이어서 돈다. 다른 방향을 눌렀을 때만 방향을 바꾼다. 속도 배율은 가장 낮다(베르티고 0.68)

> 관성형은 아직 설계만 있다(2-19).

### 2-21. 3지선다 트리거 — 다시 점유율 자동 (260920 조각 게이지 → 260923 다시 점유율 → 260924 일곱 문턱)

> **260924_균등한 5% 간격을 웨이브 목표(70%) 기준 일곱 지점으로 바꿨다**
> (`5 · 12 · 20 · 30 · 42 · 56 · 65%`, `CStage_Manager.s_arrCardThreshold`). 5%씩 균등하면
> 뒤로 갈수록(먹기 어려운 구간일수록) 카드도 뜸해지는 모양이 됐다 — 웨이브 목표를 다 채우기까지
> 정확히 일곱 번 고를 수 있게, 전 구간에 걸쳐 손맛이 고르게 오도록 지점을 다시 짰다. 문턱 세는
> 로직(`Check_CardReady`)은 그대로다 — 등차 나눗셈 한 줄이 배열 순회 한 줄로 바뀐 것뿐이다.
>
> **260923_다시 점유율 자동 트리거로 되돌렸다.** 조각 게이지(아래 260920 기록)는 "몬스터를 죽이는 것"과
> "그 보상을 줍는 것"을 서로 다른 장소로 떼어 놓아 위험을 한 번 더 강제하려 한 시도였다. 실플레이에서는
> 그 분리가 재미로 이어지기보다 "선을 긋고 돌아오는 왕복" 뒤에 또 다른 심부름이 하나 늘어난 느낌으로
> 다가왔다 — 이 장르의 긴장은 왕복 그 자체에서 나오지, 왕복 끝에 픽업을 주우러 다시 나가는 별도 단계에서
> 나오지 않는다. **점령률이 오르는 것 자체가 이미 위험을 감수한 결과이므로, 그 결과에 곧바로 보상을 거는
> 쪽이 더 직접적이다.**
> - `CStage_Manager.s_arrCardThreshold`(코드 상수 배열 — 맵마다 다르게 줄 이유가 아직 없어 CSV로 빼지 않았다).
>   점유율이 이 배열의 지점을 하나 더 넘을 때마다 3지선다를 하나 연다(`Check_CardReady`가 웨이브당
>   `m_iRatioStep`으로 지금까지 넘은 문턱 수를 센다)
> - **한 번에 여러 문턱을 건너뛰면 그만큼 큐에 쌓인다** — 조각 게이지 시절 만들어 둔 `CPickQueue`(한 번에
>   한 장만 연다, 260921)를 그대로 재사용했다. 큰 도형을 한 번에 닫아 문턱을 여럿 건너뛰는 경우가 예전의
>   "자석으로 한꺼번에 줍는" 경우와 같은 문제라 같은 해법이 그대로 들어맞았다
> - **점령 조각(`CShard`) 픽업 자체를 지웠다** — 스폰 · 줍기 · 게이지 누적 코드를 걷어내고, 그 유일한
>   용도였던 `CTerritoryGrid.Collect_CaptureFrontier`(점령 직후 바깥 프론티어 계산)도 함께 지웠다
> - `MapInfo.csv`에서 `iCellPerShard` · `iShardPerKill` · `iGaugeBase` · `iGaugeAdd` · `iShardPerTrapKill`
>   다섯 열을 걷어내고 뒤 열들을 당겼다 — **이 CSV를 참조하는 다른 스프레드시트가 있다면 열 순서가 다시
>   밀렸으니 맞출 것**(2-18에서 이미 한 번 있었던 것과 같은 주의사항)
> - **웨이브가 넘어가 점령률이 0으로 돌아가면 문턱(`m_iRatioStep`)도 같이 리셋된다**(`Enter_Wave`) —
>   `m_iCardGiven` 리셋(2-5)과 같은 이유. 안 그러면 2·3웨이브에서 카드가 거의 안 나온다
> - HUD(`CUI_InGame.Refresh_Status`)에서 `◆게이지` 표시를 뺐다 — 이제 점유율(`OWNED_RATIO`) 자체가
>   다음 선택지까지 남은 거리를 보여 준다
>
> 아래는 260920에 조각 게이지로 바꿨을 때의 기록이다 — 조각 픽업 자체가 지금은 없으므로 참고만 할 것.

**성장의 조건을 "땅을 먹으면 저절로"에서 "위험한 바깥으로 주우러 나가야"로 바꿨었다.**
예전에는 점령률이 `strCardRatio`의 지점을 넘으면 3지선다가 열렸다 — 클리어하려면 어차피 먹어야 하는
점령률이라 **따로 할 일이 없는 수동적인 보상**이었다. 뱀서라이크의 경험치 보석이 재미있는 이유는
"몬스터를 잡는 것"과 "그 보상을 줍는 것"이 **서로 다른 장소에서 일어나서**, 보상을 받으려면 한 번 더
위험을 감수해야 한다는 점이다. 그 구조를 이 게임에 옮긴 것이 조각이었다 — 먹은 칸 수만큼 미점령
지역에 조각을 흩뿌리고, 몬스터 처치로도 조각이 나오고, 요구량(`iGaugeBase + iGaugeAdd * 고른 횟수`)을
모으면 3지선다가 열렸다. 가둬 죽인 몬스터는 조각을 즉시 지급했고, 점령한 땅 안에 들어간 조각은 주운
것으로 쳐서 크게 먹을수록 이득이라는 2-18 · 2-20과 같은 결을 유지했다.

#### 픽업 두 종류는 같은 뼈대를 쓴다 (`CPickup`)
영혼 · 아이템이 하는 일은 같다 — 한 칸에 놓이고, 수명이 있으면 옅어지다 사라지고, 밖에서 끝낼 수 있다.
같은 코드를 두 번 쓰게 되어 `02.GameObject/CPickup.cs`로 모았다(1-1). 파생 클래스는 '무엇인가'만 더한다.
(260920~260923에는 조각(`CShard`)도 같은 뼈대를 썼지만, 조각 게이지를 되돌리며 클래스째 지웠다.)

| 클래스 | 크기 | 수명 | 주웠을 때 |
|---|---|---|---|
| `CSoul` | 1.2 | 12초 | 그 판 한정 속도 가산 |
| `CFieldItem` | 1.6 | 표(`fLifeTime`) | 종류별 효과 (2-20) |

**줍는 판정은 둘 다 `CStage_Manager`가 한다** — 플레이어를 만지는 곳을 한군데로 모으는 규칙 그대로다.
두 종류 모두 "가까이 갔거나, 그 칸이 점령돼 내 땅이 됐으면" 주운 것으로 본다.

### 2-20. 맵 위 상호작용 아이템 (260920)
지나가면서 줍는 픽업이다. `CSoul`(2-11-1)과 같은 자리이지만 목적이 달라 **클래스를 따로 만들었다**
(2-19에서 설계해 둔 그대로) — 영혼은 "주울수록 빨라지는 누적 보상"이고, 이쪽은 "지금 이 순간을 뒤집는 한 방"이다.

```
FieldItemInfo.csv       종류 · 가중치 · 수치 · 지속 · 수명        ← 무엇을 하는가
MapInfo.csv             iFieldItemOnWave · fFieldItemCool · fFieldItemDropRate  ← 이 맵에서 얼마나 나오는가
CFieldItem              위치 · 수명 · 겉모습만 안다 (CSoul과 같은 구조)
CStage_Manager          Spawn_FieldItem(한 곳) · Tick_FieldItem(줍기) · Apply_FieldItem(효과)
CGameManager            OnFieldItemUsed → 소리 · 진동
```

| 종류 | 하는 일 | 표의 값 |
|---|---|---|
| `MASS_STUN` 번개 구슬 | 살아 있는 몬스터를 전부 기절 + 화면 연출 | `fDuration` 기절 시간 |
| `HEAL_LIFE` 회복 물약 | 목숨을 되돌린다(최대치 넘지 않음) | `fValue` 회복량 |
| `MAGNET_ALL` 전체 자석 | 맵 위 픽업(영혼 · 다른 아이템)을 전부 그 자리에서 먹는다 | — |

- **세 경로로 나온다** — 웨이브 시작에 몇 개 깔고(`iFieldItemOnWave`), 시간마다 하나씩(`fFieldItemCool`),
  몬스터를 잡으면 확률로(`fFieldItemDropRate`). 전부 `Spawn_FieldItem` 하나를 지나므로 상한(`MAX_FIELD_ITEM` 8)도 한 곳에 걸린다
- **미점령 칸에만 놓는다.** 안전 지대에 놓으면 아무 위험 없이 주울 수 있어 의미가 없다
- **점령한 땅 안에 들어간 픽업은 주운 것으로 친다**(260920). 영혼도 같이 바꿨다 —
  예전에는 그 칸을 점령하는 순간 조용히 사라져서 **먹으려고 남겨 둔 것을 내 땅으로 덮으면 손해**가 됐다.
  이제 크게 한 번에 먹으면 그 안의 픽업이 전부 딸려 온다(2-18의 "크게 먹는 쪽이 이득"과 같은 결)
- 줍는 반경은 영혼과 같다(`SOUL_PICKUP_RADIUS_BASE` + 자석 런 스킬 보너스) — 같은 숫자를 두 곳에 두지 않는다
- **전체 마비는 런 스킬에서 여기로 옮겼다**(2-11-2). 쿨마다 도는 마비는 몬스터를 영영 세워 두었다 —
  주워서 쓰는 한 방이 되면서 "언제 주우러 가느냐"가 판단이 됐다. `IRunSkillHost.Stun_AllEnemies`와
  `OnMassStun` 연출은 그대로 재사용한다(호출하는 쪽만 바뀌었다)
- 종류가 늘어도 **프리팹은 `Prefab_FieldItem` 하나**다. 색만 바꾼다(`CFieldItem.Get_Color`) — `CEnemy`의 기믹별 색과 같은 자리
- 개발용 스위치는 `GameConfig.asset`의 `m_bFieldItemEnabled`(1-6). 끄면 아예 안 나온다. 옵션 UI에는 아직 안 올렸다

### 2-19. 다음 작업 후보 — 재미 개선 (260918, 설계만·코드 미착수)
"재화 획득 이펙트/구간 배율"(2-18)과 같은 날 나온 아이디어 중 오늘 처리 못한 것들이다.
클라우드 세션은 프리팹·씬을 못 만들므로 로컬에서 바로 집어갈 수 있게 설계만 적어 둔다.

#### 필드 상호작용 아이템 (알파벳형 수집물)
> 260920_**아이템은 2-20으로 만들었다.** 3지선다를 여는 조각 게이지(2-21)도 같이 만들었었지만 260923에
> 다시 점유율 자동 트리거로 되돌렸다 — 아래 남은 것은 "N개 모아야 별 3개" 같은 퀘스트 조건 틀뿐이다.
`CSoul`(2-11-1)과 같은 자리 — 스테이지 위에 무작위로 놓이고, 플레이어가 지나가면 줍는
픽업 오브젝트. 다만 목적이 다르다: 소울은 "판이 끝날 때까지 유지되는 속도 가산"이 목적이고,
이 아이템은 "**반드시 주울 이유**"(예: 별 3개 조건에 걸기)가 목적이라 소울 클래스를 그대로
재사용하기보다 **별도 클래스로 새로 만들 것** — 목적이 섞이면 나중에 "소울인데 별 조건까지
거는" 특수 케이스가 생겨 조합 구조(1-1)가 깨진다.

- **"퀘스트로 주면 반드시 할 것 같다"는 아이디어는 아직 걸 곳이 없다.** 이 프로젝트에
  퀘스트/미션 시스템 자체가 없다(웨이브 클리어 조건 = 누적 점령률뿐, 2-7의 별은 "웨이브
  달성 수"이지 "조건 달성"이 아니다). 아이템 픽업 자체는 오늘 설계한 대로 만들 수 있지만,
  "N개 모아야 별 3개"처럼 **웨이브 클리어와 별개인 조건을 스테이지에 거는 틀**은 먼저 설계가
  필요하다 — `CStage_Manager`가 웨이브 조건(`OWNED_RATIO >= fClearRatio`) 외에 별 조건을
  더 볼 수 있어야 하는데, 지금은 그 자리가 없다
- 최소 골격: `IRunSkillHost.Spawn_Soul()`과 같은 자리에 `Spawn_FieldItem()`을 만들고,
  `MapInfo.csv`에 `iFieldItemGoal`(웨이브당 목표 개수) 같은 열을 추가, `CStage_Manager`가
  수집 개수를 세어 뒀다가 웨이브 클리어 판정에 "그리고 이 개수 이상 모았는가"를 더하는 형태가
  자연스러워 보인다 — 확정은 아니다

#### 캐릭터별 이동 방식 차별화
지금은 전 캐릭터가 같은 4방향 이동(`CMoveHandler`, `MOVE_DIR`)을 쓴다. 캐릭터마다 이동 자체를
다르게 주는 안을 검토했다 — 실현 가능성 위주로 정리한다.

| 안 | 실현 가능성 | 메모 |
|---|---|---|
| 4방향(현재) | — | 기준점 |
| 8방향 대각선(느림) | **260920_만들었다 → 2-22** | `MOVE_DIR`에 대각 4개 추가, `Get_NextCell`(2-11-1 "어디로든 신발"이 정리해 둔 그 함수)에 대각 좌표만 더하면 이동 자체는 된다. 다만 두 가지를 반드시 같이 정해야 한다 — ① **코너 컷팅**: 대각선의 양옆 두 칸이 전부 벽/트레일/몬스터일 때 그 사이로 빠져나가게 둘지 막을지(`Step_To` 판정에 영향), ② **8방향 속도 통일(260918 확정)**: 방향 벡터를 **항상 정규화**해서 8방향 전부 같은 속도로 움직이게 한다 — x·y를 그대로 더하면 대각선(±1,±1)의 합성 속도가 직선보다 √2배 빨라지는 사고가 나므로, 대각선은 (±0.707, ±0.707)로 정규화해 크기를 1로 맞춘다. "느리다"는 이 정규화와는 별개로 **8방향 캐릭터 자체의 이동 속도(배율)를 4방향 캐릭터보다 낮게** 잡아서 만든다 — 대각선만 따로 느리게 하는 게 아니라 캐릭터 전체가 느린 것이다 |
| 나선형(어려움, 재화 보너스 빠름) | **가능하지만 다른 성격의 작업** | `Step_To`(그리드 규칙의 단일 진입점, 2-3)는 그대로 두고, **입력을 해석하는 `CMoveHandler` 쪽만 바꾸면 된다** — "다음 칸 = 현재 + 입력 방향"이 아니라 "자동으로 계속 전진하며 입력이 곡률만 조절한다"(뱀/컬링 방식)로 바꾸는 것. 연속 이동 경로가 새 칸에 진입할 때마다 그 칸으로 `Step_To`를 부르면 규칙 자체는 안 깨진다. 다만 이건 "이동 규칙 확장"이 아니라 **"조작 방식 자체를 재설계"하는 작업**이라 8방향보다 공수가 훨씬 크고, 조이스틱 입력 매핑(`CInputHandler`)도 같이 손봐야 한다 |

**추천(4번 대안):**
- **관성형** — 방향 전환에 저항을 줘서 급커브를 못 하게 한다(고속·저컨트롤), 대신 직선 이동이 빠르다. 대각선안과 비슷한 공수로 될 것 같다
- **자동전진형(스네이크식)** — 항상 전진하고 90도 꺾기만 입력으로 받는다. 나선형만큼 조작 방식을 바꾸지만, 곡률 대신 "꺾을지 말지"만 보면 되어 구현이 더 단순하다
- 나선형 자체가 재미있다면 밀어붙일 가치는 있으나, **공수 대비 임팩트를 8방향/관성형과 먼저 비교해 볼 것을 제안한다** — 결정은 아니다

### 2-25. 다음 작업 후보 — 로그라이트 전환 브리프 (260923, 설계만·코드 미착수)
260923에 "노션 기획서 및 에셋/코드 수정" 요청으로 들어온 큰 브리프 중, 이번 세션에서는
**두 가지 시스템 되돌리기**(2-14 HP 풀 + 보호막 + 넉백, 2-21 점유율 자동 트리거)만 실제로 구현했다.
나머지는 클라우드 세션이 프리팹 · 씬을 못 만드는 제약과 별개로 **설계 확정이 먼저 필요한 범위**라
코드를 건드리지 않고 여기 적어 둔다. 브리프의 핵심 원칙은 이미 지키고 있다 —
**"선을 긋는 동안은 약하다"는 이번에도 건드리지 않았다**(2-3, 2-14의 260923 기록 참고).

> 브리프의 네 가지 갈림길 중 사용자가 확인해 준 답: HP 풀 전환(구현함) · 점유율 트리거 복귀(구현함) ·
> 영웅 전용 패시브+액티브 도입(원한다고 확인했으나 범위가 커 이번엔 설계만) · 몬스터 배치는 **바꾸지 않는다**
> ("웨이브 표대로 여러 마리로 시작"을 그대로 유지하라는 명시적 답 — 스테이지 1 몬스터 0마리 · 2웨이브부터
> 1마리라는 브리프 원안은 반영하지 않았다. 몬스터 수 자체를 조정하고 싶다면 스테이지 구성은
> `MapInfo.csv`의 `strWaveEnemy` 열만 고치면 되므로 코드 작업이 아니다 — 기획 결정이 서면 표만 바꿀 것).

#### 영웅 컬렉션 — 전용 패시브 + 액티브
**지금 캐릭터 시스템(2-17)은 "전용 스킬이 없다"가 핵심 결정이다** — 런 스킬 풀이 전 캐릭터 공통이고,
캐릭터는 스킨 · 스탯 배율만 다르다(2-17 도입부 참고). 브리프는 이 결정을 뒤집는 요청("영웅마다 고유
패시브 + 액티브")이라 **캐릭터 시스템의 기존 설계와 직접 충돌한다** — 확인 없이 되돌릴 사안이 아니었고,
사용자가 도입을 원한다고 확인했다.

- **런 스킬(2-11-1)과는 다른 층위다.** 런 스킬은 판 안에서 주워 쌓는 "이번 판 한정" 빌드고,
  영웅 전용 스킬은 "이 캐릭터를 쓰는 한 항상 켜져 있는" 정체성이다 — 뱀서라이크에서 "캐릭터 고유
  무기"와 "픽업 빌드"가 따로 노는 것과 같은 구도
- 최소 골격 후보: `CharacterInfo.csv`에 `iPassiveSkillID` · `iActiveSkillID` 두 열을 추가하고,
  `CPlayer`가 장착 캐릭터에 맞는 패시브 모듈 하나 + 액티브 모듈 하나를 추가로 든다 —
  기존 `CSkillEffect`(로비 장착 스킬, 2-11)와도 `CRunSkillEffect`(런 픽업, 2-11-1)와도 다른
  **세 번째 조합 슬롯**이 되므로, 새 표(`HeroSkillInfo.csv`?)와 새 효과 모듈 계열이 필요해 보인다 — 확정 아님
- 브리프가 예시로 든 액티브들(점멸 · 3초 전력질주 · 3초 유령화 · 화면 전체 몬스터 1초 정지, "히메폼"류 변신)은
  대부분 **기존 스킬(2-11)이나 런 스킬(2-11-1/2-11-3)에 이미 있는 효과와 겹친다** — 점멸=WARP, 유령화=GHOST_STEP,
  화면 정지=전체 마비(필드 아이템으로 옮겨진 MASS_STUN, 2-20). **새 효과를 만들기보다 기존 효과 모듈을
  캐릭터 전용 슬롯에서도 재사용하는 방향이 코드량이 적을 것** — 다만 "쿨타임 · 해금 없이 항상 그 캐릭터면
  쓸 수 있다"는 성격이 로비 장착 스킬(쿨타임 있음, 하나만 장착) · 런 스킬(레벨업 · 뽑기 필요)과 또 달라서
  세 번째 계열을 새로 만들지, 기존 두 계열 중 하나를 확장할지부터 기획이 정해야 한다
- **"변신"(히메폼류) 연출은 프리팹 · 애니메이션이 있어야 해서 클라우드 세션에서는 아예 손댈 수 없다** —
  3-1의 프리팹 누락 폴백과 같은 사정. 로컬에서 스킨을 만들 때 같이 설계할 것

#### 몬스터 증식 — 1마리에서 시작해 분열/소환
브리프의 "몬스터는 1마리로 시작해 시간이 지나며 분열 · 증식한다"는 **사용자가 명시적으로 반려했다**
("아니오 — 지금처럼 웨이브 표대로 여러 마리로 시작"). 다만 "몬스터가 스스로 늘어난다"는 개념 자체는
이미 `ENEMY_GIMMICK.SPAWN`(부하 소환, 2-6)으로 존재한다 — 브리프가 원한 "증식"에 더 가까운 결을
원하면 SPAWN 기믹의 `RefID`가 자기 자신을 가리키게 표를 채우기만 하면 된다(단, 총량 상한
`CStage_Manager.MAX_ENEMY`(32)가 이미 무한증식을 막아 준다, 2-6). **코드 변경 없이 `EnemyInfo.csv` 값만으로
브리프의 취지를 상당 부분 흉내 낼 수 있다** — 필요해지면 기획이 표를 채워 시험해 볼 것.

#### 이벤트 트리거형 스킬 다섯 — 예시별 현황
브리프가 예로 든 다섯은 사실 **전부 이미 있거나 거의 똑같은 것이 있다.** 새로 설계할 필요 없이
아래처럼 기존 걸로 매핑되는지부터 확인할 것 — 없는 것만 추가로 설계하면 된다.

| 브리프 예시 | 이미 있는 것 | 비고 |
|---|---|---|
| 선 긋기 시작 시 유령화 | `GHOST_STEP`(2-11-3) | 그대로 일치 |
| 도형을 닫을 때 번개 | 없음 — 가장 가까운 매칭은 `MASS_STUN` 필드 아이템(2-20, 트리거는 "주웠을 때") | **닫을 때 자동 발동**은 새로 설계해야 한다 — 3지선다 각성 스킬(2-11-2)의 "만렙 액티브가 자동으로 강화" 패턴을 참고할 만하다 |
| 한 번에 10%+ 점령 시 보호막 1회 | 없음 — `HUGE!` 스타일 판정(2-24, 맵의 10% 이상 한 번에 점령)이 이미 그 조건을 판정하고 있다 | `CStyleTracker`가 이미 "10% 이상 한 번에 점령"을 감지하므로, 그 신호에 `CPlayer.Add_Shield()`(2-14)를 붙이기만 하면 된다 — 코드량이 가장 적은 후보 |
| 도형을 닫을 때 선 길이에 비례한 피해 | 없음 | 닫힌 트레일 길이는 `CTerritoryGrid`가 점령 처리 중에만 알고 있다 — `Capture()`가 반환하는 정보에 트레일 길이를 얹어야 호출부(`CStage_Manager`)에서 쓸 수 있다. 규칙 단일 진입점(2-3)을 건드리는 작업이라 신중할 것 |
| 선 긋기 시작 시 1초간 +30% 속도 | 없음 — `SPIRAL_RUSH`(2-11-3, 선 길이에 비례해 가속)가 가장 비슷하지만 트리거가 다르다(길이 비례 vs 시작 즉시 1초) | `CPlayer.OnDrawStart`(2-11-3에서 이미 만든 훅)에 바로 걸 수 있어 코드량이 적다 |

**공통 결론**: 다섯 중 "닫을 때 자동 발동"류(번개 · 선 길이 피해)만 기존 훅이 없어 설계가 더 필요하고,
나머지 셋은 기존 시스템에 한두 줄 붙이는 수준이다. 이걸 **런 스킬 후보로 넣을지, 영웅 전용 액티브로
넣을지**(바로 위 항목)는 서로 얽혀 있어 먼저 정할 것.

#### "요즘 스타일" 연출 — 슬로우모 · 콤보 표시
가변 피해 · 보호막 · 넉백(2-14)과 웨이브 필 캡처 VFX는 이번에 반영했거나(피격 반응) 이미 있다
(`CGridRenderer`의 점령 갱신, 2-3). 브리프의 나머지 둘은 코드 없이 설계만 남겨 둔다:
- **대형 점령 시 슬로모** — `CGameConfig`에 배율 · 지속시간 필드 하나, `CStage_Manager.OnStylish`의
  `HUGE` 신호(2-24)에 `Set_ActorTimeScale`(2-8, 이미 있는 액터 정지 경로)을 짧게 낮추는 형태가 자연스럽다
- **콤보 표시** — `CStyleTracker`의 `CHAIN ×N`(2-24)이 사실상 이미 콤보 카운터다. 브리프가 원하는 게
  이것과 다른 "더 상시적인 콤보 UI"라면 어떤 행동을 콤보로 셀지부터 기획이 정해야 한다

#### 새 UI 오버레이 셋 — 전부 코드보다 화면 설계가 먼저
셋 다 **로직은 이미 있거나 가볍고, 정말 필요한 건 레이아웃 · 프리팹**이라 클라우드 세션에서 만들면
로컬에서 다시 배치해야 하는 3-1의 사정과 정면으로 부딪힌다. 로컬에서 `Setup Assets`로 새 UI 오브젝트를
짤 때 아래 로직 골격을 참고할 것.
- **선을 긋는 동안 실시간 점유 예상 %** — 플레이어 오른쪽 위에 표시. `CTerritoryGrid`가 트레일이 닫혔을
  때의 점령 넓이를 **미리(닫기 전에)** 계산해야 하는데, 지금은 `Step_To`가 실제로 닫힐 때만 플러드필을
  돈다 — 매 프레임 미리보기용 플러드필을 도는 건 비용이 크다. 트레일 + 기존 점령 경계로 "닫혔다면
  얼마나 넓을지"를 근사하는 가벼운 방법부터 고민할 것(정확한 값이 필요한 화면이 아니다)
- **회피 시 "회피" 텍스트 팝업** — `CPlayer.OnEvade`(이미 있는 이벤트, 2-14/2-24)를 `CUI_InGame.Show_Callout`
  (2-24, `NEAR MISS!` 등에 이미 쓰는 콜아웃 표시 함수)에 그대로 연결하면 된다 — **가장 코드량이 적은 항목**
- **화면 밖 몬스터 방향 표시** — 카메라가 플레이어를 따라가며 시야가 제한되므로(2-10) 필요성은 명확하다.
  화면 가장자리에 몬스터 방향으로 화살표를 놓는 표준적인 오프스크린 인디케이터 패턴 — 몬스터 수만큼
  아이콘을 풀링해야 하고(`CEnemy` 수만큼), 웨이브가 넘어갈 때 회수하는 경로(2-8)에 같이 태워야 한다

#### 스테이지 1 = 몬스터 0마리 튜토리얼
브리프가 요청했지만 **몬스터 배치를 바꾸지 말라는 반려**와 겹치는 항목이라 함께 보류했다.
필요해지면 `MapInfo.csv` 1번 맵 행의 `strWaveEnemy` 열을 빈 값(또는 존재하지 않는 몬스터 수 0)으로
채우는 것만으로 충분하다 — `CStage_Manager.Spawn_Enemies`가 이미 "표에 적힌 수만큼"만 채우므로
0으로 두면 안 나온다. **코드 작업이 아니라 표 값 하나** — 기획이 원하면 바로 반영 가능하다.

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
