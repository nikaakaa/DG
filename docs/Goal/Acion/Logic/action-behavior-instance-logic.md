# 行为层架构(顶层)

> 本文档是行为层 runtime 架构的**顶层契约**。范围限定:运行时角色、行为契约、扩展性 DoD、最小落地路径。
>
> 不在本文档范围:网络同步、客户端预测/纠错、表现层投影、Luban 表结构、具体行为的状态图。这些在派生子文档里讲。
>
> 本文是 [`old/action-behavior-instance-logic-before-runner-statemachine.md`](old/action-behavior-instance-logic-before-runner-statemachine.md) 的替代版本。改动核心:**所有行为统一走 `IBehaviorRunner` 接口,per-instance 实例化,主流程对具体行为完全无知**。

---

## 1. 顶层结论

```
行为层 = 唯一运行时主语是 ActionBehaviorInstance
       + 唯一行为契约是 IBehaviorRunner
       + 主流程只跟接口对话, 不认识任何具体行为

加新行为 = 新写一个 IBehaviorRunner 类 + 注册一行 factory + Luban 配一行
         主流程白名单文件零修改
         这条契约由一个常绿的 DummyRunner 扩展性测试守护
```

---

## 2. 两层运行时角色

```text
BehaviorEngine          [每 tick 编排器 + 运行中实例管理器, 全局 1 个]
  drain inputs → build candidates → admission → start running →
  step due → apply outputs → emit facts
  持有 Dictionary<InstanceId, IBehaviorRunner> active runners
  持有 RunningBehaviorInstanceStore (claim 索引: bySubject / byCell / byResource)
  Start(instance) / StepDue(serverTick) / Complete(instanceId) / QueryClaims
  可以读取 world snapshot, 但所有 world 修改必须经 CommitProposal
  不解析 RunnerId 之外的具体行为内容, 不出现具体行为名字分支

IBehaviorRunner         [单实例状态机, 每个 ActionBehaviorInstance 一个]
  对单个 instance 实现 Enter / Tick / Exit
  状态字段是 Runner 类私有, 强类型, 不污染外部
  输入: BehaviorRunnerContext (instance + 只读 world + spec + serverTick)
  输出: BehaviorStepOutput (commit proposals / facts / claims / deferred / trace)
```

调用方向**只**自上而下:

```text
BehaviorEngine → IBehaviorRunner
反向调用、跨层调用、Runner 之间直接调用 — 全部禁止
```

`BehaviorRuntime` 和 `BehaviorInstanceRunner` 可以作为代码落地时的内部拆分类存在,但它们不是必须的架构层级。真正不能混在一起的只有两件事:

```text
per-tick orchestration   无状态编排: 处理本 tick 输入、admission、输出应用
cross-tick instances     有状态集合: 持有运行中的 per-instance Runner
```

这两件事可以由一个 `BehaviorEngine` 承担。不要为了贴合当前 WIP 代码形态,把三层拆分写成硬架构。

---

## 3. 行为契约 `IBehaviorRunner`(伪代码)

```csharp
public interface IBehaviorRunner
{
    BehaviorStepOutput Enter(BehaviorRunnerContext ctx);
    BehaviorStepOutput Tick (BehaviorRunnerContext ctx);
    BehaviorStepOutput Exit (BehaviorRunnerContext ctx);

    // 服务端权威回放 / 调试快照
    BehaviorRunnerSnapshot Snapshot();
    void Restore(BehaviorRunnerSnapshot snapshot);
}

public readonly struct BehaviorRunnerContext
{
    public ActionBehaviorInstance Instance { get; }
    public IReadOnlyWorld         World    { get; }   // 只读视图
    public ActionSpec             Spec     { get; }
    public long                   ServerTick { get; }
    // 必要的 policy 视图 (conflict/merge/blocked_result/...)
}
```

**统一接口,状态图复杂度自由**。退化(单 tick)行为可以 `Tick` 返回 `Empty`、`Exit` 不做事;复杂行为(rotate / door / charge)在 `Tick` 内显式实现 transition。

