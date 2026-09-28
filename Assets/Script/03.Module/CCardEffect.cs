namespace Client
{
    // 260928_카드 시스템 재작성(Docs/Design_Roguelite_Rewrite.md 6장) — CRunSkillEffect와 같은 조합
    // 구조다(1-1). CPlayer가 List로 여러 개를 동시에 들고, 레벨이 바뀔 때(처음 획득 포함)
    // On_LevelChanged가 한 번 불린다.
    /// <summary>
    /// 실제 카드(K/M/G/F 30종)는 태스크 #22에서 여기 자식 클래스로 붙는다. 지금은 골격뿐이다 —
    /// <see cref="Create"/>가 아무것도 못 만드는 것은 버그가 아니라 "아직 카드가 없다"는 뜻이다
    /// (다음 태스크에서 케이스를 하나씩 채워 나간다, <c>CRunSkillEffect.Create</c>가 그랬던 것과 같은 자리).
    /// </summary>
    public abstract class CCardEffect
    {
        protected CPlayer m_cOwner;
        protected int      m_iLevel;

        public CARD_TYPE TYPE { get; private set; }

        public static CCardEffect Create(CARD_TYPE eType)
        {
            // 260928_태스크 #22에서 카드가 하나씩 늘 때마다 여기 케이스가 붙는다
            // (CRunSkillEffect.Create처럼 switch로 만들고 cEffect.TYPE = eType;로 표시할 것).
            return null;
        }

        public void Initialize(CPlayer cOwner) => m_cOwner = cOwner;

        /// <summary> 레벨이 바뀔 때(1레벨 최초 획득 포함) 불린다. </summary>
        public virtual void On_LevelChanged(int iLevel) => m_iLevel = iLevel;

        public virtual void Tick(float fDeltaTime) { }

        /// <summary> 카드를 잃을 때(스테이지 종료) — 걸어 둔 플래그 · 구독을 되돌린다. </summary>
        public virtual void Release() { }
    }
}
