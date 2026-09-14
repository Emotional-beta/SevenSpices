using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using SevenSpices.Core.Game;

namespace SevenSpices;

/// <summary>
/// M6 阶段取证驱动器：仅在显式传入 <c>--ui-tour</c> 用户参数时由 <see cref="Main"/> 挂载。
/// <para>
/// 目的：把「碗末池空 / 锅末推进、锅末（奖励 / 伙伴 / 商店 / 路线）、最终锅、终局结算」这些无法用
/// <c>--write-movie</c> 录开局自动跑帧覆盖的界面状态，变成可复现、可回归的截图证据。
/// </para>
/// <para>
/// 约束：只通过 <see cref="GameController"/> 的公开接口推进（与 UI 按钮回调同一套），
/// 不直接改 <c>GameState</c>、不绕过门控、不触碰 <c>Core/</c>；只读地观察状态与 UI 区块做断言。
/// 不带 <c>--ui-tour</c> 时本类完全不会被创建，默认启动路径零副作用。
/// </para>
/// <para>
/// 退出契约：任何异常都会落成非零退出码（见 <see cref="RunTourGuardedAsync"/>），
/// 目录准备失败 / 断言失败 / 检查点未到达 / 动作上限耗尽同样为非零，绝不静默「成功」。
/// </para>
/// </summary>
public partial class UiSnapshotTour : Node
{
    private const string OutputDir = "res://generated-images/ui-m6";

    /// <summary>
    /// M7 像素回归基线目录。刻意放在 <c>Tests/</c>（版本库内、非 generated-images）下，
    /// 且以 <c>.bin</c> 存盘而非 PNG：Godot 只为图片扩展名生成 <c>.import</c>，
    /// 用 .bin 可避免把导入噪音一并纳入版本库。（格式见下方说明。）
    /// </summary>
    private const string BaselineDir = "res://Tests/Baselines/UiTour";

    /// <summary>
    /// 基线文件格式（<c>.bin</c>，自定义、不依赖 Godot 导入）：
    /// GZip 压缩流，内含 8 字节魔数 + int32 宽 + int32 高（小端）+ Rgba8 原始像素。
    /// 用压缩而非裸像素，是因为像素画大片纯色经 GZip 后仅数十 KB，
    /// 避免把每个约 3.6MB 的裸基线纳入版本库。
    /// </summary>
    private static readonly byte[] BaselineMagic = System.Text.Encoding.ASCII.GetBytes("SSTOURB1");

    /// <summary>
    /// 差异像素占比容差：取 0，即要求逐字节完全一致。
    /// 理由：tour 有固定随机种子且同一 Godot 版本、同一渲染后端，画面应当完全确定；
    /// 任何像素差异都可能是真实回归，不应用容差掩盖。若将来跨机器出现平台字体渲染差异，再议放宽。
    /// </summary>
    private const double PixelToleranceRatio = 0.0;

    /// <summary>截图前的稳定帧数：等事件驱动置脏的 RefreshUI 与布局各跑一轮以上。</summary>
    private const int SettleFrames = 3;

    /// <summary>每个动作后的推进帧数，让下一帧的 RefreshUI 与自动定位落地。</summary>
    private const int ActionFrames = 2;

    /// <summary>推进动作上限，兜底防死循环（正常一轮约 200 步内完成）。</summary>
    private const int ActionGuard = 5000;

    private Main _main = null!;
    private string _outputPath = string.Empty;
    private bool _anyFail;
    private bool _stuck;
    private bool _guardExhausted;
    private bool _finalPotReached;
    private string _finalPotNote = "未到达";

    // M7：基线模式。_updateBaseline=true 时只写基线、不比较；否则与基线比较并报告差异。
    private bool _updateBaseline;
    private int _baselineCompared;
    private int _baselineFailed;
    private int _baselineWritten;

    private readonly List<Checkpoint> _pending = new();
    private readonly List<string> _captured = new();
    private readonly List<string> _unreached = new();
    private readonly List<string> _baselineReport = new();

    /// <summary>在 <c>AddChild</c> 之前注入宿主，保证 <c>_Ready</c> 时已有可用引用。</summary>
    public void Bind(Main main, bool updateBaseline)
    {
        _main = main;
        _updateBaseline = updateBaseline;
    }

