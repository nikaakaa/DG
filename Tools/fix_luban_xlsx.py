from __future__ import annotations

from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile
import html


ROOT = Path(__file__).resolve().parents[1]
DATA_DIR = ROOT / "Config" / "Luban" / "Datas" / "gamecore"


def col_name(index: int) -> str:
    name = ""
    while index:
        index, rem = divmod(index - 1, 26)
        name = chr(65 + rem) + name
    return name


def ref(col: int, row: int) -> str:
    return f"{col_name(col)}{row}"


def build_shared_strings(values: list[str]) -> str:
    items = []
    for value in values:
        items.append(f'<si><t>{html.escape(value, quote=False)}</t></si>')
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
        f'count="{len(values)}" uniqueCount="{len(values)}">'
        + "".join(items)
        + "</sst>"
    )


def cell_xml(col: int, row: int, value, strings: dict[str, int]) -> str:
    cell_ref = ref(col, row)
    if isinstance(value, int):
        return f'<c r="{cell_ref}"><v>{value}</v></c>'
    text = str(value)
    return f'<c r="{cell_ref}" t="s"><v>{strings[text]}</v></c>'


def sheet_xml(columns: list[str], types: list[str], groups: list[str], rows: list[list[object]], strings: dict[str, int]) -> str:
    all_rows = [
        ["##var", *columns],
        ["##type", *types],
        ["##group", *groups],
        *[["", *row] for row in rows],
    ]
    max_row = len(all_rows)
    max_col = max(len(row) for row in all_rows)
    parts = []
    for row_index, row in enumerate(all_rows, start=1):
        cells = []
        for col_index, value in enumerate(row, start=1):
            cells.append(cell_xml(col_index, row_index, value, strings))
        parts.append(f'<row r="{row_index}" spans="1:{max_col}">{"".join(cells)}</row>')
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
        f'<dimension ref="A1:{ref(max_col, max_row)}"/>'
        '<sheetViews><sheetView workbookViewId="0"/></sheetViews>'
        '<sheetFormatPr defaultRowHeight="14"/>'
        '<sheetData>'
        + "".join(parts)
        + "</sheetData>"
        '<pageMargins left="0.7" right="0.7" top="0.75" bottom="0.75" header="0.3" footer="0.3"/>'
        "</worksheet>"
    )


def workbook_xml(sheet_name: str) -> str:
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" '
        'xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">'
        "<sheets>"
        f'<sheet name="{html.escape(sheet_name, quote=True)}" sheetId="1" r:id="rId1"/>'
        "</sheets>"
        "</workbook>"
    )


def write_xlsx(path: Path, sheet_name: str, columns: list[str], types: list[str], groups: list[str], rows: list[list[object]]) -> None:
    values: list[str] = []
    for value in ["##var", "##type", "##group", "", *columns, *types, *groups]:
        if value not in values:
            values.append(value)
    for row in rows:
        for value in row:
            if not isinstance(value, int) and str(value) not in values:
                values.append(str(value))
    strings = {value: index for index, value in enumerate(values)}
    path.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(path, "w", ZIP_DEFLATED) as z:
        z.writestr("[Content_Types].xml", content_types_xml())
        z.writestr("_rels/.rels", root_rels_xml())
        z.writestr("xl/workbook.xml", workbook_xml(sheet_name))
        z.writestr("xl/_rels/workbook.xml.rels", workbook_rels_xml())
        z.writestr("xl/styles.xml", styles_xml())
        z.writestr("xl/sharedStrings.xml", build_shared_strings(values))
        z.writestr("xl/worksheets/sheet1.xml", sheet_xml(columns, types, groups, rows, strings))


def content_types_xml() -> str:
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">'
        '<Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>'
        '<Default Extension="xml" ContentType="application/xml"/>'
        '<Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>'
        '<Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>'
        '<Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>'
        '<Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>'
        "</Types>"
    )


def root_rels_xml() -> str:
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
        '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>'
        "</Relationships>"
    )


def workbook_rels_xml() -> str:
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
        '<Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>'
        '<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>'
        '<Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>'
        "</Relationships>"
    )


