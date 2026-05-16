using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum RuntimeEffectKind
{
    TemporaryBlocking = 1,
    TemporaryAutoMove = 2,
    TemporaryPushable = 3,
    TemporaryPort = 4,
    TemporaryImmobile = 5,
    TemporaryTag = 6,
    TemporaryRotatePivot = 7
}

public enum EffectKind
{
    Blocking = 1,
    AutoMove = 2,
    Pushable = 3,
    PortConnector = 4,
    MovementPermission = 5,
    Tag = 6,
    RotatePivot = 7
}

public enum EffectDurationPolicy
{
    Instant = 1,
    TimedTicks = 2,
    InfiniteUntilRemove = 3
}

public enum EffectStackPolicy
{
    ReplaceByStackKey = 1,
    RefreshDuration = 2,
    AllowMultiple = 3,
    RejectDuplicate = 4
}

public enum EffectRemovePolicy
{
    ExplicitOrExpire = 1,
    ExplicitOnly = 2
}

public enum EffectTargetBinding
{
    TargetEntity = 1,
    TargetCell = 2,
    TargetBody = 3
}

}
