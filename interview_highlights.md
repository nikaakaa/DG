# DG 项目面试亮点与难点解析

> 项目定位：服务器权威的 2D 网格沙盒多人游戏原型（PvPvE）

---

## 一、项目整体架构

```
┌─────────────────────────────────────────────────────┐
│  Client (Unity 2022.3)                              │
│  ├── ClientWorldRunner  ← 固定帧率 Tick 循环         │
│  ├── MovementSystem     ← 本地预测                   │
│  └── 网络层             ← Fantasy.Unity              │
├─────────────────────────────────────────────────────┤
│  Shared (DG.GameCore, netstandard2.1)               │
│  ├── GameWorld          ← 核心仿真引擎               │
│  ├── Component System   ← 组合式实体系统             │
│  └── Spatial Index      ← 空间分块索引               │
├─────────────────────────────────────────────────────┤
│  Server (.NET 8 + Fantasy)                          │
│  ├── AuthoritativeMoveRunner ← 权威移动处理          │
│  ├── MoveObserverRegistry    ← 多客户端广播          │
│  └── Luban Config            ← 数据驱动实体配置      │
└─────────────────────────────────────────────────────┘
```

**核心设计原则**：客户端只负责展示，所有状态变更必须经过服务器权威确认。`GameCore` 是纯 C# 库，客户端和服务端共用同一套仿真逻辑。

---

## 二、项目亮点

### 亮点 1：客户端/服务端共享仿真核心（GameCore）

**是什么**：`Shared/DG.GameCore` 是一个不依赖 UnityEngine 的纯 C# 库，客户端和服务端都运行同一套实体规则。

**为什么难**：通常 Unity 项目会把游戏逻辑和引擎 API 混在一起，导致服务端无法复用。这里通过严格的分层，让核心逻辑完全可测试、可移植。

**怎么讲**：
> "我把游戏核心逻辑抽成了一个独立的 netstandard2.1 库，不引用任何 UnityEngine 命名空间。这样服务端可以直接跑同一套移动规则，单元测试也不需要启动 Unity 编辑器，大幅提升了开发效率和正确性保障。"

---

### 亮点 2：组合式实体系统（Component-Based Entity System）

**是什么**：实体（Entity）是空壳 ID，行为由组件（Component）和标签（Tag）组合决定，而非继承层次。

**关键设计**：
- `ComponentStore<T>` — 泛型组件存储，类型安全
- `TagSetComponent` — 位标志枚举，零分配多状态查询
- `EntityArchetype` — Luban 配置驱动的实体模板，代码无需改动即可新增实体类型

```csharp
// 位标志标签，高效多状态判断
component.Has(WorldTag.StateStunned | WorldTag.ImmuneMechanismPush)
```

**怎么讲**：
> "实体系统用组合代替继承。一个'可推动的箱子'和一个'免疫推动的玩家'的区别，只是标签集合不同，不需要写新的子类。新增实体类型只需改 Luban 配置表，不动代码。"

---

### 亮点 3：固定帧率 Tick 累加器（Fixed Timestep Accumulator）

**是什么**：客户端用累加器模式将 Unity 的可变帧率与游戏逻辑的固定 Tick 解耦。

```
Update(deltaTime)
  → accumulator += deltaTime
  → while accumulator >= fixedTickInterval:
        TickOnce()
        accumulator -= fixedTickInterval
```

**关键细节**：`maxTicksPerFrame` 上限防止网络卡顿时帧内积累过多 Tick 导致卡死。

**怎么讲**：
> "游戏逻辑以固定 0.1 秒一个 Tick 运行，和 Unity 渲染帧率完全解耦。这是实现确定性仿真的基础——同样的输入序列，无论帧率多少，结果都一样。"

---

### 亮点 4：增量脏数据同步（Delta Dirty Tracking）

**是什么**：`SpatialDirtyTracker` 只追踪发生变化的格子和分块，`FlushDelta()` 生成 `WorldDelta` 增量包，而非每帧发送全量世界状态。

**空间分块**：32×32 格子为一个 Chunk，支持按区域快速查询和脏标记。

**怎么讲**：
> "世界状态同步不发全量快照，而是追踪哪些格子变了，只发 WorldDelta 增量包。随着世界规模扩大，带宽消耗不会线性增长，这是多人游戏可扩展性的关键。"

---

### 亮点 5：服务器权威移动 + 异步等待结果

**是什么**：客户端发送 `C2G_MoveRequest`，服务端在单线程 Tick 循环中确定性处理，用 `FTask` 异步等待结果后返回 `G2C_MoveResponse`。

