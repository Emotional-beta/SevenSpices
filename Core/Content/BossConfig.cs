namespace SevenSpices.Core.Content;

/// <summary>
/// 单个饕餮形态的静态配置。
/// 数值全部标注 TBD，集中在此，不要散落硬编码。
/// </summary>
public class BossFormConfig
{
    /// <summary>形态 Id（方案 §三 约定，如 taotie_child）。</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>称谓语（如「饕餮·幼体」）。</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>满意度阈值 TBD（本锅累计最终分 &gt;= 该值即满意）。</summary>
    public int SatisfyThreshold { get; init; }

    /// <summary>台词（占位，可调）：毒舌、贪吃、嘴硬心软。</summary>
    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();

    /// <summary>
    /// 满意赏赐的道具 Id（默认仙丹粉末）。必须是 <see cref="ItemData.SpecialRegistry"/> 中已注册的 Id
    /// （当前仅 <see cref="ItemData.ImmortalPowderId"/>），否则结算时会抛异常。
    /// </summary>
    public string RewardItemId { get; init; } = ItemData.ImmortalPowderId;
}

/// <summary>
/// 饕餮 Boss 的静态配置：章节 → 形态 / 称谓 / 阈值 / 台词 / 赏赐。
/// 阈值与系数全部 TBD，集中在此，便于后续调参（CLAUDE.md §13 禁止散落硬编码）。
/// 章 / 锅推进仍以 <c>RunController</c> 的常量为准，此处不重复定义。
/// </summary>
public class BossConfig
{
    /// <summary>全局默认配置。</summary>
    public static BossConfig Default { get; } = new();

    /// <summary>第 1/2/3 章末形态，索引 0 = 第 1 章。四个 Id 用方案 §三 的约定。</summary>
    public IReadOnlyList<BossFormConfig> ChapterForms { get; init; } = new[]
    {
        new BossFormConfig
        {
            Id = "taotie_child",
            Name = "饕餮·幼体",
            SatisfyThreshold = 30,
            Lines = new[]
            {
                "就……就这点？本座还没尝出味儿呢！",
                "哼，算你识相，勉强……能吃吧。",
                "别、别以为本座馋，本座只是刚好饿了！",
            },
        },
        new BossFormConfig
        {
            Id = "taotie_maiden",
            Name = "饕餮·少女",
            SatisfyThreshold = 60,
            Lines = new[]
            {
                "喂，你这手艺……也就比路边摊强一点点吧？",
                "再给本座来一碗，本座可不是求你，是命令！",
                "唔……看在你这么用心的份上，本座记住了。",
            },
        },
        new BossFormConfig
        {
            Id = "taotie_lady",
            Name = "饕餮·御姐",
            SatisfyThreshold = 90,
            Lines = new[]
            {
                "火候尚可，可惜你还差得远呢，小厨子。",
                "能让本座多看一眼的粥……你倒是头一个。",
                "别得意，本座只是怕浪费粮食罢了。",
            },
        },
    };

    /// <summary>最终锅真身（方案 §三 taotie_true）。</summary>
    public BossFormConfig TrueForm { get; init; } = new BossFormConfig
    {
        Id = "taotie_true",
        Name = "饕餮",
        SatisfyThreshold = 120,
        Lines = new[]
        {
            "区区凡人，也敢喂我？……不过，确实香。",
            "千万年了，终于有人的味道，让我想起人间。",
            "再来。这一次，连你的魂一起尝。",
        },
    };

    /// <summary>章末被嫌弃时的保底评价文案（TBD，可调）。</summary>
    public string LoseLine { get; init; } = "嫌弃，但是鼓励";

    /// <summary>全部形态（3 个章末形态 + 真身）。</summary>
    public IEnumerable<BossFormConfig> AllForms => ChapterForms.Append(TrueForm);

    /// <summary>按 Id 查形态；找不到抛 <see cref="ArgumentException"/>。</summary>
    public BossFormConfig GetForm(string id)
    {
        foreach (var form in AllForms)
        {
            if (form.Id == id)
                return form;
        }

        throw new ArgumentException($"Boss form with Id '{id}' not found.", nameof(id));
    }

    /// <summary>
    /// 按 Id 查形态；找不到返回 null。
    /// 用于判定当前食客是否为饕餮（GameController），区别于「找不到即抛」的 <see cref="GetForm"/>。
    /// </summary>
    public BossFormConfig? FindForm(string id)
    {
        foreach (var form in AllForms)
        {
            if (form.Id == id)
                return form;
        }

        return null;
    }

    /// <summary>按章节（1-based）取章末形态；越界抛 <see cref="ArgumentOutOfRangeException"/>。</summary>
    public BossFormConfig GetChapterForm(int chapter)
    {
        if (chapter < 1 || chapter > ChapterForms.Count)
            throw new ArgumentOutOfRangeException(
                nameof(chapter), chapter, $"Chapter must be between 1 and {ChapterForms.Count}.");

        return ChapterForms[chapter - 1];
    }

    /// <summary>「仙丹粉末」基础分加成 TBD。</summary>
    public int ImmortalPowderBaseScore { get; init; } = 10;

    /// <summary>「仙丹粉末」七味各 +N TBD。</summary>
    public int ImmortalPowderFlavorAmount { get; init; } = 2;
}
