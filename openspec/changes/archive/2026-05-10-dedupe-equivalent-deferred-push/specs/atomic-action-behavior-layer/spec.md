## ADDED Requirements

### Requirement: Deferred Queue Growth Boundary
系统 SHALL keep cross-tick deferred output processing finite per tick by merging equivalent queued outputs. This boundary MUST prevent queue growth caused only by duplicate same-subject same-direction deferred outputs from multiple parent branches, while preserving periodic closed-loop output and distinct-subject fanout.

#### Scenario: 分支汇合同质输出不膨胀
- **WHEN** several propagation branches in one tick all produce a deferred push for the same resolved subject, same direction, same spec id, and same ready tick
- **THEN** only one equivalent queued output is consumed on that ready tick
- **AND** the number of queued actions does not grow solely because parent branch causality ids differ

#### Scenario: 周期结构仍跨 tick 运行
- **WHEN** a closed-loop device produces one deferred output per period
- **THEN** that output may still re-enter the queue on its future ready tick
- **AND** dedupe does not stop a periodic device merely because it has emitted in a previous tick

#### Scenario: 不替代 push 仲裁
- **WHEN** equivalent deferred dedupe runs
- **THEN** it does not decide interruption, cancellation, opposite-direction resolution, strength stacking, TTL expiry, or energy decay
- **AND** those policies require separate proposals if needed
