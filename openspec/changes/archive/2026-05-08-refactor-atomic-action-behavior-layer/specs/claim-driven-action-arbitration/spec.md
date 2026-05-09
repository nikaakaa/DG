## MODIFIED Requirements
### Requirement: Data-Driven Blocked Policy Resolution
系统 SHALL resolve blocked policies in the arbiter using `ActionSpec`, final component state, final tags, world state, and claims. Execution and planner MUST NOT hardcode whether a blocked move becomes push, bounce, or reject. When a blocked move derives another behavior, the arbiter SHALL emit a derived action unit proposal and leave parent waiting / retry state to the pending action unit boundary.

#### Scenario: Start push if pushable
- **WHEN** an action with `StartPushIfPushable` hits an external blocker with final pushable component state
- **THEN** the arbiter derives a blocker push action unit proposal
- **AND** the original action unit enters waiting state instead of being replaced by the blocker action
- **AND** the original owner action does not report success until the parent unit retries and succeeds

#### Scenario: Bounce if bouncable
- **WHEN** an action with `BounceIfBouncable` hits a final bouncable blocker
- **THEN** the arbiter emits a bounce commit proposal or rejected result according to its `ActionSpec`
- **AND** execution and planner do not branch on an auto-move behavior name

### Requirement: Accepted Actions Feed Planning
系统 SHALL pass only accepted action units or accepted claims to planning. Planning MUST create plans from accepted arbitration output and MUST NOT rediscover pushable, bounce, or blocked policy outcomes. A parent action unit that is waiting for a derived action unit MUST NOT be planned until it retries and is accepted on its own ready tick.

#### Scenario: Planner consumes accepted claims
- **WHEN** arbitration accepts a move action unit with body move claims
- **THEN** planner builds `MovePlan` members from accepted body move claims
- **AND** planner does not re-check whether the target blocker should be pushed or bounced

#### Scenario: Waiting parent action does not plan
- **WHEN** the arbiter derives a blocker action unit from a blocked parent action unit
- **THEN** planner does not build a move plan for the waiting parent action unit
- **AND** action result and pending action unit state reflect that the parent is waiting

### Requirement: Pending Push Uses Unified Action Flow
系统 SHALL express pending push continuation as parent / derived action units in the same claim-driven arbiter. Pending push MUST NOT use execution-layer special branches for port front, chain expansion, or final move acceptance when those can be represented through reusable arbitration policies and action unit retry.

#### Scenario: Pending parent retries through claims
- **WHEN** a derived blocker action unit succeeds
- **THEN** the waiting parent action unit becomes ready to retry on a later ready tick
- **AND** the retried parent action unit enters the same claim-driven arbiter

#### Scenario: Pending completion waits for parent result
- **WHEN** a derived push action unit commits successfully
- **THEN** the owner action is not marked successful solely because the derived action succeeded
- **AND** the owner action succeeds only after the parent action unit retries and commits its own claims

#### Scenario: Pending push keeps chain safety
- **WHEN** an action unit retry would loop, exceed retry limits, exceed pending timeout, or duplicate an active child
- **THEN** the pending action unit boundary fails the unit with stable reason
- **AND** existing covered push outcomes remain compatible
