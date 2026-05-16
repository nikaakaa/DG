## Context
当前同步模型是服务端权威 `GameWorld` 产生 `WorldDelta`，通过 `G2C_WorldDeltaNotify` 广播给观察者。客户端用 `WorldDelta` 更新 `ClientMapWorld`，再用 `WorldDeltaAnimationMetadata` 与前后快照生成动画事件。

问题不在网络模型，而在表现语义边界：服务端 tick runner 直接根据普通 action 名称推断动画类型和 style key，客户端又维护一套 motion enum 和 fallback style，导致表现扩展和玩法行为耦合。

## Goals
- 保留服务端权威和 `WorldDelta` 收敛模型。
- 表现事件由服务端权威生成，但不改变世界状态。
- 表现事件使用可扩展字符串 ID，不使用表现枚举。
- 表现配置数据驱动，普通 action 新增表现不修改 tick runner。
- 客户端表现层只消费表现事件和本地 cue 配置。

## Non-Goals
- 不做客户端预测、回滚、状态历史、输入重放。
- 不引入独立表现消息通道。
- 不要求一次性迁移所有视觉实现到最终美术系统。

## Architecture

```text
ActionQueue / RuleExecutionSystem
  -> StateDrivenRuleExecutionResult
  -> PresentationEventComposer
  -> GameWorld.AddPresentationEvent
  -> WorldDelta.PresentationEvents
  -> G2C_WorldDeltaNotify.PresentationEvents
  -> ClientPresentationLayer
  -> ClientWorldVisuals / cue players
```

## Data Shape

`PresentationEvent` 使用固定通用字段：

- `eventId`
- `serverTick`
- `kindId`
- `cueId`
- `causeActionId`
- `clientInputId`
- `sourceEntityId`
- `subjectEntityIds`
- `fromX`
- `fromY`
- `toX`
- `toY`
- `direction`
- `payloadJson`

`kindId` 表达领域表现语义，例如 `entity.moved`、`entity.pushed`、`pivot.rotated`、`entity.spawned`、`entity.removed`。

`cueId` 表达客户端表现入口，例如 `move.player`、`push.default`、`pivot.rotate`、`pivot.bounce`。

`payloadJson` 只承载表现专属参数，例如旋转 pivot、旋转方向、impact 坐标、bounce 标记。能用通用字段表达的内容不得放入 payload；影响权威世界状态的内容不得放入 payload。

## Compatibility
实现阶段可以保留兼容适配，将旧 `WorldDeltaAnimationMetadata` 迁移到 `PresentationEvent`。最终权威新路径不再新增 `WorldDeltaMotionKind` 或客户端 `ClientAnimationMotionKind` 成员。

## Testing
- Unity TestFramework EditMode 覆盖客户端表现事件入队、payload 解析、快照状态和表现事件分离。
- 服务端验证覆盖 player move、mechanism push、rotate pivot、blocked bounce 生成稳定表现事件。
- 手动端到端验证两个客户端收到同一 `WorldDelta` 后世界状态一致，表现 cue 一致。
