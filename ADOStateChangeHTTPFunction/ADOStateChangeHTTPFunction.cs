using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Linq;
using ADOStateProcessor.ViewModels;
using Newtonsoft.Json.Linq;
using ADOStateProcessor.Repos.Interfaces;
using ADOStateProcessor.Misc;
using ADOStateProcessor.Processor;

namespace ADOStateChangeHTTPFunction
{
    public class ADOStateChangeHTTPFunction
    {
        private readonly IWorkItemRepo _workItemRepo;
        private readonly IRulesRepo _rulesRepo;
        private readonly IHelper _helper;

        public ADOStateChangeHTTPFunction(IWorkItemRepo workItemRepo, IRulesRepo rulesRepo, IHelper helper)
        {
            _workItemRepo = workItemRepo;
            _rulesRepo = rulesRepo;
            _helper = helper;
        }

        [FunctionName("ADOStateChangeHTTPFunction")]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log, ExecutionContext context)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            log.LogInformation(" C# HTTP trigger function processed a request.");
            //make sure pat is not empty
            string pat = System.Environment.GetEnvironmentVariable("ADO_PAT", EnvironmentVariableTarget.Process);
            if (string.IsNullOrEmpty(pat))
            {
                log.LogWarning(" Pat not found in env Process, trying user level");
                pat = System.Environment.GetEnvironmentVariable("ADO_PAT", EnvironmentVariableTarget.User);
            }
            if (string.IsNullOrEmpty(pat))
            {
                log.LogCritical(" Pat not found to process, exiting");
                return new BadRequestObjectResult("Pat not found to process, exiting");
            }
            //make sure processType is not empty, otherwise default to scrum
            string processType = System.Environment.GetEnvironmentVariable("ADO_PROCESS_TYPE", EnvironmentVariableTarget.Process);
            processType = string.IsNullOrEmpty(processType) ? System.Environment.GetEnvironmentVariable("ADO_PROCESS_TYPE", EnvironmentVariableTarget.User) : "scrum";
            log.LogInformation(" ProcessType:"+processType);
            //Parse request body as JObject
            JObject payload = JObject.Parse(requestBody);

            // Need to read the rules file from the rules folder in current context
            string functionAppCurrDirectory = context.FunctionAppDirectory;

            var adoEngine = new ADOProcessor(_workItemRepo, _rulesRepo, _helper, log);

            Task.WaitAll(
                adoEngine.ProcessUpdate(payload, pat, functionAppCurrDirectory, processType));

            string responseMessage = "This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }

    }



}
