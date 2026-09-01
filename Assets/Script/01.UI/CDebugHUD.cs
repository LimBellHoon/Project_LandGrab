using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 임시 디버그 HUD (정식 UI 전까지만 사용)
    public class CDebugHUD : MonoBehaviour
    {
        private GUIStyle m_cStyle;

        private void OnGUI()
        {
            CStage_Manager cStage = CGameManager.STAGE_MANAGER;
            if (cStage == null || cStage.GRID == null)
                return;

            if (m_cStyle == null)
            {
                m_cStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize  = 22,
                    fontStyle = FontStyle.Bold,
                };
                m_cStyle.normal.textColor = Color.white;
            }

            GUILayout.BeginArea(new Rect(16f, 16f, 460f, 200f));
            GUILayout.Label($"점령률   {cStage.OWNED_RATIO:P1}  /  목표 {cStage.CLEAR_RATIO:P0}", m_cStyle);
            GUILayout.Label($"남은 시간 {cStage.REMAIN_TIME:F1}s     목숨 {cStage.LIFE}", m_cStyle);
            GUILayout.Label($"상태     {cStage.STATE}", m_cStyle);
            GUILayout.Label("WASD / 방향키로 이동", m_cStyle);
            GUILayout.EndArea();
        }
    }
}
