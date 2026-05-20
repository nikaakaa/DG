# 行为层模块地图

## Shared GameCore

Shared GameCore 是小 GAS 的核心实现位置，必须保持纯 C#，不依赖 UnityEngine，不依赖 Fantasy 协议类型。

### ActionRuntime

| 目录 | 职责 |
|---|---|
| `ActionRuntime/Specs` | `ActionSpecId`、`ActionSpec`、policy model、registry。 |
| `ActionRuntime/InputIntent` | 输入意图模型。 |
| `ActionRuntime/Requests` | `WorldAction` 到 `ActionRequest` 的运行时请求模型和适配。 |
| `ActionRuntime/Queue` | `WorldActionQueue`、`DeferredAction`、push origin context、ready tick draining。 |
| `ActionRuntime/Targeting` | 目标选择器、target data、target filter、component fact query。 |
| `ActionRuntime/Gating` | tag/component/condition gate。 |
| `ActionRuntime/Subjects` | action subject 解析，包含 connected body 语义入口。 |
| `ActionRuntime/Strategies` | `IActionStrategy`、strategy context、注册、生成、具体策略。 |
| `ActionRuntime/Claims` | action claim、claim arbitration、push vector composition、rotate response 当前实现。 |
| `ActionRuntime/Blocking` | blocking contact、blocked policy matching、derive/bounce/reject/noop outcome。 |
| `ActionRuntime/Planning` | move plan、connected body、port graph、body capability。 |
| `ActionRuntime/Commit` | commit proposal、commit resolver、commit handler。 |
| `ActionRuntime/Lifecycle` | action unit lifecycle state 和 transition。 |
| `ActionRuntime/Execution` | tick 级行为编排和 timed unit 调度。 |
| `ActionRuntime/Generated` | 显式生成的 strategy registration。 |

### Domain / World / Configuration

| 目录 | 职责 |
|---|---|
| `Domain/Components` | final component 数据结构。 |
| `Domain/Entities` | entity 身份和基础模型。 |
| `Domain/ValueObjects` | direction、coord 等稳定值对象。 |
| `World` | `GameWorld` 公开入口。 |
| `World/Storage` | entity/component storage adapter。 |
| `World/Spatial` | cell、chunk、空间索引、dirty tracking。 |
| `World/Snapshots` | snapshot / delta projector / applier。 |
| `Configuration` | GameCore 稳定配置模型、provider、fallback provider。 |
| `Configuration/Luban` | Luban adapter 和加载边界。 |
| `Configuration/Generated/LubanTables` | Luban 生成代码，不手改。 |

### RuntimeEffects

| 目录 | 职责 |
|---|---|
| `RuntimeEffects/Specs` | effect id、spec、kind。 |
| `RuntimeEffects/Store` | runtime effect instance 和 store。 |
| `RuntimeEffects/Settlement` | source contribution、component/tag settlement、result resolver。 |

RuntimeEffect 的结果应进入 final component/tag fact，再被行为层查询。不要让 effect 直接绕过 action/commit 改最终行为结果。

## Server Hotfix

Fantasy 服务端是权威外壳，不是规则层。

| 目录 | 职责 |
|---|---|
| `Server/Hotfix/AuthoritativeMove/Handlers` | C2G/G2C Handler，做协议校验、调用服务、reply。 |
| `Application` | 权威应用服务、player/session/entity 管理。 |
| `Runtime` | server tick、输入队列、runner、deferred enqueue。 |
| `Sync` | snapshot/delta、observer、broadcast。 |
| `Debugging` | 权威 debug 编辑服务。 |
| `WorldBootstrap` | 服务端 world 和配置初始化。 |
| `ProtocolMapping` | Fantasy 协议类型和 GameCore 类型转换。 |
| `World` / `Infrastructure` / `Domain` | 服务端外壳的 world/provider/基础服务。 |

规则：

- Handler 不写 action 规则。
- Fantasy 协议类型不进入 Shared GameCore。
- 服务端 tick 只通过 Shared GameCore 入口推进权威世界。
- 同步层只广播权威 snapshot/delta 和表现事实。

## Unity ClientWorld

Unity 客户端负责输入、镜像和表现。

| 目录 | 职责 |
|---|---|
| `Bootstrap` | 客户端 world、配置、runtime 启动。 |
| `Networking` | Fantasy 连接、request、push handler、runtime。 |
| `Mirror` | 服务端 snapshot/delta 的客户端镜像。 |
| `Input` | Play Mode 输入和 intent 生成。 |
| `Presentation` | GameObject、动画、playback plan、group track、视觉刷新。 |
| `DebugTools` | runtime debug UI、结构块、布局工具。 |
| `EditorTools` | Unity Editor 菜单和窗口。 |
| `Runtime` / `Spatial` / `Interaction` | 客户端运行时辅助、空间辅助和交互接口。 |

