using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AdoStateProcessor.Repos.Interfaces;
using AdoStateProcessor.Misc;
using AdoStateProcessor.ViewModels;
using Newtonsoft.Json.Linq;
using AdoStateProcessor.Models;

namespace AdoStateProcessor.Processor
{
    public class AdoProcessor
    {
        private const string ADO_BASE_URL = "https://dev.azure.com/";
        private readonly IWorkItemRepo _workItemRepo;
        private readonly IRulesRepo _rulesRepo;
        private readonly IHelper _helper;
        private readonly ILogger logger;

        public AdoProcessor(IWorkItemRepo workItemRepo, IRulesRepo rulesRepo, IHelper helper,ILogger logger)
        {
            _workItemRepo = workItemRepo;
            _rulesRepo = rulesRepo;
            _helper = helper;
            this.logger = logger;
        } 
        public async Task ProcessUpdate(JObject payload, string pat, string functionAppCurrDirectory, string processType)
        {
            PayloadViewModel vm = BuildPayloadViewModel(payload);

            logger.LogTrace(" Masked PAT:"+ Mask(pat));
            vm.pat = pat;
            //if the event type is something other the updated, then lets just return an ok
            if (vm.eventType != "workitem.updated") return;

            // create our azure devops connection
            Uri baseUri = new Uri(ADO_BASE_URL + vm.organization);

            VssCredentials clientCredentials = new VssCredentials(new VssBasicCredential("username", vm.pat));
            VssConnection vssConnection = new VssConnection(baseUri, clientCredentials);

            // load the work item posted 
            WorkItem workItem = await _workItemRepo.GetWorkItem(vssConnection, vm.workItemId);

            // this should never happen, but if we can't load the work item from the id, then exit with error
            if (workItem == null) return ;

            // get the related parent
            WorkItemRelation parentRelation = workItem.Relations.Where<WorkItemRelation>(x => x.Rel.Equals("System.LinkTypes.Hierarchy-Reverse")).FirstOrDefault();

            // if we don't have any parents to worry about, then just abort
            if (parentRelation == null) return;

            Int32 parentId = _helper.GetWorkItemIdFromUrl(parentRelation.Url);
            WorkItem parentWorkItem = await _workItemRepo.GetWorkItem(vssConnection, parentId);

            if (parentWorkItem == null) return;

            string parentState = parentWorkItem.Fields["System.State"] == null ? string.Empty : parentWorkItem.Fields["System.State"].ToString();

            // load rules for updated work item
            RulesModel rulesModel = await _rulesRepo.ListRules(vm.workItemType, functionAppCurrDirectory, processType);
            //loop through each rule
            foreach (var rule in rulesModel.Rules)
            {
                logger.LogInformation(" Executing against rule:" + rule.IfChildState);
                if (rule.IfChildState.Equals(vm.state))
                {
                    if (!rule.AllChildren)
                    {
                         logger.LogInformation(" In !rule.AllChildren:" + vm.state);
                        if (!rule.NotParentStates.Contains(parentState))
                        {
                            await _workItemRepo.UpdateWorkItemState(vssConnection, parentWorkItem, rule.SetParentStateTo);
                        }
                    }
                    else
                    {
                        // get a list of all the child items to see if they are all closed or not
                        List<WorkItem> childWorkItems = await _workItemRepo.ListChildWorkItemsForParent(vssConnection, parentWorkItem);

                        // check to see if any of the child items are not closed, if so, we will get a count > 0
                        int count = childWorkItems.Where(x => !x.Fields["System.State"].ToString().Equals(rule.IfChildState)).ToList().Count;

                        if (count.Equals(0))
                            await _workItemRepo.UpdateWorkItemState(vssConnection, parentWorkItem, rule.SetParentStateTo);
                    }

                }
               
            }
        }


        private  string Mask(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            const char maskChar = '*';
            if (s.Length < 4)
                return "".PadLeft(s.Length, maskChar);

            return string.Format("{0}{1}{2}", s[0], "".PadLeft(s.Length - 2, maskChar), s[s.Length - 1]);
        } 
         private PayloadViewModel BuildPayloadViewModel(JObject body)
        {
            PayloadViewModel vm = new PayloadViewModel();

            string url = body["resource"]["url"] == null ? null : body["resource"]["url"].ToString();
            string org = GetOrganization(url);

            vm.workItemId = body["resource"]["workItemId"] == null ? -1 : Convert.ToInt32(body["resource"]["workItemId"].ToString());
            vm.workItemType = body["resource"]["revision"]["fields"]["System.WorkItemType"] == null ? null : body["resource"]["revision"]["fields"]["System.WorkItemType"].ToString();
            vm.eventType = body["eventType"] == null ? null : body["eventType"].ToString();
            vm.rev = body["resource"]["rev"] == null ? -1 : Convert.ToInt32(body["resource"]["rev"].ToString());
            vm.url = body["resource"]["url"] == null ? null : body["resource"]["url"].ToString();
            vm.organization = org;
            vm.teamProject = body["resource"]["fields"]["System.AreaPath"] == null ? null : body["resource"]["fields"]["System.AreaPath"].ToString();
            vm.state = body["resource"]["fields"]["System.State"]["newValue"] == null ? null : body["resource"]["fields"]["System.State"]["newValue"].ToString();
            
            //debug the parsing logic
            String tempParentId = body["resource"]["revision"]["fields"]["System.Parent"] == null ? null : body["resource"]["revision"]["fields"]["System.Parent"].ToString();
            String tempTeamProject = body["resource"]["revision"]["fields"]["System.TeamProject"] == null ? null : body["resource"]["revision"]["fields"]["System.TeamProject"].ToString();
            logger.LogInformation(" Requested Payload eventType:" + vm.eventType);
            logger.LogInformation(" Requested Payload state:" + vm.state);
            logger.LogInformation(" Requested Payload workItemType:" + vm.workItemType);
            logger.LogInformation(" Requested Payload workItemId:" + vm.workItemId);
            logger.LogInformation(" Requested Payload orgnization:" + vm.organization);
            logger.LogInformation(" Requested Payload teamProject:" + tempTeamProject);
            logger.LogInformation(" Requested Payload parentId:" + tempParentId);
            return vm;
        }

        private string GetOrganization(string url)
        {
            url = url.Replace("http://", string.Empty);
            url = url.Replace("https://", string.Empty);

            if (url.Contains(value: "visualstudio.com"))
            {
                string[] split = url.Split('.');
                return split[0].ToString();
            }

            if (url.Contains("dev.azure.com"))
            {
                url = url.Replace("dev.azure.com/", string.Empty);
                string[] split = url.Split('/');
                return split[0].ToString();
            }

            return string.Empty;
        }
    }
}