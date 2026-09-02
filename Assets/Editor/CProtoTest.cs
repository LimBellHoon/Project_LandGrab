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

            // 오른쪽은 점령지 내부 → 가로지를 수 없다
            cMove.Tick(1f, MOVE_DIR.RIGHT, out Vector2Int _);
            Check("점령지 내부로 진입 차단", cMove.CUR_CELL == new Vector2Int(3, 0));

            // 왼쪽은 아직 미점령 지대와 맞닿은 '선' → 이동 가능
            cMove.Tick(1f, MOVE_DIR.LEFT, out Vector2Int _);
            Check("경계 위로는 이동 가능", cMove.CUR_CELL == new Vector2Int(2, 0));

            // 선 위에서 미점령 지대로 나가는 것은 여전히 가능해야 한다
            Walk(cGrid, cMove, MOVE_DIR.UP, 2, null);
            Check("선에서 미점령 지대로 진입 가능", cMove.CUR_CELL == new Vector2Int(2, 2));
            Check("나가면 다시 선을 그린다", cGrid.IS_DRAWING);
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
            CStageDesc cStageDesc = new CStageDesc();

            CTerritoryGrid cGrid = new CTerritoryGrid();
            cGrid.Initialize(cStageDesc.iGridWidth, cStageDesc.iGridHeight, cStageDesc.fCellSize,
                             Vector2.zero, cStageDesc.iBorderThick);

            CMoveHandler cMove = new CMoveHandler();
            cMove.Initialize(cGrid, new Vector2Int(cStageDesc.iGridWidth / 2, cStageDesc.iBorderThick - 1), STEP_SPEED);

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
            CGridRenderer cRenderer = new CGridRenderer();
            cRenderer.Initialize(cGrid, goOverlay.AddComponent<SpriteRenderer>());

            Texture2D texMask = goOverlay.GetComponent<SpriteRenderer>().sprite.texture;
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
