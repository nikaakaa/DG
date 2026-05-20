# ECS 存储与运行时演进目标

## 文档目的

这份文档只回答一个问题：

```text
DG 的世界存储和运行时是否应该迁移到 Unity ECS（DOTS / Entities）或自研 ECS，
以及如果迁移，应遵守什么顺序、边界和停止条件。
```

它不是已经批准的 OpenSpec 需求，也不是立即实现清单。它用于统一后续重构前的目标、边界、约束和验证口径，避免在规则层未收敛时触发大规模存储层重写。

## 项目目标输入（2026-05-14 确认）

以下输入已由用户确认，本文档其余结论以此为前提：

```text
目标实体规模     ~ 万级别的 active entities / 房间
玩法形态         PVPVE
工厂结构         由现有实体 / 组件 / 规则结构搭建
                 单台机器 5–20 entities，几十台联动很容易到千级
同步模型         server authoritative + WorldDelta（不是 lockstep）
跨端共享         同一份 Shared/DG.GameCore.dll
                 同时被 Fantasy 服务端和 Unity 客户端加载
确定性需求       服务端权威 + 客户端镜像
                 不需要 lockstep 级别 bit-exact 跨端复现
```

这条输入对后续结论的影响：

```text
- 自研 ECS 的触发条件第 #1 #2 项（万级实体 / tick 压力）
  不再是纯假设，而是需要提前埋点、压测和预留 API 边界的高风险方向。
- 因此对外 API 的 archetype-friendly 纪律应从现在开始执行；
  但 archetype / chunk 存储迁移仍必须等待 profile 证据和规则层收敛。
- DOTS 跨端共享在该输入下仍然不成立，理由见第一节物理性失败链。
```

## 当前判断

当前 DG 的存储结构事实如下：

```text
Shared/DG.GameCore
  netstandard2.1 纯 C#
  目标框架约束 = 服务端 Fantasy + Unity 客户端必须共用同一套规则核心

World/GameWorld.cs
  Dictionary<long, GameEntity> entities
  Dictionary<Type, IComponentStore> componentStores
  ChunkStore chunks
  SpatialEntityIndex spatial
  SpatialDirtyTracker spatialDirty

Components/ComponentStore.cs
  ComponentStore<T> = Dictionary<long, TComponent>
  没有 archetype / chunk / SoA / iterator 优化

Entities/GameEntity.cs
  只保存 EntityId / ConfigId / ArchetypeId / EntityTarget 与字符串 tag set
  真实组件数据全部放在 World 的 ComponentStore 里
```

也就是说当前结构在语义上已经是 ECS（entity + component + system），但在**存储和迭代层面仍然是字典 ECS**，不是 archetype/chunk ECS。

这是早期阶段的合理选择：字段清晰、重构容易、测试友好，完全能覆盖 demo 阶段的实体数量和规则数量。但它不是"Unity DOTS 级别的 data-oriented 存储"，也不是自研性能导向 ECS。

## 核心结论

两个结论先给：

```text
1. Unity ECS (DOTS / Entities) 在本项目不是可选项。
2. 自研 ECS 是可选的，但在当前规则层收敛前应延后。
```

原因如下。

## 一、为什么 Unity ECS (DOTS) 在本项目不适用

### 这不是依赖配置问题，是物理加载失败

容易出现的误解是"在 csproj 里加上 Unity.Entities 的引用就行"。这条不成立。Unity.Entities 不是独立托管库，它是 Unity Player 进程内的客户端代码。具体失败链：

```text
Unity.Entities.dll
  -> Unity.Collections.dll       (NativeArray / Allocator)
  -> Unity.Burst.dll              (JIT, 依赖 Unity 内嵌 mono)
  -> UnityEngine.CoreModule.dll  (内部全是 extern / [DllImport])
  -> UnityPlayerCore native runtime（Unity Player 进程私有）

Fantasy 服务端 = .NET 8 / .NET Standard 2.1 进程
  -> 没有 Unity Player runtime
  -> 没有 mono 内嵌环境
  -> 没有 UnityPlayerCore native

加载流程：
  1. dotnet run 启动 Fantasy 进程
  2. CLR 加载 DG.GameCore.dll
  3. CLR 解析 Unity.Entities 引用 → 找不到 native 入口
  4. 第一次执行 EntityManager / NativeArray 方法
     → TypeLoadException / DllNotFoundException
     或 P/Invoke 返回未定义行为
```

