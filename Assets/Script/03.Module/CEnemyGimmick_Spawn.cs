using UnityEngine;

namespace Client
{
    // 260904_부하 소환 기믹
    /// <summary>
    /// Cool=소환주기, Value=소환 마리수, RefID=소환할 몬스터의 EnemyInfo ID.
    /// 소환 총량은 스테이지가 막는다 — RefID가 다시 SPAWN 몬스터를 가리키면 무한히 늘어나기 때문.
    /// </summary>
    public class CEnemyGimmick_Spawn : CEnemyGimmick
    {
        protected override void Fire(Vector2 vPlayerPos)
        {
            int iCount = Mathf.RoundToInt(m_fValue);
            if (m_iRefID <= 0 || iCount <= 0)
                return;

            m_cHost.Spawn_Minion(m_iRefID, iCount, m_cOwner.POS);
        }
    }
}
