# WorldDelta 与客户端镜像子目标

## 定位

本子文档负责定义 runtime effect 如何同步给 Unity 客户端，以及 `ClientMapWorld` 如何镜像这些状态。

客户端只镜像和显示，不做权威裁决。

## 目标

WorldDelta 层必须回答：

```text
哪些 effect 变化需要同步
effect delta 数据结构包含哪些字段
tag/component 变化如何与 effect 变化对应
ClientMapWorld 如何保存镜像
Debug 面板如何展示 effect
显示层如何读取 effect/tag/component
```

## Delta 类型

运行时效果相关 delta：

```text
AddedEffects
RefreshedEffects
RemovedEffects
ChangedTags
ChangedComponents
```

`AddedEffects` 表示服务端提交了新实例。

`RefreshedEffects` 表示已有实例被刷新或 stack count 改变。

`RemovedEffects` 表示实例过期或被移除。

`ChangedTags` 表示 effect contribution 导致最终 tag 状态变化。

`ChangedComponents` 表示 runtime component contribution 变化。

## Effect delta 字段

基础字段：

```text
RuntimeEffectId
EffectKind
SourceEntityId
TargetEntityId
CreatedTick
ExpireTick
StackCount
ContributedTags
RuntimeComponents
Reason
```

客户端显示不需要拿到完整服务端 payload。payload 是否同步由具体 effect 的表现需求决定。

## ClientMapWorld mirror

`ClientMapWorld` 需要保存 effect mirror。

职责：

```text
按 entity 查询 active effects
按 effect id 查询 active effect
应用 added/refreshed/removed effect delta
应用 tag/component delta
提供 Debug 面板读取接口
提供显示层读取接口
```

禁止：

```text
本地新增权威 effect
本地刷新权威 effect
本地移除权威 effect
本地根据 effect 改变权威坐标
```

## Debug 面板

Debug 面板显示：

```text
entity id
effect kind
source entity id
created tick
expire tick
stack count
contributed tags
runtime components
mirror/server 标识
```

Debug 面板发起修改时必须走服务端 Debug RPC 或 action queue。

## 显示层

显示层可以读取：

```text
effect kind
tag state
runtime component state
remaining tick
source reason
```

显示层不能写：

```text
GameWorld 权威状态
ClientMapWorld 权威 mirror 以外的数据
WorldActionQueue
RuntimeEffectStore
```

## 与网络同步的关系

WorldDelta 只定义状态变化。

AOI、兴趣管理、按客户端过滤、不在本子文档中定义。后续网络同步文档决定哪些客户端收到 effect delta。

## 与其他子文档的关系

```text
runtime-effect-model.md
  定义 RuntimeEffectInstance 与 dirty changes。

runtime-effect-rules-integration.md
  定义 commit 后何时 flush delta。

runtime-effect-verification.md
  验证客户端只镜像 effect delta。
```
