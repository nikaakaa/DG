## ADDED Requirements
### Requirement: 客户端本地 legacy 规则迁移边界
Unity 客户端 SHALL NOT keep the legacy `MovementResolveSystem` as the default authority for server-authoritative movement. If a local movement system remains for offline tests or non-authoritative tooling, it MUST be explicitly separated from the server-authoritative runtime path and MUST NOT update authoritative mirror state while server-authoritative mode is enabled.

#### Scenario: 服务端权威模式不执行本地 legacy 移动裁决
- **WHEN** Unity 客户端处于 server-authoritative movement mode
- **AND** the local player submits movement input
- **THEN** the client sends the movement request through the network submitter
- **AND** the local legacy movement resolver does not update the authoritative mirror position before server response or world delta

#### Scenario: 本地辅助路径必须显式标识
- **WHEN** a local movement system remains for EditMode tests, sandbox local mode, or offline debugging
- **THEN** it is named and wired as a non-authoritative helper
- **AND** tests distinguish it from the server-authoritative world mirror path