```
客户端发请求 → 服务端入队 AuthoritativeMoveInput
→ 服务端 Tick 处理（冲突解决、规则校验）
→ await input.WaitAsync() 返回结果
→ 广播给所有观察者
```

**怎么讲**：
> "服务端用单线程 Tick 循环处理所有移动请求，天然避免了并发竞争。客户端通过 async/await 等待权威结果，既保证了服务器权威性，又不阻塞其他请求处理。"

---

## 三、项目难点

### 难点 1：多实体同时移动的冲突解决

**问题**：同一个 Tick 内，多个实体可能同时想移动到同一格子，或形成推动链（A 推 B，B 推 C）。

**解决方案**：`BehaviorIntent → IntentArbiter → RulePlanner → MovePlan → ConflictResolver → Commit` 流水线，在提交前统一解决所有冲突。

**难在哪**：
- 推动链可能有环（A 推 B，B 推 A）
- 免疫标签（`ImmuneMechanismPush`）需要在规划阶段提前过滤
- 冲突解决结果必须确定性，相同输入永远产生相同输出

**怎么讲**：
> "最难的是同帧冲突。两个玩家同时走向同一格，或者形成推动链，必须在一个 Tick 内确定性地解决。我设计了一个意图→仲裁→规划→冲突解决→提交的流水线，确保无论输入顺序如何，结果都是确定的。"

---

### 难点 2：客户端预测与服务器权威的对账（Reconciliation）

**问题**：客户端本地先执行移动（预测），服务端返回权威结果后，如果不一致需要回滚并重放。

**关键字段**：`G2C_MoveResponse` 携带 `ClientTick` 用于对账，客户端可以知道哪个本地预测被否定了。

**难在哪**：
- 网络延迟导致客户端和服务端状态存在时间差
- 回滚后需要重放后续已确认的输入
- 视觉上要平滑，不能出现明显的位置跳变

**怎么讲**：
> "客户端预测让操作感觉即时，但服务端可能拒绝这个移动。我在响应包里带了 ClientTick，客户端可以精确找到哪一帧的预测是错的，回滚到那一帧再重放后续输入，保证最终一致性。"

---

### 难点 3：纯 C# 共享库的架构约束

**问题**：GameCore 不能引用 UnityEngine，但需要支持客户端和服务端两种运行环境。

**解决方案**：
- 所有组件用值类型 struct，避免 GC 压力
- 空间索引、脏追踪等基础设施全部用纯 C# 实现
- 客户端通过适配层（`ClientWorldContext`）将 GameCore 状态映射到 Unity 渲染

**难在哪**：需要在设计阶段就严格划定边界，任何一处不小心引入 UnityEngine 依赖都会破坏服务端可用性。

**怎么讲**：
> "维持 GameCore 的纯 C# 约束需要持续的架构纪律。我用接口和适配层隔离 Unity 依赖，让核心逻辑可以在 .NET 8 服务端直接运行，也可以在 Unity 编辑器里跑单元测试，不需要进入 Play Mode。"

---

### 难点 4：数据驱动配置与代码的边界

**问题**：用 Luban 配置表定义实体原型（EntityArchetype），但配置变更不能破坏运行时逻辑。

**解决方案**：`ComponentApplicationRegistry` 将 `ComponentKind` 枚举映射到组件实例化逻辑，配置表只声明"这个实体有哪些组件"，不包含行为逻辑。

**怎么讲**：
> "Luban 配置表定义实体的组件组合，代码定义每个组件的行为规则。策划可以通过改配置表新增实体类型，不需要程序介入，同时也不会意外改变已有实体的行为。"

---

## 四、技术栈总结

| 层次 | 技术 | 版本 |
|------|------|------|
| 客户端引擎 | Unity | 2022.3.62f2c1 |
| 网络框架 | Fantasy / Fantasy.Unity | 2026.0.1018 |
| 序列化 | LightProto + MemoryPack | — |
| 服务端运行时 | .NET | 8 |
| 配置导出 | Luban | v4.7.0 |
| 共享核心 | C# (netstandard2.1) | — |

---

## 五、一句话总结（面试开场用）

> "这是一个服务器权威的多人网格游戏原型，核心挑战是在保证服务端权威性的前提下，实现低延迟的客户端响应。我通过共享仿真核心、固定 Tick 确定性仿真、增量脏数据同步和意图冲突解决流水线来解决这些问题。"
