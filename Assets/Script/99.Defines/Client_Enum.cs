namespace Client
{
    // 260901_땅따먹기 프로토타입 정의
    /// <summary> 그리드 한 칸의 상태 </summary>
    public enum CELL_STATE : byte
    {
        EMPTY = 0,      // 미점령 (바깥 = 위험 지대)
        OWNED = 1,      // 점령 완료 (플레이어 안전 지대)
        TRAIL = 2,      // 점령 시도 중 남긴 선분
    }

    /// <summary> 그리드 이동 방향 (4방향) </summary>
    public enum MOVE_DIR
    {
        NONE = -1,
        UP = 0,
        DOWN = 1,
        LEFT = 2,
        RIGHT = 3,
    }

    /// <summary> 셀에 도착했을 때 일어난 일 (CTerritoryGrid.Step_To의 결과) </summary>
    public enum STEP_RESULT
    {
        SAFE,       // 안전 지대 위를 이동
        DRAW,       // 미점령 지대에 선분을 남김
        CAPTURE,    // 도형이 닫혀 영토를 점령
        DEAD,       // 자기 선분을 밟음
    }

    /// <summary> 스테이지 진행 상태 </summary>
    public enum STAGE_STATE
    {
        READY,
        PLAYING,
        CLEAR,
        FAIL,
    }

    /// <summary> Addressable 라벨 (Engine.CData_Manager.LoadAssetAsync 인자) </summary>
    public static class CAddressableLabel
    {
        public const string PREFAB = "Prefabs";
        public const string TEXTURE = "Images";
    }
}
