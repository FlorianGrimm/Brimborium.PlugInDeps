/*
dotnet build && dotnet run --project .\MainPrg
*/
using System.Formats.Asn1;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Runtime.Serialization;

using CommonLib;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;

namespace MainPrg;

internal class Program {
    private static void Main(string[] args) {
        var assemblyLocation = typeof(Program).Assembly.Location;

        var latestAssemblyResolver = new LatestAssemblyResolver();
        var plugInDependencyContext = latestAssemblyResolver.PlugInDependencyContext;

        var listPlugInFolder = new string[] { @"PlugInA\bin\Debug\net8.0", @"PlugInB\bin\Debug\net9.0" };
        var listAbsolutePluginFilename = GetPlugins(assemblyLocation, listPlugInFolder);
        PrintLoadedAssemblies();

        var dependencyContext = Microsoft.Extensions.DependencyModel.DependencyContext.Default;
        plugInDependencyContext.Add(typeof(Program).Assembly, dependencyContext);
        if (!(dependencyContext is { })) { throw new Exception("dependencyContext is null"); }

        //{
        //    System.Console.Out.WriteLine("MainPrg");
        //    foreach (var compileLibrary in dependencyContext.CompileLibraries) {
        //        System.Console.Out.WriteLine($"  c  {compileLibrary.Name} {compileLibrary.Version}");
        //        AddCompileLibraryByName(dictCompileLibraryByName, compileLibrary);
        //    }
        //    System.Console.Out.WriteLine("MainPrg");
        //    foreach (var runtimeLibrary in dependencyContext.RuntimeLibraries) {
        //        System.Console.Out.WriteLine($"  r  {runtimeLibrary.Name} {runtimeLibrary.Type} {runtimeLibrary.Version}");
        //        if (("project" == runtimeLibrary.Type)
        //            || ("package" == runtimeLibrary.Type)) {
        //            foreach (var group in runtimeLibrary.RuntimeAssemblyGroups) {
        //                foreach (var runtimeFile in group.RuntimeFiles) {
        //                    if (runtimeFile.AssemblyVersion is { Length: > 0 }) {
        //                        System.Console.Out.WriteLine($"    f  {runtimeFile.Path} {runtimeFile.AssemblyVersion} {runtimeFile.FileVersion}");
        //                    } else {
        //                        System.Console.Out.WriteLine($"    f  {runtimeFile.Path}");
        //                    }
        //                }
        //            }
        //        }
        //        //AddCompileLibraryByName(dictCompileLibraryByName, runtimeLibrary);
        //    }
        //}

        //var mergedDependencyContext = dependencyContext;
        foreach (var absolutePluginFilename in listAbsolutePluginFilename) {
            var reader = new DependencyContextJsonReader();
            var absolutePluginDepsFilename = $"{absolutePluginFilename[0..^4]}.deps.json";
            if (!System.IO.File.Exists(absolutePluginDepsFilename)) {
                throw new Exception($"{absolutePluginDepsFilename} not found.");
            }
            using var stream = System.IO.File.OpenRead(absolutePluginDepsFilename);
            var dependencyContextLib = reader.Read(stream);
            if (!(dependencyContextLib is { })) { throw new Exception("dependencyContextLib is null"); }
            var absolutePluginFolder = System.IO.Path.GetDirectoryName(absolutePluginDepsFilename)!;
            plugInDependencyContext.Add(absolutePluginFolder, dependencyContextLib);

            //System.Console.Out.WriteLine(absolutePluginDepsFilename);
            //foreach (var compileLibrary in dependencyContextLib.CompileLibraries) {
            //    System.Console.Out.WriteLine($"  c  {compileLibrary.Name} {compileLibrary.Version}");
            //    AddCompileLibraryByName(dictCompileLibraryByName, compileLibrary);
            //}
            //System.Console.Out.WriteLine(absolutePluginDepsFilename);
            //foreach (var runtimeLibrary in dependencyContextLib.RuntimeLibraries) {
            //    System.Console.Out.WriteLine($"  r  {runtimeLibrary.Name} {runtimeLibrary.Version}");
            //    //AddCompileLibraryByName(dictCompileLibraryByName, runtimeLibrary);
            //}
            //mergedDependencyContext = mergedDependencyContext.Merge(dependencyContextLib);
        }

        {
            System.Console.Out.WriteLine("PlugInDependencyContext");
            foreach (var runtimeFileByName in plugInDependencyContext.RuntimeFileByName) {
                System.Console.Out.WriteLine($"  .  {runtimeFileByName.Key} {runtimeFileByName.Value.BaseFolder} {runtimeFileByName.Value.Value.Path}");
            }
        }

        var assemblyResolver = new AssemblyResolver(
            resolver: new CompositeCompilationAssemblyResolver(
            new ICompilationAssemblyResolver[]
            {
                latestAssemblyResolver,
                new AppBaseCompilationAssemblyResolver(),
                new ReferenceAssemblyPathResolver(),
                new PackageCompilationAssemblyResolver()
            }), plugInDependencyContext: plugInDependencyContext);

        var listTypePlugInModule = new List<System.Type>();
        foreach (var absolutePluginFilename in listAbsolutePluginFilename) {
            var assembly = assemblyResolver.AssemblyLoadContext.LoadFromAssemblyPath(absolutePluginFilename);
            foreach (var type in assembly.ExportedTypes) {
                if ("PlugInModule" == type.Name) {
                    listTypePlugInModule.Add(type);
                }
            }
        }
        Start(args, listTypePlugInModule.ToArray());
        PrintLoadedAssemblies();
    }

