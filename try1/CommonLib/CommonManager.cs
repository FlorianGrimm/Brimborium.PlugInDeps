using Microsoft.Extensions.Options;

namespace CommonLib;

public class CommonManager
{
    public CommonManager() {
    }

    public CommonManager(IEnumerable<PlugInModuleBase> listPlugInModule) {
        this.ListPlugInModule.AddRange(listPlugInModule);
    }

    public List<PlugInModuleBase> ListPlugInModule { get; } = new List<PlugInModuleBase>();

    public virtual void Initialize() {
        foreach(var plugInModule in this.ListPlugInModule) {
            plugInModule.Initialize();
        }
    }
}

public class PlugInOption { 
}


public class PlugInModuleBase
{
    public PlugInModuleBase() {
        this.Options = new PlugInOption();
    }

    public PlugInModuleBase(IOptions<PlugInOption> options) {
        this.Options = options.Value;
    }

    public PlugInOption Options { get; }

    public virtual void Initialize() {
    }
}

