namespace XianTu
{
    /// <summary>
    /// 房间类型 —— 领域枚举，单一真源。
    ///
    /// V0.4.2 关卡层解耦：从 <c>Minimap</c> 内嵌枚举提升为顶层领域类型，
    /// 解除"UI 类持有领域模型"的反向依赖（原先 GameManager/Room 依赖 UI 的 Minimap.RoomType）。
    /// 成员顺序 / 整数值与旧 <c>Minimap.RoomType</c> 保持一致，避免任何潜在序列化偏移。
    /// </summary>
    public enum RoomType
    {
        Battle = 0,
        Shop = 1,
        Rest = 2,
        Treasure = 3,
        Boss = 4,
        Upgrade = 5,
        Elite = 6,
        Event = 7,
        Landing = 8,
    }
}