    private static void PrintLoadedAssemblies() {
        System.Console.Out.WriteLine("Loaded Assemblies");
        foreach (var ass in System.AppDomain.CurrentDomain.GetAssemblies()) {
            System.Console.Out.WriteLine($"  .  {ass.FullName} {ass.Location}");
        }
    }

    private static List<string> GetPlugins(string assemblyLocation, IEnumerable<string> listPlugInFolder) {
        var result = new List<string>();
        var location = System.IO.Path.GetDirectoryName(assemblyLocation);
        var baseLocation = System.IO.Path.GetDirectoryName(
            System.IO.Path.GetDirectoryName(
                System.IO.Path.GetDirectoryName(
                    System.IO.Path.GetDirectoryName(location))));

        System.Console.Out.WriteLine($"location:{location}");
        System.Console.Out.WriteLine($"baseLocation:{baseLocation}");
        if (!(baseLocation is { Length: > 0 })) {
            throw new Exception("baseLocation is empty.");
        }


        foreach (var plugInFolder in listPlugInFolder) {
            var absolutePluginFolder = System.IO.Path.Combine(baseLocation, plugInFolder);
            if (!System.IO.Directory.Exists(absolutePluginFolder)) {
                throw new Exception($"{plugInFolder}  {absolutePluginFolder} not found.");
            }

            var name = plugInFolder.Split(new char[] { '/', '\\' })[0];
            var plugInFilename = $"{name}.dll";
            var absolutePluginFilename = System.IO.Path.Combine(absolutePluginFolder, plugInFilename);
            if (!System.IO.File.Exists(absolutePluginFilename)) {
                throw new Exception($"{plugInFolder}  {absolutePluginFilename} not found.");
            }
            System.Console.Out.WriteLine(absolutePluginFilename);
            result.Add(absolutePluginFilename);
        }

        return result;
    }

    private static void Start(string[] args, Type[] listTypePlugInModule) {
        var serviceCollection = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        serviceCollection.AddSingleton<CommonManager>();
        serviceCollection.AddOptions<PlugInOption>();
        foreach(var typePlugInModule in listTypePlugInModule) {
            serviceCollection.Add(new ServiceDescriptor(serviceType:typeof(PlugInModuleBase), implementationType: typePlugInModule, lifetime: ServiceLifetime.Transient));
        }
        var serviceProvider = serviceCollection.BuildServiceProvider();
        var commonManager = serviceProvider.GetRequiredService<CommonManager>();
        commonManager.Initialize();
        System.Console.Out.WriteLine($"commonManager: {commonManager.ListPlugInModule.Count}");
    }

}
