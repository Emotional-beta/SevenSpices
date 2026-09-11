# 七荤八素（SevenSpices）开发规则

## 1. 项目基本信息

* 项目名称：七荤八素（SevenSpices）
* 游戏类型：2D 独立游戏
* 引擎：Godot 4
* Godot 版本：4.7.2 .NET / Mono
* 编程语言：C#
* 操作系统：Windows 10
* 主要开发工具：Claude Code
* 版本管理：Git + GitHub
* 远程仓库：GitHub 私有仓库
* 默认分支：main

项目根目录：

```text
D:\Games\SevenSpices
```

---

# 2. 重要文档

开始进行任何涉及游戏规则、核心系统或架构的开发前，必须优先阅读：

```text
docs/游戏设计文档.md
docs/程序架构设计文档.md
```

如果文档与当前代码实现存在冲突：

1. 不要自行猜测；
2. 不要擅自修改游戏规则；
3. 明确指出冲突；
4. 优先向用户确认。

如果只是代码实现与架构文档不一致，应优先保持架构设计，并说明需要修改的范围。

---

# 3. 总体开发原则

## 3.1 核心目标

本项目的核心目标是：

> 核心游戏逻辑与 Godot 表现层彻底分离。

游戏必须能够在没有 UI 的情况下运行核心模拟逻辑。

核心系统应尽量做到：

```text
输入
 ↓
GameState / Domain
 ↓
规则计算
 ↓
状态变化
 ↓
事件
 ↓
Presentation
```

而不是：

```text
UI
 ↓
修改游戏逻辑
 ↓
修改其他 UI
 ↓
修改场景
```

---

# 4. 分层架构

项目采用以下四层结构：

```text
Presentation
    ↓
Game Layer
    ↓
Domain Layer
    ↓
Data Layer
```

目录：

```text
Core/
Data/
Scenes/
UI/
Audio/
Art/
VFX/
Tests/
```

## 4.1 Domain / Core

核心游戏规则所在位置。

不得直接依赖：

* Control
* Node2D
* AnimationPlayer
* Label
* Button
* Godot UI
* 场景节点
* 具体表现逻辑

核心逻辑应该可以脱离 UI 进行测试。

---

# 5. GameState

`GameState` 是整个游戏运行状态的根。

它负责保存状态，不负责承担复杂的游戏流程逻辑。

主要状态包括：

```text
GameState
├── PlayerState
├── RunState
├── PotState
├── BottomState
└── CustomerState
```

重要状态必须集中管理。

不要把核心游戏状态隐藏在 UI Node、Scene 或临时变量中。

---

# 6. 食材系统

食材必须严格区分：

```text
IngredientDefinition
IngredientInstance
```

## IngredientDefinition

表示一种食材的静态定义。

例如：

```text
ID
Name
Rarity
BaseScore
Flavor
Effects
```

## IngredientInstance

表示实际存在于食材池中的某一份食材。

每一个实例必须拥有独立 ID。

例如：

```text
IngredientInstance
├── InstanceID
└── DefinitionID
```

以下设计禁止：

```text
"rice"
"rice"
"rice"
```

直接作为三个无法区分的对象。

必须能够区分：

```text
rice_001
rice_002
rice_003
```

---

# 7. 食材池

`IngredientPool` 负责：

* 当前可抽取食材实例
* 随机抽取
* 无放回抽取
* 最多抽取 3 个
* 不足 3 个时全部抽出
* 玩家选择的实例移出食材池
* 未选择的实例返回食材池

基本流程：

```text
IngredientPool
    ↓
Draw(3)
    ↓
Candidate[]
    ↓
Player Choose
    ├── Selected → Remove
    └── Others → Return
```

---

# 8. 效果系统

效果系统是项目最重要的核心系统之一。

核心对象：

```text
Effect
EffectContext
EffectSystem
```

食材、道具、伙伴等内容不要直接修改整个游戏系统。

推荐流程：

```text
Ingredient
    ↓
产生 Effect
    ↓
EffectSystem
    ↓
修改 GameState / PotState
```

不要让具体食材代码承担大量全局逻辑。

---

# 9. EffectContext 与效果链

每次完整效果链必须拥有独立的：

```text
EffectContext
ChainID
```

`EffectContext` 至少应该能够追踪：

```text
ChainID
CurrentBowl
CurrentIngredient
TriggeredSources
TriggeredEffects
GameState
PotState
```

同一条 Effect Chain 中：

> 同一个触发源不得无限重复触发。

例如：

```text
A
 ↓
B
 ↓
C
 ↓
A
```

如果 A 已经在当前 ChainID 中触发，则禁止无限重新触发 A。

下一次加入食材时必须创建新的 EffectContext / ChainID。

---

# 10. 锅系统

核心对象：

```text
PotState
PotController
```

一锅的核心流程：

```text
StartPot
    ↓
StartBowl
    ↓
DrawIngredients
    ↓
PlayerChoose
    ↓
UseItems
    ↓
AddIngredient
    ↓
ResolveEffects
    ↓
CalculateScore
    ↓
ApplyMultiplier
    ↓
LockScore
    ↓
ServeCustomer
    ↓
Reward
    ↓
NextBowl
```

