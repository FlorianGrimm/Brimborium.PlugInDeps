/*
dotnet build && dotnet run --project .\MainPrg
*/
using System.Reflection;
using System.Runtime.Loader;
using System.Xml.Linq;

using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;

namespace MainPrg;

public class LatestAssemblyResolver : ICompilationAssemblyResolver {
    //protected Dictionary<string, FolderBasedCompilationLibrary> CompilationLibraryByName = new Dictionary<string, FolderBasedCompilationLibrary>(StringComparer.OrdinalIgnoreCase);
    public PlugInDependencyContext PlugInDependencyContext { get; } = new PlugInDependencyContext();
    public bool TryResolveAssemblyPaths(CompilationLibrary library, List<string>? assemblies) {
        if (this.PlugInDependencyContext.CompilationLibraryByName.TryGetValue(library.Name, out var folderBasedCompilationLibrary)) {
            var (baseFolder, leatestLibrary) = folderBasedCompilationLibrary;
            var listAssemlbyFileName = new List<string>();
            foreach (var assembly in leatestLibrary.Assemblies) {
                var assemlbyFileName = System.IO.Path.Combine(baseFolder, assembly);
                if (System.IO.File.Exists(assemlbyFileName)) {
                    listAssemlbyFileName.Add(assemlbyFileName);
                } else {
                    return false;
                }
            }
            assemblies?.AddRange(listAssemlbyFileName);
            return true;
        }
        return false;
    }
}

public class PlugInDependencyContext {
    public Dictionary<string, WithBasePath<CompilationLibrary>> CompilationLibraryByName = new();
    public Dictionary<string, WithBasePath<RuntimeLibrary>> RuntimeLibraryByName = new();
    public Dictionary<string, WithBasePath<RuntimeFile>> RuntimeFileByName = new();

    public PlugInDependencyContext() {
    }

    public void Add(Assembly assembly, DependencyContext? dependencyContext) {
        if (dependencyContext is null) { return; }

        var assemblyLocation = assembly.Location;
        if (!(assemblyLocation is { Length: > 0 })) { return; }

        var assemblyFolder = System.IO.Path.GetDirectoryName(assemblyLocation);
        if (!(assemblyFolder is { Length: > 0 })) { return; }

        this.Add(assemblyFolder, dependencyContext);
    }

    public void Add(string folder, DependencyContext? dependencyContext) {
        if (dependencyContext is null) { return; }

        var hsRuntimeLibraryToLoad = new HashSet<string>();

        foreach (var compilationLibrary in dependencyContext.CompileLibraries) {
            if (("project" == compilationLibrary.Type)
                || ("package" == compilationLibrary.Type)) {
                var compilationLibrary_Version = GetVersion(compilationLibrary.Version);
                if (this.CompilationLibraryByName.TryGetValue(compilationLibrary.Name, out var existingCompilationLibrary)) {
                    var existingCompilationLibrary_Version = GetVersion(existingCompilationLibrary.Value.Version);
                    if (compilationLibrary_Version <= existingCompilationLibrary_Version) { continue; }
                }
                this.CompilationLibraryByName[compilationLibrary.Name] = new WithBasePath<CompilationLibrary>(folder, compilationLibrary);

                hsRuntimeLibraryToLoad.Add(compilationLibrary.Name);
                if (compilationLibrary.Path is { Length: > 0 } path) {
                    hsRuntimeLibraryToLoad.Add(path);
                }
            }
            //compilationLibrary.Dependencies
            //foreach (var assembly in compilationLibrary.Assemblies) {}
        }

        foreach (var runtimeLibrary in dependencyContext.RuntimeLibraries) {
            var isTypeProject = "project" == runtimeLibrary.Type;
            var IsTypePackage = "package" == runtimeLibrary.Type;
            if (isTypeProject || IsTypePackage) {
                if (!hsRuntimeLibraryToLoad.Contains(runtimeLibrary.Name)) { continue; }

                var runtimeLibrary_Version = GetVersion(runtimeLibrary.Version);
                if (this.RuntimeLibraryByName.TryGetValue(runtimeLibrary.Name, out var existingRuntimeLibrary)) {
                    var existingRuntimeLibrary_Version = GetVersion(existingRuntimeLibrary.Value.Version);
                    if (runtimeLibrary_Version <= existingRuntimeLibrary_Version) { continue; }
                }

                var value = new WithBasePath<RuntimeLibrary>(folder, runtimeLibrary);
                this.RuntimeLibraryByName[runtimeLibrary.Name] = value;
                if (runtimeLibrary.Path is { Length: > 0 } path) {
                    this.RuntimeLibraryByName[path] = value;
                }

                foreach (var runtimeAssemblyGroup in runtimeLibrary.RuntimeAssemblyGroups) {
                    foreach (var runtimeFile in runtimeAssemblyGroup.RuntimeFiles) {
                        if (this.RuntimeFileByName.TryGetValue(runtimeFile.Path, out var existing)) {
                            var runtimeFile_Version = GetVersion(runtimeFile.AssemblyVersion);
                            var existingRuntimeFile_Version = GetVersion(existing.Value.AssemblyVersion);
                            if (runtimeFile_Version <= existingRuntimeFile_Version) { continue; }
                        }
                        if (isTypeProject) {
                            this.RuntimeFileByName[runtimeFile.Path] = new WithBasePath<RuntimeFile>(folder, runtimeFile);
                        } else if (IsTypePackage) {
                            var filename = runtimeFile.Path;
                            if (filename.StartsWith("lib/")) {
                                var splittedPath = filename.Split('/', 3);
                                if (3 == splittedPath.Length
                                    && splittedPath[2] is { Length: > 0 }) {
                                    filename = splittedPath[2];
                                }
                            }
                            this.RuntimeFileByName[filename] = new WithBasePath<RuntimeFile>(folder, runtimeFile);
                        }
                    }
                }
            }
        }

    }

