using UnityEngine;

using Engine;

namespace Client
{
    // 260904_투사체 — 포수 몬스터가 쏘는 탄
    // 260917_Project_GYM 구조 이식 — 규칙은 CProjectileCore가 전부 들고, 여기는 풀 수명과 그리기만 한다
    /// <summary>
    /// 모양 · 이동 · 특성 조합은 ProjectileInfo.csv 한 줄이 정한다. 프리팹은 이것 하나다(2-6 원칙).
    /// 충돌 판정은 여기서 하지 않는다 — 누가 맞을 수 있는지는 스테이지가 알고 있다(CStage_Manager.Tick_Projectile).
    /// </summary>
    public class CProjectile : CGameObject
    {
        // 260917_쏜 쪽을 색으로 가른다. 적탄은 몬스터(포수)색, 플레이어 탄은 차가운 색.
        private static readonly Color COLOR_ENEMY_SHOT  = new Color(1f, 0.45f, 0.2f);
        private static readonly Color COLOR_PLAYER_SHOT = new Color(0.35f, 0.9f, 1f);

        // 레이저 · 충격파는 사각형으로 그린다. 스프라이트를 따로 굽지 않고 흰 텍스처로 한 번 만들어 돌려쓴다.
        private static Sprite s_spRect;

        [SerializeField] private SpriteRenderer m_srBody;

        private readonly CProjectileCore m_cCore = new CProjectileCore();
        private Sprite m_spCircle;      // 프리팹에 들어 있던 원 스프라이트

        public CProjectileCore  CORE        => m_cCore;
        public Vector2          POS         => m_cCore.POS;
        public PROJECTILE_SIDE  SIDE        => m_cCore.SIDE;
        /// <summary>
        /// 260904_Engine은 bCollect가 선 오브젝트를 레이어 Tick 뒤에 알아서 풀로 돌려준다.
        /// 따로 만료 플래그를 두면 같은 일을 두 번 하는 셈이라 프레임워크 것을 그대로 쓴다.
        /// </summary>
        public bool             IS_EXPIRED  => bCollect;

        #region Engine.CGameObject
        public override bool Initialize(IGameObjectDesc iBaseDesc)
        {
            base.Initialize(iBaseDesc);

            if ((iBaseDesc is CProjectileDesc cDesc) == false)
            {
                Debug.LogError("[CProjectile] CProjectileDesc가 아닙니다.");
                return false;
            }

            bCollect = false;   // 풀에서 재사용되므로 반드시 내려 둔다

            if (m_cCore.Initialize(cDesc.cInfo, cDesc.lstImpact, cDesc.cHost, cDesc.fCellSize,
                                   cDesc.vStartPos, cDesc.vDir, cDesc.eSide, cDesc.cOwner) == false)
            {
                bCollect = true;
                return false;
            }

            m_cCore.Set_SpawnScale(cDesc.fScale);
            if (m_srBody != null && m_spCircle == null)
                m_spCircle = m_srBody.sprite;

            Refresh_View();
            return true;
        }

        public override void Tick(float fDeltaTime)
        {
            if (bCollect == true)
                return;

            m_cCore.Tick(fDeltaTime);

            if (m_cCore.IS_EXPIRED == true)
            {
                bCollect = true;
                return;
            }

            Refresh_View();
        }

        public override void Hide()
        {
            m_cCore.Expire();   // 닿아 있던 대상의 속박 · 감속을 풀어 준다
            base.Hide();
        }
        #endregion Engine.CGameObject

        /// <summary> 플레이어에게 맞았을 때처럼 밖에서 끝내야 할 때 부른다. </summary>
        public void Expire()
        {
            m_cCore.Expire();
            bCollect = true;
        }

        // 모양이 낸 값을 그대로 옮긴다. 스프라이트는 1 월드 유닛 크기다(CProtoSetup.Create_ActorPrefab).
        private void Refresh_View()
        {
            CProjectileShape cShape = m_cCore.SHAPE;
            if (cShape == null)
                return;

            transform.position   = cShape.VIEW_CENTER;
            transform.rotation   = Quaternion.Euler(0f, 0f, cShape.VIEW_ANGLE);
            transform.localScale = new Vector3(cShape.VIEW_SIZE.x, cShape.VIEW_SIZE.y, 1f);

            if (m_srBody == null)
                return;

            Sprite spWant = cShape.IS_RECT == true ? Get_RectSprite() : m_spCircle;
            if (spWant != null && m_srBody.sprite != spWant)
                m_srBody.sprite = spWant;

            Color cColor = m_cCore.SIDE == PROJECTILE_SIDE.PLAYER_SHOT ? COLOR_PLAYER_SHOT : COLOR_ENEMY_SHOT;
            cColor.a = cShape.VIEW_ALPHA;
            m_srBody.color = cColor;
        }

        private static Sprite Get_RectSprite()
        {
            if (s_spRect == null)
            {
                Texture2D texWhite = Texture2D.whiteTexture;
                s_spRect = Sprite.Create(texWhite, new Rect(0f, 0f, texWhite.width, texWhite.height),
                                         new Vector2(0.5f, 0.5f), texWhite.width);
            }

            return s_spRect;
        }
    }
}
