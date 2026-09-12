# 七荤八素（SevenSpices）— 项目工作区指令

本文件是**项目层**。跨项目的工作流协议、模型分工、Git 红线、记忆层用法在全局 `~/.dsh/AGENTS.md`，此处不重复。
本文件只写本项目专属的事实与差异；两者冲突时以本文件为准。

## 1. 规则真源（必读）

开始任何开发前必须先完整阅读：

    CLAUDE.md                   ← 开发规则真源（具约束力）
    docs/游戏设计文档.md          ← 游戏规则真源
    docs/程序架构设计文档.md       ← 架构真源

### 术语映射

`CLAUDE.md` 是按外部编码 Agent 的口径写的，本工作区一律按下表理解：

| CLAUDE.md 的写法 | 本工作区含义 |
|---|---|
| Claude / Claude Code | 本会话的 DSH agent 与 subagent |
| §25.1「Claude 可以执行 git status/diff/log/branch」 | agent 可直接执行这些只读命令 |
| §25.2「Claude 不得擅自 Push」 | 不得 `git push`，除非用户明确要求 |

其余条款（四层架构、Core 不依赖 Godot UI 与场景节点、数据驱动、禁止硬编码、修改前先读文档并检查现有实现、§28 破坏性 Git 操作禁令）原样生效。

## 2. 真实命令

本项目**不是** xunit / NUnit 工程，测试是自建控制台 runner。不要用 `dotnet test`。

| 目的 | 命令 |
|---|---|
| 跑全部测试 | `dotnet run --project Tests.Runner` |
| 构建游戏工程 | `dotnet build SevenSpices.csproj` |

- `Tests.Runner/Tests.Runner.csproj` 直接 `Include` 了 `..\Tests\**\*.cs` 与 `..\Core\**\*.cs`，因此核心逻辑可脱离 Godot 运行。
- 根 `SevenSpices.csproj` 使用 `Godot.NET.Sdk/4.7.2`，并 `Compile Remove` 了 `Tests\**` 与 `Tests.Runner\**`。

## 3. 本项目的 workflow 约定

沿用全局三段式（`plan` → `execute` → `review`），本项目补充：

- `execute` 阶段的验收标准**必须**包含：`dotnet run --project Tests.Runner` 通过。
- 涉及 `Core/` 的改动，`review` 阶段必须核对 `CLAUDE.md` §4 分层约束（Core 不得依赖 Godot UI 与场景节点）。
- **游戏规则类改动永远不在工作流里自动实施** —— `plan` 阶段只输出方案，等用户确认后再单独执行。
- 实现与 `docs/` 冲突时只报告，不擅自修改规则（`CLAUDE.md` §2）。

## 4. 修改边界

- 新增内容（食材 / 食客 / 道具 / 伙伴）走数据驱动：新增 Definition + Effect 配置，**不得为单个内容修改核心系统**；确需修改核心系统时必须先说明原因。
- 遵守 `CLAUDE.md` §24：改前先查代码、先读文档、判断是否已有类似系统、尽量复用已有实现。
