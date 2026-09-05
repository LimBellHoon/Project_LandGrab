using System.Collections.Generic;
using System.IO;
using System.Text;

using UnityEditor;

using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 코어 규칙 헤드리스 검증
    /// <summary>
    /// 그리드 상태 전이 · 플러드필 점령 · 그리드 이동 규칙을 에디터 없이 검증한다.
    /// 배치모드: -executeMethod Client.CProtoTest.Run  (결과: proto_test_result.txt)
    /// </summary>
    public static class CProtoTest
    {
        private const int   GRID_SIZE       = 20;
        private const int   BORDER_THICK    = 1;
        private const float STEP_SPEED      = 1f;   // 속도 1 · dt 1 → Tick 1회당 정확히 한 칸 이동

        private static StringBuilder s_sbLog;
        private static int s_iPass;
        private static int s_iFail;

        [MenuItem("Tools/LandGrab/Run Core Test")]
        public static void Run()
        {
            s_sbLog = new StringBuilder();
            s_iPass = 0;
            s_iFail = 0;

            Test_InitialBorder();
            Test_CaptureWithoutEnemy();
            Test_CaptureWithEnemy();
            Test_ClearTrail();
            Test_StepOnOwnTrailIsDeadly();
            Test_MoveRules();
            Test_BoundaryOnlyMove();
            Test_LineFollow();
            Test_Enemy();
            Test_ShapeMask();
            Test_DirtyCell();
            Test_StageProgress();
            Test_Star();
            Test_Currency();
            Test_Skill();
            Test_Inventory();
            Test_Joystick();

            s_sbLog.AppendLine($"\n===== RESULT : PASS {s_iPass} / FAIL {s_iFail} =====");
            string strResult = s_sbLog.ToString();

            File.WriteAllText("proto_test_result.txt", strResult, new UTF8Encoding(true));
            Debug.Log(strResult);

            if (Application.isBatchMode == true)
                EditorApplication.Exit(s_iFail == 0 ? 0 : 1);
        }

        #region 테스트 케이스
        // 외곽 테두리만 점령된 상태로 시작하는가
        private static void Test_InitialBorder()
        {
            CTerritoryGrid cGrid = Make_Grid();

            // 20x20에서 두께 1 테두리 = 400 - 18*18 = 76칸
            Check("초기 점령 칸 수", Count_Owned(cGrid), 76);
            Check("초기 점령률", Mathf.RoundToInt(cGrid.OWNED_RATIO * 10000f), 1900);
            Check("시작 시 선분 없음", cGrid.IS_DRAWING == false);
            Check("테두리는 안전 지대", cGrid.Get_Cell(0, 0) == CELL_STATE.OWNED);
            Check("내부는 미점령", cGrid.Get_Cell(10, 10) == CELL_STATE.EMPTY);
        }

        // 몬스터가 없으면 가장 넓은 영역만 남기고 나머지를 점령한다
        private static void Test_CaptureWithoutEnemy()
        {
            CTerritoryGrid cGrid = Make_Grid();
            int iCaptured = Walk_ClosedLoop(cGrid, null, out CMoveHandler _);

            // 선분 16칸 + 갇힌 주머니 (x 4~9, y 1~4) 24칸 = 40칸
            Check("점령 칸 수(몬스터 없음)", iCaptured, 40);
            Check("누적 점령 칸 수", Count_Owned(cGrid), 116);
            Check("점령 후 선분 없음", cGrid.IS_DRAWING == false);
            Check("주머니 내부가 점령됨", cGrid.Get_Cell(6, 2) == CELL_STATE.OWNED);
            Check("바깥 영역은 미점령 유지", cGrid.Get_Cell(15, 15) == CELL_STATE.EMPTY);
            Check("선분이 점령지로 승격", cGrid.Get_Cell(10, 3) == CELL_STATE.OWNED);
        }

        // 몬스터가 들어있는 영역은 점령되지 않고, 반대쪽(몬스터 없는 넓은 영역)이 점령된다
        private static void Test_CaptureWithEnemy()
        {
            CTerritoryGrid cGrid = Make_Grid();
            List<Vector2Int> lstEnemy = new List<Vector2Int> { new Vector2Int(6, 2) };   // 주머니 안에 몬스터
            int iCaptured = Walk_ClosedLoop(cGrid, lstEnemy, out CMoveHandler _);

            // 선분 16칸 + 몬스터가 없는 바깥 영역 284칸 = 300칸
            Check("점령 칸 수(몬스터 있음)", iCaptured, 300);
            Check("몬스터가 있는 영역은 미점령", cGrid.Get_Cell(6, 2) == CELL_STATE.EMPTY);
            Check("몬스터 없는 영역이 점령됨", cGrid.Get_Cell(15, 15) == CELL_STATE.OWNED);
        }

        // 사망 시 그리던 선분이 원상복구되는가
        private static void Test_ClearTrail()
        {
            CTerritoryGrid cGrid = Make_Grid();
            CMoveHandler cMove = Make_Move(cGrid);

            Walk(cGrid, cMove, MOVE_DIR.UP, 4, null);
            Check("선분을 그리는 중", cGrid.IS_DRAWING);
            Check("선분 칸 수", cGrid.TRAIL_COUNT, 4);

            cGrid.Clear_Trail();
            Check("선분 제거 후 미점령 복구", cGrid.Get_Cell(10, 2) == CELL_STATE.EMPTY);
            Check("선분 제거 후 점령 칸 수 불변", Count_Owned(cGrid), 76);
            Check("선분 제거 후 그리기 종료", cGrid.IS_DRAWING == false);
        }

        // 자기 선분을 밟으면 DEAD
        private static void Test_StepOnOwnTrailIsDeadly()
        {
            CTerritoryGrid cGrid = Make_Grid();
            CMoveHandler cMove = Make_Move(cGrid);

            Walk(cGrid, cMove, MOVE_DIR.UP, 4, null);       // (10,1)~(10,4)
            Walk(cGrid, cMove, MOVE_DIR.LEFT, 2, null);     // (9,4), (8,4)

            // (9,4)는 이미 자기 선분 → 밟으면 즉사
            STEP_RESULT eResult = cGrid.Step_To(new Vector2Int(9, 4), null, out int _);
            Check("자기 선분 밟기 = DEAD", eResult == STEP_RESULT.DEAD);

            // 선분을 그리던 중 안전 지대를 밟으면 도형이 닫힌 것이므로 CAPTURE
            Check("그리는 중 안전 지대 복귀 = CAPTURE",
                  cGrid.Step_To(new Vector2Int(0, 0), null, out int _) == STEP_RESULT.CAPTURE);

            // 선분이 없는 상태에서 안전 지대 위 이동은 SAFE
            CTerritoryGrid cCleanGrid = Make_Grid();
            Check("안전 지대 이동 = SAFE",
                  cCleanGrid.Step_To(new Vector2Int(0, 0), null, out int _) == STEP_RESULT.SAFE);
        }

        // 이동 규칙: 맵 밖 차단 · 안전 지대에서 정지 · 미점령 지대에서 정지 불가 · 180도 반전 차단
        private static void Test_MoveRules()
        {
            CTerritoryGrid cGrid = Make_Grid();
            CMoveHandler cMove = Make_Move(cGrid);

            // 아래는 맵 밖 → 이동 불가
            cMove.Tick(1f, MOVE_DIR.DOWN, out Vector2Int _);
            Check("맵 밖으로는 못 나감", cMove.CUR_CELL == new Vector2Int(10, 0));

            // 안전 지대에서 입력이 없으면 정지
            cMove.Tick(1f, MOVE_DIR.NONE, out Vector2Int _);
            Check("안전 지대에서 정지", cMove.CUR_CELL == new Vector2Int(10, 0));

            // 미점령 지대로 진입 후에는 입력이 없어도 계속 전진
            Walk(cGrid, cMove, MOVE_DIR.UP, 2, null);
            Check("선을 그리기 시작", cMove.CUR_CELL == new Vector2Int(10, 2));

            Walk(cGrid, cMove, MOVE_DIR.NONE, 1, null);
            Check("미점령 지대에서는 정지 불가", cMove.CUR_CELL == new Vector2Int(10, 3));

            // 180도 반전은 자기 선분을 밟게 되므로 차단하고 진행 방향을 유지한다
            Walk(cGrid, cMove, MOVE_DIR.DOWN, 1, null);
            Check("180도 반전 차단", cMove.CUR_CELL == new Vector2Int(10, 4));
        }
        // 260902_점령지 내부는 통과 불가, 영토의 선(경계)만 따라 이동
        private static void Test_BoundaryOnlyMove()
        {
            CTerritoryGrid cGrid = Make_Grid();

            // 시작 테두리는 전부 '선' — 모서리도 8방향 판정이라 끊기지 않는다
            Check("테두리 변은 경계", cGrid.Is_Boundary(new Vector2Int(10, 0)));
            Check("테두리 모서리도 경계", cGrid.Is_Boundary(new Vector2Int(0, 0)));
            Check("미점령 칸은 경계 아님", cGrid.Is_Boundary(new Vector2Int(10, 10)) == false);

            // ㄷ자로 한 번 점령 → 주머니(x 4~9, y 1~4)가 통째로 점령지가 된다
            Walk_ClosedLoop(cGrid, null, out CMoveHandler cMove);
            Check("점령 후 위치", cMove.CUR_CELL == new Vector2Int(3, 0));

            Check("점령지 한가운데는 경계 아님", cGrid.Is_Boundary(new Vector2Int(6, 2)) == false);
            Check("점령지에 닿은 테두리도 내부가 됨", cGrid.Is_Boundary(new Vector2Int(4, 0)) == false);
            Check("현재 칸은 경계", cGrid.Is_Boundary(new Vector2Int(3, 0)));

            // 오른쪽은 점령지 내부 → 가로지를 수 없다 (선을 타고 위로 우회한다)
            cMove.Tick(1f, MOVE_DIR.RIGHT, out Vector2Int _);
            Check("점령지 내부로 진입 차단", cMove.CUR_CELL != new Vector2Int(4, 0));

            // 왼쪽은 아직 미점령 지대와 맞닿은 '선' → 이동 가능
            cMove.Teleport(new Vector2Int(3, 0));
            cMove.Tick(1f, MOVE_DIR.LEFT, out Vector2Int _);
            Check("경계 위로는 이동 가능", cMove.CUR_CELL == new Vector2Int(2, 0));

            // 선 위에서 미점령 지대로 나가는 것은 여전히 가능해야 한다
            Walk(cGrid, cMove, MOVE_DIR.UP, 2, null);
            Check("선에서 미점령 지대로 진입 가능", cMove.CUR_CELL == new Vector2Int(2, 2));
            Check("나가면 다시 선을 그린다", cGrid.IS_DRAWING);
        }
        // 260902_선분 자동 추적 — 가려던 방향이 막혀도 선이 꺾여 이어지면 따라간다
        private static void Test_LineFollow()
        {
            CTerritoryGrid cGrid = Make_Grid();
            Walk_ClosedLoop(cGrid, null, out CMoveHandler cMove);
            // 이 시점의 점령 모양: 아래 테두리(y=0) 위에 x 3~10 · y 1~5 블록이 얹힌 계단

            // (3,0)에서 오른쪽은 블록 내부 → 블록의 왼쪽 벽을 타고 자동으로 올라간다
            cMove.Teleport(new Vector2Int(3, 0));
            for (int i = 0; i < 5; ++i)
                cMove.Tick(1f, MOVE_DIR.RIGHT, out Vector2Int _);

            Check("막힌 방향 대신 이어지는 선을 따라감", cMove.CUR_CELL == new Vector2Int(3, 5));

            // 블록 꼭대기에 도달하면 원래 누르던 방향으로 자연스럽게 복귀한다
            cMove.Tick(1f, MOVE_DIR.RIGHT, out Vector2Int _);
            Check("길이 열리면 원래 방향으로 복귀", cMove.CUR_CELL == new Vector2Int(4, 5));

            cMove.Tick(1f, MOVE_DIR.RIGHT, out Vector2Int _);
            Check("복귀 후 계속 진행", cMove.CUR_CELL == new Vector2Int(5, 5));

            // 반대쪽도 대칭으로 동작 (블록 오른쪽 벽을 타고 올라감)
            cMove.Teleport(new Vector2Int(10, 0));
            for (int i = 0; i < 5; ++i)
                cMove.Tick(1f, MOVE_DIR.LEFT, out Vector2Int _);

            Check("반대쪽 벽도 자동 추적", cMove.CUR_CELL == new Vector2Int(10, 5));

            // 갈림길에서는 멈춰서 플레이어가 고르게 한다 (양쪽 다 선이라 방향을 정할 수 없음)
            cMove.Teleport(new Vector2Int(15, 0));
            cMove.Tick(1f, MOVE_DIR.DOWN, out Vector2Int _);
            Check("갈림길에서는 자동 추적하지 않음", cMove.CUR_CELL == new Vector2Int(15, 0));

            // 선 위에서 미점령 지대로 나가는 것은 자동 추적보다 우선한다
            cMove.Teleport(new Vector2Int(15, 0));
            cMove.Tick(1f, MOVE_DIR.UP, out Vector2Int vArrived);
            cGrid.Step_To(vArrived, null, out int _);
            Check("미점령 지대 진입이 우선", cMove.CUR_CELL == new Vector2Int(15, 1));
            Check("나가면 선을 그린다", cGrid.IS_DRAWING);

            // 그리는 중에는 자동 추적을 하지 않는다 (좌우로 꺾이면 도형이 뭉개진다)
            Walk(cGrid, cMove, MOVE_DIR.UP, 2, null);
            Walk(cGrid, cMove, MOVE_DIR.DOWN, 1, null);      // 180도 반전 = 막힘
            Check("그리는 중에는 자동 추적 안 함", cMove.CUR_CELL == new Vector2Int(15, 4));
        }
        // 260902_몬스터 — 미점령 지대만 다니고, 점령지에 튕기고, 나온 플레이어를 쫓는다
        private static void Test_Enemy()
        {
            // ① 점령지에 부딪히면 튕긴다 (아래 테두리를 향해 내려보낸다)
            CTerritoryGrid cGrid = Make_Grid();
            CEnemyMoveHandler cEnemy = new CEnemyMoveHandler();
            cEnemy.Initialize(cGrid, new Vector2(10.5f, 1.5f), Vector2.down, 1f);

            cEnemy.Tick(1f, false, Vector2.zero, 0f);
            Check("점령지에 튕겨 방향이 뒤집힘", cEnemy.DIR.y > 0f);
            Check("튕긴 축은 제자리", Mathf.Approximately(cEnemy.POS.y, 1.5f));

            // ② 오래 돌려도 절대 점령지 안으로 들어가지 않는다
            cEnemy.Initialize(cGrid, new Vector2(10.5f, 10.5f), new Vector2(1f, 1f), 3f);
            bool bStayedOutside = true;
            for (int i = 0; i < 300; ++i)
            {
                cEnemy.Tick(0.1f, false, Vector2.zero, 0f);
                if (cGrid.Get_Cell(cEnemy.CELL) == CELL_STATE.OWNED)
                {
                    bStayedOutside = false;
                    break;
                }
            }
            Check("미점령 지대를 벗어나지 않음", bStayedOutside);

            // ③ 추적 상태면 플레이어 쪽으로 선회한다 (속도 0으로 두어 선회만 검증)
            cEnemy.Initialize(cGrid, new Vector2(10.5f, 10.5f), Vector2.right, 0f);
            cEnemy.Tick(1f, true, new Vector2(10.5f, 15.5f), 1f);   // 위쪽으로 1라디안 선회
            Check("추적 시 목표 쪽으로 선회", cEnemy.DIR.y > 0.5f);
            Check("선회는 즉시 꺾이지 않음", cEnemy.DIR.x > 0.3f);

            // ④ 추적하지 않으면 방향을 유지한다 (안전 지대의 플레이어는 쫓지 않음)
            cEnemy.Initialize(cGrid, new Vector2(10.5f, 10.5f), Vector2.right, 0f);
            cEnemy.Tick(1f, false, new Vector2(10.5f, 15.5f), 1f);
            Check("배회 중에는 목표를 무시", Mathf.Approximately(cEnemy.DIR.y, 0f));

            // ⑤ 점령 판정과 겹쳐 점령지 안에 갇히면 가장 가까운 미점령 칸으로 탈출한다
            CTerritoryGrid cBlockGrid = Make_Grid();
            Walk_ClosedLoop(cBlockGrid, null, out CMoveHandler _);   // x 3~10 · y 1~5 블록 생성
            Check("갇힘 상황 준비", cBlockGrid.Get_Cell(new Vector2Int(6, 2)) == CELL_STATE.OWNED);

            CEnemyMoveHandler cTrapped = new CEnemyMoveHandler();
            cTrapped.Initialize(cBlockGrid, new Vector2(6.5f, 2.5f), Vector2.right, 1f);
            cTrapped.Tick(0.1f, false, Vector2.zero, 0f);
            Check("점령지에 갇히면 탈출", cBlockGrid.Get_Cell(cTrapped.CELL) == CELL_STATE.EMPTY);

            // ⑥ 링 탐색 자체 검증
            Check("이미 조건을 만족하면 제자리 반환",
                  cGrid.Try_Find_NearestCell(new Vector2Int(10, 10), CELL_STATE.EMPTY, 4, out Vector2Int vSelf)
                  && vSelf == new Vector2Int(10, 10));
            Check("테두리에서 가장 가까운 미점령 칸을 찾음",
                  cGrid.Try_Find_NearestCell(new Vector2Int(0, 0), CELL_STATE.EMPTY, 4, out Vector2Int vNear)
                  && cGrid.Get_Cell(vNear) == CELL_STATE.EMPTY);
        }
        // 260904_맵 모양 마스크 — 잘라낸 칸은 아무도 못 들어가고 점령률 분모에서도 빠진다
        private static void Test_ShapeMask()
        {
            // 20x20에서 왼쪽 절반(x < 10)만 플레이 가능한 맵을 만든다
            bool[] arrPlayable = new bool[GRID_SIZE * GRID_SIZE];
            for (int y = 0; y < GRID_SIZE; ++y)
            {
                for (int x = 0; x < GRID_SIZE; ++x)
                    arrPlayable[y * GRID_SIZE + x] = x < 10;
            }

            CTerritoryGrid cGrid = new CTerritoryGrid();
            Check("모양 마스크로 초기화",
                  cGrid.Initialize(GRID_SIZE, GRID_SIZE, 1f, Vector2.zero, BORDER_THICK, arrPlayable));

            Check("잘라낸 칸은 BLOCK", cGrid.Get_Cell(15, 10) == CELL_STATE.BLOCK);
            Check("잘라낸 칸 판정", cGrid.Is_Blocked(new Vector2Int(15, 10)));
            Check("남긴 칸은 살아 있음", cGrid.Is_Blocked(new Vector2Int(5, 10)) == false);

            // 점령률 분모는 BLOCK을 뺀 200칸
            Check("점령률 분모에서 제외", cGrid.PLAYABLE_COUNT, GRID_SIZE * 10);

            // 길이가 안 맞는 마스크는 거부한다
            CTerritoryGrid cBadGrid = new CTerritoryGrid();
            Check("길이가 틀린 마스크는 거부",
                  cBadGrid.Initialize(GRID_SIZE, GRID_SIZE, 1f, Vector2.zero, BORDER_THICK, new bool[3]) == false);

            // BLOCK 칸으로는 이동할 수 없다 — 잘린 경계(x=9)에서 오른쪽으로 밀어 본다
            CMoveHandler cMove = new CMoveHandler();
            cMove.Initialize(cGrid, new Vector2Int(9, 10), STEP_SPEED);
            cMove.Tick(1f, MOVE_DIR.RIGHT, out Vector2Int _);
            Check("BLOCK으로는 진입 불가", cMove.CUR_CELL == new Vector2Int(9, 10));

            // Reset을 해도 BLOCK은 되살아난다 (웨이브가 넘어갈 때마다 Reset을 탄다)
            cGrid.Reset(BORDER_THICK);
            Check("Reset 후에도 BLOCK 유지", cGrid.Get_Cell(15, 10) == CELL_STATE.BLOCK);
            Check("Reset 후에도 분모 유지", cGrid.PLAYABLE_COUNT, GRID_SIZE * 10);
        }

        // 260904_렌더러 부분 갱신용 변경 셀 추적
        private static void Test_DirtyCell()
        {
            CTerritoryGrid cGrid = Make_Grid();
            cGrid.Clear_Dirty();
            Check("갱신 직후에는 깨끗함", cGrid.IS_DIRTY == false);

            CMoveHandler cMove = Make_Move(cGrid);
            Walk(cGrid, cMove, MOVE_DIR.UP, 1, null);
            Check("선을 그리면 더러워짐", cGrid.IS_DIRTY);
            Check("바뀐 칸만 올라옴", cGrid.DIRTY_CELLS.Count, 1);
            Check("한 칸만 바뀌면 전체 갱신이 아님", cGrid.IS_FULL_DIRTY == false);

            cGrid.Clear_Dirty();
            Check("목록도 비워짐", cGrid.DIRTY_CELLS.Count, 0);

            // 점령은 한 번에 많이 바뀌므로 전체 갱신을 요청한다
            Walk(cGrid, cMove, MOVE_DIR.UP, 4, null);
            Walk(cGrid, cMove, MOVE_DIR.LEFT, 7, null);
            Walk(cGrid, cMove, MOVE_DIR.DOWN, 5, null);
            Check("점령은 전체 갱신", cGrid.IS_FULL_DIRTY);
        }
        // 260904_진행도 / 순차 해금
        // 260905_재화·강화 — 별 1개당 코인, 갱신분만 지급
        // 260905_액티브 스킬 — 쿨타임과 발동 조건
        private static void Test_Skill()
        {
            CSkillInfo cInfo = new CSkillInfo
            {
                iSkillID = 1, eType = SKILL_TYPE.WARP, eCategory = SKILL_CATEGORY.ACTIVE,
                fCoolTime = 4f, fValue = 6f,
            };

            CSkillHandler cSkill = new CSkillHandler();
            cSkill.Initialize(cInfo);

            Check("스킬을 가지고 있음", cSkill.HAS_SKILL);
            Check("시작하자마자 쓸 수 있음", cSkill.IS_READY);
            Check("쿨타임 게이지 비어 있음", Mathf.RoundToInt(cSkill.COOL_RATIO * 100f), 0);

            Check("발동 성공", cSkill.Try_Use());
            Check("발동 직후에는 못 쓴다", cSkill.IS_READY == false);
            Check("게이지 가득", Mathf.RoundToInt(cSkill.COOL_RATIO * 100f), 100);
            Check("연속 발동 차단", cSkill.Try_Use() == false);

            cSkill.Tick(2f);
            Check("절반 차면 절반 남음", Mathf.RoundToInt(cSkill.COOL_RATIO * 100f), 50);
            Check("아직은 못 쓴다", cSkill.IS_READY == false);

            cSkill.Tick(2.1f);
            Check("쿨타임이 끝나면 다시 쓴다", cSkill.IS_READY);
            Check("게이지가 다시 비었음", Mathf.RoundToInt(cSkill.COOL_RATIO * 100f), 0);

            // 스킬을 안 가졌으면 아무 일도 일어나지 않는다
            CSkillHandler cEmpty = new CSkillHandler();
            cEmpty.Initialize(null);
            Check("스킬 없음", cEmpty.HAS_SKILL == false);
            Check("스킬이 없으면 발동 불가", cEmpty.Try_Use() == false);

            // 멈춰 있으면 어디로 갈지 알 수 없으므로 워프는 실패해야 한다
            CTerritoryGrid cGrid = Make_Grid();
            GameObject goPlayer = new GameObject("Test_SkillPlayer");
            CPlayer cPlayer = goPlayer.AddComponent<CPlayer>();
            cPlayer.Initialize(new CPlayerDesc
            {
                eObjectType   = Engine.OBJECT_TYPE.PLAYER,
                strPrefabName = "Prefab_Player",
                cGrid         = cGrid,
                vStartCell    = new Vector2Int(GRID_SIZE / 2, BORDER_THICK - 1),
                fMoveSpeed    = STEP_SPEED,
                iLife         = 3,
                cSkillInfo    = cInfo,
            });

            Check("스킬이 장착됨", cPlayer.SKILL.HAS_SKILL);
            Check("멈춰 있으면 워프 불가", cPlayer.Try_UseSkill() == false);
            Check("실패했으니 쿨타임은 그대로", cPlayer.SKILL.IS_READY);
            Object.DestroyImmediate(goPlayer);
        }

        // 260905_인벤토리 — 보유 / 장착(슬롯당 하나) / 소모품 / 스탯 합산
        private static void Test_Inventory()
        {
            CStageProgress cProgress = new CStageProgress();

            // 보유 개수
            Check("처음에는 없음", cProgress.Has_Item(101) == false);
            cProgress.Add_Item(101, 1);
            Check("장비 획득", cProgress.Has_Item(101));
            Check("개수", cProgress.Get_ItemCount(101), 1);

            // 소모품은 쌓인다
            cProgress.Add_Item(401, 2);
            cProgress.Add_Item(401, 3);
            Check("소모품 누적", cProgress.Get_ItemCount(401), 5);

            Check("소모품 사용", cProgress.Use_Item(401, 2));
            Check("쓴 만큼 줄어듦", cProgress.Get_ItemCount(401), 3);
            Check("가진 것보다 많이는 못 쓴다", cProgress.Use_Item(401, 99) == false);

            cProgress.Use_Item(401, 3);
            Check("다 쓰면 0", cProgress.Get_ItemCount(401), 0);
            Check("다 쓴 항목은 목록에서 빠짐", cProgress.Has_Item(401) == false);

            // 장착 — 같은 슬롯은 하나만
            List<int> lstShoes = new List<int> { 101, 102 };
            cProgress.Equip(101, lstShoes);
            Check("장착됨", cProgress.Is_Equipped(101));

            cProgress.Equip(102, lstShoes);
            Check("같은 슬롯 새 장비 장착", cProgress.Is_Equipped(102));
            Check("이전 장비는 자동으로 벗겨짐", cProgress.Is_Equipped(101) == false);

            cProgress.Unequip(102);
            Check("해제됨", cProgress.Is_Equipped(102) == false);

            // 스킬은 통틀어 하나
            cProgress.iEquippedSkillID = 1;
            Check("스킬 장착 저장", cProgress.iEquippedSkillID, 1);

            // 매니저 — 장비 스탯 합산
            CCSVData_EquipInfo cEquipTable = Load_EquipTable();
            if (cEquipTable == null || cEquipTable.COUNT == 0)
            {
                Check("EquipInfo.csv 로드", false);
                return;
            }

            Check("EquipInfo 행 수", cEquipTable.COUNT > 0);

            CCSVData_MapInfo cMapTable = Load_MapTable();
            if (cMapTable == null)
                return;

            CProgress_Manager cManager = new CProgress_Manager();
            cManager.Initialize(cMapTable, new CFakeProgressRepository(), cEquipTable);

            Check("아무것도 안 꼈으면 0", Mathf.RoundToInt(cManager.Get_EquipStat(STAT_TYPE.SPEED) * 100f), 0);

            // 갖고 있지 않으면 장착되지 않는다
            Check("미보유 장비는 장착 불가", cManager.Try_Equip(101) == false);

            cManager.Add_Item(101);
            Check("보유 후 장착 성공", cManager.Try_Equip(101));
            Check("신발 스탯 반영", Mathf.RoundToInt(cManager.Get_EquipStat(STAT_TYPE.SPEED) * 100f), 8);

            // 같은 슬롯으로 갈아끼우면 이전 것이 빠진다 (합산이 두 배가 되면 안 된다)
            cManager.Add_Item(102);
            Check("상위 신발 장착", cManager.Try_Equip(102));
            Check("갈아끼우면 합산이 겹치지 않음",
                  Mathf.RoundToInt(cManager.Get_EquipStat(STAT_TYPE.SPEED) * 100f), 18);

            // 다른 슬롯은 함께 적용된다
            cManager.Add_Item(201);
            cManager.Try_Equip(201);
            Check("가방은 회피", Mathf.RoundToInt(cManager.Get_EquipStat(STAT_TYPE.EVASION) * 100f), 6);
            Check("신발은 그대로", Mathf.RoundToInt(cManager.Get_EquipStat(STAT_TYPE.SPEED) * 100f), 18);

            Check("슬롯으로 조회", cManager.Get_Equipped(EQUIP_SLOT.SHOES)?.iEquipID ?? 0, 102);
            Check("빈 슬롯은 null", cManager.Get_Equipped(EQUIP_SLOT.NECKLACE) == null);

            // 소모품은 장착되지 않는다
            cManager.Add_Item(401);
            Check("소모품은 장착 불가", cManager.Try_Equip(401) == false);

            // 스킬 장착
            cManager.Set_EquippedSkill(1);
            Check("스킬 장착", cManager.EQUIPPED_SKILL_ID, 1);
        }

        private static CCSVData_EquipInfo Load_EquipTable()
        {
            TextAsset cText = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/EquipInfo.csv");
            if (cText == null)
                return null;

            CCSVData_EquipInfo cTable = new CCSVData_EquipInfo();
            cTable.Read_CSVData(cText);
            return cTable;
        }


        private static void Test_Currency()
        {
            CStageProgress cProgress = new CStageProgress();

            // 새로 던 별만큼만 준다 — 같은 판을 반복해 무한히 벌 수 없어야 한다
            Check("처음에는 코인 0", cProgress.iCoin, 0);
            cProgress.Add_Coin(100);
            Check("코인 적립", cProgress.iCoin, 100);
            cProgress.Add_Coin(-50);
            Check("음수는 무시", cProgress.iCoin, 100);

            Check("모자란 쓰기 실패", cProgress.Use_Coin(200) == false);
            Check("실패 시 코인 그대로", cProgress.iCoin, 100);
            Check("코인 쓰기", cProgress.Use_Coin(60));
            Check("쓴 만큼 줄어듦", cProgress.iCoin, 40);

            // 강화 레벨
            Check("처음에는 0레벨", cProgress.Get_UpgradeLevel(STAT_TYPE.SPEED), 0);
            cProgress.Set_UpgradeLevel(STAT_TYPE.SPEED, 2);
            Check("레벨 저장", cProgress.Get_UpgradeLevel(STAT_TYPE.SPEED), 2);
            Check("다른 항목은 그대로", cProgress.Get_UpgradeLevel(STAT_TYPE.EVASION), 0);

            // 비용 공식 : iCostBase + iCostAdd * 현재레벨
            CUpgradeInfo cInfo = new CUpgradeInfo
            {
                eType = STAT_TYPE.SPEED, iMaxLevel = 3,
                iCostBase = 100, iCostAdd = 80, fValuePerLevel = 0.04f,
            };

            Check("0레벨 비용", cInfo.Get_Cost(0), 100);
            Check("1레벨 비용", cInfo.Get_Cost(1), 180);
            Check("만렉은 비용 0", cInfo.Get_Cost(3), 0);
            Check("2레벨 수치", Mathf.RoundToInt(cInfo.Get_Value(2) * 100f), 8);
            Check("만렉 초과 수치는 상한", Mathf.RoundToInt(cInfo.Get_Value(99) * 100f), 12);
        }

        // 260905_별 기록 — 웨이브 하나만 달성해도 클리어, 최고 기록만 남는다
        private static void Test_Star()
        {
            CStageProgress cProgress = new CStageProgress();

            Check("처음에는 별 0", cProgress.Get_Star(101), 0);
            Check("별 0이면 클리어 아님", cProgress.Is_Cleared(101) == false);

            Check("별 1 기록됨", cProgress.Set_Star(101, 1));
            Check("웨이브 하나만 달성해도 클리어", cProgress.Is_Cleared(101));

            Check("더 높은 기록은 갱신", cProgress.Set_Star(101, 3));
            Check("갱신된 별", cProgress.Get_Star(101), 3);

            Check("낮은 기록은 무시", cProgress.Set_Star(101, 2) == false);
            Check("최고 기록 유지", cProgress.Get_Star(101), 3);
            Check("같은 기록도 무시", cProgress.Set_Star(101, 3) == false);
            Check("별 0 이하는 기록하지 않음", cProgress.Set_Star(102, 0) == false);

            cProgress.Set_Star(102, 2);
            Check("클리어한 맵 수", cProgress.Get_ClearedCount(), 2);
            Check("모은 별 총합", cProgress.Get_TotalStar(), 5);

            // 별이 없던 시절의 저장본을 별 1개짜리로 옮긴다
            CStageProgress cLegacy = new CStageProgress();
            cLegacy.lstClearedMap.Add(201);
            cLegacy.lstClearedMap.Add(202);

            Check("구버전 기록 이관됨", cLegacy.Migrate_Legacy());
            Check("이관 후 별 1", cLegacy.Get_Star(201), 1);
            Check("이관 후 클리어 유지", cLegacy.Is_Cleared(202));
            Check("이관 후 구버전 목록 비움", cLegacy.lstClearedMap.Count, 0);
            Check("이미 이관했으면 다시 하지 않음", cLegacy.Migrate_Legacy() == false);

            // 표기
            Check("별 표기 0/3", CStar_Utility.Get_Text(0, 3) == "☆☆☆");
            Check("별 표기 2/3", CStar_Utility.Get_Text(2, 3) == "★★☆");
            Check("별 표기 3/3", CStar_Utility.Get_Text(3, 3) == "★★★");
            Check("웨이브 수가 0이면 빈 문자열", CStar_Utility.Get_Text(1, 0) == string.Empty);
        }

        private static void Test_StageProgress()
        {
            CCSVData_MapInfo cTable = Load_MapTable();
            if (cTable == null || cTable.COUNT < 2)
            {
                Check("MapInfo.csv에 맵이 2개 이상", false);
                return;
            }

            int iFirst  = cTable.ALL[0].iMapID;
            int iSecond = cTable.ALL[1].iMapID;

            CFakeProgressRepository cRepo = new CFakeProgressRepository();
            CProgress_Manager cProgress = new CProgress_Manager();
            Check("진행도 초기화", cProgress.Initialize(cTable, cRepo));

            // 처음에는 첫 맵만 열려 있다
            Check("첫 맵은 항상 열림", cProgress.Is_Unlocked(iFirst));
            Check("두 번째 맵은 잠김", cProgress.Is_Unlocked(iSecond) == false);
            Check("표에 없는 맵은 잠김", cProgress.Is_Unlocked(99999) == false);

            // 첫 맵을 깨면 다음이 열린다
            cProgress.Set_Star(iFirst, 1);
            Check("클리어 기록됨", cProgress.Is_Cleared(iFirst));
            Check("클리어하면 다음 맵이 열림", cProgress.Is_Unlocked(iSecond));
            Check("클리어 수", cProgress.CLEARED_COUNT, 1);
            Check("저장이 호출됨", cRepo.SAVE_COUNT > 0);

            // 같은 맵을 또 깨도 기록은 늘지 않는다
            int iSaveCount = cRepo.SAVE_COUNT;
            cProgress.Set_Star(iFirst, 1);
            Check("중복 클리어는 저장하지 않음", cRepo.SAVE_COUNT, iSaveCount);
            Check("중복 클리어로 수가 늘지 않음", cProgress.CLEARED_COUNT, 1);

            // 디버그 전체 개방
            CProgress_Manager cFresh = new CProgress_Manager();
            cFresh.Initialize(cTable, new CFakeProgressRepository());
            Check("개방 전에는 잠김", cFresh.Is_Unlocked(iSecond) == false);
            cFresh.Set_UnlockAll(true);
            Check("디버그 개방 시 전부 열림", cFresh.Is_Unlocked(iSecond));
            Check("개방 플래그 반영", cFresh.IS_UNLOCK_ALL);

            // 마지막 맵 기억 — 잠긴 맵을 기억하고 있으면 첫 맵으로 되돌린다
            cProgress.Set_LastMap(iSecond);
            Check("마지막 맵 기억", cProgress.Get_LastMapID(), iSecond);

            // 저장 왕복 (JSON 직렬화가 깨지지 않는지)
            CStageProgress cSaved = cRepo.STORED;
            Check("저장된 기록에 클리어가 담김", cSaved != null && cSaved.Is_Cleared(iFirst));
        }

        /// <summary> 테스트용 메모리 저장소 — PlayerPrefs를 건드리지 않는다. </summary>
        private class CFakeProgressRepository : IStageProgress
        {
            private CStageProgress m_cStored = new CStageProgress();
            private int m_iSaveCount;

            public CStageProgress STORED     => m_cStored;
            public int            SAVE_COUNT => m_iSaveCount;

            public CStageProgress Load() => m_cStored;

            public void Save(CStageProgress cProgress)
            {
                // 실제 저장소처럼 JSON을 한 번 왕복시켜 직렬화가 깨지는지 함께 본다.
                m_cStored = JsonUtility.FromJson<CStageProgress>(JsonUtility.ToJson(cProgress));
                ++m_iSaveCount;
            }
        }

        private static CCSVData_MapInfo Load_MapTable()
        {
            TextAsset cText = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Data/MapInfo.csv");
            if (cText == null)
                return null;

            CCSVData_MapInfo cTable = new CCSVData_MapInfo();
            cTable.Read_CSVData(cText);
            return cTable;
        }
        // 260904_가상 조이스틱 — 판정만 떼어 두었으므로 화면 없이 검증할 수 있다
        private static void Test_Joystick()
        {
            const int   SCREEN_H = 1000;
            const float RADIUS   = 100f;
            const float DEADZONE = 25f;

            // 4방향 양자화 — 더 많이 기운 축을 따른다
            Check("오른쪽", CVirtualJoystick.To_Dir(new Vector2(50f, 10f), DEADZONE) == MOVE_DIR.RIGHT);
            Check("왼쪽",   CVirtualJoystick.To_Dir(new Vector2(-50f, 10f), DEADZONE) == MOVE_DIR.LEFT);
            Check("위",     CVirtualJoystick.To_Dir(new Vector2(10f, 50f), DEADZONE) == MOVE_DIR.UP);
            Check("아래",   CVirtualJoystick.To_Dir(new Vector2(10f, -50f), DEADZONE) == MOVE_DIR.DOWN);
            Check("데드존 안은 방향 없음",
                  CVirtualJoystick.To_Dir(new Vector2(10f, 10f), DEADZONE) == MOVE_DIR.NONE);

            CVirtualJoystick cJoystick = new CVirtualJoystick();
            cJoystick.Initialize(RADIUS, DEADZONE, 0.6f);
            Check("처음에는 비활성", cJoystick.IS_ACTIVE == false);

            // 화면 위쪽(활성 영역 밖)에서는 잡히지 않는다
            cJoystick.Update_State(true, new Vector2(500f, 900f), SCREEN_H);
            Check("활성 영역 밖에서는 안 잡힘", cJoystick.IS_ACTIVE == false);

            // 아래쪽에서 누르면 그 자리가 중심이 된다 (플로팅)
            Vector2 vPress = new Vector2(300f, 200f);
            cJoystick.Update_State(true, vPress, SCREEN_H);
            Check("아래쪽에서 잡힘", cJoystick.IS_ACTIVE);
            Check("누른 자리가 중심", cJoystick.ORIGIN == vPress);
            Check("잡은 직후엔 방향 없음", cJoystick.DIR == MOVE_DIR.NONE);

            // 오른쪽으로 끌면 오른쪽
            cJoystick.Update_State(true, vPress + new Vector2(60f, 0f), SCREEN_H);
            Check("끌면 방향이 생김", cJoystick.DIR == MOVE_DIR.RIGHT);
            Check("중심은 그대로", cJoystick.ORIGIN == vPress);

            // 반경을 넘겨도 손잡이는 반경 안에 머문다
            cJoystick.Update_State(true, vPress + new Vector2(500f, 0f), SCREEN_H);
            Check("손잡이가 반경을 넘지 않음",
                  Vector2.Distance(cJoystick.ORIGIN, cJoystick.HANDLE) <= RADIUS + 0.01f);
            Check("반경 밖에서도 방향 유지", cJoystick.DIR == MOVE_DIR.RIGHT);

            // 한 번 잡은 뒤에는 활성 영역 밖으로 끌어도 놓지 않는다
            cJoystick.Update_State(true, new Vector2(300f, 950f), SCREEN_H);
            Check("잡은 뒤에는 위로 끌어도 유지", cJoystick.IS_ACTIVE);
            Check("위로 끌면 위쪽", cJoystick.DIR == MOVE_DIR.UP);

            // 떼면 초기화
            cJoystick.Update_State(false, Vector2.zero, SCREEN_H);
            Check("떼면 비활성", cJoystick.IS_ACTIVE == false);
            Check("떼면 방향 없음", cJoystick.DIR == MOVE_DIR.NONE);
        }
        #endregion 테스트 케이스

        #region 프리뷰 렌더
        /// <summary>
        /// 실제 스테이지 크기로 몇 번 점령시킨 뒤, CGridRenderer가 만든 마스크를 배경 위에 합성해 PNG로 저장한다.
        /// "점령한 만큼 뒤 이미지가 드러난다"를 에디터를 열지 않고 눈으로 확인하는 용도.
        /// 배치모드: -executeMethod Client.CProtoTest.Render_Preview
        /// </summary>
        [MenuItem("Tools/LandGrab/Render Preview PNG")]
        public static void Render_Preview()
        {
            // 260904_스테이지 규칙은 MapInfo.csv로 옮겼다. 프리뷰도 같은 표를 읽어 크기를 맞춘다.
            CMapInfo cMapInfo = CProtoSetup.Load_MapInfo(1);

            CTerritoryGrid cGrid = new CTerritoryGrid();
            cGrid.Initialize(cMapInfo.iGridWidth, cMapInfo.iGridHeight, cMapInfo.fCellSize,
                             Vector2.zero, cMapInfo.iBorderThick);

            CMoveHandler cMove = new CMoveHandler();
            cMove.Initialize(cGrid, new Vector2Int(cMapInfo.iGridWidth / 2, cMapInfo.iBorderThick - 1), STEP_SPEED);

            // 1차 점령 — 오른쪽 아래 사각형
            Walk(cGrid, cMove, MOVE_DIR.UP, 30, null);
            Walk(cGrid, cMove, MOVE_DIR.RIGHT, 20, null);
            Walk(cGrid, cMove, MOVE_DIR.DOWN, 30, null);

            // 260902_이제 점령지 내부를 가로지를 수 없으므로 '선'을 따라 우회해서 다음 출발점으로 간다
            Walk(cGrid, cMove, MOVE_DIR.RIGHT, 5, null);

            // 2차 점령 — 위쪽 큰 ㄱ자
            Walk(cGrid, cMove, MOVE_DIR.UP, 60, null);
            Walk(cGrid, cMove, MOVE_DIR.LEFT, 20, null);
            Walk(cGrid, cMove, MOVE_DIR.DOWN, 25, null);
            Walk(cGrid, cMove, MOVE_DIR.LEFT, 10, null);
            Walk(cGrid, cMove, MOVE_DIR.DOWN, 35, null);

            // 실제 런타임과 동일한 경로로 마스크를 만든다.
            GameObject goOverlay = new GameObject("Overlay_Preview");
            SpriteRenderer srCover = goOverlay.AddComponent<SpriteRenderer>();

            CGridRenderer cRenderer = new CGridRenderer();
            cRenderer.Initialize(cGrid, srCover, null);     // 프리뷰는 배경을 직접 합성하므로 reveal이 없다

            Texture2D texMask = srCover.sprite.texture;
            // 임포트된 스프라이트는 Read/Write가 꺼져 있으므로 원본 PNG를 직접 디코드한다.
            Texture2D texBG = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texBG.LoadImage(File.ReadAllBytes("Assets/Art/Tex_Reward_Placeholder.png"));

            Texture2D texOut = Composite(texBG, texMask);
            File.WriteAllBytes("proto_preview.png", texOut.EncodeToPNG());

            Debug.Log($"[CProtoTest] 프리뷰 저장 — 점령률 {cGrid.OWNED_RATIO:P1} → proto_preview.png");

            Object.DestroyImmediate(texOut);
            Object.DestroyImmediate(goOverlay);

            if (Application.isBatchMode == true)
                EditorApplication.Exit(0);
        }

        /// <summary> 배경 위에 마스크를 알파 합성한다 (셰이더 없이 SpriteRenderer가 하는 일과 동일). </summary>
        private static Texture2D Composite(Texture2D texBG, Texture2D texMask)
        {
            int iWidth  = texBG.width;
            int iHeight = texBG.height;

            Color32[] arrBG   = texBG.GetPixels32();
            Color32[] arrMask = texMask.GetPixels32();
            Color32[] arrOut  = new Color32[iWidth * iHeight];

            for (int y = 0; y < iHeight; ++y)
            {
                int my = Mathf.Clamp(y * texMask.height / iHeight, 0, texMask.height - 1);

                for (int x = 0; x < iWidth; ++x)
                {
                    int mx = Mathf.Clamp(x * texMask.width / iWidth, 0, texMask.width - 1);

                    Color cBG   = arrBG[y * iWidth + x];
                    Color cMask = arrMask[my * texMask.width + mx];
                    Color cOut  = Color.Lerp(cBG, cMask, cMask.a);
                    cOut.a = 1f;

                    arrOut[y * iWidth + x] = cOut;
                }
            }

            Texture2D texOut = new Texture2D(iWidth, iHeight, TextureFormat.RGBA32, false);
            texOut.SetPixels32(arrOut);
            texOut.Apply();
            return texOut;
        }
        #endregion 프리뷰 렌더

        #region 헬퍼
        private static CTerritoryGrid Make_Grid()
        {
            CTerritoryGrid cGrid = new CTerritoryGrid();
            cGrid.Initialize(GRID_SIZE, GRID_SIZE, 1f, Vector2.zero, BORDER_THICK);
            return cGrid;
        }

        private static CMoveHandler Make_Move(CTerritoryGrid cGrid)
        {
            CMoveHandler cMove = new CMoveHandler();
            cMove.Initialize(cGrid, new Vector2Int(GRID_SIZE / 2, BORDER_THICK - 1), STEP_SPEED);
            return cMove;
        }

        /// <summary> (10,0)에서 출발해 ㄷ자로 돌아 안전 지대로 복귀하는 닫힌 도형. 점령 칸 수 반환. </summary>
        private static int Walk_ClosedLoop(CTerritoryGrid cGrid, IReadOnlyList<Vector2Int> lstEnemy,
                                           out CMoveHandler cMove)
        {
            cMove = Make_Move(cGrid);

            Walk(cGrid, cMove, MOVE_DIR.UP, 5, lstEnemy);       // (10,1)~(10,5)
            Walk(cGrid, cMove, MOVE_DIR.LEFT, 7, lstEnemy);     // (9,5)~(3,5)
            return Walk(cGrid, cMove, MOVE_DIR.DOWN, 5, lstEnemy);  // (3,4)~(3,1), 마지막 (3,0)에서 점령
        }

        /// <summary> 지정 방향으로 iStep칸 이동시키며 도착할 때마다 규칙을 적용한다. 마지막 점령 칸 수 반환. </summary>
        private static int Walk(CTerritoryGrid cGrid, CMoveHandler cMove, MOVE_DIR eDir, int iStep,
                                IReadOnlyList<Vector2Int> lstEnemy)
        {
            int iCaptured = 0;

            for (int i = 0; i < iStep; ++i)
            {
                if (cMove.Tick(1f, eDir, out Vector2Int vArrived) == false)
                    continue;

                if (cGrid.Step_To(vArrived, lstEnemy, out int iCount) == STEP_RESULT.CAPTURE)
                    iCaptured = iCount;
            }

            return iCaptured;
        }

        private static int Count_Owned(CTerritoryGrid cGrid)
        {
            int iCount = 0;
            for (int y = 0; y < cGrid.HEIGHT; ++y)
            {
                for (int x = 0; x < cGrid.WIDTH; ++x)
                {
                    if (cGrid.Get_Cell(x, y) == CELL_STATE.OWNED)
                        ++iCount;
                }
            }
            return iCount;
        }

        private static void Check(string strName, bool bCondition)
        {
            if (bCondition == true)
            {
                ++s_iPass;
                s_sbLog.AppendLine($"  PASS  {strName}");
                return;
            }

            ++s_iFail;
            s_sbLog.AppendLine($"  FAIL  {strName}");
        }

        private static void Check(string strName, int iActual, int iExpect)
        {
            if (iActual == iExpect)
            {
                ++s_iPass;
                s_sbLog.AppendLine($"  PASS  {strName} = {iActual}");
                return;
            }

            ++s_iFail;
            s_sbLog.AppendLine($"  FAIL  {strName} : expect {iExpect}, actual {iActual}");
        }
        #endregion 헬퍼
    }
}
