# 与classic版本差异

当前版本相对于classic版本简化了代码，更易定制。数据、代码格式基本同旧版，但**本地化**实现差异大。

## 移除不必要的模块
- 移除 Proto、DB 生成及云生成，大幅简化代码

## Excel格式调整
- A1单元以`##`开头，第一行为注释行（旧版为字段名定义行）

## 命令行参数调整
变化极大，支持自定义参数

## 类型系统调整
- 移除 vector2/3/4，改由 type mapper 实现
- text 类型为`string#text=1`的语法糖，只包含key（旧版含key/value）

## 定义调整
- enum、bean支持group参数
- table的read_from_file属性调整为readSchemaFromFile，excel中对应属性为read_schema_from_file
- 移除externaltype，改用typeMapper并在enum/bean子元素中定义

## 多代码/数据target支持
允许`-c target1 -c target2`一次生成多个目标，可通过层级参数机制指定输出目录

## 语言支持调整
不再内置erlang支持，需自行实现

## 管线定制能力
可在不影响原始代码的情况下，单独定制和调整管线及几乎所有模块
