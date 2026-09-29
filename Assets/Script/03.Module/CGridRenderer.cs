using System.Collections.Generic;

using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 그리드 상태를 마스크 텍스처로 그리기
    // 260904_웨이브 이미지 스택 — 가림막을 걷어내면 아래 이미지가 드러난다
    /// <summary>
    /// 화면은 두 장으로 이뤄진다.
    ///   · srReveal — 이번 웨이브를 다 점령하면 드러날 이미지. 항상 전체가 깔려 있다.
    ///   · srCover  — 그 위를 덮는 가림막. 점령한 칸만 알파 0으로 뚫어 아래를 보여준다.
    /// 가림막 텍스처는 원본을 다시 찍은 사본이다. 원본을 그대로 쓰면 셀 단위로 구멍을 낼 수 없기 때문 —
    /// 셰이더 없이 SpriteRenderer만으로 해결하려는 제약 때문이다.
    ///
    /// N웨이브의 가림막은 이미지 스택의 [N-1]이고, 그걸 다 걷으면 [N]이 나온다.
    /// 그래서 1웨이브의 가림막이 곧 '마스크'다 (MapInfo.csv의 strLayerTex 참고).
    ///
    /// 260928_로그라이트 재작성으로 땅이 다시 칸이 됐다(Docs/Design_Roguelite_Rewrite.md 3장) — 가림막도
    /// 칸 그대로 뚫는다(칸 하나 = 사각형 하나, 안티앨리어싱 없음). 긋는 중인 선은 가림막에 찍지 않고
    /// 띠 메시(CTrailMesh_Utility)로 따로 그린다 — 트레일이 칸을 따라가는 꺾은선이라도 매끈하게 보인다.
    ///
    /// 260912_사본은 원본과 같은 해상도로 만든다.
    /// 한 장이 이번 웨이브에는 보상(reveal)이었다가 다음 웨이브에는 가림막(cover)이 되므로,
    /// 사본을 더 낮은 해상도로 찍으면 같은 그림이 역할만 바뀌었는데 갑자기 거칠어져
    /// 크기가 달라진 것처럼 보인다. 두 자리에서 똑같이 보이는 것이 이 연출의 전제다.
    /// </summary>
    public class CGridRenderer
    {
        // 가림막 원본이 없을 때만 쓰는 기본 해상도 (셀 하나를 몇 픽셀로 찍을지).
        // 원본이 있으면 그 해상도를 그대로 따라가므로 이 값은 쓰이지 않는다.
        private const int PIXEL_PER_CELL = 4;

        // 260924_선 색은 시각 정체성이라 여기 상수로 둔다(CGameConfig에는 굵기 · 켬끔만) — CUI_InGame의 플래시 색과 같은 자리
        private static readonly Color   COLOR_TRAIL      = new Color32(90, 225, 255, 255);  // 플레이어 쪽(머리)
        private static readonly Color   COLOR_TRAIL_TAIL = new Color32(40, 130, 225, 165);  // 선이 시작된 쪽(꼬리) — 옅게 사라진다
        private static readonly Color   COLOR_FIRE      = new Color32(255, 140, 40, 255);   // 260923_도화선의 불 머리
        private static readonly Color   COLOR_OUTLINE   = new Color32(150, 245, 255, 210);  // 260930_점령지 외곽선
        private const float             GLOW_ALPHA      = 0.25f;                            // 260924_발광 띠의 진하기
        private static readonly Color32 COLOR_BLOCK     = new Color32(0, 0, 0, 255);        // 맵 밖

        private const float TRAIL_WIDTH_DEFAULT = 0.3f;    // 260924_선 굵기 기본값(칸). GameConfig로 덮어쓴다
        private const float FIRE_LENGTH_CELL  = 0.8f;      // 불 머리 길이(칸)
        private const float GLOW_WIDTH_SCALE  = 2.4f;      // 260924_발광 띠는 본 선의 몇 배 굵기인가
        private const string RESOURCE_TRAIL_MATERIAL = "Mat_Trail";   // 260924_있으면 이 재질(=셰이더)로 선을 그린다

        // 260930_점령지 외곽선 — 내 땅과 빈 땅이 맞닿은 변만 따라 두른다
        private const float OUTLINE_WIDTH_CELL  = 0.22f;   // 굵기(칸). 긋는 선보다 가늘게 — 지금 긋는 선이 먼저 읽혀야 한다
        private const int   OUTLINE_SORT_OFFSET = 1;       // 가림막 바로 위(선과 같은 층). 선을 나중에 넣어 그 위에 온다
        private const int   TRAIL_SORT_OFFSET = 1;         // 가림막보다 한 칸 위에 그린다
        private static readonly Color32 COLOR_FALLBACK  = new Color32(8, 10, 20, 235);      // 가림막을 못 읽었을 때

        private CTerritoryGrid  m_cGrid;
        private SpriteRenderer  m_srCover;
        private SpriteRenderer  m_srReveal;

        private Texture2D   m_texMask;
        private Sprite      m_spMask;
        private Sprite      m_spReveal;

        private Color32[]   m_arrPixel;         // 마스크 전체 픽셀
        private Color32[]   m_arrCoverPixel;    // 가림막 이미지를 마스크 해상도로 미리 샘플링해 둔 것

        // 260923_선은 직접 만든 띠 메시로 그린다(CTrailMesh_Utility) — 왜 LineRenderer가 아닌지는 그 파일에 적어 두었다
        private GameObject              m_goTrailRoot;
        private MeshFilter              m_cTrailFilter;
        private Mesh                    m_cTrailMesh;
        private Material                m_cTrailMaterial;
        private Texture2D               m_texTrail;
        private readonly List<Vector3>  m_lstLinePoint = new List<Vector3>();
        private readonly List<Vector3>  m_lstVertex    = new List<Vector3>();
        private readonly List<int>      m_lstIndex     = new List<int>();
        private readonly List<Color>    m_lstColor     = new List<Color>();
        private readonly List<Vector2>  m_lstUV        = new List<Vector2>();
        private float                   m_fTrailWidthCell = TRAIL_WIDTH_DEFAULT;

        // 260930_점령지 외곽선 — 점령지가 바뀔 때만 다시 만든다(선은 매 프레임, 이쪽은 IS_DIRTY 때만)
        private GameObject              m_goOutline;
        private Mesh                    m_cOutlineMesh;
        private bool                    m_bOutline = true;
        private readonly List<Vector3>  m_lstOutlinePoint  = new List<Vector3>();
        private readonly List<Vector3>  m_lstOutlineVertex = new List<Vector3>();
        private readonly List<int>      m_lstOutlineIndex  = new List<int>();
        private readonly List<Color>    m_lstOutlineColor  = new List<Color>();
        private readonly List<Vector2>  m_lstOutlineUV     = new List<Vector2>();
        private bool                    m_bTrailGlow      = true;
        private bool                    m_bOwnMaterial;

        private int m_iTexWidth;
        private int m_iTexHeight;

        #region 초기화 / 해제
        /// <param name="srCover"> 가림막(마스크)을 그릴 SpriteRenderer. 정렬 순서가 srReveal보다 앞이어야 한다. </param>
        /// <param name="srReveal"> 점령하면 드러날 이미지를 깔 SpriteRenderer. </param>
        public bool Initialize(CTerritoryGrid cGrid, SpriteRenderer srCover, SpriteRenderer srReveal)
        {
            if (cGrid == null || srCover == null)
            {
                Debug.LogError("[CGridRenderer] Grid 또는 Cover SpriteRenderer가 null 입니다.");
                return false;
            }

            // 스테이지를 다시 초기화해도 이전 텍스처/스프라이트가 남지 않게 먼저 정리한다.
            Release();

            m_cGrid     = cGrid;
            m_srCover   = srCover;
            m_srReveal  = srReveal;

            // 아직 가림막 원본을 모르므로 기본 해상도로 잡아 둔다.
            // 첫 Set_WaveTexture에서 원본 크기에 맞춰 다시 잡힌다.
            Build_Mask(cGrid.WIDTH * PIXEL_PER_CELL, cGrid.HEIGHT * PIXEL_PER_CELL);
            Fill_CoverFallback();

            m_goTrailRoot = new GameObject("TrailMesh");
            m_goTrailRoot.transform.SetParent(srCover.transform.parent, false);

            m_cTrailMesh = new Mesh { name = "Trail" };
            m_cTrailMesh.MarkDynamic();
            m_cTrailFilter = m_goTrailRoot.AddComponent<MeshFilter>();
            m_cTrailFilter.sharedMesh = m_cTrailMesh;

            // 260924_`Resources/Mat_Trail.mat`이 있으면 **그 재질(= 셰이더)로 선을 그린다.** 선에 스타일을
            // 입히려면 유니티에서 그 이름으로 재질을 하나 만들면 된다 — 메시가 꼭짓점 색과 UV를 같이 넘기므로
            // (CTrailMesh_Utility) 셰이더에서 그라디언트 · 흐르는 무늬 · 발광을 마음대로 쓸 수 있다.
            // u = 지나온 거리(굵기 한 칸이 1), v = 띠를 가로지르는 0~1.
            Material cCustom = Resources.Load<Material>(RESOURCE_TRAIL_MATERIAL);
            if (cCustom != null)
            {
                m_cTrailMaterial = cCustom;
                m_bOwnMaterial   = false;
            }
            else
            {
                // 없으면 가림막과 같은 재질을 쓰되 **그림이 비치지 않게 흰 1x1로 갈아 끼운다** —
                // 스프라이트 재질을 그대로 쓰면 선에 맵 그림이 늘어나 붙는다.
                m_texTrail = new Texture2D(1, 1);
                m_texTrail.SetPixel(0, 0, Color.white);
                m_texTrail.Apply(false);

                m_cTrailMaterial = new Material(srCover.sharedMaterial) { mainTexture = m_texTrail };
                m_bOwnMaterial   = true;
            }

            MeshRenderer cRenderer = m_goTrailRoot.AddComponent<MeshRenderer>();
            cRenderer.sharedMaterial   = m_cTrailMaterial;
            cRenderer.sortingLayerID   = srCover.sortingLayerID;
            cRenderer.sortingOrder     = srCover.sortingOrder + TRAIL_SORT_OFFSET;
            cRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cRenderer.receiveShadows   = false;

            // 260930_외곽선도 같은 띠 메시 · 같은 재질을 쓴다 — 새 재질도 셰이더도 만들지 않는다
            m_goOutline = new GameObject("TerritoryOutline");
            m_goOutline.transform.SetParent(srCover.transform.parent, false);

            m_cOutlineMesh = new Mesh { name = "TerritoryOutline" };
            m_cOutlineMesh.MarkDynamic();
            m_goOutline.AddComponent<MeshFilter>().sharedMesh = m_cOutlineMesh;

            MeshRenderer cOutline = m_goOutline.AddComponent<MeshRenderer>();
            cOutline.sharedMaterial    = m_cTrailMaterial;
            cOutline.sortingLayerID    = srCover.sortingLayerID;
            cOutline.sortingOrder      = srCover.sortingOrder + OUTLINE_SORT_OFFSET;
            cOutline.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cOutline.receiveShadows    = false;

            Refresh_All();
            return true;
        }

        // Sprite.Create가 만든 스프라이트는 임포트된 에셋이 아니라 런타임 인스턴스다.
        // 텍스처만 파괴하면 스프라이트가 그대로 새고, 렌더러는 파괴된 텍스처를 물고 깨져 보인다.
        public void Release()
        {
            Clear_Mask();
            Clear_Sprite(m_srReveal, ref m_spReveal);

            if (m_goOutline != null)
                Object.Destroy(m_goOutline);
            if (m_cOutlineMesh != null)
                Object.Destroy(m_cOutlineMesh);

            m_goOutline    = null;
            m_cOutlineMesh = null;

            if (m_goTrailRoot != null)
                Object.Destroy(m_goTrailRoot);
            if (m_cTrailMesh != null)
                Object.Destroy(m_cTrailMesh);
            if (m_cTrailMaterial != null && m_bOwnMaterial == true)
                Object.Destroy(m_cTrailMaterial);       // Resources에서 가져온 재질은 우리 것이 아니다
            if (m_texTrail != null)
                Object.Destroy(m_texTrail);

            m_goTrailRoot    = null;
            m_cTrailFilter   = null;
            m_cTrailMesh     = null;
            m_cTrailMaterial = null;
            m_texTrail       = null;
            m_bOwnMaterial   = false;

            m_cGrid     = null;
            m_srCover   = null;
            m_srReveal  = null;
        }

        private void Clear_Mask()
        {
            Clear_Sprite(m_srCover, ref m_spMask);

            if (m_texMask != null)
                Object.Destroy(m_texMask);

            m_texMask       = null;
            m_arrPixel      = null;
            m_arrCoverPixel = null;
        }

        private static void Clear_Sprite(SpriteRenderer srTarget, ref Sprite spOwned)
        {
            if (srTarget != null && srTarget.sprite == spOwned)
                srTarget.sprite = null;

            if (spOwned != null)
                Object.Destroy(spOwned);

            spOwned = null;
        }

        // 260912_마스크 텍스처를 그 해상도로 새로 만든다.
        // 웨이브마다 원본 크기가 같으면 한 번만 돌고, 다르면 그때만 다시 만든다.
        private void Build_Mask(int iTexWidth, int iTexHeight)
        {
            // 칸 하나에 최소 1픽셀은 있어야 구멍을 낼 수 있다.
            m_iTexWidth  = Mathf.Max(m_cGrid.WIDTH, iTexWidth);
            m_iTexHeight = Mathf.Max(m_cGrid.HEIGHT, iTexHeight);

            Clear_Mask();

            m_texMask = new Texture2D(m_iTexWidth, m_iTexHeight, TextureFormat.RGBA32, false)
            {
                name        = "Tex_TerritoryMask",
                filterMode  = FilterMode.Bilinear,  // 260923_다각형 가장자리를 부드럽게 — 칸 경계를 지킬 이유가 사라졌다
                wrapMode    = TextureWrapMode.Clamp,
            };

            m_arrPixel       = new Color32[m_iTexWidth * m_iTexHeight];
            m_arrCoverPixel  = new Color32[m_iTexWidth * m_iTexHeight];

            m_spMask = Sprite.Create(m_texMask, new Rect(0f, 0f, m_iTexWidth, m_iTexHeight),
                                     new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
            m_spMask.name = "Sprite_TerritoryMask";
            m_srCover.sprite = m_spMask;

            Fit_ToGrid(m_srCover, m_spMask);
        }

        // 260912_가림막과 보상은 반드시 같은 자리에 같은 크기로 놓인다.
        // 한쪽만 원본 비율을 지키면 역할이 바뀔 때 그림이 어긋나 보인다.
        private void Fit_ToGrid(SpriteRenderer srTarget, Sprite spSprite)
        {
            if (srTarget == null || spSprite == null)
                return;

            Vector2 vSpriteSize = spSprite.bounds.size;
            if (vSpriteSize.x <= 0f || vSpriteSize.y <= 0f)
                return;

            Vector2 vWorldSize = m_cGrid.WORLD_SIZE;
            srTarget.transform.position   = new Vector3(m_cGrid.WORLD_CENTER.x, m_cGrid.WORLD_CENTER.y, 0f);
            srTarget.transform.localScale = new Vector3(vWorldSize.x / vSpriteSize.x,
                                                        vWorldSize.y / vSpriteSize.y, 1f);
        }
        #endregion 초기화 / 해제

        // 260904_보상 공개 연출.
        // 픽셀을 다시 찍지 않고 SpriteRenderer의 알파만 건드린다 —
        // 마스크 텍스처를 매 프레임 다시 올리면 모바일에서 감당이 안 되기 때문이다.
        /// <param name="fAlpha"> 1이면 평소대로 가림, 0이면 완전히 걷힘 </param>
        public void Set_CoverAlpha(float fAlpha)
        {
            if (m_srCover == null)
                return;

            Color cColor = m_srCover.color;
            cColor.a = Mathf.Clamp01(fAlpha);
            m_srCover.color = cColor;
        }

        #region 웨이브 이미지
        // 260904_웨이브가 넘어갈 때마다 두 장을 갈아 끼운다.
        /// <param name="texCover"> 이번 웨이브를 덮을 가림막 (이미지 스택의 [웨이브-1]) </param>
        /// <param name="texReveal"> 다 점령하면 드러날 이미지 (이미지 스택의 [웨이브]) </param>
        public void Set_WaveTexture(Texture2D texCover, Texture2D texReveal)
        {
            Fill_Cover(texCover);
            Set_RevealTexture(texReveal);
            Refresh_All();

            Log_LayerBounds();
        }

        // 260912_두 장이 화면에서 정말 같은 자리·같은 크기인지 실행 중에 확인한다.
        // 보상은 점령한 칸으로만 드러나므로 눈으로는 테두리 한 줄밖에 안 보인다 —
        // 어긋나 있어도 '뭔가 이상하다'까지만 느껴지고 어디가 틀렸는지 알 수 없다.
        // 웨이브가 바뀔 때만 도는 검사라 비용은 없다시피 하다.
        private void Log_LayerBounds()
        {
            if (m_srCover == null || m_srReveal == null || m_srReveal.sprite == null)
                return;

            Bounds bCover  = m_srCover.bounds;
            Bounds bReveal = m_srReveal.bounds;
            Vector2 vGrid  = m_cGrid.WORLD_SIZE;

            const float EPSILON = 0.001f;
            bool bMatch = Mathf.Abs(bCover.size.x   - bReveal.size.x)   < EPSILON
                       && Mathf.Abs(bCover.size.y   - bReveal.size.y)   < EPSILON
                       && Mathf.Abs(bCover.center.x - bReveal.center.x) < EPSILON
                       && Mathf.Abs(bCover.center.y - bReveal.center.y) < EPSILON;

            string strBody = $"가림막 {bCover.size.x:F3}x{bCover.size.y:F3} @({bCover.center.x:F3}, {bCover.center.y:F3})"
                           + $" / 보상 {bReveal.size.x:F3}x{bReveal.size.y:F3} @({bReveal.center.x:F3}, {bReveal.center.y:F3})"
                           + $" / 그리드 {vGrid.x:F3}x{vGrid.y:F3}";

            if (bMatch == true)
                Debug.Log($"[CGridRenderer] 두 장 크기 일치 — {strBody}");
            else
                Debug.LogError($"[CGridRenderer] 두 장 크기가 어긋납니다 — {strBody}");
        }

        private void Set_RevealTexture(Texture2D texReveal)
        {
            if (m_srReveal == null)
                return;

            Clear_Sprite(m_srReveal, ref m_spReveal);

            // 260912_보상 텍스처를 못 받으면 렌더러를 반드시 비운다.
            // 씬에 배치해 둔 스프라이트(Tex_Reward_Placeholder)가 그대로 남아 있으면
            // 그 크기가 그리드가 아니라 원본 그대로(5.4 x 9.0)라, 가림막보다 작은 네모가
            // 뒤에 깔린 것처럼 보인다. 이미지가 빠졌다는 사실도 가려진다.
            if (texReveal == null)
            {
                m_srReveal.sprite = null;
                Debug.LogWarning("[CGridRenderer] 보상 이미지를 받지 못했습니다. "
                               + "MapInfo.csv의 strLayerTex 이름과 Addressable 등록을 확인하세요.");
                return;
            }

            m_spReveal = Sprite.Create(texReveal, new Rect(0f, 0f, texReveal.width, texReveal.height),
                                       new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
            m_spReveal.name = "Sprite_Reveal";
            m_srReveal.sprite = m_spReveal;

            Fit_ToGrid(m_srReveal, m_spReveal);
        }

        /// <summary>
        /// 가림막 이미지를 마스크 해상도로 미리 샘플링해 둔다.
        /// 매 갱신마다 원본을 다시 읽지 않으려는 것이고, 웨이브가 바뀔 때만 다시 만든다.
        /// </summary>
        private void Fill_Cover(Texture2D texCover)
        {
            if (texCover == null)
            {
                Fill_CoverFallback();
                return;
            }

            // 임포트 설정에서 Read/Write를 켜지 않으면 GetPixels32가 예외를 던진다.
            if (texCover.isReadable == false)
            {
                Debug.LogError($"[CGridRenderer] '{texCover.name}'은 Read/Write가 꺼져 있어 가림막으로 쓸 수 없습니다. "
                             + "텍스처 임포트 설정에서 Read/Write Enabled를 켜세요.");
                Fill_CoverFallback();
                return;
            }

            // 260912_원본과 같은 해상도로 맞춘다. 줄여 찍으면 같은 그림인데도 거칠어져
            // 보상이었을 때와 가림막이 됐을 때가 다르게 보인다.
            if (texCover.width != m_iTexWidth || texCover.height != m_iTexHeight)
                Build_Mask(texCover.width, texCover.height);

            Color32[] arrSrc = texCover.GetPixels32();
            int iSrcW = texCover.width;
            int iSrcH = texCover.height;

            for (int py = 0; py < m_iTexHeight; ++py)
            {
                int sy = py * iSrcH / m_iTexHeight;
                int iSrcRow = sy * iSrcW;
                int iDstRow = py * m_iTexWidth;

                for (int px = 0; px < m_iTexWidth; ++px)
                {
                    Color32 cColor = arrSrc[iSrcRow + (px * iSrcW / m_iTexWidth)];
                    cColor.a = 255;                 // 가림막은 완전히 가려야 한다
                    m_arrCoverPixel[iDstRow + px] = cColor;
                }
            }
        }

        private void Fill_CoverFallback()
        {
            for (int i = 0; i < m_arrCoverPixel.Length; ++i)
                m_arrCoverPixel[i] = COLOR_FALLBACK;
        }
        #endregion 웨이브 이미지

        #region 갱신
        /// <summary> 260924_긋는 중인 선의 굵기(칸)와 발광. 보이기만 하는 값이라 점령 판정과는 무관하다(2-3). </summary>
        public void Set_TrailStyle(float fWidthCell, bool bGlow)
        {
            m_fTrailWidthCell = Mathf.Clamp(fWidthCell, 0.05f, 1f);
            m_bTrailGlow      = bGlow;
        }

        /// <summary> 260930_점령지 외곽선을 두를지. 끄면 메시를 비운다(2-3-1) </summary>
        public void Set_TerritoryOutline(bool bOutline)
        {
            m_bOutline = bOutline;
            Refresh_Outline();
        }

        /// <summary> 점령지가 바뀌었을 때만 가림막을 다시 뚫는다. 선은 매 프레임 다시 그린다(점 몇 개뿐이다). </summary>
        public void Tick()
        {
            if (m_cGrid == null)
                return;

            if (m_cGrid.IS_DIRTY == true)
            {
                Refresh_All();
                Refresh_Outline();          // 260930_외곽선도 점령지가 바뀔 때만 다시 만든다
                m_cGrid.Clear_Dirty();
            }

            Refresh_Trail();
        }

        // 260928_로그라이트 재작성 — 땅이 다시 칸이 되면서(3장) 가림막도 칸 그대로 뚫는다.
        // 칸은 축에 맞춘 사각형이라 다각형 시절의 서브픽셀 안티앨리어싱(가로줄을 여러 번 나눠 재기)이
        // 필요 없어졌다 — 그 칸이 OWNED인지만 보고 통째로 뚫거나 덮는다.
        private void Refresh_All()
        {
            if (m_texMask == null)
                return;

            float fPixelPerCellX = (float)m_iTexWidth / m_cGrid.WIDTH;
            float fCellPerPixelY = (float)m_cGrid.HEIGHT / m_iTexHeight;

            for (int py = 0; py < m_iTexHeight; ++py)
            {
                int iRow   = py * m_iTexWidth;
                int iCellY = Mathf.Min(m_cGrid.HEIGHT - 1, (int)(py * fCellPerPixelY));

                for (int px = 0; px < m_iTexWidth; ++px)
                {
                    int iCellX = Mathf.Min(m_cGrid.WIDTH - 1, (int)(px / fPixelPerCellX));
                    CELL_STATE eState = m_cGrid.Get_Cell(iCellX, iCellY);

                    if (eState == CELL_STATE.BLOCK)
                    {
                        m_arrPixel[iRow + px] = COLOR_BLOCK;
                        continue;
                    }

                    Color32 cColor = m_arrCoverPixel[iRow + px];
                    cColor.a = eState == CELL_STATE.OWNED ? (byte)0 : (byte)255;
                    m_arrPixel[iRow + px] = cColor;
                }
            }

            m_texMask.SetPixels32(m_arrPixel);
            m_texMask.Apply(false);
        }

        // 260930_점령지 외곽선 — **내 땅과 빈 땅(또는 맵 밖)이 맞닿은 변**만 모아 띠로 두른다.
        /// <summary>
        /// 칸마다 네 변을 다 그리면 안쪽 격자까지 그물처럼 보이므로, 이웃이 점령지가 아닌 변만 남긴다.
        /// 같은 줄에서 이어지는 변은 **한 토막으로 합쳐** 꼭짓점 수를 줄인다 — 칸마다 사각형을 하나씩
        /// 만들면 60x100 맵에서 수백 개가 되고, 이어 붙인 자리마다 이음매가 보인다.
        /// 긋는 선과 **같은 띠 메시 · 같은 재질**을 쓴다(2-3-1) — 새 셰이더도 드로우콜도 늘리지 않는다.
        /// </summary>
        private void Refresh_Outline()
        {
            if (m_cOutlineMesh == null)
                return;

            m_lstOutlineVertex.Clear();
            m_lstOutlineIndex.Clear();
            m_lstOutlineColor.Clear();
            m_lstOutlineUV.Clear();

            if (m_bOutline == true && m_cGrid != null)
            {
                // 가로 변 — 아래쪽(dy -1) · 위쪽(dy +1)을 줄마다 훑으며 이어지는 만큼 합친다
                for (int y = 0; y < m_cGrid.HEIGHT; ++y)
                {
                    Collect_EdgeRun(y, 0, -1);
                    Collect_EdgeRun(y, 0, 1);
                }

                // 세로 변 — 왼쪽(dx -1) · 오른쪽(dx +1)
                for (int x = 0; x < m_cGrid.WIDTH; ++x)
                {
                    Collect_EdgeRun(x, -1, 0);
                    Collect_EdgeRun(x, 1, 0);
                }
            }

            m_cOutlineMesh.Clear();
            if (m_lstOutlineIndex.Count == 0)
                return;

            m_cOutlineMesh.SetVertices(m_lstOutlineVertex);
            m_cOutlineMesh.SetColors(m_lstOutlineColor);
            m_cOutlineMesh.SetUVs(0, m_lstOutlineUV);
            m_cOutlineMesh.SetTriangles(m_lstOutlineIndex, 0);
            m_cOutlineMesh.RecalculateBounds();
        }

        // 줄 하나(가로면 y, 세로면 x)를 훑으며 '이웃이 점령지가 아닌' 변이 이어지는 구간을 토막으로 만든다.
        // (iDx, iDy)는 어느 쪽 이웃을 보는지 — 그 방향의 변이 곧 그릴 변이다.
        private void Collect_EdgeRun(int iLine, int iDx, int iDy)
        {
            bool bHorizontal = iDy != 0;
            int  iCount      = bHorizontal ? m_cGrid.WIDTH : m_cGrid.HEIGHT;
            int  iRunStart   = -1;

            for (int i = 0; i <= iCount; ++i)
            {
                bool bEdge = false;
                if (i < iCount)
                {
                    int x = bHorizontal ? i : iLine;
                    int y = bHorizontal ? iLine : i;
                    bEdge = m_cGrid.Get_Cell(x, y) == CELL_STATE.OWNED
                         && m_cGrid.Get_Cell(x + iDx, y + iDy) != CELL_STATE.OWNED;
                }

                if (bEdge == true)
                {
                    if (iRunStart < 0)
                        iRunStart = i;

                    continue;
                }

                if (iRunStart < 0)
                    continue;

                Add_OutlineSegment(iLine, iRunStart, i, iDx, iDy, bHorizontal);
                iRunStart = -1;
            }
        }

        // 합쳐진 한 토막을 띠로 만든다. 칸 (x,y)는 [x,x+1) x [y,y+1)이라 변의 좌표가 정수로 떨어진다(2-3)
        private void Add_OutlineSegment(int iLine, int iFrom, int iTo, int iDx, int iDy, bool bHorizontal)
        {
            Vector2 vFrom, vTo;
            if (bHorizontal == true)
            {
                float fY = iDy > 0 ? iLine + 1 : iLine;
                vFrom = new Vector2(iFrom, fY);
                vTo   = new Vector2(iTo, fY);
            }
            else
            {
                float fX = iDx > 0 ? iLine + 1 : iLine;
                vFrom = new Vector2(fX, iFrom);
                vTo   = new Vector2(fX, iTo);
            }

            m_lstOutlinePoint.Clear();
            m_lstOutlinePoint.Add(m_cGrid.Grid_ToWorld(vFrom));
            m_lstOutlinePoint.Add(m_cGrid.Grid_ToWorld(vTo));

            float fWidth = OUTLINE_WIDTH_CELL * m_cGrid.CELL_SIZE;
            CTrailMesh_Utility.CTrailStyle cStyle = new CTrailMesh_Utility.CTrailStyle
            {
                fWidth      = fWidth,
                cTail       = COLOR_OUTLINE,
                cHead       = COLOR_OUTLINE,
                fArcFrom    = 0f,
                fArcTo      = 1f,
                fUVPerWorld = 1f / Mathf.Max(1e-4f, fWidth),
            };

            CTrailMesh_Utility.Append(m_lstOutlinePoint, cStyle,
                                      m_lstOutlineVertex, m_lstOutlineIndex, m_lstOutlineColor, m_lstOutlineUV);
        }

        // 260923_긋는 중인 선. 도화선이 탄 구간은 빼고, 불 머리는 주황으로 그린다
        // 260924_발광 띠를 먼저 깔고 그 위에 본 선을 얹는다 — 한 메시 안에서는 넣은 순서대로 그려진다
        private void Refresh_Trail()
        {
            if (m_cTrailMesh == null)
                return;

            m_lstVertex.Clear();
            m_lstIndex.Clear();
            m_lstColor.Clear();
            m_lstUV.Clear();

            if (m_cGrid.IS_DRAWING == true)
            {
                if (m_bTrailGlow == true)
                    Build_Trail(true);

                Build_Trail(false);
            }

            m_cTrailMesh.Clear();
            if (m_lstIndex.Count == 0)
                return;

            m_cTrailMesh.SetVertices(m_lstVertex);
            m_cTrailMesh.SetColors(m_lstColor);
            m_cTrailMesh.SetUVs(0, m_lstUV);
            m_cTrailMesh.SetTriangles(m_lstIndex, 0);
            m_cTrailMesh.RecalculateBounds();
        }

        // 선 한 겹을 만든다(발광 겹 / 본 겹). 조각마다 탄 구간을 빼고 남은 토막만 그린다
        private void Build_Trail(bool bGlow)
        {
            float fBurnFrom = m_cGrid.BURN_FROM;
            float fBurnTo   = m_cGrid.BURN_TO;
            bool  bBurning  = fBurnFrom >= 0f;
            float fOffset   = 0f;

            IReadOnlyList<List<Vector2>> lstPiece = m_cGrid.TRAIL_PIECES;
            for (int p = 0; p < lstPiece.Count; ++p)
            {
                float fLength = CPolygon_Utility.Get_Length(lstPiece[p]);

                if (bBurning == false)
                {
                    Draw_Part(lstPiece[p], 0f, fLength, fOffset, COLOR_TRAIL, bGlow);
                }
                else
                {
                    // 이 조각에서 탄 구간 [fBurnFrom, fBurnTo]를 뺀 앞 · 뒤만 그린다
                    Draw_Part(lstPiece[p], 0f, Mathf.Min(fLength, fBurnFrom - fOffset), fOffset, COLOR_TRAIL, bGlow);
                    Draw_Part(lstPiece[p], Mathf.Max(0f, fBurnTo - fOffset), fLength, fOffset, COLOR_TRAIL, bGlow);
                    Draw_Part(lstPiece[p], Mathf.Max(0f, fBurnTo - fOffset - FIRE_LENGTH_CELL),
                              Mathf.Min(fLength, fBurnTo - fOffset), fOffset, COLOR_FIRE, bGlow);
                }

                fOffset += fLength;
            }
        }

        /// <summary> 폴리라인에서 길이 [fFrom, fTo] 구간만 띠로 만든다. fPieceArc는 이 조각이 선 전체에서 시작하는 자리 </summary>
        private void Draw_Part(List<Vector2> lstLine, float fFrom, float fTo, float fPieceArc, Color cColor, bool bGlow)
        {
            if (fTo - fFrom < 1e-3f || lstLine.Count < 2)
                return;

            m_lstLinePoint.Clear();
            float fWalked = 0f;

            for (int i = 0; i + 1 < lstLine.Count; ++i)
            {
                Vector2 a = lstLine[i];
                Vector2 b = lstLine[i + 1];
                float fLen = Vector2.Distance(a, b);
                float fSegFrom = fWalked;
                float fSegTo   = fWalked + fLen;
                fWalked = fSegTo;

                if (fSegTo < fFrom || fSegFrom > fTo || fLen <= 0f)
                    continue;

                if (m_lstLinePoint.Count == 0)
                    m_lstLinePoint.Add(m_cGrid.Grid_ToWorld(Vector2.Lerp(a, b, Mathf.Clamp01((fFrom - fSegFrom) / fLen))));

                m_lstLinePoint.Add(m_cGrid.Grid_ToWorld(Vector2.Lerp(a, b, Mathf.Clamp01((fTo - fSegFrom) / fLen))));
            }

            if (m_lstLinePoint.Count < 2)
                return;

            float fWidth = m_fTrailWidthCell * m_cGrid.CELL_SIZE * (bGlow ? GLOW_WIDTH_SCALE : 1f);
            float fTotal = Mathf.Max(1e-4f, m_cGrid.TRAIL_LENGTH);

            // 선 전체에서 이 토막이 차지하는 구간 — 꼬리에서 머리로 색이 하나로 이어져 흐른다
            bool  bTrail = cColor == COLOR_TRAIL;
            Color cHead  = bGlow ? Get_Glow(cColor) : cColor;
            Color cTail  = bTrail ? COLOR_TRAIL_TAIL : cColor;
            if (bGlow == true)
                cTail = Get_Glow(cTail);

            CTrailMesh_Utility.CTrailStyle cStyle = new CTrailMesh_Utility.CTrailStyle
            {
                fWidth      = fWidth,
                cTail       = cTail,
                cHead       = cHead,
                fArcFrom    = Mathf.Clamp01((fPieceArc + fFrom) / fTotal),
                fArcTo      = Mathf.Clamp01((fPieceArc + fTo) / fTotal),
                fUVPerWorld = 1f / Mathf.Max(1e-4f, fWidth),
            };

            CTrailMesh_Utility.Append(m_lstLinePoint, cStyle, m_lstVertex, m_lstIndex, m_lstColor, m_lstUV);
        }

        // 발광 띠의 색 — 본 색을 밝게 띄우고 아주 옅게 깐다
        private static Color Get_Glow(Color cBase)
            => new Color(Mathf.Min(1f, cBase.r + 0.25f), Mathf.Min(1f, cBase.g + 0.25f), Mathf.Min(1f, cBase.b + 0.25f),
                         cBase.a * GLOW_ALPHA);

        #endregion 갱신
    }
}