---

## 4. Runner 实例化模型(per-instance)

```csharp
public sealed class BehaviorRunnerRegistry
{
    private readonly Dictionary<RunnerId, Func<IBehaviorRunner>> factories = new();

    public void Register(RunnerId id, Func<IBehaviorRunner> factory)
        => factories[id] = factory;

    public IBehaviorRunner Create(RunnerId id)
        => factories[id]();    // 每个 instance 新分配一个 Runner 对象
}
```

```csharp
// BehaviorRunnerCatalog.cs - 不在主流程白名单, 允许新行为往这里加一行
public static class BehaviorRunnerCatalog
{
    public static BehaviorRunnerRegistry CreateDefault()
    {
        var r = new BehaviorRunnerRegistry();
        r.Register(new RunnerId("step_runner"),         () => new StepRunner());
        r.Register(new RunnerId("rotate_pivot_runner"), () => new RotatePivotRunner());
        r.Register(new RunnerId("spawn_runner"),        () => new SpawnRunner());
        // ↑ 新行为加一行, 完
        return r;
    }
}
```

**为什么 per-instance 而不是 singleton + 状态外置?**

- 状态字段强类型,在 Runner 类里以普通字段形式存在,IDE / 重构 / 编译期检查全部生效
- 单元测试可以直接 `new RotatePivotRunner()` 给 ctx 跑 Enter/Tick,不需要构造弱类型 StateData 或外部状态池
- Snapshot/Restore 由 Runner 自己负责自己的字段,不需要在外层维护一个"所有可能状态"的 union 类型
- 字段改名、阶段拆分、状态重构都走 IDE refactor,不靠 string key + grep
- 同时 Running 的 instance 量级 ~10–50,allocation 成本可忽略;这不是选择 per-instance 的核心理由,只是说明它没有性能负担

参考:UE GAS 用 per-instance UObject;UE BehaviorTree 用 singleton + typed memory blob。我们选 GAS 风格,因为行为复杂度更接近 ability 而不是 BT task。

---

## 5. 退化与复杂行为(同一接口,不同复杂度)

### 退化:`step_runner`(单 tick 移动)

```csharp
public sealed class StepRunner : IBehaviorRunner
{
    // 没有字段 — 它没状态

    public BehaviorStepOutput Enter(BehaviorRunnerContext ctx)
    {
        var entity = ctx.Instance.Subjects.Single();
        var from   = ctx.World.PositionOf(entity);
        var to     = from + ctx.Instance.MoveDirection;

        return new BehaviorStepOutput {
            Proposals = [ CommitProposal.MoveEntity(entity, from, to) ],
            Facts     = [ ActionFact.EntityMoved(entity, from, to) ],
            EndTick   = ctx.ServerTick + 1,
        };
    }

    public BehaviorStepOutput Tick(BehaviorRunnerContext ctx) => BehaviorStepOutput.Empty;
    public BehaviorStepOutput Exit(BehaviorRunnerContext ctx) => BehaviorStepOutput.Empty;
    public BehaviorRunnerSnapshot Snapshot() => BehaviorRunnerSnapshot.Empty;
    public void Restore(BehaviorRunnerSnapshot s) { }
}
```

### 复杂:`rotate_pivot_runner`(多 tick + 多状态)

