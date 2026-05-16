# 设计：服务端权威拍点输入层

## Context
现有链路是 `C2G_MoveRequestHandler -> AuthoritativeInputQueue.EnqueueMove -> AuthoritativeWorldTickRunner.Tick -> ActionQueue.EnqueuePlayerMove -> StateDrivenRuleExecutionSystem -> WorldDelta`。这个链路已经把行为裁决放在服务端权威 action pipeline 中，问题在输入层仍是 FIFO 移动请求：同一实体一拍多输入会进入多个 action，客户端还用目标格表达普通移动。

本项目目标是大世界服务端权威同步，接近 Minecraft 式最终状态收敛，但玩家输入需要节奏地牢式手感。输入层必须支持本地即时反馈和后续局部表现修正，同时不能让客户端成为规则真相源，也不能回滚万级实体大世界。

## Goals
- 玩家输入以 beat 为边界表达最终意图。
- 同一实体同一拍最多一个普通玩家输入进入行为系统。
- 服务端使用权威世界当前位置把方向意图转换为目标格移动 action。
- 被替换、过期、非法、已消费输入都有明确结果，不留下等待中的 RPC 或本地状态。
- 客户端可以立即播放非权威反馈，但 `ClientMapWorld` 仍只镜像服务端 snapshot/delta。
- 保持现有 action / arbitration / commit / WorldDelta 管线为唯一世界裁决路径。

## Non-Goals
- 不做完整 rollback/replay。
- 不保存全世界历史快照。
- 不实现工厂、机关、connected body 的客户端预测裁决。
- 不在本变更中引入 AOI、分区同步或 chunk streaming。
- 不把服务端 tick 改成高频帧同步。
- 不把输入确认和移动结算彻底拆成最终网络形态；可在兼容路径中返回结算结果。

## Architecture

### Input model
普通玩家输入表达为：

```text
clientInputId
entityId
beatTick
direction
clientTick
```

`direction` 是玩家意图，`beatTick` 是服务端消费边界。普通玩家输入不再以客户端目标格作为正式语义。调试拖拽仍使用目标格，因为它是编辑工具语义，不是玩家移动。

### Server buffer
服务端新增拍点输入缓冲，内部使用：

```text
beatTick + entityId -> buffered player input
```

提交规则：
- `beatTick < currentTick` 的输入进入过期/拒绝结果。
- `beatTick == currentTick` 或未来拍输入可缓存。
- 同一 key 已存在时，后输入覆盖前输入。
- 被覆盖输入立即完成为 `Replaced`。
- `Drain(beatTick)` 只返回该拍的最终输入，并移除对应 key。
- drain 输出按 `beatTick, entityId, submitSequence` 稳定排序。

### Tick integration
`AuthoritativeWorldTickRunner.Tick` 推进 `serverTick` 后，只消费当前拍输入。每个输入通过服务端权威当前位置和 direction 推导目标格，再调用现有 `ActionQueue.EnqueuePlayerMove` 或后续等价入口。

行为裁决仍由 `StateDrivenRuleExecutionSystem` 完成。输入层不得判断阻挡、推动、机关、connected body 或 runtime effect 结果。

### Compatibility path
落地时可以短期保留 `C2G_MoveRequest` 兼容入口，但它必须被适配为拍点输入，不能继续让 FIFO 多移动请求直接进入 action queue。正式普通玩家输入协议应迁移到方向/意图提交。

兼容期间的目标格字段只用于推导方向或校验相邻目标；服务端最终目标格仍必须来自权威位置加方向。

### Client boundary
客户端输入层负责：
- 根据按键生成 direction 和本地输入序号。
- 提交普通玩家输入意图。
- 记录待确认输入和非权威表现反馈。

客户端不得：
- 因普通输入直接修改 `ClientMapWorld.CoreWorld` 的权威坐标。
- 本地裁决推动、阻挡、机关链或最终坐标。
- 在服务端权威模式下把普通输入失败 fallback 到本地规则。

### Result sync
最终世界状态仍通过 `G2C_WorldSnapshotNotify` 和 `G2C_WorldDeltaNotify` 收敛。输入响应只用于说明输入状态或兼容返回本次结算结果，不替代 WorldDelta。

## Risks / Trade-offs
- 兼容旧 `C2G_MoveRequest` 会让协议语义短期不够干净；通过明确“目标格只适配成方向”降低风险。
- 如果客户端和服务端 beat 估计不同，输入可能落入相邻拍；本变更先以服务端 tick 为准，后续再做独立 InputAck 和时钟校准。
- 后输入覆盖前输入手感宽容，但会丢弃窗口内早按方向；这是节奏输入的预期行为，需要测试和手动验证确认。

## Migration Plan
1. 新增输入状态和拍点输入缓冲，并用测试固定替换、过期、消费语义。
2. 改造服务端 tick runner，从 drain FIFO move 改为 drain 当前拍最终输入。
3. 增加方向意图协议或兼容适配层，让普通输入不再依赖客户端目标格裁决。
4. 调整客户端 submitter，使普通玩家输入提交方向意图并保留非权威反馈边界。
5. 保留调试建造、调试拖拽、调试删除路径不受普通输入缓冲影响。

## Open Questions
- beatTick 由客户端显式提交，还是第一步由服务端收到时间归属到下一拍？建议本变更支持显式字段，并允许服务端在字段为 0 时归属到下一可消费拍。
- 旧 `C2G_MoveRequest` 是否需要保留到所有调试 UI 改完？建议保留兼容入口，但不作为新输入层主路径。
