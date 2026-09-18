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
        // 260912_배치 이미지의 셀당 픽셀 수. 실제 아트가 들어오면 이 비율만 지키면 된다.
        private const int TEX_PIXEL_PER_CELL = 9;

        // 260904_CSV 테이블. 파일명이 곧 Client.CCSVData_<파일명> 클래스 이름이다.
        private static readonly string[] ARR_CSV = { "EnemyInfo", "MapInfo", "UpgradeInfo", "SkillInfo", "EquipInfo", "CardInfo", "RunSkillInfo", "ProjectileInfo", "ImpactInfo", "AwakenInfo", "CharacterInfo", "GachaInfo", "CaptureRewardInfo" };
        // Type.GetType은 부르는 어셈블리(에디터)만 뒤지므로 런타임 클래스를 못 찾는다.
        // 컴파일 시점에 확정되는 typeof로 들고 있어야 이름 규칙을 제대로 검증할 수 있다.
        private static readonly System.Type[] ARR_CSV_TYPE =
        {
            typeof(CCSVData_EnemyInfo), typeof(CCSVData_MapInfo), typeof(CCSVData_UpgradeInfo),
            typeof(CCSVData_SkillInfo), typeof(CCSVData_EquipInfo), typeof(CCSVData_CardInfo),
            typeof(CCSVData_RunSkillInfo), typeof(CCSVData_ProjectileInfo), typeof(CCSVData_ImpactInfo),
            typeof(CCSVData_AwakenInfo), typeof(CCSVData_CharacterInfo), typeof(CCSVData_GachaInfo),
            typeof(CCSVData_CaptureRewardInfo),
        };

        private const int DEFAULT_MAP_ID = 1;       // 씬/프리뷰가 기준으로 삼는 맵
        private const string PATH_PREFAB     = DIR_PREFAB + "/Prefab_Player.prefab";
        private const string PATH_PREFAB_ENEMY = DIR_PREFAB + "/Prefab_Enemy.prefab";
        // 260904_기믹 소환물
        private const string PATH_TEX_PROJECTILE  = DIR_ART + "/Tex_Projectile.png";
        private const string PATH_TEX_WEB         = DIR_ART + "/Tex_Web.png";
        private const string PATH_PREFAB_PROJECTILE = DIR_PREFAB + "/Prefab_Projectile.prefab";
        private const string PATH_PREFAB_WEB        = DIR_PREFAB + "/Prefab_Web.prefab";
        // 260916_런 스킬 '영혼 수집가' 픽업
        private const string PATH_TEX_SOUL          = DIR_ART + "/Tex_Soul.png";
        private const string PATH_PREFAB_SOUL       = DIR_PREFAB + "/Prefab_Soul.prefab";
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
        // 260905_로비(하단 탭바) / 강화 화면
        private const string PATH_PREFAB_UI_LOBBY   = DIR_PREFAB + "/Prefab_UI_Lobby.prefab";
        private const string PATH_PREFAB_UI_UPGRADE = DIR_PREFAB + "/Prefab_UI_Upgrade.prefab";
        private const string UI_LOBBY               = "Prefab_UI_Lobby";
        private const string UI_UPGRADE             = "Prefab_UI_Upgrade";
        private const string PATH_PREFAB_UI_SHOP    = DIR_PREFAB + "/Prefab_UI_Shop.prefab";
        private const string UI_SHOP                = "Prefab_UI_Shop";
        private const string PATH_PREFAB_UI_INVEN   = DIR_PREFAB + "/Prefab_UI_Inventory.prefab";
        private const string PATH_PREFAB_UI_CARD    = DIR_PREFAB + "/Prefab_UI_CardPick.prefab";
        private const string UI_INVENTORY           = "Prefab_UI_Inventory";
        private const string UI_CARDPICK            = "Prefab_UI_CardPick";
        // 260918_수집한 카드(웨이브 보상) 갤러리 — 위 3지선다 팝업(CardPick)과는 다른 화면이다.
        private const string PATH_PREFAB_UI_CARD_GALLERY = DIR_PREFAB + "/Prefab_UI_Card.prefab";
        private const string UI_CARD_GALLERY             = "Prefab_UI_Card";
        // 260918_카드 크게 보기 (갤러리에서 한 장을 누르면 뜨는 전체 화면 팝업)
        private const string PATH_PREFAB_UI_CARD_VIEWER  = DIR_PREFAB + "/Prefab_UI_CardViewer.prefab";
        private const string UI_CARD_VIEWER              = "Prefab_UI_CardViewer";

        // 260912_카드 아이콘. CARD_TYPE 이름을 그대로 쓴다 — 표에 종류를 더하면 여기에만 추가하면 된다.
        private static readonly string[] ARR_CARD_ICON =
        {
            "Tex_Card_SHIELD", "Tex_Card_HEAL", "Tex_Card_SPEED", "Tex_Card_EVASION", "Tex_Card_SLOW",
        };
        private const string TEX_CARD_GLOW = "Tex_CardGlow";
        // 260918_장비 부위 아이콘. EQUIP_SLOT 순서(NONE 빼고) — 가방의 B 슬롯과 장비 칸이 같이 쓴다.
        private static readonly string[] ARR_EQUIP_ICON =
        {
            "Tex_Equip_SHOES", "Tex_Equip_BAG", "Tex_Equip_NECKLACE", "Tex_Equip_CONSUMABLE",
        };
        private const int EQUIP_ICON_KIND_START = 100;      // Is_IconInk에서 카드 · 런 스킬 모양 다음
        // 260917_런 스킬 아이콘. RUN_SKILL_TYPE 순서(NONE 빼고)를 그대로 따른다 — CUI_CardPick이 (int)타입 - 1로 찾는다.
        // 흰색으로 구워 두고 분류(액티브/패시브) 색은 런타임에 칠한다.
        private static readonly string[] ARR_RUN_SKILL_ICON =
        {
            "Tex_RunSkill_MOONWALK", "Tex_RunSkill_EDGE_WRAP", "Tex_RunSkill_SOUL_COLLECTOR", "Tex_RunSkill_RAGE",
            "Tex_RunSkill_MAGNET", "Tex_RunSkill_EVASION", "Tex_RunSkill_ORBIT", "Tex_RunSkill_CLUB",
            "Tex_RunSkill_MAGIC_BOLT", "Tex_RunSkill_LASER_BEAM", "Tex_RunSkill_BOOMERANG", "Tex_RunSkill_BOUNCE_SHOT",
            "Tex_RunSkill_STUN_SHOT", "Tex_RunSkill_MASS_STUN",
        };
        private const int RUN_SKILL_ICON_KIND_START = 5;    // Is_IconInk에서 카드 다섯 모양 다음부터
        private const string UI_INGAME              = "Prefab_UI_InGame";
        private const string PATH_PREFAB_UI_POPUP   = DIR_PREFAB + "/Prefab_UI_Popup.prefab";
        private const string UI_POPUP               = "Prefab_UI_Popup";
        private const string PATH_TEX_JOY_BASE      = DIR_ART + "/Tex_JoystickBase.png";
        private const string PATH_TEX_JOY_HANDLE    = DIR_ART + "/Tex_JoystickHandle.png";
        private const string PATH_SCENE      = DIR_SCENE + "/LV_Proto.unity";

        [MenuItem("Tools/LandGrab/Setup Prototype")]   // 씬까지 새로 만든다
        public static void Setup_All()
        {
            Setup_Assets();
            Build_Scene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CProtoSetup] 프로토타입 셋업 완료 — Assets/Scenes/LV_Proto.unity 를 열고 Play 하세요.");
        }

        // 260902_씬을 갈아엎지 않는 안전한 메뉴 — 작업 중인 씬이 열려 있어도 쓸 수 있다.
        [MenuItem("Tools/LandGrab/Setup Assets")]      // 씬은 건드리지 않는다
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
            iFail += Validate_ActorPrefab(PATH_PREFAB_SOUL, "Prefab_Soul", typeof(CSoul));
            iFail += Validate_StageSelectUI();
            iFail += Validate_UIPrefab<CUI_InGame>(PATH_PREFAB_UI_INGAME, UI_INGAME,
                        new[] { "m_trJoystickBase", "m_trJoystickHandle", "m_txtStatus",
                                "m_imgProgress", "m_txtTime", "m_btnPause", "m_imgFlash" });
            iFail += Validate_UIPrefab<CUI_Lobby>(PATH_PREFAB_UI_LOBBY, UI_LOBBY,
                                                 new[] { "m_trContent", "m_txtCoin", "m_txtStar", "m_arrTabButton" });
            iFail += Validate_UIPrefab<CUI_Upgrade>(PATH_PREFAB_UI_UPGRADE, UI_UPGRADE,
                                                 new[] { "m_trContent", "m_btnTemplate", "m_txtTitle" });
            iFail += Validate_UIPrefab<CUI_Shop>(PATH_PREFAB_UI_SHOP, UI_SHOP,
                                                 new[] { "m_trContent", "m_btnTemplate", "m_txtTitle" });
            iFail += Validate_UIPrefab<CUI_Inventory>(PATH_PREFAB_UI_INVEN, UI_INVENTORY,
                        new[] { "m_trContent", "m_btnTemplate", "m_txtTitle", "m_arrTabButton",
                                "m_btnCharacter", "m_imgCharacter", "m_txtCharacter", "m_spDefaultCharacter",
                                "m_arrSlotButton", "m_arrSlotIcon" });
            iFail += Validate_UIPrefab<CUI_CardPick>(PATH_PREFAB_UI_CARD, UI_CARDPICK,
                                                 new[] { "m_txtTitle", "m_btnTemplate", "m_trContent", "m_arrIcon", "m_arrRunSkillIcon" });
            iFail += Validate_UIPrefab<CUI_Card>(PATH_PREFAB_UI_CARD_GALLERY, UI_CARD_GALLERY,
                                                 new[] { "m_trContent", "m_btnTemplate", "m_txtTitle" });
            iFail += Validate_UIPrefab<CUI_CardViewer>(PATH_PREFAB_UI_CARD_VIEWER, UI_CARD_VIEWER,
                        new[] { "m_imgPhoto", "m_cFitter", "m_txtCaption", "m_btnPrev", "m_btnNext", "m_btnClose" });
            iFail += Validate_UIPrefab<CUI_Popup>(PATH_PREFAB_UI_POPUP, UI_POPUP,
                        new[] { "m_txtTitle", "m_txtBody", "m_btnPrimary", "m_btnSecondary" });

            // 260904_CSV 테이블과 웨이브 이미지가 빠지면 스테이지가 통째로 안 뜬다.
            iFail += Validate_CsvTables();
            iFail += Validate_LayerTextures();
            iFail += Validate_GameConfig();
            iFail += Validate_LayerAspect();

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
        private static bool Is_ArrayFilled(SerializedProperty cProperty)
        {
            if (cProperty.arraySize == 0)
                return false;

            for (int i = 0; i < cProperty.arraySize; ++i)
            {
                if (cProperty.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    return false;
            }

            return true;
        }

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
                    // 260905_배열 필드는 objectReferenceValue가 항상 null이라
                    // 원소가 하나라도 있고 전부 채워졌는지로 본다.
                    if (cProperty != null && cProperty.isArray == true
                        && cProperty.propertyType != SerializedPropertyType.String)
                    {
                        if (Is_ArrayFilled(cProperty) == false)
                        {
                            Debug.LogError($"  FAIL  {typeof(T).Name}.{arrField[i]} 배열이 비어 있거나 빈 칸이 있음");
                            ++iFail;
                        }
                        continue;
                    }

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

            // 260912_표 이름과 파싱 클래스를 두 배열로 나란히 들고 있다.
            // 한쪽에만 추가하면 아래 루프가 배열 밖을 짚어 예외로 죽는다 —
            // 무슨 일이 났는지 알 수 없으므로 여기서 먼저 이름을 대고 멈춘다.
            if (ARR_CSV.Length != ARR_CSV_TYPE.Length)
            {
                Debug.LogError($"  FAIL  ARR_CSV({ARR_CSV.Length})와 ARR_CSV_TYPE({ARR_CSV_TYPE.Length})의 "
                             + "개수가 다릅니다. 표를 추가했으면 두 배열 모두에 넣으세요.");
                return 1;
            }

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

        // 260912_웨이브 이미지는 그리드 비율과 같아야 한다.
        // 가림막과 보상은 둘 다 그리드 크기로 늘려 깔리므로, 원본 비율이 다르면 그림이 눌린다.
        // 둘 다 똑같이 눌리기 때문에 '한쪽만 이상하다'로는 안 보이고 그냥 그림이 어색해진다 —
        // 눈으로 찾기 어려운 종류라 여기서 숫자로 잡아 준다.
        private static int Validate_LayerAspect()
        {
            CMapInfo cMapInfo = Load_MapInfo(1);
            float fGridAspect = (float)cMapInfo.iGridWidth / cMapInfo.iGridHeight;

            int iFail = 0;
            for (int i = 0; i < ARR_LAYER_TEX.Length; ++i)
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{DIR_ART}/{ARR_LAYER_TEX[i]}.png");
                if (tex == null)
                    continue;   // 존재 여부는 Validate_LayerTextures가 본다

                float fTexAspect = (float)tex.width / tex.height;
                if (Mathf.Abs(fTexAspect - fGridAspect) <= 0.01f)
                    continue;

                Debug.LogError($"  FAIL  {ARR_LAYER_TEX[i]} 비율이 그리드와 다릅니다 "
                             + $"({tex.width}x{tex.height} = {fTexAspect:F3} / 그리드 {fGridAspect:F3}). "
                             + "그림이 눌려 보입니다.");
                ++iFail;
            }

            return iFail;
        }

        // 260905_옵션 에셋. 없어도 게임은 기본값으로 돌지만, 값을 바꿔도 반영이 안 되는
        // 상황을 눈치채기 어려우므로 여기서 알려 준다.
        private static int Validate_GameConfig()
        {
            const string PATH = "Assets/Resources/" + CGameConfig.ASSET_NAME + ".asset";

            CGameConfig cConfig = AssetDatabase.LoadAssetAtPath<CGameConfig>(PATH);
            if (cConfig == null)
            {
                Debug.LogError($"  FAIL  옵션 에셋 없음 : {PATH}");
                return 1;
            }

            Debug.Log($"  PASS  GameConfig — 속도 x{cConfig.PLAYER_SPEED_SCALE:0.##}"
                    + $" / 전체해금 {cConfig.UNLOCK_ALL_STAGE} / 무료 {cConfig.FREE_SPEND}"
                    + $" / 시작코인 {cConfig.START_COIN}"
                    + $" / 흔들림 {cConfig.CAMERA_SHAKE_ENABLED}(피격{cConfig.TRAUMA_ON_HIT:0.##}"
                    + $"·사망{cConfig.TRAUMA_ON_DEATH:0.##})"
                    + $" / 펀치 {cConfig.CAMERA_PUNCH_ENABLED}(점령{cConfig.PUNCH_ON_CAPTURE:0.##})"
                    + $" / 플래시 {cConfig.SCREEN_FLASH_ENABLED}"
                    + $" / 효과음 {cConfig.SFX_ENABLED}(볼륨{cConfig.SFX_VOLUME:0.##})"
                    + $" / 햅틱 {cConfig.HAPTIC_ENABLED}");
            return 0;
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

            // 260912_배치 이미지는 그리드 비율에 정확히 맞춘다.
            // 가림막과 보상은 둘 다 그리드 크기로 늘려 깔리므로(CGridRenderer.Fit_ToGrid),
            // 원본 비율이 그리드와 다르면 그림이 눌려 보인다.
            // 칸 하나가 정수 픽셀이 되게 맞추면 사본을 찍을 때 줄이 생기지도 않는다.
            CMapInfo cMapInfo = Load_MapInfo(1);
            int iBgW = cMapInfo.iGridWidth  * TEX_PIXEL_PER_CELL;
            int iBgH = cMapInfo.iGridHeight * TEX_PIXEL_PER_CELL;

            Write_Png(PATH_TEX_BG, Make_RewardPlaceholder(iBgW, iBgH));
            Import_AsSprite(PATH_TEX_BG, 100, true);

            Create_LayerTextures(iBgW, iBgH);
            Create_CardTextures();

            // 260904_기믹 소환물. 탄은 작고 밝게, 거미줄은 성기게 비치도록 반투명하게.
            // 260917_흰색으로 굽는다. 적탄 · 플레이어 탄 색은 CProjectile이 런타임에 칠한다.
            Write_Png(PATH_TEX_PROJECTILE, Make_CircleTexture(32, Color.white));
            Import_AsSprite(PATH_TEX_PROJECTILE, 32);

            Write_Png(PATH_TEX_WEB, Make_WebTexture(64));
            Import_AsSprite(PATH_TEX_WEB, 64);

            // 260916_런 스킬 '영혼 수집가' 픽업. 기존 원형 텍스처 생성기를 그대로 쓴다.
            Write_Png(PATH_TEX_SOUL, Make_CircleTexture(28, new Color(0.6f, 0.95f, 1f)));
            Import_AsSprite(PATH_TEX_SOUL, 28);

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
        }

        // 260912_카드 아이콘과 테두리 발광.
        // 아이콘은 종류를 한눈에 가르는 것이 목적이라 모양과 색만 다르게 그린다.
        private static void Create_CardTextures()
        {
            const int ICON = 128;

            for (int i = 0; i < ARR_CARD_ICON.Length; ++i)
            {
                string strPath = $"{DIR_ART}/{ARR_CARD_ICON[i]}.png";
                Write_Png(strPath, Make_CardIcon(ICON, i));
                Import_AsSprite(strPath, 100);
            }

            for (int i = 0; i < ARR_RUN_SKILL_ICON.Length; ++i)
            {
                string strPath = $"{DIR_ART}/{ARR_RUN_SKILL_ICON[i]}.png";
                Write_Png(strPath, Make_CardIcon(ICON, RUN_SKILL_ICON_KIND_START + i));
                Import_AsSprite(strPath, 100);
            }

            // 260918_장비 부위 아이콘 (흰색 — 가방이 상태에 따라 색을 곱한다)
            for (int i = 0; i < ARR_EQUIP_ICON.Length; ++i)
            {
                string strPath = $"{DIR_ART}/{ARR_EQUIP_ICON[i]}.png";
                Write_Png(strPath, Make_CardIcon(ICON, EQUIP_ICON_KIND_START + i));
                Import_AsSprite(strPath, 100);
            }

            // 테두리 발광 — 9슬라이스로 늘려 쓰므로 가운데는 비워 둔다.
            string strGlow = $"{DIR_ART}/{TEX_CARD_GLOW}.png";
            Write_Png(strGlow, Make_CardGlow(64));
            Import_AsSprite(strGlow, 100);
            Set_SpriteBorder(strGlow, 20);
        }

        /// <summary> 카드 종류별 아이콘. 색과 모양만 달라도 셋을 가르는 데는 충분하다. </summary>
        private static Texture2D Make_CardIcon(int iSize, int iKind)
        {
            Texture2D tex = new Texture2D(iSize, iSize, TextureFormat.RGBA32, false);

            Color[] arrColor =
            {
                new Color(0.45f, 0.80f, 1.00f),     // SHIELD  — 푸른 방패
                new Color(0.45f, 1.00f, 0.60f),     // HEAL    — 초록 십자
                new Color(1.00f, 0.85f, 0.35f),     // SPEED   — 노란 화살
                new Color(0.80f, 0.60f, 1.00f),     // EVASION — 보라 잔상
                new Color(1.00f, 0.55f, 0.55f),     // SLOW    — 붉은 모래시계
            };
            // 260917_런 스킬 아이콘은 흰색 — 분류 색을 런타임에 곱해 칠한다.
            Color cInk = iKind < arrColor.Length ? arrColor[Mathf.Max(0, iKind)] : Color.white;

            float fHalf = iSize * 0.5f;

            for (int y = 0; y < iSize; ++y)
            {
                for (int x = 0; x < iSize; ++x)
                {
                    float fU = (x + 0.5f - fHalf) / fHalf;      // -1 ~ 1
                    float fV = (y + 0.5f - fHalf) / fHalf;
                    bool bInk = Is_IconInk(iKind, fU, fV);

                    Color cColor = cInk;
                    cColor.a = bInk == true ? 1f : 0f;
                    tex.SetPixel(x, y, cColor);
                }
            }

            tex.Apply();
            return tex;
        }

        // 모양만 다르게 — 아이콘 다섯 개를 한 함수에서 가른다.
        private static bool Is_IconInk(int iKind, float fU, float fV)
        {
            float fAbsU = Mathf.Abs(fU);

            switch (iKind)
            {
                case 0:     // 방패 — 위는 각지고 아래는 뾰족하다
                    return fAbsU < 0.62f && fV < 0.66f && fV > -0.88f + fAbsU * 0.9f;

                case 1:     // 십자
                    return (fAbsU < 0.22f && Mathf.Abs(fV) < 0.68f)
                        || (Mathf.Abs(fV) < 0.22f && fAbsU < 0.68f);

                case 2:     // 오른쪽을 가리키는 겹화살
                    return Is_Chevron(fU - 0.18f, fV) || Is_Chevron(fU + 0.30f, fV);

                case 3:     // 잔상 — 세로 막대 셋, 오른쪽으로 갈수록 굵다
                    return (Mathf.Abs(fU + 0.52f) < 0.07f && Mathf.Abs(fV) < 0.34f)
                        || (Mathf.Abs(fU) < 0.11f && Mathf.Abs(fV) < 0.5f)
                        || (Mathf.Abs(fU - 0.52f) < 0.16f && Mathf.Abs(fV) < 0.66f);

                case 4:     // 모래시계
                    if (Mathf.Abs(fV) > 0.72f)
                        return fAbsU < 0.5f;

                    return fAbsU < Mathf.Abs(fV) * 0.68f + 0.05f;

                default:
                    if (iKind >= EQUIP_ICON_KIND_START)
                        return Is_EquipInk(iKind - EQUIP_ICON_KIND_START, fU, fV);
                    return Is_RunSkillInk(iKind - RUN_SKILL_ICON_KIND_START, fU, fV);
            }
        }

        // 260918_장비 부위 넷 — 신발 · 가방 · 목걸이 · 소모품
        private static bool Is_EquipInk(int iSlot, float fU, float fV)
        {
            float fAbsU = Mathf.Abs(fU);
            float fDist = Mathf.Sqrt(fU * fU + fV * fV);

            switch (iSlot)
            {
                case 0:     // 신발 — 세로 발목 + 가로 발바닥
                    return (fU > -0.45f && fU < -0.05f && fV > -0.35f && fV < 0.65f)
                        || (fU > -0.45f && fU < 0.62f && fV > -0.62f && fV < -0.25f);

                case 1:     // 가방 — 몸통 + 위 손잡이 고리
                {
                    bool bBody   = fAbsU < 0.58f && fV > -0.62f && fV < 0.28f;
                    float fHandle = Mathf.Sqrt(fU * fU + (fV - 0.3f) * (fV - 0.3f));
                    bool bHandle = fV >= 0.28f && fHandle > 0.2f && fHandle < 0.34f;
                    return bBody || bHandle;
                }

                case 2:     // 목걸이 — 위가 열린 줄 + 아래 보석
                {
                    bool bChain = fDist > 0.5f && fDist < 0.6f && fV < 0.35f;
                    float fGem = Mathf.Abs(fU) + Mathf.Abs(fV + 0.55f);
                    return bChain || fGem < 0.22f;
                }

                default:    // 소모품 — 카드 보호막 모양을 그대로 쓴다
                    return Is_IconInk(0, fU, fV);
            }
        }

        // 260917_런 스킬 여덟 모양. 순서는 ARR_RUN_SKILL_ICON(= RUN_SKILL_TYPE)과 같다.
        private static bool Is_RunSkillInk(int iSkill, float fU, float fV)
        {
            float fAbsU = Mathf.Abs(fU);
            float fAbsV = Mathf.Abs(fV);
            float fDist = Mathf.Sqrt(fU * fU + fV * fV);

            switch (iSkill)
            {
                case 0:     // 월보 — 초승달
                {
                    float fInner = Mathf.Sqrt((fU - 0.28f) * (fU - 0.28f) + (fV - 0.1f) * (fV - 0.1f));
                    return fDist < 0.68f && fInner > 0.5f;
                }

                case 1:     // 어디로든 신발 — 양쪽 화살(좌우를 잇는다)
                    return (fAbsV < 0.1f && fAbsU < 0.55f)
                        || Is_Chevron(fU - 0.25f, fV * 1.4f)
                        || Is_Chevron(-fU - 0.25f, fV * 1.4f);

                case 2:     // 영혼 수집가 — 아래는 둥글고 위로 뾰족한 불꽃
                    return (fDist < 0.42f && fV < 0.1f)
                        || (fV >= 0.1f && fV < 0.75f && fAbsU < (0.75f - fV) * 0.62f);

                case 3:     // 분노 — 번개
                    return (fV > 0f && fV < 0.72f && Mathf.Abs(fU - fV * 0.5f + 0.05f) < 0.14f)
                        || (fAbsV < 0.09f && fU > -0.3f && fU < 0.3f)
                        || (fV < 0f && fV > -0.72f && Mathf.Abs(fU - fV * 0.5f - 0.05f) < 0.14f);

                case 4:     // 자석 — 말굽(U)
                    if (fV > 0f)
                        return fAbsU > 0.3f && fAbsU < 0.62f && fV < 0.7f;

                    return fDist > 0.3f && fDist < 0.62f;

                case 5:     // 회피 — 카드 회피와 같은 잔상(색으로 가른다)
                    return Is_IconInk(3, fU, fV);

                case 6:     // 회전탄 — 고리와 그 위를 도는 탄 둘
                {
                    bool bRing = fDist > 0.46f && fDist < 0.56f;
                    float fDotA = Mathf.Sqrt((fU - 0.36f) * (fU - 0.36f) + (fV - 0.36f) * (fV - 0.36f));
                    float fDotB = Mathf.Sqrt((fU + 0.36f) * (fU + 0.36f) + (fV + 0.36f) * (fV + 0.36f));
                    return bRing || fDotA < 0.17f || fDotB < 0.17f || fDist < 0.14f;
                }

                // 260917_투사체 무기 넷
                case 8:     // 마법탄 — 마름모 별(가운데가 빈 사방 별)
                    return fAbsU + fAbsV < 0.72f && (fAbsU < 0.12f || fAbsV < 0.12f || fAbsU + fAbsV < 0.3f);

                case 9:     // 레이저 — 왼쪽 원점에서 오른쪽으로 뻗는 굵은 빔
                    return (fAbsV < 0.13f && fU > -0.45f && fU < 0.78f)
                        || Mathf.Sqrt((fU + 0.55f) * (fU + 0.55f) + fV * fV) < 0.25f;

                case 10:    // 부메랑 — 꺾인 ㄱ자 두 날
                    return (fV > -0.1f && fV < 0.12f && fU > -0.7f && fU < 0.35f)
                        || (fU > 0.13f && fU < 0.35f && fV > -0.7f && fV < 0.12f);

                case 11:    // 튕기는 탄 — 지그재그 궤적과 끝의 탄
                {
                    float fZig = Mathf.Abs(Mathf.Repeat(fU * 1.6f + 1.6f, 1f) - 0.5f) * 0.9f - 0.2f;
                    bool bTrail = fU < 0.35f && fU > -0.75f && Mathf.Abs(fV - fZig) < 0.1f;
                    return bTrail || Mathf.Sqrt((fU - 0.52f) * (fU - 0.52f) + (fV - 0.25f) * (fV - 0.25f)) < 0.2f;
                }

                // 260918_마비 둘
                case 12:    // 마비탄 — 탄 하나와 그 둘레의 짧은 번개 넷
                {
                    bool bCore = fDist < 0.26f;
                    bool bSpark = (Mathf.Abs(fU) < 0.07f && fAbsV > 0.38f && fAbsV < 0.72f)
                               || (Mathf.Abs(fV) < 0.07f && fAbsU > 0.38f && fAbsU < 0.72f);
                    return bCore || bSpark;
                }

                case 13:    // 전체 마비 — 가운데 점과 퍼지는 고리 둘
                    return fDist < 0.14f || (fDist > 0.34f && fDist < 0.44f) || (fDist > 0.62f && fDist < 0.72f);

                default:    // 몽둥이 — 대각선 자루와 굵은 머리
                {
                    float fAlong  = (fU + fV) * 0.7071f;
                    float fAcross = Mathf.Abs(fU - fV) * 0.7071f;
                    return fAlong > -0.72f && fAlong < 0.72f && fAcross < 0.09f + Mathf.Max(0f, fAlong - 0.15f) * 0.45f;
                }
            }
        }

        private static bool Is_Chevron(float fU, float fV)
        {
            float fEdge = 0.5f - Mathf.Abs(fV) * 0.62f;      // 위아래로 갈수록 왼쪽으로
            return fU < fEdge && fU > fEdge - 0.2f && Mathf.Abs(fV) < 0.62f;
        }

        /// <summary> 카드 테두리 발광 — 바깥은 투명하고 테두리에서 안으로 옅어진다. </summary>
        private static Texture2D Make_CardGlow(int iSize)
        {
            Texture2D tex = new Texture2D(iSize, iSize, TextureFormat.RGBA32, false);
            const float RIM = 12f;      // 테두리 두께(픽셀)

            for (int y = 0; y < iSize; ++y)
            {
                for (int x = 0; x < iSize; ++x)
                {
                    // 네 변 중 가장 가까운 변까지의 거리
                    float fEdge = Mathf.Min(Mathf.Min(x, iSize - 1 - x), Mathf.Min(y, iSize - 1 - y));
                    float fGlow = Mathf.Clamp01(1f - fEdge / RIM);

                    // 바깥 1픽셀은 선명한 테두리, 안으로 갈수록 부드럽게 사라진다
                    float fAlpha = fEdge < 1.5f ? 1f : fGlow * fGlow * 0.7f;

                    Color cColor = Color.white;
                    cColor.a = fAlpha;
                    tex.SetPixel(x, y, cColor);
                }
            }

            tex.Apply();
            return tex;
        }

        // 260912_9슬라이스 경계. 가운데를 늘려도 테두리 두께가 그대로 유지된다.
        private static void Set_SpriteBorder(string strPath, int iBorder)
        {
            TextureImporter cImporter = AssetImporter.GetAtPath(strPath) as TextureImporter;
            if (cImporter == null)
                return;

            cImporter.spriteBorder = new Vector4(iBorder, iBorder, iBorder, iBorder);
            cImporter.SaveAndReimport();
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
                    // 260912_가로를 비율만큼 늘려서 재야 화면에서 '진짜 원'으로 보인다.
                    // 정규화 좌표에서 그냥 재면 세로로 긴 타원이 되어, 그림이 눌린 것인지
                    // 원래 그렇게 그린 것인지 구분할 수 없다 — 눈으로 확인할 수 있는 기준을 남긴다.
                    float fAspect = (float)iWidth / iHeight;
                    float fDist = Vector2.Distance(new Vector2(fU * fAspect, fT),
                                                   new Vector2(0.5f * fAspect, 0.6f));
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

            // 260912_스프라이트 영역을 텍스처 전체로 못 박는다.
            // 기본값(Tight)은 투명한 가장자리를 잘라내 스프라이트가 텍스처보다 작아진다.
            // 가림막과 보상은 둘 다 bounds를 기준으로 그리드에 맞춰 깔리므로(Fit_ToGrid),
            // 한쪽만 잘려 있으면 같은 그리드에 맞춰도 그림이 서로 다른 크기로 보인다.
            TextureImporterSettings cSettings = new TextureImporterSettings();
            cImporter.ReadTextureSettings(cSettings);
            cSettings.spriteMeshType = SpriteMeshType.FullRect;
            cSettings.spriteExtrude  = 0;
            cImporter.SetTextureSettings(cSettings);
            cImporter.spriteBorder = Vector4.zero;

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
            Create_ActorPrefab<CSoul>("Prefab_Soul", PATH_TEX_SOUL, PATH_PREFAB_SOUL, 14);
            Create_StageSelectUI();
            Create_InGameUI();
            Create_LobbyUI();
            Create_UpgradeUI();
            Create_ShopUI();
            Create_InventoryUI();
            Create_PopupUI();
            Create_CardViewerUI();
            Create_CardPickUI();
            Create_CardUI();
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
            Regist_Addressable(cSettings, PATH_PREFAB_SOUL, "Prefab_Soul", CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_UI_SELECT, UI_STAGE_SELECT, CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_UI_INGAME, UI_INGAME, CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_UI_LOBBY, UI_LOBBY, CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_UI_UPGRADE, UI_UPGRADE, CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_UI_SHOP, UI_SHOP, CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_UI_INVEN, UI_INVENTORY, CAddressableLabel.PREFAB);
            for (int i = 0; i < ARR_CARD_ICON.Length; ++i)
            {
                Regist_Addressable(cSettings, $"{DIR_ART}/{ARR_CARD_ICON[i]}.png",
                                   ARR_CARD_ICON[i], CAddressableLabel.TEXTURE);
            }
            Regist_Addressable(cSettings, $"{DIR_ART}/{TEX_CARD_GLOW}.png",
                               TEX_CARD_GLOW, CAddressableLabel.TEXTURE);

            Regist_Addressable(cSettings, PATH_PREFAB_UI_CARD, UI_CARDPICK, CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_UI_CARD_GALLERY, UI_CARD_GALLERY, CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_UI_CARD_VIEWER, UI_CARD_VIEWER, CAddressableLabel.PREFAB);
            Regist_Addressable(cSettings, PATH_PREFAB_UI_POPUP, UI_POPUP, CAddressableLabel.PREFAB);

            // 260904_웨이브 이미지 스택과 모양 마스크. 주소를 파일명과 맞춰야 CSV에 적은 이름으로 찾을 수 있다.
            for (int i = 0; i < ARR_LAYER_TEX.Length; ++i)
                Regist_Addressable(cSettings, $"{DIR_ART}/{ARR_LAYER_TEX[i]}.png", ARR_LAYER_TEX[i], CAddressableLabel.TEXTURE);


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
        // 260905_로비 — 상단 재화 / 가운데 탭 화면 자리 / 하단 탭바
        private static void Create_LobbyUI()
        {
            GameObject goRoot = Create_UIObject(UI_LOBBY, null);
            Stretch_Full(goRoot.GetComponent<RectTransform>());

            GameObject goBG = Create_UIObject("Panel", goRoot.transform);
            Stretch_Full(goBG.GetComponent<RectTransform>());
            goBG.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.09f, 1f);

            // 260912_내용은 안전 영역 안에서만 논다. 하단 탭바가 홈 인디케이터에 걸리면
            // 탭이 눌리지 않는다 — 배경(goBG)은 일부러 밖에 두어 화면 끝까지 칠한다.
            GameObject goSafe = Create_UIObject("SafeArea", goRoot.transform);
            Stretch_Full(goSafe.GetComponent<RectTransform>());
            goSafe.AddComponent<CSafeArea>();

            // 상단 재화 바
            GameObject goTop = Create_UIObject("TopBar", goSafe.transform);
            RectTransform trTop = goTop.GetComponent<RectTransform>();
            trTop.anchorMin = new Vector2(0f, 1f);
            trTop.anchorMax = new Vector2(1f, 1f);
            trTop.pivot     = new Vector2(0.5f, 1f);
            trTop.offsetMin = new Vector2(0f, -130f);
            trTop.offsetMax = new Vector2(0f, 0f);
            goTop.AddComponent<Image>().color = new Color(0.10f, 0.13f, 0.22f, 1f);

            GameObject goStar = Create_UIObject("Txt_Star", goTop.transform);
            RectTransform trStar = goStar.GetComponent<RectTransform>();
            trStar.anchorMin = new Vector2(0f, 0f);
            trStar.anchorMax = new Vector2(0.5f, 1f);
            trStar.offsetMin = new Vector2(32f, 0f);
            trStar.offsetMax = Vector2.zero;
            Text txtStar = Make_Text(goStar, "\u2605 0", 34, TextAnchor.MiddleLeft);
            txtStar.raycastTarget = false;

            GameObject goCoin = Create_UIObject("Txt_Coin", goTop.transform);
            RectTransform trCoin = goCoin.GetComponent<RectTransform>();
            trCoin.anchorMin = new Vector2(0.5f, 0f);
            trCoin.anchorMax = new Vector2(1f, 1f);
            trCoin.offsetMin = Vector2.zero;
            trCoin.offsetMax = new Vector2(-32f, 0f);
            Text txtCoin = Make_Text(goCoin, "\ucf54\uc778 0", 34, TextAnchor.MiddleRight);
            txtCoin.raycastTarget = false;

            // 가운데 — 탭 화면이 열릴 자리. 위아래로 바를 피해 둔다.
            GameObject goContent = Create_UIObject("Content", goSafe.transform);
            RectTransform trContent = goContent.GetComponent<RectTransform>();
            trContent.anchorMin = new Vector2(0f, 0f);
            trContent.anchorMax = new Vector2(1f, 1f);
            trContent.offsetMin = new Vector2(0f, 200f);
            trContent.offsetMax = new Vector2(0f, -130f);

            // 하단 탭바 — 4칸 균등
            GameObject goTabBar = Create_UIObject("TabBar", goSafe.transform);
            RectTransform trTabBar = goTabBar.GetComponent<RectTransform>();
            trTabBar.anchorMin = new Vector2(0f, 0f);
            trTabBar.anchorMax = new Vector2(1f, 0f);
            trTabBar.pivot     = new Vector2(0.5f, 0f);
            trTabBar.offsetMin = new Vector2(0f, 0f);
            trTabBar.offsetMax = new Vector2(0f, 200f);
            goTabBar.AddComponent<Image>().color = new Color(0.08f, 0.10f, 0.17f, 1f);

            HorizontalLayoutGroup cTabLayout = goTabBar.AddComponent<HorizontalLayoutGroup>();
            cTabLayout.spacing = 8f;
            cTabLayout.padding = new RectOffset(8, 8, 12, 12);
            cTabLayout.childForceExpandWidth  = true;
            cTabLayout.childForceExpandHeight = true;
            cTabLayout.childControlWidth      = true;
            cTabLayout.childControlHeight     = true;

            // 260918_LOBBY_TAB \uc21c\uc11c(\uc804\ud22c/\uac15\ud654/\uac00\ubc29/\uc0c1\uc810/\uce74\ub4dc)\uc640 1:1\ub85c \ub9de\ucd98\ub2e4.
            string[] arrTabName = { "\uc804\ud22c", "\uac15\ud654", "\uac00\ubc29", "\uc0c1\uc810", "\uce74\ub4dc" };
            Button[] arrTabButton = new Button[arrTabName.Length];

            for (int i = 0; i < arrTabName.Length; ++i)
            {
                GameObject goTab = Create_UIObject($"Btn_Tab_{i}", goTabBar.transform);
                goTab.AddComponent<Image>().color = new Color(0.13f, 0.16f, 0.26f, 1f);
                arrTabButton[i] = goTab.AddComponent<Button>();

                GameObject goTabLabel = Create_UIObject("Label", goTab.transform);
                Stretch_Full(goTabLabel.GetComponent<RectTransform>());
                Make_Text(goTabLabel, arrTabName[i], 32, TextAnchor.MiddleCenter).raycastTarget = false;
            }

            CUI_Lobby cUI = goRoot.AddComponent<CUI_Lobby>();
            SerializedObject cSerialized = new SerializedObject(cUI);
            cSerialized.FindProperty("m_trContent").objectReferenceValue = trContent;
            cSerialized.FindProperty("m_txtCoin").objectReferenceValue   = txtCoin;
            cSerialized.FindProperty("m_txtStar").objectReferenceValue   = txtStar;

            SerializedProperty cArray = cSerialized.FindProperty("m_arrTabButton");
            cArray.arraySize = arrTabButton.Length;
            for (int i = 0; i < arrTabButton.Length; ++i)
                cArray.GetArrayElementAtIndex(i).objectReferenceValue = arrTabButton[i];

            cSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(goRoot, PATH_PREFAB_UI_LOBBY);
            Object.DestroyImmediate(goRoot);
        }

        // 260905_강화 화면. 목록은 UpgradeInfo.csv를 보고 런타임에 만든다.
        // 260905_강화 · 상점 · 인벤토리는 '제목 + 세로 목록 + 버튼 템플릿'으로 모양이 같다.
        // 프리팹 빌더를 하나로 모아 두면 화면이 늘어도 이 함수만 부르면 된다.
        private static void Create_ListUI<T>(string strName, string strPath, string strTitle,
                                             float fRowHeight) where T : Component
        {
            GameObject goRoot = Create_UIObject(strName, null);
            Stretch_Full(goRoot.GetComponent<RectTransform>());

            GameObject goTitle = Create_UIObject("Title", goRoot.transform);
            RectTransform trTitle = goTitle.GetComponent<RectTransform>();
            trTitle.anchorMin = new Vector2(0f, 1f);
            trTitle.anchorMax = new Vector2(1f, 1f);
            trTitle.pivot     = new Vector2(0.5f, 1f);
            trTitle.offsetMin = new Vector2(32f, -100f);
            trTitle.offsetMax = new Vector2(-32f, -20f);
            Text txtTitle = Make_Text(goTitle, strTitle, 38, TextAnchor.MiddleLeft);

            GameObject goContent = Create_UIObject("Content", goRoot.transform);
            RectTransform trContent = goContent.GetComponent<RectTransform>();
            trContent.anchorMin = new Vector2(0f, 0f);
            trContent.anchorMax = new Vector2(1f, 1f);
            trContent.offsetMin = new Vector2(32f, 20f);
            trContent.offsetMax = new Vector2(-32f, -110f);

            VerticalLayoutGroup cLayout = goContent.AddComponent<VerticalLayoutGroup>();
            cLayout.spacing              = 14f;
            cLayout.childAlignment       = TextAnchor.UpperCenter;
            cLayout.childForceExpandWidth  = true;
            cLayout.childForceExpandHeight = false;
            cLayout.childControlWidth      = true;
            cLayout.childControlHeight     = false;

            GameObject goButton = Create_UIObject("Btn_Template", goContent.transform);
            RectTransform trButton = goButton.GetComponent<RectTransform>();
            trButton.sizeDelta = new Vector2(0f, fRowHeight);
            goButton.AddComponent<LayoutElement>().minHeight = fRowHeight;
            goButton.AddComponent<Image>().color = new Color(0.16f, 0.20f, 0.34f, 1f);
            Button cButton = goButton.AddComponent<Button>();

            GameObject goLabel = Create_UIObject("Label", goButton.transform);
            Stretch_Full(goLabel.GetComponent<RectTransform>());
            Make_Text(goLabel, "ITEM", 28, TextAnchor.MiddleCenter);

            goButton.SetActive(false);       // 템플릿은 항상 꺼 둔다

            T cUI = goRoot.AddComponent<T>();
            SerializedObject cSerialized = new SerializedObject(cUI);
            cSerialized.FindProperty("m_trContent").objectReferenceValue   = goContent.transform;
            cSerialized.FindProperty("m_btnTemplate").objectReferenceValue = cButton;
            cSerialized.FindProperty("m_txtTitle").objectReferenceValue    = txtTitle;
            cSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(goRoot, strPath);
            Object.DestroyImmediate(goRoot);
        }

        // 260905_강화 화면. 목록은 UpgradeInfo.csv를 보고 런타임에 만든다.
        private static void Create_UpgradeUI()
        {
            Create_ListUI<CUI_Upgrade>(UI_UPGRADE, PATH_PREFAB_UI_UPGRADE, "\ub2a5\ub825\uce58 \uac15\ud654", 130f);
        }

        // 260905_인벤토리. 공용 목록 위에 안쪽 탭(장비/스킬) 두 개를 얹는다.
        private static void Create_InventoryUI()
        {
            // 260918_네 구역으로 다시 짰다 — 위 패널(A 장착 캐릭터 + B 부위별 슬롯) · C 보유 격자 · D 탭. 뽑기는 상점에 있다.
            // 로비 Content 안에 깔리므로 세로 기준 약 1590px 높이를 전제로 위아래를 고정 픽셀로 나눈다.
            GameObject goRoot = Create_UIObject(UI_INVENTORY, null);
            Stretch_Full(goRoot.GetComponent<RectTransform>());

            Color cPanel = new Color(0.10f, 0.12f, 0.20f, 1f);

            // ---------------- 위 패널 (A + B) ----------------
            GameObject goTop = Create_UIObject("Panel_Top", goRoot.transform);
            RectTransform trTop = goTop.GetComponent<RectTransform>();
            trTop.anchorMin = new Vector2(0f, 1f);
            trTop.anchorMax = new Vector2(1f, 1f);
            trTop.pivot     = new Vector2(0.5f, 1f);
            trTop.offsetMin = new Vector2(24f, -600f);
            trTop.offsetMax = new Vector2(-24f, -16f);
            goTop.AddComponent<Image>().color = cPanel;

            // A — 가운데 캐릭터
            GameObject goChar = Create_UIObject("Btn_Character", goTop.transform);
            RectTransform trChar = goChar.GetComponent<RectTransform>();
            trChar.anchorMin = new Vector2(0.28f, 0.04f);
            trChar.anchorMax = new Vector2(0.72f, 0.96f);
            trChar.offsetMin = Vector2.zero;
            trChar.offsetMax = Vector2.zero;
            goChar.AddComponent<Image>().color = new Color(0.16f, 0.19f, 0.30f, 1f);
            Button cCharButton = goChar.AddComponent<Button>();

            GameObject goCharImg = Create_UIObject("Img_Character", goChar.transform);
            RectTransform trCharImg = goCharImg.GetComponent<RectTransform>();
            trCharImg.anchorMin = new Vector2(0.1f, 0.2f);
            trCharImg.anchorMax = new Vector2(0.9f, 0.95f);
            trCharImg.offsetMin = Vector2.zero;
            trCharImg.offsetMax = Vector2.zero;
            Image imgChar = goCharImg.AddComponent<Image>();
            imgChar.preserveAspect = true;
            imgChar.raycastTarget  = false;

            GameObject goCharTxt = Create_UIObject("Txt_Character", goChar.transform);
            RectTransform trCharTxt = goCharTxt.GetComponent<RectTransform>();
            trCharTxt.anchorMin = new Vector2(0f, 0f);
            trCharTxt.anchorMax = new Vector2(1f, 0.2f);
            trCharTxt.offsetMin = Vector2.zero;
            trCharTxt.offsetMax = Vector2.zero;
            Text txtChar = Make_Text(goCharTxt, "캐릭터", 30, TextAnchor.MiddleCenter);
            txtChar.raycastTarget = false;

            // B — 왼쪽(신발 · 가방) / 오른쪽(목걸이 · 소모품). EQUIP_SLOT 순서로 배열에 담는다.
            Vector2[] arrSlotMin = { new Vector2(0.02f, 0.52f), new Vector2(0.02f, 0.04f),
                                     new Vector2(0.74f, 0.52f), new Vector2(0.74f, 0.04f) };
            Button[] arrSlot = new Button[ARR_EQUIP_ICON.Length];
            for (int i = 0; i < arrSlot.Length; ++i)
            {
                GameObject goSlot = Make_InventoryCell($"Btn_Slot_{i}", goTop.transform, false);
                RectTransform trSlot = goSlot.GetComponent<RectTransform>();
                trSlot.anchorMin = arrSlotMin[i];
                trSlot.anchorMax = arrSlotMin[i] + new Vector2(0.24f, 0.44f);
                trSlot.offsetMin = Vector2.zero;
                trSlot.offsetMax = Vector2.zero;
                arrSlot[i] = goSlot.GetComponent<Button>();
            }

            // ---------------- C — 보유 목록 ----------------
            GameObject goList = Create_UIObject("Panel_List", goRoot.transform);
            RectTransform trList = goList.GetComponent<RectTransform>();
            trList.anchorMin = new Vector2(0f, 0f);
            trList.anchorMax = new Vector2(1f, 1f);
            trList.offsetMin = new Vector2(24f, 150f);
            trList.offsetMax = new Vector2(-24f, -620f);
            goList.AddComponent<Image>().color = cPanel;

            GameObject goTitle = Create_UIObject("Txt_Title", goList.transform);
            RectTransform trTitle = goTitle.GetComponent<RectTransform>();
            trTitle.anchorMin = new Vector2(0f, 1f);
            trTitle.anchorMax = new Vector2(1f, 1f);
            trTitle.pivot     = new Vector2(0.5f, 1f);
            trTitle.offsetMin = new Vector2(24f, -80f);
            trTitle.offsetMax = new Vector2(-24f, 0f);
            Text txtTitle = Make_Text(goTitle, "장비", 32, TextAnchor.MiddleLeft);

            GameObject goViewport = Create_UIObject("Viewport", goList.transform);
            RectTransform trViewport = goViewport.GetComponent<RectTransform>();
            trViewport.anchorMin = new Vector2(0f, 0f);
            trViewport.anchorMax = new Vector2(1f, 1f);
            trViewport.offsetMin = new Vector2(16f, 16f);
            trViewport.offsetMax = new Vector2(-16f, -86f);
            goViewport.AddComponent<RectMask2D>();
            goViewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);     // 드래그를 받을 자리

            GameObject goGrid = Create_UIObject("Content", goViewport.transform);
            RectTransform trGrid = goGrid.GetComponent<RectTransform>();
            trGrid.anchorMin = new Vector2(0f, 1f);
            trGrid.anchorMax = new Vector2(1f, 1f);
            trGrid.pivot     = new Vector2(0.5f, 1f);
            trGrid.offsetMin = Vector2.zero;
            trGrid.offsetMax = Vector2.zero;
            GridLayoutGroup cGrid = goGrid.AddComponent<GridLayoutGroup>();
            cGrid.cellSize        = new Vector2(180f, 220f);
            cGrid.spacing         = new Vector2(16f, 16f);
            cGrid.constraint      = GridLayoutGroup.Constraint.FixedColumnCount;
            cGrid.constraintCount = 5;
            cGrid.childAlignment  = TextAnchor.UpperCenter;
            ContentSizeFitter cFitter = goGrid.AddComponent<ContentSizeFitter>();
            cFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect cScroll = goList.AddComponent<ScrollRect>();
            cScroll.content    = trGrid;
            cScroll.viewport   = trViewport;
            cScroll.horizontal = false;
            cScroll.vertical   = true;
            cScroll.movementType = ScrollRect.MovementType.Clamped;

            GameObject goTemplate = Make_InventoryCell("Btn_Template", goGrid.transform, true);
            goTemplate.SetActive(false);        // 템플릿은 항상 꺼 둔다

            // ---------------- D — 탭 + 뽑기 ----------------
            GameObject goTabBar = Create_UIObject("Panel_Tabs", goRoot.transform);
            RectTransform trTabBar = goTabBar.GetComponent<RectTransform>();
            trTabBar.anchorMin = new Vector2(0f, 0f);
            trTabBar.anchorMax = new Vector2(1f, 0f);
            trTabBar.pivot     = new Vector2(0.5f, 0f);
            trTabBar.offsetMin = new Vector2(24f, 16f);
            trTabBar.offsetMax = new Vector2(-24f, 136f);
            HorizontalLayoutGroup cLayout = goTabBar.AddComponent<HorizontalLayoutGroup>();
            cLayout.spacing = 12f;
            cLayout.childForceExpandWidth  = true;
            cLayout.childForceExpandHeight = true;
            cLayout.childControlWidth      = true;
            cLayout.childControlHeight     = true;

            // INVENTORY_TAB 순서(장비/캐릭터/펫)와 1:1
            string[] arrTabName = { "장비", "캐릭터", "펫" };
            Button[] arrTabButton = new Button[arrTabName.Length];
            for (int i = 0; i < arrTabName.Length; ++i)
                arrTabButton[i] = Make_TabButton($"Btn_InnerTab_{i}", goTabBar.transform, arrTabName[i],
                                                 new Color(0.13f, 0.16f, 0.26f, 1f));


            // ---------------- 연결 ----------------
            CUI_Inventory cUI = goRoot.AddComponent<CUI_Inventory>();
            SerializedObject cSerialized = new SerializedObject(cUI);
            cSerialized.FindProperty("m_trContent").objectReferenceValue    = trGrid;
            cSerialized.FindProperty("m_btnTemplate").objectReferenceValue  = goTemplate.GetComponent<Button>();
            cSerialized.FindProperty("m_txtTitle").objectReferenceValue     = txtTitle;
            cSerialized.FindProperty("m_btnCharacter").objectReferenceValue = cCharButton;
            cSerialized.FindProperty("m_imgCharacter").objectReferenceValue = imgChar;
            cSerialized.FindProperty("m_txtCharacter").objectReferenceValue = txtChar;
            cSerialized.FindProperty("m_spDefaultCharacter").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(PATH_TEX_PLAYER);

            Set_ObjectArray(cSerialized, "m_arrTabButton", arrTabButton);
            Set_ObjectArray(cSerialized, "m_arrSlotButton", arrSlot);

            Sprite[] arrIcon = new Sprite[ARR_EQUIP_ICON.Length];
            for (int i = 0; i < arrIcon.Length; ++i)
                arrIcon[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{DIR_ART}/{ARR_EQUIP_ICON[i]}.png");
            Set_ObjectArray(cSerialized, "m_arrSlotIcon", arrIcon);

            cSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(goRoot, PATH_PREFAB_UI_INVEN);
            Object.DestroyImmediate(goRoot);
        }

        // 260918_가방 칸 하나 — 목록 격자와 B 슬롯이 같은 모양을 쓴다(CUI_Inventory.Paint_Cell이 이름으로 찾는다).
        /// <param name="bUseBadge"> 목록 칸만 왼쪽 위 'E' 배지를 단다 </param>
        private static GameObject Make_InventoryCell(string strName, Transform trParent, bool bUseBadge)
        {
            GameObject goCell = Create_UIObject(strName, trParent);
            goCell.AddComponent<Image>().color = new Color(0.20f, 0.23f, 0.36f, 1f);
            goCell.AddComponent<Button>();

            GameObject goIcon = Create_UIObject("Img_Icon", goCell.transform);
            RectTransform trIcon = goIcon.GetComponent<RectTransform>();
            trIcon.anchorMin = new Vector2(0.15f, 0.36f);
            trIcon.anchorMax = new Vector2(0.85f, 0.92f);
            trIcon.offsetMin = Vector2.zero;
            trIcon.offsetMax = Vector2.zero;
            Image imgIcon = goIcon.AddComponent<Image>();
            imgIcon.preserveAspect = true;
            imgIcon.raycastTarget  = false;

            GameObject goLabel = Create_UIObject("Txt_Label", goCell.transform);
            RectTransform trLabel = goLabel.GetComponent<RectTransform>();
            trLabel.anchorMin = new Vector2(0f, 0f);
            trLabel.anchorMax = new Vector2(1f, 0.36f);
            trLabel.offsetMin = new Vector2(4f, 2f);
            trLabel.offsetMax = new Vector2(-4f, 0f);
            Text txtLabel = Make_Text(goLabel, "Lv.0", 22, TextAnchor.MiddleCenter);
            txtLabel.raycastTarget = false;
            txtLabel.horizontalOverflow = HorizontalWrapMode.Wrap;

            if (bUseBadge == true)
            {
                GameObject goBadge = Create_UIObject("Badge_Equip", goCell.transform);
                RectTransform trBadge = goBadge.GetComponent<RectTransform>();
                trBadge.anchorMin = new Vector2(0f, 1f);
                trBadge.anchorMax = new Vector2(0f, 1f);
                trBadge.pivot     = new Vector2(0f, 1f);
                trBadge.anchoredPosition = new Vector2(6f, -6f);
                trBadge.sizeDelta = new Vector2(48f, 48f);
                Image imgBadge = goBadge.AddComponent<Image>();
                imgBadge.color = new Color(0.95f, 0.70f, 0.20f, 1f);
                imgBadge.raycastTarget = false;

                GameObject goE = Create_UIObject("Txt_E", goBadge.transform);
                Stretch_Full(goE.GetComponent<RectTransform>());
                Make_Text(goE, "E", 30, TextAnchor.MiddleCenter).raycastTarget = false;
                goBadge.SetActive(false);
            }

            return goCell;
        }

        private static Button Make_TabButton(string strName, Transform trParent, string strLabel, Color cColor)
        {
            GameObject goTab = Create_UIObject(strName, trParent);
            goTab.AddComponent<Image>().color = cColor;
            Button cButton = goTab.AddComponent<Button>();

            GameObject goLabel = Create_UIObject("Label", goTab.transform);
            Stretch_Full(goLabel.GetComponent<RectTransform>());
            Make_Text(goLabel, strLabel, 30, TextAnchor.MiddleCenter).raycastTarget = false;
            return cButton;
        }

        private static void Set_ObjectArray(SerializedObject cSerialized, string strProperty, Object[] arrObject)
        {
            SerializedProperty cArray = cSerialized.FindProperty(strProperty);
            cArray.arraySize = arrObject.Length;
            for (int i = 0; i < arrObject.Length; ++i)
                cArray.GetArrayElementAtIndex(i).objectReferenceValue = arrObject[i];
        }


        // 260905_상점. 목록은 EquipInfo.csv를 보고 런타임에 만든다.
        private static void Create_ShopUI()
        {
            Create_ListUI<CUI_Shop>(UI_SHOP, PATH_PREFAB_UI_SHOP, "\uc0c1\uc810", 130f);
        }

        // 260918_\uc218\uc9d1\ud55c \uce74\ub4dc(\uc6e8\uc774\ube0c \ubcf4\uc0c1) \uac24\ub7ec\ub9ac. \ubaa9\ub85d\uc740 MapInfo.csv + \ubcc4 \uae30\ub85d\uc744 \ubcf4\uace0 \ub7f0\ud0c0\uc784\uc5d0 \ub9cc\ub4e0\ub2e4.
        private static void Create_CardUI()
        {
            Create_ListUI<CUI_Card>(UI_CARD_GALLERY, PATH_PREFAB_UI_CARD_GALLERY, "\uce74\ub4dc", 130f);
        }




        // 260912_세로(9:16) 기준 배치. 기준 해상도 1080x1920.
        // 위 10% / 아래 22%는 CGameConfig에서 카메라가 비워 두는 띠다 —
        // 맵이 그 안으로 들어오지 않으면 HUD가 플레이 영역을 가린다.
        private static void Create_InGameUI()
        {
            GameObject goRoot = Create_UIObject(UI_INGAME, null);
            Stretch_Full(goRoot.GetComponent<RectTransform>());
            goRoot.AddComponent<CSafeArea>();

            // 상단 정보 바
            GameObject goBar = Create_UIObject("Panel_Top", goRoot.transform);
            RectTransform trBar = goBar.GetComponent<RectTransform>();
            trBar.anchorMin = new Vector2(0f, 1f);
            trBar.anchorMax = new Vector2(1f, 1f);
            trBar.pivot     = new Vector2(0.5f, 1f);
            trBar.anchoredPosition = new Vector2(0f, -18f);
            trBar.sizeDelta = new Vector2(-32f, 190f);
            Image imgBar = goBar.AddComponent<Image>();
            imgBar.color = new Color(0.06f, 0.07f, 0.12f, 0.78f);
            imgBar.raycastTarget = false;

            // 일시정지는 정보 바 오른쪽 끝. 엄지가 닿기 먼 자리라 잘못 눌리지 않는다.
            GameObject goPause = Create_UIObject("Btn_Pause", goBar.transform);
            RectTransform trPause = goPause.GetComponent<RectTransform>();
            trPause.anchorMin = new Vector2(1f, 1f);
            trPause.anchorMax = new Vector2(1f, 1f);
            trPause.pivot     = new Vector2(1f, 1f);
            trPause.anchoredPosition = new Vector2(-14f, -14f);
            trPause.sizeDelta = new Vector2(92f, 92f);
            goPause.AddComponent<Image>().color = new Color(0.16f, 0.20f, 0.34f, 0.92f);
            Button cPause = goPause.AddComponent<Button>();

            GameObject goPauseLabel = Create_UIObject("Label", goPause.transform);
            Stretch_Full(goPauseLabel.GetComponent<RectTransform>());
            Make_Text(goPauseLabel, "II", 38, TextAnchor.MiddleCenter).raycastTarget = false;

            // 남은 시간은 크게 따로 뽑는다 — 쫓기는 느낌이 이 게임의 긴장감이다.
            GameObject goTime = Create_UIObject("Txt_Time", goBar.transform);
            RectTransform trTime = goTime.GetComponent<RectTransform>();
            trTime.anchorMin = new Vector2(1f, 1f);
            trTime.anchorMax = new Vector2(1f, 1f);
            trTime.pivot     = new Vector2(1f, 1f);
            trTime.anchoredPosition = new Vector2(-118f, -16f);
            trTime.sizeDelta = new Vector2(200f, 88f);
            Text txtTime = Make_Text(goTime, "00:00", 52, TextAnchor.MiddleRight);
            txtTime.raycastTarget = false;

            GameObject goStatus = Create_UIObject("Txt_Status", goBar.transform);
            RectTransform trStatus = goStatus.GetComponent<RectTransform>();
            trStatus.anchorMin = new Vector2(0f, 1f);
            trStatus.anchorMax = new Vector2(1f, 1f);
            trStatus.pivot     = new Vector2(0.5f, 1f);
            trStatus.offsetMin = new Vector2(22f, -100f);
            trStatus.offsetMax = new Vector2(-330f, -16f);
            Text txtStatus = Make_Text(goStatus, string.Empty, 30, TextAnchor.MiddleLeft);
            txtStatus.raycastTarget = false;

            // 점령률 게이지 — 목표 대비 얼마나 왔는지 한 줄로 보여 준다.
            GameObject goGaugeBg = Create_UIObject("Img_GaugeBg", goBar.transform);
            RectTransform trGaugeBg = goGaugeBg.GetComponent<RectTransform>();
            trGaugeBg.anchorMin = new Vector2(0f, 0f);
            trGaugeBg.anchorMax = new Vector2(1f, 0f);
            trGaugeBg.pivot     = new Vector2(0.5f, 0f);
            trGaugeBg.anchoredPosition = new Vector2(0f, 20f);
            trGaugeBg.sizeDelta = new Vector2(-44f, 30f);
            Image imgGaugeBg = goGaugeBg.AddComponent<Image>();
            imgGaugeBg.color = new Color(0f, 0f, 0f, 0.55f);
            imgGaugeBg.raycastTarget = false;

            GameObject goGauge = Create_UIObject("Img_Gauge", goGaugeBg.transform);
            Stretch_Full(goGauge.GetComponent<RectTransform>());
            Image imgProgress = goGauge.AddComponent<Image>();
            imgProgress.color = new Color(0.30f, 0.78f, 0.46f, 0.95f);
            imgProgress.raycastTarget = false;
            imgProgress.type = Image.Type.Filled;
            imgProgress.fillMethod = Image.FillMethod.Horizontal;
            imgProgress.fillOrigin = (int)Image.OriginHorizontal.Left;
            imgProgress.fillAmount = 0f;

            // 하단 조작 영역 — 조이스틱은 왼쪽 55%에서만 잡히므로
            // (CInputHandler.ACTIVE_WIDTH) 버튼을 오른쪽에 두면 터치가 겹치지 않는다.
            RectTransform trBase   = Make_JoystickPart("Joystick_Base", goRoot.transform, PATH_TEX_JOY_BASE);
            RectTransform trHandle = Make_JoystickPart("Joystick_Handle", goRoot.transform, PATH_TEX_JOY_HANDLE);

            GameObject goSkill = Create_UIObject("Btn_Skill", goRoot.transform);
            RectTransform trSkill = goSkill.GetComponent<RectTransform>();
            trSkill.anchorMin = new Vector2(1f, 0f);
            trSkill.anchorMax = new Vector2(1f, 0f);
            trSkill.pivot     = new Vector2(1f, 0f);
            trSkill.anchoredPosition = new Vector2(-48f, 130f);
            trSkill.sizeDelta = new Vector2(210f, 210f);
            goSkill.AddComponent<Image>().color = new Color(0.20f, 0.42f, 0.70f, 0.92f);
            Button cSkill = goSkill.AddComponent<Button>();

            GameObject goSkillLabel = Create_UIObject("Label", goSkill.transform);
            Stretch_Full(goSkillLabel.GetComponent<RectTransform>());
            Text txtSkill = Make_Text(goSkillLabel, "점멸", 36, TextAnchor.MiddleCenter);
            txtSkill.raycastTarget = false;

            // 쿨타임 덮개 — 위에서 아래로 줄어들며 언제 다시 쓸 수 있는지 보여 준다.
            GameObject goCool = Create_UIObject("Img_Cool", goSkill.transform);
            Stretch_Full(goCool.GetComponent<RectTransform>());
            Image imgCool = goCool.AddComponent<Image>();
            imgCool.color = new Color(0f, 0f, 0f, 0.65f);
            imgCool.raycastTarget = false;
            imgCool.type = Image.Type.Filled;
            imgCool.fillMethod = Image.FillMethod.Vertical;
            imgCool.fillOrigin = (int)Image.OriginVertical.Top;

            // 소모품은 스킬 왼쪽. 둘 다 오른손 엄지 안쪽이고 아래 띠를 넘지 않는다.
            GameObject goItem = Create_UIObject("Btn_Item", goRoot.transform);
            RectTransform trItem = goItem.GetComponent<RectTransform>();
            trItem.anchorMin = new Vector2(1f, 0f);
            trItem.anchorMax = new Vector2(1f, 0f);
            trItem.pivot     = new Vector2(1f, 0f);
            trItem.anchoredPosition = new Vector2(-282f, 150f);
            trItem.sizeDelta = new Vector2(170f, 170f);
            goItem.AddComponent<Image>().color = new Color(0.60f, 0.34f, 0.20f, 0.92f);
            Button cItem = goItem.AddComponent<Button>();

            GameObject goItemLabel = Create_UIObject("Label", goItem.transform);
            Stretch_Full(goItemLabel.GetComponent<RectTransform>());
            Text txtItem = Make_Text(goItemLabel, "아이템", 26, TextAnchor.MiddleCenter);
            txtItem.raycastTarget = false;

            // 260916_화면 플래시 — 맨 마지막 자식이라 조이스틱/버튼 위에 그려진다.
            // raycastTarget은 반드시 꺼야 한다 — 안 그러면 화면 전체를 덮은 이 이미지가
            // 그 아래 스킬/아이템/일시정지 버튼의 터치를 전부 가로챈다.
            GameObject goFlash = Create_UIObject("Img_Flash", goRoot.transform);
            Stretch_Full(goFlash.GetComponent<RectTransform>());
            Image imgFlash = goFlash.AddComponent<Image>();
            imgFlash.color = new Color(1f, 1f, 1f, 0f);
            imgFlash.raycastTarget = false;

            CUI_InGame cUI = goRoot.AddComponent<CUI_InGame>();
            SerializedObject cSerialized = new SerializedObject(cUI);
            cSerialized.FindProperty("m_trJoystickBase").objectReferenceValue   = trBase;
            cSerialized.FindProperty("m_trJoystickHandle").objectReferenceValue = trHandle;
            cSerialized.FindProperty("m_txtStatus").objectReferenceValue        = txtStatus;
            cSerialized.FindProperty("m_imgProgress").objectReferenceValue      = imgProgress;
            cSerialized.FindProperty("m_txtTime").objectReferenceValue          = txtTime;
            cSerialized.FindProperty("m_btnPause").objectReferenceValue         = cPause;
            cSerialized.FindProperty("m_btnSkill").objectReferenceValue         = cSkill;
            cSerialized.FindProperty("m_imgSkillCool").objectReferenceValue     = imgCool;
            cSerialized.FindProperty("m_txtSkill").objectReferenceValue         = txtSkill;
            cSerialized.FindProperty("m_btnItem").objectReferenceValue          = cItem;
            cSerialized.FindProperty("m_txtItem").objectReferenceValue          = txtItem;
            cSerialized.FindProperty("m_imgFlash").objectReferenceValue         = imgFlash;
            cSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(goRoot, PATH_PREFAB_UI_INGAME);
            Object.DestroyImmediate(goRoot);
        }

        // 260904_공용 팝업 프리팹. 일시정지와 결과 화면이 이걸 돌려쓴다.
        // 260912_카드 3지선다. 카드는 비활성 템플릿을 복제해 쓰므로 겉모습은 여기서만 정한다.
        private static void Create_CardPickUI()
        {
            GameObject goRoot = Create_UIObject(UI_CARDPICK, null);
            Stretch_Full(goRoot.GetComponent<RectTransform>());
            goRoot.AddComponent<CSafeArea>();

            // 뒤를 어둡게 덮어 카드에 시선이 가게 하고, 뒤쪽 클릭도 막는다.
            GameObject goDim = Create_UIObject("Dim", goRoot.transform);
            Stretch_Full(goDim.GetComponent<RectTransform>());
            goDim.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.82f);

            GameObject goTitle = Create_UIObject("Txt_Title", goRoot.transform);
            RectTransform trTitle = goTitle.GetComponent<RectTransform>();
            trTitle.anchorMin = new Vector2(0f, 0.5f);
            trTitle.anchorMax = new Vector2(1f, 0.5f);
            trTitle.pivot     = new Vector2(0.5f, 0f);
            trTitle.anchoredPosition = new Vector2(0f, 330f);
            trTitle.sizeDelta = new Vector2(-80f, 90f);
            Text txtTitle = Make_Text(goTitle, "카드를 고르세요", 52, TextAnchor.MiddleCenter);
            txtTitle.raycastTarget = false;

            // 카드 세 장이 가로로 늘어선다. 세로 화면이라 폭이 좁아 간격을 촘촘히 둔다.
            GameObject goContent = Create_UIObject("Content", goRoot.transform);
            RectTransform trContent = goContent.GetComponent<RectTransform>();
            trContent.anchorMin = new Vector2(0f, 0.5f);
            trContent.anchorMax = new Vector2(1f, 0.5f);
            trContent.pivot     = new Vector2(0.5f, 0.5f);
            trContent.anchoredPosition = new Vector2(0f, 0f);
            trContent.sizeDelta = new Vector2(-40f, 560f);

            HorizontalLayoutGroup cLayout = goContent.AddComponent<HorizontalLayoutGroup>();
            cLayout.padding   = new RectOffset(8, 8, 0, 0);
            cLayout.spacing   = 14f;
            cLayout.childAlignment = TextAnchor.MiddleCenter;
            cLayout.childForceExpandWidth  = true;
            cLayout.childForceExpandHeight = true;
            cLayout.childControlWidth  = true;
            cLayout.childControlHeight = true;

            // 템플릿 — 꺼 둔 채로 프리팹에 남겨 두고 런타임에 복제한다.
            GameObject goCard = Create_UIObject("Btn_CardTemplate", goContent.transform);
            goCard.AddComponent<Image>().color = new Color(0.09f, 0.11f, 0.20f, 0.97f);
            Button cCard = goCard.AddComponent<Button>();

            // 260912_테두리 발광. 9슬라이스라 카드 크기가 달라져도 두께가 유지된다.
            // 색은 런타임에 카드 종류에 맞춰 CUI_CardPick이 칠한다.
            GameObject goGlow = Create_UIObject("Img_Glow", goCard.transform);
            RectTransform trGlow = goGlow.GetComponent<RectTransform>();
            Stretch_Full(trGlow);
            trGlow.offsetMin = new Vector2(-6f, -6f);       // 카드보다 살짝 크게 — 빛이 밖으로 번지게
            trGlow.offsetMax = new Vector2(6f, 6f);
            Image imgGlow = goGlow.AddComponent<Image>();
            imgGlow.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{DIR_ART}/{TEX_CARD_GLOW}.png");
            imgGlow.type   = Image.Type.Sliced;
            imgGlow.raycastTarget = false;

            // 아이콘 — 이름 아래, 설명 위
            GameObject goIcon = Create_UIObject("Img_Icon", goCard.transform);
            RectTransform trIcon = goIcon.GetComponent<RectTransform>();
            trIcon.anchorMin = new Vector2(0.5f, 1f);
            trIcon.anchorMax = new Vector2(0.5f, 1f);
            trIcon.pivot     = new Vector2(0.5f, 1f);
            trIcon.anchoredPosition = new Vector2(0f, -104f);
            trIcon.sizeDelta = new Vector2(120f, 120f);
            Image imgIcon = goIcon.AddComponent<Image>();
            imgIcon.preserveAspect = true;
            imgIcon.raycastTarget  = false;

            GameObject goName = Create_UIObject("Txt_Name", goCard.transform);
            RectTransform trName = goName.GetComponent<RectTransform>();
            trName.anchorMin = new Vector2(0f, 1f);
            trName.anchorMax = new Vector2(1f, 1f);
            trName.pivot     = new Vector2(0.5f, 1f);
            trName.anchoredPosition = new Vector2(0f, -26f);
            trName.sizeDelta = new Vector2(-16f, 72f);
            Make_Text(goName, "카드", 38, TextAnchor.MiddleCenter).raycastTarget = false;

            GameObject goDesc = Create_UIObject("Txt_Desc", goCard.transform);
            RectTransform trDesc = goDesc.GetComponent<RectTransform>();
            trDesc.anchorMin = new Vector2(0f, 0f);
            trDesc.anchorMax = new Vector2(1f, 1f);
            trDesc.offsetMin = new Vector2(12f, 18f);
            trDesc.offsetMax = new Vector2(-12f, -240f);
            Text txtDesc = Make_Text(goDesc, "설명", 24, TextAnchor.UpperCenter);
            txtDesc.raycastTarget = false;
            txtDesc.horizontalOverflow = HorizontalWrapMode.Wrap;

            goCard.SetActive(false);

            CUI_CardPick cUI = goRoot.AddComponent<CUI_CardPick>();
            SerializedObject cSerialized = new SerializedObject(cUI);
            cSerialized.FindProperty("m_txtTitle").objectReferenceValue    = txtTitle;
            cSerialized.FindProperty("m_btnTemplate").objectReferenceValue = cCard;
            cSerialized.FindProperty("m_trContent").objectReferenceValue   = trContent;
            cSerialized.FindProperty("m_arrIcon").arraySize                 = ARR_CARD_ICON.Length;
            cSerialized.FindProperty("m_arrRunSkillIcon").arraySize         = ARR_RUN_SKILL_ICON.Length;

            for (int i = 0; i < ARR_RUN_SKILL_ICON.Length; ++i)
            {
                Sprite spIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{DIR_ART}/{ARR_RUN_SKILL_ICON[i]}.png");
                cSerialized.FindProperty("m_arrRunSkillIcon").GetArrayElementAtIndex(i).objectReferenceValue = spIcon;
            }

            for (int i = 0; i < ARR_CARD_ICON.Length; ++i)
            {
                Sprite spIcon = AssetDatabase.LoadAssetAtPath<Sprite>($"{DIR_ART}/{ARR_CARD_ICON[i]}.png");
                cSerialized.FindProperty("m_arrIcon").GetArrayElementAtIndex(i).objectReferenceValue = spIcon;
            }
            cSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(goRoot, PATH_PREFAB_UI_CARD);
            Object.DestroyImmediate(goRoot);
        }

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

        // 260918_카드 크게 보기. 화면 전체를 어둡게 덮고 그림은 비율을 지킨 채 가운데에 맞춘다.
        // 루트의 Dim이 드래그를 받는다(좌우로 밀어 넘기기). 버튼은 그 위에 놓여 먼저 눌린다.
        private static void Create_CardViewerUI()
        {
            GameObject goRoot = Create_UIObject(UI_CARD_VIEWER, null);
            Stretch_Full(goRoot.GetComponent<RectTransform>());

            // 루트에 붙인 Image가 레이캐스트를 받아야 드래그 이벤트가 CUI_CardViewer로 온다.
            goRoot.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.92f);

            // 그림 영역 — 위 캡션 · 아래 버튼 자리를 뺀 가운데
            GameObject goArea = Create_UIObject("PhotoArea", goRoot.transform);
            RectTransform trArea = goArea.GetComponent<RectTransform>();
            Stretch_Full(trArea);
            trArea.offsetMin = new Vector2(24f, 260f);
            trArea.offsetMax = new Vector2(-24f, -200f);

            GameObject goPhoto = Create_UIObject("Img_Photo", goArea.transform);
            RectTransform trPhoto = goPhoto.GetComponent<RectTransform>();
            trPhoto.anchorMin = new Vector2(0.5f, 0.5f);
            trPhoto.anchorMax = new Vector2(0.5f, 0.5f);
            trPhoto.pivot     = new Vector2(0.5f, 0.5f);
            RawImage imgPhoto = goPhoto.AddComponent<RawImage>();
            imgPhoto.raycastTarget = false;     // 드래그는 뒤의 루트가 받는다
            AspectRatioFitter cFitter = goPhoto.AddComponent<AspectRatioFitter>();
            cFitter.aspectMode  = AspectRatioFitter.AspectMode.FitInParent;
            cFitter.aspectRatio = 0.6f;

            GameObject goCaption = Create_UIObject("Txt_Caption", goRoot.transform);
            RectTransform trCaption = goCaption.GetComponent<RectTransform>();
            trCaption.anchorMin = new Vector2(0f, 1f);
            trCaption.anchorMax = new Vector2(1f, 1f);
            trCaption.pivot     = new Vector2(0.5f, 1f);
            trCaption.offsetMin = new Vector2(140f, -190f);
            trCaption.offsetMax = new Vector2(-140f, -40f);
            Text txtCaption = Make_Text(goCaption, "카드", 36, TextAnchor.MiddleCenter);
            txtCaption.raycastTarget = false;

            Button cClose = Make_ViewerButton("Btn_Close", goRoot.transform, new Vector2(1f, 1f), new Vector2(-30f, -40f),
                                              new Vector2(110f, 110f), "X");
            Button cPrev  = Make_ViewerButton("Btn_Prev", goRoot.transform, new Vector2(0f, 0f), new Vector2(40f, 80f),
                                              new Vector2(220f, 130f), "< 이전");
            Button cNext  = Make_ViewerButton("Btn_Next", goRoot.transform, new Vector2(1f, 0f), new Vector2(-40f, 80f),
                                              new Vector2(220f, 130f), "다음 >");

            CUI_CardViewer cUI = goRoot.AddComponent<CUI_CardViewer>();
            SerializedObject cSerialized = new SerializedObject(cUI);
            cSerialized.FindProperty("m_imgPhoto").objectReferenceValue   = imgPhoto;
            cSerialized.FindProperty("m_cFitter").objectReferenceValue    = cFitter;
            cSerialized.FindProperty("m_txtCaption").objectReferenceValue = txtCaption;
            cSerialized.FindProperty("m_btnPrev").objectReferenceValue    = cPrev;
            cSerialized.FindProperty("m_btnNext").objectReferenceValue    = cNext;
            cSerialized.FindProperty("m_btnClose").objectReferenceValue   = cClose;
            cSerialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(goRoot, PATH_PREFAB_UI_CARD_VIEWER);
            Object.DestroyImmediate(goRoot);
        }

        /// <param name="vAnchor"> 모서리 앵커 — 피벗도 같은 자리에 둔다 </param>
        private static Button Make_ViewerButton(string strName, Transform trParent, Vector2 vAnchor, Vector2 vOffset,
                                                Vector2 vSize, string strLabel)
        {
            GameObject go = Create_UIObject(strName, trParent);
            RectTransform trButton = go.GetComponent<RectTransform>();
            trButton.anchorMin = vAnchor;
            trButton.anchorMax = vAnchor;
            trButton.pivot     = vAnchor;
            trButton.anchoredPosition = vOffset;
            trButton.sizeDelta = vSize;

            go.AddComponent<Image>().color = new Color(0.20f, 0.26f, 0.44f, 0.9f);
            Button cButton = go.AddComponent<Button>();

            GameObject goLabel = Create_UIObject("Label", go.transform);
            Stretch_Full(goLabel.GetComponent<RectTransform>());
            Make_Text(goLabel, strLabel, 36, TextAnchor.MiddleCenter).raycastTarget = false;

            return cButton;
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
