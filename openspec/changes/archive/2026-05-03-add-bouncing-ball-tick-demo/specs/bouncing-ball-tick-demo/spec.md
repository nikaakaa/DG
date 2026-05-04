## ADDED Requirements
### Requirement: 服务端权威无限 Tick Demo World
系统 SHALL 提供一个服务端权威的 demo world，在 world 运行期间按固定 tick 间隔持续推进反弹球逻辑，并且不依赖客户端输入触发移动。

#### Scenario: 服务端持续推进 tick
- **WHEN** demo world 已启动
- **AND** 没有任何客户端发送移动请求
- **THEN** 服务端仍按固定间隔推进 demo tick
- **AND** 每次推进产生单调递增的 server tick 序号

#### Scenario: 防止重复 tick loop
- **WHEN** demo world 已经处于运行状态
- **AND** 外部入口再次请求启动 demo tick
- **THEN** 系统不会创建第二个并行 tick loop
- **AND** 后续 server tick 序号仍保持单调递增且不跳跃执行两次规则

#### Scenario: world 停止时结束 tick
- **WHEN** demo world 被关闭或释放
- **THEN** 服务端停止继续推进该 demo world 的 tick
- **AND** 不再向 observer 广播该 world 的新状态

### Requirement: 配置驱动的两实体反弹模型
系统 SHALL 从 demo 配置中实例化并维护两个服务端权威 entity：一个带位置、速度和可反弹能力的运动体实例，一个带边界范围能力的边界体实例。entity 本身 MUST NOT 依赖业务名字参与规则查询。

#### Scenario: 从配置初始化两个 demo entity
- **WHEN** demo world 首次启动
- **THEN** 服务端读取 demo 配置中的两个 entity instance 定义
- **AND** 服务端为每个 instance 创建唯一 entity id
- **AND** 服务端把配置声明的 component 和 tag 附到对应 entity 上
- **AND** 两个 entity 都拥有可用于网络同步的 entity id 和配置标识

#### Scenario: 球在边界内部移动
- **WHEN** 带可反弹能力的运动体下一步位置仍位于边界体范围内部
- **THEN** tick system 将该运动体移动到下一步位置
- **AND** 该运动体的速度方向保持不变

#### Scenario: 球触达边界后反弹
- **WHEN** 带可反弹能力的运动体下一步位置会越过边界体的 X 或 Y 边界
- **THEN** tick system 将该运动体保持在边界体允许范围内
- **AND** tick system 反转对应轴向的速度方向
- **AND** 本次 tick 产生可验证的反弹结果

### Requirement: Tick 结果 Dirty 与同步
系统 SHALL 在每次 demo tick 改变运动体状态后记录 dirty state，并通过同步边界向当前 observer 广播服务端权威状态。

#### Scenario: 移动后标记 dirty
- **WHEN** demo tick 更新了运动体 entity 的位置或速度方向
- **THEN** world dirty state 包含该 entity 的变化
- **AND** dirty state 可被同步系统读取并在广播后清理

#### Scenario: 广播服务端最终状态
- **WHEN** sync system flush demo dirty state
- **THEN** 它向当前在线 observer 广播变更 entity 的 entity id、配置标识、server tick 和服务端最终状态
- **AND** 广播内容来自服务端 world 状态，而不是来自客户端本地推导

#### Scenario: 边界体 snapshot 可同步
- **WHEN** 客户端 Join 或 Observe demo world
- **THEN** 服务端发送边界体 entity 的当前 snapshot
- **AND** snapshot 包含客户端显示或调试边界所需的最小范围数据

#### Scenario: 无 observer 时不阻塞 tick
- **WHEN** demo world 没有任何在线 observer
- **THEN** 服务端 tick 仍可继续推进
- **AND** 同步系统不会因为 observer 集合为空而报错

### Requirement: Fantasy 网络入口边界
系统 SHALL 通过 Fantasy Handler 暴露 Join/Observe demo 的网络入口，但 Handler MUST NOT 承担反弹规则、tick 推进或同步批处理。

#### Scenario: Handler 注册 observer
- **WHEN** 客户端请求进入或观察反弹球 demo
- **THEN** Fantasy Handler 将当前 session 加入 demo observer 集合
- **AND** Handler 返回或触发当前 demo 配置实例化 entity 的 snapshot

#### Scenario: Handler 不计算反弹
- **WHEN** demo tick 需要推进运动体 entity
- **THEN** 反弹位置和速度由服务端 System 计算
- **AND** Fantasy Handler 不直接修改运动体 entity 的位置或速度

### Requirement: Unity 双客户端演示场景
Unity 客户端 SHALL 提供一个最小演示场景，用于连接服务端、观察同一 demo world，并根据 snapshot 中的配置标识显示服务端同步的两个 demo entity。

#### Scenario: 客户端创建或更新两个实体视图
- **WHEN** Unity 客户端收到两个配置实例化 entity 的 snapshot
- **THEN** 客户端在当前 `World` 中创建或更新对应 entity
- **AND** 客户端渲染层能根据配置标识显示运动体和边界体

#### Scenario: 两个客户端显示同一服务端 tick
- **WHEN** 客户端 A 和客户端 B 同时观察同一 demo world
- **AND** 服务端广播某个 server tick 的运动体状态
- **THEN** A 和 B 都应用同一 entity id、server tick 和服务端最终位置
- **AND** A 和 B 显示的球位置与服务端日志一致

#### Scenario: 客户端不本地推导权威运动
- **WHEN** Unity 客户端尚未收到下一个服务端 tick 状态
- **THEN** 客户端不通过本地反弹规则提前改变运动体的权威坐标
- **AND** 客户端下一次位置变化来自服务端 snapshot 或 delta

### Requirement: 验证路径
系统 SHALL 提供自动化测试和手动端到端验收路径，证明反弹规则、tick 生命周期、网络同步和双客户端显示都可验证。

#### Scenario: 服务端规则验证
- **WHEN** 运行服务端 demo 规则验证
- **THEN** 测试覆盖普通移动、X 轴反弹、Y 轴反弹、dirty flush 和重复启动保护

#### Scenario: Unity TestFramework 验证
- **WHEN** 运行 Unity TestFramework 测试
- **THEN** 测试覆盖客户端接收 snapshot 后按配置标识创建两个 entity
- **AND** 测试覆盖客户端应用同一 server tick 的运动体状态后 world 坐标与 dirty state 正确

#### Scenario: 双客户端手动端到端验收
- **WHEN** 服务端运行中
- **AND** 两个 Unity 客户端连接并观察 demo world
- **THEN** 两个客户端都能看到持续反弹的球
- **AND** 两个客户端日志中的 server tick、entity id 和球坐标与服务端广播日志一致