```csharp
public sealed class RotatePivotRunner : IBehaviorRunner
{
    private enum Phase { Anticipating, Contacting, Recovering, Completing }

    private Phase    phase;
    private long     anticipateUntilTick;
    private long     contactUntilTick;
    private long     pivotEntity;
    private GridCoord[] originalCells = Array.Empty<GridCoord>();
    private bool     bounced;

    public BehaviorStepOutput Enter(BehaviorRunnerContext ctx)
    {
        pivotEntity         = ctx.Instance.Subjects[0];
        originalCells       = ResolveOriginalCells(ctx.World, ctx.Instance);
        anticipateUntilTick = ctx.ServerTick + ctx.Spec.AnticipateTicks;
        phase               = Phase.Anticipating;

        return new BehaviorStepOutput {
            Facts   = [ ActionFact.RotateStarted(pivotEntity) ],
            EndTick = anticipateUntilTick,
        };
    }

    public BehaviorStepOutput Tick(BehaviorRunnerContext ctx)
    {
        switch (phase)
        {
            case Phase.Anticipating:
                if (IsBlocked(ctx.World, originalCells)) {
                    bounced          = true;
                    phase            = Phase.Recovering;
                    contactUntilTick = ctx.ServerTick + ctx.Spec.RecoverTicks;
                    return new BehaviorStepOutput {
                        Facts   = [ ActionFact.RotateContacted(pivotEntity) ],
                        EndTick = contactUntilTick,
                    };
                }
                phase = Phase.Completing;
                return new BehaviorStepOutput {
                    Proposals = BuildRotateCommits(ctx.World, ctx.Instance),
                    Facts     = [ ActionFact.RotateCompleted(pivotEntity, bounced: false) ],
                    EndTick   = ctx.ServerTick + 1,
                };

            case Phase.Recovering:
                phase = Phase.Completing;
                return new BehaviorStepOutput {
                    Facts   = [ ActionFact.RotateCompleted(pivotEntity, bounced: true) ],
                    EndTick = ctx.ServerTick + 1,
                };

            default:
                return BehaviorStepOutput.Empty;
        }
    }

    public BehaviorStepOutput Exit(BehaviorRunnerContext ctx) => BehaviorStepOutput.Empty;

    public BehaviorRunnerSnapshot Snapshot()
        => new RotateSnapshot(phase, anticipateUntilTick, contactUntilTick,
                              pivotEntity, originalCells, bounced);

    public void Restore(BehaviorRunnerSnapshot s) { /* 还原字段 */ }
}
```

两者走**完全相同的** `IBehaviorRunner` 接口、相同的 `BehaviorEngine.Start / StepDue` 路径、相同的 lifecycle 5 态。不存在"瞬时行为绕过状态机"的简化路径。

---

## 6. 封闭原语 vs 开放扩展面(DoD 的根本依据)

行为层的扩展性合同**不靠把一切做成 registry**,而是清晰区分:

### 封闭(sealed)— 世界变更原语 / 仲裁原语

```text
CommitProposalKind  enum
  MoveEntity / SetDirection / CreateEntity / DeleteEntity
  AddTag / RemoveTag / AddRuntimeEffect / RemoveRuntimeEffect
  SetComponentResult / SetAutoMoveTick / ClearRuntimeSources
  ↑ 这是「世界能被改的方式」的全集, 不是「行为种类」。
    施加 Freeze 状态 = AddRuntimeEffect(FreezeEffect) 或 AddTag("frozen")
    Teleport 行为 = MoveEntity
    新行为通过【组合既有原语】实现, 不发明新 commit kind。

ClaimKind   enum    Subject / Cell / Resource         (只有 3 类可被占用)
ClaimMode   enum    Exclusive / Shared                (并发关系只有 2 种)
ClaimChannel         暂不作为必需原语。只有当 Subject/Cell/Resource + Exclusive/Shared
                    无法表达真实冲突域时再引入。
```

仲裁(`ActionArbiter`)只看 claim 重叠 + mode,**不认识具体行为名字**。早期不要默认引入 channel,避免把 future abstraction 写进第一刀。

### 开放(extensible)— 真正的扩展点

```text
IBehaviorRunner 类           新行为 = 新类, 实现 4 个方法, 注册 factory 一行
Tag (string key)              新 tag 名 = 字符串, 零代码改动
EffectSpec (Luban)            新 effect 类型 = 加表行
ActionSpec / BehaviorDefinition (Luban)   新行为定义 = 加表行
```

**就这 4 个**。

---

## 7. Runner 之间的交互载体(4 个常规载体 + 1 个少数场景工具)