    public override void _Ready()
    {
        if (_main == null)
        {
            GD.PrintErr("[Tour] FAIL：未绑定 Main，无法运行。");
            GetTree().Quit(1);
            return;
        }

        _ = RunTourGuardedAsync();
    }

    /// <summary>
    /// 异常兜底：主体抛任何异常都不得让进程挂死，必须打印异常并强制非零退出。
    /// </summary>
    private async Task RunTourGuardedAsync()
    {
        bool ok = false;
        try
        {
            ok = await RunTourAsync();
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"[Tour] FAIL：驱动器抛出未处理异常，强制失败退出：\n{ex}");
        }
        finally
        {
            GetTree().Quit(ok ? 0 : 1);
        }
    }

    // ── 检查点定义 ────────────────────────────────────────────────────────────

    private sealed class Checkpoint
    {
        /// <summary>固定序号（对应任务约定的检查点顺序），与到达顺序无关，保证文件名稳定可覆盖。</summary>
        public required int Index { get; init; }

        public required string Name { get; init; }

        /// <summary>是否已进入该阶段（用于决定何时截图）。</summary>
        public required Func<GameController, bool> Reached { get; init; }

        /// <summary>轻量断言：返回 (是否通过, 实测详情)。</summary>
        public required Func<GameController, (bool Ok, string Detail)> Verify { get; init; }
    }

    private void BuildCheckpoints()
    {
        _pending.Add(new Checkpoint
        {
            Index = 1,
            Name = "bowl-ingredient",
            Reached = gc => gc.CanSelectIngredient,
            Verify = gc =>
            {
                int candidates = _main.CandidateRow.GetChildCount();
                // 有候选时「倒水」必须处于禁用态：这是真实门控信号（Visible 恒为 true，不可用）。
                bool skipDisabled = _main.SkipBowlButton.Disabled;
                bool ok = gc.CanSelectIngredient && candidates > 0 && skipDisabled;
                return (ok,
                    $"CanSelectIngredient={gc.CanSelectIngredient}，候选按钮数={candidates}，倒水禁用={skipDisabled}");
            },
        });

        _pending.Add(new Checkpoint
        {
            Index = 2,
            Name = "bowl-pool-empty",
            Reached = gc => gc.CanSkipBowl,
            Verify = gc =>
            {
                // 池空、处于可点「倒水」的真实推进态：必须断言按钮确实可用（非 Visible）。
                bool skipEnabled = !_main.SkipBowlButton.Disabled;
                bool ok = gc.CanSkipBowl && skipEnabled;
                return (ok,
                    $"CanSkipBowl={gc.CanSkipBowl}，倒水可用={skipEnabled}");
            },
        });

        _pending.Add(new Checkpoint
        {
            Index = 3,
            Name = "pot-advance",
            Reached = gc => gc.CanAdvanceToNextPot,
            Verify = gc =>
            {
                // 锅末、奖励 / 伙伴 / 商店 / 路线均已处理，「进入下一锅」真实可用。
                bool nextEnabled = !_main.NextPotButton.Disabled;
                bool ok = gc.CanAdvanceToNextPot && nextEnabled;
                return (ok,
                    $"CanAdvanceToNextPot={gc.CanAdvanceToNextPot}，下一锅可用={nextEnabled}");
            },
        });

        _pending.Add(new Checkpoint
        {
            Index = 4,
            Name = "pot-end-reward",
            Reached = gc => gc.IsAwaitingReward,
            Verify = gc =>
            {
                int buttons = _main.RewardRow.GetChildCount();
                bool visible = _main.RewardSection.Visible;
                bool ok = gc.IsAwaitingReward && visible && buttons > 0;
                return (ok,
                    $"IsAwaitingReward={gc.IsAwaitingReward}，奖励区块可见={visible}，候选按钮数={buttons}");
            },
        });

        _pending.Add(new Checkpoint
        {
            Index = 5,
            Name = "pot-end-companion",
            Reached = gc => gc.IsAwaitingCompanionChoice,
            Verify = gc =>
            {
                int columns = _main.CompanionCandidateRow.GetChildCount();
                bool visible = _main.CompanionSection.Visible;
                bool ok = gc.IsAwaitingCompanionChoice && visible && columns > 0;
                return (ok,
                    $"IsAwaitingCompanionChoice={gc.IsAwaitingCompanionChoice}，伙伴区块可见={visible}，候选数={columns}");
            },
        });

        _pending.Add(new Checkpoint
        {
            Index = 6,
            Name = "pot-end-shop",
            Reached = gc => gc.IsShopOpen,
            Verify = gc =>
            {
                int offers = _main.ShopRow.GetChildCount();
                bool visible = _main.ShopSection.Visible;
                bool ok = gc.IsShopOpen && visible && offers > 0;
                return (ok,
                    $"IsShopOpen={gc.IsShopOpen}，商店区块可见={visible}，报价数={offers}");
            },
        });

        _pending.Add(new Checkpoint
        {
            Index = 7,
            Name = "pot-end-route",
            Reached = gc => gc.IsAwaitingRouteChoice,
            Verify = gc =>
            {
                int offers = _main.RouteRow.GetChildCount();
                bool visible = _main.RouteSection.Visible;
                bool ok = gc.IsAwaitingRouteChoice && visible && offers > 0;
                return (ok,
                    $"IsAwaitingRouteChoice={gc.IsAwaitingRouteChoice}，路线区块可见={visible}，候选数={offers}");
            },
        });

        _pending.Add(new Checkpoint
        {
            Index = 8,
            Name = "final-pot",
            Reached = gc => gc.IsFinalPot,
            Verify = gc =>
            {
                bool labelOk = _main.PotLabel.Text == "最终锅";
                bool restartHidden = !_main.RestartButton.Visible;
                bool ok = gc.IsFinalPot && labelOk && restartHidden;
                return (ok,
                    $"IsFinalPot={gc.IsFinalPot}，锅标题='{_main.PotLabel.Text}'，重开按钮可见={!restartHidden}");
            },
        });

        _pending.Add(new Checkpoint
        {
            Index = 9,
            Name = "run-end",
            Reached = gc => gc.IsRunComplete,
            Verify = gc =>
            {
                bool restartVisible = _main.RestartButton.Visible;
                bool endVisible = _main.RunEndLabel.Visible;
                bool ok = gc.IsRunComplete && restartVisible && endVisible;
                return (ok,
                    $"IsRunComplete={gc.IsRunComplete}，重开按钮可见={restartVisible}，结算文案可见={endVisible}，"
                    + $"failed={gc.Run.IsFailed}，outcome={gc.Run.Outcome}");
            },
        });
    }

    // ── 主流程 ────────────────────────────────────────────────────────────────

    /// <returns>是否全部检查点到达且断言通过。</returns>
    private async Task<bool> RunTourAsync()
    {
        if (!TryPrepareOutputDir(out _outputPath, out string dirError))
        {
            GD.PrintErr($"[Tour] FAIL：无法准备输出目录，已中止（不沿用旧证据）：{dirError}");
            return false;
        }

        BuildCheckpoints();

        var gc = _main.Controller;
        GD.Print($"[Tour] 阶段取证开始（输出目录：{_outputPath}）");

        int guard = 0;
        while (!gc.IsRunComplete && !_stuck && guard < ActionGuard)
        {
            await CaptureMatchingCheckpointsAsync(gc);
            Act(gc);
            await WaitFrames(ActionFrames);
            guard++;
        }

        _guardExhausted = guard >= ActionGuard && !gc.IsRunComplete;
        if (_guardExhausted)
            GD.PrintErr($"[Tour] FAIL：推进动作达到上限 {ActionGuard} 仍未结束本局"
                + $"（run={gc.Run.Chapter}/{gc.Run.PotIndex}，final={gc.Run.IsFinalPot}，"
                + $"pot={gc.Pot.Phase}，bowl={gc.Pot.CurrentBowlPhase}）。");

        if (_stuck)
            GD.PrintErr($"[Tour] FAIL：驱动器卡住，无法继续推进（run={gc.Run.Chapter}/{gc.Run.PotIndex}，"
                + $"final={gc.Run.IsFinalPot}，pot={gc.Pot.Phase}，bowl={gc.Pot.CurrentBowlPhase}）。");

        // 终局检查点的到达往往就是循环退出条件，循环退出前不会再检查，这里补一次。
        await CaptureMatchingCheckpointsAsync(gc);

        foreach (var cp in _pending)
            _unreached.Add(cp.Name);

        PrintSummary(gc);

        bool allOk = !_anyFail && !_stuck && !_guardExhausted && _unreached.Count == 0;
        GD.Print($"[Tour] 总判定：{(allOk ? "PASS" : "FAIL")}（exit {(allOk ? 0 : 1)}）");
        return allOk;
    }

    /// <summary>对当前仍匹配的检查点依次截图 + 断言，并从待办中移除。</summary>
    private async Task CaptureMatchingCheckpointsAsync(GameController gc)
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            var checkpoint = _pending[i];
            if (!checkpoint.Reached(gc))
                continue;

            _pending.RemoveAt(i);
            await CaptureAsync(checkpoint, gc);
        }
    }

    private async Task CaptureAsync(Checkpoint checkpoint, GameController gc)
    {
        // 事件驱动置脏 → 至少 1~2 帧后 UI 才刷新稳定；这里留 3 帧余量。
        await WaitFrames(SettleFrames);

        string fileName = $"tour-{checkpoint.Index:00}-{checkpoint.Name}.png";
        string fullPath = _outputPath.PathJoin(fileName);

        string captureNote;
        string baselineNote;
        bool captured;
        bool baselineOk = true;
        try
        {
            var image = GetViewport().GetTexture().GetImage();
            if (image == null)
            {
                captured = false;
                captureNote = "GetViewport().GetTexture().GetImage() 返回 null";
                baselineNote = "未比较（无截图）";
                baselineOk = false;
            }
            else
            {
                if (image.GetFormat() != Image.Format.Rgba8)
                    image.Convert(Image.Format.Rgba8);

                Error saveErr = image.SavePng(fullPath);
                captured = saveErr == Error.Ok;
                captureNote = captured
                    ? $"{image.GetWidth()}x{image.GetHeight()} -> {fullPath}"
                    : $"SavePng 失败，错误={saveErr}（{fullPath}）";

                baselineNote = HandleBaseline(image, fileName, out baselineOk);
            }
        }
        catch (System.Exception ex)
        {
            captured = false;
            captureNote = $"截图异常：{ex.Message}";
            baselineNote = "未比较（截图异常）";
            baselineOk = false;
        }

        var (verifyOk, detail) = checkpoint.Verify(gc);
        bool ok = captured && verifyOk && baselineOk;
        if (!ok)
            _anyFail = true;

        _baselineReport.Add($"{fileName}  [{(_updateBaseline ? "写基线" : "比较")}]  {baselineNote}");

        if (checkpoint.Name == "final-pot")
        {
            _finalPotReached = true;
            _finalPotNote = ok ? "已到达且断言通过" : "已到达但断言失败";
        }

        _captured.Add($"{fileName}  [{(ok ? "PASS" : "FAIL")}]  {detail}");

        if (ok)
            GD.Print($"[Tour] 检查点 {checkpoint.Name} PASS：截图 {captureNote}；实测 {detail}");
        else
            GD.PrintErr($"[Tour] 检查点 {checkpoint.Name} FAIL：截图 {captureNote}；实测 {detail}");
    }

    /// <summary>
    /// 用 GameController 的公开接口推进一步（与 UI 按钮回调同一套，不绕过门控）。
    /// 贪心策略：奖励选基础分最高的候选；选食材按 PreviewIngredient 的预计最终分最高者；
    /// 伙伴选第一位；商店 / 路线按「跳过」推进（跳过路线＝自动领保底）。
    /// </summary>
    private void Act(GameController gc)
    {
        if (gc.IsAwaitingReward)
        {
            var best = gc.RewardCandidates[0];
            foreach (var candidate in gc.RewardCandidates)
            {
                if (candidate.Definition.BaseScore > best.Definition.BaseScore)
                    best = candidate;
            }
            gc.ChooseReward(best.InstanceId);
            return;
        }

        if (gc.IsAwaitingCompanionChoice)
        {
            gc.ChooseCompanion(gc.CompanionCandidates[0].Id);
            return;
        }

        if (gc.IsShopOpen)
        {
            gc.SkipShop();
            return;
        }

        if (gc.IsAwaitingRouteChoice)
        {
            gc.SkipRoute();
            return;
        }

        if (gc.CanSelectIngredient)
        {
            var best = gc.CurrentCandidates[0];
            int bestScore = int.MinValue;
            foreach (var candidate in gc.CurrentCandidates)
            {
                int score = gc.PreviewIngredient(candidate).PreviewFinalScore;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            gc.SelectIngredient(best.InstanceId);
            return;
        }

        if (gc.CanSkipBowl)
        {
            gc.SkipBowl();
            return;
        }

        if (gc.CanAdvanceToNextPot)
        {
            gc.AdvanceToNextPot();
            return;
        }

        if (gc.CanEndCooking)
        {
            gc.EndCooking();
            return;
        }

        _stuck = true;
    }

    // ── 输出与收尾 ────────────────────────────────────────────────────────────

    private void PrintSummary(GameController gc)
    {
        GD.Print("[Tour] ===== 汇总 =====");
        GD.Print($"[Tour] 检查点到达：{_captured.Count}/{_captured.Count + _unreached.Count}");
        foreach (var line in _captured)
            GD.Print($"[Tour]   {line}");
        foreach (var name in _unreached)
            GD.PrintErr($"[Tour]   未到达检查点：{name}");

        GD.Print(_updateBaseline ? "[Tour] ===== 基线（更新模式：只写基线，不比较）=====" : "[Tour] ===== 像素回归基线 =====");
        foreach (var line in _baselineReport)
            GD.Print($"[Tour]   {line}");
        if (_updateBaseline)
            GD.Print($"[Tour] 基线写入：{_baselineWritten} 个（目录 {ProjectSettings.GlobalizePath(BaselineDir)}）");
        else
            GD.Print($"[Tour] 基线比较：{_baselineCompared - _baselineFailed}/{_baselineCompared} 通过，"
                + $"失败 {_baselineFailed} 个，容差 {PixelToleranceRatio * 100:0.00}%");

        GD.Print($"[Tour] 章/锅：{gc.Run.Chapter}/{gc.Run.PotIndex}，最终锅={gc.Run.IsFinalPot}，"
            + $"结局={gc.Run.Outcome}，终止={gc.Run.IsFailed}");
        GD.Print($"[Tour] 最终锅可达性：{(_finalPotReached ? "可达" : "不可达")}（{_finalPotNote}）");
    }

    /// <summary>
    /// 准备输出目录：创建目录 + 清掉上一次取证留下的 tour-*.png。
    /// 任一步失败都返回 false 并给出原因（调用方据此显式失败退出），绝不静默沿用旧图当本次证据。
    /// </summary>
    private static bool TryPrepareOutputDir(out string path, out string error)
    {
        path = ProjectSettings.GlobalizePath(OutputDir);

        Error mkdir = DirAccess.MakeDirRecursiveAbsolute(path);
        if (mkdir != Error.Ok && mkdir != Error.AlreadyExists)
        {
            error = $"MakeDirRecursiveAbsolute 失败，错误={mkdir}（{path}）";
            return false;
        }

        using var dir = DirAccess.Open(path);
        if (dir == null)
        {
            error = $"DirAccess.Open 失败，无法读取输出目录（{path}）";
            return false;
        }

        foreach (string file in dir.GetFiles())
        {
            if (!file.StartsWith("tour-") || !file.EndsWith(".png"))
                continue;

            Error remove = dir.Remove(file);
            if (remove != Error.Ok)
            {
                error = $"删除旧证据 {file} 失败，错误={remove}（{path}）";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }

    // ── M7：像素回归基线 ──────────────────────────────────────────────────────

    /// <summary>
    /// 按模式处理像素基线：更新模式只写基线；比较模式读基线并比对差异像素。
    /// 基线缺失 / 损坏 / 尺寸不符均判 FAIL（绝不静默 PASS），并提示用更新模式重新生成。
    /// </summary>
    private string HandleBaseline(Image image, string fileName, out bool ok)
    {
        string path = System.IO.Path.Combine(
            ProjectSettings.GlobalizePath(BaselineDir), fileName + ".bin");

        if (_updateBaseline)
        {
            if (TryWriteBaseline(image, path, out string writeError))
            {
                _baselineWritten++;
                ok = true;
                return $"已写基线 {path}";
            }

            ok = false;
            return $"写基线失败：{writeError}（{path}）";
        }

        var (compareOk, detail) = CompareBaseline(image, path);
        _baselineCompared++;
        if (!compareOk)
            _baselineFailed++;
        ok = compareOk;
        return detail;
    }

    /// <summary>
    /// 把本次截图的宽高与 Rgba8 像素 GZip 压缩后写入基线文件（.bin，不走 Godot 导入）。
    /// 采用「先写同目录临时文件、成功后再原子替换」的两步写法：任一步失败都保留旧基线，
    /// 不会因磁盘满 / 中断先把旧基线截断。
    /// </summary>
    private static bool TryWriteBaseline(Image image, string path, out string error)
    {
        string tmpPath = path + ".tmp";
        try
        {
            string? dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                System.IO.Directory.CreateDirectory(dir);

            byte[] pixels = image.GetData();
            int expectedBytes = image.GetWidth() * image.GetHeight() * 4;
            if (pixels.Length != expectedBytes)
            {
                error = $"像素字节数 {pixels.Length} != 宽×高×4={expectedBytes}（图像格式非 Rgba8？）";
                return false;
            }

            using (var stream = new System.IO.FileStream(
                tmpPath, System.IO.FileMode.Create, System.IO.FileAccess.Write))
            using (var gzip = new System.IO.Compression.GZipStream(
                stream, System.IO.Compression.CompressionLevel.SmallestSize))
            using (var writer = new System.IO.BinaryWriter(gzip))
            {
                writer.Write(BaselineMagic);
                writer.Write(image.GetWidth());
                writer.Write(image.GetHeight());
                writer.Write(pixels);
            }

            // 临时文件已完整落盘，原子替换目标文件（Windows 下 MoveFileEx 覆盖语义）。
            System.IO.File.Move(tmpPath, path, overwrite: true);
            error = string.Empty;
            return true;
        }
        catch (System.Exception ex)
        {
            TryDeleteTemp(tmpPath);
            error = ex.Message;
            return false;
        }
    }

    /// <summary>尽力清理写入失败的临时文件；清理失败不影响「保留旧基线」这一主目标。</summary>
    private static void TryDeleteTemp(string tmpPath)
    {
        try
        {
            if (System.IO.File.Exists(tmpPath))
                System.IO.File.Delete(tmpPath);
        }
        catch
        {
            // 忽略：临时文件残留无害，旧基线仍在。
        }
    }

    /// <summary>读取基线并与本次截图逐像素比较，返回是否在容差内与差异详情。</summary>
    private static (bool Ok, string Detail) CompareBaseline(Image image, string path)
    {
        if (!System.IO.File.Exists(path))
            return (false, $"无基线（{path}）；请用 --ui-tour-update-baseline 生成");

        try
        {
            byte[] baseline;
            int width;
            int height;
            using (var stream = new System.IO.FileStream(
                path, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            using (var gzip = new System.IO.Compression.GZipStream(
                stream, System.IO.Compression.CompressionMode.Decompress))
            using (var reader = new System.IO.BinaryReader(gzip))
            {
                byte[] magic = reader.ReadBytes(BaselineMagic.Length);
                if (magic.Length != BaselineMagic.Length)
                    return (false, "基线损坏：读取魔数失败");
                for (int i = 0; i < BaselineMagic.Length; i++)
                {
                    if (magic[i] != BaselineMagic[i])
                        return (false, "基线损坏：魔数不符");
                }

                width = reader.ReadInt32();
                height = reader.ReadInt32();
                if (width != image.GetWidth() || height != image.GetHeight())
                {
                    return (false,
                        $"尺寸不符：基线 {width}x{height}，本次 {image.GetWidth()}x{image.GetHeight()}");
                }

                int expected = width * height * 4;
                baseline = reader.ReadBytes(expected);
                if (baseline.Length != expected)
                    return (false, $"基线损坏：像素字节数 {baseline.Length} != 期望 {expected}");
            }

            int totalPixels = width * height;
            byte[] pixels = image.GetData();
            if (pixels.Length != totalPixels * 4)
                return (false, $"本次像素字节数 {pixels.Length} != 期望 {totalPixels * 4}");

            int diffPixels = 0;
            for (int p = 0; p < totalPixels; p++)
            {
                int c = p * 4;
                if (baseline[c] != pixels[c]
                    || baseline[c + 1] != pixels[c + 1]
                    || baseline[c + 2] != pixels[c + 2]
                    || baseline[c + 3] != pixels[c + 3])
                {
                    diffPixels++;
                }
            }

            double ratio = diffPixels / (double)totalPixels;
            bool ok = ratio <= PixelToleranceRatio;
            string detail =
                $"像素差异={diffPixels}/{totalPixels}（{ratio * 100:0.0000}%，容差 {PixelToleranceRatio * 100:0.00}%）";
            return (ok, ok ? detail : detail + " → FAIL");
        }
        catch (System.Exception ex)
        {
            return (false, $"基线读取异常：{ex.Message}");
        }
    }

    private async Task WaitFrames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
