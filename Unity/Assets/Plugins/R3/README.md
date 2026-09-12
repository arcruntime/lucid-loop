# R3 managed dependencies

The pinned `com.osuframework.unity` UPM dependency requires R3. R3's Unity adapter is installed through UPM; its managed core is supplied here from the corresponding NuGet packages so a fresh Unity checkout needs no local NuGet cache or NuGetForUnity setup.

| Assembly | NuGet version | Framework |
| --- | --- | --- |
| R3 | 1.3.0 | netstandard2.1 |
| Microsoft.Bcl.TimeProvider | 8.0.0 | netstandard2.0 |
| Microsoft.Bcl.AsyncInterfaces | 6.0.0 | netstandard2.1 |
| System.Threading.Channels | 8.0.0 | netstandard2.1 |
| System.Runtime.CompilerServices.Unsafe | 6.0.0 | netstandard2.0 |
| System.ComponentModel.Annotations | 5.0.0 | netstandard2.1 |

DLLs were copied without modification from these NuGet package versions. Package manifests and licenses are adjacent. DLLs are stored through Git LFS. Unity supplies the remaining .NET Standard 2.1 framework assemblies. Do not add competing copies through another package manager.

Sources: https://github.com/Cysharp/R3 and https://github.com/dotnet/runtime . The R3 Unity adapter and DI package commits are locked in `Packages/manifest.json` and `Packages/packages-lock.json`.
