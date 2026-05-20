from __future__ import annotations

import re
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Config" / "Luban" / "Defines" / "gamecore.xml"
TARGET = ROOT / "Shared" / "DG.GameCore" / "Configuration" / "ComponentKind.cs"


def read_component_kind() -> list[tuple[str, str]]:
    tree = ET.parse(SOURCE)
    root = tree.getroot()
    enum = root.find("./enum[@name='ComponentKind']")
    if enum is None:
        raise RuntimeError(f"ComponentKind enum not found: {SOURCE}")

    result = []
    for item in enum.findall("var"):
        name = item.attrib.get("name")
        value = item.attrib.get("value")
        if not name or not value:
            raise RuntimeError("ComponentKind var must have name and value")
        if not re.match(r"^[A-Za-z_][A-Za-z0-9_]*$", name):
            raise RuntimeError(f"Invalid ComponentKind name: {name}")
        if not re.match(r"^\d+$", value):
            raise RuntimeError(f"Invalid ComponentKind value: {value}")
        result.append((name, value))

    if not result:
        raise RuntimeError("ComponentKind enum has no values")
    return result


def write_runtime_enum(values: list[tuple[str, str]]) -> bool:
    body = "\n".join(f"    {name} = {value}," for name, value in values)
    new_text = f"""using System;
using System.Collections.Generic;
using System.Linq;

namespace DG.GameCore
{{
public enum ComponentKind
{{
{body}
}}

public readonly struct ComponentId : IEquatable<ComponentId>
{{
    public ComponentId(string value)
        : this(RuntimeKeyUtility.StableRuntimeKey(value), value)
    {{
    }}

    public ComponentId(ComponentKind kind)
        : this(kind.ToString())
    {{
    }}

    public ComponentId(int runtimeKey, string debugName)
    {{
        RuntimeKey = runtimeKey;
        Value = debugName ?? string.Empty;
    }}

    public string Value {{ get; }}
    public int RuntimeKey {{ get; }}
    public bool IsValid => RuntimeKey != 0;

    public bool Equals(ComponentId other)
    {{
        return RuntimeKey == other.RuntimeKey;
    }}

    public override bool Equals(object obj)
    {{
        return obj is ComponentId other && Equals(other);
    }}

    public override int GetHashCode()
    {{
        return RuntimeKey;
    }}

    public override string ToString()
    {{
        return Value;
    }}

    public static implicit operator ComponentId(string value)
    {{
        return new ComponentId(value);
    }}
}}
}}
"""
    old_text = TARGET.read_text(encoding="utf-8-sig") if TARGET.exists() else ""
    if new_text == old_text:
        return False
    TARGET.parent.mkdir(parents=True, exist_ok=True)
    TARGET.write_text(new_text, encoding="utf-8")
    return True


if __name__ == "__main__":
    changed = write_runtime_enum(read_component_kind())
    print(f"{TARGET}: {'updated' if changed else 'unchanged'}")
