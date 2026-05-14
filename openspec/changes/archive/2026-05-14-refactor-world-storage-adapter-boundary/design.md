## Context

当前 `GameWorld` 已经把大量实体和组件读写集中到 `WorldDataStorage`，并通过窄查询、dirty journal、空间查询和 observation 降低了规则层直接接触存储细节的程度。

但当前结构仍然是：

```text
GameWorld
  new WorldDataStorage()
```

这说明内部实现已经集中，但还不是可替换边界。如果现在直接接 Arch，会变成：

```text
GameWorld
  WorldDataStorage details
  Arch details
  SpatialEntityIndex
  DirtyWorldJournal
```

这样容易形成两套状态来源，也会让规则层开始关心第三方 ECS 的 entity、query、chunk 和生命周期。

## Goals

- 将 `GameWorld` 对具体 storage 的依赖改成内部 storage adapter 合同。
- 将当前 indexed typed-pool 实现命名和职责明确为默认 adapter。
- 维持 `GameWorld` public API 不变。
- 维持 `EntityId`、组件、空间、dirty、snapshot/delta 和 observation 语义不变。
- 让后续 Arch 评估只需要新增一个 adapter 小切片，而不是改规则层。
- 用测试证明 adapter 化前后行为一致。

## Non-Goals

- 不引入第三方 ECS。
- 不做 Arch adapter。
- 不做完整 archetype/chunk 存储。
- 不调整 action policy pipeline。
- 不调整 AutoMove self push 行为。
- 不把空间 chunk 和 ECS chunk 合并。
- 不让规则层缓存 storage row、pool、mask 或 query cache。

## Decisions

### Decision: Storage adapter 是 GameWorld 内部合同

`IWorldDataStorage` 或等价接口只存在于 Shared GameCore 内部边界。它服务于 `GameWorld`，不是给规则模块直接调用的新入口。

合同至少覆盖：

```text
EntityCount
AddEntity
RemoveEntity
TryGetEntity
EnumerateEntities
QueryEntities
HasComponent
TryGetComponent
GetComponent
SetComponent
RemoveComponent
```

空间索引、dirty journal、runtime effect、snapshot/delta 仍由 `GameWorld` 组合控制，不塞进 storage adapter。

### Decision: 当前实现改名为 indexed adapter

当前 `WorldDataStorage` 的实体注册表、组件 type id、dense pool、component mask 和 query cache 继续保留，但它应被视为默认 indexed storage adapter。

合理命名可以是：

```text
IndexedWorldDataStorage
```

或者保持文件名但让类型职责明确。实现阶段以最小改动和编译安全为准。

### Decision: Arch 只作为后续 adapter 候选

后续如果接 Arch，正确形状是：

```text
GameWorld
  IWorldDataStorage
    IndexedWorldDataStorage
    ArchWorldDataStorage
```

Arch 类型不得出现在：

```text
ActionArbiter
RulePlanning
CommitRules
BodyCapabilityResolver
StateDrivenRules
AuthoritativeWorldTickRunner
AuthoritativeWorldSyncSystem
ClientMapWorld
Outer protocol generated handlers
```

### Decision: Adapter 不拥有 DG 权威语义

storage adapter 只负责实体、组件和组件组合查询。以下职责仍属于 DG：

```text
stable EntityId contract
GridCoord spatial occupancy
changed cells/chunks
DirtyWorldJournal
WorldDelta
runtime effect final component resolution
connected body view
push/blocked/action arbitration
Fantasy session ownership
client mirror convergence
```

这样即使 storage 从 indexed pool 换成 Arch，DG 的权威语义也不会随第三方库移动。

### Decision: 先证明边界，再评估收益

本变更完成后，是否继续接 Arch 仍要另开 proposal。Arch 小切片必须至少证明：

```text
EntityId <-> Arch entity 映射
SetComponent / TryGetComponent
Position + Direction + AutoMove query
dirty journal 仍由 GameWorld 控制
WorldDelta 仍由 DG 构造
server verification 通过
Unity EditMode 通过
```

没有 profile 或 observation 证据时，不继续扩大到完整 Arch storage。

## Risks / Trade-offs

- 风险：为了接口化引入过宽 abstraction。
  - Mitigation：adapter 只覆盖当前 `GameWorld` 已经使用的 storage 操作。
- 风险：规则层绕过 `GameWorld` 使用 adapter。
  - Mitigation：adapter 类型保持 internal，测试或扫描禁止规则层引用。
- 风险：把 dirty/spatial 也塞进 storage，导致边界变浑。
  - Mitigation：明确 storage 只做实体/组件/query，dirty 和 spatial 继续由 `GameWorld` 控制。
- 风险：过早引入 Arch。
  - Mitigation：本变更 Non-Goals 禁止新增第三方 ECS 依赖。

## Sequencing

1. 完成当前 storage change 的自动化验证基线。
2. 引入 storage adapter 合同。
3. 将当前 storage 实现改为默认 indexed adapter。
4. 确认 `GameWorld` 构造和测试可注入默认 adapter 或等价边界。
5. 跑 Shared、Server、Unity EditMode 验证。
6. 用户手动 Play Mode + 双客户端验证。
7. 之后再决定是否开 Arch adapter 评估提案。
