# Change: 增加服务端权威拍点输入层

## Why
当前玩家移动输入直接作为目标格移动请求进入服务端 FIFO 队列，同一实体在同一服务端 tick 内可以产生多条移动 action。节奏地牢式输入需要先把玩家操作收口为“某一拍的最终意图”，再交给现有服务端权威行为系统裁决，否则一拍多输入、网络抖动和后续局部表现修正都会放大不同步风险。

## What Changes
- 增加服务端权威拍点输入缓冲语义，按 `entityId + beatTick` 保存玩家最终输入。
- 同一实体同一拍只允许一个最终玩家输入，默认后输入覆盖前输入，并给被覆盖输入明确结果。
- 服务端 tick 只消费当前拍输入，并把最终输入转换为现有 `WorldAction` / action pipeline，而不是新增一套行为裁决。
- 普通玩家输入从“客户端指定目标格”迁移为“方向/动作意图”，目标格由服务端根据权威当前位置推导。
- 客户端输入层只提交意图和播放非权威反馈，不修改 `ClientMapWorld` 权威镜像。
- 保留 `WorldDelta` 作为最终结果同步路径；本变更不实现完整预测、局部回滚、AOI 或独立 InputAck 网络阶段。

## Impact
- Affected specs: `authoritative-move-runner`, `client-world-runner`
- Affected code: `Tools/NetworkProtocol/Outer/OuterMessage.proto`, `Server/Hotfix/AuthoritativeMove/Runtime/AuthoritativeInputQueue.cs`, `Server/Hotfix/AuthoritativeMove/Runtime/AuthoritativeWorldTickRunner.cs`, `Server/Hotfix/AuthoritativeMove/Handlers`, `Client/DG_Client/Assets/Scripts/ClientWorld/Networking/Runtime/ClientMoveNetworkSubmitter.cs`
- Tests: Unity TestFramework EditMode 覆盖输入缓冲、替换、消费边界；服务端 authoritative verification 覆盖一拍多输入最终只产生一个玩家 movement action；手动双客户端验证最终 WorldDelta 收敛。
