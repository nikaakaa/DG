using System;

namespace DG.GameCore
{
[Flags]
public enum WorldTag
{
    None = 0,
    SourcePlayer = 1 << 0,
    SourceMechanism = 1 << 1,
    SourceAuto = 1 << 2,
    SourceDebug = 1 << 3,
    SourceSkill = 1 << 4,
    AbilityMove = 1 << 5,
    AbilityPlayerPush = 1 << 6,
    AbilityMechanismPush = 1 << 7,
    AbilityAutoMove = 1 << 8,
    StateStunned = 1 << 9,
    StateRooted = 1 << 10,
    StateSuperArmor = 1 << 11,
    ImmuneMechanismPush = 1 << 12,
    BlockPlayerMove = 1 << 13
}
}
