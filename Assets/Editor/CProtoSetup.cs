using System.IO;

using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 씬/프리팹/Addressable 자동 구성
    /// <summary>
    /// 프로토타입에 필요한 에셋과 씬을 코드로 만든다.
    /// 배치모드에서도 실행 가능: -executeMethod Client.CProtoSetup.Setup_All
    /// </summary>
    public static class CProtoSetup
    {
        private const string DIR_ART        = "Assets/Art";
        private const string DIR_PREFAB     = "Assets/Prefabs";
        private const string DIR_SCENE      = "Assets/Scenes";
        private const string DIR_DATA       = "Assets/Data";

        private const string PATH_TEX_PLAYER = DIR_ART + "/Tex_PlayerBody.png";
        private const string PATH_TEX_ENEMY  = DIR_ART + "/Tex_EnemyBody.png";
        private const string PATH_TEX_BG     = DIR_ART + "/Tex_Reward_Placeholder.png";

        // 260904_MapInfo.csv의 strLayerTex가 가리키는 이미지 스택.
        // [0]=마스크(1웨이브 가림막) → [1][2][3]=웨이브별 보상. 이름이 CSV와 어긋나면 스테이지가 못 뜬다.
        private static readonly string[] ARR_LAYER_TEX =
        {
            "Tex_Mask_01", "Tex_Reward_01", "Tex_Reward_02", "Tex_Reward_03",
        };
        private const string TEX_SHAPE_02 = "Tex_Shape_02";      // MapInfo.csv 2번 맵의 모양 마스크

        // 260904_CSV 테이블. 파일명이 곧 Client.CCSVData_<파일명> 클래스 이름이다.
        private static readonly string[] ARR_CSV = { "EnemyInfo", "MapInfo" };
        // Type.GetType은 부르는 어셈블리(에디터)만 뒤지므로 런타임 클래스를 못 찾는다.
        // 컴파일 시점에 확정되는 typeof로 들고 있어야 이름 규칙을 제대로 검증할 수 있다.
        private static readonly System.Type[] ARR_CSV_TYPE =
        {
            typeof(CCSVData_EnemyInfo), typeof(CCSVData_MapInfo),
        };

        private const int DEFAULT_MAP_ID = 1;       // 씬/프리뷰가 기준으로 삼는 맵
        private const string PATH_PREFAB     = DIR_PREFAB + "/Prefab_Player.prefab";
        private const string PATH_PREFAB_ENEMY = DIR_PREFAB + "/Prefab_Enemy.prefab";
        // 260904_기믹 소환물
        private const string PATH_TEX_PROJECTILE  = DIR_ART + "/Tex_Projectile.png";
        private const string PATH_TEX_WEB         = DIR_ART + "/Tex_Web.png";
        private const string PATH_PREFAB_PROJECTILE = DIR_PREFAB + "/Prefab_Projectile.prefab";
        private const string PATH_PREFAB_WEB        = DIR_PREFAB + "/Prefab_Web.prefab";
        // 260904_스테이지 선택 UI
        // 260904_UI 프리팹 이름은 Engine이 강제한다.
        // Engine.CUI_Manager.Open<T>가 Desc의 strPrefabName을
        //     "Prefab_" + Engine_Utility.Convert_TypeToString<T>()   (typeof(T).Name에서 앞 'C' 제거)
        // 로 덮어쓰고, 그 이름으로 프리팹을 찾는다.
        // 그래서 Addressable 주소가 반드시 "Prefab_UI_StageSelect" 꼴이어야 한다.
        private const string PATH_PREFAB_UI_SELECT  = DIR_PREFAB + "/Prefab_UI_StageSelect.prefab";
        private const string UI_STAGE_SELECT        = "Prefab_UI_StageSelect";
        // 260904_인게임 HUD (가상 조이스틱)
        private const string PATH_PREFAB_UI_INGAME  = DIR_PREFAB + "/Prefab_UI_InGame.prefab";
        private const string UI_INGAME              = "Prefab_UI_InGame";
        private const string PATH_PREFAB_UI_POPUP   = DIR_PREFAB + "/Prefab_UI_Popup.prefab";
        private const string UI_POPUP               = "Prefab_UI_Popup";
        private const string PATH_TEX_JOY_BASE      = DIR_ART + "/Tex_JoystickBase.png";
        private const string PATH_TEX_JOY_HANDLE    = DIR_ART + "/Tex_JoystickHandle.png";
        private const string PATH_SCENE      = DIR_SCENE + "/LV_Proto.unity";

        [MenuItem("Tools/LandGrab/Setup Prototype (씬까지 새로 만듦)")]
        public static void Setup_All()
        {
            Setup_Assets();
            Build_Scene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CProtoSetup] 프로토타입 셋업 완료 — Assets/Scenes/LV_Proto.unity 를 열고 Play 하세요.");
        }

        // 260902_씬을 갈아엎지 않는 안전한 메뉴 — 작업 중인 씬이 열려 있어도 쓸 수 있다.
        [MenuItem("Tools/LandGrab/Setup Assets (씬 건드리지 않음)")]
        public static void Setup_Assets()
        {
            Ensure_Folder(DIR_ART);
            Ensure_Folder(DIR_PREFAB);
            Ensure_Folder(DIR_SCENE);
            Ensure_Folder(DIR_DATA);

            Delete_LegacyUIPrefabs();
            Create_Sprites();
            Create_Prefabs();
            Setup_Addressables();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CProtoSetup] 에셋 셋업 완료 (스프라이트 / 프리팹 / Addressable)");
        }

        // 260904_UI 프리팹 이름을 Engine 규칙("Prefab_" + T)에 맞추면서 옛 이름의 산출물이 남는다.
        // 주소가 달라 로드되지도 않는데 Addressable 목록에는 남아 헷갈리므로 여기서 지운다.
        private static readonly string[] ARR_LEGACY_UI_PREFAB =
        {
            DIR_PREFAB + "/UI_StageSelect.prefab",
            DIR_PREFAB + "/UI_InGame.prefab",
            DIR_PREFAB + "/UI_Popup.prefab",
        };

        private static void Delete_LegacyUIPrefabs()
        {
            foreach (string strPath in ARR_LEGACY_UI_PREFAB)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(strPath) == null)
                    continue;

                AssetDatabase.DeleteAsset(strPath);
                Debug.Log($"[CProtoSetup] 옛 이름의 UI 프리팹을 정리했습니다 — {strPath}");
            }
        }

        // 260903_에셋이 실제로 로드되는지 확인 (Play 전에 프리팹 누락을 잡는 용도)
        [MenuItem("Tools/LandGrab/Validate Assets")]
        public static void Validate_Assets()
        {
            int iFail = 0;

            iFail += Validate_ActorPrefab(PATH_PREFAB, "Prefab_Player", typeof(CPlayer));
            iFail += Validate_ActorPrefab(PATH_PREFAB_ENEMY, "Prefab_Enemy", typeof(CEnemy));
            iFail += Validate_ActorPrefab(PATH_PREFAB_PROJECTILE, "Prefab_Projectile", typeof(CProjectile));
            iFail += Validate_ActorPrefab(PATH_PREFAB_WEB, "Prefab_Web", typeof(CWeb));
            iFail += Validate_StageSelectUI();
            iFail += Validate_UIPrefab<CUI_InGame>(PATH_PREFAB_UI_INGAME, UI_INGAME,
                        new[] { "m_trJoystickBase", "m_trJoystickHandle", "m_txtStatus", "m_btnPause" });
            iFail += Validate_UIPrefab<CUI_Popup>(PATH_PREFAB_UI_POPUP, UI_POPUP,
                        new[] { "m_txtTitle", "m_txtBody", "m_btnPrimary", "m_btnSecondary" });

            // 260904_CSV 테이블과 웨이브 이미지가 빠지면 스테이지가 통째로 안 뜬다.
            iFail += Validate_CsvTables();
            iFail += Validate_LayerTextures();

            if (iFail == 0)
                Debug.Log("[CProtoSetup] 에셋 검증 통과 — 프리팹 / 스프라이트 / CSV / Addressable 정상");
            else
                Debug.LogError($"[CProtoSetup] 에셋 검증 실패 {iFail}건 — Tools/LandGrab/Setup Assets 를 실행하세요.");

            if (Application.isBatchMode == true)
                EditorApplication.Exit(iFail == 0 ? 0 : 1);
        }

        // 260904_CSV는 파일 존재 · Addressable 라벨 · 짝이 되는 파싱 클래스까지 셋 다 봐야 한다.
        // 셋 중 하나만 어긋나도 Engine은 조용히 경고만 남기고 표를 비운 채 넘어간다.
        // 260904_UI 프리팹은 m_srBody가 없으므로 Validate_ActorPrefab을 쓸 수 없다. 따로 본다.
        private static int Validate_StageSelectUI()
        {
            return Validate_UIPrefab<CUI_StageSelect>(PATH_PREFAB_UI_SELECT, UI_STAGE_SELECT,
                        new[] { "m_trContent", "m_btnTemplate", "m_txtTitle" });
        }

        /// <summary> UI 프리팹의 컴포넌트 · [SerializeField] 연결 · Addressable을 한꺼번에 본다. </summary>
        private static int Validate_UIPrefab<T>(string strPath, string strAddress, string[] arrField)
            where T : Component
        {
            GameObject goPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(strPath);
            if (goPrefab == null)
            {
                Debug.LogError($"  FAIL  UI 프리팹 없음 : {strPath}");
                return 1;
            }

            int iFail = 0;
            T cUI = goPrefab.GetComponent<T>();

            if (cUI == null)
            {
                Debug.LogError($"  FAIL  {typeof(T).Name} 컴포넌트 없음");
                ++iFail;
            }
            else
            {
                SerializedObject cSerialized = new SerializedObject(cUI);

                for (int i = 0; i < arrField.Length; ++i)
                {
                    SerializedProperty cProperty = cSerialized.FindProperty(arrField[i]);
                    if (cProperty == null || cProperty.objectReferenceValue == null)
                    {
                        Debug.LogError($"  FAIL  {typeof(T).Name}.{arrField[i]} 미연결");
                        ++iFail;
                    }
                }

                if (iFail == 0)
                    Debug.Log($"  PASS  {typeof(T).Name} 참조 연결됨");
            }

            return iFail + Validate_AddressableEntry(strPath, strAddress, CAddressableLabel.PREFAB);
        }

        private static int Validate_CsvTables()
        {
            int iFail = 0;

            for (int i = 0; i < ARR_CSV.Length; ++i)
            {
                string strName = ARR_CSV[i];
                string strPath = $"{DIR_DATA}/{strName}.csv";

                TextAsset cText = AssetDatabase.LoadAssetAtPath<TextAsset>(strPath);
                if (cText == null)
                {
                    Debug.LogError($"  FAIL  CSV 없음 : {strPath}");
                    ++iFail;
                    continue;
                }

                // Engine.CCSVDataHolder가 찾는 이름 규칙 — 어긋나면 표가 조용히 비어 버린다.
                string strExpect = "Client.CCSVData_" + strName;
                string strActual = ARR_CSV_TYPE[i].FullName;

                if (strActual != strExpect)
                {
                    Debug.LogError($"  FAIL  파싱 클래스 이름이 규칙과 다름 : {strActual} (기대 {strExpect})");
                    ++iFail;
                }
                else
                {
                    Debug.Log($"  PASS  CSV / 파싱 클래스 : {strName} ↔ {strActual}");
                }

                // 구분자가 탭이 아니면 Engine이 한 덩어리로 읽어 전부 깨진다.
                string strHeader = cText.text.Split('\n')[0];
                if (strHeader.Contains("\t") == false)
                {
                    Debug.LogError($"  FAIL  {strName}.csv의 헤더에 탭이 없습니다. 쉼표가 아니라 탭으로 구분해야 합니다.");
                    ++iFail;
                }

                iFail += Validate_AddressableEntry(strPath, strName, CAddressableLabel.CSV);
            }

            return iFail;
        }

        private static int Validate_LayerTextures()
        {
            int iFail = 0;

            for (int i = 0; i < ARR_LAYER_TEX.Length; ++i)
                iFail += Validate_Texture(ARR_LAYER_TEX[i]);

            iFail += Validate_Texture(TEX_SHAPE_02);
            return iFail;
        }

        private static int Validate_Texture(string strName)
        {
            string strPath = $"{DIR_ART}/{strName}.png";

            if (AssetDatabase.LoadAssetAtPath<Texture2D>(strPath) == null)
            {
                Debug.LogError($"  FAIL  텍스처 없음 : {strPath}");
                return 1;
            }

            int iFail = 0;

            // 가림막과 모양 마스크는 런타임에 픽셀을 읽는다 — Read/Write가 꺼져 있으면 예외가 난다.
            TextureImporter cImporter = AssetImporter.GetAtPath(strPath) as TextureImporter;
            if (cImporter == null || cImporter.isReadable == false)
            {
                Debug.LogError($"  FAIL  '{strName}'의 Read/Write가 꺼져 있습니다.");
                ++iFail;
            }

            return iFail + Validate_AddressableEntry(strPath, strName, CAddressableLabel.TEXTURE);
        }

        private static int Validate_ActorPrefab(string strPath, string strAddress, System.Type tComponent)
        {
            int iFail = 0;

            GameObject goPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(strPath);
            if (goPrefab == null)
            {
                Debug.LogError($"  FAIL  프리팹 없음 : {strPath}");
                return 1;
            }
            Debug.Log($"  PASS  프리팹 로드 : {strPath}");

            Component cComponent = goPrefab.GetComponent(tComponent);
            if (cComponent == null)
            {
                Debug.LogError($"  FAIL  {tComponent.Name} 컴포넌트 없음");
                ++iFail;
            }
            else
            {
                SerializedObject cSerialized = new SerializedObject(cComponent);
                SerializedProperty cBody = cSerialized.FindProperty("m_srBody");

                if (cBody == null || cBody.objectReferenceValue == null)
                {
                    Debug.LogError($"  FAIL  {tComponent.Name}.m_srBody 미연결");
                    ++iFail;
                }
                else
                {
                    Debug.Log($"  PASS  {tComponent.Name}.m_srBody 연결됨");
                }
            }

            SpriteRenderer srBody = goPrefab.GetComponent<SpriteRenderer>();
            if (srBody == null || srBody.sprite == null)
            {
                Debug.LogError("  FAIL  SpriteRenderer 스프라이트 미할당");
                ++iFail;
            }
            else
            {
                Debug.Log($"  PASS  스프라이트 할당 : {srBody.sprite.name} (sortingOrder {srBody.sortingOrder})");
            }

            iFail += Validate_AddressableEntry(strPath, strAddress, CAddressableLabel.PREFAB);
            return iFail;
        }

        private static int Validate_AddressableEntry(string strPath, string strAddress, string strLabel)
        {
            AddressableAssetSettings cSettings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (cSettings == null)
            {
                Debug.LogError("  FAIL  Addressable 설정 없음");
                return 1;
            }

            string strGuid = AssetDatabase.AssetPathToGUID(strPath);
            AddressableAssetEntry cEntry = cSettings.FindAssetEntry(strGuid);

            if (cEntry == null)
            {
                Debug.LogError($"  FAIL  Addressable 엔트리 없음 : {strAddress}");
                return 1;
            }

            if (cEntry.address != strAddress)
            {
                // Engine.CPrefabDataHolder가 GameObject.name을 키로 캐싱하므로 주소가 이름과 달라도
                // 로드 자체는 되지만, 혼동을 막기 위해 경고한다.
                Debug.LogWarning($"  WARN  주소가 프리팹 이름과 다름 : {cEntry.address} != {strAddress}");
            }

            if (cEntry.labels.Contains(strLabel) == false)
            {
                Debug.LogError($"  FAIL  '{strLabel}' 라벨 없음 : {strAddress}");
                return 1;
            }

            Debug.Log($"  PASS  Addressable : {cEntry.address} [{strLabel}]");
            return 0;
        }

        #region 스프라이트 생성
        private static void Create_Sprites()
        {
            // 플레이어 / 몬스터: 1 월드 유닛 크기의 원 (각자 셀 크기에 맞춰 스케일한다)
            const int BODY_SIZE = 64;
            Write_Png(PATH_TEX_PLAYER, Make_CircleTexture(BODY_SIZE, new Color(0.45f, 0.95f, 1f)));
            Import_AsSprite(PATH_TEX_PLAYER, BODY_SIZE);

            // 몬스터는 흰색으로 만들어 두고 CEnemy가 상태별로 틴트한다 (배회=빨강 / 추적=노랑)
            Write_Png(PATH_TEX_ENEMY, Make_CircleTexture(BODY_SIZE, Color.white));
            Import_AsSprite(PATH_TEX_ENEMY, BODY_SIZE);

            // 배경: 실제 보상 카드 이미지가 들어갈 자리. 점령 시 드러나는 게 보이도록 알록달록하게.
            const int BG_W = 540;
            const int BG_H = 960;
            Write_Png(PATH_TEX_BG, Make_RewardPlaceholder(BG_W, BG_H));
            Import_AsSprite(PATH_TEX_BG, 100, true);

            Create_LayerTextures(BG_W, BG_H);

            // 260904_기믹 소환물. 탄은 작고 밝게, 거미줄은 성기게 비치도록 반투명하게.
            Write_Png(PATH_TEX_PROJECTILE, Make_CircleTexture(32, new Color(1f, 0.55f, 0.2f)));
            Import_AsSprite(PATH_TEX_PROJECTILE, 32);

            Write_Png(PATH_TEX_WEB, Make_WebTexture(64));
            Import_AsSprite(PATH_TEX_WEB, 64);

            // 260904_조이스틱. 바깥은 테두리 링, 손잡이는 꽉 찬 원.
            Write_Png(PATH_TEX_JOY_BASE, Make_RingTexture(128));
            Import_AsSprite(PATH_TEX_JOY_BASE, 128);

            Write_Png(PATH_TEX_JOY_HANDLE, Make_CircleTexture(64, new Color(0.65f, 0.85f, 1f)));
            Import_AsSprite(PATH_TEX_JOY_HANDLE, 64);
        }

        /// <summary> 조이스틱 바깥 링 — 가운데가 비어 있어 게임 화면을 덜 가린다. </summary>
        private static Texture2D Make_RingTexture(int iSize)
        {
            Texture2D tex = new Texture2D(iSize, iSize, TextureFormat.RGBA32, false);
            Vector2 vCenter = new Vector2(iSize * 0.5f, iSize * 0.5f);
            float fOuter = iSize * 0.5f - 1f;
            float fInner = fOuter * 0.78f;

            for (int y = 0; y < iSize; ++y)
            {
                for (int x = 0; x < iSize; ++x)
                {
                    float fDist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), vCenter);

                    // 링 두께 안쪽/바깥쪽 모두 1px 안티에일리어싱
                    float fAlpha = Mathf.Clamp01(fOuter - fDist) * Mathf.Clamp01(fDist - fInner);
                    tex.SetPixel(x, y, new Color(0.85f, 0.92f, 1f, fAlpha * 0.55f));
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary> 거미줄 — 방사선 + 동심원. 반투명이라 아래 가림막이 비친다. </summary>
        private static Texture2D Make_WebTexture(int iSize)
        {
            Texture2D tex = new Texture2D(iSize, iSize, TextureFormat.RGBA32, false);
            Vector2 vCenter = new Vector2(iSize * 0.5f, iSize * 0.5f);
            float fRadius = iSize * 0.5f - 1f;

            for (int y = 0; y < iSize; ++y)
            {
                for (int x = 0; x < iSize; ++x)
                {
                    Vector2 vPos = new Vector2(x + 0.5f, y + 0.5f) - vCenter;
                    float fDist = vPos.magnitude;

                    if (fDist > fRadius)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    float fAngle = Mathf.Atan2(vPos.y, vPos.x) / Mathf.PI * 4f;   // 8방향 방사선
                    bool bSpoke = Mathf.Abs(Mathf.Repeat(fAngle, 1f) - 0.5f) > 0.42f;
                    bool bRing  = Mathf.Repeat(fDist / fRadius * 3f, 1f) < 0.18f;

                    Color cColor = new Color(0.85f, 0.92f, 1f, bSpoke || bRing ? 0.75f : 0.08f);
                    tex.SetPixel(x, y, cColor);
                }
            }

            tex.Apply();
            return tex;
        }

        // 260904_웨이브 이미지 스택.
        // [0]은 1웨이브를 덮는 '마스크'라 무채색으로, 나머지는 웨이브가 넘어갈수록 밝아지는
        // 보상 이미지로 만들어 어느 장이 벗겨졌는지 눈으로 바로 구분되게 한다.
        private static void Create_LayerTextures(int iWidth, int iHeight)
        {
            for (int i = 0; i < ARR_LAYER_TEX.Length; ++i)
            {
                string strPath = $"{DIR_ART}/{ARR_LAYER_TEX[i]}.png";

                Texture2D tex = i == 0 ? Make_CoverMask(iWidth, iHeight)
                                       : Make_RewardLayer(iWidth, iHeight, i);
                Write_Png(strPath, tex);

                // 가림막은 런타임에 픽셀을 읽어 마스크로 다시 찍으므로 Read/Write가 반드시 켜져 있어야 한다.
                Import_AsSprite(strPath, 100, true);
            }

            string strShapePath = $"{DIR_ART}/{TEX_SHAPE_02}.png";
            Write_Png(strShapePath, Make_ShapeMask(iWidth, iHeight));
            Import_AsSprite(strShapePath, 100, true);
        }

        /// <summary> 1웨이브를 덮는 마스크 — 격자 무늬가 옅게 깔린 어두운 막. </summary>
        private static Texture2D Make_CoverMask(int iWidth, int iHeight)
        {
            Texture2D tex = new Texture2D(iWidth, iHeight, TextureFormat.RGBA32, false);

            for (int y = 0; y < iHeight; ++y)
            {
                for (int x = 0; x < iWidth; ++x)
                {
                    bool bGrid = (x % 48) < 2 || (y % 48) < 2;
                    Color cColor = bGrid ? new Color(0.16f, 0.19f, 0.30f) : new Color(0.07f, 0.08f, 0.14f);
                    cColor.a = 1f;
                    tex.SetPixel(x, y, cColor);
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary> 웨이브별 보상 이미지 — 단계가 올라갈수록 밝고 채도가 높아진다. </summary>
        private static Texture2D Make_RewardLayer(int iWidth, int iHeight, int iStep)
        {
            Texture2D tex = new Texture2D(iWidth, iHeight, TextureFormat.RGBA32, false);
            float fStep = iStep / 3f;

            Color cTop    = Color.Lerp(new Color(0.30f, 0.34f, 0.55f), new Color(1.00f, 0.78f, 0.30f), fStep);
            Color cBottom = Color.Lerp(new Color(0.18f, 0.22f, 0.40f), new Color(0.95f, 0.35f, 0.55f), fStep);

            for (int y = 0; y < iHeight; ++y)
            {
                float fT = (float)y / (iHeight - 1);
                Color cBase = Color.Lerp(cBottom, cTop, fT);

                for (int x = 0; x < iWidth; ++x)
                {
                    float fU = (float)x / (iWidth - 1);
                    float fStripe = Mathf.Sin((fU * 10f) + (fT * 16f) + iStep) * 0.5f + 0.5f;
                    Color cColor = Color.Lerp(cBase, cBase * 1.4f, fStripe * 0.4f);

                    // 몇 번째 장인지 한눈에 보이도록 큰 숫자 대신 동심원 개수로 표시한다.
                    float fDist = Vector2.Distance(new Vector2(fU, fT), new Vector2(0.5f, 0.6f));
                    float fRing = Mathf.Repeat(fDist * iStep * 14f, 1f);
                    if (fDist < 0.26f && fRing < 0.35f)
                        cColor = Color.Lerp(cColor, Color.white, 0.45f);

                    cColor.a = 1f;
                    tex.SetPixel(x, y, cColor);
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary> 맵 모양 마스크 예시 — 가운데를 세로로 잘라낸 모래시계 형태. </summary>
        private static Texture2D Make_ShapeMask(int iWidth, int iHeight)
        {
            Texture2D tex = new Texture2D(iWidth, iHeight, TextureFormat.RGBA32, false);

            for (int y = 0; y < iHeight; ++y)
            {
                float fT = (float)y / (iHeight - 1);
                // 가운데(0.5)에서 가장 좁아지는 폭
                float fHalf = Mathf.Lerp(0.5f, 0.22f, 1f - Mathf.Abs(fT - 0.5f) * 2f);

                for (int x = 0; x < iWidth; ++x)
                {
                    float fU = (float)x / (iWidth - 1);
                    bool bInside = Mathf.Abs(fU - 0.5f) <= fHalf;
                    tex.SetPixel(x, y, bInside ? Color.white : Color.black);
                }
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D Make_CircleTexture(int iSize, Color cRim)
        {
            Texture2D tex = new Texture2D(iSize, iSize, TextureFormat.RGBA32, false);
            float fRadius = iSize * 0.5f - 1f;
            Vector2 vCenter = new Vector2(iSize * 0.5f, iSize * 0.5f);

            for (int y = 0; y < iSize; ++y)
            {
                for (int x = 0; x < iSize; ++x)
                {
                    float fDist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), vCenter);
                    float fAlpha = Mathf.Clamp01(fRadius - fDist);              // 가장자리 1px 안티에일리어싱
                    float fInner = Mathf.Clamp01((fRadius - 3f - fDist) / 3f);  // 안쪽 하이라이트
                    Color cColor = Color.Lerp(cRim, Color.white, fInner);
                    cColor.a = fAlpha;
                    tex.SetPixel(x, y, cColor);
                }
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D Make_RewardPlaceholder(int iWidth, int iHeight)
        {
            Texture2D tex = new Texture2D(iWidth, iHeight, TextureFormat.RGBA32, false);

            for (int y = 0; y < iHeight; ++y)
            {
                float fT = (float)y / (iHeight - 1);
                Color cBase = Color.Lerp(new Color(0.98f, 0.42f, 0.55f), new Color(0.28f, 0.35f, 0.85f), fT);

                for (int x = 0; x < iWidth; ++x)
                {
                    float fU = (float)x / (iWidth - 1);
                    // 대각 줄무늬 + 큰 원 하나 — "드러났다"가 한눈에 보이게 하는 용도
                    float fStripe = Mathf.Sin((fU * 14f) + (fT * 22f)) * 0.5f + 0.5f;
                    Color cColor = Color.Lerp(cBase, cBase * 1.35f, fStripe * 0.35f);

                    float fDist = Vector2.Distance(new Vector2(fU, fT), new Vector2(0.5f, 0.62f));
                    if (fDist < 0.22f)
                        cColor = Color.Lerp(new Color(1f, 0.93f, 0.6f), cColor, Mathf.SmoothStep(0f, 1f, fDist / 0.22f));

                    cColor.a = 1f;
                    tex.SetPixel(x, y, cColor);
                }
            }

            tex.Apply();
            return tex;
        }

        private static void Write_Png(string strPath, Texture2D tex)
        {
            File.WriteAllBytes(strPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(strPath, ImportAssetOptions.ForceUpdate);
        }

        // 260904_bReadable : 런타임에 GetPixels32로 읽어야 하는 텍스처(가림막 / 모양 마스크)는 반드시 켠다.
        // 꺼진 채로 두면 CGridRenderer.Fill_Cover가 예외를 던진다.
        private static void Import_AsSprite(string strPath, float fPixelsPerUnit, bool bReadable = false)
        {
            TextureImporter cImporter = AssetImporter.GetAtPath(strPath) as TextureImporter;
            if (cImporter == null)
                return;

            cImporter.textureType           = TextureImporterType.Sprite;
            cImporter.spriteImportMode      = SpriteImportMode.Single;
            cImporter.spritePixelsPerUnit   = fPixelsPerUnit;
            cImporter.alphaIsTransparency   = true;
            cImporter.mipmapEnabled         = false;
            cImporter.isReadable            = bReadable;
            cImporter.SaveAndReimport();
        }
        #endregion 스프라이트 생성

        #region 프리팹 / Addressable
        private static void Create_Prefabs()
        {
            Create_ActorPrefab<CPlayer>("Prefab_Player", PATH_TEX_PLAYER, PATH_PREFAB, 20);
            Create_ActorPrefab<CEnemy>("Prefab_Enemy", PATH_TEX_ENEMY, PATH_PREFAB_ENEMY, 15);
            // 260904_탄은 몬스터보다 앞에, 거미줄은 바닥에 깔리도록 정렬 순서를 나눈다.
            Create_ActorPrefab<CProjectile>("Prefab_Projectile", PATH_TEX_PROJECTILE, PATH_PREFAB_PROJECTILE, 18);
            Create_ActorPrefab<CWeb>("Prefab_Web", PATH_TEX_WEB, PATH_PREFAB_WEB, 12);
            Create_StageSelectUI();
            Create_InGameUI();
            Create_PopupUI();
        }

        /// <summary> 스프라이트 1장 + CGameObject 파생 컴포넌트 1개로 이루어진 프리팹을 만든다. </summary>
        private static void Create_ActorPrefab<T>(string strName, string strTexPath, string strPrefabPath,
                                                  int iSortingOrder) where T : Component
        {
            GameObject go = new GameObject(strName);

            SpriteRenderer srBody = go.AddComponent<SpriteRenderer>();
            srBody.sprite       = AssetDatabase.LoadAssetAtPath<Sprite>(strTexPath);
            srBody.sortingOrder = iSortingOrder;     // 마스크(10)보다 위

            T cComponent = go.AddComponent<T>();

            // m_srBody는 private [SerializeField]이므로 SerializedObject로 연결한다.
            SerializedObject cSerialized = new SerializedObject(cComponent);
            cSerialized.FindProperty("m_srBody").objectReferenceValue = srBody;
            cSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, strPrefabPath);
            Object.DestroyImmediate(go);
        }

        private static void Setup_Addressables()
        {
            AddressableAssetSettings cSettings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            if (cSettings == null)
            {
                Debug.LogError("[CProtoSetup] Addressable 설정 생성 실패");
                return;
            }

            cSettings.AddLabel(CAddressableLabel.PREFAB, false);
            cSettings.AddLabel(CAddressableLabel.TEXTURE, false);
            cSettings.AddLabel(CAddressableLabel.CSV, false);

            Regist_Addressable(cSettings, PATH_PREFAB, "Prefab_Player", CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_ENEMY, "Prefab_Enemy", CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_PROJECTILE, "Prefab_Projectile", CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_WEB, "Prefab_Web", CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_UI_SELECT, UI_STAGE_SELECT, CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_UI_INGAME, UI_INGAME, CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_UI_POPUP, UI_POPUP, CAddressableLabel.PREFAB);

            // 260904_웨이브 이미지 스택과 모양 마스크. 주소를 파일명과 맞춰야 CSV에 적은 이름으로 찾을 수 있다.
            for (int i = 0; i < ARR_LAYER_TEX.Length; ++i)
                Regist_Addressable(cSettings, $"{DIR_ART}/{ARR_LAYER_TEX[i]}.png", ARR_LAYER_TEX[i], CAddressableLabel.TEXTURE);

            Regist_Addressable(cSettings, $"{DIR_ART}/{TEX_SHAPE_02}.png", TEX_SHAPE_02, CAddressableLabel.TEXTURE);

            // 260904_CSV 테이블. Engine이 TextAsset 이름으로 파싱 클래스를 찾으므로 주소도 파일명과 맞춘다.
            for (int i = 0; i < ARR_CSV.Length; ++i)
                Regist_Addressable(cSettings, $"{DIR_DATA}/{ARR_CSV[i]}.csv", ARR_CSV[i], CAddressableLabel.CSV);

            EditorUtility.SetDirty(cSettings);
        }

        private static void Regist_Addressable(AddressableAssetSettings cSettings, string strAssetPath,
                                               string strAddress, string strLabel)
        {
            string strGuid = AssetDatabase.AssetPathToGUID(strAssetPath);
            if (string.IsNullOrEmpty(strGuid) == true)
            {
                Debug.LogError($"[CProtoSetup] 에셋이 없습니다 : {strAssetPath}");
                return;
            }

            AddressableAssetEntry cEntry = cSettings.CreateOrMoveEntry(strGuid, cSettings.DefaultGroup, false, false);
            if (cEntry == null)
            {
                Debug.LogError($"[CProtoSetup] Addressable 엔트리 생성 실패 : {strAssetPath}");
                return;
            }

            // Engine의 각 DataHolder가 에셋 이름을 키로 캐싱하므로 주소도 이름과 맞춰 둔다.
            cEntry.address = strAddress;
            cEntry.SetLabel(strLabel, true, false, false);
        }
        // 260904_스테이지 선택 UI 프리팹.
        // 목록은 런타임에 채워지므로 여기서는 '틀'만 만든다 —
        // 배경 패널 / 제목 / 버튼이 쌓일 Content / 복제될 버튼 템플릿(비활성).
        // 겉모습을 다듬는 것은 Unity에서 이 프리팹을 직접 여는 편이 빠르다.
        private static void Create_StageSelectUI()
        {
            GameObject goRoot = Create_UIObject(UI_STAGE_SELECT, null);
            Stretch_Full(goRoot.GetComponent<RectTransform>());

            GameObject goPanel = Create_UIObject("Panel", goRoot.transform);
            Stretch_Full(goPanel.GetComponent<RectTransform>());
            goPanel.AddComponent<Image>().color = new Color(0.05f, 0.06f, 0.10f, 0.92f);

            GameObject goTitle = Create_UIObject("Title", goRoot.transform);
            RectTransform trTitle = goTitle.GetComponent<RectTransform>();
            trTitle.anchorMin = new Vector2(0f, 1f);
            trTitle.anchorMax = new Vector2(1f, 1f);
            trTitle.pivot     = new Vector2(0.5f, 1f);
            trTitle.offsetMin = new Vector2(40f, -140f);
            trTitle.offsetMax = new Vector2(-40f, -40f);
            Text txtTitle = Make_Text(goTitle, "스테이지 선택", 44, TextAnchor.MiddleCenter);

            GameObject goContent = Create_UIObject("Content", goRoot.transform);
            RectTransform trContent = goContent.GetComponent<RectTransform>();
            trContent.anchorMin = new Vector2(0f, 0f);
            trContent.anchorMax = new Vector2(1f, 1f);
            trContent.offsetMin = new Vector2(80f, 80f);
            trContent.offsetMax = new Vector2(-80f, -160f);

            VerticalLayoutGroup cLayout = goContent.AddComponent<VerticalLayoutGroup>();
            cLayout.spacing              = 16f;
            cLayout.childAlignment       = TextAnchor.UpperCenter;
            cLayout.childForceExpandWidth  = true;
            cLayout.childForceExpandHeight = false;
            cLayout.childControlWidth      = true;
            cLayout.childControlHeight     = false;

            GameObject goButton = Create_UIObject("Btn_Template", goContent.transform);
            RectTransform trButton = goButton.GetComponent<RectTransform>();
            trButton.sizeDelta = new Vector2(0f, 110f);
            goButton.AddComponent<LayoutElement>().minHeight = 110f;
            goButton.AddComponent<Image>().color = new Color(0.16f, 0.20f, 0.34f, 1f);
            Button cButton = goButton.AddComponent<Button>();

            GameObject goLabel = Create_UIObject("Label", goButton.transform);
            Stretch_Full(goLabel.GetComponent<RectTransform>());
            Make_Text(goLabel, "MAP", 32, TextAnchor.MiddleCenter);

            goButton.SetActive(false);       // 템플릿은 항상 꺼 둔다

            CUI_StageSelect cUI = goRoot.AddComponent<CUI_StageSelect>();
            SerializedObject cSerialized = new SerializedObject(cUI);
            cSerialized.FindProperty("m_trContent").objectReferenceValue   = goContent.transform;
            cSerialized.FindProperty("m_btnTemplate").objectReferenceValue = cButton;
            cSerialized.FindProperty("m_txtTitle").objectReferenceValue    = txtTitle;
            cSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(goRoot, PATH_PREFAB_UI_SELECT);
            Object.DestroyImmediate(goRoot);
        }

        // 260904_인게임 HUD 프리팹.
        // 조이스틱은 위치를 코드로 직접 잡으므로 레이아웃 그룹에 넣지 않는다.
        // 터치는 EventSystem을 거치지 않고 Input으로 직접 읽으므로 raycastTarget은 전부 끈다 —
        // HUD가 화면을 덮고 있어도 다른 UI의 클릭을 막지 않게 하기 위해서다.
        private static void Create_InGameUI()
        {
            GameObject goRoot = Create_UIObject(UI_INGAME, null);
            Stretch_Full(goRoot.GetComponent<RectTransform>());

            GameObject goStatus = Create_UIObject("Txt_Status", goRoot.transform);
            RectTransform trStatus = goStatus.GetComponent<RectTransform>();
            trStatus.anchorMin = new Vector2(0f, 1f);
            trStatus.anchorMax = new Vector2(1f, 1f);
            trStatus.pivot     = new Vector2(0.5f, 1f);
            trStatus.offsetMin = new Vector2(24f, -90f);
            trStatus.offsetMax = new Vector2(-24f, -20f);
            Text txtStatus = Make_Text(goStatus, string.Empty, 28, TextAnchor.MiddleLeft);
            txtStatus.raycastTarget = false;

            RectTransform trBase   = Make_JoystickPart("Joystick_Base", goRoot.transform, PATH_TEX_JOY_BASE);
            RectTransform trHandle = Make_JoystickPart("Joystick_Handle", goRoot.transform, PATH_TEX_JOY_HANDLE);

            // 260904_일시정지 버튼은 오른쪽 위. 조이스틱이 아래 60%만 잡으므로 겹치지 않는다.
            GameObject goPause = Create_UIObject("Btn_Pause", goRoot.transform);
            RectTransform trPause = goPause.GetComponent<RectTransform>();
            trPause.anchorMin = new Vector2(1f, 1f);
            trPause.anchorMax = new Vector2(1f, 1f);
            trPause.pivot     = new Vector2(1f, 1f);
            trPause.anchoredPosition = new Vector2(-24f, -110f);
            trPause.sizeDelta = new Vector2(120f, 120f);
            goPause.AddComponent<Image>().color = new Color(0.16f, 0.20f, 0.34f, 0.85f);
            Button cPause = goPause.AddComponent<Button>();

            GameObject goPauseLabel = Create_UIObject("Label", goPause.transform);
            Stretch_Full(goPauseLabel.GetComponent<RectTransform>());
            Make_Text(goPauseLabel, "II", 40, TextAnchor.MiddleCenter).raycastTarget = false;

            CUI_InGame cUI = goRoot.AddComponent<CUI_InGame>();
            SerializedObject cSerialized = new SerializedObject(cUI);
            cSerialized.FindProperty("m_trJoystickBase").objectReferenceValue   = trBase;
            cSerialized.FindProperty("m_trJoystickHandle").objectReferenceValue = trHandle;
            cSerialized.FindProperty("m_txtStatus").objectReferenceValue        = txtStatus;
            cSerialized.FindProperty("m_btnPause").objectReferenceValue         = cPause;
            cSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(goRoot, PATH_PREFAB_UI_INGAME);
            Object.DestroyImmediate(goRoot);
        }

        // 260904_공용 팝업 프리팹. 일시정지와 결과 화면이 이걸 돌려쓴다.
        private static void Create_PopupUI()
        {
            GameObject goRoot = Create_UIObject(UI_POPUP, null);
            Stretch_Full(goRoot.GetComponent<RectTransform>());

            // 뒤를 어둡게 덮어 팝업에 시선이 가게 하고, 뒤쪽 클릭도 막는다.
            GameObject goDim = Create_UIObject("Dim", goRoot.transform);
            Stretch_Full(goDim.GetComponent<RectTransform>());
            goDim.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

            GameObject goPanel = Create_UIObject("Panel", goRoot.transform);
            RectTransform trPanel = goPanel.GetComponent<RectTransform>();
            trPanel.anchorMin = new Vector2(0.5f, 0.5f);
            trPanel.anchorMax = new Vector2(0.5f, 0.5f);
            trPanel.pivot     = new Vector2(0.5f, 0.5f);
            trPanel.sizeDelta = new Vector2(760f, 460f);
            goPanel.AddComponent<Image>().color = new Color(0.10f, 0.12f, 0.20f, 1f);

            GameObject goTitle = Create_UIObject("Txt_Title", goPanel.transform);
            RectTransform trTitle = goTitle.GetComponent<RectTransform>();
            trTitle.anchorMin = new Vector2(0f, 1f);
            trTitle.anchorMax = new Vector2(1f, 1f);
            trTitle.pivot     = new Vector2(0.5f, 1f);
            trTitle.offsetMin = new Vector2(30f, -130f);
            trTitle.offsetMax = new Vector2(-30f, -30f);
            Text txtTitle = Make_Text(goTitle, "제목", 48, TextAnchor.MiddleCenter);
            txtTitle.raycastTarget = false;

            GameObject goBody = Create_UIObject("Txt_Body", goPanel.transform);
            RectTransform trBody = goBody.GetComponent<RectTransform>();
            trBody.anchorMin = new Vector2(0f, 0f);
            trBody.anchorMax = new Vector2(1f, 1f);
            trBody.offsetMin = new Vector2(30f, 150f);
            trBody.offsetMax = new Vector2(-30f, -140f);
            Text txtBody = Make_Text(goBody, "본문", 30, TextAnchor.MiddleCenter);
            txtBody.raycastTarget = false;

            Button cSecondary = Make_PopupButton("Btn_Secondary", goPanel.transform, -190f, "나가기");
            Button cPrimary   = Make_PopupButton("Btn_Primary", goPanel.transform, 190f, "확인");

            CUI_Popup cUI = goRoot.AddComponent<CUI_Popup>();
            SerializedObject cSerialized = new SerializedObject(cUI);
            cSerialized.FindProperty("m_txtTitle").objectReferenceValue     = txtTitle;
            cSerialized.FindProperty("m_txtBody").objectReferenceValue      = txtBody;
            cSerialized.FindProperty("m_btnPrimary").objectReferenceValue   = cPrimary;
            cSerialized.FindProperty("m_btnSecondary").objectReferenceValue = cSecondary;
            cSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(goRoot, PATH_PREFAB_UI_POPUP);
            Object.DestroyImmediate(goRoot);
        }

        private static Button Make_PopupButton(string strName, Transform trParent, float fOffsetX, string strLabel)
        {
            GameObject go = Create_UIObject(strName, trParent);
            RectTransform trButton = go.GetComponent<RectTransform>();
            trButton.anchorMin = new Vector2(0.5f, 0f);
            trButton.anchorMax = new Vector2(0.5f, 0f);
            trButton.pivot     = new Vector2(0.5f, 0f);
            trButton.anchoredPosition = new Vector2(fOffsetX, 36f);
            trButton.sizeDelta = new Vector2(320f, 96f);

            go.AddComponent<Image>().color = new Color(0.20f, 0.26f, 0.44f, 1f);
            Button cButton = go.AddComponent<Button>();

            GameObject goLabel = Create_UIObject("Label", go.transform);
            Stretch_Full(goLabel.GetComponent<RectTransform>());
            Make_Text(goLabel, strLabel, 32, TextAnchor.MiddleCenter).raycastTarget = false;

            return cButton;
        }

        private static RectTransform Make_JoystickPart(string strName, Transform trParent, string strTexPath)
        {
            GameObject go = Create_UIObject(strName, trParent);
            RectTransform trPart = go.GetComponent<RectTransform>();

            // 화면 어디에나 놓이므로 앵커는 가운데 한 점으로 고정한다.
            trPart.anchorMin = new Vector2(0.5f, 0.5f);
            trPart.anchorMax = new Vector2(0.5f, 0.5f);
            trPart.pivot     = new Vector2(0.5f, 0.5f);
            trPart.sizeDelta = new Vector2(200f, 200f);

            Image cImage = go.AddComponent<Image>();
            cImage.sprite        = AssetDatabase.LoadAssetAtPath<Sprite>(strTexPath);
            cImage.raycastTarget = false;

            go.SetActive(false);        // 잡기 전에는 보이지 않는다
            return trPart;
        }

        private static GameObject Create_UIObject(string strName, Transform trParent)
        {
            GameObject go = new GameObject(strName, typeof(RectTransform));
            if (trParent != null)
                go.transform.SetParent(trParent, false);

            return go;
        }

        private static void Stretch_Full(RectTransform trTarget)
        {
            trTarget.anchorMin = Vector2.zero;
            trTarget.anchorMax = Vector2.one;
            trTarget.offsetMin = Vector2.zero;
            trTarget.offsetMax = Vector2.zero;
        }

        // 레거시 Text를 쓴다 — 이 프로젝트에는 TextMeshPro 패키지가 없다.
        // 내장 폰트 이름이 Unity 버전마다 달라서 둘 다 시도한다.
        private static Text Make_Text(GameObject goTarget, string strContent, int iFontSize, TextAnchor eAnchor)
        {
            Text cText = goTarget.AddComponent<Text>();
            cText.text      = strContent;
            cText.fontSize  = iFontSize;
            cText.alignment = eAnchor;
            cText.color     = Color.white;

            cText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                      ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (cText.font == null)
                Debug.LogWarning("[CProtoSetup] 내장 폰트를 찾지 못했습니다. 프리팹에서 폰트를 직접 지정하세요.");

            return cText;
        }
        #endregion 프리팹 / Addressable

        #region 데이터
        // 260904_에디터도 런타임과 똑같은 파서를 쓴다.
        // Engine.CCSVData.Read_CSVData는 public이라 Addressable을 거치지 않고 바로 먹일 수 있다 —
        // 덕분에 맵 크기 같은 값을 에디터 코드에 다시 적지 않아도 된다.
        public static CMapInfo Load_MapInfo(int iMapID)
        {
            CMapInfo cFallback = new CMapInfo
            {
                iMapID = iMapID, iGridWidth = 60, iGridHeight = 100, fCellSize = 0.12f, iBorderThick = 2,
            };

            TextAsset cText = AssetDatabase.LoadAssetAtPath<TextAsset>($"{DIR_DATA}/MapInfo.csv");
            if (cText == null)
            {
                Debug.LogWarning("[CProtoSetup] MapInfo.csv가 없어 기본값을 씁니다.");
                return cFallback;
            }

            CCSVData_MapInfo cTable = new CCSVData_MapInfo();
            cTable.Read_CSVData(cText);

            CMapInfo cMapInfo = cTable.Get_Info(iMapID);
            return cMapInfo ?? cFallback;
        }
        #endregion 데이터

        #region 씬 구성
        private static void Build_Scene()
        {
            UnityEngine.SceneManagement.Scene cScene =
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 2D에서는 필요 없는 기본 조명 제거
            GameObject goLight = GameObject.Find("Directional Light");
            if (goLight != null)
                Object.DestroyImmediate(goLight);

            // 260904_카메라 크기는 맵 크기에서 나온다. 숫자를 여기 다시 적지 않고 MapInfo.csv를 읽는다.
            CMapInfo cMapInfo = Load_MapInfo(DEFAULT_MAP_ID);
            Setup_Camera(cMapInfo.iGridHeight * cMapInfo.fCellSize);

            SpriteRenderer srBackground = Create_Renderer("BG_Reward", 0,
                                            AssetDatabase.LoadAssetAtPath<Sprite>(PATH_TEX_BG));
            SpriteRenderer srOverlay    = Create_Renderer("Overlay_Mask", 10, null);

            GameObject goGameManager = new GameObject("GameManager");
            CGameManager cGameManager = goGameManager.AddComponent<CGameManager>();
            goGameManager.AddComponent<CDebugHUD>();

            // 260904_UI 캔버스. Engine이 UI를 붙일 자리를 알아야 해서 세 개로 나눠 둔다.
            Create_UICanvas(out Transform trField, out Transform trMain, out Transform trPopup);

            SerializedObject cSerialized = new SerializedObject(cGameManager);
            cSerialized.FindProperty("m_srBackground").objectReferenceValue = srBackground;
            cSerialized.FindProperty("m_srOverlay").objectReferenceValue    = srOverlay;
            cSerialized.FindProperty("m_trUIField").objectReferenceValue    = trField;
            cSerialized.FindProperty("m_trUIMain").objectReferenceValue     = trMain;
            cSerialized.FindProperty("m_trUIPopup").objectReferenceValue    = trPopup;
            cSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(cScene, PATH_SCENE);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(PATH_SCENE, true) };
        }

        // 260904_Engine.CUI_Manager가 OBJECT_TYPE으로 캔버스를 골라 쓰므로 세 자리를 만들어 둔다.
        private static void Create_UICanvas(out Transform trField, out Transform trMain, out Transform trPopup)
        {
            GameObject goCanvas = new GameObject("UICanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            Canvas cCanvas = goCanvas.GetComponent<Canvas>();
            cCanvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler cScaler = goCanvas.GetComponent<CanvasScaler>();
            cScaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cScaler.referenceResolution = new Vector2(1080f, 1920f);
            cScaler.matchWidthOrHeight  = 0.5f;

            trField = Create_CanvasLayer(goCanvas.transform, "Field");
            trMain  = Create_CanvasLayer(goCanvas.transform, "Main");
            trPopup = Create_CanvasLayer(goCanvas.transform, "Popup");

            // 버튼을 누르려면 EventSystem이 있어야 한다. 없으면 UI가 떠도 반응하지 않는다.
            if (Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private static Transform Create_CanvasLayer(Transform trParent, string strName)
        {
            GameObject go = Create_UIObject(strName, trParent);
            Stretch_Full(go.GetComponent<RectTransform>());
            return go.transform;
        }

        private static void Setup_Camera(float fWorldHeight)
        {
            Camera cCamera = Camera.main;
            if (cCamera == null)
            {
                GameObject goCamera = new GameObject("Main Camera") { tag = "MainCamera" };
                cCamera = goCamera.AddComponent<Camera>();
            }

            if (cCamera.GetComponent<UniversalAdditionalCameraData>() == null)
                cCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();

            cCamera.orthographic        = true;
            cCamera.orthographicSize    = fWorldHeight * 0.5f + 0.3f;   // 맵 전체 + 약간의 여백
            cCamera.clearFlags          = CameraClearFlags.SolidColor;
            cCamera.backgroundColor     = new Color(0.05f, 0.05f, 0.08f);
            cCamera.transform.position  = new Vector3(0f, 0f, -10f);
            cCamera.transform.rotation  = Quaternion.identity;
        }

        private static SpriteRenderer Create_Renderer(string strName, int iSortingOrder, Sprite spSprite)
        {
            GameObject go = new GameObject(strName);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite       = spSprite;
            sr.sortingOrder = iSortingOrder;
            return sr;
        }
        #endregion 씬 구성

        private static void Ensure_Folder(string strPath)
        {
            if (Directory.Exists(strPath) == true)
                return;

            Directory.CreateDirectory(strPath);
            AssetDatabase.Refresh();
        }
    }
}
