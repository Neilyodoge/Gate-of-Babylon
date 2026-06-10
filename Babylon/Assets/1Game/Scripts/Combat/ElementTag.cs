namespace XianTu
{
    /// <summary>
    /// 元素标签 —— 用于 StatusEffect、灵物 modTag、技能 modifier。
    /// 与 GDD 6.7.1 / 6.5.3 / 5.6 元素反应表保持一致。
    /// </summary>
    public enum ElementTag
    {
        None = 0,
        Fire,
        Ice,
        Thunder,
        Wind,
        Wood,
        Water,
        Earth,
        Pierce,   // 穿透系（飞剑等）
        Life      // 生命系（灵藤草、回灵丹等续航类）
    }
}
