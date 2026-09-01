using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 그리드 상태를 마스크 텍스처로 그리기
    /// <summary>
    /// 셀 1칸 = 텍스처 1픽셀인 오버레이를 배경 이미지 위에 덮는다.
    /// 점령한 칸은 알파 0이 되어 뒤 이미지가 드러난다 (= 이 게임의 목적).
    /// 셰이더 없이 SpriteRenderer + Point 필터만으로 동작한다.
    /// </summary>
    public class CGridRenderer
    {
        // 미점령 = 뒤 이미지를 가리는 어두운 막, 점령 = 완전 투명, 트레일 = 눈에 띄는 선
        private static readonly Color32 COLOR_EMPTY = new Color32(8, 10, 20, 235);
        private static readonly Color32 COLOR_OWNED = new Color32(0, 0, 0, 0);
        private static readonly Color32 COLOR_TRAIL = new Color32(90, 225, 255, 255);

        private CTerritoryGrid  m_cGrid;
        private SpriteRenderer  m_srOverlay;
        private Texture2D       m_texMask;
        private Color32[]       m_arrPixel;     // SetPixels32용 버퍼 — 매 갱신마다 재사용

        public bool Initialize(CTerritoryGrid cGrid, SpriteRenderer srOverlay)
        {
            if (cGrid == null || srOverlay == null)
            {
                Debug.LogError("[CGridRenderer] Grid 또는 Overlay SpriteRenderer가 null 입니다.");
                return false;
            }

            m_cGrid     = cGrid;
            m_srOverlay = srOverlay;

            int iWidth  = cGrid.WIDTH;
            int iHeight = cGrid.HEIGHT;

            m_texMask = new Texture2D(iWidth, iHeight, TextureFormat.RGBA32, false)
            {
                name        = "Tex_TerritoryMask",
                filterMode  = FilterMode.Point,     // 셀 경계가 뭉개지지 않도록
                wrapMode    = TextureWrapMode.Clamp,
            };
            m_arrPixel = new Color32[iWidth * iHeight];

            // pixelsPerUnit을 셀 크기의 역수로 두면 스프라이트 월드 크기 = 그리드 월드 크기가 된다.
            Sprite spMask = Sprite.Create(m_texMask, new Rect(0f, 0f, iWidth, iHeight),
                                          new Vector2(0.5f, 0.5f), 1f / cGrid.CELL_SIZE, 0u, SpriteMeshType.FullRect);
            spMask.name = "Sprite_TerritoryMask";

            m_srOverlay.sprite = spMask;
            m_srOverlay.transform.position = new Vector3(cGrid.WORLD_CENTER.x, cGrid.WORLD_CENTER.y, 0f);

            Refresh();
            return true;
        }

        public void Release()
        {
            if (m_texMask != null)
                Object.Destroy(m_texMask);

            m_texMask   = null;
            m_arrPixel  = null;
            m_cGrid     = null;
        }

        /// <summary> 그리드가 변했을 때만 텍스처를 다시 올린다. </summary>
        public void Tick()
        {
            if (m_cGrid == null || m_cGrid.IS_DIRTY == false)
                return;

            Refresh();
            m_cGrid.Clear_Dirty();
        }

        private void Refresh()
        {
            // 그리드 인덱스(y * W + x, y=0이 아래)와 Texture2D 픽셀 순서가 그대로 일치한다.
            for (int i = 0; i < m_arrPixel.Length; ++i)
            {
                switch (m_cGrid.Get_Cell(i % m_cGrid.WIDTH, i / m_cGrid.WIDTH))
                {
                    case CELL_STATE.OWNED: m_arrPixel[i] = COLOR_OWNED; break;
                    case CELL_STATE.TRAIL: m_arrPixel[i] = COLOR_TRAIL; break;
                    default:               m_arrPixel[i] = COLOR_EMPTY; break;
                }
            }

            m_texMask.SetPixels32(m_arrPixel);
            m_texMask.Apply(false);
        }
    }
}
