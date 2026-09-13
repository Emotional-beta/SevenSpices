namespace SevenSpices.Core.Content;

/// <summary>
/// 锅结束奖励配置（设计文档 §17）。
/// 普通锅结束后，玩家获得一次 <b>X 选 1</b> 食材选择，选中的食材直接进入食材篮。
/// <see cref="ChoiceCount"/>（X）是后续调优的<b>唯一入口</b>：
/// 当前为全局固定值，后续可按章节 / 锅动态化，不要在流程代码里分散硬编码。
/// 纯 C#，数据驱动，不依赖 Godot。
/// </summary>
public class PotRewardConfig
{
    /// <summary>
    /// 普通锅结束奖励的候选数量 X（默认 3）。
    /// 候选从正式食材 Registry 中随机抽取且互不重复；超过食材总数时返回全部。
    /// </summary>
    public int ChoiceCount { get; init; } = 3;
}