普通锅和最终锅应该尽量复用同一套核心逻辑。

不要为了最终锅重新复制一套 Pot 系统。

---

# 11. 碗状态机

每一碗必须拥有明确阶段。

推荐：

```text
Start
↓
Customer
↓
ItemPhase
↓
IngredientSelection
↓
IngredientResolve
↓
ScoreCalculation
↓
ScoreLocked
↓
Serving
↓
Reward
↓
End
```

不同阶段只能执行允许的操作。

例如：

```text
ScoreLocked
```

之后不得再次加入食材。

---

# 12. 分数系统

分数计算必须集中在：

```text
ScoreCalculator
```

不要把最终分数计算散落到食材代码中。

推荐流程：

```text
基础状态
 ↓
味道
 ↓
道具
 ↓
食材效果
 ↓
其他规则
 ↓
最终基础分
 ↓
倍率
 ↓
FinalScore
 ↓
Lock
```

分数只有完成全部效果结算后才能锁定。

---

# 13. 倍率系统

倍率属于独立规则系统。

不要在代码中大量出现：

```csharp
if (bowl == 6)
```

之类的硬编码。

默认倍率：

```text
1  → ×1
2  → ×1
3  → ×1
4  → ×1
5  → ×1
6  → ×2
7  → ×4
8  → ×8
9  → ×16
10 → ×32
```

倍率必须在最终基础分计算完成后应用。

---

# 14. 锅底系统

核心对象：

```text
BottomState
BottomExtractor
```

锅底本质上是：

> 状态，而不是一个结算时临时添加的 Bonus。

新锅开始时：

```text
BottomState
    ↓
ApplyToPot
    ↓
PotState
```

例如：

```text
BottomState:
Sweet = 6
```

新锅开始：

```text
PotState.Flavor.Sweet = 6
```

之后它与本锅产生的甜味没有区别。

---

# 15. 锅底衰减规则

锅结束后：

```text
CurrentPotFinalState
    ↓
BottomExtractor
    ↓
30%
    ↓
Floor
    ↓
Minimum 1
    ↓
BottomState
```

锅底进入下一锅后：

> 不允许再次因为进入下一锅而自动进行 30% 衰减。

锅底只能按照游戏设计规定的时机进行衰减。

---

# 16. 食客系统

拆分为：

```text
CustomerDefinition
CustomerInstance
CustomerSystem
```

满意条件应该数据驱动。

例如：

```text
Score >= X
OR
Flavor >= Y
```

满足条件：

```text
Satisfied = true
```

然后执行对应奖励：

```text
DropIngredients()
DropItems()
AddToCompanionCandidates()
```

---

# 17. 伙伴系统

伙伴采用：

```text
CompanionDefinition
CompanionInstance
CompanionSystem
```

伙伴不要直接把大量规则硬编码进：

```text
PotController
```

优先通过：

```text
Effect
Modifier
Event
Rule
```

参与游戏。

伙伴应该能够：

> 改变规则，而不仅仅是增加一个数字。

---

# 18. 事件系统

项目使用轻量事件系统。

主要事件包括：

```text
PotStarted
BowlStarted
IngredientDrawn
IngredientAdded
EffectTriggered
ScoreCalculated
ScoreLocked
CustomerServed
CustomerSatisfied
PotEnded
CompanionAdded
```

系统之间优先通过事件进行松耦合通信。

例如：

```text
UI
监听
ScoreCalculated
```

而不是：

```text
ScoreCalculator
直接寻找 UI Node
```

---

# 19. UI 原则

UI 只负责：

> 显示状态 + 接收玩家输入。

例如：

```text
IngredientCard
```

负责显示：

```text
名称
稀有度
分数
味道
效果
```

玩家点击后：

```text
GameController.SelectIngredient(instanceId)
```

UI 不应该自己决定游戏规则。

---

# 20. 数据驱动

食材、食客、道具、伙伴等内容应尽量数据驱动。

新增一个普通内容时，理想情况应该是：

```text
新增 Definition
+
新增 Effect 配置
```

而不是：

```text
修改 PotController
+
修改 ScoreCalculator
+
增加大量 if/else
```

禁止为了增加单个食材而修改核心系统。

如果确实需要修改核心系统，必须说明原因。

---

# 21. Godot 使用原则

Godot Scene / Node 主要负责：

* 表现
* 输入
* UI
* 动画
* 音效
* VFX
* 场景组织

核心游戏状态不得依赖 Scene 是否存在。

不要把重要规则藏在：

```text
_ready()
_process()
_button_pressed()
```

等 UI 生命周期代码中。

---

# 22. 测试

核心规则必须有自动化测试。

测试目录：

```text
Tests/
├── Effects/
├── Pot/
├── Ingredients/
├── Scoring/
├── Customers/
├── Bottom/
└── Run/
```

至少覆盖：