Runner **不能**直接调另一个 Runner、订阅事件、传闭包、共享 mutable 引用。Runner 之间所有交互**只**通过下面 5 种载体:

```text
1. World Fact          Component / Tag / RuntimeEffect / Position
                       (读: Runner 在 Enter/Tick 里读 ctx.World)

2. Claim               SubjectClaim / CellClaim / ResourceClaim
                       (声明意图占用, admission 时被仲裁)

3. CommitProposal      固定 11 种世界变更原语
                       (输出 → CommitResolver 应用 → world 变化)

4. ActionFact          权威事件流 (世界 delta + 行为时间线 两层)
                       (输出 → 投影/网络/审计/trace)

5. DeferredAction      少数场景工具: 本 tick 产生的延迟 action
                       (输出 → InputQueue → 下 tick 重走 admission)
```

前 4 个是常规载体。`DeferredAction` 不跟 proposal / fact 平起平坐,只在"我需要触发另一个完整 action,并且它必须重新经过 admission"时使用。例子:

| 场景 | 走哪条 |
|---|---|
| rotate 接触后触发 push | rotate emit DeferredAction(push) → 下 tick 重新 admission |
| effect 让 entity 不能移动 | effect 贡献 `Immobile` tag → StepRunner admission 时读 tag 自拒 |
| charge 蓄力期间占用身体 | charge 声明 `SubjectClaim(entity, Exclusive)` → 其他行为 admission 被挡 |
| door 打开后允许通过 | door emit `RemoveTag("blocked")` 或 `AddRuntimeEffect("door_open")` |
| 行为完成通知客户端动画 | emit `BehaviorEvent("charge_released")` 或 `BehaviorEvent("door_opened")` |
| 行为消耗 mana | 声明 `ResourceClaim("mana", Exclusive)` → 自动仲裁 |

旧 Runner 一行不用改,**因为它不需要知道有新行为存在**,它只看世界事实。

关键边界:

```text
不允许: Runner A 调 Runner B 的方法 / 订阅 B 事件 / 持有 B 引用
允许:   Runner A 读取 world 里由 B 之前写入的 tag / effect / component / position
```

这就是 World Fact 间接通信,合法且必要。

---

## 8. ActionFact 拆两层

```text
WorldDeltaFact     [sealed enum / typed struct]
  EntityMoved / EntityPushed / EntitySpawned / EntityRemoved
  RuntimeEffectApplied / RuntimeEffectRemoved
  TagAdded / TagRemoved / ComponentChanged
  ↑ 跟 CommitProposalKind 1:1, 是世界变更的事件化版本
  ↑ 客户端用来同步物理世界状态, 类型安全

BehaviorEvent      [open]   struct { string KindId; object Payload; }
  rotate_started / rotate_contacted / rotate_completed
  door_opened / charge_released / dummy_emitted ...
  ↑ 行为时间线事件, 各行为按需自由 emit
  ↑ 投影层 / 表现层按 KindId 分派, 没注册的 kindId 就忽略
```

**新行为可以 emit 新 BehaviorEvent kindId,不动主流程任何文件**。世界 delta 由仲裁后的 CommitProposal 自动产生对应 WorldDeltaFact,行为 emit 列表里不必重复。

> 落地节奏:这件事不要拖到很后。rotate 垂直切片先不强拆,但如果 PR2/PR3 继续出现 behavior-specific fact 膨胀,就提前拆成 WorldDeltaFact + BehaviorEvent。

---

## 9. 主流程白名单(扩展时禁止修改)

```text
Shared/DG.GameCore/ActionRuntime/Execution/BehaviorEngine.cs           # 编排 + 运行中实例集合
Shared/DG.GameCore/ActionRuntime/Execution/IBehaviorRunner.cs          # 契约接口
Shared/DG.GameCore/ActionRuntime/Execution/BehaviorRunnerRegistry.cs   # registry
Shared/DG.GameCore/ActionRuntime/Claims/ActionArbiter.cs              # 仲裁
Shared/DG.GameCore/ActionRuntime/Claims/ActionClaims.cs               # claim 数据模型
Shared/DG.GameCore/ActionRuntime/Commit/CommitProposal.cs             # 原语 + 数据模型
Shared/DG.GameCore/ActionRuntime/Networking/ActionRequestAdapter.cs    # WorldAction → ActionRequest
```

