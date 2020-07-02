using AdoStateProcessor.Misc;
using AdoStateProcessor.Repos;
using AdoStateProcessor.Repos.Interfaces;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(AdoStateChangeHTTPFunction.Startup))]
namespace AdoStateChangeHTTPFunction
{
    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
             builder.Services.AddSingleton<IWorkItemRepo, WorkItemRepo>();
             builder.Services.AddSingleton<IRulesRepo, RulesRepo>();
             builder.Services.AddSingleton<IHelper, Helper>();
        }
    }
    
}
