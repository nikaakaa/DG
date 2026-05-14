## Context

当前事实：

- `GameWorld`  owns `Dictionary<long, GameEntity>`、`Dictionary<Type, IComponentStore>`、`ChunkStore`、`SpatialEntityIndex`、`SpatialDirtyTracker`、dirty/delta 和 runtime effect 解析入口。
- `ComponentStore<T>` 使用 `Dictionary<long, TComponent>`。
- `SpatialEntityIndex` 已经按 cell/chunk/target 建立空间索引，但查询返回仍有 `ToArray` / 临时集合。
- `ActionSpecId` 和 `BlockedResultPolicyId` 已经是 strong struct，但内部值仍是 string。
- `WorldTag` 在规则组件层已经是 flags enum，`GameEntity.Tags` 和 Luban archetype tags 仍是字符串集合。
- Luban 表使用 `spec_id`、`policy_id`、`output_spec_id` 等字符串字段作为可读配置来源。

目标不是立即把所有字典删除，而是把运行时合同改成可连续数组 / archetype 存储可以承接的形态。

## Goals / Non-Goals

Goals:

- 保持 Shared GameCore 纯 C#、netstandard2.1、Fantasy 服务端与 Unity 客户端共用。
- 建立运行时 ID 合同：规则热路径不比较字符串 spec/policy/tag。
- 建立 ECS 存储抽象：外部只依赖 `GameWorld` / query API，不依赖字典返回顺序、拷贝数组或内部 store 类型。
- 先建立观测和回归测试，再替换内部存储。
- 保留空间 chunk/cell 索引，并让它通过 entity id / entity location 对接未来 archetype 存储。

Non-Goals:

- 不引入 Unity DOTS / Entities / Jobs / Burst。
- 不把服务端改成 Unity Headless Server。
- 不一次性全量重写规则层、snapshot/delta、空间索引和表现层。
- 不把配置层可读字符串全部删除。
- 不新增脚本语言、行为树、VM 或完整 GameplayTag 系统。

## Decisions

### Decision: ID 化分两层

配置层可以继续使用可读字符串，例如 `player_move`、`mechanism_push`、`push_or_block`。导入层必须把这些字符串解析成稳定运行时 ID。

运行时 ID 可以先用强类型 struct 包装数值和 debug name，后续再根据 Luban 生成方式决定是否直接由 Excel 提供 int id。规则热路径只消费 ID。

```text
配置层:
  spec_id/name = "mechanism_push"
  policy_id/name = "push_or_block"

导入层:
  name -> ActionSpecRuntimeId
  name -> BlockedResultPolicyRuntimeId
  string tag -> WorldTag

运行时:
  ActionRequest.SpecId = runtime id
  ActionSpec.RegistryKey = runtime id
  BlockedResultPolicy.PolicyId = runtime id
  TagSetComponent = WorldTag bitmask
```

### Decision: 字符串不是策略输入

字符串允许用于：

- Luban 源数据和 generated table 原始字段
- provider / registry 构建时的解析
- 错误信息
- debug 日志
- editor / sandbox / test authoring
- 客户端表现 style 映射

字符串不得用于：

- action arbitration
- pending / handoff / deferred output
- rule planning
- commit / conflict
- component result resolution
- server authoritative tick strategy selection

### Decision: ECS 存储先抽象后替换

第一阶段不直接引入 archetype table，而是建立可迁移合同：

```text
EntityId -> EntityLocation
Component type/kind -> store metadata
Query descriptor -> iterable view
Subset index -> hot path candidates
Spatial index -> entity ids / locations
Dirty tracker -> entity/cell/chunk changes
```

等这些 API 被规则层使用并有观测数据后，再把 `ComponentStore<T>` 从字典替换为连续存储。

### Decision: 空间 chunk 不等于 ECS chunk

空间 chunk 仍是 32x32 cell 的地图划分。ECS chunk 是同 archetype 连续存储块。两者不合并。

```text
SpatialEntityIndex:
  cell/chunk key -> entity id list

ECS storage:
  entity id -> archetype chunk + row
  archetype chunk -> component arrays
```

空间查询先减少候选实体，组件查询再通过 entity location 获取数据。

### Decision: 热路径先做可观测性

本变更实施阶段需要记录：

- `EnumerateEntities` 调用次数、排序/拷贝次数
- `TryGetComponent` / `HasComponent` 命中次数
- `GetEntitiesAt` / `GetEntitiesAround` / `TryGetChunkEntities` 分配和调用次数
- 自动移动、push 传播、connected body、snapshot/delta 的 tick 成本

没有观测数据前，不把任意单点改动描述成“已经完成 ECS 化”。

### Decision: 测试断言语义，不断言内部布局

测试应断言：

- 实体拥有组件
- 行为结果、坐标、dirty/delta、WorldDelta 一致
- 两个不同配置名字但同一 ID/policy 数据时行为一致
- 热路径不新增字符串策略分支

测试不应断言：

- 某个 archetype chunk 的内部数量
- 某个内部数组下标
- 字典 key 顺序

## Phasing

### Phase 0: 运行时 ID 合同和 API 边界

- 增加 ID 解析/注册边界。
- 扫描并限制规则层字符串 spec/policy/tag 比较。
- 建立 query / subset index / entity location façade。
- 加入观测点。

### Phase 1: 热路径 subset index

- 为 AutoMove、Position+Collider、PushOnEnter、PortConnector 等高频组合提供索引或 query view。
- 减少全表扫、排序、数组拷贝。

### Phase 2: 连续存储试点

- 选择一个低风险组件组作为试点。
- 保持 `GameWorld` API 不变。
- 验证 snapshot/delta、规则测试和服务器验证一致。

### Phase 3: archetype table / chunk migration

- 引入 component set / archetype key。
- add/remove component 迁移 entity location。
- Query 由 component set 匹配到连续存储。

## Risks / Trade-offs

- 风险：过早做完整 archetype 会放大当前 active change 的 rebase 成本。
  - Mitigation：先做 ID 合同、观测和 API 边界。
- 风险：配置字符串转 ID 会降低可读性。
  - Mitigation：配置层保留 alias/name，运行时 ID 保留 debug name。
- 风险：扫描字符串分支会误报表现层和测试。
  - Mitigation：规则层、配置导入层、表现层分别设置允许清单。
- 风险：空间索引和 archetype 存储联动复杂。
  - Mitigation：空间索引继续以 entity id 为边界，entity location 由存储层解析。

## Verification Strategy

- `openspec validate refactor-gamecore-ecs-storage-and-id-policy --strict --no-interactive`
- Unity TestFramework EditMode:
  - ID 解析等价性
  - 同 policy 不同配置名行为等价
  - 规则层无新增字符串策略分支
  - 查询 API 语义保持
  - dirty/snapshot/delta 语义保持
- Shared/server:
  - `dotnet build Shared/DG.GameCore/DG.GameCore.csproj --no-restore`
  - `dotnet run --project Server/Tests/AuthoritativeMoveVerification/AuthoritativeMoveVerification.csproj --no-restore`
- 手动端到端:
  - 用户在 Unity Play Mode + 服务端 + 双客户端验证 WorldDelta 一致。