DOTS 的设计前提是"在 Unity Player 进程内运行"，脱离这个进程没有运行时支持。这不是 NuGet 配置问题，不是版本问题，是 Unity 把 DOTS 设计成 Unity 内部组件、不向 .NET 生态发布的结果。

### Shared 层的硬约束

DOTS 不是单纯的存储库，它是 Unity Runtime 耦合的运行时：

```text
Unity.Entities          -> 依赖 UnityEngine
Unity.Collections       -> 依赖 Unity Job System
Unity.Burst             -> 依赖 Unity Burst Compiler
EntityManager / World   -> 依赖 Unity PlayerLoop / SubScene
```

本项目 Shared 层的硬约束是：

```text
Shared/DG.GameCore
  netstandard2.1 纯 C#
  不依赖 UnityEngine
  服务端 Fantasy (纯 .NET) 必须能直接引用
  客户端 Unity 也引用同一份 DLL / package
```

把规则核心迁到 Unity ECS，意味着两条路都走不通：

```text
路径 A：服务端改为 Unity Headless Server
  成本：服务端失去 Fantasy 原生 .NET 生态、部署改为 Unity Player 打包
       容器化 / 扩容 / 热更 / CI 全部重建
  收益：能共用 DOTS 组件定义
  结论：不可接受

路径 B：规则层拆成 Unity 客户端版本与服务端版本各一份
  成本：组件、系统、Query、archetype、JobSystem 代码要维护两份
       权威语义要跨两套存储模型保持一致
       违反"服务端权威 + 客户端镜像"的单一真源原则
  结论：不可接受
```

因此 DOTS 迁移在本项目的第一性约束下直接否决，不需要进入详细评估。

### 唯一可走通的 DOTS 路径（不推荐）

如果有人坚持 DOTS，唯一可行的形态是：

```text
路径 C：服务端改为 Unity Headless Server Build
  - 用 Unity Editor 构建 Linux Server Player
  - 服务端进程 = Unity Player（headless），不是 .NET 进程
  - 抛弃 Fantasy 框架（Async / Network / Scene / Session 全替换）
  - Server/Hotfix/AuthoritativeMove 整套按 Unity NetCode 或自写网络层重做
  - 容器镜像从 ~80MB 变 ~500–800MB
  - 工作量估算：3–6 个月专职

收益：服务端可以直接用 DOTS Component / System
代价：项目脱离 Fantasy 生态，CI / 部署 / 热更全部重建
结论：除非用户主动接受这个代价，本路径不在本文档讨论范围
```

后续不再讨论 Unity ECS 作为服务端方案。Unity 客户端可以继续使用 MonoBehaviour + 客户端镜像层，或者**作为独立决策**引入 DOTS 作为客户端渲染镜像（见第十节）。GameCore 不会成为 Unity ECS World。

## 二、自研 ECS 的评估

### 自研 ECS 真正要解决的问题

自研 ECS 的价值不在"听起来高级"，而在以下几项可观察的收益：

```text
1. 迭代性能：按 archetype 连续内存遍历，不再按字典键查。
2. 查询表达力：WithAll / WithAny / WithNone 语义化组合，不再手写 Foreach + Has 判断。
3. 结构变更成本可控：add/remove component 的代价落到 chunk 迁移上，便于剖析。
4. 批量结构变更：支持 command buffer，一次 tick 内统一提交。
5. 系统调度清晰：System 顺序、读写依赖可静态声明。
```

以上每一项的前提都是：**真实出现了可观察的压力**。

### 自研 ECS 的成本

