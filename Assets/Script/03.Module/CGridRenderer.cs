using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 그리드 상태를 마스크 텍스처로 그리기
    // 260904_웨이브 이미지 스택 — 가림막을 걷어내면 아래 이미지가 드러난다
    /// <summary>
    /// 화면은 두 장으로 이뤄진다.
    ///   · srReveal — 이번 웨이브를 다 점령하면 드러날 이미지. 항상 전체가 깔려 있다.
    ///   · srCover  — 그 위를 덮는 가림막. 점령한 칸만 알파 0으로 뚫어 아래를 보여준다.
    /// 가림막 텍스처는 셀 격자에 맞춰 다시 찍은 사본이다. 원본을 그대로 쓰면 셀 단위로
    /// 구멍을 낼 수 없기 때문 — 셰이더 없이 SpriteRenderer만으로 해결하려는 제약 때문이다.
    ///
    /// N웨이브의 가림막은 이미지 스택의 [N-1]이고, 그걸 다 걷으면 [N]이 나온다.
    /// 그래서 1웨이브의 가림막이 곧 '마스크'다 (MapInfo.csv의 strLayerTex 참고).
    /// </summary>
    public class CGridRenderer
    {
        // 셀 하나를 텍스처 몇 픽셀로 찍을지. 1이면 가림막 그림이 셀 크기로 뭉개진다.
        // 올릴수록 가림막이 선명해지지만 갱신 비용과 메모리가 제곱으로 는다.
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
        private Color32[]   m_arrCellPixel;     // 셀 한 칸(PIXEL_PER_CELL 제곱) 부분 갱신용 버퍼

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

            m_iTexWidth  = cGrid.WIDTH * PIXEL_PER_CELL;
            m_iTexHeight = cGrid.HEIGHT * PIXEL_PER_CELL;

            m_texMask = new Texture2D(m_iTexWidth, m_iTexHeight, TextureFormat.RGBA32, false)
            {
                name        = "Tex_TerritoryMask",
                filterMode  = FilterMode.Point,     // 셀 경계가 뭉개지지 않도록
                wrapMode    = TextureWrapMode.Clamp,
            };

            m_arrPixel      = new Color32[m_iTexWidth * m_iTexHeight];
            m_arrCoverPixel = new Color32[m_iTexWidth * m_iTexHeight];
            m_arrCellPixel  = new Color32[PIXEL_PER_CELL * PIXEL_PER_CELL];

            Fill_Cover(null);

            // pixelsPerUnit을 '셀 하나당 픽셀 수 / 셀 크기'로 두면 스프라이트 월드 크기 = 그리드 월드 크기.
            m_spMask = Sprite.Create(m_texMask, new Rect(0f, 0f, m_iTexWidth, m_iTexHeight),
                                     new Vector2(0.5f, 0.5f), PIXEL_PER_CELL / cGrid.CELL_SIZE,
                                     0u, SpriteMeshType.FullRect);
            m_spMask.name = "Sprite_TerritoryMask";

            m_srCover.sprite = m_spMask;
            m_srCover.transform.position = new Vector3(cGrid.WORLD_CENTER.x, cGrid.WORLD_CENTER.y, 0f);
            m_srCover.transform.localScale = Vector3.one;

            Refresh_All();
            return true;
        }

        // Sprite.Create가 만든 스프라이트는 임포트된 에셋이 아니라 런타임 인스턴스다.
        // 텍스처만 파괴하면 스프라이트가 그대로 새고, 렌더러는 파괴된 텍스처를 물고 깨져 보인다.
        public void Release()
        {
            Clear_Sprite(m_srCover, ref m_spMask);
            Clear_Sprite(m_srReveal, ref m_spReveal);

            if (m_texMask != null)
                Object.Destroy(m_texMask);

            m_texMask       = null;
            m_arrPixel      = null;
            m_arrCoverPixel = null;
            m_arrCellPixel  = null;
            m_cGrid         = null;
            m_srCover       = null;
            m_srReveal      = null;
        }

        private static void Clear_Sprite(SpriteRenderer srTarget, ref Sprite spOwned)
        {
            if (srTarget != null && srTarget.sprite == spOwned)
                srTarget.sprite = null;

            if (spOwned != null)
                Object.Destroy(spOwned);

            spOwned = null;
        }
        #endregion 초기화 / 해제

        #region 웨이브 이미지
        // 260904_웨이브가 넘어갈 때마다 두 장을 갈아 끼운다.
        /// <param name="texCover"> 이번 웨이브를 덮을 가림막 (이미지 스택의 [웨이브-1]) </param>
        /// <param name="texReveal"> 다 점령하면 드러날 이미지 (이미지 스택의 [웨이브]) </param>
        public void Set_WaveTexture(Texture2D texCover, Texture2D texReveal)
        {
            Fill_Cover(texCover);
            Set_RevealTexture(texReveal);
            Refresh_All();
        }

        private void Set_RevealTexture(Texture2D texReveal)
        {
            if (m_srReveal == null)
                return;

            Clear_Sprite(m_srReveal, ref m_spReveal);

            if (texReveal == null)
                return;

            m_spReveal = Sprite.Create(texReveal, new Rect(0f, 0f, texReveal.width, texReveal.height),
                                       new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect);
            m_spReveal.name = "Sprite_Reveal";
            m_srReveal.sprite = m_spReveal;

            // 보상 이미지를 그리드와 정확히 같은 크기로 맞춘다 (원본 비율은 무시하고 꽉 채운다).
            Vector2 vSpriteSize = m_spReveal.bounds.size;
            if (vSpriteSize.x <= 0f || vSpriteSize.y <= 0f)
                return;

            Vector2 vWorldSize = m_cGrid.WORLD_SIZE;
            m_srReveal.transform.position   = new Vector3(m_cGrid.WORLD_CENTER.x, m_cGrid.WORLD_CENTER.y, 0f);
            m_srReveal.transform.localScale = new Vector3(vWorldSize.x / vSpriteSize.x,
                                                          vWorldSize.y / vSpriteSize.y, 1f);
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

        private void Refresh_All()
        {
            if (m_texMask == null)
                return;

            int iCellCount = m_cGrid.WIDTH * m_cGrid.HEIGHT;

            for (int i = 0; i < iCellCount; ++i)
            {
                CELL_STATE eState = m_cGrid.Get_Cell(i);
                int px0 = (i % m_cGrid.WIDTH) * PIXEL_PER_CELL;
                int py0 = (i / m_cGrid.WIDTH) * PIXEL_PER_CELL;

                for (int dy = 0; dy < PIXEL_PER_CELL; ++dy)
                {
                    int iRow = (py0 + dy) * m_iTexWidth + px0;

                    for (int dx = 0; dx < PIXEL_PER_CELL; ++dx)
                        m_arrPixel[iRow + dx] = Get_PixelColor(eState, iRow + dx);
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

                int px0 = (iIndex % m_cGrid.WIDTH) * PIXEL_PER_CELL;
                int py0 = (iIndex / m_cGrid.WIDTH) * PIXEL_PER_CELL;

                for (int dy = 0; dy < PIXEL_PER_CELL; ++dy)
                {
                    int iRow = (py0 + dy) * m_iTexWidth + px0;

                    for (int dx = 0; dx < PIXEL_PER_CELL; ++dx)
                    {
                        Color32 cColor = Get_PixelColor(eState, iRow + dx);
                        m_arrPixel[iRow + dx]                   = cColor;
                        m_arrCellPixel[dy * PIXEL_PER_CELL + dx] = cColor;
                    }
                }

                m_texMask.SetPixels32(px0, py0, PIXEL_PER_CELL, PIXEL_PER_CELL, m_arrCellPixel);
            }

            m_texMask.Apply(false);
        }

        private Color32 Get_PixelColor(CELL_STATE eState, int iPixel)
        {
            switch (eState)
            {
                case CELL_STATE.OWNED: return COLOR_OWNED;
                case CELL_STATE.TRAIL: return COLOR_TRAIL;
                case CELL_STATE.BLOCK: return COLOR_BLOCK;
                default:               return m_arrCoverPixel[iPixel];
            }
        }
        #endregion 갱신
    }
}