**违反信号**(grep 检测):

```text
白名单文件里出现 if (Primitive == X) / switch (BehaviorId) / 任何具体行为字符串
新行为 PR 的 diff 包含上述任一文件
```

任一命中视为扩展性回归,拒合入。

---

## 10. 扩展性 DoD(常绿测试守护)

```csharp
[Test]
public void DummyRunner_AddingNewBehaviorRequiresZeroMainFlowChanges()
{
    // 1. 测试本地定义一个 dummy runner
    var registry = BehaviorRunnerCatalog.CreateDefault();
    registry.Register(new RunnerId("dummy_runner"), () => new DummyRunner());

    // 2. 注册 dummy ActionSpec / BehaviorDefinition (测试 fixture, 不动主代码)
    var specs = ActionSpecRegistry.WithExtra("dummy_action", behaviorId: "dummy");
    var defs  = BehaviorDefinitionRegistry.WithExtra("dummy", runnerId: "dummy_runner");

    // 3. 跑一个完整 tick
    var engine = new BehaviorEngine(specs, defs, registry, ...);
    var result  = engine.Tick([ new WorldAction(spec: "dummy_action", ...) ], serverTick: 1, world);

    // 4. 断言
    Assert.AreEqual(1, result.Facts.Count(f => f.KindId == "dummy_emitted"));
    Assert.AreEqual(BehaviorInstanceState.Completed, result.Lifecycles.Single().State);
}

internal sealed class DummyRunner : IBehaviorRunner
{
    public BehaviorStepOutput Enter(BehaviorRunnerContext ctx)
        => new BehaviorStepOutput {
            Facts   = [ ActionFact.BehaviorEvent("dummy_emitted") ],
            EndTick = ctx.ServerTick + 1,
        };
    public BehaviorStepOutput Tick(BehaviorRunnerContext ctx) => BehaviorStepOutput.Empty;
    public BehaviorStepOutput Exit(BehaviorRunnerContext ctx) => BehaviorStepOutput.Empty;
    public BehaviorRunnerSnapshot Snapshot() => BehaviorRunnerSnapshot.Empty;
    public void Restore(BehaviorRunnerSnapshot s) { }
}
```

**这个测试一旦红,行为层架构就坏了。常绿是 DoD 的硬指标。**

---

## 11. 不变量(测试与 assert 围绕这些写)

```text
I1   每个 RunnerId 必须对应唯一 IBehaviorRunner factory, instance 运行中不可换 Runner
I2   每个 ActionBehaviorInstance 同一时刻只有一个 lifecycle state
     (Running / Completed / Rejected / Failed / Cancelled), 不可并存
I3   IBehaviorRunner 字段是私有的, 外层不可读 / 不可写
I4   Runner 之间所有交互必须走 5 种载体之一, 无侧信道
I5   ActionFact 一旦 emit 不可撤回, 修正只能由后续 fact 表达
I6   admission 是纯函数 (snapshot + world fact + spec → 决策), 同输入两次调用产出一致
I7   ClaimSet 通过 admission 后不可被 Runner 自行 release 再 acquire
     (只能 extend, 或在 Exit 一次性 release)
I8   BehaviorEngine 主流程 grep 不出任何具体行为名字字符串
I9   外层不直接读写 world entity, 一切修改经 CommitProposal
I10  Runner 类字段 + Snapshot 输出必须能完整描述其状态 (回放可重建)
```

---

## 12. 最小落地路径(PR1 范围)

**目标:rotate 一条垂直切片打通,DummyRunner 测试常绿。同时把 per-tick 编排和 cross-tick instance 集合收敛到两层模型。其它 runner 留旧路径,后续 PR 一条条迁。**

