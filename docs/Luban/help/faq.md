# FAQ

## How to Specify a Primary Key
You specify the primary key list via the `index` field of the table. For details, see the documentation on `table` mode and `index` in [Configuration Related Definitions](/docs/manual/schema).

Both map and list tables support primary key concepts. If mode and index are not specified, the mode defaults to `map`, and the first field of the bean is used as the primary key.

Assuming the `TbTest` table uses the `Test` type as its record, and you want to use the `my_index` field of `Test` as the key:
1.  If defining the table in XML:
    ```xml
    <table name="TbTest" value="Test" index="my_index"/>
    ```
2.  If defining the table in `table.xlsx`:
    |##var|full_name|value_type|define_from_excel|input|index|...|
    |---|---|---|---|---|---|---|
    | |TbTest|Test|true|equip.xlsx|my_index|...|

---

## Does it Support Multiple Primary Keys?
Yes. When `table mode=list`, both combined multi-primary key mode and independent multi-primary key mode are supported. For details, see the documentation on `table` mode in [Configuration Related Definitions](/docs/manual/schema).

---

## Does it Support Exporting Different Tables and Fields for Client and Server?
Yes. See the documentation on hierarchical definition and grouped export in [Configuration Related Definitions](/docs/manual/schema).

---

## What Source Data File Types are Supported?
- Excel family: csv, xls, xlm, xlsx, xlsm, etc.
- json
- xml
- lua
- yaml

---

## Can Configuration Table Data Come from Multiple Files?
Yes. See the documentation on `table.input` in [Configuration Related Definitions](/docs/manual/schema).

---

## Can Multiple Tables Be Placed in the Same Excel File?
Yes. See the documentation on `table.input` in [Configuration Related Definitions](/docs/manual/schema).

---

## When Using an XLSX Data File, Does Luban Read the First Sheet or All Sheets?
Luban reads all sheets, but ignores sheets where the A1 cell does not start with `##`.

---

## How Can Designers Have a Non-Data Sheet in Their XLSX File?
Just make sure the A1 cell of that sheet does not start with `##`.

---

## How Do I Comment Out a Column?
Leave the column name empty, or use a name like `#xxxx`.

---

## How Do I Comment Out a Row of Records?
Fill `##` in the first cell of that row.

---

## What if I Only Want Some Configurations for Internal Testing During Development, and Not Export Them for Official Release?
Luban supports the data tag concept. The first column of the Excel sheet is the tag:
1.  When the tag is `##`, the row is ignored
2.  When the tag is `xxx`, the record will not be exported if you use `--export_exclude_tags xxx` in the Luban.Client command line

---

## I Want Each JSON to Save One Record, But There Are Too Many Files and Specifying Them in Input Is Tedious. How Do I Fix This?
Use directory data sources. Put all JSON files in a directory (can be a directory tree), set `input` to that directory. Luban will automatically traverse the entire directory tree and read each file as one record. For details, see [Other Data Sources - JSON](/docs/manual/otherdatasource).

---

## Can a Single JSON File Contain Multiple Records?
Yes. You must specify it in the data source in the form of `*@xxx.json`. For details, see [Other Data Sources - JSON](/docs/manual/otherdatasource).

---

## Can Records Come From a Deeply Nested Field in a JSON File?
Yes, there are two cases:
1.  Read one record from a field: specify in the form `a.b.c@xx.json`
2.  Read a list of records from a field: specify in the form `*[a.b.c@xx.json]`

For details, see [Other Data Sources - JSON](/docs/manual/otherdatasource).

---

## Can I Put Multiple Table Data into a Single JSON File Like I Do with Excel?
Yes. Similar to the Excel data source, just specify each table using `field@xx.json` or `*[field@xx.json]`. For details, see [Other Data Sources - JSON](/docs/manual/otherdatasource).

---

## Does it Support Asynchronous Loading of Configuration Tables?
Not directly, but you can implement asynchronous loading via custom templates.

---

## Can I Reference Existing Enums and Structures? For Example, I Want to Use UnityEngine.AudioType and UnityEngine.Color in the Generated Code
Yes, external enums and structures are supported, and currently only the C# language is supported. For details, see the type mapper documentation in [Configuration Definition Introduction](/docs/manual/schema).
