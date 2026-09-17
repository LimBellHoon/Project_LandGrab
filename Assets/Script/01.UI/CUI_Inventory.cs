using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using Engine;

namespace Client
{
    // 260905_인벤토리 (로비의 가방 탭)
    // 260918_네 구역으로 다시 짰다 — 레퍼런스 게임의 가방 화면과 같은 배치다.
    /// <summary>
    /// <code>
    /// ┌──────────── 위 패널 ────────────┐
    /// │ B 슬롯   A 장착 캐릭터   B 슬롯 │   A: 지금 장착한 캐릭터 그림 (누르면 캐릭터 탭)
    /// └─────────────────────────────────┘   B: 부위별 장착 장비 (누르면 그 장비 상세)
    /// ┌──────────── C 목록 ─────────────┐   C: 보유 목록 격자. 장착한 칸은 왼쪽 위에 'E'
    /// └─────────────────────────────────┘
    ///   D: [장비] [캐릭터] [펫]              D: 목록 탭 (장비 뽑기는 상점으로 옮겼다 — CUI_Shop)
    /// </code>
    /// 장착 · 강화 · 레벨업은 칸을 눌러 뜨는 상세 팝업에서 한다. 팝업을 여는 것은 CGameManager다(2-7) —
    /// 여기서는 무엇을 보여 줄지(CUI_PopupDesc)만 만들어 넘긴다.
    /// 목록은 표를 훑어 만들되 장비는 '보유한 것'만, 캐릭터는 못 가진 것도 잠긴 채로 보여 준다.
    /// </summary>
    public class CUI_Inventory : CUI
    {
        private static readonly Color COLOR_TAB_ON      = new Color(0.24f, 0.52f, 0.86f, 1f);
        private static readonly Color COLOR_TAB_OFF     = new Color(0.13f, 0.16f, 0.26f, 1f);
        private static readonly Color COLOR_SLOT_EMPTY  = new Color(1f, 1f, 1f, 0.18f);
        private static readonly Color COLOR_LOCKED      = new Color(0.45f, 0.45f, 0.5f, 1f);

        // 260918_부위 이름. EQUIP_SLOT 순서(NONE 빼고)와 같다 — m_arrSlotButton · m_arrSlotIcon도 같은 순서다.
        private static readonly string[] ARR_SLOT_NAME = { "신발", "가방", "목걸이", "소모품" };

        [Header("C — 목록")]
        [SerializeField] private Transform  m_trContent;        // 격자 (GridLayoutGroup)
        [SerializeField] private Button     m_btnTemplate;      // 칸 복제 원본 (항상 비활성). 자식: Img_Icon · Txt_Label · Badge_Equip
        [SerializeField] private Text       m_txtTitle;         // 목록 머리 (탭 이름 · 안내)

        [Header("D — 탭")]
        [SerializeField] private Button[]   m_arrTabButton;     // INVENTORY_TAB 순서와 1:1

        [Header("A — 장착 캐릭터")]
        [SerializeField] private Button     m_btnCharacter;
        [SerializeField] private Image      m_imgCharacter;
        [SerializeField] private Text       m_txtCharacter;
        [SerializeField] private Sprite     m_spDefaultCharacter;   // 캐릭터 프리팹이 없을 때 쓸 기본 몸 (Prefab_Player와 같은 그림)

        [Header("B — 부위별 장착 장비")]
        [SerializeField] private Button[]   m_arrSlotButton;    // EQUIP_SLOT - 1 순서
        [SerializeField] private Sprite[]   m_arrSlotIcon;      // EQUIP_SLOT - 1 순서. 장비 아이콘으로도 쓴다

        private readonly List<Button> m_lstButton = new List<Button>();

        private CCSVData_EquipInfo     m_cEquipTable;
        private CCSVData_CharacterInfo m_cCharacterTable;
        private CProgress_Manager      m_cProgress;
        private Action                 m_OnChanged;
        private Action<CUI_PopupDesc>  m_OnRequestPopup;
        private INVENTORY_TAB          m_eTab;

