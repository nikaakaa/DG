## MODIFIED Requirements
### Requirement: Data-Driven Blocked Policy Resolution
系统 SHALL resolve blocked policies in the arbiter using `ActionSpec`, final component state, final tags, world state, and claims. Execution and planner MUST NOT hardcode whether a blocked move becomes push, bounce, or reject. When a blocked move derives another behavior, the arbiter SHALL use the resolved `ActionSpec` handoff policy to emit a derived action unit proposal and leave parent waiting / retry state to the pending action unit boundary.

#### Scenario: Start push if pushable
- **WHEN** an action with `StartPushIfPushable` hits an external blocker with final pushable component state
- **THEN** the arbiter derives a blocker push action unit proposal using the action's configured handoff policy
- **AND** the derived spec id and derived subject are resolved from policy data rather than hardcoded action names
- **AND** the original action unit enters waiting state instead of being replaced by the blocker action
- **AND** the original owner action does not report success until the parent unit retries and succeeds

#### Scenario: Bounce if bouncable
- **WHEN** an action with `BounceIfBouncable` hits a final bouncable blocker
- **THEN** the arbiter emits a bounce commit proposal or rejected result according to its `ActionSpec`
- **AND** execution and planner do not branch on an auto-move behavior name

### Requirement: Data-Driven Priority And Behavior Policy
系统 SHALL treat action priority, source, required tags, blocked tags, blocked policy, handoff policy, conflict policy, merge policy, interrupt policy, subject policy, plan rule, commit rule, and cost policy as data supplied by `ActionSpec` or the action spec registry. Core execution, planning, pending state, and commit orchestration MUST NOT hardcode ordinary behavior priorities or ordinary behavior policies.

#### Scenario: Priority comes from action spec
- **WHEN** a player move, auto move, mechanism push, configured wind push, or debug action is created
- **THEN** the request priority is resolved from its `ActionSpec` or explicit runtime override allowed by that spec
- **AND** the arbiter uses that priority data for merge and interrupt decisions

#### Scenario: Behavior policy comes from action spec
- **WHEN** a behavior changes from reject-on-block to start-push-if-pushable or bounce-if-bouncable
- **THEN** the change is expressed by updating behavior config or registry data
- **AND** `StateDrivenRules`, planner, pending state, and commit orchestration do not add behavior-name branches

#### Scenario: handoff spec comes from action spec
- **WHEN** an action derives a push handoff
- **THEN** the derived action's `ActionSpecId`, subject selection rule, and branch behavior come from the parent action's handoff policy
- **AND** pending state does not default an omitted spec id to `"player_push"`

## ADDED Requirements
### Requirement: Handoff Policy Verification
系统 SHALL verify handoff behavior as explicit policy data. Tests MUST prove that changing handoff policy changes the derived action spec or subject selection without changing core arbiter, pending state, planner, or commit code.

#### Scenario: 两个 action 使用不同 handoff policy
- **WHEN** two move-like action specs hit the same pushable blocker but configure different handoff spec ids
- **THEN** arbitration creates derived action units with the configured spec ids
- **AND** no branch matches either action id string

#### Scenario: connected body handoff 不靠名字
- **WHEN** a pushable blocker belongs to a port connected body
- **THEN** derived subject selection follows the configured handoff subject policy
- **AND** it does not require a hardcoded `"connected_body_move"` spec id
