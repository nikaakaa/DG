# Runner 实例契约

## 结论

每个 `ActionBehaviorInstance` 拥有一个独立 `IBehaviorRunner` 对象。Runner 是 per-instance,不是 singleton。

```text
ActionBehaviorInstance #1001 -> RotatePivotRunner 实例 A
ActionBehaviorInstance #1002 -> RotatePivotRunner 实例 B
```

两个实例即使使用同一个 Runner 类型,内部字段也不能共享。

## 为什么 per-instance

per-instance 的核心理由不是性能,而是工程化:

```text
强类型状态:
  private Phase phase;
  private long contactUntilTick;
  private GridCoord[] originalCells;

测试简单:
  new RotatePivotRunner()
  runner.Enter(ctx)
  runner.Tick(ctx)

重构友好:
  字段改名和阶段拆分由 IDE / 编译器守护

Snapshot 清晰:
  Runner 自己知道哪些字段构成状态
```

singleton + `StateData string` 的问题:

```text
弱类型
字段名靠字符串
状态 schema 难迁移
单测要先构造外部状态容器
所有行为状态容易变成一个巨型 union
```

## Runner 私有状态

Runner 字段只能由 Runner 自己读写:

```csharp
public sealed class ChargeRunner : IBehaviorRunner
{
    private Phase phase;
    private long releaseTick;
    private long targetEntityId;
}
```

`BehaviorEngine` 不能读取这些字段。它只能调用接口并保存 Runner 实例。

## Snapshot / Restore

Snapshot 是回放、调试、断线恢复需要的状态快照。它不是第一天就要做成通用序列化框架。

最小口径:

```text
每个 Runner 自己 Snapshot 自己的字段
每个 Runner 自己 Restore 自己的字段
外层只保存 opaque snapshot
```

不要在外层维护:

```text
RotateSnapshotFields
DoorSnapshotFields
ChargeSnapshotFields
...
```

否则外层重新认识具体行为。

## Runner 不能互调

禁止:

```text
ChargeRunner new MoveRunner().Tick(...)
DoorRunner.OnOpened += MoveRunner.HandleDoorOpened
RotateRunner 持有 PushRunner 引用
```

原因:

```text
1. 具体行为之间产生编译期依赖
2. 加新行为时旧 Runner 要跟着改
3. 主流程外出现侧信道
4. replay / deterministic audit 难以追踪
```

允许:

```text
Runner A 写 CommitProposal / BehaviorEvent
BehaviorEngine apply 后 world 出现事实
Runner B 之后读取 world fact
```

这叫 World Fact 间接通信,不是 Runner 互调。