### 新增

```text
Shared/DG.GameCore/ActionRuntime/Execution/
  IBehaviorRunner.cs                  ← 契约接口
  BehaviorRunnerContext.cs            ← ctx 结构
  BehaviorRunnerRegistry.cs           ← factory map
  BehaviorEngine.cs                   ← 编排 + running instances
Shared/DG.GameCore/ActionRuntime/Execution/Runners/
  BehaviorRunnerCatalog.cs            ← 注册 rotate_pivot_runner
Shared/DG.GameCore/ActionRuntime/Execution/Runners/RotatePivotRunner/
  RotatePivotRunner.cs                ← 真状态机, 替代 ResponseProcessor
```

### 改名 / 合并

```text
IBehaviorStateMachine                  →   IBehaviorRunner   (补 Snapshot/Restore)
BehaviorRunner (包装类)                →   删
ActionBehaviorStateMachine (假状态机)  →   删
BehaviorRuntime + BehaviorInstanceRunner → 合并为 BehaviorEngine,除非代码体量证明拆分更清晰
```

### 主流程接入(最少)

```text
BehaviorEngine.cs
  - 删除调用 RotatePivotResponseProcessor 的那段
  - rotate request 走 per-instance runner Start / StepDue
  - 不出现 "rotate" 字符串, 通过 RunnerId 间接引用

running instances
  - singleton IBehaviorStateMachine  →  Dictionary<InstanceId, IBehaviorRunner>
  - Start(instance): registry.Create(runnerId) 分配
  - StepDue / Complete: 调对应 runner 的 Tick / Exit
```

### 删除(减法纪律 — 同 commit)

```text
Shared/DG.GameCore/ActionRuntime/Claims/RotatePivotResponseProcessor.cs   ← 旁路死亡
ActionBehaviorModels.cs 内:
  CompletedActionBehaviorStateMachine 类
  BehaviorRunner 包装类
  IBehaviorStateMachine 接口 (改名为 IBehaviorRunner)
```

### 测试

```text
RotatePivotRunnerStateMachineTests.cs       ← 5 个 lifecycle × 2 个子图(success / bounce)
DummyRunnerExtensibilityTest.cs              ← DoD 守护测试
```

### PR1 完工标准

```text
代码侧
  grep -r "RotatePivotResponseProcessor" Shared/ Server/ Client/   = 0
  grep -r "rotate\|pivot" BehaviorEngine.cs IBehaviorRunner.cs BehaviorRunnerRegistry.cs = 0
  rotate 路径只通过 IBehaviorRunner 接口

测试侧
  RotatePivotRunnerStateMachineTests 全绿 (5×2)
  DummyRunnerExtensibilityTest 全绿 (< 80 行)
  AuthoritativeMoveVerification + EditMode 套件不回归

DoD 锁
  PR diff 里 RotatePivotRunner 之外的代码不出现 "rotate" / "pivot" 字符串
```

---

## 13. 后续 PR 顺序(WIP=1, 每个独立 commit)

```text
PR2  迁 move/step    → IBehaviorRunner, 删 StepRunner delegate 形态;必要时提前拆 Fact
PR3  迁 spawn/remove → IBehaviorRunner, 删 PrimitiveRunner 相关;继续压瘦 Fact
PR4  迁 apply_effect/remove_effect/set_tag → IBehaviorRunner
PR5  删 PrimitiveRunner / PrimitiveRunnerRegistry / PrimitiveRunnerCatalog /
     PrimitiveRequestDispatcher / RunnerRegistry  (全部死)
PR6  业务行为 (door / charge / parry / carry / push 变体)
```

每个 PR 同 commit 加新 + 删旧。**不允许"等以后再清"的迁移。**

---

## 14. 派生子文档清单

