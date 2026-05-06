# Luban Documentation (v4.x)

Downloaded from https://www.datable.cn/ for AI reference.

## Directory Structure

```
Luban/
├── README.md                    # This index
├── intro.md                     # Introduction & core features
├── beginner/                    # Beginner tutorials
│   ├── quickstart.md           # Quick start guide
│   ├── generatecodeanddata.md  # Generate code and data
│   ├── integratetoproject.md   # Integrate into project
│   ├── loadinruntime.md        # Load config at runtime
│   ├── usecustomtype.md        # Custom types (enum, bean)
│   ├── usecollection.md        # Container types (array, list, set, map)
│   ├── streamandcolumnformat.md # Column-limited & compact formats
│   ├── usevalidator.md         # Data validators (ref, path)
│   ├── usepolymorphismtype.md  # Polymorphic types
│   └── importtable.md          # Auto-import tables
├── manual/                      # Usage guide (detailed reference)
│   ├── migrate.md              # Differences from classic version
│   ├── architecture.md         # Design philosophy (type system, DPP pipeline)
│   ├── traits.md               # Feature list
│   ├── luban.conf.md           # luban.conf configuration
│   ├── schema.md               # Schema logical structure (enum, bean, table)
│   ├── defaultschemacollector.md # Default schema collector (XML/Excel definitions)
│   ├── importtable.md          # Auto-import tables (manual)
│   ├── commandtools.md         # CLI tools (code/data targets, xargs)
│   ├── cascadingoption.md      # Cascading option mechanism
│   ├── types.md                # Type system (basic, container, nullable)
│   ├── typemapper.md           # Type mapping (e.g. vector3 → UnityEngine.Vector3)
│   ├── excel.md                # Excel format (basic)
│   ├── exceladvanced.md        # Excel format (advanced)
│   ├── excelcompactformat.md   # Excel compact format
│   ├── otherdatasource.md      # Non-excel data sources (json, lua, xml, yaml)
│   ├── generatecodedata.md     # Code & data generation
│   ├── codestyle.md            # Code style conventions
│   ├── loadconfigatruntime.md  # Runtime config loading
│   ├── validator.md            # Data validators (ref, path, range, etc.)
│   ├── template.md             # Custom templates
│   ├── tag.md                  # Data tags
│   ├── variants.md             # Field variants
│   ├── l10n.md                 # Localization
│   ├── bestpractices.md        # Best practices
│   └── extendluban.md          # Extending Luban
├── help/
│   └── faq.md                  # FAQ
└── other.md                     # Other info
```

## Key Concepts for This Project (DG)

- **Code Target**: `cs-newtonsoft-json` (server + shared) / `cs-simple-json` (Unity client)
- **Data Target**: `json`
- **Schema format**: XML definitions in `Config/Luban/Defines/`
- **Data format**: Excel (.xlsx) in `Config/Luban/Datas/`
- **Generated code**: `Shared/DG.GameCore/Config/Generated/LubanTables/`
- **Generated data**: `Config/Luban/Generated/json/` → copied to `Client/DG_Client/Assets/StreamingAssets/GameConfig/`
- **Config loading**: `LubanConfigLoader` → `cfg.Tables` → `LubanGameConfigProvider` → `IGameConfigProvider`
