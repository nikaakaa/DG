# DoD 与验证

## 结论

行为层 DoD 的核心不是"某个业务行为能跑",而是:

```text
新行为能通过公开扩展点接入
主流程不认识具体行为
Runner 不绕过 Claim / Commit / Fact 载体
持续状态走 RuntimeEffect
测试证明边界没有回归
```

## DummyRunner 扩展性测试

测试思想:

```text
在测试 fixture 里定义一个完全虚构的 dummy 行为
注册 dummy runner / dummy action spec
跑一个完整 tick
断言 dummy event 被 emit
断言主流程文件没有改动 / 没硬编码 dummy 名字
```

这个测试红了,通常代表:

```text
主流程开始认识具体行为
扩展点不再足够
接口签名破坏了 fixture 接入
runner 注册不再能走公开路径
```

它不是业务测试,是架构守护测试。

## grep DoD

主流程白名单文件禁止出现:

```text
if (runnerId == "charge")
if (instance.BehaviorId == "rotate")
switch (spec.SpecId)
new RotatePivotRunner()
new ChargeRunner()
```

新行为 PR 的 diff 如果修改主流程白名单文件,默认视为可疑。除非该 PR 本身就是行为层架构 PR,并且有明确 OpenSpec / 测试证明。

## Unity TestFramework

进入实现后必须有 Unity 自带 TestFramework 测试。最小测试矩阵:

```text
EditMode:
  Claim 仲裁:
    同 tick 两个 move 争同 cell,高优先级获胜
    长期 Claim 占用期间其他 action 被拒
    Runner Exit / interrupt 后 Claim 释放

  CommitProposal:
    Runner 不直接写 world
    CommitResolver apply 后 world 变化
    commit 失败返回明确 reason

  RuntimeEffect:
    AddRuntimeEffect 后 RuntimeEffectStore 有实例
    temporary_immobile 结算出 MovementPermission(canMove=false)
    move 被拒
    effect 过期后 move 恢复

  Fact/Event:
    MoveEntity 成功产生 WorldDeltaFact.EntityMoved
    Runner emit BehaviorEvent 不需要改主流程 enum

  DummyRunner:
    新 dummy runner 不改主流程也能完整 tick
```

## 手动端到端验证

每个 OpenSpec change 完成时,用户要能手动验证:

```text
启动服务端
启动 Unity 客户端
进入调试场景
执行目标动作
观察 world 状态 / debug panel / 客户端表现
检查日志无错误
```

RuntimeEffect 示例:

```text
1. 对实体施加 temporary_immobile
2. 立即尝试移动该实体
3. 观察移动被拒
4. tick 到 effect 过期
5. 再次移动
6. 观察移动恢复
```

## 文档成熟度标记

文档中必须区分:

```text
已定架构:
  已经做出的设计决定

已有骨架:
  源码已有模块,但不代表端到端成熟

待验证:
  必须补测试或手动验证后才能归档
```

不要把"源码里有类"写成"系统完整完成"。

## 归档口径

OpenSpec archive 表示用户已经测试过。归档前必须:

```text
1. tasks 全部勾选
2. Unity TestFramework 相关测试通过
3. 手动端到端验证步骤写清楚
4. 用户确认验证完成
```