```text
Logic/action-behavior-layers/README.md
  子文档索引与阅读顺序

Logic/action-behavior-layers/01-runtime-two-layer-model.md
  BehaviorEngine + IBehaviorRunner 两层模型

Logic/action-behavior-layers/02-runner-instance-contract.md
  per-instance Runner / 私有状态 / Snapshot / Runner 禁止互调

Logic/action-behavior-layers/03-claim-admission-arbitration.md
  Claim / admission / arbitration / commit 前仲裁的必要性

Logic/action-behavior-layers/04-commit-proposal-apply.md
  CommitProposal 世界写指令 / CommitResolver / commit kind 封闭性

Logic/action-behavior-layers/05-runner-interaction-carriers.md
  World Fact / Claim / CommitProposal / BehaviorEvent / DeferredAction

Logic/action-behavior-layers/06-runtime-effect-state-layer.md
  RuntimeEffect 状态层 / freeze 和 immobile 不新建专属 Runner

Logic/action-behavior-layers/07-fact-event-split.md
  WorldDeltaFact / BehaviorEvent 拆分

Logic/action-behavior-layers/08-dod-and-verification.md
  DoD / DummyRunner / grep 规则 / Unity TestFramework / 手动端到端验证
```

子文档与本文档的关系:**本文档是契约,子文档是细节展开**。子文档不引入新概念,只在本文档已定的契约下展开。

---

## 15. 与旧文档的差异

跟 [`old/action-behavior-instance-logic-before-runner-statemachine.md`](old/action-behavior-instance-logic-before-runner-statemachine.md) 相比,本文档的核心变化:

```text
1. 明确"每个 instance 一个 IBehaviorRunner 对象"(B 方案)
   旧版倾向 singleton + state on instance(C 方案), 实际上做不到强类型状态

2. 明确"封闭原语 vs 开放扩展面"两分
   旧版列了一堆"开放扩展点", 把 CommitProposalKind / ClaimChannel 也算进去
   实际这些是封闭原语, 用组合应对扩展, 不应该开放成 registry

3. 明确 ActionFact 拆两层
   旧版把世界 delta 和行为时间线混在一个固定 fact 列表里
   导致加新行为可能要改主流程 fact 文件

4. 明确"最小落地路径"是 rotate 单条垂直切片
   旧版没有落地节奏, 容易再走"先建框架后迁"的错路

5. 删除了网络协议 / 表现层 / Luban 重构等非本层内容
   这些在各自文档里讲, 不在行为层顶层架构污染

6. 三层运行时角色收敛为两层
   旧版把 BehaviorRuntime / BehaviorInstanceRunner / IBehaviorRunner 写成硬分层
   实际不可省的是 per-tick 编排和 cross-tick instance 集合的职责差异,不是三个类名

7. DeferredAction 降级为少数场景工具
   它只用于触发另一个完整 action 并要求下 tick 重走 admission,不是 Runner 的常规输出

8. ClaimChannel 标注为 YAGNI
   早期只保留 ClaimKind + ClaimMode。需要真实冲突域后再加 channel
```

---

## 16. 当前决策快照

```text
✓  IBehaviorRunner 是唯一行为契约 (Q1=A)
✓  每个 instance 一个 Runner 对象 (Q2=B)
✓  封闭原语 sealed, 开放扩展面只 4 个 (Q3 修正)
✓  BehaviorEngine + IBehaviorRunner 两层足够,不把当前 WIP 三类结构写成硬架构
✓  ActionFact 拆 WorldDeltaFact (sealed) + BehaviorEvent (open),PR2/PR3 若继续膨胀就提前做
✓  CostTicks ≥ 1, 不存在 same-tick 完成
✓  并行/互斥先由 ClaimKind + ClaimMode 决定, channel 暂不引入
✓  Runner 不直接调 Runner, 只通过 4 个常规载体 + DeferredAction 少数场景工具交互
✓  主流程白名单文件 grep 检测违反信号
✓  DummyRunnerExtensibilityTest 是 DoD 硬指标
✓  首个 PR 范围 = rotate 一条垂直切片打通
```