```text
3选1
无放回抽取
食材实例唯一性
效果链防循环
EffectContext
倍率
锅底30%
锅底最低保留1
锅底不会重复衰减
稀有食客 OR 满意条件
最终锅无限添加
分数锁定
```

修改核心规则后必须检查相关测试。

---

# 23. 开发阶段

不要一次性实现整个游戏。

按照以下阶段开发：

## Phase 1：核心模拟器

优先实现：

```text
GameState
Pot
Ingredient
IngredientPool
Effect
EffectContext
Score
Bottom
Customer
```

目标：

> 没有 UI 也可以完整模拟一锅。

---

## Phase 2：数据驱动

加入：

```text
IngredientDefinition
CustomerDefinition
ItemDefinition
CompanionDefinition
```

目标：

> 增加内容不需要修改核心系统。

---

## Phase 3：Godot UI

加入：

```text
锅
食材卡
食客
分数
金币
按钮
效果显示
```

---

## Phase 4：完整流程

实现：

```text
开始游戏
↓
章节
↓
普通锅
↓
Boss
↓
最终锅
↓
结算
```

---

## Phase 5：表现

最后加入：

```text
动画
粒子
音效
屏幕反馈
食材入锅
喝粥
爆发
饕餮
```

表现层不能反过来污染核心规则。

---

# 24. 修改代码前的要求

进行较大修改前：

1. 先检查相关代码。
2. 先阅读相关设计文档。
3. 判断当前实现是否已经存在类似系统。
4. 尽量复用已有系统。
5. 不要为了一个简单需求创建大量新抽象。
6. 不要未经确认修改游戏设计规则。
7. 如果发现架构冲突，先告诉用户。

禁止：

> 为了"以后可能用到"而提前实现大量没有需求的复杂系统。

优先：

> 简单、清晰、可测试、符合当前需求。

---

# 25. Git 使用规则

本项目使用 Git 进行版本管理。

GitHub 仓库为私有仓库。

默认分支：

```text
main
```

## 25.1 Claude 可以执行

Claude 可以：

```text
git status
git diff
git log
git branch
git diff --check
```

Claude 可以在用户明确要求提交时创建 Commit。

## 25.2 Claude 不得擅自 Push

除非用户明确要求：

```text
Push
推送
上传到 GitHub
```

否则不得执行：

```text
git push
```

最终 Push 优先由用户通过 GitHub Desktop 完成。

---

# 26. Git Commit 规范

每一个重要开发阶段都应该形成清晰的 Commit。

Commit 必须使用简体中文。

推荐格式：

```text
类型：模块：简短说明
```

例如：

```text
feat：核心：实现食材池三选一抽取
```

```text
feat：效果：实现 EffectContext 效果链
```

```text
feat：锅：实现十碗流程状态机
```

```text
fix：锅底：修复锅底重复衰减问题
```

```text
test：效果：增加效果链循环测试
```

```text
refactor：分数：整理 ScoreCalculator
```

---

# 27. Commit 必须详细

提交前必须检查：

```text
git status
git diff
```

确认没有无关文件被修改。

Commit message 应该让未来的开发者可以快速理解：

1. 修改了什么；
2. 为什么修改；
3. 影响了什么；
4. 是否新增测试。

推荐 Commit body：

```text
feat：效果：实现 EffectContext 效果链

修改：
- 新增 EffectContext
- 新增 ChainID
- 增加触发源记录
- 增加效果链循环保护

原因：
统一管理单次食材加入产生的效果链。

测试：
- 新增效果链循环测试
- 验证同一 ChainID 下不会无限触发

影响：
为后续食材、道具、伙伴效果提供统一基础。
```

---

# 28. Git 回退安全规则

任何破坏性 Git 操作之前必须向用户确认。

禁止未经确认执行：

```text
git reset --hard
git clean
git checkout -- .
git restore .
git push --force
```

尤其禁止：

```text
git push --force
```

如果发现代码出现严重问题，优先：

```text
git status
git log
git diff
```

然后告诉用户当前状态和推荐回退点。

---

# 29. 每次任务完成后的报告

完成一个开发任务后，用简体中文简要报告：

```text
本次完成：
- xxx
- xxx

修改文件：
- xxx
- xxx

测试：
- xxx

Git：
- 当前分支：main
- 工作区：干净 / 有修改
- 是否创建 Commit：是 / 否
- 是否 Push：是 / 否

注意：
- xxx
```

如果存在未解决问题，必须明确列出。

---

# 30. 最重要的原则

始终遵守：

> 不擅自改变游戏规则。

> 不让 UI 控制核心游戏逻辑。

> 不让具体食材污染核心系统。

> 不用大量 if/else 硬编码内容。

> 不为了未来可能的需求制造不必要的复杂架构。

> 核心逻辑必须可以测试。

> 修改前先理解现有代码。

> 修改后检查测试。

> Git 提交必须留下清晰、详细的中文记录。

> 未经用户明确要求，不执行 git push。

> 如果设计文档与用户当前要求冲突，以用户明确确认的最新规则为准。
