# DG 服务端权威 ECS 架构目标

## 目标

DG 的长期架构目标是：

```text
服务端权威推进世界。
Shared/DG.GameCore 承载规则和状态。
Fantasy 承载服务端外壳。
Unity 客户端承载镜像、输入和表现。
Luban 承载配置编译和 ID 映射。
```

这份文档不是 OpenSpec 提案，也不是任务清单。它只定义后续重构、提案、测试和架构讨论必须遵守的边界。

## 总体架构

```text
Client / Unity
  输入、镜像、表现、动画、调试
  |
  | C2G input / debug request
  v
Server / Fantasy
  Session、Scene、Handler、生命周期、广播
  |
  | 调用权威规则核心
  v
Shared / DG.GameCore
  GameWorld、组件、空间索引、规则、动作裁决、提交、Snapshot、WorldDelta
  |
  | WorldSnapshot / WorldDelta
  v
Client / Unity
  应用权威结果，播放表现，必要时软校正
```

一句话：

```text
Fantasy 是服务器外壳；
Shared/DG.GameCore 是权威规则世界；
Unity 是客户端镜像和表现；
WorldDelta 是权威状态同步协议。
```

## 分层边界

### Fantasy Server

Fantasy 负责服务端工程外壳：

```text
进程启动
Scene 生命周期
Session 连接
Handler 消息入口
FTask 异步流程
observer 管理
WorldDelta 广播
```

Fantasy 不负责表达格子世界实体，也不负责持有玩法规则真相。

正确关系是：

```text
Fantasy Scene
  owns AuthoritativeWorldTickRunner

AuthoritativeWorldTickRunner
  owns / accesses GameWorld

GameWorld
  owns DG rule state
```

不要把 Fantasy Entity 当成 DG 的格子实体。格子实体继续用 `GameWorld` 的 `EntityId` 表达。

### Shared/DG.GameCore

`Shared/DG.GameCore` 是权威规则核心，必须保持纯 C#。

它负责：

```text
EntityId
ComponentStore
SpatialEntityIndex
RuntimeEffectStore
WorldActionQueue
ActionSpecRegistry
ActionArbiter
PushVectorArbiter
RulePlanner
CommitResolver
ConflictResolver
Snapshot
WorldDelta
```

它不依赖：

```text
UnityEngine
Unity.Entities
Unity.Collections
Burst
Jobs
Fantasy.Entity
Fantasy.Scene
Fantasy.Session
```

服务端和客户端可以同时引用 `Shared/DG.GameCore`，但权威执行只发生在服务端路径。客户端引用它，是为了镜像、工具、测试和类型复用。

### Unity Client

Unity 客户端负责：

```text
连接 Fantasy 服务端
提交输入和 debug 请求
接收 WorldSnapshot / WorldDelta
维护 ClientMapWorld 镜像
驱动 ClientWorldVisuals / ClientAnimationLayer
提供本地即时反馈
提供调试和编辑器工具
```

客户端可以做表现预测和软校正，但不能裁决权威规则。

客户端不能决定：

```text
最终坐标
工厂机器最终状态
push / blocked / handoff / commit 结果
多人冲突结果
WorldDelta 内容
```

`ClientMapWorld` 的定位是镜像容器，不是第二套权威世界。

### Luban Config

Luban 是配置编译层，不是规则执行层。

数据流是：

```text
策划表 / schema
  -> Luban generated C# / json
  -> LubanGameConfigProvider
  -> Shared GameCore typed config
  -> runtime ActionSpec / ComponentApplication / policy
```

配置层可以保留可读字符串。进入规则运行时后，必须收口为：

```text
ActionSpecId
BlockedResultPolicyId
WorldTag
ComponentKind
ActionPrimitive
ActionSubjectKind
ActionCommitRule
```

规则层不通过普通字符串选择策略。字符串只允许出现在配置源、导入解析、日志、错误信息、调试展示和测试 authoring 边界。

## 当前存储定位

当前 `GameWorld` 是 ECS 语义，但不是 archetype/chunk ECS。

当前真实结构是：

```text
GameWorld
  Dictionary<long, GameEntity>
  Dictionary<Type, IComponentStore>

ComponentStore<T>
  Dictionary<long, TComponent>

SpatialEntityIndex
  按格子和 target 索引 entityId
```

因此当前状态应表述为：

```text
规则和同步架构是服务端权威。
GameCore 是字典 ECS。
GameWorld 已开始收口查询门面。
底层存储尚未迁移到 archetype/chunk。
```

不要把当前 Phase 0 误读成完整 ECS 存储迁移。

