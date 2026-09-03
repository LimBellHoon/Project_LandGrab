using System.IO;

using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;

using UnityEngine;
using UnityEngine.Rendering.Universal;

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

        private const string PATH_TEX_PLAYER = DIR_ART + "/Tex_PlayerBody.png";
        private const string PATH_TEX_ENEMY  = DIR_ART + "/Tex_EnemyBody.png";
        private const string PATH_TEX_BG     = DIR_ART + "/Tex_Reward_Placeholder.png";
        private const string PATH_PREFAB     = DIR_PREFAB + "/Prefab_Player.prefab";
        private const string PATH_PREFAB_ENEMY = DIR_PREFAB + "/Prefab_Enemy.prefab";
        private const string PATH_SCENE      = DIR_SCENE + "/LV_Proto.unity";

        // 260904_몬스터 기믹
        private const string PATH_TEX_WEB           = DIR_ART + "/Tex_Web.png";
        private const string PATH_PREFAB_PROJECTILE = DIR_PREFAB + "/Prefab_Projectile.prefab";
        private const string PATH_PREFAB_WEB        = DIR_PREFAB + "/Prefab_Web.prefab";

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

            Create_Sprites();
            Create_Prefabs();
            Setup_Addressables();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CProtoSetup] 에셋 셋업 완료 (스프라이트 / 프리팹 / Addressable)");
        }

        // 260903_에셋이 실제로 로드되는지 확인 (Play 전에 프리팹 누락을 잡는 용도)
        [MenuItem("Tools/LandGrab/Validate Assets")]
        public static void Validate_Assets()
        {
            int iFail = 0;

            iFail += Validate_ActorPrefab(PATH_PREFAB, "Prefab_Player", typeof(CPlayer));
            iFail += Validate_ActorPrefab(PATH_PREFAB_ENEMY, "Prefab_Enemy", typeof(CEnemy));
            // 260904_몬스터 기믹
            iFail += Validate_ActorPrefab(PATH_PREFAB_PROJECTILE, "Prefab_Projectile", typeof(CProjectile));
            iFail += Validate_ActorPrefab(PATH_PREFAB_WEB, "Prefab_Web", typeof(CWeb));

            if (iFail == 0)
                Debug.Log("[CProtoSetup] 에셋 검증 통과 — 프리팹 / 스프라이트 / Addressable 정상");
            else
                Debug.LogError($"[CProtoSetup] 에셋 검증 실패 {iFail}건 — Tools/LandGrab/Setup Assets 를 실행하세요.");

            if (Application.isBatchMode == true)
                EditorApplication.Exit(iFail == 0 ? 0 : 1);
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

            iFail += Validate_AddressableEntry(strPath, strAddress);
            return iFail;
        }

        private static int Validate_AddressableEntry(string strPath, string strAddress)
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

            if (cEntry.labels.Contains(CAddressableLabel.PREFAB) == false)
            {
                Debug.LogError($"  FAIL  '{CAddressableLabel.PREFAB}' 라벨 없음 : {strAddress}");
                return 1;
            }

            Debug.Log($"  PASS  Addressable : {cEntry.address} [{CAddressableLabel.PREFAB}]");
            return 0;
        }

        #region 스프라이트 생성
        private static void Create_Sprites()
        {
            // 플레이어 / 몬스터: 1 월드 유닛 크기의 원 (각자 셀 크기에 맞춰 스케일한다)
            const int BODY_SIZE = 64;
            Write_Png(PATH_TEX_PLAYER, Make_CircleTexture(BODY_SIZE, new Color(0.45f, 0.95f, 1f)));
            Import_AsSprite(PATH_TEX_PLAYER, BODY_SIZE);

            // 몬스터는 흰색으로 만들어 두고 CEnemy가 기믹/상태별로 틴트한다.
            // 투사체도 같은 원을 쓴다 (CProjectile이 색을 입힌다).
            Write_Png(PATH_TEX_ENEMY, Make_CircleTexture(BODY_SIZE, Color.white));
            Import_AsSprite(PATH_TEX_ENEMY, BODY_SIZE);

            // 260904_거미줄은 원과 구분되어야 하므로 동심원 무늬로 따로 만든다
            Write_Png(PATH_TEX_WEB, Make_WebTexture(BODY_SIZE));
            Import_AsSprite(PATH_TEX_WEB, BODY_SIZE);

            // 배경: 실제 보상 카드 이미지가 들어갈 자리. 점령 시 드러나는 게 보이도록 알록달록하게.
            const int BG_W = 540;
            const int BG_H = 960;
            Write_Png(PATH_TEX_BG, Make_RewardPlaceholder(BG_W, BG_H));
            Import_AsSprite(PATH_TEX_BG, 100);
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

        // 260904_거미줄: 동심원 + 방사선. 원형 몬스터/투사체와 실루엣이 구분되게.
        private static Texture2D Make_WebTexture(int iSize)
        {
            Texture2D tex = new Texture2D(iSize, iSize, TextureFormat.RGBA32, false);
            float fRadius = iSize * 0.5f - 1f;
            Vector2 vCenter = new Vector2(iSize * 0.5f, iSize * 0.5f);

            for (int y = 0; y < iSize; ++y)
            {
                for (int x = 0; x < iSize; ++x)
                {
                    Vector2 vOffset = new Vector2(x + 0.5f, y + 0.5f) - vCenter;
                    float fDist = vOffset.magnitude;

                    if (fDist > fRadius)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    float fNormal = fDist / fRadius;
                    float fRing   = Mathf.Abs(Mathf.Sin(fNormal * Mathf.PI * 4f));           // 동심원
                    float fSpoke  = Mathf.Abs(Mathf.Cos(Mathf.Atan2(vOffset.y, vOffset.x) * 4f)); // 방사선
                    float fAlpha  = Mathf.Max(fRing, fSpoke) * (1f - fNormal * 0.5f);

                    tex.SetPixel(x, y, new Color(0.85f, 1f, 0.75f, Mathf.Clamp01(fAlpha)));
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

        private static void Import_AsSprite(string strPath, float fPixelsPerUnit)
        {
            TextureImporter cImporter = AssetImporter.GetAtPath(strPath) as TextureImporter;
            if (cImporter == null)
                return;

            cImporter.textureType           = TextureImporterType.Sprite;
            cImporter.spriteImportMode      = SpriteImportMode.Single;
            cImporter.spritePixelsPerUnit   = fPixelsPerUnit;
            cImporter.alphaIsTransparency   = true;
            cImporter.mipmapEnabled         = false;
            cImporter.SaveAndReimport();
        }
        #endregion 스프라이트 생성

        #region 프리팹 / Addressable
        private static void Create_Prefabs()
        {
            // sortingOrder — 마스크(10) < 거미줄(12) < 투사체(18) < 몬스터(15는 마스크 위) < 플레이어(20)
            Create_ActorPrefab<CPlayer>("Prefab_Player", PATH_TEX_PLAYER, PATH_PREFAB, 20);
            Create_ActorPrefab<CEnemy>("Prefab_Enemy", PATH_TEX_ENEMY, PATH_PREFAB_ENEMY, 15);
            // 260904_몬스터 기믹
            Create_ActorPrefab<CProjectile>("Prefab_Projectile", PATH_TEX_ENEMY, PATH_PREFAB_PROJECTILE, 18);
            Create_ActorPrefab<CWeb>("Prefab_Web", PATH_TEX_WEB, PATH_PREFAB_WEB, 12);
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

            Regist_Addressable(cSettings, PATH_PREFAB, "Prefab_Player");
            Regist_Addressable(cSettings, PATH_PREFAB_ENEMY, "Prefab_Enemy");
            // 260904_몬스터 기믹
            Regist_Addressable(cSettings, PATH_PREFAB_PROJECTILE, "Prefab_Projectile");
            Regist_Addressable(cSettings, PATH_PREFAB_WEB, "Prefab_Web");

            EditorUtility.SetDirty(cSettings);
        }

        private static void Regist_Addressable(AddressableAssetSettings cSettings, string strAssetPath, string strAddress)
        {
            string strGuid = AssetDatabase.AssetPathToGUID(strAssetPath);
            AddressableAssetEntry cEntry = cSettings.CreateOrMoveEntry(strGuid, cSettings.DefaultGroup, false, false);
            if (cEntry == null)
            {
                Debug.LogError($"[CProtoSetup] Addressable 엔트리 생성 실패 : {strAssetPath}");
                return;
            }

            // Engine.CPrefabDataHolder는 GameObject.name을 키로 캐싱하므로 주소도 이름과 맞춰 둔다.
            cEntry.address = strAddress;
            cEntry.SetLabel(CAddressableLabel.PREFAB, true, false, false);
        }
        #endregion 프리팹 / Addressable

        #region 씬 구성
        private static void Build_Scene()
        {
            UnityEngine.SceneManagement.Scene cScene =
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // 2D에서는 필요 없는 기본 조명 제거
            GameObject goLight = GameObject.Find("Directional Light");
            if (goLight != null)
                Object.DestroyImmediate(goLight);

            CStageDesc cStageDesc = new CStageDesc();
            float fWorldHeight = cStageDesc.iGridHeight * cStageDesc.fCellSize;

            Setup_Camera(fWorldHeight);

            SpriteRenderer srBackground = Create_Renderer("BG_Reward", 0,
                                            AssetDatabase.LoadAssetAtPath<Sprite>(PATH_TEX_BG));
            SpriteRenderer srOverlay    = Create_Renderer("Overlay_Mask", 10, null);

            GameObject goGameManager = new GameObject("GameManager");
            CGameManager cGameManager = goGameManager.AddComponent<CGameManager>();
            goGameManager.AddComponent<CDebugHUD>();

            SerializedObject cSerialized = new SerializedObject(cGameManager);
            cSerialized.FindProperty("m_srBackground").objectReferenceValue = srBackground;
            cSerialized.FindProperty("m_srOverlay").objectReferenceValue    = srOverlay;
            cSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(cScene, PATH_SCENE);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(PATH_SCENE, true) };
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
