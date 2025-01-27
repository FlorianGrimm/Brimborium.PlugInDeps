# Brimborium.PlugInDeps

POC Test loading plugin

## Problem

To avoid having 2 versions of a assembly in the same process, the plugin must be compiled with the same version of the dependencies as the main program.
That leads to the problem that the plugin can not use newer versions of the dependencies.

## Example

Name       | DotNet Version  | Microsoft.Extensions.Options | System.Text.Json
-----------|-----------------|------------------------------|-----------------
CommonLib  | net8.0          | 8.0.0                        | 
PlugInA    | net8.0          | 8.0.0                        | 9.0.1 Explicit
PlugInB    | net9.0          | 9.0.1                        |
MainPrg    | net9.0          | 9.0.0                        | 9.0.0 Transitive

## How to solve

Load the assemblies with the highest version of ALL of the dependencies.

## .deps.json

Loading all .deps.json files and merge them.

## Run 

```cmd
dotnet build && dotnet run --project .\MainPrg
```

