# GameWorld 数据结构收口下一步

## 当前判断

现在不要把目标写成“自研 ECS”或“直接接 Arch”。当前最需要解决的是 `GameWorld` 内部数据结构和访问边界不够清楚。

当前事实是：

```text
Fantasy 负责服务端外壳
GameWorld 是权威世界入口
Shared/DG.GameCore 承载规则和最终状态
Unity 客户端只镜像和表现
```

`GameWorld` 现在同时承载实体、组件、空间索引、dirty、snapshot、delta、规则访问入口和诊断入口。继续扩展前，必须先把这些职责切清楚。

## 现在要做什么

当前 OpenSpec change `refactor-authoritative-world-data-oriented-storage` 的目标是：

```text
收口 GameWorld 数据结构
稳定 GameWorld public API
隐藏内部 storage / pool / mask / cache
减少 hot path 全实体扫描
让 dirty / delta 来源更集中
保持协议和规则语义不变
```

它不是：

```text
造一个通用 ECS 框架
引入 Unity DOTS
直接把规则层改成 Arch
重写 ActionSpec / push / connected body / runtime effect
改变 WorldDelta 协议
```

## 为什么先做数据结构收口

如果不先收口，直接接 Arch，会出现两套真相源：

```text
一部分状态在旧 GameWorld / spatial / dirty
一部分状态在 Arch World
规则层不知道该读哪边
WorldDelta 仍然要手动从 DG 状态拼
runtime effect final component 仍然要自己合并
session ownership 仍然不属于 Arch
```

Arch 能解决的是实体、组件、查询和 chunk iteration。它不能直接解决 DG 自己的权威语义：

```text
稳定 EntityId 和协议 id 映射
GridCoord 空间索引
push / blocked / connected body
runtime effect -> final component
dirty journal
WorldDelta
JoinWorld snapshot
Fantasy session ownership
客户端 mirror 收敛
```

因此现在要先让外部只依赖 `GameWorld` 边界。这样未来内部 storage 才能替换。

## 目标结构

近期目标结构：

```text
GameWorld
  public facade
  AddEntity / RemoveEntity
  SetComponent / TryGetComponent
  QueryEntities
  GetEntitiesAt
  FlushDelta / CreateSnapshot

WorldDataStorage
  EntityRegistry
  ComponentTypeRegistry
  ComponentPool<T>
  ComponentMask
  QueryCache

EntityLocationStore
  entity id -> coord / target / spatial presence

SpatialEntityIndex
  coord / target -> entity ids

DirtyWorldJournal
  changed entities
  removed entities
  animation metadata
  later: touched entities / changed cells if needed
```

规则层只能通过 `GameWorld` public API 读写最终状态，不保存内部 row、pool、mask、cache。

## 仍未完全收口的地方

### Query 结果还是 GameEntity

当前 `QueryEntities` 返回 `IReadOnlyList<GameEntity>`。调用方仍然会继续 `TryGetComponent`。

下一步可以优先补窄查询结果：

```text
AutoMove query result:
  entity id
  position
  direction
  auto move

PushOnEnter query result:
  entity id
  position
  direction
  push-on-enter config
```

不要一次做通用 query view。先做真实 hot path。

### dirty 还分散

当前 dirty journal 主要管：

```text
changed entities
removed entities
animation metadata
```

空间 dirty 仍在 `SpatialDirtyTracker`，tick touched diagnostics 仍在 `AuthoritativeWorldTickRunner` 临时集合里。

下一步要明确：

```text
哪些 SetComponent 自动 dirty
哪些写入需要手动 MarkDirty
changed cells/chunks 是否进入 DirtyWorldJournal
touched entities 是否进入统一 observation / journal
```

### 空间查询还是宽接口

当前 `GetEntitiesAt(coord)` 返回实体列表，然后规则层再过滤组件。

可以补更窄的空间查询：

```text
TryGetFirstBlockingAt(coord, excludedBody)
GetColliderEntitiesAt(coord, target)
GetPushableEntitiesAt(coord)
```

但必须按真实 hot path 添加，不要提前铺一整套空间 query framework。

### 规则层临时集合不是首要问题

规则层中的 `Dictionary` / `HashSet` 很多是单次 action / tick 的临时裁决状态，不是权威组件主存储。

这些可以保留：

```text
action result map
moved entity set
occupied target set
connected body visited set
push composition grouping
session ownership map
observer map
config lookup map
```

本阶段只清理会长期持有最终状态或导致热路径全实体扫描的结构。

## Arch 的位置

Arch 可以作为未来候选，但不应该直接暴露给规则层。

如果未来接 Arch，正确方式是：

```text
GameWorld
  IWorldDataStorage
    IndexedWorldDataStorage
    ArchWorldDataStorage
```

规则层不直接使用：

```text
Arch.World
Arch.Entity
Arch.Query
```

Arch 只作为 `GameWorld` 内部 storage adapter。这样即使以后换回自研 storage，规则层和协议层也不需要重写。

## 何时评估 Arch

满足以下条件再开独立提案评估 Arch：

```text
active entities 稳定达到 2000+
profile 显示 query / component lookup / storage iteration 是主要瓶颈
多个 hot path 系统依赖同类组件组合查询
当前 WorldDataStorage 的查询和写入已经成为维护负担
WorldDelta / runtime effect / push / connected body 语义已经稳定
```

评估 Arch 时只做小切片：

```text
EntityId <-> Arch entity 映射
SetComponent / TryGetComponent
Position + Direction + AutoMove query
dirty journal 仍由 DG 控制
WorldDelta 仍由 DG 构造
server verification 必须通过
Unity EditMode 必须通过
```

如果这个切片不能证明收益，就不继续接。

## 下一步执行顺序

1. 完成 `refactor-authoritative-world-data-oriented-storage`。
2. 保持 `GameWorld` public API 稳定。
3. 把 auto move 和 push-on-enter 改成窄 query result，减少 query 后重复 `TryGetComponent`。
4. 明确 `SetComponent` 与 dirty 的责任边界。
5. 补窄空间查询，只覆盖 blocking / collider / pushable 的真实 hot path。
6. 将 touched diagnostics 从 runner 临时集合逐步收口到统一 observation / journal。
7. 等 profile 证明需要，再开独立 change 评估 Arch adapter。

## 停止条件

如果本阶段开始出现以下行为，说明方向又偏了：

```text
为所有组件生成通用 query view
引入 system scheduler
引入 archetype chunk 抽象
让规则层直接使用第三方 ECS 类型
把空间 chunk 和 ECS chunk 混成一个概念
为了消灭所有 Dictionary 重写临时裁决逻辑
```

出现这些情况时，应该停止并回到目标：

```text
数据结构收口
GameWorld 边界稳定
规则语义不变
协议语义不变
测试和手动验证可确认
```