    public static Version GetVersion(string? version) {
        if (Version.TryParse(version, out var result)) { return result; }
        return new Version();
    }
}

public record WithBasePath<T>(string BaseFolder, T Value);

/*
public class FolderBasedCompilationLibrary : CompilationLibrary {
    public FolderBasedCompilationLibrary(
        string type,
        string name,
        string version,
        string? hash,
        IEnumerable<string> assemblies,
        IEnumerable<Dependency> dependencies,
        bool serviceable,
        string? path,
        string? hashPath,
        string? folderPath
        )
        : base(type, name, version, hash, assemblies, dependencies, serviceable, path, hashPath) {
    }
}
*/
/*
https://samcragg.wordpress.com/2017/06/30/resolving-assemblies-in-net-core/
*/
internal sealed class AssemblyResolver : IDisposable {
    public readonly ICompilationAssemblyResolver Resolver;
    public readonly PlugInDependencyContext PlugInDependencyContext;
    public readonly AssemblyLoadContext AssemblyLoadContext;

    public AssemblyResolver(ICompilationAssemblyResolver resolver, PlugInDependencyContext plugInDependencyContext) {
        this.Resolver = resolver;
        this.PlugInDependencyContext = plugInDependencyContext;
        AssemblyLoadContext = AssemblyLoadContext.Default;
        AssemblyLoadContext.Resolving += this.OnResolving;
        System.AppDomain.CurrentDomain.AssemblyResolve += this.OnAssemblyResolve;
    }

    private Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args) {
        var fileName = $"{args.Name}.dll";
        if (this.PlugInDependencyContext.RuntimeFileByName.TryGetValue(fileName, out var runtimeFileByName)) {
            {
                var fullPath = System.IO.Path.Combine(runtimeFileByName.BaseFolder, runtimeFileByName.Value.Path);
                if (System.IO.File.Exists(fullPath)) {
                    var assemblyName =AssemblyName.GetAssemblyName(fullPath);
                    return System.Reflection.Assembly.Load(assemblyName);
                }
            }

            {
                var fullPath = System.IO.Path.Combine(runtimeFileByName.BaseFolder, fileName);
                if (System.IO.File.Exists(fullPath)) {
                    var assemblyName = AssemblyName.GetAssemblyName(fullPath);
                    return System.Reflection.Assembly.Load(assemblyName);
                }
            }
        }
        return null;
    }

    public void Dispose() {
        this.AssemblyLoadContext.Resolving -= this.OnResolving;
    }
    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName name) {
        var fileName = $"{name.Name}.dll";
        if (this.PlugInDependencyContext.RuntimeFileByName.TryGetValue(fileName, out var runtimeFileByName)) {
            {
                var fullPath = System.IO.Path.Combine(runtimeFileByName.BaseFolder, runtimeFileByName.Value.Path);
                if (System.IO.File.Exists(fullPath)) {
                    return context.LoadFromAssemblyPath(fullPath);
                }
            }

            {
                var fullPath = System.IO.Path.Combine(runtimeFileByName.BaseFolder, fileName);
                if (System.IO.File.Exists(fullPath)) {
                    return context.LoadFromAssemblyPath(fullPath);
                }
            }
        }

        return null;
    }
    /*
    private readonly ICompilationAssemblyResolver assemblyResolver;
    private readonly DependencyContext dependencyContext;

    public AssemblyResolver(string path)
    {
        this.Assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
        this.dependencyContext = DependencyContext.Load(this.Assembly) ?? throw new Exception();;

        this.assemblyResolver = new CompositeCompilationAssemblyResolver(new ICompilationAssemblyResolver[]
        {
            new AppBaseCompilationAssemblyResolver(System.IO.Path.GetDirectoryName(path)),
            new ReferenceAssemblyPathResolver(),
            new PackageCompilationAssemblyResolver()
        });

        this.AssemblyLoadContext = AssemblyLoadContext.GetLoadContext(this.Assembly);
        this.AssemblyLoadContext.Resolving += OnResolving;
    }

    public Assembly Assembly { get; }


    private Assembly OnResolving(AssemblyLoadContext context, AssemblyName name)
    {
        bool NamesMatch(RuntimeLibrary runtime)
        {
            return string.Equals(runtime.Name, name.Name, StringComparison.OrdinalIgnoreCase);
        }

        RuntimeLibrary library =
            this.dependencyContext.RuntimeLibraries.FirstOrDefault(NamesMatch);
        if (library != null)
        {
            var wrapper = new CompilationLibrary(
                library.Type,
                library.Name,
                library.Version,
                library.Hash,
                library.RuntimeAssemblyGroups.SelectMany(g => g.AssetPaths),
                library.Dependencies,
                library.Serviceable);

            var assemblies = new List<string>();
            this.assemblyResolver.TryResolveAssemblyPaths(wrapper, assemblies);
            if (assemblies.Count > 0)
            {
                return this.AssemblyLoadContext.LoadFromAssemblyPath(assemblies[0]);
            }
        }

        return null;
    }
    */
}
