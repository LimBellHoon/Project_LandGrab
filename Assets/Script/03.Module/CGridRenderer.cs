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
    /// 260923_땅이 다각형이 되면서 가림막도 **다각형 모양 그대로** 뚫는다 — 가장자리는 픽셀을 나눠 재어 부드럽게 옅어진다.
    /// 긋는 중인 선은 가림막에 찍지 않고 띠 메시(CTrailMesh_Utility)로 따로 그린다 — 사선 · 곡선이 계단 없이 보인다.
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

        private static readonly Color   COLOR_TRAIL     = new Color32(90, 225, 255, 255);   // 그리는 중인 선
        private static readonly Color   COLOR_FIRE      = new Color32(255, 140, 40, 255);   // 260923_도화선의 불 머리
        private static readonly Color32 COLOR_BLOCK     = new Color32(0, 0, 0, 255);        // 맵 밖

        // 260923_가림막을 다각형으로 뚫을 때 한 픽셀 줄을 몇 번 나눠 재는가 — 가장자리가 계단 없이 옅어진다
        private const int   COVERAGE_SUBROW   = 4;
        private const float TRAIL_WIDTH_CELL  = 0.55f;     // 선 굵기(칸)
        private const float FIRE_LENGTH_CELL  = 0.8f;      // 불 머리 길이(칸)
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
        private float[]     m_arrRowCoverage;   // 260923_한 줄의 픽셀마다 점령지가 덮은 비율(0~1)
        private readonly List<float> m_lstCross = new List<float>();

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

            // 가림막과 같은 재질을 쓰되 **그림이 비치지 않게 흰 1x1로 갈아 끼운다** — 스프라이트 재질을
            // 그대로 쓰면 선에 맵 그림이 늘어나 붙어 꺾을 때마다 무늬가 일그러져 보인다.
            m_texTrail = new Texture2D(1, 1);
            m_texTrail.SetPixel(0, 0, Color.white);
            m_texTrail.Apply(false);

            m_cTrailMaterial = new Material(srCover.sharedMaterial) { mainTexture = m_texTrail };

            MeshRenderer cRenderer = m_goTrailRoot.AddComponent<MeshRenderer>();
            cRenderer.sharedMaterial   = m_cTrailMaterial;
            cRenderer.sortingLayerID   = srCover.sortingLayerID;
            cRenderer.sortingOrder     = srCover.sortingOrder + TRAIL_SORT_OFFSET;
            cRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cRenderer.receiveShadows   = false;

            Refresh_All();
            return true;
        }

        // Sprite.Create가 만든 스프라이트는 임포트된 에셋이 아니라 런타임 인스턴스다.
        // 텍스처만 파괴하면 스프라이트가 그대로 새고, 렌더러는 파괴된 텍스처를 물고 깨져 보인다.
        public void Release()
        {
            Clear_Mask();
            Clear_Sprite(m_srReveal, ref m_spReveal);

            if (m_goTrailRoot != null)
                Object.Destroy(m_goTrailRoot);
            if (m_cTrailMesh != null)
                Object.Destroy(m_cTrailMesh);
            if (m_cTrailMaterial != null)
                Object.Destroy(m_cTrailMaterial);
            if (m_texTrail != null)
                Object.Destroy(m_texTrail);

            m_goTrailRoot    = null;
            m_cTrailFilter   = null;
            m_cTrailMesh     = null;
            m_cTrailMaterial = null;
            m_texTrail       = null;

            m_cGrid     = null;
            m_srCover   = null;
            m_srReveal  = null;
        }

        private void Clear_Mask()
        {
            Clear_Sprite(m_srCover, ref m_spMask);

            if (m_texMask != null)
                Object.Destroy(m_texMask);

            m_texMask        = null;
            m_arrPixel       = null;
            m_arrCoverPixel  = null;
            m_arrRowCoverage = null;
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
            m_arrRowCoverage = new float[m_iTexWidth];

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
        /// <summary> 점령지가 바뀌었을 때만 가림막을 다시 뚫는다. 선은 매 프레임 다시 그린다(점 몇 개뿐이다). </summary>
        public void Tick()
        {
            if (m_cGrid == null)
                return;

            if (m_cGrid.IS_DIRTY == true)
            {
                Refresh_All();
                m_cGrid.Clear_Dirty();
            }

            Refresh_Trail();
        }

        // 260923_가림막을 점령지 다각형 모양으로 뚫는다. 픽셀 한 줄을 COVERAGE_SUBROW번 나눠 가로줄이 경계와 만나는
        // x를 구하고, 그 사이에 든 몫만큼 픽셀을 투명하게 한다 — 가장자리가 반쯤 걸친 픽셀은 반쯤 비친다.
        private void Refresh_All()
        {
            if (m_texMask == null)
                return;

            IReadOnlyList<Vector2[]> lstRing = m_cGrid.RINGS;
            float fPixelPerCellX = (float)m_iTexWidth / m_cGrid.WIDTH;
            float fCellPerPixelY = (float)m_cGrid.HEIGHT / m_iTexHeight;
            float fSubWeight     = 1f / COVERAGE_SUBROW;

            for (int py = 0; py < m_iTexHeight; ++py)
            {
                System.Array.Clear(m_arrRowCoverage, 0, m_iTexWidth);

                for (int iSub = 0; iSub < COVERAGE_SUBROW; ++iSub)
                {
                    float fGridY = (py + (iSub + 0.5f) * fSubWeight) * fCellPerPixelY;
                    CPolygon_Utility.Collect_RowCrossings(lstRing, fGridY, m_lstCross);

                    for (int k = 0; k + 1 < m_lstCross.Count; k += 2)
                        Add_Span(m_lstCross[k] * fPixelPerCellX, m_lstCross[k + 1] * fPixelPerCellX, fSubWeight);
                }

                int iRow  = py * m_iTexWidth;
                int iCellY = Mathf.Min(m_cGrid.HEIGHT - 1, (int)(py * fCellPerPixelY));

                for (int px = 0; px < m_iTexWidth; ++px)
                {
                    int iCellX = Mathf.Min(m_cGrid.WIDTH - 1, (int)(px / fPixelPerCellX));
                    if (m_cGrid.Get_Cell(iCellX, iCellY) == CELL_STATE.BLOCK)
                    {
                        m_arrPixel[iRow + px] = COLOR_BLOCK;
                        continue;
                    }

                    Color32 cColor = m_arrCoverPixel[iRow + px];
                    cColor.a = (byte)Mathf.RoundToInt(255f * (1f - Mathf.Clamp01(m_arrRowCoverage[px])));
                    m_arrPixel[iRow + px] = cColor;
                }
            }

            m_texMask.SetPixels32(m_arrPixel);
            m_texMask.Apply(false);
        }

        // 픽셀 좌표 [fX0, fX1)을 덮었다 — 걸친 몫만큼 더한다
        private void Add_Span(float fX0, float fX1, float fWeight)
        {
            fX0 = Mathf.Max(0f, fX0);
            fX1 = Mathf.Min(m_iTexWidth, fX1);
            if (fX1 <= fX0)
                return;

            int iStart = (int)fX0;
            int iEnd   = Mathf.Min(m_iTexWidth - 1, (int)fX1);

            if (iStart == iEnd)
            {
                m_arrRowCoverage[iStart] += (fX1 - fX0) * fWeight;
                return;
            }

            m_arrRowCoverage[iStart] += (iStart + 1 - fX0) * fWeight;
            for (int px = iStart + 1; px < iEnd; ++px)
                m_arrRowCoverage[px] += fWeight;

            if (iEnd < m_iTexWidth)
                m_arrRowCoverage[iEnd] += (fX1 - iEnd) * fWeight;
        }

        // 260923_긋는 중인 선. 도화선이 탄 구간은 빼고, 불 머리는 주황으로 그린다
        private void Refresh_Trail()
        {
            if (m_cTrailMesh == null)
                return;

            m_lstVertex.Clear();
            m_lstIndex.Clear();
            m_lstColor.Clear();

            if (m_cGrid.IS_DRAWING == true)
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
                        Draw_Part(lstPiece[p], 0f, fLength, COLOR_TRAIL);
                    }
                    else
                    {
                        // 이 조각에서 탄 구간 [fBurnFrom, fBurnTo]를 뺀 앞 · 뒤만 그린다
                        Draw_Part(lstPiece[p], 0f, Mathf.Min(fLength, fBurnFrom - fOffset), COLOR_TRAIL);
                        Draw_Part(lstPiece[p], Mathf.Max(0f, fBurnTo - fOffset), fLength, COLOR_TRAIL);
                        Draw_Part(lstPiece[p], Mathf.Max(0f, fBurnTo - fOffset - FIRE_LENGTH_CELL),
                                  Mathf.Min(fLength, fBurnTo - fOffset), COLOR_FIRE);
                    }

                    fOffset += fLength;
                }
            }

            m_cTrailMesh.Clear();
            if (m_lstIndex.Count == 0)
                return;

            m_cTrailMesh.SetVertices(m_lstVertex);
            m_cTrailMesh.SetColors(m_lstColor);
            m_cTrailMesh.SetTriangles(m_lstIndex, 0);
            m_cTrailMesh.RecalculateBounds();
        }

        // 폴리라인에서 길이 [fFrom, fTo] 구간만 선 하나로 그린다
        private void Draw_Part(List<Vector2> lstLine, float fFrom, float fTo, Color cColor)
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

            CTrailMesh_Utility.Append(m_lstLinePoint, TRAIL_WIDTH_CELL * m_cGrid.CELL_SIZE, cColor,
                                      m_lstVertex, m_lstIndex, m_lstColor);
        }

        #endregion 갱신
    }
}