## ECS 演进原则

DG 可以演进成自研 ECS 或纯 C# archetype 存储，但触发条件必须来自真实压力，而不是架构洁癖。

迁移目标只解决这些问题：

```text
减少全表扫描
减少每 tick 排序和数组分配
减少 Dictionary 组件访问成本
显式化 query 语义
批量提交结构变化
让 Snapshot / WorldDelta 不暴露内部存储布局
```

空间 chunk 和 ECS chunk 必须分开：

```text
Spatial chunk
  地图坐标分块
  用于可见性、邻域查询、加载和调试

ECS chunk / archetype table
  组件布局分组
  用于系统迭代和连续存储
```

两者可以通过 `EntityId` / location 关联，但不能混成同一个模型。

## 近期方向

当前阶段只做边界收口：

```text
保留字典存储
收口 GameWorld 查询 API
显式区分有序和无序枚举
减少热路径全表扫描
规则运行时使用 typed ID / enum / policy data
清理 legacy pending 链
统一 deferred action 路径
保持 Snapshot / WorldDelta 语义稳定
补齐测试和手动验证口径
```

当前阶段不做：

```text
引入 Unity DOTS 到 Shared
把服务端改成 Unity Headless
把 Fantasy Entity 当成玩法实体
维护客户端和服务端两套规则实现
立刻替换完整 archetype/chunk 存储
让客户端承担权威裁决
```

## 何时进入真正存储迁移

至少满足以下条件中的两条，再启动完整存储迁移：

```text
单 world active entities 稳定达到 2000+
服务端 tick profile 显示 query / ComponentStore / full scan 是主要瓶颈
组件种类达到 30+，且多个系统频繁组合查询
多个系统开始各自维护临时 subset 缓存来避开全表扫描
push / blocked / handoff / cost / commit 规则已经稳定
Snapshot / WorldDelta 协议不再频繁变动
```

不满足这些条件时，继续使用：

```text
字典 ECS
GameWorld 查询门面
必要的 subset index
profile 驱动的小步优化
```

## Unity DOTS 边界

`Shared/DG.GameCore` 不引入 Unity DOTS。

原因是：

```text
Shared 必须被 Fantasy .NET 服务端加载。
DOTS 依赖 Unity runtime。
把规则核心写成 DOTS 会迫使服务端改成 Unity Headless，或维护两套规则实现。
这两条都破坏当前架构目标。
```

Unity DOTS 只能作为客户端表现层的独立选择：

```text
WorldDeltaToDotsBridge
  把服务端 delta 应用到客户端 DOTS World

DOTS World
  只做渲染、插值、表现预测
  不跑权威规则
```

是否引入客户端 DOTS，只由客户端渲染 profile 决定，不影响服务端权威架构。

## 网络同步模型

DG 不走全世界 rollback，也不走 lockstep。

目标模型是：

```text
客户端提交输入
服务端收集输入
服务端推进 GameWorld
服务端裁决规则和冲突
服务端产出 WorldDelta
所有观察者应用同一份 WorldDelta
客户端做表现确认或软校正
```

工厂机器、机关链、多人冲突和最终坐标都以服务端结果为准。

客户端即时反馈是手感层，不是权威层。

## 验证口径

涉及 `GameWorld`、权威规则、同步协议或存储边界的变更，必须覆盖：

```text
Unity TestFramework EditMode
Server/Tests/AuthoritativeMoveVerification
OpenSpec validate
用户手动 Play Mode / 双客户端端到端验证
```

其中：

```text
OpenSpec validate 只证明规格格式和变更结构有效。
EditMode 和 server verification 证明自动化语义。
Play Mode / 双客户端验证由用户确认端到端表现和同步一致性。
```

不能用单项结果替代整体验证。

## 最终判断

当前正在做的服务端权威和 `Shared/DG.GameCore` 边界收口是必要的。

必要的是：

```text
权威世界在服务端
规则核心在 Shared
客户端只做镜像和表现
配置进入运行时前完成 ID 化
GameWorld 查询边界先稳定
Snapshot / WorldDelta 保持权威同步协议
```

暂时不必要的是完整存储换代。

最终目标保持不变：

```text
DG 的权威世界属于 Shared/DG.GameCore。
它由 Fantasy 服务端托管和推进。
它由 Unity 客户端镜像和表现。
它未来可以演进成纯 C# 自研 ECS。
它不引入 Unity DOTS 到 Shared，也不绑定 Fantasy Entity。
所有最终状态通过服务端 WorldDelta 收敛。
```
