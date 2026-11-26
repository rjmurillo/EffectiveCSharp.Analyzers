# AGENTS.md

Standing facts for coding agents in this repo. Read before changing build files.

## .NET SDK and target frameworks

- `global.json` pins the .NET SDK. It uses `rollForward: patch`.
- CI reads the SDK from `global.json` through `actions/setup-dotnet`.
- Bump the SDK in `global.json` only. Do not add a version to a workflow.
- The analyzer project targets `netstandard2.0`. Both Visual Studio and the .NET SDK can load that.
- The TFM alone does not set host support. The Roslyn package pins also matter.
- `Directory.Packages.props` pins `Microsoft.CodeAnalysis.CSharp` 4.8, so the floor is VS 2022 17.8 and .NET 8 SDK.
- Do not raise those pins as part of a TFM or SDK bump.
- `src/tools/Dogfood` compiles the analyzer sources, so it stays on `netstandard2.0` too.
- Tests, benchmarks, and `src/tools/PerfDiff` target `net10.0`.
- Move those three together when the repo moves to a new .NET major.
- `global.json` also pins the `DotNet.ReproducibleBuilds.Isolated` MSBuild SDK.
- Keep that MSBuild SDK at 1.2.39 or later. Version 1.2.4 broke .NET 10 restore (PR #81).
- This floor is only for the `.Isolated` SDK in `global.json`.
- The `DotNet.ReproducibleBuilds` NuGet package in `build/targets/reproducible/Packages.props` versions on its own.