```text
1. 要重写 ComponentStore<T> / Entity 结构 / GameWorld 查询层。
2. 要重写空间索引与 archetype 的联动（SpatialEntityIndex / ChunkStore / SpatialDirtyTracker 现在都按 entityId 索引）。
3. 要重写测试基座（当前 Unity TestFramework / SandboxScenario / 服务端权威验证全部依赖现在的 GameWorld API）。
4. 要重写或适配仍在演进、且动到 World / Snapshot / WorldDelta 的 OpenSpec change 与本地改动路径。
5. 要重新定义序列化边界（Snapshot / WorldDelta 现在依赖当前字段形态）。
```

### 收益 vs 成本

当前观察：

```text
实体数量         = demo 阶段，单地图个位数到数十。
组件数量         = 约 10 个左右，全部是小 struct。
迭代热点         = 目前没有 profile 数据指出 ComponentStore 字典查找是瓶颈。
规则层稳定性     = behavior policy / blocked / cost / rule execution 仍在演进，active change 数量以 openspec list 当前输出为准。
序列化稳定性     = Snapshot / WorldDelta 字段仍在扩。
测试现状         = 以最近一次 Unity TestFramework / 服务端权威验证 / SandboxScenario local runner 的实际输出为准。
```

在当前事实下：

```text
自研 ECS 的收益 = 未兑现的潜在性能 + 未兑现的潜在表达力。
自研 ECS 的成本 = 立即支付的结构性重写、序列化边界重写与所有在途规则变更的 rebase 成本。
```

这是明显的负收益。因此**自研 ECS 不应作为当前阶段的主线动作**。

### 候选实现路线

如果将来确实要落地 archetype 存储，候选不止"完全自研"一条。结合 netstandard2.1 跨端约束，可选项有：

```text
Arch (https://github.com/genaray/Arch)
  候选理由：无 Unity 运行时依赖，偏 archetype / chunk 模型
  archetype + chunk + WithAll/WithAny/WithNone 查询
  社区活跃，工厂类项目案例最多
  代价：GameWorld 内部存储替换为 Arch World，
        SpatialEntityIndex / ChunkStore 需要对接
  状态：候选，需要技术 spike 验证当前版本的目标框架、AOT/Unity 兼容性、序列化边界

friflo Engine.ECS
  候选理由：无 Unity 运行时依赖，带结构变更 buffer 与序列化能力
  自带结构变更 buffer + JSON / 二进制序列化
  对 Snapshot / WorldDelta 已成型的项目契合度高
  状态：候选，需要技术 spike 验证当前版本的目标框架、性能曲线、与 GameWorld API 的贴合度

DefaultECS
  候选理由：无 Unity 运行时依赖，API 简洁
  archetype 不是核心模型（dictionary + bitmask）
  迁移收益不如 Arch
  状态：备选，仅在迁移目标偏查询表达力而非 chunk 存储时再评估

自研薄 archetype
  完全自控，没有外部依赖
  当前 ChunkStore / SpatialEntityIndex / Snapshot 已经写出 ~60% 雏形
  缺：Archetype = sorted component type set 抽象、chunk migration、
      query compile、command buffer
  代价：3–5 周写一个"够用"版本 + 持续维护
  状态：候选，需要先证明外部 ECS 库的适配成本高于长期维护成本

不推荐：
  Unity DOTS — 跨端硬约束已说
  LeoECS Lite — 生态偏 Unity，跨端踩坑
  Svelto.ECS — Unity 重耦合
  Entitas — 代码生成器跨端 csproj 配置麻烦
```

最终选择留到触发条件成立后，根据当时的 profile、当前包版本、目标框架兼容性和已有代码贴合度决定。文档不在此刻锁死任何第三方库。

## 三、触发自研 ECS 迁移的条件

自研 ECS 的启动条件必须是可观察、可复现的，不是"以防万一"。以下条件至少满足两条时再启动：

```text
1. 单 tick 世界规则耗时超出目标（例如每 tick ≥ 5ms 稳定出现），
   且 profile 显示 ComponentStore 字典访问 / Foreach 扫描是主要开销。
2. 实体数量稳定超过千级（例如 >= 2000 active entities / server world）。
3. 组件数量稳定超过 30 个，且多个系统频繁 WithAll / WithAny 查询组合。
4. 规则层已经收敛：push / blocked / cost / action pipeline 至少一年内不会大改。
5. 存在至少三个系统因为当前字典存储被迫做出明显妥协（例如不得不缓存自己的 subset 以避免全扫）。
```

