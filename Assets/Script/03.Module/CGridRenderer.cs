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

        private static readonly Color32 COLOR_OWNED     = new Color32(0, 0, 0, 0);          // 뚫린 칸
        private static readonly Color32 COLOR_TRAIL     = new Color32(90, 225, 255, 255);   // 그리는 중인 선
        private static readonly Color32 COLOR_BLOCK     = new Color32(0, 0, 0, 255);        // 맵 밖
        private static readonly Color32 COLOR_FALLBACK  = new Color32(8, 10, 20, 235);      // 가림막을 못 읽었을 때

        private CTerritoryGrid  m_cGrid;
        private SpriteRenderer  m_srCover;
        private SpriteRenderer  m_srReveal;

        private Texture2D   m_texMask;
        private Sprite      m_spMask;
        private Sprite      m_spReveal;

        private Color32[]   m_arrPixel;         // 마스크 전체 픽셀
        private Color32[]   m_arrCoverPixel;    // 가림막 이미지를 마스크 해상도로 미리 샘플링해 둔 것
        private Color32[]   m_arrCellPixel;     // 셀 한 칸 부분 갱신용 버퍼

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

            Refresh_All();
            return true;
        }

        // Sprite.Create가 만든 스프라이트는 임포트된 에셋이 아니라 런타임 인스턴스다.
        // 텍스처만 파괴하면 스프라이트가 그대로 새고, 렌더러는 파괴된 텍스처를 물고 깨져 보인다.
        public void Release()
        {
            Clear_Mask();
            Clear_Sprite(m_srReveal, ref m_spReveal);

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
            m_arrCellPixel  = null;
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
                filterMode  = FilterMode.Point,     // 셀 경계가 뭉개지지 않도록
                wrapMode    = TextureWrapMode.Clamp,
            };

            m_arrPixel      = new Color32[m_iTexWidth * m_iTexHeight];
            m_arrCoverPixel = new Color32[m_iTexWidth * m_iTexHeight];

            // 칸마다 픽셀 수가 1씩 다를 수 있어(나누어떨어지지 않는 경우) 가장 큰 칸에 맞춰 둔다.
            int iMaxW = Mathf.CeilToInt((float)m_iTexWidth / m_cGrid.WIDTH) + 1;
            int iMaxH = Mathf.CeilToInt((float)m_iTexHeight / m_cGrid.HEIGHT) + 1;
            m_arrCellPixel = new Color32[iMaxW * iMaxH];

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
        /// <summary> 그리드가 변했을 때만 텍스처를 다시 올린다. </summary>
        public void Tick()
        {
            if (m_cGrid == null || m_cGrid.IS_DIRTY == false)
                return;

            // 260904_선을 그리는 동안에는 한 프레임에 한두 칸만 바뀐다.
            // 전체를 다시 찍으면 모바일에서 그대로 낭비이므로 바뀐 칸만 올린다.
            if (m_cGrid.IS_FULL_DIRTY == true)
                Refresh_All();
            else
                Refresh_DirtyCells();

            m_cGrid.Clear_Dirty();
        }

        // 260912_칸 하나가 차지하는 픽셀 범위. 텍스처 크기가 칸 수로 나누어떨어지지 않아도
        // 빈틈이나 겹침이 생기지 않도록 '다음 칸의 시작'을 끝으로 삼는다.
        private int Cell_ToPixelX(int x) => x * m_iTexWidth / m_cGrid.WIDTH;
        private int Cell_ToPixelY(int y) => y * m_iTexHeight / m_cGrid.HEIGHT;

        private void Refresh_All()
        {
            if (m_texMask == null)
                return;

            int iCellCount = m_cGrid.WIDTH * m_cGrid.HEIGHT;

            for (int i = 0; i < iCellCount; ++i)
            {
                CELL_STATE eState = m_cGrid.Get_Cell(i);

                int cx = i % m_cGrid.WIDTH;
                int cy = i / m_cGrid.WIDTH;

                int px0 = Cell_ToPixelX(cx);
                int px1 = Cell_ToPixelX(cx + 1);
                int py0 = Cell_ToPixelY(cy);
                int py1 = Cell_ToPixelY(cy + 1);

                for (int py = py0; py < py1; ++py)
                {
                    int iRow = py * m_iTexWidth;

                    for (int px = px0; px < px1; ++px)
                        m_arrPixel[iRow + px] = Get_PixelColor(eState, i, iRow + px);
                }
            }

            m_texMask.SetPixels32(m_arrPixel);
            m_texMask.Apply(false);
        }

        private void Refresh_DirtyCells()
        {
            if (m_texMask == null)
                return;

            System.Collections.Generic.IReadOnlyList<int> lstDirty = m_cGrid.DIRTY_CELLS;

            for (int n = 0; n < lstDirty.Count; ++n)
            {
                int iIndex = lstDirty[n];
                CELL_STATE eState = m_cGrid.Get_Cell(iIndex);

                int cx = iIndex % m_cGrid.WIDTH;
                int cy = iIndex / m_cGrid.WIDTH;

                int px0 = Cell_ToPixelX(cx);
                int py0 = Cell_ToPixelY(cy);
                int iW  = Cell_ToPixelX(cx + 1) - px0;
                int iH  = Cell_ToPixelY(cy + 1) - py0;

                if (iW <= 0 || iH <= 0)
                    continue;

                for (int dy = 0; dy < iH; ++dy)
                {
                    int iRow = (py0 + dy) * m_iTexWidth;

                    for (int dx = 0; dx < iW; ++dx)
                    {
                        Color32 cColor = Get_PixelColor(eState, iIndex, iRow + px0 + dx);
                        m_arrPixel[iRow + px0 + dx] = cColor;
                        m_arrCellPixel[dy * iW + dx] = cColor;
                    }
                }

                m_texMask.SetPixels32(px0, py0, iW, iH, m_arrCellPixel);
            }

            m_texMask.Apply(false);
        }

        // 260912_시작 테두리는 점령 상태지만 뚫지 않는다.
        // 스테이지에 들어서자마자 보상이 테두리처럼 비치면 '드러내는 재미'가 먼저 새어 나간다.
        // 플레이어가 직접 딴 땅만 구멍이 난다.
        private Color32 Get_PixelColor(CELL_STATE eState, int iCellIndex, int iPixel)
        {
            switch (eState)
            {
                case CELL_STATE.OWNED:
                    return m_cGrid.Is_StartOwned(iCellIndex) == true ? m_arrCoverPixel[iPixel]
                                                                    : COLOR_OWNED;
                case CELL_STATE.TRAIL: return COLOR_TRAIL;
                case CELL_STATE.BLOCK: return COLOR_BLOCK;
                default:               return m_arrCoverPixel[iPixel];
            }
        }
        #endregion 갱신
    }
}