def styles_xml() -> str:
    return (
        '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
        '<styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">'
        '<fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>'
        '<fills count="1"><fill><patternFill patternType="none"/></fill></fills>'
        '<borders count="1"><border/></borders>'
        '<cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>'
        '<cellXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/></cellXfs>'
        "</styleSheet>"
    )


def main() -> None:
    write_xlsx(
        DATA_DIR / "entity_archetype.xlsx",
        "entity_archetype",
        ["config_id", "archetype_id", "entity_target", "components", "tags", "default_auto_move_interval_ticks"],
        ["int", "int", "EntityTarget", "(list#sep=,),ComponentKind", "(list#sep=,),string", "int"],
        ["c,s", "c,s", "c,s", "c,s", "c,s", "c,s"],
        [
            [1, 1, "Player", "Position,Collider,Blocking,PlayerControl", "Entity.Player", 1],
            [1001, 2, "Monster", "Position,Direction,Collider,Blocking,Bouncable,AutoMove", "Entity.Ball,Movement.Bouncable", 1],
            [1002, 3, "Object", "Position,Collider,Blocking", "Entity.Blocker", 1],
            [1003, 5, "Object", "Position,Collider,Blocking,Pushable", "Entity.PushableBlocker", 1],
            [1004, 6, "Object", "Position,Direction,Collider,Blocking,Pushable,PortConnector", "Entity.PortConnectorBlocker", 1],
            [2001, 4, "Object", "Position,Direction,Collider,PushOnEnter", "Tile.Conveyor", 1],
        ],
    )
    write_xlsx(
        DATA_DIR / "port_connector_config.xlsx",
        "port_connector_config",
        ["config_id", "ports"],
        ["int", "int"],
        ["c,s", "c,s"],
        [
            [1004, 3],
        ],
    )
    write_xlsx(
        DATA_DIR / "player_spawn_rule.xlsx",
        "player_spawn_rule",
        ["rule_id", "player_config_id", "start_x", "start_y", "step_x", "step_y", "max_attempts"],
        ["string", "int", "int", "int", "int", "int", "int"],
        ["c,s", "c,s", "c,s", "c,s", "c,s", "c,s", "c,s"],
        [["default", 1, 0, 0, 0, 1, 1024]],
    )
    write_xlsx(
        DATA_DIR / "world_spawn.xlsx",
        "world_spawn",
        ["world_id", "entity_id", "config_id", "x", "y", "direction", "auto_move_interval_ticks"],
        ["string", "long", "int", "int", "int", "Direction", "int"],
        ["c,s", "c,s", "c,s", "c,s", "c,s", "c,s", "c,s"],
        [
            ["demo", 900000001, 1001, -2, 0, "Right", 1],
            ["demo", 900000099, 1003, 0, 1, "None", 1],
            ["demo", 900000300, 1004, 0, -1, "Right", 1],
            ["demo", 900000301, 1004, 1, -1, "Right", 1],
            ["demo", 900000100, 1002, 4, 0, "None", 1],
            ["demo", 900000101, 1002, -4, 0, "None", 1],
            ["demo", 900000200, 2001, 1, 0, "Right", 1],
            ["demo", 900000201, 2001, 3, 0, "Right", 1],
        ],
    )
    action_columns = [
        "spec_id",
        "primitive",
        "source",
        "priority",
        "source_tag",
        "ability_tag",
        "required_tags",
        "blocked_tags",
        "target_rule",
        "blocked_policy",
        "handoff_policy",
        "handoff_spec_id",
        "handoff_subject_policy",
        "max_chain_depth",
        "conflict_policy",
        "interrupt_policy",
        "merge_policy",
        "subject_policy",
        "plan_rule",
        "commit_rules",
        "default_cost_ticks",
    ]
    write_xlsx(
        DATA_DIR / "action_spec.xlsx",
        "action_spec",
        action_columns,
        [
            "string",
            "ActionPrimitive",
            "ActionSourceKind",
            "ActionPriority",
            "string",
            "string",
            "(list#sep=,),string",
            "(list#sep=,),string",
            "ActionTargetRule",
            "ActionBlockedPolicy",
            "ActionHandoffPolicy",
            "string",
            "ActionSubjectPolicy",
            "int",
            "ActionConflictPolicy",
            "ActionInterruptPolicy",
            "ActionMergePolicy",
            "ActionSubjectPolicy",
            "ActionPlanRule",
            "(list#sep=,),ActionCommitRule",
            "int",
        ],
        ["c,s"] * len(action_columns),
        [
            ["player_move", "Move", "Player", "Player", "SourcePlayer", "AbilityMove", "", "BlockPlayerMove,StateStunned,StateRooted", "TargetCoordOneStep", "StartPushIfPushable", "Configured", "player_push", "ConnectedBodyIfAny", 8, "ExclusiveTargetCell", "HigherPriorityInterruptsLower", "SameClaim", "ConnectedBodyIfAny", "MoveBody", "", 1],
            ["auto_move", "Move", "Auto", "Auto", "SourceAuto", "AbilityAutoMove", "", "", "DirectionFromComponent", "BounceIfBouncable", "None", "", "HitEntity", 0, "ExclusiveTargetCell", "HigherPriorityInterruptsLower", "SameClaim", "HitEntity", "MoveBody", "SetAutoMoveTick,SetDirectionOnBounce", 1],
            ["mechanism_push", "Move", "Mechanism", "Mechanism", "SourceMechanism", "AbilityMechanismPush", "", "ImmuneMechanismPush", "DirectionFromRequest", "StartPushIfPushable", "Configured", "mechanism_push", "ConnectedBodyIfAny", 8, "ExclusiveTargetCell", "HigherPriorityInterruptsLower", "SameClaim", "ConnectedBodyIfAny", "MoveBody", "", 1],
            ["debug_move", "Move", "Debug", "Debug", "SourceDebug", "AbilityMove", "", "", "TargetCoordAny", "Reject", "None", "", "HitEntity", 0, "ExclusiveTargetCell", "HigherPriorityInterruptsLower", "SameClaim", "HitEntity", "MoveBody", "", 1],
            ["debug_spawn", "Spawn", "Debug", "Debug", "SourceDebug", "", "", "", "TargetCoordAny", "Reject", "None", "", "HitEntity", 0, "None", "None", "None", "HitEntity", "SpawnEntity", "", 1],
            ["debug_remove", "Remove", "Debug", "Debug", "SourceDebug", "", "", "", "None", "Reject", "None", "", "HitEntity", 0, "None", "None", "None", "HitEntity", "RemoveEntity", "", 1],
            ["player_push", "Move", "Player", "Player", "SourcePlayer", "AbilityPlayerPush", "", "StateStunned", "DirectionFromRequest", "StartPushIfPushable", "Configured", "player_push", "ConnectedBodyIfAny", 8, "ExclusiveTargetCell", "HigherPriorityInterruptsLower", "SameClaim", "ConnectedBodyIfAny", "MoveBody", "", 1],
            ["configured_wind_push", "Move", "Mechanism", "Mechanism", "SourceMechanism", "AbilityMechanismPush", "", "ImmuneMechanismPush", "DirectionFromRequest", "StartPushIfPushable", "Configured", "configured_wind_push", "ConnectedBodyIfAny", 8, "ExclusiveTargetCell", "HigherPriorityInterruptsLower", "SameClaim", "ConnectedBodyIfAny", "MoveBody", "", 1],
            ["connected_body_move", "Move", "Mechanism", "Mechanism", "SourceMechanism", "AbilityMechanismPush", "", "", "DirectionFromRequest", "StartPushIfPushable", "Configured", "connected_body_move", "ConnectedBodyIfAny", 8, "ExclusiveTargetCell", "HigherPriorityInterruptsLower", "SameClaim", "ConnectedBodyIfAny", "MoveBody", "", 1],
        ],
    )
    print("luban xlsx files regenerated")


if __name__ == "__main__":
    main()
