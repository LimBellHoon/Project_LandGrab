using UnityEngine;

namespace Client
{
    // 260912_세로 화면 — 노치 / 홈 인디케이터 회피
    /// <summary>
    /// 붙은 RectTransform을 <see cref="Screen.safeArea"/> 안쪽으로 줄인다.
    /// UI 루트에 하나만 달면 자식이 전부 따라온다.
    ///
    /// 세로 화면 게임은 위쪽 노치와 아래쪽 홈 인디케이터에 잘리기 쉽다.
    /// 특히 아래쪽은 조이스틱과 버튼이 앉는 자리라 몇 픽셀만 잘려도 조작이 막힌다.
    ///
    /// 에디터 Game 뷰에서는 safeArea가 화면 전체라 아무 일도 일어나지 않는다 —
    /// 기기에서만 값이 생기므로, 여기서 하는 일이 없다고 해서 잘못된 것이 아니다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CSafeArea : MonoBehaviour
    {
        private RectTransform   m_trSelf;
        private Rect            m_rcLastSafe;
        private int             m_iLastWidth;
        private int             m_iLastHeight;

        private void Awake()
        {
            m_trSelf = GetComponent<RectTransform>();
            Apply();
        }

        private void OnEnable() => Apply();

        // 회전하거나 창 크기가 바뀌면 safeArea도 바뀐다.
        private void Update()
        {
            if (Screen.safeArea == m_rcLastSafe
                && Screen.width == m_iLastWidth && Screen.height == m_iLastHeight)
                return;

            Apply();
        }

        private void Apply()
        {
            if (m_trSelf == null)
                m_trSelf = GetComponent<RectTransform>();

            int iWidth  = Screen.width;
            int iHeight = Screen.height;
            if (iWidth <= 0 || iHeight <= 0)
                return;

            m_rcLastSafe  = Screen.safeArea;
            m_iLastWidth  = iWidth;
            m_iLastHeight = iHeight;

            Vector2 vMin = Calc_AnchorMin(m_rcLastSafe, iWidth, iHeight);
            Vector2 vMax = Calc_AnchorMax(m_rcLastSafe, iWidth, iHeight);

            m_trSelf.anchorMin = vMin;
            m_trSelf.anchorMax = vMax;
            m_trSelf.offsetMin = Vector2.zero;
            m_trSelf.offsetMax = Vector2.zero;
        }

        // 화면 없이도 검증할 수 있게 계산만 떼어 둔다 (CProtoTest).
        public static Vector2 Calc_AnchorMin(Rect rcSafe, int iWidth, int iHeight)
        {
            return new Vector2(Mathf.Clamp01(rcSafe.xMin / iWidth), Mathf.Clamp01(rcSafe.yMin / iHeight));
        }

        public static Vector2 Calc_AnchorMax(Rect rcSafe, int iWidth, int iHeight)
        {
            return new Vector2(Mathf.Clamp01(rcSafe.xMax / iWidth), Mathf.Clamp01(rcSafe.yMax / iHeight));
        }
    }
}
