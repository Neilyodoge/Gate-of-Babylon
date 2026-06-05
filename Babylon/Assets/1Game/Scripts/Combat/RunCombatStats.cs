namespace XianTu
{
    /// <summary>
    /// 本局（run）战斗统计。轮回一击等"按累计伤害结算"的机制读这里。
    /// 进入新一局时由 GameManager 调 <see cref="Reset"/> 清零。
    /// </summary>
    public static class RunCombatStats
    {
        /// <summary>本局玩家对敌人造成的累计总伤害。</summary>
        public static float TotalPlayerDamage { get; private set; }

        public static void AddPlayerDamage(float amount)
        {
            if (amount > 0f) TotalPlayerDamage += amount;
        }

        public static void Reset()
        {
            TotalPlayerDamage = 0f;
        }
    }
}
