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

    private readonly List<Checkpoint> _pending = new();
    private readonly List<string> _captured = new();
    private readonly List<string> _unreached = new();

    /// <summary>在 <c>AddChild</c> 之前注入宿主，保证 <c>_Ready</c> 时已有可用引用。</summary>
    public void Bind(Main main) => _main = main;

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
        bool captured;
        try
        {
            var image = GetViewport().GetTexture().GetImage();
            if (image == null)
            {
                captured = false;
                captureNote = "GetViewport().GetTexture().GetImage() 返回 null";
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
            }
        }
        catch (System.Exception ex)
        {
            captured = false;
            captureNote = $"截图异常：{ex.Message}";
        }

        var (verifyOk, detail) = checkpoint.Verify(gc);
        bool ok = captured && verifyOk;
        if (!ok)
            _anyFail = true;

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

    private async Task WaitFrames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