不满足以上任意两条时，默认保留当前字典 ECS。

## 四、如果启动自研 ECS，演进阶梯

即便将来启动，也**不是一次性全量重写**，应分阶段：

### 阶段 0：保持外部 API 不动

```text
GameWorld 对外 API = 迁移前后语义完全一致
AddEntity / RemoveEntity / SetComponent / TryGetComponent / HasComponent
GetEntitiesAt / GetEntitiesAround / DiffVisibility / FlushDelta

内部存储从 Dictionary<Type, IComponentStore> 替换为 archetype table。
外部系统代码不重写。
```

这是最重要的约束。任何一阶段都不允许同时重写 API 与内部存储。

### 阶段 1：引入 archetype 存储

```text
1. 定义 Archetype = sorted component type set。
2. 定义 Chunk = 固定条目 archetype 行存储 (SoA 可选，初期可用 AoS)。
3. Entity -> Chunk + row index。
4. add/remove component 触发 chunk 迁移。
5. ComponentStore<T> 保留一层薄 façade，对外仍是 Get/Set/Has/Remove。
```

验证：
```text
EditMode 全部通过
服务端权威验证通过
SandboxScenario local runner 通过
性能：迭代热点应可测出改进，否则不继续下一阶段。
```

### 阶段 2：引入 Query 编译

```text
1. Query = WithAll / WithAny / WithNone 组合。
2. 加载时编译为 archetype mask。
3. 运行时按 matched archetypes 迭代 chunk。
4. 保留 TryGetComponent / HasComponent 供非迭代路径使用。
```

### 阶段 3：引入 command buffer

```text
1. Tick 内 add/remove/spawn/destroy 先写入 command buffer。
2. Tick 末批量提交。
3. 与 dirty / snapshot / delta flush 语义对齐。
```

### 阶段 4：考虑 System 调度声明

```text
1. System 声明读写组件集合。
2. 调度器按依赖排序或并行。
3. 只在确实出现多 System 并行收益的场景引入，不为了显得规范而做。
```

每一阶段的停止条件是：**如果不再带来实测收益，就在当前阶段收工**。不必走完整条路径。

## 五、非目标

第一阶段以及可预见的近期**都不做**：

```text
- 迁移到 Unity DOTS / Entities / SubScene / Baking
- 把 GameCore 拆成两份 (Unity 版 + 服务端版)
- 引入 Unity.Collections / Unity.Burst / Unity.Jobs
- 为 archetype 引入 reflection-heavy 泛型分发
- 运行时脚本化系统 (script-defined system)
- 全量 data-oriented 重写而不保留 GameWorld 对外 API
- 为了"更像 ECS"而拆未被迭代热点证明的字段
```

## 六、当前阶段应该做什么

在自研 ECS 被触发之前，仍然可以做这些**低风险、高信息量**的准备。结合"几千 active entities"目标，第六节的优先级被升级：现在要先做 API 边界收口、热路径埋点和可回滚的小优化，而不是直接替换存储模型。

### Phase 0 立即动作：去 Dictionary 化与 API 边界纪律

代码扫描已识别出在千级实体规模下风险最高的若干热路径。这些不需要换 ECS 框架就能先验证、先减压，且修完为后续 archetype 迁移铺路：

