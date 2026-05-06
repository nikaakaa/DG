# Luban 特性

## 完备的类型系统
- 基础内置类型 `bool,byte,short,int,long,float,double,string,text,datetime`
- 容器类型 `array,list,set,map`
- 自定义枚举和结构，其中结构支持无限层次的类型继承和多态
- 可空类型。除了容器以外的类型都支持定义相应的可空类型

## 支持增强的excel格式
- 支持 true、false、1、0表示bool值
- 用枚举名及别名表示枚举常量
- 支持可空变量
- 支持 datetime 数据类型
- 支持结构类型字段
- 支持拆分单元格
- 支持多态
- 支持多行填写结构列表
- 支持多级标题头

## 支持丰富的源文件类型
- excel族文件（csv、xls、xlsx、xlsm）
- json、lua、xml、yml
- 目录递归扫描
- 每个表允许指定多个数据源

## 多种导出数据格式支持
**导出格式与原始数据解耦**。无论源数据是 excel、lua、xml、json 或者它们的混合, 最终都被以统一的格式导出。支持：bin、json、lua、xml、yaml、protobuf、msgpack、flatbuffers

## 支持表与字段级别分组
支持自定义分组类型。既支持按分组选择性导出一部分表，也支持选择性导出表中的一部分字段。

## 支持数据标签
支持记录标签。比如标签为 "test"，则只在测试导出情况下才导出。

## 强大的数据校验能力
- ref 校验器（引用检查）
- path 校验器（资源路径检查）
- range 校验器（数值范围检查）
- size 校验器（容器大小检查）
- set 校验器（集合校验）
- regex 校验器（正则校验）
- not default 检验器
- index 校验器
- text 检验器

## 多种数据表模式
- one：单例表
- map：普通key-value表
- list：支持多主键联合索引和多主键独立索引

## 本地化支持
- 支持本地化时间
- 支持text类型

## 支持主流语言
c++、c#、java、go、lua、typescript、python、gdscript、php、dart

## 支持主流引擎和热更新
Unity + c#/hybridclr/tolua/xlua/ILRuntime、Unreal + c++/unlua/sluaunreal/puerts、Cocos、Godot、微信小程序等
