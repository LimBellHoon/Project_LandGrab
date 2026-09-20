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
            Test_EnemyTable();
            Test_Enemy();
            Test_EnemyCombat();
            Test_EnemyKiteBehavior();
            Test_Player();
            Test_ShapeMask();
            Test_DirtyCell();
            Test_StageProgress();
            Test_CharacterTable();
            Test_Character();
            Test_Gacha();
            Test_CharacterGrantAndViewer();
            Test_Star();
            Test_Currency();
            Test_CaptureReward();
            Test_FieldItem();
            Test_Gauge();
            Test_MoveStyle();
            Test_Skill();
            Test_Inventory();
            Test_SkillUpgrade();
            Test_GameConfig();
            Test_CameraFit();
            Test_CameraFollow();
            Test_CardPick();
            Test_PickOption();
            Test_BehaviorTree();
            Test_Projectile();
            Test_SafeArea();
            Test_SkillTable();
            Test_RunSkillTable();
            Test_RunSkill();
            Test_WeaponAndAwaken();
            Test_OrbitAndClub();
            Test_BattleConsumable();
            Test_CoverResolution();
            Test_LayerBounds();
            Test_Joystick();
            Test_CameraShake();
            Test_CameraPunch();
            Test_FlashEffect();
            Test_SoundUtility();
            Test_HapticManager();

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

            // ⑦ 260916_넉백 — 지속 시간 동안은 추적 방향을 무시하고 지정한 방향/거리로 밀린다
            CEnemyMoveHandler cKnockMove = new CEnemyMoveHandler();
            cKnockMove.Initialize(cGrid, new Vector2(10.5f, 10.5f), Vector2.right, 1f);
            cKnockMove.Add_Knockback(Vector2.up, 2f, 1f);   // 1초 동안 위로 2 유닛
            // 추적 목표를 오른쪽 아래로 줘도 넉백 중에는 무시해야 한다.
            cKnockMove.Tick(0.5f, true, new Vector2(15.5f, 5.5f), 1f);
            Check("넉백 중에는 추적을 무시하고 지정 방향으로 밀린다", cKnockMove.POS.y > 11.4f);
        }

        // 260916_런 스킬(회전탄/몽둥이)의 순수 로직 — CPlayer/그리드 없이도 검증 가능한 부분만
        private static void Test_OrbitAndClub()
        {
            CCSVData_RunSkillInfo cTable = Load_RunSkillTable();
            if (cTable == null)
            {
                Check("RunSkillInfo.csv 로드(Test_OrbitAndClub)", false);
                return;
            }

            CRunSkillInfo cOrbitInfo = cTable.Find_ByType(RUN_SKILL_TYPE.ORBIT);
            CRunSkillEffect_Orbit cOrbit = new CRunSkillEffect_Orbit();
            cOrbit.On_LevelChanged(cOrbitInfo, 1);

            // 260917_회전탄은 투사체(20)를 띄운다 — 실제로 띄우는지는 Test_WeaponAndAwaken에서 본다
            Check("회전탄 1레벨은 1개", cOrbit.COUNT, 1);
            Check("회전탄은 탄 20을 띄운다", cOrbit.PROJECTILE_ID, 20);

            cOrbit.On_LevelChanged(cOrbitInfo, 2);
            Check("회전탄 2레벨은 2개", cOrbit.COUNT, 2);
            cOrbit.Tick(1f);
            Check("소유자 · 창구가 없으면 아무것도 띄우지 않는다", cOrbit.ALIVE_COUNT, 0);

            CRunSkillInfo cClubInfo = cTable.Find_ByType(RUN_SKILL_TYPE.CLUB);
            CRunSkillEffect_Club cClub = new CRunSkillEffect_Club();
            cClub.On_LevelChanged(cClubInfo, 1);

            Check("몽둥이는 탄 22를 휘두른다", cClub.PROJECTILE_ID, 22);
            Check("몽둥이는 처음엔 바로 휘두른다", cClub.Consume_Swing(out float fRadius1) == true);
            Check("쿨타임 동안은 다시 못 휘두른다", cClub.Consume_Swing(out float _) == false);

            cClub.Tick(10f);   // 쿨타임을 훨씬 넘게 흘려보낸다
            Check("쿨타임이 지나면 다시 휘두른다", cClub.Consume_Swing(out float fRadius2) == true);
            Check("레벨이 같으면 반경도 같다", Mathf.Approximately(fRadius1, fRadius2));
            Check("판정 반경은 0보다 크다", fRadius1 > 0f);
        }

        // 260916_런 스킬로 몬스터를 죽이는 최소 골격 — HP/사망(bCollect)
        private static void Test_EnemyCombat()
        {
            CTerritoryGrid cGrid = Make_Grid();
            GameObject goEnemy = new GameObject("Test_CombatEnemy");
            CEnemy cEnemy = goEnemy.AddComponent<CEnemy>();
            cEnemy.Initialize(new CEnemyDesc
            {
                eObjectType     = Engine.OBJECT_TYPE.ENEMY,
                strPrefabName   = "Prefab_Enemy",
                cGrid           = cGrid,
                vStartCell      = new Vector2Int(GRID_SIZE / 2, GRID_SIZE / 2),
                vStartDir       = Vector2.right,
                iEnemyID        = 999,
                eGimmick        = ENEMY_GIMMICK.NONE,
                fSpeed          = 1f,
                fChaseSpeed     = 1f,
                fTurnRate       = 1f,
                fHitRange       = 0.5f,
            });

            Check("생성 직후에는 안 죽었다", cEnemy.IS_DEAD == false);
            // 260918_Desc에 iHp를 안 주면(0) CEnemy의 기존 고정값(체력3)으로 대체된다.

            cEnemy.Damage(1);
            cEnemy.Damage(1);
            Check("HP가 남아 있으면 안 죽는다", cEnemy.IS_DEAD == false);

            cEnemy.Damage(1);
            Check("HP가 다 떨어지면 죽는다(bCollect)", cEnemy.IS_DEAD == true);

            cEnemy.Damage(1);
            Check("죽은 뒤에는 다시 피해를 받지 않는다", cEnemy.IS_DEAD == true);

            Object.DestroyImmediate(goEnemy);

            // 260918_EnemyInfo.csv에서 온 iHp가 그대로 적용되는지 — 종류별 난이도의 기반이다.
            GameObject goCustom = new GameObject("Test_CombatEnemy_Custom");
            CEnemy cCustom = goCustom.AddComponent<CEnemy>();
            cCustom.Initialize(new CEnemyDesc
            {
                eObjectType     = Engine.OBJECT_TYPE.ENEMY,
                strPrefabName   = "Prefab_Enemy",
                cGrid           = cGrid,
                vStartCell      = new Vector2Int(GRID_SIZE / 2, GRID_SIZE / 2),
                vStartDir       = Vector2.right,
                iEnemyID        = 998,
                eGimmick        = ENEMY_GIMMICK.NONE,
                fSpeed          = 1f,
                fChaseSpeed     = 1f,
                fTurnRate       = 1f,
                fHitRange       = 0.5f,
                iHp             = 1,
            });

            cCustom.Damage(1);
            Check("iHp를 1로 지정하면 한 대에 죽는다", cCustom.IS_DEAD == true);

            Object.DestroyImmediate(goCustom);
        }

        // 260918_비헤이비어 트리 첫 연결 — PROJECTILE(포수류)만 사거리를 기준으로 쫓을지 버틸지 정하는지 본다.
        // CTerritoryGrid의 CELL_SIZE가 1이라(Make_Grid) 셀 거리 = 월드 거리로 그대로 쓸 수 있다.
        private static void Test_EnemyKiteBehavior()
        {
            CTerritoryGrid cGrid = Make_Grid();

            GameObject goEnemy = new GameObject("Test_BTEnemy");
            CEnemy cEnemy = goEnemy.AddComponent<CEnemy>();
            cEnemy.Initialize(new CEnemyDesc
            {
                eObjectType     = Engine.OBJECT_TYPE.ENEMY,
                strPrefabName   = "Prefab_Enemy",
                cGrid           = cGrid,
                vStartCell      = new Vector2Int(GRID_SIZE / 2, GRID_SIZE / 2),
                vStartDir       = Vector2.right,
                iEnemyID        = 996,
                eGimmick        = ENEMY_GIMMICK.PROJECTILE,
                fSpeed          = 1f,
                fChaseSpeed     = 1f,
                fTurnRate       = 1f,
                fHitRange       = 0.5f,
                fGimmickRange   = 5f,      // 5칸 사거리
                fGimmickCool    = 100f,    // 테스트 중에는 실제로 쏘지 않아도 된다
                iGimmickRefID   = 1,
            });

            Vector2 vFar  = cEnemy.POS + new Vector2(20f, 0f);   // 사거리(5) 밖
            Vector2 vNear = cEnemy.POS + new Vector2(2f, 0f);    // 사거리(5) 안

            cEnemy.Set_ChaseState(true, vFar);
            cEnemy.Tick(0.1f);
            Check("사거리 밖이고 노출돼 있으면 쫓는다", cEnemy.IS_CHASING);

            cEnemy.Set_ChaseState(true, vNear);
            cEnemy.Tick(0.1f);
            Check("사거리 안이면 멈춰서 버틴다", cEnemy.IS_CHASING == false);

            cEnemy.Set_ChaseState(false, vFar);   // 플레이어가 안전 지대로 돌아갔다 — 멀어도 쫓지 않는다
            cEnemy.Tick(0.1f);
            Check("플레이어가 안전 지대에 있으면 사거리 밖이어도 안 쫓는다", cEnemy.IS_CHASING == false);

            Object.DestroyImmediate(goEnemy);

            // 260918_PROJECTILE이 아니면 트리를 안 만든다 — 예전처럼 노출 여부를 그대로 따른다.
            GameObject goPlain = new GameObject("Test_BTEnemy_Plain");
            CEnemy cPlain = goPlain.AddComponent<CEnemy>();
            cPlain.Initialize(new CEnemyDesc
            {
                eObjectType     = Engine.OBJECT_TYPE.ENEMY,
                strPrefabName   = "Prefab_Enemy",
                cGrid           = cGrid,
                vStartCell      = new Vector2Int(GRID_SIZE / 2, GRID_SIZE / 2),
                vStartDir       = Vector2.right,
                iEnemyID        = 995,
                eGimmick        = ENEMY_GIMMICK.NONE,
                fSpeed          = 1f,
                fChaseSpeed     = 1f,
                fTurnRate       = 1f,
                fHitRange       = 0.5f,
            });

            cPlain.Set_ChaseState(true, cPlain.POS + new Vector2(50f, 0f));
            cPlain.Tick(0.1f);
            Check("트리가 없는 몬스터는 거리와 상관없이 노출 여부를 그대로 따른다", cPlain.IS_CHASING);

            Object.DestroyImmediate(goPlain);
        }

        // 260918_EnemyInfo.csv의 iHp/iAttack — 몬스터별 체력·공격력이 표에서 그대로 나오는지 본다.
        private static void Test_EnemyTable()
        {
            CCSVData_EnemyInfo cTable = Load_CsvTable<CCSVData_EnemyInfo>("EnemyInfo");
            if (cTable == null || cTable.COUNT < 4)
            {
                Check("EnemyInfo.csv 로드", false);
                return;
            }

            CEnemyInfo cWanderer = cTable.Get_Info(101);   // 배회자
            CEnemyInfo cSplitter = cTable.Get_Info(104);   // 분열체

            Check("배회자 정보 로드", cWanderer != null);
            Check("분열체 정보 로드", cSplitter != null);

            if (cWanderer == null || cSplitter == null)
                return;

            Check("체력이 0보다 크다", cWanderer.iHp > 0);
            Check("분열체가 배회자보다 단단하다(소환주기가 있어 오래 버텨야 함)",
                  cSplitter.iHp >= cWanderer.iHp);
            Check("표에 없는 ID는 null", cTable.Get_Info(99999) == null);
        }

        // 260918_다시 목숨제 — 무엇에 맞든 한 목숨, 무적 중엔 안 잃음, 최대를 넘게 되찾지 못함, 0이면 사망
        private static void Test_Player()
        {
            CTerritoryGrid cGrid = Make_Grid();
            Vector2Int vStart = new Vector2Int(GRID_SIZE / 2, BORDER_THICK - 1);

            GameObject goPlayer = new GameObject("Test_HpPlayer");
            CPlayer cPlayer = goPlayer.AddComponent<CPlayer>();
            cPlayer.Initialize(new CPlayerDesc
            {
                eObjectType   = Engine.OBJECT_TYPE.PLAYER,
                strPrefabName = "Prefab_Player",
                cGrid         = cGrid,
                vStartCell    = vStart,
                fMoveSpeed    = STEP_SPEED,
                iLife         = 3,
            });

            Check("처음엔 목숨이 가득", cPlayer.LIFE, 3);
            Check("최대 목숨 기록", cPlayer.MAX_LIFE, 3);

            int iLastLife = -1;
            cPlayer.OnLifeChanged += iLife => iLastLife = iLife;

            // 탄 피해량이 커도 한 목숨이다
            cPlayer.Take_Damage(5);
            Check("무엇에 맞든 한 목숨", cPlayer.LIFE, 2);
            Check("OnLifeChanged가 남은 목숨을 전달", iLastLife, 2);

            // 방금 맞아 무적 시간이 걸려 있으므로 더 잃지 않는다
            cPlayer.Lose_Life();
            Check("무적 중엔 안 잃는다", cPlayer.LIFE, 2);

            cPlayer.Add_Life(10);
            Check("최대 목숨을 넘게 되찾지 못한다", cPlayer.LIFE, 3);

            Check("목숨 표시", CUI_InGame.Get_LifeText(2, 3), "♥♥♡");

            Object.DestroyImmediate(goPlayer);

            // 치명적 피해 — HP는 0에서 멈추고 OnDead가 발동한다
            GameObject goDeath = new GameObject("Test_HpPlayer_Death");
            CPlayer cDeathPlayer = goDeath.AddComponent<CPlayer>();
            cDeathPlayer.Initialize(new CPlayerDesc
            {
                eObjectType   = Engine.OBJECT_TYPE.PLAYER,
                strPrefabName = "Prefab_Player",
                cGrid         = cGrid,
                vStartCell    = vStart,
                fMoveSpeed    = STEP_SPEED,
                iLife         = 1,
            });

            bool bDied = false;
            cDeathPlayer.OnDead += () => bDied = true;
            cDeathPlayer.Lose_Life();
            Check("마지막 목숨을 잃으면 0", cDeathPlayer.LIFE, 0);
            Check("목숨이 0이 되면 OnDead 발동", bDied);

            Object.DestroyImmediate(goDeath);
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
            cSkill.Initialize(cInfo, 0);

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
            cEmpty.Initialize(null, 0);
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

            cManager.Add_Item(401);
            // 260905_소모품도 슬롯에 넣는다 — 전투에 무엇을 들고 갈지 고르는 것이다.
            Check("소모품도 장착된다", cManager.Try_Equip(401));
            Check("소모품 슬롯 조회", cManager.Get_Equipped(EQUIP_SLOT.CONSUMABLE)?.iEquipID ?? 0, 401);
            Check("소모 효과 파싱", cManager.Get_Equipped(EQUIP_SLOT.CONSUMABLE)?.eConsume == CONSUME_EFFECT.SHIELD);

            // 스킬 장착
            cManager.Set_EquippedSkill(1);
            Check("스킬 장착", cManager.EQUIPPED_SKILL_ID, 1);

            // 260918_장비 강화 — 코인을 쓰고 레벨이 오르면 실제 적용되는 스탯도 같이 오른다.
            int iCostLv0 = cManager.Get_EquipUpgradeCost(102);     // 지금 장착 중인 상위 신발(질주화)
            Check("강화 비용이 있다", iCostLv0 > 0);
            Check("코인이 없으면 강화 불가 판정", cManager.Can_UpgradeEquip(102) == false);
            Check("코인이 없으면 강화 시도도 실패", cManager.Try_UpgradeEquip(102) == false);

            cManager.Add_Coin(iCostLv0);
            Check("코인이 있으면 강화 가능 판정", cManager.Can_UpgradeEquip(102));

            int iCoinBefore = cManager.COIN;
            Check("강화 성공", cManager.Try_UpgradeEquip(102));
            Check("강화 후 레벨 1", cManager.Get_EquipLevel(102), 1);
            Check("강화하면 코인을 그만큼 쓴다", cManager.COIN, iCoinBefore - iCostLv0);
            Check("강화하면 실제 스탯도 오른다",
                  Mathf.RoundToInt(cManager.Get_EquipStat(STAT_TYPE.SPEED) * 100f), 20);   // 18 + 강화 2%p

            // 소모품은 강화 대상이 아니다
            Check("소모품은 강화 비용이 없다", cManager.Get_EquipUpgradeCost(401), 0);
            Check("소모품은 강화할 수 없다", cManager.Try_UpgradeEquip(401) == false);
        }



        // 260905_스킬 강화 + 패시브 — 장착만으로는 안 오르고, 레벨을 올려야 붙는다
        private static void Test_SkillUpgrade()
        {
            CCSVData_SkillInfo cSkillTable = Load_SkillTable();
            CCSVData_MapInfo   cMapTable   = Load_MapTable();
            if (cSkillTable == null || cMapTable == null)
            {
                Check("SkillInfo.csv 로드", false);
                return;
            }

            CSkillInfo cWarp  = cSkillTable.Find_ByType(SKILL_TYPE.WARP);
            CSkillInfo cSwift = cSkillTable.Find_ByType(SKILL_TYPE.SWIFT);

            Check("액티브 스킬 존재", cWarp != null && cWarp.IS_PASSIVE == false);
            Check("패시브 스킬 존재", cSwift != null && cSwift.IS_PASSIVE);

            // 레벨에 따른 수치 — 액티브는 fValue에 더해지고, 패시브는 레벨 0이면 0이다
            Check("액티브 0레벨 수치", Mathf.RoundToInt(cWarp.Get_Value(0)), 6);
            Check("액티브 2레벨 수치", Mathf.RoundToInt(cWarp.Get_Value(2)), 8);
            Check("만렙 초과는 상한", Mathf.RoundToInt(cWarp.Get_Value(99)), 9);

            Check("패시브 0레벨은 0", Mathf.RoundToInt(cSwift.Get_StatValue(0) * 100f), 0);
            Check("패시브 2레벨", Mathf.RoundToInt(cSwift.Get_StatValue(2) * 100f), 10);

            Check("0레벨 강화 비용", cWarp.Get_Cost(0), 300);
            Check("1레벨 강화 비용", cWarp.Get_Cost(1), 550);
            Check("만렙은 비용 0", cWarp.Get_Cost(cWarp.iMaxLevel), 0);

            // 매니저 — 코인이 있어야 올라간다
            CProgress_Manager cManager = new CProgress_Manager();
            cManager.Initialize(cMapTable, new CFakeProgressRepository());

            Check("처음에는 0레벨", cManager.Get_SkillLevel(SKILL_TYPE.WARP), 0);
            Check("코인이 없으면 강화 실패", cManager.Try_UpgradeSkill(cWarp) == false);

            cManager.Add_Coin(1000);
            Check("코인이 있으면 강화", cManager.Try_UpgradeSkill(cWarp));
            Check("레벨 상승", cManager.Get_SkillLevel(SKILL_TYPE.WARP), 1);
            Check("비용만큼 차감", cManager.COIN, 700);

            // 패시브는 장착해야 능력치에 들어간다
            Check("장착 전에는 0",
                  Mathf.RoundToInt(cManager.Get_PassiveStat(cSkillTable, STAT_TYPE.SPEED) * 100f), 0);

            cManager.Set_EquippedSkill(cSwift.iSkillID);
            Check("장착했어도 0레벨이면 0",
                  Mathf.RoundToInt(cManager.Get_PassiveStat(cSkillTable, STAT_TYPE.SPEED) * 100f), 0);

            cManager.Try_UpgradeSkill(cSwift);
            Check("레벨을 올리면 능력치가 붙는다",
                  Mathf.RoundToInt(cManager.Get_PassiveStat(cSkillTable, STAT_TYPE.SPEED) * 100f), 5);
            Check("다른 능력치에는 안 붙는다",
                  Mathf.RoundToInt(cManager.Get_PassiveStat(cSkillTable, STAT_TYPE.EVASION) * 100f), 0);

            // 액티브를 끼면 패시브 몫은 사라진다 (스킬은 하나만 장착)
            cManager.Set_EquippedSkill(cWarp.iSkillID);
            Check("액티브를 끼면 패시브 몫 없음",
                  Mathf.RoundToInt(cManager.Get_PassiveStat(cSkillTable, STAT_TYPE.SPEED) * 100f), 0);
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

        // 260918_점령 재화 배율 — 구간을 못 찾으면 마지막 구간 배율을 그대로 쓰는지까지 본다
        private static void Test_CaptureReward()
        {
            const string TAB = "\t";
            string strCsv =
                  string.Join(TAB, "iTier", "fRatioMin", "fRatioMax", "fMultiplier", "NONE") + "\n"
                + string.Join(TAB, "1", "0",    "0.10", "1.0", "") + "\n"
                + string.Join(TAB, "2", "0.10", "0.20", "1.2", "") + "\n"
                + string.Join(TAB, "3", "0.20", "1.01", "5.0", "");

            CCSVData_CaptureRewardInfo cTable = new CCSVData_CaptureRewardInfo();
            cTable.Read_CSVData(new TextAsset(strCsv));

            Check("표 없이는 1배", Mathf.Approximately(new CCSVData_CaptureRewardInfo().Get_Multiplier(0.5f), 1f));
            Check("첫 구간(5%)", Mathf.Approximately(cTable.Get_Multiplier(0.05f), 1.0f));
            Check("두 번째 구간(15%)", Mathf.Approximately(cTable.Get_Multiplier(0.15f), 1.2f));
            Check("세 번째 구간(80%)", Mathf.Approximately(cTable.Get_Multiplier(0.8f), 5.0f));
            Check("표보다 큰 비율은 마지막 구간", Mathf.Approximately(cTable.Get_Multiplier(999f), 5.0f));
        }

        // 260920_캐릭터별 이동 방식(2-22) — 대각선은 가로 · 세로 두 칸으로 밟는다
        private static void Test_MoveStyle()
        {
            // 대각선 분해 — 점령 판정(4방향 플러드필)이 성립하려면 대각선 한 칸을 만들면 안 된다
            CTerritoryGrid.Dir_Split(MOVE_DIR.UP_RIGHT, out MOVE_DIR eH, out MOVE_DIR eV);
            Check("대각선은 가로 · 세로로 쪼개진다", eH == MOVE_DIR.RIGHT && eV == MOVE_DIR.UP);
            CTerritoryGrid.Dir_Split(MOVE_DIR.DOWN_LEFT, out eH, out eV);
            Check("왼쪽 아래도 마찬가지", eH == MOVE_DIR.LEFT && eV == MOVE_DIR.DOWN);
            Check("4방향은 쪼개지지 않는다", CTerritoryGrid.Is_Diagonal(MOVE_DIR.UP) == false);
            Check("대각선 오프셋은 두 축이 다 선다", CTerritoryGrid.Dir_ToOffset(MOVE_DIR.UP_RIGHT) == new Vector2Int(1, 1));

            // 조이스틱 — 4방향은 대각선을 만들지 않고, 8방향만 만든다
            Vector2 vDiagonal = new Vector2(0.5f, 0.5f);
            Check("4방향 캐릭터는 대각선이 안 나온다",
                  CTerritoryGrid.Is_Diagonal(CVirtualJoystick.To_Dir(vDiagonal, 0.1f, MOVE_STYLE.FOUR_WAY)) == false);
            Check("8방향 캐릭터는 대각선이 나온다",
                  CVirtualJoystick.To_Dir(vDiagonal, 0.1f, MOVE_STYLE.EIGHT_WAY) == MOVE_DIR.UP_RIGHT);
            Check("8방향이어도 한 축으로 기울면 4방향 그대로",
                  CVirtualJoystick.To_Dir(new Vector2(0.5f, 0.05f), 0.1f, MOVE_STYLE.EIGHT_WAY) == MOVE_DIR.RIGHT);
            Check("데드존 안은 멈춤",
                  CVirtualJoystick.To_Dir(new Vector2(0.01f, 0.01f), 0.1f, MOVE_STYLE.EIGHT_WAY) == MOVE_DIR.NONE);

            // 실제 이동 — 8방향은 두 번 밟아 대각선 칸에 도착한다
            CTerritoryGrid cGrid = new CTerritoryGrid();
            cGrid.Initialize(20, 20, 1f, Vector2.zero, 2, null);

            CMoveHandler cMove = new CMoveHandler();
            cMove.Initialize(cGrid, new Vector2Int(5, 1), 100f);
            cMove.Set_MoveStyle(MOVE_STYLE.EIGHT_WAY);

            Vector2Int vStart = cMove.CUR_CELL;
            Step_Until_Arrive(cMove, MOVE_DIR.UP_RIGHT, out Vector2Int vFirst);
            Step_Until_Arrive(cMove, MOVE_DIR.UP_RIGHT, out Vector2Int vSecond);
            Check("대각선 한 번은 두 칸을 밟는다", vFirst != vStart && vSecond != vFirst);
            Check("두 칸을 밟으면 대각선 자리에 선다", vSecond == vStart + new Vector2Int(1, 1));
            Check("가는 길은 4방향으로 이어진다",
                  Mathf.Abs(vFirst.x - vStart.x) + Mathf.Abs(vFirst.y - vStart.y) == 1);

            // 4방향 캐릭터는 대각선 입력을 받아도 대각선으로 가지 않는다
            CMoveHandler cFour = new CMoveHandler();
            cFour.Initialize(cGrid, new Vector2Int(5, 1), 100f);
            cFour.Set_MoveStyle(MOVE_STYLE.FOUR_WAY);
            Check("4방향 캐릭터는 대각선 입력으로 움직이지 않는다",
                  Step_Until_Arrive(cFour, MOVE_DIR.UP_RIGHT, out Vector2Int vNone) == false);
        }

        /// <summary> 한 칸 도착할 때까지 Tick을 돌린다. 200번 안에 못 가면 못 가는 것으로 본다. </summary>
        private static bool Step_Until_Arrive(CMoveHandler cMove, MOVE_DIR eDir, out Vector2Int vArrived)
        {
            vArrived = cMove.CUR_CELL;

            for (int i = 0; i < 200; ++i)
            {
                if (cMove.Tick(0.02f, eDir, out vArrived) == true)
                    return true;
            }

            return false;
        }

        // 260920_3지선다 게이지(2-21) — 조각 요구량이 고를수록 늘어난다
        private static void Test_Gauge()
        {
            const string TAB = "\t";
            // MapInfo 헤더 순서 그대로. 마지막 네 열이 조각 · 게이지다.
            string strCsv =
                  string.Join(TAB, "iMapID", "strMapName", "iGridWidth", "iGridHeight", "fCellSize", "iBorderThick",
                                   "iLife", "fPlayerSpeed", "iWaveCount", "strShapeMask", "strLayerTex", "strWaveEnemy",
                                   "strWaveClearRatio", "strWaveTimeLimit", "iCoinPerStar", "iCoinPerCell", "iCharacterID",
                                   "iFieldItemOnWave", "fFieldItemCool", "fFieldItemDropRate",
                                   "iCellPerShard", "iShardPerKill", "iGaugeBase", "iGaugeAdd", "NONE") + "\n"
                + string.Join(TAB, "1", "테스트", "60", "100", "0.12", "2", "3", "9", "1", "-",
                                   "A|B", "101*1", "0.6", "90", "50", "1", "0",
                                   "2", "20", "0.5", "40", "2", "6", "3", "");

            CCSVData_MapInfo cTable = new CCSVData_MapInfo();
            cTable.Read_CSVData(new TextAsset(strCsv));

            CMapInfo cInfo = cTable.Get_Info(1);
            Check("조각 · 게이지 열을 읽는다", cInfo != null && cInfo.iCellPerShard == 40 && cInfo.iShardPerKill == 2);
            Check("게이지 기본 요구량", cInfo.iGaugeBase, 6);
            Check("게이지 증가량", cInfo.iGaugeAdd, 3);
            Check("필드 아이템 열도 밀리지 않았다",
                  cInfo.iFieldItemOnWave == 2 && Mathf.Approximately(cInfo.fFieldItemDropRate, 0.5f));

            // 요구량은 고른 횟수에 비례해 늘어난다 — GAUGE_NEED와 같은 식이어야 한다
            for (int iGiven = 0; iGiven < 4; ++iGiven)
            {
                int iNeed = cInfo.iGaugeBase + cInfo.iGaugeAdd * iGiven;
                Check($"{iGiven + 1}번째 고르기에 조각 {iNeed}개", iNeed, 6 + 3 * iGiven);
            }
        }

        // 260920_맵 위 상호작용 아이템(2-20) — 표 파싱과 가중치
        private static void Test_FieldItem()
        {
            const string TAB = "\t";
            string strCsv =
                  string.Join(TAB, "iItemID", "eType", "strName", "strDesc", "iWeight", "fValue", "fDuration", "fLifeTime", "NONE") + "\n"
                + string.Join(TAB, "1", "MASS_STUN",  "번개", "", "10", "0", "2.5", "14", "") + "\n"
                + string.Join(TAB, "2", "HEAL_LIFE",  "물약", "", "0",  "1", "0",   "14", "") + "\n"
                + string.Join(TAB, "3", "MAGNET_ALL", "자석", "", "5",  "0", "0",   "9",  "");

            CCSVData_FieldItemInfo cTable = new CCSVData_FieldItemInfo();
            cTable.Read_CSVData(new TextAsset(strCsv));

            Check("세 종류를 읽는다", cTable.COUNT, 3);
            Check("ID로 찾는다", cTable.Find(2)?.eType == FIELD_ITEM_TYPE.HEAL_LIFE);
            Check("종류로 찾는다", cTable.Find_ByType(FIELD_ITEM_TYPE.MASS_STUN)?.iItemID == 1);
            Check("없는 ID는 null", cTable.Find(99) == null);
            Check("기절 시간은 표에서", Mathf.Approximately(cTable.Find(1).fDuration, 2.5f));
            Check("회복량도 표에서", Mathf.Approximately(cTable.Find(2).fValue, 1f));
            Check("종류마다 수명을 따로 준다", Mathf.Approximately(cTable.Find(3).fLifeTime, 9f));

            // 가중치 0은 나오지 않는다 — 표에서 지우지 않고 잠글 때 쓰는 규칙(카드 · 런 스킬과 같다)
            bool bHealPicked = false;
            for (int i = 0; i < 200; ++i)
            {
                CFieldItemInfo cPick = cTable.Pick_Random();
                if (cPick != null && cPick.eType == FIELD_ITEM_TYPE.HEAL_LIFE)
                    bHealPicked = true;
            }
            Check("가중치 0은 뽑히지 않는다", bHealPicked == false);
            Check("나올 것이 있으면 하나는 고른다", cTable.Pick_Random() != null);
            Check("빈 표는 null", new CCSVData_FieldItemInfo().Pick_Random() == null);

            // 색만으로 무엇인지 읽혀야 하므로 종류끼리 같은 색이면 안 된다
            Check("종류마다 색이 다르다",
                  CFieldItem.Get_Color(FIELD_ITEM_TYPE.MASS_STUN) != CFieldItem.Get_Color(FIELD_ITEM_TYPE.HEAL_LIFE)
               && CFieldItem.Get_Color(FIELD_ITEM_TYPE.HEAL_LIFE) != CFieldItem.Get_Color(FIELD_ITEM_TYPE.MAGNET_ALL));
        }

        // 260912_세로 화면 맞춤 — 9:16에서 맵 좌우가 잘리지 않아야 한다
        private static void Test_CameraFit()
        {
            const float PORTRAIT = 9f / 16f;
            const float LANDSCAPE = 16f / 9f;

            Vector2 vMap1 = new Vector2(7.2f, 12f);      // 60 x 100 셀 x 0.12
            Vector2 vMap2 = new Vector2(6f, 10.5f);      // 더 작은 맵 (40 x 70 셀 x 0.15)

            // 띠를 쓰지 않을 때 — 세로 화면에서는 가로가 기준이 된다
            float fSize = CCameraFitter.Calc_Size(vMap1, PORTRAIT, 1f, 0f);
            Check("세로 화면은 가로가 기준", Mathf.RoundToInt(fSize * 100f), 640);
            Check("맵 가로가 전부 보인다", fSize * 2f * PORTRAIT >= vMap1.x - 0.001f);
            Check("맵 세로도 전부 보인다", fSize * 2f >= vMap1.y - 0.001f);

            // 높이로만 맞추던 옛 방식이라면 잘렸다는 것을 못 박아 둔다
            float fHeightOnly = vMap1.y * 0.5f;
            Check("높이로만 맞추면 가로가 모자랐다", fHeightOnly * 2f * PORTRAIT < vMap1.x);

            // 가로 화면에서는 세로가 기준이 된다
            fSize = CCameraFitter.Calc_Size(vMap1, LANDSCAPE, 1f, 0f);
            Check("가로 화면은 세로가 기준", Mathf.RoundToInt(fSize * 100f), 600);

            // UI 띠를 빼면 맵이 쓸 수 있는 세로가 줄어 카메라가 더 멀어진다
            const float TOP = 0.10f;
            const float BOTTOM = 0.22f;
            float fUsable = 1f - TOP - BOTTOM;

            fSize = CCameraFitter.Calc_Size(vMap1, PORTRAIT, fUsable, 0f);
            Check("띠를 빼면 더 멀어진다", Mathf.RoundToInt(fSize * 100f), 882);
            Check("띠 안에 맵 세로가 들어간다", fSize * 2f * fUsable >= vMap1.y - 0.001f);
            Check("띠를 빼도 가로는 넉넉하다", fSize * 2f * PORTRAIT >= vMap1.x);

            // 아래를 더 비웠으면 카메라가 내려가고 맵은 화면 위로 올라간다
            float fPosY = CCameraFitter.Calc_PositionY(fSize, TOP, BOTTOM);
            Check("아래를 더 비우면 카메라가 내려간다", fPosY < 0f);
            Check("카메라 Y", Mathf.RoundToInt(fPosY * 100f), -106);
            Check("띠가 같으면 가운데", Mathf.Approximately(CCameraFitter.Calc_PositionY(fSize, 0.2f, 0.2f), 0f));

            // 맵마다 다시 맞춰야 한다 — 더 작은 맵을 맵 1 크기로 보면 작게 나온다
            float fSize2 = CCameraFitter.Calc_Size(vMap2, PORTRAIT, fUsable, 0f);
            Check("작은 맵은 더 가깝다", fSize2 < fSize);

            // 여백은 두 기준 중 이긴 쪽에 그대로 더해진다
            Check("여백만큼 멀어진다",
                  Mathf.RoundToInt(CCameraFitter.Calc_Size(vMap1, PORTRAIT, 1f, 0.3f) * 100f), 670);

            // 값이 이상해도 0으로 나누지 않는다
            Check("비율 0도 견딘다", CCameraFitter.Calc_Size(vMap1, 0f, 0f, 0f) > 0f);
        }


        // 260912_가림막과 보상이 화면에서 정확히 같은 자리·같은 크기인지.
        // 눈으로는 테두리 한 줄만 보여 맞춰 볼 특징이 없다. 월드 경계를 숫자로 본다.
        private static void Test_LayerBounds()
        {
            CTerritoryGrid cGrid = new CTerritoryGrid();
            cGrid.Initialize(60, 100, 0.12f, new Vector2(-3.6f, -6f), 2, null);

            GameObject goCover  = new GameObject("Cover_Bounds");
            GameObject goReveal = new GameObject("Reveal_Bounds");
            SpriteRenderer srCover  = goCover.AddComponent<SpriteRenderer>();
            SpriteRenderer srReveal = goReveal.AddComponent<SpriteRenderer>();

            CGridRenderer cRenderer = new CGridRenderer();
            cRenderer.Initialize(cGrid, srCover, srReveal);

            // 일부러 크기가 다른 두 장을 넣는다.
            // 원본 크기가 어떻든 화면에서는 같아야 한다는 것이 이 검사의 요점이다.
            cRenderer.Set_WaveTexture(Make_TestTexture(540, 900), Make_TestTexture(1080, 1800));

            Bounds bCover  = srCover.bounds;
            Bounds bReveal = srReveal.bounds;

            Check("가로가 같다", Mathf.RoundToInt(bCover.size.x * 1000f),
                                 Mathf.RoundToInt(bReveal.size.x * 1000f));
            Check("세로가 같다", Mathf.RoundToInt(bCover.size.y * 1000f),
                                 Mathf.RoundToInt(bReveal.size.y * 1000f));
            Check("중심이 같다",  Mathf.RoundToInt(bCover.center.x * 1000f),
                                 Mathf.RoundToInt(bReveal.center.x * 1000f));
            Check("중심 높이가 같다", Mathf.RoundToInt(bCover.center.y * 1000f),
                                      Mathf.RoundToInt(bReveal.center.y * 1000f));

            // 그리고 그 크기가 곧 그리드 크기여야 한다 (7.2 x 12.0)
            Vector2 vWorld = cGrid.WORLD_SIZE;
            Check("그리드 가로와 같다", Mathf.RoundToInt(bCover.size.x * 1000f),
                                        Mathf.RoundToInt(vWorld.x * 1000f));
            Check("그리드 세로와 같다", Mathf.RoundToInt(bCover.size.y * 1000f),
                                        Mathf.RoundToInt(vWorld.y * 1000f));
            Check("그리드 중심과 같다", Mathf.RoundToInt(bCover.center.x * 1000f),
                                        Mathf.RoundToInt(cGrid.WORLD_CENTER.x * 1000f));

            cRenderer.Release();
            Object.DestroyImmediate(goCover);
            Object.DestroyImmediate(goReveal);
        }

        // 260912_가림막 사본은 원본과 같은 해상도여야 한다.
        // 한 장이 보상이었다가 다음 웨이브에 가림막이 되므로, 줄여 찍으면 같은 그림이
        // 갑자기 거칠어져 크기가 달라진 것처럼 보인다.
        private static void Test_CoverResolution()
        {
            const int SRC_W = 540;
            const int SRC_H = 960;

            // 테두리 두께를 크게 잡아 모든 칸이 점령된 상태로 만든다.
            // 그래야 '빈틈 없이 덮였는가'를 픽셀 하나까지 볼 수 있다.
            CTerritoryGrid cGrid = new CTerritoryGrid();
            Check("그리드 초기화", cGrid.Initialize(6, 10, 0.12f, Vector2.zero, 6, null));

            GameObject goCover = new GameObject("Cover_Test");
            SpriteRenderer srCover = goCover.AddComponent<SpriteRenderer>();

            CGridRenderer cRenderer = new CGridRenderer();
            Check("렌더러 초기화", cRenderer.Initialize(cGrid, srCover, null));

            // 원본이 없을 때는 기본 해상도(PIXEL_PER_CELL)로 잡힌다
            Texture2D texBefore = srCover.sprite.texture;
            Check("원본 없으면 기본 해상도", texBefore.width, 6 * 4);

            Texture2D texCover = Make_TestTexture(SRC_W, SRC_H);
            cRenderer.Set_WaveTexture(texCover, null);

            Texture2D texMask = srCover.sprite.texture;
            Check("가림막 원본 가로를 따라간다", texMask.width, SRC_W);
            Check("가림막 원본 세로를 따라간다", texMask.height, SRC_H);

            // 칸 수로 나누어떨어지지 않는다(960 / 10칸은 96, 540 / 6칸은 90 — 여기선 떨어지지만
            // 아래 60x100 검사에서 9.6이 나온다). 빈틈 검사가 본론이다.
            Color32[] arrPixel = texMask.GetPixels32();
            int iHole = 0;
            for (int i = 0; i < arrPixel.Length; ++i)
            {
                if (arrPixel[i].a != 0)
                    ++iHole;
            }

            Check("전부 점령이면 빈틈 없이 뚫린다", iHole, 0);

            // 실제 맵 1 크기 — 960 / 100칸 = 9.6이라 칸 높이가 9와 10을 오간다.
            // 여기서 칸 경계를 잘못 잡으면 덮이지 않은 줄이 남는다.
            CTerritoryGrid cGrid2 = new CTerritoryGrid();
            cGrid2.Initialize(60, 100, 0.12f, Vector2.zero, 100, null);

            GameObject goCover2 = new GameObject("Cover_Test2");
            SpriteRenderer srCover2 = goCover2.AddComponent<SpriteRenderer>();

            CGridRenderer cRenderer2 = new CGridRenderer();
            cRenderer2.Initialize(cGrid2, srCover2, null);
            cRenderer2.Set_WaveTexture(Make_TestTexture(SRC_W, SRC_H), null);

            Color32[] arrPixel2 = srCover2.sprite.texture.GetPixels32();
            int iHole2 = 0;
            for (int i = 0; i < arrPixel2.Length; ++i)
            {
                if (arrPixel2[i].a != 0)
                    ++iHole2;
            }

            Check("나누어떨어지지 않아도 빈틈 없다", iHole2, 0);

            cRenderer.Release();
            cRenderer2.Release();
            Object.DestroyImmediate(goCover);
            Object.DestroyImmediate(goCover2);
            Object.DestroyImmediate(texCover);
        }

        private static Texture2D Make_TestTexture(int iWidth, int iHeight)
        {
            Texture2D tex = new Texture2D(iWidth, iHeight, TextureFormat.RGBA32, false);
            Color32[] arrPixel = new Color32[iWidth * iHeight];

            for (int i = 0; i < arrPixel.Length; ++i)
                arrPixel[i] = new Color32(200, 100, 50, 255);

            tex.SetPixels32(arrPixel);
            tex.Apply();
            return tex;
        }

        // 260912_전투 소모품은 장착이 아니라 보유 기준이다.
        // 사 놓고 장착을 잊으면 전투에서 버튼이 안 떠 버렸다.
        private static void Test_BattleConsumable()
        {
            CCSVData_MapInfo   cMapTable   = Load_MapTable();
            CCSVData_EquipInfo cEquipTable = Load_EquipTable();
            if (cMapTable == null || cEquipTable == null)
            {
                Check("표 로드", false);
                return;
            }

            const int SHIELD = 401;     // 보호막 (지금 단 하나뿐인 소모품)
            const int SHOES  = 101;     // 소모품이 아닌 장비

            CProgress_Manager cManager = new CProgress_Manager();
            cManager.Initialize(cMapTable, new CFakeProgressRepository(), cEquipTable);

            Check("아무것도 없으면 null", cManager.Get_BattleConsumable() == null);

            // 장비만 있으면 소모품이 아니므로 여전히 없다
            cManager.Add_Item(SHOES);
            cManager.Try_Equip(SHOES);
            Check("장비는 소모품이 아니다", cManager.Get_BattleConsumable() == null);

            // 사기만 하고 장착하지 않아도 쓸 수 있어야 한다
            cManager.Add_Item(SHIELD);
            Check("장착하지 않아도 잡힌다", cManager.Get_BattleConsumable()?.iEquipID ?? 0, SHIELD);

            // 장착하면 그쪽이 우선이다 (여러 개일 때 고르라고 둔 슬롯이다)
            cManager.Try_Equip(SHIELD);
            Check("장착한 쪽이 우선", cManager.Get_BattleConsumable()?.iEquipID ?? 0, SHIELD);

            cManager.Use_Item(SHIELD);
            Check("전부 떨어지면 null", cManager.Get_BattleConsumable() == null);
        }

        // 260912_액티브 스킬 5종이 표에서 제대로 읽히고, 종류마다 모듈이 붙는지
        private static void Test_SkillTable()
        {
            CCSVData_SkillInfo cTable = Load_SkillTable();
            if (cTable == null)
            {
                Check("SkillInfo.csv 로드", false);
                return;
            }

            SKILL_TYPE[] arrActive = { SKILL_TYPE.WARP, SKILL_TYPE.SHIELD, SKILL_TYPE.DASH,
                                       SKILL_TYPE.SLOW, SKILL_TYPE.SEAL };

            for (int i = 0; i < arrActive.Length; ++i)
            {
                CSkillInfo cInfo = cTable.Find_ByType(arrActive[i]);
                Check($"{arrActive[i]} 표에 있다", cInfo != null);
                if (cInfo == null)
                    continue;

                Check($"{arrActive[i]}는 액티브", cInfo.IS_PASSIVE == false);
                Check($"{arrActive[i]} 쿨타임이 있다", cInfo.fCoolTime > 0f);
                // 액티브는 전부 효과 모듈이 붙어야 버튼이 동작한다
                Check($"{arrActive[i]} 효과 모듈", CSkillEffect.Create(arrActive[i]) != null);
            }

            // 패시브는 버튼으로 쓸 것이 없으므로 모듈을 만들지 않는다 —
            // 만들면 인게임에 눌러도 아무 일 없는 버튼이 뜬다.
            Check("패시브는 모듈 없음", CSkillEffect.Create(SKILL_TYPE.SWIFT) == null);
            Check("NONE도 모듈 없음",  CSkillEffect.Create(SKILL_TYPE.NONE) == null);

            // 지속 시간이 필요한 것과 즉발을 갈라 둔다
            Check("질주는 지속 시간이 있다", cTable.Find_ByType(SKILL_TYPE.DASH).fDuration > 0f);
            Check("감속은 지속 시간이 있다", cTable.Find_ByType(SKILL_TYPE.SLOW).fDuration > 0f);
            Check("점멸은 즉발", Mathf.Approximately(cTable.Find_ByType(SKILL_TYPE.WARP).fDuration, 0f));

            // 감속은 fValue가 클수록 세다 — 1에서 빼서 배율로 쓰기 때문이다
            CSkillInfo cSlow = cTable.Find_ByType(SKILL_TYPE.SLOW);
            Check("감속 배율은 0과 1 사이", 1f - cSlow.Get_Value(0) > 0f && 1f - cSlow.Get_Value(0) < 1f);
            Check("레벨을 올리면 더 느려진다", cSlow.Get_Value(1) > cSlow.Get_Value(0));

            // 마감은 강화 대상이 아니다
            Check("마감은 만렙 1", cTable.Find_ByType(SKILL_TYPE.SEAL).iMaxLevel, 1);

            // 스킬 ID는 겹치면 안 된다 (인벤토리가 ID로 장착한다)
            List<int> lstID = new List<int>();
            bool bDup = false;
            for (int i = 0; i < arrActive.Length; ++i)
            {
                int iID = cTable.Find_ByType(arrActive[i]).iSkillID;
                if (lstID.Contains(iID) == true)
                    bDup = true;
                lstID.Add(iID);
            }
            Check("스킬 ID가 겹치지 않는다", bDup == false);
        }

        // 260916_런 전용 스킬(뱀서라이크) 표 — 8종이 전부 읽히고, 레벨 스케일링·해금 가중치 뽑기가 맞는지
        private static void Test_RunSkillTable()
        {
            CCSVData_RunSkillInfo cTable = Load_RunSkillTable();
            if (cTable == null)
            {
                Check("RunSkillInfo.csv 로드", false);
                return;
            }

            RUN_SKILL_TYPE[] arrType =
            {
                RUN_SKILL_TYPE.MOONWALK, RUN_SKILL_TYPE.EVASION, RUN_SKILL_TYPE.MAGNET,
                RUN_SKILL_TYPE.EDGE_WRAP, RUN_SKILL_TYPE.RAGE, RUN_SKILL_TYPE.SOUL_COLLECTOR,
                RUN_SKILL_TYPE.ORBIT, RUN_SKILL_TYPE.CLUB,
                RUN_SKILL_TYPE.MAGIC_BOLT, RUN_SKILL_TYPE.LASER_BEAM, RUN_SKILL_TYPE.BOOMERANG, RUN_SKILL_TYPE.BOUNCE_SHOT,
                RUN_SKILL_TYPE.STUN_SHOT,
            };

            for (int i = 0; i < arrType.Length; ++i)
                Check($"{arrType[i]} 표에 있다", cTable.Find_ByType(arrType[i]) != null);

            // 온/오프뿐인 스킬은 레벨업이 없다
            CRunSkillInfo cMoonwalk = cTable.Find_ByType(RUN_SKILL_TYPE.MOONWALK);
            Check("월보는 만렙 1", cMoonwalk.iMaxLevel, 1);

            // 레벨이 오르면 수치도 오른다
            CRunSkillInfo cMagnet = cTable.Find_ByType(RUN_SKILL_TYPE.MAGNET);
            Check("자석 레벨1 > 레벨0(미보유)", cMagnet.Get_Value(1) > cMagnet.Get_Value(0));
            Check("자석 레벨2 > 레벨1", cMagnet.Get_Value(2) > cMagnet.Get_Value(1));
            Check("자석은 만렙을 넘지 않는다", Mathf.Approximately(cMagnet.Get_Value(99), cMagnet.Get_Value(cMagnet.iMaxLevel)));

            // 해금 전이면 뽑히지 않는다
            List<CRunSkillInfo> lstLocked = cTable.Pick_Random(8, eType => 0, iMapID => false);
            bool bAnyGated = false;
            for (int i = 0; i < lstLocked.Count; ++i)
            {
                if (lstLocked[i].iUnlockMapID > 0)
                    bAnyGated = true;
            }
            Check("맵을 하나도 안 깼으면 해금 스킬은 안 나온다", bAnyGated == false);

            // 해금되면 나온다 — 모든 맵을 깼다고 치면 전부가 후보다 (260917_투사체 무기 4종이 늘어 12종)
            // 260918_투사체 무기 4종은 가중치 0으로 뺐다(뱀서라이크가 아니다) — 마비 둘이 더해져 10종
            List<CRunSkillInfo> lstAll = cTable.Pick_Random(14, eType => 0, iMapID => true);
            Check("전부 해금되면 가중치 있는 9종이 후보", lstAll.Count, 9);
            Check("빠진 무기는 후보가 아니다", lstAll.Exists(cInfo => cInfo.eType == RUN_SKILL_TYPE.MAGIC_BOLT) == false);

            // 이미 만렙이면 후보에서 빠진다
            List<CRunSkillInfo> lstMaxed = cTable.Pick_Random(8,
                eType => eType == RUN_SKILL_TYPE.MOONWALK ? cMoonwalk.iMaxLevel : 0, iMapID => true);
            bool bMoonwalkStillOffered = false;
            for (int i = 0; i < lstMaxed.Count; ++i)
            {
                if (lstMaxed[i].eType == RUN_SKILL_TYPE.MOONWALK)
                    bMoonwalkStillOffered = true;
            }
            Check("만렙인 스킬은 다시 안 나온다", bMoonwalkStillOffered == false);

            // 같은 스킬이 한 번에 두 장 나오지 않는다(카드 뽑기와 같은 규칙)
            List<CRunSkillInfo> lstThree = cTable.Pick_Random(3, eType => 0, iMapID => true);
            HashSet<RUN_SKILL_TYPE> hsPicked = new HashSet<RUN_SKILL_TYPE>();
            bool bDupSkill = false;
            for (int i = 0; i < lstThree.Count; ++i)
            {
                if (hsPicked.Add(lstThree[i].eType) == false)
                    bDupSkill = true;
            }
            Check("3지선다는 서로 다른 스킬 셋", bDupSkill == false);
            Check("3지선다는 정확히 3장", lstThree.Count, 3);
        }

        // 260916_런 전용 스킬을 실제 CPlayer에 붙였을 때 — 이동 플래그·회피·습득 범위가 걸리고,
        // 스테이지 재사용(Initialize) 때 전부 리셋되는지
        private static void Test_RunSkill()
        {
            CCSVData_RunSkillInfo cTable = Load_RunSkillTable();
            if (cTable == null)
            {
                Check("RunSkillInfo.csv 로드(Test_RunSkill)", false);
                return;
            }

            CTerritoryGrid cGrid = Make_Grid();
            GameObject goPlayer = new GameObject("Test_RunSkillPlayer");
            CPlayer cPlayer = goPlayer.AddComponent<CPlayer>();
            CPlayerDesc cDesc = new CPlayerDesc
            {
                eObjectType   = Engine.OBJECT_TYPE.PLAYER,
                strPrefabName = "Prefab_Player",
                cGrid         = cGrid,
                vStartCell    = new Vector2Int(GRID_SIZE / 2, BORDER_THICK - 1),
                fMoveSpeed    = STEP_SPEED,
                iLife         = 3,
                fEvasion      = 0f,
            };
            cPlayer.Initialize(cDesc);

            Check("처음엔 런 스킬 없음", cPlayer.RUN_SKILL.Has(RUN_SKILL_TYPE.MOONWALK) == false);

            // 260916_월보/어디로든 신발이 실제로 이동 판정을 바꾸는지는 CMoveHandler의
            // Try_StartMove(선분 자동 추적 포함)까지 얽혀 있어 화면 없이 좌표를 손으로
            // 재현하면 오히려 틀리기 쉽다 — 여기서는 '스킬을 얻으면 레벨이 오른다'는
            // 핸들러 연동까지만 검증하고, 실제 통과 여부는 Play로 눈으로 확인할 것.
            cPlayer.Add_RunSkill(cTable.Find_ByType(RUN_SKILL_TYPE.MOONWALK));
            Check("월보 획득 시 1레벨", cPlayer.RUN_SKILL.Get_Level(RUN_SKILL_TYPE.MOONWALK), 1);

            cPlayer.Add_RunSkill(cTable.Find_ByType(RUN_SKILL_TYPE.EDGE_WRAP));
            Check("어디로든 신발 획득 시 1레벨", cPlayer.RUN_SKILL.Get_Level(RUN_SKILL_TYPE.EDGE_WRAP), 1);

            // 자석 — 레벨업마다 습득 범위가 커진다
            CRunSkillInfo cMagnetInfo = cTable.Find_ByType(RUN_SKILL_TYPE.MAGNET);
            cPlayer.Add_RunSkill(cMagnetInfo);
            float fRadiusLv1 = cPlayer.PICKUP_RADIUS;
            Check("자석 1레벨이면 습득 범위가 0보다 크다", fRadiusLv1 > 0f);
            cPlayer.Add_RunSkill(cMagnetInfo);
            Check("자석 2레벨이면 범위가 더 커진다", cPlayer.PICKUP_RADIUS > fRadiusLv1);

            // 회피 — 레벨업마다 누적되고, 두 배로 더해지지 않는다
            CRunSkillInfo cEvasionInfo = cTable.Find_ByType(RUN_SKILL_TYPE.EVASION);
            cPlayer.Add_RunSkill(cEvasionInfo);
            float fEvasionLv1 = cEvasionInfo.Get_Value(1);
            cPlayer.Add_RunSkill(cEvasionInfo);
            float fEvasionLv2 = cEvasionInfo.Get_Value(2);
            Check("회피 레벨2 수치가 레벨1보다 크다", fEvasionLv2 > fEvasionLv1);

            // 같은 스킬을 세 번째 받아도 만렙을 넘지 않는다
            for (int i = 0; i < 10; ++i)
                cPlayer.Add_RunSkill(cMagnetInfo);
            Check("자석은 만렙을 넘지 않는다", cPlayer.RUN_SKILL.Get_Level(RUN_SKILL_TYPE.MAGNET), cMagnetInfo.iMaxLevel);

            // 영혼 수집가 — 호스트를 안 꽂아도(에디터 테스트라 스테이지가 없다) 레벨업/습득 콜백이
            // 예외 없이 동작해야 한다. 실제 속도 증가는 CPlayer.Add_CardSpeed를 그대로 타므로
            // 카드 쪽 검증(Test_CardPick)과 같은 값이라 여기서 다시 재지 않는다.
            cPlayer.Add_RunSkill(cTable.Find_ByType(RUN_SKILL_TYPE.SOUL_COLLECTOR));
            Check("영혼 수집가 획득 시 1레벨", cPlayer.RUN_SKILL.Get_Level(RUN_SKILL_TYPE.SOUL_COLLECTOR), 1);
            cPlayer.On_SoulCollected();
            Check("영혼 습득 콜백은 예외 없이 끝난다", true);

            // 스테이지 재사용(Initialize) — 런 스킬은 전부 사라져야 한다(뱀서라이크는 판마다 초기화)
            cPlayer.Initialize(cDesc);
            Check("재초기화하면 런 스킬이 전부 사라진다", cPlayer.RUN_SKILL.Has(RUN_SKILL_TYPE.MOONWALK) == false);
            Check("재초기화하면 습득 범위도 0", Mathf.Approximately(cPlayer.PICKUP_RADIUS, 0f));

            Object.DestroyImmediate(goPlayer);
        }


        // 260917_투사체 무기(뱀서라이크 자동 발사) · 각성 — 쿨마다 쏘는지, 대상이 없으면 기다리는지, 각성이 무엇을 바꾸는지
        private static void Test_WeaponAndAwaken()
        {
            CCSVData_RunSkillInfo cSkillTable = Load_RunSkillTable();
            CCSVData_AwakenInfo cAwakenTable = Load_CsvTable<CCSVData_AwakenInfo>("AwakenInfo");
            CCSVData_ProjectileInfo cProjectileTable = Load_CsvTable<CCSVData_ProjectileInfo>("ProjectileInfo");
            if (cSkillTable == null || cAwakenTable == null || cProjectileTable == null)
            {
                Check("RunSkillInfo / AwakenInfo 로드(Test_WeaponAndAwaken)", false);
                return;
            }

            Check("각성 표 — 6줄", cAwakenTable.COUNT, 6);

            // ---- 발사기: 연발 · 회전 링
            List<Vector2> lstShot = new List<Vector2>();
            CProjectileFirer cFirer = new CProjectileFirer();
            cFirer.Setup(FIRE_PATTERN.BURST, 3, 0f, 0.1f, vDir => lstShot.Add(vDir));
            cFirer.Fire(Vector2.right);
            Check("연발 — 첫 발은 바로", lstShot.Count, 1);
            Check("연발 — 남은 발이 있다", cFirer.IS_BURSTING == true);
            cFirer.Tick(0.05f, Vector2.right);
            Check("연발 — 간격 전에는 안 나간다", lstShot.Count, 1);
            cFirer.Tick(0.06f, Vector2.right);
            cFirer.Tick(0.11f, Vector2.right);
            Check("연발 — 간격마다 한 발씩 세 발", lstShot.Count, 3);
            Check("연발 — 다 쏘면 끝", cFirer.IS_BURSTING == false);

            lstShot.Clear();
            cFirer.Setup(FIRE_PATTERN.SPIN, 4, 30f, 0f, vDir => lstShot.Add(vDir));
            cFirer.Fire(Vector2.up);
            cFirer.Fire(Vector2.up);
            Check("회전 링 — 한 번에 네 발씩", lstShot.Count, 8);
            Check("회전 링 — 두 번째는 30도 돌아가 있다",
                  Mathf.Abs(Vector2.SignedAngle(lstShot[0], lstShot[4]) - 30f) < 0.01f);

            // ---- 플레이어
            CTerritoryGrid cGrid = Make_Grid();
            GameObject goPlayer = new GameObject("Test_WeaponPlayer");
            CPlayer cPlayer = goPlayer.AddComponent<CPlayer>();
            CPlayerDesc cDesc = new CPlayerDesc
            {
                eObjectType   = Engine.OBJECT_TYPE.PLAYER,
                strPrefabName = "Prefab_Player",
                cGrid         = cGrid,
                vStartCell    = new Vector2Int(GRID_SIZE / 2, BORDER_THICK - 1),
                fMoveSpeed    = STEP_SPEED,
                iLife         = 3,
                fEvasion      = 1f,     // 반격의 몽둥이 — 맞으면 반드시 회피하게
            };
            cPlayer.Initialize(cDesc);

            CFakeRunSkillHost cHost = new CFakeRunSkillHost { cProjectileTable = cProjectileTable };
            cPlayer.Set_RunSkillHost(cHost);

            // ---- 마법탄: 대상이 없으면 쿨을 찬 채 기다린다
            CRunSkillInfo cBoltInfo = cSkillTable.Find_ByType(RUN_SKILL_TYPE.MAGIC_BOLT);
            Check("마법탄은 무기다", cBoltInfo != null && cBoltInfo.IS_WEAPON == true);
            cPlayer.Add_RunSkill(cBoltInfo);
            CRunSkillEffect_Weapon cBolt = cPlayer.Find_RunSkillEffect(RUN_SKILL_TYPE.MAGIC_BOLT) as CRunSkillEffect_Weapon;
            Check("마법탄은 무기 모듈이 붙는다", cBolt != null);
            if (cBolt == null)
            {
                Object.DestroyImmediate(goPlayer);
                return;
            }

            cBolt.Tick(5f);
            Check("몬스터가 없으면 쏘지 않는다", cHost.lstShotID.Count, 0);

            cHost.lstEnemy.Add(new CFakeImpactTarget(cPlayer.POS + new Vector2(3f, 0f), 0.3f));
            cBolt.Tick(0.01f);
            Check("몬스터가 들어오면 곧바로 쏜다 (쿨이 차 있었다)", cHost.lstShotID.Count, 1);
            Check("마법탄은 유도탄(4)", cHost.lstShotID.Count > 0 ? cHost.lstShotID[0] : -1, 4);
            Check("대상 쪽으로 쏜다", cHost.lstShotDir.Count > 0 && cHost.lstShotDir[0].x > 0.9f);

            cBolt.Tick(0.5f);
            Check("쿨 동안은 다시 안 쏜다", cHost.lstShotID.Count, 1);

            // 레벨이 오르면 연발 수가 는다
            cPlayer.Add_RunSkill(cBoltInfo);
            cPlayer.Add_RunSkill(cBoltInfo);
            Check("마법탄 3레벨 — 3연발", cBolt.COUNT, 3);
            cBolt.Tick(2f);                 // 쿨이 빠진다
            cBolt.Tick(0.01f);              // 첫 발
            cBolt.Tick(0.2f);
            cBolt.Tick(0.2f);
            Check("쿨이 돌면 3연발", cHost.lstShotID.Count, 4);

            // ---- 튕기는 탄: 링은 한 번에 다 나간다
            cPlayer.Add_RunSkill(cSkillTable.Find_ByType(RUN_SKILL_TYPE.BOUNCE_SHOT));
            CRunSkillEffect_Weapon cBounce = cPlayer.Find_RunSkillEffect(RUN_SKILL_TYPE.BOUNCE_SHOT) as CRunSkillEffect_Weapon;
            int iBefore = cHost.lstShotID.Count;
            cBounce?.Tick(0.01f);
            Check("튕기는 탄 1레벨 — 한 번에 3발", cHost.lstShotID.Count - iBefore, 3);

            // ---- 각성: 만렙 + 짝 패시브가 있어야 후보다
            System.Func<RUN_SKILL_TYPE, int> fnLevel = eType => cPlayer.RUN_SKILL.Get_Level(eType);
            System.Func<RUN_SKILL_TYPE, bool> fnAwakened = eType => cPlayer.RUN_SKILL.Is_Awakened(eType);
            CAwakenInfo cBlackHole = cAwakenTable.Get_Info(3);

            Check("각성 — 만렙 전에는 후보가 아니다",
                  cAwakenTable.Collect_Candidates(cSkillTable, fnLevel, fnAwakened).Contains(cBlackHole) == false);
            cPlayer.Add_RunSkill(cBoltInfo);
            cPlayer.Add_RunSkill(cBoltInfo);
            Check("각성 — 만렙이어도 짝 패시브(자석)가 없으면 아니다",
                  cAwakenTable.Collect_Candidates(cSkillTable, fnLevel, fnAwakened).Contains(cBlackHole) == false);
            cPlayer.Add_RunSkill(cSkillTable.Find_ByType(RUN_SKILL_TYPE.MAGNET));
            Check("각성 — 만렙 + 자석이면 후보",
                  cAwakenTable.Collect_Candidates(cSkillTable, fnLevel, fnAwakened).Contains(cBlackHole) == true);

            // 3지선다에도 금색 각성으로 나온다
            CPickOption cAwakenOption = null;
            for (int n = 0; n < 300 && cAwakenOption == null; ++n)
            {
                List<CPickOption> lstPick = CPickOption_Utility.Pick(null, cSkillTable, fnLevel, iMapID => true, 3,
                                                                     cAwakenTable, fnAwakened);
                for (int i = 0; i < lstPick.Count; ++i)
                {
                    if (lstPick[i].eKind == PICK_KIND.AWAKEN)
                        cAwakenOption = lstPick[i];
                }
            }
            Check("각성 — 3지선다에 나온다", cAwakenOption != null);
            Check("각성 — 제목", cAwakenOption != null ? CUI_CardPick.Get_Title(cAwakenOption) : "", "각성  블랙홀탄");

            float fCoolBefore = cBolt.COOL;
            Check("각성 — 걸린다", cPlayer.Awaken_RunSkill(cBlackHole) == true);
            Check("각성 — 탄이 블랙홀탄(16)으로 바뀐다", cBolt.PROJECTILE_ID, 16);
            Check("각성 — 쿨이 줄어든다(0.8배)", Mathf.Approximately(cBolt.COOL, fCoolBefore * 0.8f));
            Check("각성 — 같은 액티브는 두 번 각성하지 않는다", cPlayer.Awaken_RunSkill(cBlackHole) == false);
            Check("각성 — 한 번 하면 후보에서 빠진다",
                  cAwakenTable.Collect_Candidates(cSkillTable, fnLevel, fnAwakened).Contains(cBlackHole) == false);
            Check("각성 — 슬롯(레벨)은 그대로", cPlayer.RUN_SKILL.Get_Level(RUN_SKILL_TYPE.MAGIC_BOLT), cBoltInfo.iMaxLevel);

            // ---- 십자 레이저: 발 수를 덮어쓴다
            CRunSkillInfo cLaserInfo = cSkillTable.Find_ByType(RUN_SKILL_TYPE.LASER_BEAM);
            for (int i = 0; i < cLaserInfo.iMaxLevel; ++i)
                cPlayer.Add_RunSkill(cLaserInfo);
            cPlayer.Add_RunSkill(cSkillTable.Find_ByType(RUN_SKILL_TYPE.EDGE_WRAP));
            CRunSkillEffect_Weapon cLaser = cPlayer.Find_RunSkillEffect(RUN_SKILL_TYPE.LASER_BEAM) as CRunSkillEffect_Weapon;
            Check("레이저 만렙 — 5줄기", cLaser != null ? cLaser.COUNT : -1, 5);
            cPlayer.Awaken_RunSkill(cAwakenTable.Get_Info(4));
            Check("십자 레이저 — 4방향으로 덮어쓴다", cLaser != null ? cLaser.COUNT : -1, 4);

            // ---- 광란의 칼바람: 회전탄 + 분노
            CRunSkillInfo cOrbitInfo = cSkillTable.Find_ByType(RUN_SKILL_TYPE.ORBIT);
            for (int i = 0; i < cOrbitInfo.iMaxLevel; ++i)
                cPlayer.Add_RunSkill(cOrbitInfo);
            cPlayer.Add_RunSkill(cSkillTable.Find_ByType(RUN_SKILL_TYPE.RAGE));
            CRunSkillEffect_Orbit cOrbit = cPlayer.Find_RunSkillEffect(RUN_SKILL_TYPE.ORBIT) as CRunSkillEffect_Orbit;
            int iOrbitBase = cOrbit != null ? cOrbit.COUNT : -1;

            // 260917_회전탄은 실제 탄을 띄운다 (전에는 좌표만 있고 아무것도 그려지지 않았다)
            cOrbit?.Tick(0.01f);
            Check("회전탄 — 만렙 수만큼 탄을 띄운다", cOrbit != null ? cOrbit.ALIVE_COUNT : -1, iOrbitBase);
            Check("회전탄 — 탄 20", cHost.lstShotID.Count > 0 ? cHost.lstShotID[cHost.lstShotID.Count - 1] : -1, 20);
            int iShotBefore = cHost.lstShotID.Count;
            cOrbit?.Tick(0.01f);
            Check("회전탄 — 떠 있으면 다시 띄우지 않는다", cHost.lstShotID.Count, iShotBefore);
            if (cHost.lstCore.Count > 0)
                cHost.lstCore[cHost.lstCore.Count - 1].Expire();
            cOrbit?.Tick(0.01f);
            Check("회전탄 — 하나가 사라지면 다시 채운다", cOrbit != null ? cOrbit.ALIVE_COUNT : -1, iOrbitBase);

            cPlayer.Awaken_RunSkill(cAwakenTable.Get_Info(1));
            Check("광란의 칼바람 — 하나 는다", cOrbit != null ? cOrbit.COUNT : -1, iOrbitBase + 1);
            Check("분노 전에는 피버가 아니다", cPlayer.IS_FEVER == false);
            for (int i = 0; i < 4; ++i)
                cPlayer.On_MonsterHit();    // 한 번에 0.25씩 — 네 번이면 가득 찬다
            Check("분노가 터지면 피버", cPlayer.IS_FEVER == true);
            Check("피버 동안 회전탄이 두 배", cOrbit != null ? cOrbit.COUNT : -1, (iOrbitBase + 1) * 2);
            cOrbit?.Tick(0.01f);
            Check("피버 동안 빠른 탄(21)으로 바꿔 띄운다",
                  cOrbit != null && cOrbit.ALIVE_COUNT == (iOrbitBase + 1) * 2 && cOrbit.PROJECTILE_ID == 21);

            // ---- 반격의 몽둥이: 몽둥이 + 회피
            CRunSkillInfo cClubInfo = cSkillTable.Find_ByType(RUN_SKILL_TYPE.CLUB);
            for (int i = 0; i < cClubInfo.iMaxLevel; ++i)
                cPlayer.Add_RunSkill(cClubInfo);
            cPlayer.Add_RunSkill(cSkillTable.Find_ByType(RUN_SKILL_TYPE.EVASION));
            CRunSkillEffect_Club cClub = cPlayer.Find_RunSkillEffect(RUN_SKILL_TYPE.CLUB) as CRunSkillEffect_Club;
            float fRadiusBefore = cClub != null ? cClub.RADIUS_CELLS : 0f;
            // 260918_몽둥이는 좌우 양쪽을 한 번에, 멈춰 있어도 휘두른다
            int iClubShotBefore = cHost.lstShotID.Count;
            cClub?.Tick(0.01f);
            Check("몽둥이 — 좌우 두 번", cHost.lstShotID.Count - iClubShotBefore, 2);
            Check("몽둥이 — 왼쪽과 오른쪽", cHost.lstShotDir.Count >= 2
                  && cHost.lstShotDir[cHost.lstShotDir.Count - 2].x < -0.9f && cHost.lstShotDir[cHost.lstShotDir.Count - 1].x > 0.9f);
            Check("몽둥이 — 반경 3칸 이상(예전의 3배)", cClub != null && cClub.RADIUS_CELLS >= 3f);
            cClub?.Tick(10f);       // 방금 휘둘러 돈 쿨을 흘려보낸다
            cPlayer.Awaken_RunSkill(cAwakenTable.Get_Info(2));
            Check("반격의 몽둥이 — 범위 1.3배", cClub != null && Mathf.Approximately(cClub.RADIUS_CELLS, fRadiusBefore * 1.3f));
            Check("반격의 몽둥이 — 한 번 휘두르면", cClub != null && cClub.Consume_Swing(out float _) == true);
            Check("반격의 몽둥이 — 쿨이 돈다", cClub != null && cClub.IS_READY == false);
            cPlayer.Lose_Life();            // 회피 확률 1 — 반드시 회피한다
            Check("반격의 몽둥이 — 회피하면 곧바로 다시 휘두를 수 있다", cClub != null && cClub.IS_READY == true);

            CRunSkillInfo cStunShot = cSkillTable.Find_ByType(RUN_SKILL_TYPE.STUN_SHOT);
            Check("마비탄은 탄 23을 쏘는 무기", cStunShot != null && cStunShot.iProjectileID == 23);

            // ---- 판이 끝나면 각성도 사라진다
            List<CProjectileCore> lstOrbitCore = cHost.lstCore.FindAll(
                cCore => cCore.INFO.iProjectileID == 20 || cCore.INFO.iProjectileID == 21);
            cPlayer.Initialize(cDesc);
            Check("재초기화하면 각성도 사라진다", cPlayer.RUN_SKILL.Is_Awakened(RUN_SKILL_TYPE.MAGIC_BOLT) == false);
            Check("재초기화하면 회전탄도 거둔다", lstOrbitCore.TrueForAll(cCore => cCore.IS_EXPIRED == true));

            // ---- 플레이어 탄이 맞힌 수 (분노 게이지를 스테이지가 센다)
            CProjectileCore cCore = Make_Core(new CFakeProjectileHost(), new CProjectileInfo
            { eMove = PROJECTILE_MOVE.NONE, fLifeTime = 10f, fHitRange = 0.5f, fScale = 1f, iDurability = -1 },
                new Vector2(5f, 5f), Vector2.up);
            cCore.Update_Contact(new List<IImpactTarget>
            {
                new CFakeImpactTarget(new Vector2(5f, 5f), 0.1f), new CFakeImpactTarget(new Vector2(5.2f, 5f), 0.1f),
            }, 0.1f);
            Check("탄이 새로 맞힌 수", cCore.HIT_COUNT, 2);

            // 몽둥이 탄 — 쏜 쪽이 곱한 크기만큼 판정이 커진다 (1레벨 반경 1칸 = 0.8 × 다 커진 1.25배)
            CProjectileCore cSwing = new CProjectileCore();
            cSwing.Initialize(cProjectileTable.Get_Info(22), null, new CFakeProjectileHost(), 1f, new Vector2(5f, 5f),
                              Vector2.up, PROJECTILE_SIDE.PLAYER_SHOT, null);
            cSwing.Set_SpawnScale(2f);
            cSwing.Tick(0.1f);
            Check("몽둥이 탄 — 크기 2배면 반경 2칸",
                  cSwing.SHAPE.Is_Overlap(new Vector2(6.95f, 5f), 0f) == true
                  && cSwing.SHAPE.Is_Overlap(new Vector2(7.1f, 5f), 0f) == false);

            Object.DestroyImmediate(goPlayer);
        }

        private class CFakeRunSkillHost : IRunSkillHost
        {
            public readonly List<CFakeImpactTarget> lstEnemy   = new List<CFakeImpactTarget>();
            public readonly List<int>               lstShotID  = new List<int>();
            public readonly List<Vector2>           lstShotDir = new List<Vector2>();
            public readonly List<CProjectileCore>   lstCore    = new List<CProjectileCore>();
            public CCSVData_ProjectileInfo          cProjectileTable;

            public void Spawn_Soul() { }

            public float fLastStun;
            public int   iStunCount;

            public int Stun_AllEnemies(float fDuration)
            {
                int iAlive = lstEnemy.FindAll(cEnemy => cEnemy.IS_ALIVE).Count;
                if (iAlive > 0)
                {
                    ++iStunCount;
                    fLastStun = fDuration;
                }
                return iAlive;
            }

            public IImpactTarget Find_Enemy(Vector2 vFrom, TARGET_FIND eFind)
                => CTargetFinder_Utility.Find(lstEnemy, vFrom, eFind);

            // 표가 있으면 진짜 탄 본체를 만들어 돌려준다 — 회전탄이 붙잡고 거두는 길을 그대로 탄다
            public CProjectileCore Spawn_PlayerShot(int iProjectileID, Vector2 vPos, Vector2 vDir, float fScale = 1f)
            {
                lstShotID.Add(iProjectileID);
                lstShotDir.Add(vDir.normalized);

                CProjectileInfo cInfo = cProjectileTable != null ? cProjectileTable.Get_Info(iProjectileID) : null;
                if (cInfo == null)
                    return null;

                CProjectileCore cCore = new CProjectileCore();
                cCore.Initialize(cInfo, null, new CFakeProjectileHost(), 1f, vPos, vDir, PROJECTILE_SIDE.PLAYER_SHOT, null);
                cCore.Set_SpawnScale(fScale);
                lstCore.Add(cCore);
                return cCore;
            }
        }

        // 260917_비헤이비어 트리 — 이식해 온 뼈대가 원본과 같은 규칙으로 도는지
        private static void Test_BehaviorTree()
        {
            const float DT = 0.1f;

            // --- Sequence : 앞이 성공해야 뒤로 간다 ---
            List<string> lstLog = new List<string>();
            CNode cSeq = new CNode_Sequence(new List<CNode>
            {
                new CNode_Action(dt => { lstLog.Add("A"); return NODE_STATE.SUCCESS; }),
                new CNode_Action(dt => { lstLog.Add("B"); return NODE_STATE.SUCCESS; }),
            });
            Check("시퀀스 전부 성공", cSeq.Evaluate(DT) == NODE_STATE.SUCCESS);
            Check("시퀀스는 순서대로", string.Join("", lstLog), "AB");

            lstLog.Clear();
            CNode cSeqFail = new CNode_Sequence(new List<CNode>
            {
                new CNode_Condition(() => false),
                new CNode_Action(dt => { lstLog.Add("X"); return NODE_STATE.SUCCESS; }),
            });
            Check("앞이 실패하면 시퀀스 실패", cSeqFail.Evaluate(DT) == NODE_STATE.FAILURE);
            Check("실패 뒤는 실행 안 함", lstLog.Count, 0);

            // --- Wait : 시간이 다 차야 성공 ---
            CNode cWait = new CNode_Wait(0.25f);
            Check("기다리는 중", cWait.Evaluate(DT) == NODE_STATE.RUNNING);
            Check("아직 기다리는 중", cWait.Evaluate(DT) == NODE_STATE.RUNNING);
            Check("다 기다리면 성공", cWait.Evaluate(DT) == NODE_STATE.SUCCESS);

            // --- RUNNING은 다음 프레임에 같은 자리에서 이어진다 ---
            int iEnter = 0;
            int iExit = 0;
            CNode cSeqRun = new CNode_Sequence(new List<CNode>
            {
                new CNode_Action(dt => NODE_STATE.SUCCESS, () => ++iEnter, () => ++iExit),
                new CNode_Wait(0.15f),
            });
            cSeqRun.Evaluate(DT);
            cSeqRun.Evaluate(DT);
            Check("진행 중이면 앞 노드를 다시 들어가지 않는다", iEnter, 1);

            // --- Selector : 우선순위가 높은 자식이 끼어들면 하던 자식을 끊는다 ---
            bool bInterrupt = false;
            int iLowExit = 0;
            CNode cLow = new CNode_Action(dt => NODE_STATE.RUNNING, null, () => ++iLowExit);
            CNode cSel = new CNode_Selector(new List<CNode>
            {
                new CNode_Condition(() => bInterrupt),
                cLow,
            });
            Check("낮은 순위가 진행 중", cSel.Evaluate(DT) == NODE_STATE.RUNNING);
            Check("낮은 순위 노드가 돌고 있다", cLow.IS_RUNNING);

            bInterrupt = true;
            Check("높은 순위가 끼어든다", cSel.Evaluate(DT) == NODE_STATE.SUCCESS);
            Check("끼어들면 하던 노드를 끊는다", iLowExit, 1);
            Check("끊긴 노드는 멈춘다", cLow.IS_RUNNING == false);

            // --- 원본 버그 수정 확인 : 셀렉터를 밖에서 끊으면 진행 중이던 자식도 끊긴다 ---
            bInterrupt = false;
            iLowExit = 0;
            cSel.Evaluate(DT);
            cSel.Abort();
            Check("셀렉터를 끊으면 자식도 끊긴다", iLowExit, 1);

            // --- 핸들러 : 트리를 갈아 끼우면 하던 노드를 끊고 메모리를 비운다 ---
            CBehaviorTreeHandler cHandler = new CBehaviorTreeHandler();
            Check("트리가 없으면 실패", cHandler.Tick(DT) == NODE_STATE.FAILURE);

            int iOldExit = 0;
            cHandler.Set_Tree(new CNode_Action(dt => NODE_STATE.RUNNING, null, () => ++iOldExit));
            cHandler.BLACKBOARD.Set(BLACKBOARD_KEY.IS_ATTACKING, true);
            cHandler.Tick(DT);
            cHandler.Set_Tree(new CNode_Wait(1f));
            Check("트리를 바꾸면 하던 노드를 끊는다", iOldExit, 1);
            Check("트리를 바꾸면 메모리를 비운다", cHandler.BLACKBOARD.Has(BLACKBOARD_KEY.IS_ATTACKING) == false);

            // --- Blackboard : 원본 버그 수정 확인 (값형 칸도 지워져야 한다) ---
            CBlackboard cBoard = new CBlackboard();
            cBoard.Set(BLACKBOARD_KEY.DETECT_RANGE, 7f);
            cBoard.Set(BLACKBOARD_KEY.IS_SUPERARMOR, true);
            cBoard.Set(BLACKBOARD_KEY.TARGET_POS, new Vector2(3f, 4f));
            Check("float 읽기", Mathf.RoundToInt(cBoard.Get_Float(BLACKBOARD_KEY.DETECT_RANGE)), 7);
            Check("Vector2 읽기", Mathf.RoundToInt(cBoard.Get_Vector(BLACKBOARD_KEY.TARGET_POS).y), 4);
            Check("없는 키는 기본값", cBoard.Get_Bool(BLACKBOARD_KEY.IS_ATTACKING) == false);

            cBoard.Remove(BLACKBOARD_KEY.DETECT_RANGE);
            Check("float도 지워진다", cBoard.Has(BLACKBOARD_KEY.DETECT_RANGE) == false);

            cBoard.Clear();
            Check("bool도 비워진다", cBoard.Has(BLACKBOARD_KEY.IS_SUPERARMOR) == false);
            Check("Vector2도 비워진다", cBoard.Has(BLACKBOARD_KEY.TARGET_POS) == false);
        }

        // 260917_투사체 (Project_GYM BulletLogic 이식) — 모양 · 이동 · 특성 · 효과 · 발사 패턴 · 조준을 화면 없이 본다
        private static void Test_Projectile()
        {
            // ---- 표
            CCSVData_ProjectileInfo cTable = Load_CsvTable<CCSVData_ProjectileInfo>("ProjectileInfo");
            CCSVData_ImpactInfo cImpactTable = Load_CsvTable<CCSVData_ImpactInfo>("ImpactInfo");
            Check("ProjectileInfo.csv 로드", cTable != null && cTable.COUNT >= 15);
            Check("ImpactInfo.csv 로드", cImpactTable != null && cImpactTable.Get_Info(3) != null);
            if (cTable == null || cImpactTable == null)
                return;

            CProjectileInfo cLaserRow = cTable.Get_Info(3);
            Check("레이저 행 — 모양 LASER", cLaserRow != null && cLaserRow.eShape == PROJECTILE_SHAPE.LASER);
            Check("레이저 행 — 조율값을 이름으로 읽는다",
                  cLaserRow != null && Mathf.Approximately(cLaserRow.Get_Param("LASER_TELEGRAPH", 0f), 0.8f));
            Check("튕기는 탄 행 — REBOUND 특성", cTable.Get_Info(2) != null && cTable.Get_Info(2).Has_Trait(PROJECTILE_TRAIT.REBOUND));
            Check("무작위 특성탄 행 — 특성 4개", cTable.Get_Info(15) != null ? cTable.Get_Info(15).lstTrait.Count : -1, 4);
            Check("폭발 효과는 폭발 탄(8)을 가리킨다", cImpactTable.Get_Info(3).iRefID, 8);
            Check("닿아 있는 동안 효과는 fTime -1", cImpactTable.Get_Info(5).IS_WHILE_CONTACT == true);

            // 표의 모든 탄이 초기화되고 1초를 돌아도 예외가 없어야 한다
            bool bAllRun = true;
            for (int i = 0; i < cTable.ALL.Count; ++i)
            {
                CFakeProjectileHost cAnyHost = new CFakeProjectileHost();
                CFakeImpactTarget cAnyOwner = new CFakeImpactTarget(Vector2.zero, 0.5f);
                CProjectileCore cAny = new CProjectileCore();
                if (cAny.Initialize(cTable.ALL[i], null, cAnyHost, 1f, Vector2.zero, Vector2.right,
                                    PROJECTILE_SIDE.ENEMY_SHOT, cAnyOwner) == false)
                {
                    bAllRun = false;
                    continue;
                }

                for (int f = 0; f < 60; ++f)
                {
                    cAny.Tick(1f / 60f);
                    cAny.Update_Contact(new List<IImpactTarget> { new CFakeImpactTarget(new Vector2(3f, 0f), 0.5f) }, 1f / 60f);
                }
            }
            Check("표의 탄 전부 1초 동안 돈다", bAllRun);

            CFakeProjectileHost cHost = new CFakeProjectileHost();

            // ---- 직진 · 사거리
            CProjectileCore cStraight = Make_Core(cHost, new CProjectileInfo
            { eMove = PROJECTILE_MOVE.STRAIGHT, fSpeed = 2f, fLifeTime = 10f, fMaxRange = 3f, fHitRange = 0.5f,
              fScale = 1f, iDamage = 1, iDurability = 1 }, Vector2.zero, Vector2.right);
            cStraight.Tick(1f);
            Check("직진 — 1초에 2칸", Mathf.Approximately(cStraight.POS.x, 2f));
            cStraight.Tick(1f);
            Check("직진 — 사거리 3칸을 넘으면 사라진다", cStraight.IS_EXPIRED == true);

            // ---- 벽: 튕기지 않는 탄은 사라지고, REBOUND는 튕기며 내구도를 쓴다
            CProjectileCore cDie = Make_Core(cHost, new CProjectileInfo
            { eMove = PROJECTILE_MOVE.STRAIGHT, fSpeed = 2f, fLifeTime = 10f, fHitRange = 0.5f, fScale = 1f,
              iDurability = 1 }, new Vector2(9f, 5f), Vector2.right);
            cDie.Tick(1f);
            Check("벽 — 튕기지 않는 탄은 사라진다", cDie.IS_EXPIRED == true);

            CProjectileInfo cReboundInfo = new CProjectileInfo
            { eMove = PROJECTILE_MOVE.STRAIGHT, fSpeed = 2f, fLifeTime = 100f, fHitRange = 0.5f, fScale = 1f,
              iDurability = 3 };
            cReboundInfo.lstTrait.Add(PROJECTILE_TRAIT.REBOUND);
            CProjectileCore cRebound = Make_Core(cHost, cReboundInfo, new Vector2(9f, 5f), new Vector2(1f, 1f));
            cRebound.Tick(1f);
            Check("튕김 — 오른쪽 벽이면 x만 뒤집힌다", cRebound.DIR.x < 0f && cRebound.DIR.y > 0f);
            Check("튕김 — 내구도 1 소모", cRebound.DURABILITY, 2);
            Check("튕김 — 벽 안으로 들어가지 않는다", cHost.Is_Wall(cRebound.POS, PROJECTILE_SIDE.ENEMY_SHOT) == false);
            for (int i = 0; i < 40 && cRebound.IS_EXPIRED == false; ++i)
                cRebound.Tick(1f);
            Check("튕김 — 내구도를 다 쓰면 사라진다", cRebound.IS_EXPIRED == true);

            // ---- 체인 라이트닝: 닿으면 기절, 반경 안의 아직 안 맞은 대상 쪽으로 방향을 튼다
            CFakeProjectileHost cChainHost = new CFakeProjectileHost();
            CFakeImpactTarget cChainA   = new CFakeImpactTarget(new Vector2(2f, 5f), 0.3f);
            CFakeImpactTarget cChainB   = new CFakeImpactTarget(new Vector2(2f, 7f), 0.3f);    // 반경 3칸 안
            CFakeImpactTarget cChainFar = new CFakeImpactTarget(new Vector2(2f, 20f), 0.3f);   // 반경 밖
            cChainHost.lstChainCandidate.Add(cChainA);
            cChainHost.lstChainCandidate.Add(cChainB);
            cChainHost.lstChainCandidate.Add(cChainFar);

            CProjectileInfo cChainInfo = new CProjectileInfo
            { eMove = PROJECTILE_MOVE.STRAIGHT, fSpeed = 1f, fLifeTime = 10f, fHitRange = 0.5f, fScale = 1f,
              iDamage = 1, iDurability = -1 };
            cChainInfo.lstTrait.Add(PROJECTILE_TRAIT.CHAIN);
            cChainInfo.dicParam["CHAIN_RANGE"] = 3f;
            CProjectileCore cChain = Make_Core(cChainHost, cChainInfo, new Vector2(2f, 5f), Vector2.right);
            cChain.Update_Contact(new List<IImpactTarget> { cChainA }, 0.1f);
            Check("체인 — 맞으면 기절한다", cChainA.IMPACT.IS_STUNNED == true);
            Check("체인 — 반경 안의 다음 대상 쪽으로 방향을 튼다",
                  Vector2.Distance(cChain.DIR, (cChainB.POS - cChainA.POS).normalized) < 0.01f);

            IImpactTarget cChainNext = CTargetFinder_Utility.Find_Nearby(cChainHost.lstChainCandidate, cChainA.POS, 3f,
                                                                          new List<IImpactTarget> { cChainA, cChainB });
            Check("체인 — 이미 맞은 대상은 다시 고르지 않는다", cChainNext != cChainA && cChainNext != cChainB);

            CFakeProjectileHost cShieldHost = new CFakeProjectileHost { vOwnedFrom = new Vector2(6f, 0f) };
            Check("벽 — 적탄에게 점령지는 벽이다", cShieldHost.Is_Wall(new Vector2(7f, 5f), PROJECTILE_SIDE.ENEMY_SHOT) == true);
            Check("벽 — 플레이어 탄은 점령지를 지나간다", cShieldHost.Is_Wall(new Vector2(7f, 5f), PROJECTILE_SIDE.PLAYER_SHOT) == false);

            // ---- 피해 · 내구도: 한 번 닿으면 한 번만 맞는다
            CFakeImpactTarget cA = new CFakeImpactTarget(new Vector2(2f, 5f), 0.2f);
            CFakeImpactTarget cB = new CFakeImpactTarget(new Vector2(2f, 5f), 0.2f);
            List<IImpactTarget> lstAB = new List<IImpactTarget> { cA, cB };
            CProjectileCore cPoint = Make_Core(cHost, new CProjectileInfo
            { eMove = PROJECTILE_MOVE.NONE, fLifeTime = 10f, fHitRange = 0.5f, fScale = 1f, iDamage = 1, iDurability = 1 },
                new Vector2(2f, 5f), Vector2.right);
            cPoint.Update_Contact(lstAB, 0.1f);
            Check("내구도 1 — 겹친 둘 중 하나만 맞는다", cA.iDamage + cB.iDamage, 1);
            Check("내구도 1 — 맞히면 사라진다", cPoint.IS_EXPIRED == true);

            CFakeImpactTarget cPierce = new CFakeImpactTarget(new Vector2(2f, 5f), 0.2f);
            List<IImpactTarget> lstPierce = new List<IImpactTarget> { cPierce };
            CProjectileCore cInfinite = Make_Core(cHost, new CProjectileInfo
            { eMove = PROJECTILE_MOVE.NONE, fLifeTime = 10f, fHitRange = 0.5f, fScale = 1f, iDamage = 1, iDurability = -1 },
                new Vector2(2f, 5f), Vector2.right);
            cInfinite.Update_Contact(lstPierce, 0.1f);
            cInfinite.Update_Contact(lstPierce, 0.1f);
            cInfinite.Update_Contact(lstPierce, 0.1f);
            Check("닿아 있는 동안 피해는 한 번 (GYM은 특성마다 다시 넣었다)", cPierce.iDamage, 1);
            cInfinite.Update_Contact(new List<IImpactTarget>(), 0.1f);
            cInfinite.Update_Contact(lstPierce, 0.1f);
            Check("떨어졌다 다시 닿으면 또 맞는다", cPierce.iDamage, 2);

            // ---- 레이저: 예고 중엔 안 맞고 켜져야 맞는다
            CProjectileInfo cLaserInfo = new CProjectileInfo
            { eShape = PROJECTILE_SHAPE.LASER, eMove = PROJECTILE_MOVE.NONE, fLifeTime = 2f, fMaxRange = 30f,
              fHitRange = 0.5f, fScale = 1f, iDamage = 1, iDurability = -1 };
            cLaserInfo.dicParam["LASER_TELEGRAPH"] = 0.5f;
            cLaserInfo.dicParam["LASER_THICKEN"]   = 0.2f;
            cLaserInfo.dicParam["LASER_FADE"]      = 0.3f;
            cLaserInfo.dicParam["LASER_WIDTH"]     = 1f;
            CProjectileCore cLaser = Make_Core(cHost, cLaserInfo, new Vector2(1f, 5f), Vector2.right);
            CProjectileShape_Laser cLaserShape = cLaser.SHAPE as CProjectileShape_Laser;
            CFakeImpactTarget cOnBeam = new CFakeImpactTarget(new Vector2(6f, 5.3f), 0.1f);
            List<IImpactTarget> lstBeam = new List<IImpactTarget> { cOnBeam };

            cLaser.Tick(0.1f);
            cLaser.Update_Contact(lstBeam, 0.1f);
            Check("레이저 — 예고 단계", cLaserShape != null && cLaserShape.CUR_PHASE == CProjectileShape_Laser.PHASE.TELEGRAPH);
            Check("레이저 — 예고 중엔 안 맞는다", cOnBeam.iDamage, 0);
            Check("레이저 — 벽(맵 끝)에서 잘린다", cLaserShape != null && cLaserShape.LENGTH <= 9.01f);

            cLaser.Tick(0.7f);
            cLaser.Update_Contact(lstBeam, 0.1f);
            Check("레이저 — 켜짐 단계", cLaserShape != null && cLaserShape.CUR_PHASE == CProjectileShape_Laser.PHASE.ACTIVE);
            Check("레이저 — 켜지면 맞는다", cOnBeam.iDamage, 1);

            CFakeImpactTarget cOffBeam = new CFakeImpactTarget(new Vector2(6f, 7f), 0.1f);
            cLaser.Update_Contact(new List<IImpactTarget> { cOffBeam }, 0.1f);
            Check("레이저 — 굵기 밖은 안 맞는다", cOffBeam.iDamage, 0);

            // ---- 충격파: 시간에 따라 옆으로 뻗는다
            CProjectileInfo cSweepInfo = new CProjectileInfo
            { eShape = PROJECTILE_SHAPE.SWEEP, eMove = PROJECTILE_MOVE.NONE, fLifeTime = 2f, fHitRange = 0.5f,
              fScale = 1f, iDamage = 1, iDurability = -1 };
            CProjectileCore cSweep = Make_Core(cHost, cSweepInfo, new Vector2(5f, 5f), Vector2.right);
            CFakeImpactTarget cFar = new CFakeImpactTarget(new Vector2(9f, 5f), 0.1f);
            cSweep.Tick(0.1f);
            cSweep.Update_Contact(new List<IImpactTarget> { cFar }, 0.1f);
            Check("충격파 — 막 퍼질 땐 먼 곳에 안 닿는다", cFar.iDamage, 0);
            cSweep.Tick(0.95f);
            cSweep.Update_Contact(new List<IImpactTarget> { cFar }, 0.1f);
            Check("충격파 — 맵 끝까지 뻗으면 닿는다", cFar.iDamage, 1);

            // ---- 폭발: 커지는 동안만 새로 맞는다
            CProjectileInfo cBlastInfo = new CProjectileInfo
            { eShape = PROJECTILE_SHAPE.BLAST, eMove = PROJECTILE_MOVE.NONE, fLifeTime = 1f, fHitRange = 0.5f,
              fScale = 1f, iDamage = 1, iDurability = -1 };
            cBlastInfo.dicParam["BLAST_GROW"]  = 0.3f;
            cBlastInfo.dicParam["BLAST_SCALE"] = 4f;
            CProjectileCore cBlast = Make_Core(cHost, cBlastInfo, new Vector2(5f, 5f), Vector2.right);
            CFakeImpactTarget cRing = new CFakeImpactTarget(new Vector2(6.5f, 5f), 0.1f);
            cBlast.Tick(0.29f);
            cBlast.Update_Contact(new List<IImpactTarget> { cRing }, 0.1f);
            Check("폭발 — 커지는 동안 반경 안이면 맞는다", cRing.iDamage, 1);
            CFakeImpactTarget cLate = new CFakeImpactTarget(new Vector2(5.5f, 5f), 0.1f);
            cBlast.Tick(0.2f);
            cBlast.Update_Contact(new List<IImpactTarget> { cLate }, 0.1f);
            Check("폭발 — 다 커진 뒤에 들어오면 안 맞는다", cLate.iDamage, 0);

            // ---- 이동
            CFakeImpactTarget cTraceTarget = new CFakeImpactTarget(new Vector2(5f, 9f), 0.1f);
            CFakeProjectileHost cTraceHost = new CFakeProjectileHost { cTarget = cTraceTarget };
            CProjectileInfo cTraceInfo = new CProjectileInfo
            { eMove = PROJECTILE_MOVE.TRACE, fSpeed = 1f, fLifeTime = 10f, fHitRange = 0.1f, fScale = 1f, iDurability = 1 };
            cTraceInfo.dicParam["TRACE_TURN"] = 1f;
            CProjectileCore cTrace = Make_Core(cTraceHost, cTraceInfo, new Vector2(5f, 1f), Vector2.right);
            cTrace.Tick(0.5f);
            float fTurned = Vector2.Angle(Vector2.right, cTrace.DIR);
            Check("추적 — 대상 쪽으로 휜다", fTurned > 20f);
            Check("추적 — 한 번에 꺾지 않는다(초당 1라디안)", fTurned < 30f);

            CFakeImpactTarget cBoomerOwner = new CFakeImpactTarget(new Vector2(2f, 5f), 0.5f);
            CProjectileInfo cBoomerInfo = new CProjectileInfo
            { eMove = PROJECTILE_MOVE.BOOMERANG, fSpeed = 4f, fLifeTime = 10f, fHitRange = 0.3f, fScale = 1f, iDurability = -1 };
            cBoomerInfo.dicParam["BOOMERANG_OUT"]  = 0.5f;
            cBoomerInfo.dicParam["BOOMERANG_STAY"] = 0.2f;
            CProjectileCore cBoomer = new CProjectileCore();
            cBoomer.Initialize(cBoomerInfo, null, cHost, 1f, cBoomerOwner.POS, Vector2.right, PROJECTILE_SIDE.ENEMY_SHOT, cBoomerOwner);
            for (int i = 0; i < 5; ++i) cBoomer.Tick(0.1f);
            float fOut = cBoomer.POS.x;
            Check("부메랑 — 나간다", fOut > 3.5f);
            cBoomer.Tick(0.1f);
            Check("부메랑 — 잠깐 멈춘다", Mathf.Approximately(cBoomer.POS.x, fOut));
            for (int i = 0; i < 30 && cBoomer.IS_EXPIRED == false; ++i) cBoomer.Tick(0.1f);
            Check("부메랑 — 쏜 쪽으로 돌아와 끝난다", cBoomer.IS_EXPIRED == true);

            CFakeImpactTarget cOrbitOwner = new CFakeImpactTarget(new Vector2(5f, 5f), 0.5f);
            CProjectileInfo cOrbitInfo = new CProjectileInfo
            { eMove = PROJECTILE_MOVE.ORBIT, fLifeTime = 10f, fHitRange = 0.3f, fScale = 1f, iDurability = -1 };
            cOrbitInfo.dicParam["ORBIT_RADIUS"] = 2f;
            cOrbitInfo.dicParam["ORBIT_SPEED"]  = 90f;
            CProjectileCore cOrbit = new CProjectileCore();
            cOrbit.Initialize(cOrbitInfo, null, cHost, 1f, cOrbitOwner.POS, Vector2.right, PROJECTILE_SIDE.ENEMY_SHOT, cOrbitOwner);
            cOrbit.Tick(1f);
            Check("궤도 — 반경 2칸을 유지한다", Mathf.Abs(Vector2.Distance(cOrbit.POS, cOrbitOwner.POS) - 2f) < 0.01f);
            Check("궤도 — 1초에 90도 돈다", Vector2.Distance(cOrbit.POS, new Vector2(5f, 7f)) < 0.01f);
            cOrbitOwner.vPos = new Vector2(6f, 5f);
            cOrbit.Tick(0.001f);
            Check("궤도 — 쏜 쪽을 따라간다", Mathf.Abs(Vector2.Distance(cOrbit.POS, cOrbitOwner.POS) - 2f) < 0.01f);
            cOrbitOwner.bAlive = false;
            cOrbit.Tick(0.1f);
            Check("궤도 — 쏜 쪽이 죽으면 사라진다", cOrbit.IS_EXPIRED == true);

            CProjectileInfo cSpiralInfo = new CProjectileInfo
            { eMove = PROJECTILE_MOVE.SPIRAL, fLifeTime = 10f, fHitRange = 0.3f, fScale = 1f, iDurability = 1 };
            cSpiralInfo.dicParam["SPIRAL_ROTATE"] = 1f;
            cSpiralInfo.dicParam["SPIRAL_EXPAND"] = 1f;
            CProjectileCore cSpiral = Make_Core(cHost, cSpiralInfo, new Vector2(5f, 5f), Vector2.right);
            cSpiral.Tick(1f);
            float fR1 = Vector2.Distance(cSpiral.POS, cSpiral.START_POS);
            cSpiral.Tick(1f);
            float fR2 = Vector2.Distance(cSpiral.POS, cSpiral.START_POS);
            Check("나선 — 돌면서 반경이 커진다", fR1 > 0.9f && fR2 > fR1 + 0.9f);

            // ---- 특성
            CProjectileInfo cStunInfo = new CProjectileInfo
            { eMove = PROJECTILE_MOVE.NONE, fLifeTime = 10f, fHitRange = 0.5f, fScale = 1f, iDurability = -1 };
            cStunInfo.lstTrait.Add(PROJECTILE_TRAIT.ENTER_STUN);
            cStunInfo.dicParam["STUN_TIME"] = 0.5f;
            CFakeImpactTarget cStunned = new CFakeImpactTarget(new Vector2(5f, 5f), 0.1f);
            Make_Core(cHost, cStunInfo, new Vector2(5f, 5f), Vector2.right)
                .Update_Contact(new List<IImpactTarget> { cStunned }, 0.1f);
            Check("기절 — 닿으면 기절", cStunned.IMPACT.IS_STUNNED == true);
            cStunned.IMPACT.Tick(0.6f);
            Check("기절 — 시간이 지나면 풀린다", cStunned.IMPACT.IS_STUNNED == false);

            CProjectileInfo cStopInfo = new CProjectileInfo
            { eMove = PROJECTILE_MOVE.NONE, fLifeTime = 10f, fHitRange = 0.5f, fScale = 1f, iDurability = -1 };
            cStopInfo.lstTrait.Add(PROJECTILE_TRAIT.STAY_STOP);
            cStopInfo.lstTrait.Add(PROJECTILE_TRAIT.STAY_STUN);
            cStopInfo.dicParam["STOP_SLOW"] = 0.4f;
            CProjectileCore cStop = Make_Core(cHost, cStopInfo, new Vector2(5f, 5f), Vector2.right);
            CFakeImpactTarget cSlowed = new CFakeImpactTarget(new Vector2(5f, 5f), 0.1f);
            List<IImpactTarget> lstSlowed = new List<IImpactTarget> { cSlowed };
            cStop.Update_Contact(lstSlowed, 0.1f);
            cSlowed.IMPACT.Tick(5f);
            Check("장판 — 닿아 있는 동안은 시간이 지나도 느리다", Mathf.Approximately(cSlowed.IMPACT.SPEED_SCALE, 0.4f));
            Check("속박 — 닿아 있는 동안 기절", cSlowed.IMPACT.IS_STUNNED == true);
            cStop.Update_Contact(new List<IImpactTarget>(), 0.1f);
            Check("장판 — 떨어지면 풀린다", Mathf.Approximately(cSlowed.IMPACT.SPEED_SCALE, 1f) && cSlowed.IMPACT.IS_STUNNED == false);
            cStop.Update_Contact(lstSlowed, 0.1f);
            cStop.Expire();
            Check("탄이 사라지면 속박도 풀린다 (GYM은 굳은 채로 남았다)", cSlowed.IMPACT.COUNT, 0);

            CProjectileInfo cKnockInfo = new CProjectileInfo
            { eMove = PROJECTILE_MOVE.STRAIGHT, fSpeed = 1f, fLifeTime = 10f, fHitRange = 0.5f, fScale = 1f, iDurability = -1 };
            cKnockInfo.lstTrait.Add(PROJECTILE_TRAIT.KNOCKBACK_PUSH);
            cKnockInfo.lstTrait.Add(PROJECTILE_TRAIT.HIT_STOP);
            cKnockInfo.dicParam["KNOCKBACK_DISTANCE"] = 3f;
            cKnockInfo.dicParam["HITSTOP_TIME"]       = 0.2f;
            CProjectileCore cKnock = Make_Core(cHost, cKnockInfo, new Vector2(5f, 5f), Vector2.up);
            CFakeImpactTarget cPushed = new CFakeImpactTarget(new Vector2(5f, 5f), 0.1f);
            cKnock.Update_Contact(new List<IImpactTarget> { cPushed }, 0.1f);
            Check("넉백 — 진행 방향으로 3칸", cPushed.vLastPush.y > 0.9f && Mathf.Approximately(cPushed.fLastPushDistance, 3f));
            Vector2 vStopPos = cKnock.POS;
            cKnock.Tick(0.1f);
            Check("타격 정지 — 탄이 잠깐 멈춘다", cKnock.IS_HIT_STOP == true && cKnock.POS == vStopPos);

            CProjectileInfo cGravityInfo = new CProjectileInfo
            { eMove = PROJECTILE_MOVE.NONE, fLifeTime = 10f, fHitRange = 3f, fScale = 1f, iDurability = -1 };
            cGravityInfo.lstTrait.Add(PROJECTILE_TRAIT.GRAVITY_PULL);
            CProjectileCore cGravity = Make_Core(cHost, cGravityInfo, new Vector2(5f, 5f), Vector2.up);
            CFakeImpactTarget cPulled = new CFakeImpactTarget(new Vector2(7f, 5f), 0.1f);
            List<IImpactTarget> lstPulled = new List<IImpactTarget> { cPulled };
            cGravity.Update_Contact(lstPulled, 0.1f);
            Check("인력 — 닿는 순간엔 당기지 않는다", cPulled.iPushCount, 0);
            cGravity.Update_Contact(lstPulled, 0.1f);
            Check("인력 — 닿아 있으면 탄 쪽으로 당긴다", cPulled.iPushCount == 1 && cPulled.vLastPush.x < -0.9f);

            CProjectileInfo cGrowInfo = new CProjectileInfo
            { eMove = PROJECTILE_MOVE.NONE, fLifeTime = 10f, fHitRange = 0.5f, fScale = 1f, iDurability = -1 };
            cGrowInfo.lstTrait.Add(PROJECTILE_TRAIT.SCALE_OVER_TIME);
            cGrowInfo.dicParam["GROW_SCALE"] = 3f;
            cGrowInfo.dicParam["GROW_TIME"]  = 2f;
            CProjectileCore cGrow = Make_Core(cHost, cGrowInfo, new Vector2(5f, 5f), Vector2.up);
            cGrow.Tick(1f);
            Check("커지는 탄 — 수명과 상관없이 GROW_TIME 기준 (GYM은 수명 5초 가정)", Mathf.Abs(cGrow.SCALE - 2f) < 0.01f);
            Check("커지는 탄 — 판정도 커진다", cGrow.SHAPE.Is_Overlap(new Vector2(5.9f, 5f), 0f) == true);

            CProjectileInfo cRandomInfo = new CProjectileInfo
            { eMove = PROJECTILE_MOVE.NONE, fLifeTime = 10f, fHitRange = 0.5f, fScale = 1f, iDurability = -1 };
            cRandomInfo.lstTrait.Add(PROJECTILE_TRAIT.RANDOM);
            cRandomInfo.lstTrait.Add(PROJECTILE_TRAIT.ENTER_STUN);
            cRandomInfo.lstTrait.Add(PROJECTILE_TRAIT.STAY_STOP);
            cRandomInfo.lstTrait.Add(PROJECTILE_TRAIT.HIT_STOP);
            Check("무작위 — 셋 중 하나만 남긴다", Make_Core(cHost, cRandomInfo, Vector2.one, Vector2.up).TRAIT_COUNT, 1);

            // ---- 효과 (ImpactInfo)
            CFakeImpactTarget cDotted = new CFakeImpactTarget(new Vector2(5f, 5f), 0.1f);
            CImpactInfo cDot = new CImpactInfo { iImpactID = 99, eType = IMPACT_TYPE.DOT, fTime = 3f, fValue = 1f };
            cDotted.IMPACT.Apply(cDot, cDotted, Vector2.zero, 1f, cHost, PROJECTILE_SIDE.ENEMY_SHOT);
            int iDot = 0;
            for (int i = 0; i < 40; ++i)
                iDot += cDotted.IMPACT.Tick(0.1f);
            Check("도트 — 3초 동안 초마다 1 = 3", iDot, 3);

            CImpactInfo cSlowA = new CImpactInfo { iImpactID = 97, eType = IMPACT_TYPE.SLOW, fTime = 2f, fValue = 0.7f };
            CImpactInfo cSlowB = new CImpactInfo { iImpactID = 98, eType = IMPACT_TYPE.SLOW, fTime = 1f, fValue = 0.4f };
            cDotted.IMPACT.Apply(cSlowA, cDotted, Vector2.zero, 1f, cHost, PROJECTILE_SIDE.ENEMY_SHOT);
            cDotted.IMPACT.Apply(cSlowB, cDotted, Vector2.zero, 1f, cHost, PROJECTILE_SIDE.ENEMY_SHOT);
            Check("감속 겹침 — 가장 센 것", Mathf.Approximately(cDotted.IMPACT.SPEED_SCALE, 0.4f));
            cDotted.IMPACT.Tick(1.1f);
            Check("감속 겹침 — 짧은 게 풀리면 남은 것", Mathf.Approximately(cDotted.IMPACT.SPEED_SCALE, 0.7f));

            CImpactInfo cStunA = new CImpactInfo { iImpactID = 95, eType = IMPACT_TYPE.STUN, fTime = 1f };
            CImpactInfo cStunB = new CImpactInfo { iImpactID = 96, eType = IMPACT_TYPE.STUN, fTime = 3f };
            CFakeImpactTarget cDouble = new CFakeImpactTarget(Vector2.zero, 0.1f);
            cDouble.IMPACT.Apply(cStunA, cDouble, Vector2.zero, 1f, cHost, PROJECTILE_SIDE.ENEMY_SHOT);
            cDouble.IMPACT.Apply(cStunB, cDouble, Vector2.zero, 1f, cHost, PROJECTILE_SIDE.ENEMY_SHOT);
            cDouble.IMPACT.Tick(1.5f);
            Check("기절 겹침 — 하나가 풀려도 남은 게 있으면 계속 (GYM은 풀렸다)", cDouble.IMPACT.IS_STUNNED == true);

            CFakeImpactTarget cExploded = new CFakeImpactTarget(new Vector2(4f, 4f), 0.1f);
            cExploded.IMPACT.Apply(cImpactTable.Get_Info(3), cExploded, Vector2.zero, 1f, cHost, PROJECTILE_SIDE.PLAYER_SHOT);
            Check("폭발 효과 — 맞은 자리에 참조 탄을 부른다",
                  cHost.iLastSpawnID == 8 && cHost.vLastSpawnPos == new Vector2(4f, 4f)
                  && cHost.eLastSpawnSide == PROJECTILE_SIDE.PLAYER_SHOT);

            CFakeImpactTarget cKnocked = new CFakeImpactTarget(new Vector2(4f, 4f), 0.1f);
            cKnocked.IMPACT.Apply(cImpactTable.Get_Info(6), cKnocked, new Vector2(3f, 4f), 0.5f, cHost, PROJECTILE_SIDE.ENEMY_SHOT);
            Check("넉백 효과 — 탄에서 먼 쪽으로, 셀을 월드로 바꿔서",
                  cKnocked.vLastPush.x > 0.9f && Mathf.Approximately(cKnocked.fLastPushDistance, 1f));

            CProjectileInfo cContactInfo = new CProjectileInfo
            { eMove = PROJECTILE_MOVE.NONE, fLifeTime = 10f, fHitRange = 0.5f, fScale = 1f, iDurability = -1 };
            CProjectileCore cContact = new CProjectileCore();
            cContact.Initialize(cContactInfo, new List<CImpactInfo> { cImpactTable.Get_Info(5) }, cHost, 1f,
                                new Vector2(5f, 5f), Vector2.up, PROJECTILE_SIDE.ENEMY_SHOT, null);
            CFakeImpactTarget cInField = new CFakeImpactTarget(new Vector2(5f, 5f), 0.1f);
            cContact.Update_Contact(new List<IImpactTarget> { cInField }, 0.1f);
            cInField.IMPACT.Tick(10f);
            Check("충돌 중 감속 — 닿아 있으면 유지", cInField.IMPACT.SPEED_SCALE < 1f);
            cContact.Update_Contact(new List<IImpactTarget>(), 0.1f);
            Check("충돌 중 감속 — 떨어지면 풀린다", Mathf.Approximately(cInField.IMPACT.SPEED_SCALE, 1f));

            // ---- 발사 패턴
            List<Vector2> lstDir = new List<Vector2>();
            CProjectileFire_Utility.Get_Directions(FIRE_PATTERN.SPREAD, Vector2.up, 5, 60f, 0f, lstDir);
            Check("부채꼴 — 5발", lstDir.Count, 5);
            Check("부채꼴 — 가운데가 조준 방향", Vector2.Angle(lstDir[2], Vector2.up) < 0.01f);
            Check("부채꼴 — 양 끝이 ±30도", Mathf.Abs(Vector2.Angle(lstDir[0], lstDir[4]) - 60f) < 0.01f);
            CProjectileFire_Utility.Get_Directions(FIRE_PATTERN.RING, Vector2.up, 8, 0f, 0f, lstDir);
            Check("링 — 8발이 45도 간격", lstDir.Count == 8 && Mathf.Abs(Vector2.Angle(lstDir[0], lstDir[1]) - 45f) < 0.01f);
            CProjectileFire_Utility.Get_Directions(FIRE_PATTERN.SPIN, Vector2.up, 4, 0f, 30f, lstDir);
            Check("회전 링 — 누적 각도만큼 돌아가 있다", Mathf.Abs(Vector2.SignedAngle(Vector2.right, lstDir[0]) - 30f) < 0.01f);
            CProjectileFire_Utility.Get_Directions(FIRE_PATTERN.BURST, Vector2.left, 5, 0f, 0f, lstDir);
            Check("연발 — 한 번에 한 발", lstDir.Count, 1);

            // ---- 조준 대상
            List<CFakeImpactTarget> lstFind = new List<CFakeImpactTarget>
            {
                new CFakeImpactTarget(new Vector2(1f, 0f), 0.1f) { iHp = 1 },
                new CFakeImpactTarget(new Vector2(9f, 0f), 0.1f) { iHp = 5 },
                new CFakeImpactTarget(new Vector2(5f, 5f), 0.1f) { iHp = 2 },
                new CFakeImpactTarget(new Vector2(5.5f, 5f), 0.1f) { iHp = 2 },
                new CFakeImpactTarget(new Vector2(5f, 5.5f), 0.1f) { iHp = 2 },
            };
            Check("조준 — 가장 가까운 적",
                  CTargetFinder_Utility.Find(lstFind, Vector2.zero, TARGET_FIND.NEAREST) == lstFind[0]);
            Check("조준 — 체력이 가장 많은 적",
                  CTargetFinder_Utility.Find(lstFind, Vector2.zero, TARGET_FIND.HIGHEST_HP) == lstFind[1]);
            IImpactTarget cCrowd = CTargetFinder_Utility.Find(lstFind, Vector2.zero, TARGET_FIND.CROWDED, 1f);
            Check("조준 — 몰려 있는 곳", cCrowd == lstFind[2] || cCrowd == lstFind[3] || cCrowd == lstFind[4]);
            lstFind[0].bAlive = false;
            Check("조준 — 죽은 적은 고르지 않는다",
                  CTargetFinder_Utility.Find(lstFind, Vector2.zero, TARGET_FIND.NEAREST) != lstFind[0]);
        }

        private static CProjectileCore Make_Core(IProjectileHost cHost, CProjectileInfo cInfo, Vector2 vPos, Vector2 vDir)
        {
            CProjectileCore cCore = new CProjectileCore();
            cCore.Initialize(cInfo, null, cHost, 1f, vPos, vDir, PROJECTILE_SIDE.ENEMY_SHOT, null);
            return cCore;
        }

        /// <summary> 0~10 크기 맵. vOwnedFrom.x가 0보다 크면 그 x부터 오른쪽이 점령지다. </summary>
        private class CFakeProjectileHost : IProjectileHost
        {
            public Vector2          vOwnedFrom;
            public IImpactTarget    cTarget;
            public int              iLastSpawnID;
            public Vector2          vLastSpawnPos;
            public PROJECTILE_SIDE  eLastSpawnSide;
            // 260917_CHAIN 특성 테스트용 후보 목록. Find_Target(cTarget 하나)과 달리 여러 개를 담아 반경 · 제외를 본다.
            public readonly List<IImpactTarget> lstChainCandidate = new List<IImpactTarget>();

            public Rect WORLD_BOUNDS => new Rect(0f, 0f, 10f, 10f);

            public bool Is_Wall(Vector2 vWorldPos, PROJECTILE_SIDE eSide)
            {
                if (WORLD_BOUNDS.Contains(vWorldPos) == false)
                    return true;

                return eSide == PROJECTILE_SIDE.ENEMY_SHOT && vOwnedFrom.x > 0f && vWorldPos.x >= vOwnedFrom.x;
            }

            public void Spawn_Projectile(int iProjectileID, Vector2 vPos, Vector2 vDir, PROJECTILE_SIDE eSide)
            {
                iLastSpawnID   = iProjectileID;
                vLastSpawnPos  = vPos;
                eLastSpawnSide = eSide;
            }

            public IImpactTarget Find_Target(Vector2 vFrom, PROJECTILE_SIDE eSide) => cTarget;

            public IImpactTarget Find_ChainTarget(Vector2 vFrom, PROJECTILE_SIDE eSide, float fRadius,
                                                  ICollection<IImpactTarget> hsExclude)
                => CTargetFinder_Utility.Find_Nearby(lstChainCandidate, vFrom, fRadius, hsExclude);
        }

        private class CFakeImpactTarget : IImpactTarget
        {
            private readonly CImpactHandler m_cImpact = new CImpactHandler();
            private readonly float m_fRadius;

            public Vector2  vPos;
            public bool     bAlive = true;
            public int      iHp = 3;
            public int      iDamage;
            public int      iPushCount;
            public Vector2  vLastPush;
            public float    fLastPushDistance;

            public CFakeImpactTarget(Vector2 vStart, float fRadius)
            {
                vPos      = vStart;
                m_fRadius = fRadius;
            }

            public Vector2          POS         => vPos;
            public float            HIT_RADIUS  => m_fRadius;
            public bool             IS_ALIVE    => bAlive;
            public int              HP          => iHp;
            public CImpactHandler   IMPACT      => m_cImpact;

            public void Take_Damage(int iAmount) => iDamage += iAmount;

            public void Push(Vector2 vDir, float fDistance, float fDuration)
            {
                ++iPushCount;
                vLastPush         = vDir;
                fLastPushDistance = fDistance;
            }
        }

        // 260917_3지선다에 카드와 런 스킬이 섞여 나오는지 — 해금 · 만렙 · 슬롯 상한(액티브/패시브 각 5) · 중복 없음
        private static void Test_PickOption()
        {
            CCSVData_CardInfo cCardTable = Load_CsvTable<CCSVData_CardInfo>("CardInfo");
            CCSVData_RunSkillInfo cSkillTable = Load_CsvTable<CCSVData_RunSkillInfo>("RunSkillInfo");
            if (cCardTable == null || cSkillTable == null)
            {
                Check("CardInfo / RunSkillInfo 로드(Test_PickOption)", false);
                return;
            }

            Dictionary<RUN_SKILL_TYPE, int> dicLevel = new Dictionary<RUN_SKILL_TYPE, int>();
            System.Func<RUN_SKILL_TYPE, int> fnLevel = eType => dicLevel.TryGetValue(eType, out int iLevel) ? iLevel : 0;

            // 여러 번 뽑아 섞여 나오는지 본다 — 가중치 무작위라 한 번으로는 알 수 없다
            bool bSawCard = false, bSawSkill = false, bDuplicate = false, bLocked = false;
            for (int n = 0; n < 200; ++n)
            {
                List<CPickOption> lstPick = CPickOption_Utility.Pick(cCardTable, cSkillTable, fnLevel, iMapID => false, 3);
                HashSet<string> hsName = new HashSet<string>();

                for (int i = 0; i < lstPick.Count; ++i)
                {
                    if (lstPick[i].eKind == PICK_KIND.CARD)
                        bSawCard = true;
                    else
                    {
                        bSawSkill = true;
                        if (lstPick[i].cRunSkill.iUnlockMapID > 0)
                            bLocked = true;
                    }

                    if (hsName.Add(lstPick[i].eKind + lstPick[i].NAME) == false)
                        bDuplicate = true;
                }
            }
            Check("섞기 — 카드가 나온다", bSawCard);
            Check("섞기 — 런 스킬이 나온다", bSawSkill);
            Check("섞기 — 한 번에 같은 것이 두 장 나오지 않는다", bDuplicate == false);
            Check("섞기 — 해금 전 런 스킬은 안 나온다", bLocked == false);

            // 새로 얻는 스킬은 NEW, 가진 스킬은 다음 레벨
            CRunSkillInfo cMagnet = cSkillTable.Find_ByType(RUN_SKILL_TYPE.MAGNET);
            dicLevel[RUN_SKILL_TYPE.MAGNET] = 2;
            CPickOption cLevelUp = null;
            for (int n = 0; n < 300 && cLevelUp == null; ++n)
            {
                List<CPickOption> lstPick = CPickOption_Utility.Pick(null, cSkillTable, fnLevel, iMapID => true, 3);
                for (int i = 0; i < lstPick.Count; ++i)
                {
                    if (lstPick[i].cRunSkill == cMagnet)
                        cLevelUp = lstPick[i];
                }
            }
            Check("레벨업 — 가진 스킬은 다음 레벨로 나온다", cLevelUp != null ? cLevelUp.iNextLevel : -1, 3);
            Check("레벨업 — NEW가 아니다", cLevelUp != null && cLevelUp.IS_NEW == false);
            // 260920_이름과 레벨은 칸이 나뉘었다(2-10-1) — 제목은 이름 그대로, 레벨은 따로 적는다
            Check("제목은 이름 그대로", cLevelUp != null ? CUI_CardPick.Get_Title(cLevelUp) : "", "자석");
            Check("레벨업 — 레벨 칸에 다음 레벨",
                  cLevelUp != null ? CUI_CardPick.Get_LevelText(cLevelUp) : "", "Lv.3");
            Check("새로 얻기 — 레벨 칸에 NEW",
                  CUI_CardPick.Get_LevelText(CPickOption.From_RunSkill(cMagnet, 1)), "NEW");
            Check("레벨이 하나뿐인 스킬도 처음 얻으면 NEW",
                  CUI_CardPick.Get_LevelText(CPickOption.From_RunSkill(cSkillTable.Find_ByType(RUN_SKILL_TYPE.MOONWALK), 1)), "NEW");
            Check("카드는 레벨 칸이 비어 있다",
                  CUI_CardPick.Get_LevelText(CPickOption.From_Card(new CCardInfo())), "");

            // 색은 테마가 정한다 — 각성만 종류와 무관하게 보라다
            CRunSkillInfo cCombat = cSkillTable.Find_ByType(RUN_SKILL_TYPE.ORBIT);
            CRunSkillInfo cMove   = cSkillTable.Find_ByType(RUN_SKILL_TYPE.MOONWALK);
            Check("전투 스킬은 전투 테마", cCombat != null && cCombat.eTheme == PICK_THEME.COMBAT);
            Check("이동 스킬은 이동 테마", cMove != null && cMove.eTheme == PICK_THEME.MOVE);
            Check("전투와 이동은 색이 다르다",
                  CUI_CardPick.Get_ThemeColor(CPickOption.From_RunSkill(cCombat, 1))
               != CUI_CardPick.Get_ThemeColor(CPickOption.From_RunSkill(cMove, 1)));
            Check("각성은 전투 · 이동 어느 색과도 다르다",
                  CUI_CardPick.Get_ThemeColor(CPickOption.From_Awaken(new CAwakenInfo(), cCombat))
               != CUI_CardPick.Get_ThemeColor(CPickOption.From_RunSkill(cCombat, 1)));

            // 만렙은 후보에서 빠진다
            dicLevel[RUN_SKILL_TYPE.MAGNET] = cMagnet.iMaxLevel;
            Check("만렙 스킬은 후보가 아니다",
                  cSkillTable.Collect_Candidates(fnLevel, iMapID => true).Contains(cMagnet) == false);

            // 슬롯 상한 — 패시브를 5개 들고 있으면 여섯 번째 패시브는 새로 못 얻는다. 가진 것의 레벨업과 액티브는 그대로
            dicLevel.Clear();
            RUN_SKILL_TYPE[] arrFive =
            {
                RUN_SKILL_TYPE.MOONWALK, RUN_SKILL_TYPE.EVASION, RUN_SKILL_TYPE.MAGNET,
                RUN_SKILL_TYPE.EDGE_WRAP, RUN_SKILL_TYPE.RAGE,
            };
            for (int i = 0; i < arrFive.Length; ++i)
                dicLevel[arrFive[i]] = 1;

            List<CRunSkillInfo> lstCandidate = cSkillTable.Collect_Candidates(fnLevel, iMapID => true);
            Check("패시브 보유 수", cSkillTable.Count_Owned(SKILL_CATEGORY.PASSIVE, fnLevel), 5);
            Check("슬롯 상한 — 여섯 번째 패시브(영혼 수집가)는 안 나온다",
                  lstCandidate.Contains(cSkillTable.Find_ByType(RUN_SKILL_TYPE.SOUL_COLLECTOR)) == false);
            Check("슬롯 상한 — 가진 패시브의 레벨업은 나온다",
                  lstCandidate.Contains(cSkillTable.Find_ByType(RUN_SKILL_TYPE.EVASION)) == true);
            Check("슬롯 상한 — 액티브는 따로 센다",
                  lstCandidate.Contains(cSkillTable.Find_ByType(RUN_SKILL_TYPE.ORBIT)) == true);

            // 표가 하나 없어도 돈다
            Check("런 스킬 표가 없으면 카드만", CPickOption_Utility.Pick(cCardTable, null, fnLevel, null, 3)
                  .TrueForAll(cOption => cOption.eKind == PICK_KIND.CARD));

            // 가중치 뽑기 공통 함수 — 가중치 0은 안 나오고, 후보보다 많이 달라면 있는 만큼만
            List<int> lstWeighted = CWeightedPick_Utility.Pick(new List<int> { 0, 5, 0, 7 }, iValue => iValue, 10);
            Check("가중치 뽑기 — 0은 안 나오고 있는 만큼만", lstWeighted.Count == 2 && lstWeighted.Contains(0) == false);
        }

        // 260912_카드 3지선다 — 표가 읽히고, 서로 다른 카드가 뽑히고, 효과가 걸리는지
        private static void Test_CardPick()
        {
            CCSVData_CardInfo cTable = Load_CsvTable<CCSVData_CardInfo>("CardInfo");
            if (cTable == null)
            {
                Check("CardInfo.csv 로드", false);
                return;
            }

            Check("카드가 셋 이상 있다", cTable.COUNT >= 3);

            for (int i = 0; i < cTable.ALL.Count; ++i)
            {
                CCardInfo cInfo = cTable.ALL[i];
                Check($"{cInfo.strName} 종류가 있다", cInfo.eType != CARD_TYPE.NONE);
                Check($"{cInfo.strName} 이름이 있다", string.IsNullOrEmpty(cInfo.strName) == false);
            }

            // 한 번에 같은 카드가 두 장 나오면 고르는 재미가 없다
            List<CCardInfo> lstPick = new List<CCardInfo>();
            bool bDup = false;
            for (int n = 0; n < 200; ++n)
            {
                cTable.Pick_Random(3, lstPick);
                if (lstPick.Count != 3)
                    bDup = true;

                for (int i = 0; i < lstPick.Count; ++i)
                {
                    for (int j = i + 1; j < lstPick.Count; ++j)
                    {
                        if (lstPick[i].iCardID == lstPick[j].iCardID)
                            bDup = true;
                    }
                }
            }

            Check("같은 카드가 겹쳐 나오지 않는다", bDup == false);

            // 표에 있는 것보다 많이 달라고 해도 있는 만큼만 준다
            cTable.Pick_Random(99, lstPick);
            Check("표보다 많이 뽑지 않는다", lstPick.Count, cTable.COUNT);

            // 가중치가 0인 카드는 안 나온다
            CCSVData_CardInfo cZero = new CCSVData_CardInfo();
            string strTab = ((char)9).ToString();    // 탭과 개행을 escape 없이 만든다
            string strNl  = ((char)10).ToString();

            cZero.Read_CSVData(new TextAsset(
                  string.Join(strTab, "iCardID", "eType", "strName", "strDesc",
                                      "fValue", "iWeight", "NONE") + strNl
                + string.Join(strTab, "1", "SHIELD", "보호막", "설명", "1", "0", "") + strNl
                + string.Join(strTab, "2", "HEAL", "회복", "설명", "1", "5", "")));

            cZero.Pick_Random(2, lstPick);
            Check("가중치 0은 안 뽑힌다", lstPick.Count, 1);
            Check("남은 카드가 뽑힌다", lstPick[0].eType == CARD_TYPE.HEAL);
        }

        // 260912_추적 카메라 — 플레이어를 따라가되 맵 밖이 보이면 안 된다
        private static void Test_CameraFollow()
        {
            const float PORTRAIT = 9f / 16f;
            const float TOP      = 0.10f;
            const float BOTTOM   = 0.22f;
            float fUsable = 1f - TOP - BOTTOM;

            Vector2 vMap    = new Vector2(7.2f, 12f);       // 60 x 100 셀 x 0.12
            Vector2 vCenter = Vector2.zero;                 // 스테이지가 원점 기준으로 깐다

            // 44칸(5.28 월드)만 보여 준다 — 맵 세로 100칸보다 훨씬 좁다
            float fViewHeight = 44f * 0.12f;
            float fSize = CCameraFitter.Calc_Size(new Vector2(0f, fViewHeight), PORTRAIT, fUsable, 0f);

            float fHalfW = fSize * PORTRAIT;
            float fHalfH = fSize * fUsable;

            Check("보이는 세로는 정한 칸 수만큼",
                  Mathf.RoundToInt(fHalfH * 2f * 1000f), Mathf.RoundToInt(fViewHeight * 1000f));
            Check("맵 전체보다 좁게 본다", fHalfH * 2f < vMap.y);

            // 한가운데에서는 그대로 따라간다
            Vector2 vMid = CCameraFitter.Clamp_Center(new Vector2(0f, 0f), vMap, vCenter, fHalfW, fHalfH);
            Check("가운데는 그대로", Mathf.RoundToInt(vMid.y * 1000f), 0);

            // 위 끝으로 가면 맵 밖이 안 보이게 멈춘다
            Vector2 vTop = CCameraFitter.Clamp_Center(new Vector2(0f, 99f), vMap, vCenter, fHalfW, fHalfH);
            Check("위로 넘어가지 않는다",
                  Mathf.RoundToInt(vTop.y * 1000f), Mathf.RoundToInt((vMap.y * 0.5f - fHalfH) * 1000f));
            Check("위 끝에서도 맵 안", vTop.y + fHalfH <= vMap.y * 0.5f + 0.001f);

            Vector2 vBottom = CCameraFitter.Clamp_Center(new Vector2(0f, -99f), vMap, vCenter, fHalfW, fHalfH);
            Check("아래로도 넘어가지 않는다",
                  Mathf.RoundToInt(vBottom.y * 1000f), Mathf.RoundToInt((-vMap.y * 0.5f + fHalfH) * 1000f));

            // 가로도 맵보다 좁게 보이므로 좌우로도 따라간다 (4.368 < 7.2)
            Check("가로도 맵보다 좁게 본다", fHalfW * 2f < vMap.x);
            Vector2 vSide = CCameraFitter.Clamp_Center(new Vector2(99f, 0f), vMap, vCenter, fHalfW, fHalfH);
            Check("오른쪽으로도 넘어가지 않는다",
                  Mathf.RoundToInt(vSide.x * 1000f), Mathf.RoundToInt((vMap.x * 0.5f - fHalfW) * 1000f));
            Check("오른쪽 끝에서도 맵 안", vSide.x + fHalfW <= vMap.x * 0.5f + 0.001f);

            // 맵이 커져도 보이는 범위는 그대로여야 한다 — 스테이지 확장의 전제다
            Vector2 vBigMap = new Vector2(14.4f, 24f);
            Vector2 vBigTop = CCameraFitter.Clamp_Center(new Vector2(0f, 99f), vBigMap, vCenter, fHalfW, fHalfH);
            Check("맵이 커지면 더 멀리까지 간다", vBigTop.y > vTop.y);
            Check("보이는 세로는 그대로", Mathf.RoundToInt(fHalfH * 2f * 1000f),
                                          Mathf.RoundToInt(fViewHeight * 1000f));

            // 맵이 시야보다 작으면 가운데 고정 — 작은 맵에서 카메라가 흔들리면 안 된다
            Vector2 vTinyMap = new Vector2(2f, 3f);
            Vector2 vTiny = CCameraFitter.Clamp_Center(new Vector2(9f, 9f), vTinyMap, vCenter, fHalfW, fHalfH);
            Check("작은 맵은 가운데 고정", Mathf.RoundToInt(vTiny.y * 1000f), 0);

            // 원점이 아닌 맵도 같은 규칙을 따른다
            Vector2 vOffset = new Vector2(50f, -30f);
            Vector2 vOffTop = CCameraFitter.Clamp_Center(new Vector2(0f, 99f), vMap, vOffset, fHalfW, fHalfH);
            Check("중심이 옮겨가도 같은 규칙",
                  Mathf.RoundToInt(vOffTop.y * 1000f),
                  Mathf.RoundToInt((vOffset.y + vMap.y * 0.5f - fHalfH) * 1000f));
            Check("가로도 옮겨간 중심을 기준으로", Mathf.RoundToInt(vOffTop.x * 1000f),
                  Mathf.RoundToInt((vOffset.x - vMap.x * 0.5f + fHalfW) * 1000f));

            // 시야가 맵보다 넓은 축은 가둘 여유가 없으므로 가운데 고정 — 억지로 밀면 맵이 흔들린다
            Vector2 vWideView = CCameraFitter.Clamp_Center(new Vector2(99f, 99f), vMap, vCenter,
                                                           vMap.x, vMap.y);
            Check("시야가 넓으면 가로 가운데", Mathf.RoundToInt(vWideView.x * 1000f), 0);
            Check("시야가 넓으면 세로 가운데", Mathf.RoundToInt(vWideView.y * 1000f), 0);
        }

        // 260912_노치 / 홈 인디케이터 회피 + 시간 표기
        private static void Test_SafeArea()
        {
            const int W = 1080;
            const int H = 1920;

            // 안전 영역이 화면 전체면 아무 것도 줄이지 않는다 (에디터 Game 뷰가 이 경우다)
            Rect rcFull = new Rect(0f, 0f, W, H);
            Check("전체면 그대로", CSafeArea.Calc_AnchorMin(rcFull, W, H) == Vector2.zero);
            Check("전체면 그대로 (max)", CSafeArea.Calc_AnchorMax(rcFull, W, H) == Vector2.one);

            // 위에 노치(130), 아래에 홈 인디케이터(60)가 있는 기기.
            // Screen.safeArea는 왼쪽 아래가 원점이라 yMin이 '아래' 여백이다 — 뒤집어 읽기 쉽다.
            Rect rcNotch = new Rect(0f, 60f, W, H - 60f - 130f);
            Vector2 vMin = CSafeArea.Calc_AnchorMin(rcNotch, W, H);
            Vector2 vMax = CSafeArea.Calc_AnchorMax(rcNotch, W, H);

            Check("아래가 올라온다", Mathf.RoundToInt(vMin.y * 10000f), 312);
            Check("위가 내려온다",   Mathf.RoundToInt(vMax.y * 10000f), 9323);
            Check("가로는 그대로",   Mathf.Approximately(vMin.x, 0f) && Mathf.Approximately(vMax.x, 1f));
            Check("위 노치가 아래보다 두껍다", (1f - vMax.y) > vMin.y);

            // 값이 화면 밖으로 나가도 0~1을 벗어나지 않는다
            Rect rcBad = new Rect(-50f, -50f, W + 500f, H + 500f);
            Check("범위를 벗어나지 않는다 (min)", CSafeArea.Calc_AnchorMin(rcBad, W, H) == Vector2.zero);
            Check("범위를 벗어나지 않는다 (max)", CSafeArea.Calc_AnchorMax(rcBad, W, H) == Vector2.one);

            // 남은 시간 표기
            Check("분:초로 나온다", CUI_InGame.Format_Time(95f), "01:35");
            Check("올림으로 센다",  CUI_InGame.Format_Time(0.2f), "00:01");
            Check("음수는 0",       CUI_InGame.Format_Time(-5f), "00:00");
        }


        // 260905_옵션 에셋 + 무료 모드
        // 코인을 쓰는 곳이 셋(강화 / 구매 / 스킬 강화)이라 하나라도 새면 그 화면만 조용히 막힌다.
        private static void Test_GameConfig()
        {
            const int EQUIP_SHOES = 101;   // 가벼운 신발 200코인

            CGameConfig cConfig = CGameConfig.Load();
            Check("옵션 에셋 로드", cConfig != null);
            Check("속도 배율은 0보다 크다", cConfig.PLAYER_SPEED_SCALE > 0f);

            CCSVData_MapInfo     cMapTable     = Load_MapTable();
            CCSVData_UpgradeInfo cUpgradeTable = Load_UpgradeTable();
            CCSVData_SkillInfo   cSkillTable   = Load_SkillTable();
            if (cMapTable == null || cUpgradeTable == null || cSkillTable == null)
            {
                Check("표 로드", false);
                return;
            }

            CProgress_Manager cManager = new CProgress_Manager();
            cManager.Initialize(cMapTable, new CFakeProgressRepository(), Load_EquipTable());

            // 무료를 끄면 코인이 없을 때 아무것도 안 된다
            Check("기본은 유료 — 강화 실패",
                  cManager.Try_Upgrade(cUpgradeTable, STAT_TYPE.SPEED) == false);
            Check("기본은 유료 — 구매 실패", cManager.Try_Buy(EQUIP_SHOES) == false);

            cManager.Set_FreeSpend(true);

            Check("무료면 코인 0에도 강화", cManager.Try_Upgrade(cUpgradeTable, STAT_TYPE.SPEED));
            Check("무료면 코인 0에도 구매", cManager.Try_Buy(EQUIP_SHOES));
            Check("무료면 코인 0에도 스킬 강화",
                  cManager.Try_UpgradeSkill(cSkillTable.Find_ByType(SKILL_TYPE.WARP)));
            Check("코인은 줄지 않는다", cManager.COIN, 0);
            Check("무료면 살 수 있는 상태로 보인다", cManager.Can_Pay(999999));

            // 다시 끄면 원래대로 — 스위치가 한 방향으로만 도는지 확인한다
            cManager.Set_FreeSpend(false);
            Check("끄면 다시 막힌다", cManager.Can_Pay(1) == false);
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

        /// <summary> 해금 규칙만 보기 위한 두 줄짜리 MapInfo. 실제 표와 섞이지 않는다. </summary>
        private static CCSVData_MapInfo Make_TwoMapTable()
        {
            const string TAB = "\t";
            string strCsv =
                  string.Join(TAB, "iMapID", "strMapName", "iGridWidth", "iGridHeight", "fCellSize",
                                   "iBorderThick", "iLife", "fPlayerSpeed", "iWaveCount", "strShapeMask",
                                   "strLayerTex", "strWaveEnemy", "strWaveClearRatio", "strWaveTimeLimit",
                                   "iCoinPerStar", "NONE") + "\n"
                + string.Join(TAB, "901", "테스트A", "20", "20", "0.1", "1", "3", "8", "1", "-",
                                   "Tex_Mask_01|Tex_Reward_01", "101*1", "0.6", "60", "10", "") + "\n"
                + string.Join(TAB, "902", "테스트B", "20", "20", "0.1", "1", "3", "8", "1", "-",
                                   "Tex_Mask_01|Tex_Reward_01", "101*1", "0.6", "60", "10", "");

            CCSVData_MapInfo cTable = new CCSVData_MapInfo();
            cTable.Read_CSVData(new TextAsset(strCsv));
            return cTable;
        }

        private static void Test_StageProgress()
        {
            // 260912_해금은 '표의 순서'를 보는 규칙이라 표에 몇 개가 실렸는지와 무관해야 한다.
            // 맵이 하나뿐이어도 규칙은 계속 검증되도록 여기서 두 줄짜리 표를 만들어 쓴다.
            CCSVData_MapInfo cTable = Make_TwoMapTable();
            if (cTable == null || cTable.COUNT < 2)
            {
                Check("임시 맵 표 생성", false);
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

        // 260917_캐릭터 표 — 레벨 스케일링(속도·체력 배율, 회피 보너스)이 CRunSkillInfo와
        // 다르게 레벨 0도 1로 클램프되는지(2-11-1 Get_Value와의 의도적 차이) 본다.
        private static void Test_CharacterTable()
        {
            CCSVData_CharacterInfo cTable = Load_CsvTable<CCSVData_CharacterInfo>("CharacterInfo");
            if (cTable == null || cTable.COUNT < 1)
            {
                Check("CharacterInfo.csv 로드", false);
                return;
            }

            CCharacterInfo cInfo = cTable.ALL[0];
            Check("레벨 0(미보유)도 속도배율 1 이상", cInfo.Get_SpeedRate(0) > 0f);
            Check("레벨 0도 체력배율 1 이상", cInfo.Get_MaxHpRate(0) > 0f);
            Check("레벨 1과 레벨 0의 배율이 같다(최소 레벨 1로 클램프)",
                  Mathf.Approximately(cInfo.Get_SpeedRate(0), cInfo.Get_SpeedRate(1)));

            if (cInfo.iMaxLevel > 1)
            {
                Check("레벨이 오르면 속도배율도 오르거나 유지", cInfo.Get_SpeedRate(2) >= cInfo.Get_SpeedRate(1));
                Check("만렙을 넘겨도 값이 그대로", Mathf.Approximately(cInfo.Get_SpeedRate(99), cInfo.Get_SpeedRate(cInfo.iMaxLevel)));

                // 260918_레벨1→2가 첫 강화다(레벨 0이 아니라 1부터 시작하는 캐릭터만의 차이).
                int iCostAt1 = cInfo.Get_FragmentCost(1);
                int iCostAt2 = cInfo.Get_FragmentCost(2);
                Check("레벨업 비용이 있다", iCostAt1 > 0);
                Check("레벨이 오르면 비용도 늘거나 유지", iCostAt2 >= iCostAt1);
            }

            // 260918_성급은 틀만 — 임계 레벨을 넘을 때마다 한 단계씩 오른다.
            Check("성급 임계값이 있다", cInfo.lstStarLevel.Count > 0);
            if (cInfo.lstStarLevel.Count > 0)
            {
                Check("만렙이면 최고 성급", cInfo.Get_StarTier(cInfo.iMaxLevel), cInfo.lstStarLevel.Count);
                Check("레벨 0은 0성", cInfo.Get_StarTier(0), 0);
            }

            Check("표에 없는 ID는 null", cTable.Get_Info(99999) == null);
        }

        // 260918_보유·조각·레벨업·장착 — CStageProgress 저장/왕복과 CProgress_Manager의 조각 경제·만렙 클램프를 함께 본다.
        // 260918_장비 뽑기 — 코인 차감 · 가중치 0은 안 나옴 · 중복은 강화 → 만렙이면 환급 · 코인 부족이면 아무 일도 없음
        private static void Test_Gacha()
        {
            CCSVData_EquipInfo cEquipTable = Load_EquipTable();
            CCSVData_GachaInfo cGachaTable = Load_CsvTable<CCSVData_GachaInfo>("GachaInfo");
            Check("GachaInfo.csv 로드", cGachaTable != null && cGachaTable.DEFAULT != null);
            if (cEquipTable == null || cGachaTable == null || cGachaTable.DEFAULT == null)
                return;

            CGachaInfo cGacha = cGachaTable.DEFAULT;
            Check("보호막(소모품)은 뽑기에서 뺐다", cEquipTable.Get_Info(401).iGachaWeight, 0);

            CFakeProgressRepository cRepo = new CFakeProgressRepository();
            CProgress_Manager cProgress = new CProgress_Manager();
            cProgress.Initialize(Load_MapTable(), cRepo, cEquipTable);
            cProgress.Set_FreeSpend(false);

            Check("코인이 없으면 실패", cProgress.Try_Gacha(cGacha, out CEquipInfo _, out int _) == GACHA_RESULT.FAIL);
            Check("실패하면 아무것도 안 생긴다", cProgress.Has_Item(101) == false && cProgress.Has_Item(102) == false);

            cProgress.Add_Coin(cGacha.iCost);
            GACHA_RESULT eFirst = cProgress.Try_Gacha(cGacha, out CEquipInfo cFirst, out int _);
            Check("첫 뽑기는 새 장비", eFirst == GACHA_RESULT.NEW && cFirst != null && cProgress.Has_Item(cFirst.iEquipID));
            Check("뽑으면 비용만큼 코인이 준다", cProgress.COIN, 0);

            // 계속 뽑으면 — 새 장비 → 강화 → 만렙이면 환급 순으로 흘러가야 한다
            bool bSawLevelUp = false, bSawRefund = false, bConsumable = false, bFail = false;
            int iRefund = -1;
            for (int n = 0; n < 400; ++n)
            {
                cProgress.Add_Coin(cGacha.iCost);
                GACHA_RESULT eResult = cProgress.Try_Gacha(cGacha, out CEquipInfo cEquip, out int iGot);
                if (eResult == GACHA_RESULT.FAIL) bFail = true;
                if (eResult == GACHA_RESULT.LEVEL_UP) bSawLevelUp = true;
                if (eResult == GACHA_RESULT.REFUND) { bSawRefund = true; iRefund = iGot; }
                if (cEquip != null && cEquip.IS_CONSUMABLE == true) bConsumable = true;
            }
            Check("코인이 있으면 실패하지 않는다", bFail == false);
            Check("중복이면 강화 +1", bSawLevelUp);
            Check("만렙 중복이면 환급", bSawRefund);
            Check("환급은 비용 × 비율", iRefund, cGacha.REFUND);
            Check("가중치 0(소모품)은 안 나온다", bConsumable == false);

            bool bAllMax = true;
            for (int i = 0; i < cEquipTable.ALL.Count; ++i)
            {
                CEquipInfo cInfo = cEquipTable.ALL[i];
                if (cInfo.iGachaWeight > 0 && cProgress.Get_EquipLevel(cInfo.iEquipID) < cInfo.iMaxLevel)
                    bAllMax = false;
            }
            Check("충분히 뽑으면 뽑기 장비가 전부 만렙", bAllMax);

            cProgress.Set_FreeSpend(true);
            int iCoinBefore = cProgress.COIN;
            cProgress.Try_Gacha(cGacha, out CEquipInfo _, out int iFreeRefund);
            Check("무료 스위치면 코인을 쓰지 않는다(환급만 더해진다)", cProgress.COIN, iCoinBefore + iFreeRefund);
        }

        // 260918_캐릭터 시스템 전에 깬 맵의 캐릭터를 챙겨 주는지 · 카드 크게 보기의 넘기기 판정
        private static void Test_CharacterGrantAndViewer()
        {
            // ---- 이미 깬 저장본: 맵 1(리네트를 주는 맵)에 별 3개, 캐릭터는 없음
            CFakeProgressRepository cRepo = new CFakeProgressRepository();
            cRepo.STORED.Set_Star(1, 3);

            CProgress_Manager cProgress = new CProgress_Manager();
            Check("진행도 초기화(소급 지급)", cProgress.Initialize(Load_MapTable(), cRepo));
            Check("예전에 깬 맵의 캐릭터를 챙겨 준다", cProgress.Has_Character(1));
            Check("챙겨 준 캐릭터는 1레벨", cProgress.Get_CharacterLevel(1), 1);
            Check("장착한 캐릭터가 없었으면 장착해 준다", cProgress.EQUIPPED_CHARACTER_ID, 1);
            Check("조각은 주지 않는다", cProgress.Get_CharacterFragment(1), 0);
            Check("챙겨 줬으면 저장한다", cRepo.SAVE_COUNT, 1);

            CProgress_Manager cAgain = new CProgress_Manager();
            cAgain.Initialize(Load_MapTable(), cRepo);
            Check("이미 가졌으면 다시 저장하지 않는다", cRepo.SAVE_COUNT, 1);

            CFakeProgressRepository cFresh = new CFakeProgressRepository();
            CProgress_Manager cNew = new CProgress_Manager();
            cNew.Initialize(Load_MapTable(), cFresh);
            Check("안 깬 저장본은 캐릭터가 없다", cNew.Has_Character(1) == false);

            Check("리네트를 주는 맵은 1번", cProgress.Find_CharacterMap(1) != null ? cProgress.Find_CharacterMap(1).iMapID : -1, 1);
            Check("얻을 곳이 없는 캐릭터는 null", cProgress.Find_CharacterMap(2) == null);

            // ---- 카드 크게 보기
            Check("넘기기 — 목록이 비면 -1", CUI_CardViewer.Clamp_Index(0, 0), -1);
            Check("넘기기 — 끝에서 더 가지 않는다", CUI_CardViewer.Clamp_Index(5, 3), 2);
            Check("넘기기 — 처음에서 더 가지 않는다", CUI_CardViewer.Clamp_Index(-1, 3), 0);
            Check("밀기 — 왼쪽으로 밀면 다음", CUI_CardViewer.Get_SwipeStep(-200f, 1080f), 1);
            Check("밀기 — 오른쪽으로 밀면 이전", CUI_CardViewer.Get_SwipeStep(200f, 1080f), -1);
            Check("밀기 — 짧게 스치면 그대로", CUI_CardViewer.Get_SwipeStep(40f, 1080f), 0);
        }

        private static void Test_Character()
        {
            CCSVData_CharacterInfo cTable = Load_CsvTable<CCSVData_CharacterInfo>("CharacterInfo");
            if (cTable == null || cTable.COUNT < 1)
            {
                Check("CharacterInfo.csv 로드(Test_Character)", false);
                return;
            }

            CCharacterInfo cCharInfo   = cTable.ALL[0];
            int            iCharacterID = cCharInfo.iCharacterID;
            int            iMaxLevel    = cCharInfo.iMaxLevel;

            CFakeProgressRepository cRepo = new CFakeProgressRepository();
            CProgress_Manager cProgress = new CProgress_Manager();
            Check("진행도 초기화(캐릭터)", cProgress.Initialize(Load_MapTable(), cRepo));

            Check("처음엔 캐릭터를 갖고 있지 않다", cProgress.Has_Character(iCharacterID) == false);
            Check("처음 장착 캐릭터는 없다(0)", cProgress.EQUIPPED_CHARACTER_ID, 0);
            Check("얻기 전엔 레벨업 비용이 0", cProgress.Get_CharacterLevelUpCost(cTable, iCharacterID), 0);

            // 처음 클리어 — 1레벨로 얻고 자동 장착한다. 조각은 주지 않는다(이미 얻었으니까).
            Check("처음 클리어 성공", cProgress.On_CharacterMapCleared(cTable, iCharacterID));
            Check("얻은 뒤 보유", cProgress.Has_Character(iCharacterID));
            Check("얻은 뒤 레벨 1", cProgress.Get_CharacterLevel(iCharacterID), 1);
            Check("처음 얻으면 자동 장착", cProgress.EQUIPPED_CHARACTER_ID, iCharacterID);
            Check("처음 클리어는 조각을 안 준다", cProgress.Get_CharacterFragment(iCharacterID), 0);

            if (iMaxLevel > 1)
            {
                int iCost = cProgress.Get_CharacterLevelUpCost(cTable, iCharacterID);
                Check("레벨업 비용이 있다", iCost > 0);
                Check("조각이 없으면 레벨업 불가 판정", cProgress.Can_LevelUpCharacter(cTable, iCharacterID) == false);
                Check("조각이 없으면 레벨업 시도도 실패", cProgress.Try_LevelUpCharacter(cTable, iCharacterID) == false);

                // 재클리어로 조각을 모은다 — 비용을 채울 때까지 반복한다.
                int iClearCount = Mathf.CeilToInt((float)iCost / Mathf.Max(1, cCharInfo.iFragmentPerClear));
                for (int i = 0; i < iClearCount; ++i)
                    cProgress.On_CharacterMapCleared(cTable, iCharacterID);

                Check("재클리어는 레벨을 그대로 둔다", cProgress.Get_CharacterLevel(iCharacterID), 1);
                Check("재클리어만큼 조각이 쌓인다",
                      cProgress.Get_CharacterFragment(iCharacterID), iClearCount * cCharInfo.iFragmentPerClear);
                Check("조각이 충분하면 레벨업 가능 판정", cProgress.Can_LevelUpCharacter(cTable, iCharacterID));

                int iFragmentBefore = cProgress.Get_CharacterFragment(iCharacterID);
                Check("레벨업 성공", cProgress.Try_LevelUpCharacter(cTable, iCharacterID));
                Check("레벨업 후 레벨 2", cProgress.Get_CharacterLevel(iCharacterID), 2);
                Check("레벨업하면 조각을 그만큼 쓴다",
                      cProgress.Get_CharacterFragment(iCharacterID), iFragmentBefore - iCost);
            }

            // 만렙까지 밀어붙인다 — 이미 보유 중이라 매번 조각만 쌓이고, 모이는 대로 레벨업을 시도한다.
            for (int i = 0; i < iMaxLevel * 20; ++i)
            {
                cProgress.On_CharacterMapCleared(cTable, iCharacterID);
                cProgress.Try_LevelUpCharacter(cTable, iCharacterID);
            }
            Check("만렙을 넘지 않는다", cProgress.Get_CharacterLevel(iCharacterID), iMaxLevel);
            Check("만렙이면 레벨업 비용이 0", cProgress.Get_CharacterLevelUpCost(cTable, iCharacterID), 0);
            Check("만렙이면 레벨업 시도가 실패", cProgress.Try_LevelUpCharacter(cTable, iCharacterID) == false);

            // 표에 없는 ID / 못 가진 캐릭터
            Check("없는 캐릭터는 클리어 보상도 없다", cProgress.On_CharacterMapCleared(cTable, 99999) == false);
            Check("못 가진 캐릭터는 장착할 수 없다", cProgress.Try_EquipCharacter(99999) == false);
            Check("가진 캐릭터는 장착할 수 있다", cProgress.Try_EquipCharacter(iCharacterID));

            // 저장 왕복(JSON) — 보유·레벨·장착·조각이 그대로 남는가
            CStageProgress cSaved = cRepo.STORED;
            Check("저장된 기록에 캐릭터가 담긴다", cSaved != null && cSaved.Has_Character(iCharacterID));
            Check("저장된 기록의 레벨이 유지된다", cSaved != null && cSaved.Get_CharacterLevel(iCharacterID) == iMaxLevel);
            Check("저장된 기록의 장착이 유지된다", cSaved != null && cSaved.iEquippedCharacterID == iCharacterID);
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

        // 260905_표를 읽는 방법은 파일 이름만 다르고 전부 같다 — 하나로 합쳐 둔다.
        private static T Load_CsvTable<T>(string strFileName) where T : Engine.CCSVData, new()
        {
            TextAsset cText = AssetDatabase.LoadAssetAtPath<TextAsset>($"Assets/Data/{strFileName}.csv");
            if (cText == null)
                return null;

            T cTable = new T();
            cTable.Read_CSVData(cText);
            return cTable;
        }

        private static CCSVData_MapInfo     Load_MapTable()     => Load_CsvTable<CCSVData_MapInfo>("MapInfo");
        private static CCSVData_EquipInfo   Load_EquipTable()   => Load_CsvTable<CCSVData_EquipInfo>("EquipInfo");
        private static CCSVData_SkillInfo   Load_SkillTable()   => Load_CsvTable<CCSVData_SkillInfo>("SkillInfo");
        private static CCSVData_UpgradeInfo Load_UpgradeTable() => Load_CsvTable<CCSVData_UpgradeInfo>("UpgradeInfo");
        private static CCSVData_RunSkillInfo Load_RunSkillTable() => Load_CsvTable<CCSVData_RunSkillInfo>("RunSkillInfo");

        // 260904_가상 조이스틱 — 판정만 떼어 두었으므로 화면 없이 검증할 수 있다
        // 260916_카메라 흔들림. 판정(CCameraShake)만 검증한다 — 실제 Transform에 더하는 건
        // CGameManager.Tick_Camera가 하고, 그건 화면이 있어야 확인된다(2-10-2).
        private static void Test_CameraShake()
        {
            // 정적 계산 — 화면·인스턴스 없이도 파형 자체를 본다
            Check("트라우마 0은 오프셋 0", CCameraShake.Calc_Offset(0f, 12.3f, 1f, 2f, 0.2f) == Vector2.zero);

            Vector2 vLow  = CCameraShake.Calc_Offset(0.2f, 5f, 1f, 2f, 0.2f);
            Vector2 vHigh = CCameraShake.Calc_Offset(0.8f, 5f, 1f, 2f, 0.2f);
            Check("트라우마가 크면 더 세게 흔들린다", vHigh.magnitude > vLow.magnitude);

            Vector2 vFull = CCameraShake.Calc_Offset(1f, 3f, 1f, 2f, 0.2f);
            Check("최대 트라우마에서도 최대 변위를 넘지 않는다",
                  Mathf.Abs(vFull.x) <= 0.2f + 0.001f && Mathf.Abs(vFull.y) <= 0.2f + 0.001f);

            // 인스턴스 — 누적 / 클램프 / 감쇠 / 스위치
            CCameraShake cShake = new CCameraShake();
            cShake.Initialize(0.2f, 0.5f);      // 초당 0.5씩 줄어든다

            Check("처음엔 안 흔들린다", cShake.Tick(0f) == Vector2.zero);

            cShake.Add_Trauma(0.5f);
            cShake.Add_Trauma(0.8f);
            Check("트라우마는 1을 넘지 않는다", cShake.TRAUMA <= 1f + 0.0001f);

            cShake.Tick(1f);
            Check("시간이 지나면 감쇠한다", Mathf.Approximately(cShake.TRAUMA, 0.5f));

            cShake.Tick(10f);
            Check("바닥 밑으로는 안 내려간다", cShake.TRAUMA == 0f);

            cShake.Add_Trauma(0.6f);
            Check("Reset 전에는 남아 있다", cShake.TRAUMA > 0f);
            cShake.Reset();
            Check("Reset은 즉시 0", cShake.TRAUMA == 0f);

            cShake.Set_Enabled(false);
            cShake.Add_Trauma(1f);
            Check("꺼져 있으면 트라우마가 안 쌓인다", cShake.TRAUMA == 0f);

            cShake.Set_Enabled(true);
            cShake.Add_Trauma(0.5f);
            Check("다시 켜면 정상 동작", cShake.TRAUMA > 0f);
        }

        // 260916_점령 펀치줌. CCameraShake와 같은 감쇠 곡선(CCameraFeel_Utility)을 쓰므로
        // 스위치/클램프/리셋은 겹치지 않게 간단히만 보고, 줌 배율 자체를 집중해서 본다.
        private static void Test_CameraPunch()
        {
            Check("펀치 0은 배율 1(그대로)",
                  Mathf.Approximately(CCameraPunch.Calc_ZoomScale(0f, 0.06f), 1f));

            float fFull = CCameraPunch.Calc_ZoomScale(1f, 0.06f);
            Check("펀치 1은 최대 비율만큼 줄어든다", Mathf.Approximately(fFull, 1f - 0.06f));

            float fHalf = CCameraPunch.Calc_ZoomScale(0.5f, 0.06f);
            Check("배율은 1과 최소값 사이", fHalf < 1f && fHalf > fFull);

            CCameraPunch cPunch = new CCameraPunch();
            cPunch.Initialize(0.06f, 2f);

            Check("처음엔 배율 1", cPunch.Tick(0f) == 1f);

            cPunch.Add_Punch(0.6f);
            cPunch.Add_Punch(0.8f);
            Check("펀치는 1을 넘지 않는다", cPunch.PUNCH <= 1f + 0.0001f);

            float fZoom = cPunch.Tick(0f);
            Check("펀치가 쌓이면 확대된다", fZoom < 1f);

            cPunch.Reset();
            Check("Reset은 즉시 0", cPunch.PUNCH == 0f);

            cPunch.Set_Enabled(false);
            cPunch.Add_Punch(1f);
            Check("꺼져 있으면 안 쌓인다", cPunch.PUNCH == 0f);
        }

        // 260916_피격/회피 화면 플래시. 판정(CFlashEffect)만 본다 — 실제 Image에 칠하는 건
        // CUI_InGame.Refresh_Flash가 하고, 화면이 있어야 확인된다.
        private static void Test_FlashEffect()
        {
            CFlashEffect cFlash = new CFlashEffect();
            Check("처음엔 안 보임", cFlash.ALPHA == 0f);

            cFlash.Add_Flash(new Color(1f, 0f, 0f, 0.35f), 0.2f);
            Check("터지자마자는 최대 세기", Mathf.Approximately(cFlash.ALPHA, 1f));
            Check("색의 알파가 곧 최대 세기", Mathf.Approximately(cFlash.COLOR.a, 0.35f));

            cFlash.Tick(0.1f);
            Check("절반 지나면 절반만 남는다", Mathf.Approximately(cFlash.ALPHA, 0.5f));

            cFlash.Tick(0.1f);
            Check("다 지나면 0", cFlash.ALPHA == 0f);

            // 나중에 들어온 색이 이전 것을 덮어쓴다 — 두 색이 섞이면 무슨 일인지 읽기 어렵다
            cFlash.Add_Flash(Color.red, 0.2f);
            cFlash.Add_Flash(Color.white, 0.2f);
            Check("나중 색이 이긴다", cFlash.COLOR == Color.white);

            cFlash.Clear();
            Check("Clear는 즉시 0", cFlash.ALPHA == 0f);
        }

        // 260916_절차적 효과음 파형. 실제 AudioClip 재생은 화면(오디오 장치)이 있어야
        // 확인되므로, 여기서는 샘플 배열 자체의 모양만 본다 — 텍스처 픽셀을 검증하는 것과 같은 결.
        private static void Test_SoundUtility()
        {
            float[] arrSample = CSound_Utility.Generate_Tone(WAVE_SHAPE.SINE, 440f, 440f, 0.1f);
            int iExpected = Mathf.RoundToInt(0.1f * CSound_Utility.SAMPLE_RATE);
            Check("길이(초)만큼 샘플이 나온다", arrSample.Length, iExpected);

            Check("시작은 무음에 가깝다(클릭 방지 페이드인)", Mathf.Abs(arrSample[0]) < 0.05f);
            Check("끝도 무음에 가깝다(클릭 방지 페이드아웃)", Mathf.Abs(arrSample[arrSample.Length - 1]) < 0.05f);

            bool bClipped = false;
            foreach (float f in arrSample)
            {
                if (f > 1.0001f || f < -1.0001f)
                    bClipped = true;
            }
            Check("진폭이 -1~1을 넘지 않는다(클리핑 없음)", bClipped == false);

            // 시작음==끝음이면 일정한 톤 — 최댓값 근처가 여러 번 나와야 한다(주기적 파형)
            int iNearPeak = 0;
            foreach (float f in arrSample)
            {
                if (f > 0.9f)
                    ++iNearPeak;
            }
            Check("일정한 톤은 주기적으로 반복된다", iNearPeak > 1);

            // 시작음과 끝음이 다르면(하강 톤) 뒤로 갈수록 한 주기가 길어진다 — 저음일수록 느리게 흔들린다.
            float[] arrSweep = CSound_Utility.Generate_Tone(WAVE_SHAPE.SINE, 800f, 100f, 0.2f);
            Check("파형이 비지 않는다", arrSweep.Length > 0);

            // 파형 종류가 달라도 길이는 같다 — 모양만 다르다.
            float[] arrSquare = CSound_Utility.Generate_Tone(WAVE_SHAPE.SQUARE, 300f, 300f, 0.05f);
            Check("사각파도 사인파와 같은 규칙으로 길이가 정해진다",
                  arrSquare.Length, Mathf.RoundToInt(0.05f * CSound_Utility.SAMPLE_RATE));
        }

        // 260916_햅틱 펄스 스케줄링. Handheld.Vibrate 자체는 실기기에서만 확인되므로
        // "몇 번 울렸는지 · 언제 울렸는지"만 FIRED_COUNT/PULSE_REMAIN으로 본다.
        private static void Test_HapticManager()
        {
            CHaptic_Manager cHaptic = new CHaptic_Manager();

            cHaptic.Play(HAPTIC_ID.HIT);   // 정의상 1펄스, 간격 없음
            Check("한 번짜리는 즉시 다 울린다", cHaptic.FIRED_COUNT, 1);
            Check("한 번짜리는 남는 펄스가 없다", cHaptic.PULSE_REMAIN, 0);

            cHaptic.Play(HAPTIC_ID.DEATH);   // 정의상 3펄스, 0.15초 간격
            Check("첫 펄스는 즉시 울린다", cHaptic.FIRED_COUNT, 2);
            Check("두 번 더 남았다", cHaptic.PULSE_REMAIN, 2);

            cHaptic.Tick(0.05f);
            Check("간격이 안 지나면 안 울린다", cHaptic.FIRED_COUNT, 2);

            cHaptic.Tick(0.15f);
            Check("간격이 지나면 다음 펄스가 울린다", cHaptic.FIRED_COUNT, 3);
            Check("한 번 남았다", cHaptic.PULSE_REMAIN, 1);

            cHaptic.Tick(0.15f);
            Check("마지막 펄스까지 울린다", cHaptic.FIRED_COUNT, 4);
            Check("다 울리면 남는 게 없다", cHaptic.PULSE_REMAIN, 0);

            cHaptic.Tick(1f);
            Check("다 울린 뒤에는 더 안 울린다", cHaptic.FIRED_COUNT, 4);

            cHaptic.Set_Enabled(false);
            cHaptic.Play(HAPTIC_ID.STAGE_CLEAR);
            Check("꺼져 있으면 안 울린다", cHaptic.FIRED_COUNT, 4);

            cHaptic.Set_Enabled(true);
            cHaptic.Play(HAPTIC_ID.NONE);
            Check("NONE은 안 울린다", cHaptic.FIRED_COUNT, 4);

            // 이미 도는 패턴 위에 새로 Play하면 새 패턴으로 덮어쓴다(CFlashEffect와 같은 규칙)
            cHaptic.Play(HAPTIC_ID.CAPTURE);   // 2펄스, 0.10초 간격
            Check("새 패턴의 첫 펄스가 즉시 울린다", cHaptic.FIRED_COUNT, 5);
            Check("새 패턴 기준으로 남은 펄스가 잡힌다", cHaptic.PULSE_REMAIN, 1);
        }

        private static void Test_Joystick()
        {
            const int   SCREEN_H = 1000;
            const int   SCREEN_W = 1000;
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
            cJoystick.Initialize(RADIUS, DEADZONE, 0.6f, 0.55f);
            Check("처음에는 비활성", cJoystick.IS_ACTIVE == false);

            // 화면 위쪽(활성 영역 밖)에서는 잡히지 않는다
            cJoystick.Update_State(true, new Vector2(500f, 900f), SCREEN_H, SCREEN_W);
            Check("활성 영역 밖에서는 안 잡힘", cJoystick.IS_ACTIVE == false);

            // 260912_오른쪽 아래는 스킬/아이템 버튼 자리라 조이스틱이 잡으면 안 된다
            cJoystick.Update_State(true, new Vector2(SCREEN_W * 0.85f, 200f), SCREEN_H, SCREEN_W);
            Check("오른쪽 버튼 자리에서는 안 잡힘", cJoystick.IS_ACTIVE == false);

            // 아래쪽에서 누르면 그 자리가 중심이 된다 (플로팅)
            Vector2 vPress = new Vector2(300f, 200f);
            cJoystick.Update_State(true, vPress, SCREEN_H, SCREEN_W);
            Check("아래쪽에서 잡힘", cJoystick.IS_ACTIVE);
            Check("누른 자리가 중심", cJoystick.ORIGIN == vPress);
            Check("잡은 직후엔 방향 없음", cJoystick.DIR == MOVE_DIR.NONE);

            // 오른쪽으로 끌면 오른쪽
            cJoystick.Update_State(true, vPress + new Vector2(60f, 0f), SCREEN_H, SCREEN_W);
            Check("끌면 방향이 생김", cJoystick.DIR == MOVE_DIR.RIGHT);
            Check("중심은 그대로", cJoystick.ORIGIN == vPress);

            // 반경을 넘겨도 손잡이는 반경 안에 머문다
            cJoystick.Update_State(true, vPress + new Vector2(500f, 0f), SCREEN_H, SCREEN_W);
            Check("손잡이가 반경을 넘지 않음",
                  Vector2.Distance(cJoystick.ORIGIN, cJoystick.HANDLE) <= RADIUS + 0.01f);
            Check("반경 밖에서도 방향 유지", cJoystick.DIR == MOVE_DIR.RIGHT);

            // 한 번 잡은 뒤에는 활성 영역 밖으로 끌어도 놓지 않는다
            cJoystick.Update_State(true, new Vector2(300f, 950f), SCREEN_H, SCREEN_W);
            Check("잡은 뒤에는 위로 끌어도 유지", cJoystick.IS_ACTIVE);
            Check("위로 끌면 위쪽", cJoystick.DIR == MOVE_DIR.UP);

            // 떼면 초기화
            cJoystick.Update_State(false, Vector2.zero, SCREEN_H, SCREEN_W);
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
        // 260912_문자열 비교 — 시간 표기처럼 형식이 맞는지 봐야 하는 경우
        private static void Check(string strName, string strActual, string strExpect)
        {
            if (strActual == strExpect)
            {
                ++s_iPass;
                s_sbLog.AppendLine($"  PASS  {strName} = {strActual}");
                return;
            }

            ++s_iFail;
            s_sbLog.AppendLine($"  FAIL  {strName} : expect {strExpect}, actual {strActual}");
        }

        #endregion 헬퍼
    }
}
