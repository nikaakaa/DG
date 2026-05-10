## ADDED Requirements

### Requirement: Collision-Safe Composed Push Movement
系统 SHALL resolve composed push movement through discrete grid movement checks. A composed push vector MUST NOT teleport, fly over, or otherwise bypass intermediate collision cells.

#### Scenario: 单轴合成仍走 claim 和 commit
- **WHEN** push vector composition produces a non-zero single-axis intent
- **THEN** the resulting movement attempt enters the existing body claim, target claim, and commit validation pipeline
- **AND** blockers, reserved targets, source changes, and occupied cells can still reject the movement

#### Scenario: 向量终点不能跳过碰撞
- **WHEN** a composed vector has magnitude greater than one or contains multiple axis components
- **THEN** the system MUST NOT directly move the subject to the vector endpoint
- **AND** every occupied grid cell that would be crossed by an enabled path policy must be checked as a discrete movement step

#### Scenario: 双轴向量确定性拆分
- **WHEN** push vector composition produces a non-zero two-axis vector
- **THEN** the system decomposes it into deterministic single-cell steps using the configured path order
- **AND** the first implementation uses X then Y as the default path order
- **AND** each step is checked through discrete movement validation
- **AND** future eight-direction movement still requires explicit collision or corner-crossing rules before diagonal shortcuts are allowed

### Requirement: Push Composition Does Not Replace Existing Arbitration
系统 SHALL keep existing body intent arbitration, target claim arbitration, and commit validation as the final movement safety layers after push vector composition.

#### Scenario: body intent 仍然裁决
- **WHEN** a composed push intent targets a connected body
- **THEN** the body still projects its member claims through the existing claim pipeline
- **AND** all member movement remains all-or-nothing according to the existing body commit boundary

#### Scenario: target claim 仍然裁决
- **WHEN** a composed push intent and another accepted move attempt target the same cell
- **THEN** target claim arbitration still chooses or rejects candidates according to existing priority and claim rules
- **AND** push vector composition does not reserve target cells by itself