规则：

- `ClientMapWorld` 是 Shared `GameWorld` 的客户端镜像适配。
- 客户端默认不裁决权威规则。
- 表现层播放不能写回镜像来影响规则。
- 新增 EditMode 测试按模块放到 `Assets/Tests/Editor/ClientWorld/<模块名>`。

## 配置与生成文件

不能手改：

```text
*.meta
Luban Generated/LubanTables
Fantasy 协议生成文件
source generator 生成注册文件
```

应修改来源：

```text
Luban 源表 / schema / adapter
协议源文件
strategy attribute / source generator 输入
Unity 自己刷新 .meta
```

## 新增能力放置规则

### 新增普通 action

优先顺序：

```text
ActionSpec 配置
IActionStrategy
TargetSelector
SubjectPolicy
BlockedOutcome
CommitHandler
ActionFact projection
Client playback
```

不要改：

```text
StateDrivenRuleExecutionSystem 中按 action 名字分支
GameWorld 中按玩法名字分支
Fantasy Handler 中直接写规则
ClientMapWorld 中裁决权威结果
```

### 新增 component

需要同时考虑：

```text
Domain/Components
ComponentKind
ComponentApplicationRegistry
ComponentFactQueryRegistry
snapshot projector/applier
runtime effect result resolver
Luban provider mapping
Unity TestFramework
```

### 新增 blocked 语义

放置：

```text
ActionRuntime/Blocking/Policies
ActionRuntime/Blocking/Outcomes
```

不要塞进具体 strategy。

### 新增 commit 语义

放置：

```text
ActionRuntime/Commit
ActionRuntime/Commit/Handlers
```

commit 只落地世界修改，不重新判断 action 业务语义。

### 新增表现事实

服务端：

```text
ActionFact
ActionFactProjection
PresentationFact
WorldDelta
ProtocolMapping
```

客户端：

```text
ClientPresentationFactTranslator
ClientPresentationPlaybackPlanner
PresentationPlaybackScheduler
Track runtime
ClientWorldVisuals
```

表现事实不能反向影响服务端行为裁决。

## 当前重点债务

### 行为实例抽象

`TimedActionUnit` 已经能跑，但目标主语应迁移为 `ActionBehaviorInstance`。

需要继续明确：

- active store。
- state machine。
- reservation store。
- ActionFact 输出。
- composer fallback 收窄。

### rotate sweep

当前 rotate 仍有固定 90 样本痕迹。

目标：

- `CostTicks` 直接切 90 度。
- 每 tick 是弧段。
- tick 弧段内收集全部 contacts。
- 后续 tick 不再扫描。

### presentation group

目标：

- group track 成为表现层一等对象。
- entity / connection ownership 统一管理。
- 普通动画不能覆盖 group member。
- 播放结束对齐 snapshot。

### 配置治理

目标：

- provider 隔离 Luban 类型。
- 跨表引用有测试。
- ActionSpec policy 不靠字符串硬编码。
- RuntimeEffect 与 static component source 可合成、可移除、可验证。

## 查找入口

常用代码入口：

```text
Shared/DG.GameCore/ActionRuntime/Specs/ActionSpecs.cs
Shared/DG.GameCore/ActionRuntime/Specs/ActionSpecRegistry.cs
Shared/DG.GameCore/ActionRuntime/Requests/ActionRequests.cs
Shared/DG.GameCore/ActionRuntime/Queue/WorldActions.cs
Shared/DG.GameCore/ActionRuntime/Strategies/Contracts/IActionStrategy.cs
Shared/DG.GameCore/ActionRuntime/Claims/ActionClaims.cs
Shared/DG.GameCore/ActionRuntime/Claims/RotatePivotResponseProcessor.cs
Shared/DG.GameCore/ActionRuntime/Execution/StateDrivenRuleExecutionSystem.cs
Server/Hotfix/AuthoritativeMove/Runtime/AuthoritativeWorldTickRunner.cs
Server/Hotfix/AuthoritativeMove/Sync/AuthoritativeWorldSyncSystem.cs
Client/DG_Client/Assets/Scripts/ClientWorld/Presentation
Client/DG_Client/Assets/Tests/Editor/ClientWorld
```

常用文档入口：

```text
openspec/project.md
docs/source-layout.md
docs/gamecore-extension-boundaries.md
docs/Goal/Acion/structure/README.md
docs/Goal/Acion/Logic/timed-action-unit-logic.md
docs/Goal/Acion/special/rotate/rotate-architecture.md
```
