# Emergent Push Convergence

本文记录 `fix-multi-contact-push-propagation` 后续需要处理的一个语义问题：多分支 push 在涌现式 connected body 系统里重新命中同一个 downstream subject 时，不应该被误判成 `push chain cycle`。

## 现场证据

一次失败日志：

```text
request entity:1 target:(0,-1) before:(0,-2)
push cycle diagnostics tick:290
cycle:state:3
conflict:800000009
inChain:True
inAdding:False
candidate:800000009,800000010,800000011,800000012,800000013
chain:1,800000000,800000001,800000002,800000003,800000004,800000005,800000006,800000007,800000008,800000009,800000010,800000011,800000012,800000013,800000015
reason:push chain cycle
```

关键结论：

- `candidate` 是本次准备创建的下游 child subject。
- `chain` 是当前 pending push 链里已经参与过的 entity 集合。
- `candidate` 完整出现在 `chain` 中。
- 失败不是因为传播长度，也不是因为 contact 数量，而是因为同一个 connected body subject 在同一条 pending push 链里被后续分支再次解析到。

## 底层原因

DG 当前的 connected body 不是预先固定的 rigidbody，而是运行时根据这些因素涌现出来：

- entity 位置；
- `PortConnectorComponent`；
- entity `DirectionComponent`；
- runtime/static component result；
- `PortConnectionSystem.CollectConnectedGroup()`。

在该失败现场，快照中存在一组关键连接：

```text
800000011 pos:(-1,2) ports:Left, Right, Up
800000012 pos:(-1,3) ports:Left, Right, Down
```

它们上下相邻且端口互通，使上方和下方结构在运行时被连接。多分支 push 传播时，不同分支可以从不同 contact 路径解析到同一个 downstream connected body：

```text
source
  branch A -> downstream body X
  branch B -> downstream body X
```

这是涌现式系统里的收敛，不等于循环。

## 当前误判点

当前 `PendingActionState.TryAddChildUnits()` 和 `TryAddSiblingUnitsToReadyBatch()` 会先调用 `HasReadyUnitWithSubject()` 去重，再调用 `CanAddChild()` 检查 chain。

问题是 `HasReadyUnitWithSubject()` 只检查 `Ready` 状态的 unit：

```text
unit.Status == Ready && same subject key
```

如果某个 subject 已经在前序分支中进入过 pending state，并且状态已经变成 `Handoff` 或 `Succeeded`，后续分支再次解析到同一个 subject 时不会被识别为重复 subject，而是进入 `CanAddChild()`。

`CanAddChild()` 目前只做 entity 级别交集判断：

```text
candidate ids intersects Chain => push chain cycle
```

因此，同一个 subject 的重复收敛会被误报为 `push chain cycle`。

## 需要保留的原语义

修复不能放松整个 cycle guard，也不能把 pending 改成一次性全局求解器。以下语义必须保留：

- `BodyCapabilityResolver` 只负责发现 external contacts。
- `ActionSpecs` 负责把 contact 解释成 handoff subject，并按 subject 合并。
- `PendingRuleStates` 负责 child unit 生命周期、batch 结果、重复 subject 和 cycle 安全。
- 多 contact 不等于多 child。
- 多 distinct downstream subject 才等于多个 child。
- source body 在 handoff 时不移动。
- push contact batch 仍然是 all-success。
- 真实 ancestor revisit 仍然失败为 `push chain cycle`。
- 部分重叠但 subject key 不同的 candidate 仍然失败，避免同一 entity 被两个 action unit 同时管理。

## 修复方向

Pending 层需要把“完全相同 subject 的重复出现”和“部分重叠或回到祖先”区分开。

建议在 `PendingActionState` 内维护一个 subject key 集合：

```text
seenSubjectKeys
```

每个 action unit 创建时，将其 resolved subject ids 排序后生成稳定 key 并记录。

加 child 的判断顺序应改成：

```text
candidate subject ids
=> build stable subject key
=> 如果 key 已经 seen：跳过
=> 否则检查 candidate ids 是否和 Chain 或本批 adding 重叠
=> 没重叠：创建 child unit，并记录 key / ids
=> 有重叠：push chain cycle
```

语义分类：

```text
完全相同 subject 再出现 => convergence，跳过
新 subject => 创建 child unit
不同 subject 但共享 entity => cycle / unsafe overlap，失败
回到 ancestor subject => cycle，失败
```

## 为什么适合涌现系统

涌现式 connected body 允许拓扑通过端口、方向、runtime effect、位置关系动态形成。push 传播不是稳定树结构，可能自然出现多路径收敛：

```text
branch A -> X
branch B -> X
```

如果 pending 只用 entity 级别的 `Chain.Contains(id)` 判断，就会把这种收敛误判为循环。

但如果完全移除 chain guard，又会允许真正的循环：

```text
A -> B -> C -> A
```

因此正确边界是：

- subject key 相同：说明是同一个 downstream body 的重复收敛；
- subject key 不同但 entity 有交集：说明 action-unit 所有权不清，继续保守失败；
- chain 中出现相同 subject key：跳过，不再生成重复 child；
- chain 中出现不同 subject 的交集：失败。

## 需要补的测试

实现前应先补或调整测试，让行为边界明确。

1. 同一 pending 链中后续分支再次解析到完全相同 downstream subject：
   - 不报 `push chain cycle`；
   - 不创建重复 child unit；
   - owner 最终成功。

2. 真实 ancestor subject revisit：
   - 继续失败为 `push chain cycle`。

3. 两个不同 downstream subject：
   - 继续创建两个 child units；
   - 属于同一个 push contact batch；
   - all-success 后 owner 成功。

4. 不同 subject 但共享部分 entity：
   - 继续失败为 `push chain cycle` 或稳定 unsafe overlap reason；
   - 不允许同一 entity 被两个 action unit 同时管理。

## 当前判断

这不是事务系统不原子，也不是需要无限传播限制。问题是 pending 的安全判断粒度过粗：它已经有 entity 级 chain guard，但缺少 subject-level convergence 识别。

修复应保持当前 action-unit / pending-batch 模型，只把 duplicate-subject 判断从 `Ready unit` 扩展为 `state-level seen subject`。
