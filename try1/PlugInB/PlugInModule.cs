using CommonLib;

using Microsoft.Extensions.Options;

namespace PlugInB;

public class PlugInModule:PlugInModuleBase
{
    public PlugInModule(IOptions<PlugInOption> options) : base(options) {
    }
}