```text
[Server/Hotfix/AuthoritativeMove/Runtime/AuthoritativeWorldTickRunner.cs:486-503]
  EnqueueAutoMoveActions 每 tick 调 EnumerateEntities() 全表扫
  对每个实体做 3 次 TryGetComponent
  N=3000 → 每 tick 9000 次组件查询 + EnumerateEntities 的排序分配
  验证：压测记录该路径 tick 耗时、分配量和调用频率
  改造候选：维护"持有 AutoMoveComponent 的实体集合"索引，避免全扫

[Shared/DG.GameCore/World/GameWorld.cs:117]
  EnumerateEntities() 内部 entities.Values.OrderBy(...).ToArray()
  每次调用都重新分配 + 排序
  验证：先找出所有调用方是否真的依赖稳定排序
  改造候选：返回 IEnumerable<GameEntity> + 内部 yield return
        排序版本拆成显式 EnumerateEntitiesOrdered()，调用方按需

[Shared/DG.GameCore/World/GameWorld.cs:122-186]
  GetEntitiesAt / GetEntitiesAround / TryGetChunkEntities
  每次都 new List + ToArray
  验证：压测 push 传播 / 视野查询时的分配量
  改造候选：返回不拷贝的只读 view（IReadOnlyList 包内部 buffer）

[Shared/DG.GameCore/Spatial/SpatialEntityIndex.cs:88-97, 99-115]
  GetEntitiesAt / TryGetChunkEntities 内部 entities.ToArray()
  push 传播 / 视野查询每个 cell 都触发一次
  验证：确认外部不会修改内部 List
  改造候选：暴露不拷贝的访问入口

[Server/.../AuthoritativeWorldTickRunner.cs:79,82]
  CreateEntitySnapshot() 每 tick 调 2 次（before + after 日志）
  内部 entities.Values.OrderBy.ToDictionary + 6 次 TryGetComponent / entity
  N=3000 → 单 tick 36000 次组件查询 + snapshot/toDictionary 分配
  验证：区分调试日志成本与权威规则成本
  改造候选：日志快照走 touched/dirty list，而不是全表 snapshot；
        生产环境关掉或采样输出
```

这一步不动 ComponentStore 内部存储，不引入新 ECS 框架，但**锁住对外 API 边界**——任何外部代码不应依赖：

```text
- Dictionary<long, T> 的 key 顺序
- O(1) 随机访问 by entity id 的特定语义
- LINQ 链式调用（OrderBy / ToArray / ToDictionary）在热路径
- 每次查询返回新数组 / 新字典的副作用
```

### 配套纪律

```text
1. 为 ComponentStore<T> 添加一层可观测性：
   记录 Get/Set/Has 命中次数、组件数量、每 tick 迭代次数。
   这些数据是未来决定是否迁移的前置依据，不迁移时也能帮助规则层优化。
2. 在性能敏感查询点预留 archetype 友好的调用姿态：
   使用 TryGetComponent / HasComponent，而不是依赖 Dictionary 特性。
   避免 LINQ 在热路径上滥用。
3. 保持 GameWorld 对外 API 稳定：
   不把字典实现细节泄露到外部调用方。
   这是未来替换存储层的前提。
4. 规则层收敛优先：
   以 openspec list 当前输出和本地未提交改动为准，
   不要在规则未收敛时叠加存储重写。
5. 不要在测试里做 ECS archetype 预期断言：
   测试应断言"实体具有 X 组件 / 世界在 (x,y) 有 blocker"，
   而不是"某个 archetype chunk 数量"。
   否则存储实现细节会被测试锁死。
```

## 七、性能与测试边界

如果未来启动迁移，任何一阶段合并回主线必须满足：

```text
Unity TestFramework EditMode 全部通过
服务端权威验证项目通过
SandboxScenario local runner 通过
Snapshot / WorldDelta 与迁移前在同一世界状态下字段一致
不引入 UnityEngine 依赖
不引入平台特定依赖
不破坏 Shared/DG.GameCore 的 netstandard2.1 目标框架
```

禁止的验证替代：

```text
× 用"编译通过"代替 EditMode 验证
× 用"某一系统加速 N%"代替整体回归
× 用一个客户端本地 Play Mode 代替双客户端同步验证
× 用临时注释测试绕过未对齐的 API
```

## 八、当前自检

这份文档没有宣称当前存储层已经能支撑大型项目，也没有宣称自研 ECS 必须立刻启动。

当前真实状态是：