        #region Engine.CUI
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CUI_InventoryDesc cDesc) == false)
            {
                Debug.LogError("[CUI_Inventory] CUI_InventoryDesc가 아닙니다.");
                return false;
            }

            if (m_trContent == null || m_btnTemplate == null
                || m_arrTabButton == null || m_arrTabButton.Length == 0)
            {
                Debug.LogError("[CUI_Inventory] 프리팹에 목록 / 템플릿 / 탭 버튼이 연결돼 있지 않습니다. "
                             + "Tools/LandGrab/Setup Assets 를 실행하세요.");
                return false;
            }

            m_cEquipTable     = cDesc.cEquipTable;
            m_cCharacterTable = cDesc.cCharacterTable;
            m_cProgress       = cDesc.cProgress;
            m_OnChanged       = cDesc.OnChanged;
            m_OnRequestPopup  = cDesc.OnRequestPopup;

            m_btnTemplate.gameObject.SetActive(false);
            Bind_Buttons();
            Select_Tab(INVENTORY_TAB.EQUIP);
            return true;
        }

        public override void Hide()
        {
            Clear_List();

            Unbind(m_arrTabButton);
            Unbind(m_arrSlotButton);
            Unbind(m_btnCharacter);

            m_cEquipTable     = null;
            m_cCharacterTable = null;
            m_cProgress       = null;
            m_OnChanged       = null;
            m_OnRequestPopup  = null;

            base.Hide();
        }
        #endregion Engine.CUI

        private void Bind_Buttons()
        {
            for (int i = 0; i < m_arrTabButton.Length; ++i)
            {
                INVENTORY_TAB eTab = (INVENTORY_TAB)i;      // 클로저가 반복 변수를 잡지 않도록 지역 복사
                Bind(m_arrTabButton[i], () => Select_Tab(eTab));
            }

            if (m_arrSlotButton != null)
            {
                for (int i = 0; i < m_arrSlotButton.Length; ++i)
                {
                    EQUIP_SLOT eSlot = (EQUIP_SLOT)(i + 1);
                    Bind(m_arrSlotButton[i], () => On_ClickSlot(eSlot));
                }
            }

            Bind(m_btnCharacter, () => Select_Tab(INVENTORY_TAB.CHARACTER));
        }

        private void Select_Tab(INVENTORY_TAB eTab)
        {
            m_eTab = eTab;

            for (int i = 0; i < m_arrTabButton.Length; ++i)
            {
                Image imgTab = m_arrTabButton[i] != null ? m_arrTabButton[i].GetComponent<Image>() : null;
                if (imgTab != null)
                    imgTab.color = (INVENTORY_TAB)i == m_eTab ? COLOR_TAB_ON : COLOR_TAB_OFF;
            }

            Refresh();
        }

        /// <summary> 위 패널(A · B)과 목록(C)을 지금 진행도로 다시 그린다. 무엇이든 바뀌면 이것 하나를 부른다. </summary>
        private void Refresh()
        {
            Refresh_Character();
            Refresh_Slots();

            Clear_List();
            switch (m_eTab)
            {
                case INVENTORY_TAB.CHARACTER: Build_CharacterList(); break;
                case INVENTORY_TAB.PET:       Set_Title("펫   (준비 중)"); break;
                default:                      Build_EquipList();     break;
            }
        }

        private void Notify_Changed()
        {
            Refresh();
            m_OnChanged?.Invoke();
        }

        #region A — 장착 캐릭터
        private void Refresh_Character()
        {
            CCharacterInfo cInfo = m_cCharacterTable != null && m_cProgress.EQUIPPED_CHARACTER_ID > 0
                                 ? m_cCharacterTable.Get_Info(m_cProgress.EQUIPPED_CHARACTER_ID) : null;

            if (m_imgCharacter != null)
            {
                m_imgCharacter.sprite = Get_CharacterSprite(cInfo);
                m_imgCharacter.preserveAspect = true;
            }

            if (m_txtCharacter != null)
            {
                m_txtCharacter.text = cInfo != null
                                    ? $"{cInfo.strName}  Lv.{m_cProgress.Get_CharacterLevel(cInfo.iCharacterID)}"
                                    : "기본 캐릭터";
            }
        }

        // 260918_캐릭터 그림은 그 캐릭터 스킨 프리팹의 몸 스프라이트다. 스킨 프리팹이 아직 없으면
        // 스테이지가 Prefab_Player로 대신 들어가므로(2-17) 화면도 같은 기본 몸을 보여 준다.
        private Sprite Get_CharacterSprite(CCharacterInfo cInfo)
        {
            if (cInfo != null && string.IsNullOrEmpty(cInfo.strPrefabName) == false
                && CGameInstance.Instance != null && CGameInstance.Instance.Has_Prefab(cInfo.strPrefabName) == true)
            {
                GameObject goPrefab = CGameInstance.Instance.Get_Prefab(cInfo.strPrefabName);
                SpriteRenderer srBody = goPrefab != null ? goPrefab.GetComponentInChildren<SpriteRenderer>() : null;
                if (srBody != null && srBody.sprite != null)
                    return srBody.sprite;
            }

            return m_spDefaultCharacter;
        }
        #endregion A — 장착 캐릭터

        #region B — 부위별 장착 장비
        private void Refresh_Slots()
        {
            if (m_arrSlotButton == null)
                return;

            for (int i = 0; i < m_arrSlotButton.Length; ++i)
            {
                Button cButton = m_arrSlotButton[i];
                if (cButton == null)
                    continue;

                EQUIP_SLOT eSlot    = (EQUIP_SLOT)(i + 1);
                CEquipInfo cEquip   = m_cProgress.Get_Equipped(eSlot);
                string     strName  = i < ARR_SLOT_NAME.Length ? ARR_SLOT_NAME[i] : eSlot.ToString();

                Paint_Cell(cButton.gameObject, Get_SlotIcon(eSlot), cEquip != null ? Color.white : COLOR_SLOT_EMPTY,
                           cEquip != null ? $"{strName}\n{Get_EquipShortText(cEquip)}" : $"{strName}\n비어 있음",
                           false);
            }
        }

        // 비어 있으면 장비 탭으로, 끼고 있으면 그 장비 상세를 연다.
        private void On_ClickSlot(EQUIP_SLOT eSlot)
        {
            CEquipInfo cEquip = m_cProgress.Get_Equipped(eSlot);
            if (cEquip == null)
            {
                Select_Tab(INVENTORY_TAB.EQUIP);
                return;
            }

            Open_EquipDetail(cEquip);
        }
        #endregion B — 부위별 장착 장비

        #region C — 장비 목록
        private void Build_EquipList()
        {
            if (m_cEquipTable == null)
            {
                Set_Title("EquipInfo.csv를 읽지 못했습니다");
                return;
            }

            IReadOnlyList<CEquipInfo> lstInfo = m_cEquipTable.ALL;
            int iOwned = 0;

            for (int i = 0; i < lstInfo.Count; ++i)
            {
                CEquipInfo cInfo = lstInfo[i];
                if (m_cProgress.Has_Item(cInfo.iEquipID) == false)
                    continue;       // 보유한 것만 보여 준다

                ++iOwned;

                Button cButton = Make_Cell($"Btn_Equip_{cInfo.iEquipID}");
                Paint_Cell(cButton.gameObject, Get_SlotIcon(cInfo.eSlot), Color.white,
                           $"{cInfo.strName}\n{Get_EquipShortText(cInfo)}", m_cProgress.Is_Equipped(cInfo.iEquipID));

                CEquipInfo cPicked = cInfo;         // 클로저 대비 지역 복사
                cButton.onClick.AddListener(() => Open_EquipDetail(cPicked));
            }

            Set_Title(iOwned > 0 ? "장비   (눌러서 장착 · 강화)" : "장비   (상점에서 얻으세요)");
        }

        private string Get_EquipShortText(CEquipInfo cInfo)
            => cInfo.IS_CONSUMABLE == true ? $"x{m_cProgress.Get_ItemCount(cInfo.iEquipID)}"
                                           : $"Lv.{m_cProgress.Get_EquipLevel(cInfo.iEquipID)}";

        // 260918_장비 상세 — 오른쪽(주) 버튼이 강화, 왼쪽(보조)이 장착/해제다. 소모품은 강화가 없어 장착/해제만 있다.
        private void Open_EquipDetail(CEquipInfo cInfo)
        {
            bool bEquipped = m_cProgress.Is_Equipped(cInfo.iEquipID);
            string strEquip = bEquipped == true ? "해제" : "장착";
            int iEquipID = cInfo.iEquipID;

            if (cInfo.IS_CONSUMABLE == true)
            {
                Request_Popup(new CUI_PopupDesc
                {
                    strTitle   = cInfo.strName,
                    strBody    = $"{cInfo.strDesc}\n보유 {m_cProgress.Get_ItemCount(iEquipID)}개",
                    strPrimary = strEquip,
                    OnPrimary  = () => Toggle_Equip(iEquipID),
                    strSecondary = "닫기",
                });
                return;
            }

            int iLevel = m_cProgress.Get_EquipLevel(iEquipID);
            int iCost  = m_cProgress.Get_EquipUpgradeCost(iEquipID);
            bool bMax  = iCost <= 0;

            Request_Popup(new CUI_PopupDesc
            {
                strTitle     = $"{cInfo.strName}  Lv.{iLevel}/{cInfo.iMaxLevel}",
                strBody      = $"{cInfo.strDesc}\n지금 {Format_Stat(cInfo, iLevel)}"
                             + (bMax == true ? "   (최대 강화)" : $"  →  다음 {Format_Stat(cInfo, iLevel + 1)}")
                             + $"\n코인 {m_cProgress.COIN}",
                strPrimary   = bMax == true ? "닫기" : $"강화 ({iCost})",
                OnPrimary    = bMax == true ? (Action)null : () => Upgrade_Equip(iEquipID),
                strSecondary = strEquip,
                OnSecondary  = () => Toggle_Equip(iEquipID),
            });
        }

        private static string Format_Stat(CEquipInfo cInfo, int iLevel)
        {
            float fValue = cInfo.Get_StatValue(iLevel);
            switch (cInfo.eStat)
            {
                case STAT_TYPE.SPEED:   return $"이동 속도 +{fValue * 100f:0.#}%";
                case STAT_TYPE.EVASION: return $"회피 +{fValue * 100f:0.#}%";
                case STAT_TYPE.HP:      return $"목숨 +{Mathf.RoundToInt(fValue)}";
                default:                return string.Empty;
            }
        }

        // 이미 낀 것이면 벗고, 아니면 낀다 — 같은 부위의 다른 장비는 Try_Equip이 알아서 뺀다.
        private void Toggle_Equip(int iEquipID)
        {
            if (m_cProgress.Is_Equipped(iEquipID) == true)
                m_cProgress.Unequip(iEquipID);
            else if (m_cProgress.Try_Equip(iEquipID) == false)
                return;

            Notify_Changed();
        }

        // 코인이 모자라면 Try_UpgradeEquip이 조용히 실패한다 — 안내를 띄워 왜 안 됐는지 알려 준다.
        private void Upgrade_Equip(int iEquipID)
        {
            if (m_cProgress.Try_UpgradeEquip(iEquipID) == false)
            {
                Request_Popup(new CUI_PopupDesc { strTitle = "강화할 수 없습니다", strBody = "코인이 모자랍니다." });
                return;
            }

            Notify_Changed();
        }
        #endregion C — 장비 목록

        #region C — 캐릭터 목록
        private void Build_CharacterList()
        {
            if (m_cCharacterTable == null)
            {
                Set_Title("CharacterInfo.csv를 읽지 못했습니다");
                return;
            }

            // 260918_못 가진 캐릭터도 목록에 보인다 — 무엇을 모을 수 있는지 보여야 모으고 싶어진다.
            IReadOnlyList<CCharacterInfo> lstInfo = m_cCharacterTable.ALL;
            for (int i = 0; i < lstInfo.Count; ++i)
            {
                CCharacterInfo cInfo = lstInfo[i];
                bool bOwned = m_cProgress.Has_Character(cInfo.iCharacterID);

                Button cButton = Make_Cell($"Btn_Character_{cInfo.iCharacterID}");
                Paint_Cell(cButton.gameObject, Get_CharacterSprite(cInfo), bOwned == true ? Color.white : COLOR_LOCKED,
                           bOwned == true ? $"{cInfo.strName}\nLv.{m_cProgress.Get_CharacterLevel(cInfo.iCharacterID)}"
                                          : $"{cInfo.strName}\n미보유",
                           m_cProgress.EQUIPPED_CHARACTER_ID == cInfo.iCharacterID);

                CCharacterInfo cPicked = cInfo;
                cButton.onClick.AddListener(() => Open_CharacterDetail(cPicked));
            }

            Set_Title("캐릭터   (눌러서 장착 · 레벨업)");
        }

        // 260918_캐릭터 상세 — 못 가졌으면 어디서 얻는지만, 가졌으면 레벨업(주) · 장착(보조).
        private void Open_CharacterDetail(CCharacterInfo cInfo)
        {
            int iCharacterID = cInfo.iCharacterID;

            if (m_cProgress.Has_Character(iCharacterID) == false)
            {
                CMapInfo cMap = m_cProgress.Find_CharacterMap(iCharacterID);
                Request_Popup(new CUI_PopupDesc
                {
                    strTitle = $"{cInfo.strName}   (미보유)",
                    strBody  = $"{cInfo.strDesc}\n{(cMap != null ? $"{cMap.strMapName} 클리어 시 획득" : "획득처 준비 중")}",
                });
                return;
            }

            int  iLevel    = m_cProgress.Get_CharacterLevel(iCharacterID);
            int  iCost     = m_cProgress.Get_CharacterLevelUpCost(m_cCharacterTable, iCharacterID);
            bool bMax      = iCost <= 0;
            bool bEquipped = m_cProgress.EQUIPPED_CHARACTER_ID == iCharacterID;

            // 성급은 아직 틀뿐이다 — 몇 성인지만 보여 주고 실제 효과는 걸지 않는다(2-17).
            int    iTier   = cInfo.Get_StarTier(iLevel);
            string strStar = new string('★', iTier) + new string('☆', Mathf.Max(0, cInfo.lstStarLevel.Count - iTier));

            Request_Popup(new CUI_PopupDesc
            {
                strTitle     = $"{cInfo.strName}  Lv.{iLevel}/{cInfo.iMaxLevel}  {strStar}",
                strBody      = $"{cInfo.strDesc}\n"
                             + (bMax == true ? "최대 레벨"
                                             : $"레벨업 조각 {m_cProgress.Get_CharacterFragment(iCharacterID)}/{iCost}"),
                strPrimary   = bMax == true ? "닫기" : "레벨업",
                OnPrimary    = bMax == true ? (Action)null : () => LevelUp_Character(iCharacterID),
                strSecondary = bEquipped == true ? string.Empty : "장착",
                OnSecondary  = () => Equip_Character(iCharacterID),
            });
        }

        private void Equip_Character(int iCharacterID)
        {
            if (m_cProgress.Try_EquipCharacter(iCharacterID) == true)
                Notify_Changed();
        }

        private void LevelUp_Character(int iCharacterID)
        {
            if (m_cProgress.Try_LevelUpCharacter(m_cCharacterTable, iCharacterID) == false)
            {
                Request_Popup(new CUI_PopupDesc { strTitle = "레벨업할 수 없습니다",
                                                  strBody  = "조각이 모자랍니다. 캐릭터를 주는 맵을 다시 깨면 모입니다." });
                return;
            }

            Notify_Changed();
        }
        #endregion C — 캐릭터 목록

        #region 공용
        private Sprite Get_SlotIcon(EQUIP_SLOT eSlot)
        {
            int iIndex = (int)eSlot - 1;
            return m_arrSlotIcon != null && iIndex >= 0 && iIndex < m_arrSlotIcon.Length ? m_arrSlotIcon[iIndex] : null;
        }

        private Button Make_Cell(string strName)
        {
            GameObject goCell = Instantiate(m_btnTemplate.gameObject, m_trContent);
            goCell.name = strName;
            goCell.SetActive(true);

            Button cButton = goCell.GetComponent<Button>();
            cButton.onClick.RemoveAllListeners();
            m_lstButton.Add(cButton);
            return cButton;
        }

        // 칸 하나 칠하기 — 목록 칸과 B 슬롯이 같은 모양(Img_Icon · Txt_Label · Badge_Equip)을 쓴다.
        private static void Paint_Cell(GameObject goCell, Sprite spIcon, Color cTint, string strLabel, bool bEquipped)
        {
            Transform trIcon = goCell.transform.Find("Img_Icon");
            Image imgIcon = trIcon != null ? trIcon.GetComponent<Image>() : null;
            if (imgIcon != null)
            {
                imgIcon.sprite = spIcon;
                imgIcon.color  = cTint;
                imgIcon.enabled = spIcon != null;
                imgIcon.preserveAspect = true;
            }

            Transform trLabel = goCell.transform.Find("Txt_Label");
            Text txtLabel = trLabel != null ? trLabel.GetComponent<Text>() : null;
            if (txtLabel != null)
                txtLabel.text = strLabel;

            Transform trBadge = goCell.transform.Find("Badge_Equip");
            if (trBadge != null)
                trBadge.gameObject.SetActive(bEquipped);
        }

        private void Request_Popup(CUI_PopupDesc cDesc) => m_OnRequestPopup?.Invoke(cDesc);

        private void Set_Title(string strTitle)
        {
            if (m_txtTitle != null)
                m_txtTitle.text = strTitle;
        }

        private void Clear_List()
        {
            for (int i = 0; i < m_lstButton.Count; ++i)
            {
                if (m_lstButton[i] == null)
                    continue;

                m_lstButton[i].onClick.RemoveAllListeners();
                Destroy(m_lstButton[i].gameObject);
            }

            m_lstButton.Clear();
        }

        private static void Bind(Button cButton, UnityEngine.Events.UnityAction fnClick)
        {
            if (cButton == null)
                return;

            cButton.onClick.RemoveAllListeners();
            cButton.onClick.AddListener(fnClick);
        }

        private static void Unbind(Button cButton)
        {
            if (cButton != null)
                cButton.onClick.RemoveAllListeners();
        }

        private static void Unbind(Button[] arrButton)
        {
            if (arrButton == null)
                return;

            for (int i = 0; i < arrButton.Length; ++i)
                Unbind(arrButton[i]);
        }
        #endregion 공용
    }
}
