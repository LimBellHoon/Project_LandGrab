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
        private const string PATH_TEX_BG     = DIR_ART + "/Tex_Reward_Placeholder.png";
        private const string PATH_PREFAB     = DIR_PREFAB + "/Prefab_Player.prefab";
        private const string PATH_SCENE      = DIR_SCENE + "/LV_Proto.unity";

        [MenuItem("Tools/LandGrab/Setup Prototype")]
        public static void Setup_All()
        {
            Ensure_Folder(DIR_ART);
            Ensure_Folder(DIR_PREFAB);
            Ensure_Folder(DIR_SCENE);

            Create_Sprites();
            Create_PlayerPrefab();
            Setup_Addressables();
            Build_Scene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CProtoSetup] 프로토타입 셋업 완료 — Assets/Scenes/LV_Proto.unity 를 열고 Play 하세요.");
        }

        #region 스프라이트 생성
        private static void Create_Sprites()
        {
            // 플레이어: 1 월드 유닛 크기의 원 (CPlayer가 셀 크기에 맞춰 스케일한다)
            const int PLAYER_SIZE = 64;
            Write_Png(PATH_TEX_PLAYER, Make_CircleTexture(PLAYER_SIZE));
            Import_AsSprite(PATH_TEX_PLAYER, PLAYER_SIZE);

            // 배경: 실제 보상 카드 이미지가 들어갈 자리. 점령 시 드러나는 게 보이도록 알록달록하게.
            const int BG_W = 540;
            const int BG_H = 960;
            Write_Png(PATH_TEX_BG, Make_RewardPlaceholder(BG_W, BG_H));
            Import_AsSprite(PATH_TEX_BG, 100);
        }

        private static Texture2D Make_CircleTexture(int iSize)
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
                    Color cColor = Color.Lerp(new Color(0.45f, 0.95f, 1f), Color.white, fInner);
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
        private static void Create_PlayerPrefab()
        {
            GameObject goPlayer = new GameObject("Prefab_Player");

            SpriteRenderer srBody = goPlayer.AddComponent<SpriteRenderer>();
            srBody.sprite       = AssetDatabase.LoadAssetAtPath<Sprite>(PATH_TEX_PLAYER);
            srBody.sortingOrder = 20;   // 마스크(10)보다 위

            CPlayer cPlayer = goPlayer.AddComponent<CPlayer>();

            // m_srBody는 private [SerializeField]이므로 SerializedObject로 연결한다.
            SerializedObject cSerialized = new SerializedObject(cPlayer);
            cSerialized.FindProperty("m_srBody").objectReferenceValue = srBody;
            cSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(goPlayer, PATH_PREFAB);
            Object.DestroyImmediate(goPlayer);
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

            string strGuid = AssetDatabase.AssetPathToGUID(PATH_PREFAB);
            AddressableAssetEntry cEntry = cSettings.CreateOrMoveEntry(strGuid, cSettings.DefaultGroup, false, false);
            if (cEntry == null)
            {
                Debug.LogError("[CProtoSetup] Addressable 엔트리 생성 실패");
                return;
            }

            // Engine.CPrefabDataHolder는 GameObject.name을 키로 캐싱하므로 주소도 이름과 맞춰 둔다.
            cEntry.address = "Prefab_Player";
            cEntry.SetLabel(CAddressableLabel.PREFAB, true, false, false);

            EditorUtility.SetDirty(cSettings);
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
