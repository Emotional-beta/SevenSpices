namespace SevenSpices.Core.Game;

/// <summary>
/// 章末 Boss（饕餮）一次试吃的不可变验收记录。
/// 只保存结果数据，不执行判定逻辑；按触发顺序存放于 <see cref="RunState.ChapterBossRecords"/>。
/// </summary>
public class ChapterBossRecord
{
    /// <summary>发生章节（1-based）。</summary>
    public int Chapter { get; }

    /// <summary>发生锅号（1-based）。</summary>
    public int PotIndex { get; }

    /// <summary>饕餮形态 Id（如 taotie_child）。</summary>
    public string BossId { get; }

    /// <summary>饕餮称谓语。</summary>
    public string BossName { get; }

    /// <summary>是否满意。</summary>
    public bool Satisfied { get; }

    /// <summary>试吃时的本锅累计最终分（含倍率）。</summary>
    public int PotTotalFinalScore { get; }

    /// <summary>该形态的满意阈值。</summary>
    public int Threshold { get; }

    public ChapterBossRecord(
        int chapter,
        int potIndex,
        string bossId,
        string bossName,
        bool satisfied,
        int potTotalFinalScore,
        int threshold)
    {
        if (string.IsNullOrWhiteSpace(bossId))
            throw new ArgumentException("ChapterBossRecord BossId cannot be empty.", nameof(bossId));
        if (string.IsNullOrWhiteSpace(bossName))
            throw new ArgumentException("ChapterBossRecord BossName cannot be empty.", nameof(bossName));

        Chapter = chapter;
        PotIndex = potIndex;
        BossId = bossId;
        BossName = bossName;
        Satisfied = satisfied;
        PotTotalFinalScore = potTotalFinalScore;
        Threshold = threshold;
    }
}
