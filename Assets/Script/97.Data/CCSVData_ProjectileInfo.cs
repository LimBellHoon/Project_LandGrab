using System.Collections.Generic;

using UnityEngine;

using Engine;

namespace Client
{
    // 260917_투사체 한 종류 — Project_GYM SkillInfo.csv의 탄 관련 열을 옮겨 왔다
    /// <summary>
    /// 모양 하나 + 이동 하나 + 특성 여러 개 + 맞은 대상에게 남길 효과 여러 개로 탄 한 종류가 정해진다.
    /// GYM은 조합마다 프리팹을 늘렸지만(Prefab_Bullet_Laser 등) 여기는 Prefab_Projectile 하나로 끝낸다.
    ///
    /// 몇 발을 어떤 모양으로 뿌릴지(FIRE_PATTERN)는 여기 없다 — 쏘는 쪽(EnemyInfo)의 성질이다.
    /// </summary>
    public class CProjectileInfo
    {
        public int                  iProjectileID;
        public string               strName;
        public PROJECTILE_SHAPE     eShape;
        public PROJECTILE_MOVE      eMove;
        public List<PROJECTILE_TRAIT> lstTrait = new List<PROJECTILE_TRAIT>();

        public float    fSpeed;         // 초당 셀
        public float    fLifeTime;      // 초
        public float    fMaxRange;      // 셀. 0 이하면 수명까지 / LASER는 빔 길이
        public float    fHitRange;      // 셀. POINT의 반지름 · SWEEP의 절반 높이 · BLAST의 시작 반지름
        public float    fScale;         // 겉모습 배율 (판정에도 곱한다)
        public int      iDamage;
        // GYM '죽는 조건 상세'. 맞히거나 튕길 때마다 1씩 깎이고 0이 되면 사라진다. -1이면 무한
        public int      iDurability;
        public List<int> lstImpactID = new List<int>();

        // 모듈마다 쓰는 조율값. KEY:VALUE를 | 로 잇는다 — GYM은 ScriptableObject 필드와
        // 위치 기반 커스텀 변수(lstBulletLogic_Trait_CustomVariable)에 흩어 뒀다.
        public Dictionary<string, float> dicParam = new Dictionary<string, float>();

        public bool Has_Trait(PROJECTILE_TRAIT eTrait) => lstTrait.Contains(eTrait);

        public float Get_Param(string strKey, float fDefault)
            => dicParam.TryGetValue(strKey, out float fValue) ? fValue : fDefault;
    }

    public class CCSVData_ProjectileInfo : CCSVData
    {
        public const string CSV_KEY = "CCSVData_ProjectileInfo";
        private const string TABLE_NAME = "ProjectileInfo";

        private readonly Dictionary<int, CProjectileInfo> m_dicInfo = new Dictionary<int, CProjectileInfo>();
        private readonly List<CProjectileInfo> m_lstInfo = new List<CProjectileInfo>();

        public IReadOnlyList<CProjectileInfo> ALL => m_lstInfo;
        public int COUNT => m_lstInfo.Count;

        public CProjectileInfo Get_Info(int iProjectileID)
            => m_dicInfo.TryGetValue(iProjectileID, out CProjectileInfo cInfo) ? cInfo : null;

        protected override void Parse_CSVData(string[] arrField)
        {
            if (CCSV_Utility.Is_HeaderRow(arrField, "iProjectileID") == true)
                return;

            CProjectileInfo cInfo = new CProjectileInfo
            {
                iProjectileID   = CCSV_Utility.To_Int(arrField, 0),
                strName         = CCSV_Utility.To_String(arrField, 1),
                eShape          = CCSV_Utility.To_Enum(arrField, 2, PROJECTILE_SHAPE.POINT),
                eMove           = CCSV_Utility.To_Enum(arrField, 3, PROJECTILE_MOVE.STRAIGHT),
                fSpeed          = CCSV_Utility.To_Float(arrField, 5, 6f),
                fLifeTime       = CCSV_Utility.To_Float(arrField, 6, 4f),
                fMaxRange       = CCSV_Utility.To_Float(arrField, 7),
                fHitRange       = CCSV_Utility.To_Float(arrField, 8, 0.7f),
                fScale          = CCSV_Utility.To_Float(arrField, 9, 1f),
                iDamage         = CCSV_Utility.To_Int(arrField, 10, 1),
                iDurability     = CCSV_Utility.To_Int(arrField, 11, 1),
                dicParam        = CCSV_Utility.To_ParamMap(arrField, 13),
            };

            List<string> lstTrait = CCSV_Utility.To_List(arrField, 4);
            for (int i = 0; i < lstTrait.Count; ++i)
            {
                string[] arrOne = { lstTrait[i] };
                PROJECTILE_TRAIT eTrait = CCSV_Utility.To_Enum(arrOne, 0, PROJECTILE_TRAIT.NONE);
                if (eTrait != PROJECTILE_TRAIT.NONE)
                    cInfo.lstTrait.Add(eTrait);
            }

            List<string> lstImpact = CCSV_Utility.To_List(arrField, 12);
            for (int i = 0; i < lstImpact.Count; ++i)
            {
                string[] arrOne = { lstImpact[i] };
                int iImpactID = CCSV_Utility.To_Int(arrOne, 0);
                if (iImpactID > 0)
                    cInfo.lstImpactID.Add(iImpactID);
            }

            if (cInfo.iProjectileID <= 0)
            {
                Debug.LogError($"[{TABLE_NAME}] iProjectileID가 없는 행을 건너뛴다.");
                return;
            }

            if (m_dicInfo.ContainsKey(cInfo.iProjectileID) == true)
            {
                Debug.LogError($"[{TABLE_NAME}] iProjectileID {cInfo.iProjectileID}가 중복이다. 뒤의 행을 버린다.");
                return;
            }

            m_dicInfo.Add(cInfo.iProjectileID, cInfo);
            m_lstInfo.Add(cInfo);
        }
    }
}
