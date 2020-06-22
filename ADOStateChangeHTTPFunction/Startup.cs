using System;
using ADOStateProcessor.Misc;
using ADOStateProcessor.Repos;
using ADOStateProcessor.Repos.Interfaces;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

[assembly: FunctionsStartup(typeof(ADOStateChangeHTTPFunction.Startup))]
namespace ADOStateChangeHTTPFunction
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
