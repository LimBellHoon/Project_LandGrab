using UnityEngine;

namespace Client
{
    // 260901_땅따먹기 프로토타입: 임시 디버그 HUD (정식 UI 전까지만 사용)
    public class CDebugHUD : MonoBehaviour
    {
        // 260921_F1로 켜고 끈다. 정식 HUD(CUI_InGame)와 겹치는 정보라 평소에는 가려 두고,
        // 수치를 들여다볼 때만 띄운다. 처음 상태는 GameConfig.asset의 m_bDebugHudVisible(1-6).
        private const KeyCode TOGGLE_KEY = KeyCode.F1;

        private GUIStyle m_cStyle;
        private bool     m_bVisible;

        private void Awake() => m_bVisible = CGameConfig.Load().DEBUG_HUD_VISIBLE;

        private void Update()
        {
            if (Input.GetKeyDown(TOGGLE_KEY) == true)
                m_bVisible = !m_bVisible;
        }

        private void OnGUI()
        {
            if (m_bVisible == false)
                return;

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

            // 260904_웨이브 표시 추가
            GUILayout.BeginArea(new Rect(16f, 16f, 520f, 240f));
            // 260905_별 표시 추가
            GUILayout.Label($"{cStage.MAP_NAME}   웨이브 {cStage.WAVE} / {cStage.WAVE_COUNT}"
                          + $"   {CStar_Utility.Get_Text(cStage.STAR, cStage.WAVE_COUNT)}", m_cStyle);
            GUILayout.Label($"점령률   {cStage.OWNED_RATIO:P1}  /  목표 {cStage.CLEAR_RATIO:P0}", m_cStyle);
            GUILayout.Label($"남은 시간 {cStage.REMAIN_TIME:F1}s     목숨 {cStage.LIFE}/{cStage.MAX_LIFE}     몬스터 {cStage.ENEMY_COUNT}", m_cStyle);
            GUILayout.Label($"상태     {cStage.STATE}", m_cStyle);
            GUILayout.Label("WASD / 방향키로 이동     F1 디버그 끄기     F2 하트 +10", m_cStyle);
            GUILayout.EndArea();
        }
    }
}