```text
GameCore 是字典 ECS，不是 archetype ECS。
服务端和 Unity 客户端共用纯 C# 规则核心。
Unity DOTS 与该核心的跨端共享不兼容（物理加载失败，非配置问题）。
自研 ECS 的收益尚未被性能或规则复杂度证明。
规则层正在演进，active OpenSpec change 与本地未提交改动必须以当前仓库状态复核。
存储层重写会放大这些在途变更的 rebase 成本。
但工厂规模（千级实体）已确认为目标，Phase 0 API 纪律、埋点和热路径验证应立即执行。
```

因此下一步目标名应该是：

```text
Phase 0 API 去 Dictionary 化 + 规则层收敛阶段
```

而不是：

```text
ECS 存储层迁移阶段
```

## 九、待确认

以下问题需要用户和 / 或未来 profile 数据来回答，才可能触发实际启动：

```text
1. [已确认] 未来目标实体数量级 → 几千 active entities / 房间。
2. 服务端 tick 目标是多少 ms？当前是否已经接近目标？
3. 组件种类预计会增长到多少？是否会出现大量参数化组件？
4. 规则层 (push / blocked / handoff / cost) 的收敛时间表？
5. [已确认] 多人模式 → PVPVE。
6. 是否接受"规则层与存储层分阶段推进"的节奏，还是期望某个时间点一次性完成？
```

在上述未确认问题没有清晰答案前，默认结论是**保留当前字典 ECS + 执行 Phase 0 API 纪律，不启动 archetype 迁移**。

## 十、客户端 DOTS 镜像（独立决策）

客户端是否引入 DOTS 作为渲染镜像层，与服务端 ECS 选型**完全独立**。两个决策的触发条件不同：

```text
服务端换 archetype ECS  ← 触发条件：实体过千 + tick profile 显示字典/全扫路径成为主瓶颈
客户端引入 DOTS 渲染   ← 触发条件：客户端 FPS 不达标 + profile 显示 GameObject/Transform 成为主瓶颈
```

### 客户端 DOTS 镜像的架构形态

```text
Shared/DG.GameCore.dll              （netstandard2.1，规则核心）
  纯逻辑：ActionSpecs / push 传播 / blocked policy / commit
  存储抽象：GameWorld 对外 API
  不引用任何 ECS 框架

Server                              （Fantasy + 自研 archetype / Arch）
  服务端 World = GameWorld 内部存储
  权威 tick：Shared 规则 + archetype 存储
  WorldDelta broadcast 给客户端

Client                              （Unity + DOTS Entities Graphics）
  ClientWorld = DOTS World（Entities + IComponentData）
  WorldDeltaToDotsBridge：把 EntitySnapshot 应用到 DOTS World
  ❌ 不跑 Shared 规则
  ❌ 不在 DOTS 上跑 push 传播
  ✅ 只渲染 / 插值 / 客户端预测
```

### 客户端 DOTS 的收益

```text
工厂 + 几千实体渲染：
  DOTS Entities Graphics = GPU instancing 自动批量化
  传送带 / item 流 = ECS chunk 遍历 + Burst
  没有 GameObject 开销（Transform / 组件查找）

MonoBehaviour 方案对比：
  几千 GameObject = 数千个 Transform 树节点
  Update 调用栈深
  GC 压力大
```

### 客户端 DOTS 的成本

```text
1. SubScene / Baking / Authoring 学习曲线
2. Entities Graphics 配置坑多
3. 当前 ClientWorldVisuals 全套要重写
4. 客户端镜像、动画和调试 UI 仍在跟随服务端语义演进，重写展示层会放大同步成本
```

### 结论

```text
当前不引入客户端 DOTS。
触发条件：工厂 vertical slice 渲染 profile 显示 MonoBehaviour 方案 FPS 不达标。
引入时只新增 WorldDeltaToDotsBridge 层，Shared 不动，Server 不动。
```

## 一句话总结

```text
Unity ECS 被跨端物理加载约束否决（不是配置问题），
自研 ECS / Arch 应在规则层收敛并出现实测压力之后再启动，
Phase 0 API 去 Dictionary 化、埋点和热路径验证现在就执行，
客户端 DOTS 镜像是独立决策、独立触发，
存储层演进永远保持外部 API 不变。
```
