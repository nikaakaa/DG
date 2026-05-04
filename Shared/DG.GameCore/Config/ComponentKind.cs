using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{
public enum ComponentKind
{
    Position = 1,
    Direction = 2,
    Collider = 3,
    Blocking = 4,
    Bouncable = 5,
    AutoMove = 6,
    PlayerControl = 7,
    PushOnEnter = 8
}
}
