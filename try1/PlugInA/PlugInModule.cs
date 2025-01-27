using CommonLib;

using Microsoft.Extensions.Options;

using System.Reflection;

namespace PlugInA;

public class PlugInModule:PlugInModuleBase
{
    public PlugInModule(IOptions<PlugInOption> options) : base(options) {
    }

    public Assembly? Assembly { get; private set; }

    public override void Initialize() {
        base.Initialize();
        this.Assembly = typeof(System.Text.Json.JsonDocument).Assembly;
        System.Console.Out.WriteLine($"PlugInA {this.Assembly.GetName().FullName} {this.Assembly.Location}");
    }
}
