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
using Microsoft.TeamFoundation.Common;

namespace AdoStateProcessor.Processor
{
    public class AdoProcessor
    {
        private const string ADO_BASE_URL = "https://dev.azure.com/";
        private readonly IWorkItemRepo _workItemRepo;
        private readonly IRulesRepo _rulesRepo;
        private readonly IHelper _helper;
        private readonly ILogger logger;

        public AdoProcessor(IWorkItemRepo workItemRepo, IRulesRepo rulesRepo, IHelper helper, ILogger logger)
        {
            _workItemRepo = workItemRepo;
            _rulesRepo = rulesRepo;
            _helper = helper;
            this.logger = logger;
        }
        public async Task ProcessUpdate(JObject payload, string pat, string functionAppCurrDirectory, string processType)
        {
            try
            {
                PayloadViewModel vm = null;

                string eventType = this.GetPayloadValue<string>(payload, "eventType", token => token.ToString());
                if (string.IsNullOrEmpty(eventType))
                    return;
                else if (eventType == "workitem.updated")
                    vm = BuildExistingItemPayloadVM(payload);
                else if (eventType == "workitem.created")
                    vm = BuildNewItemPayloadVM(payload);

                if (vm == null)
                    return;

                logger.LogTrace(" Masked PAT:" + Mask(pat));
                vm.pat = pat;

                // create our azure devops connection
                Uri baseUri = new Uri(ADO_BASE_URL + vm.organization);

                VssCredentials clientCredentials = new VssCredentials(new VssBasicCredential("username", vm.pat));
                VssConnection vssConnection = new VssConnection(baseUri, clientCredentials);

                WorkItem parentWorkItem = await _workItemRepo.GetWorkItem(vssConnection, vm.parentId);

                if (parentWorkItem == null)
                {
                    logger.LogError(" no parent found");
                    return;
                };

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
            catch (Exception ex)
            {
                logger.LogError(" " + ex.ToString());
            }
        }

        private string Mask(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;

            const char maskChar = '*';
            if (s.Length < 4)
                return "".PadLeft(s.Length, maskChar);

            return string.Format("{0}{1}{2}", s[0], "".PadLeft(s.Length - 2, maskChar), s[s.Length - 1]);
        }
        private PayloadViewModel BuildExistingItemPayloadVM(JObject body)
        {
            PayloadViewModel vm = new PayloadViewModel();

            vm.workItemId = this.GetPayloadValue<int>(body, "resource.workItemId", token => Convert.ToInt32(token.ToString()));
            if (vm.workItemId == 0)
                return null;

            vm.workItemType = this.GetPayloadValue<string>(body, "resource.revision.fields.['System.WorkItemType']", token => token.ToString());
            if (string.IsNullOrEmpty(vm.workItemType))
                return null;

            vm.eventType = this.GetPayloadValue<string>(body, "eventType", token => token.ToString());
            if (string.IsNullOrEmpty(vm.eventType))
                return null;

            if (!this.PayloadHasValue(body, "resource.rev"))
                return null;
            else
                vm.rev = this.GetPayloadValue<int>(body, "resource.rev", token => Convert.ToInt32(token.ToString()));

            vm.url = this.GetPayloadValue<string>(body, "resource.url", token => token.ToString());
            if (string.IsNullOrEmpty(vm.url))
                return null;

            string org = GetOrganization(vm.url);
            if (!string.IsNullOrEmpty(org))
                vm.organization = org;
            else
                return null;

            vm.teamProject = this.GetPayloadValue<string>(body, "resource.revision.fields.['System.AreaPath']", token => token.ToString());
            if (string.IsNullOrEmpty(vm.teamProject))
                return null;

            vm.state = this.GetPayloadValue<string>(body, "resource.fields.['System.State'].newValue", token => token.ToString());
            if (string.IsNullOrEmpty(vm.state))
                return null;

            vm.parentId = this.GetPayloadValue<int>(body, "resource.revision.fields.['System.Parent']", token => Convert.ToInt32(token.ToString()));
            if (vm.parentId == 0)
                return null;

            logger.LogInformation(" Requested Payload eventType:" + vm.eventType);
            logger.LogInformation(" Requested Payload state:" + vm.state);
            logger.LogInformation(" Requested Payload workItemType:" + vm.workItemType);
            logger.LogInformation(" Requested Payload workItemId:" + vm.workItemId);
            logger.LogInformation(" Requested Payload orgnization:" + vm.organization);

            String tempTeamProject = this.GetPayloadValue<string>(body, "resource.revision.fields.['System.TeamProject']", token => token.ToString()) ?? "NOT FOUND";
            logger.LogInformation(" Requested Payload teamProject:" + tempTeamProject);            
            logger.LogInformation(" Requested Payload parentId:" + vm.parentId);

            return vm;
        }

        private PayloadViewModel BuildNewItemPayloadVM(JObject body)
        {
            PayloadViewModel vm = new PayloadViewModel();

            vm.workItemType = this.GetPayloadValue<string>(body, "resource.fields.['System.WorkItemType']", token => token.ToString());
            if (string.IsNullOrEmpty(vm.workItemType))
                return null;

            vm.eventType = this.GetPayloadValue<string>(body, "eventType", token => token.ToString());
            if (string.IsNullOrEmpty(vm.eventType))
                return null;

            if (!this.PayloadHasValue(body, "resource.rev"))
                return null;
            else
                vm.rev = this.GetPayloadValue<int>(body, "resource.rev", token => Convert.ToInt32(token.ToString()));

            vm.url = this.GetPayloadValue<string>(body, "resource.url", token => token.ToString());
            if (string.IsNullOrEmpty(vm.url))
                return null;

            string org = GetOrganization(vm.url);
            if (!string.IsNullOrEmpty(org))
                vm.organization = org;
            else
                return null;

            vm.teamProject = this.GetPayloadValue<string>(body, "resource.fields.['System.AreaPath']", token => token.ToString());
            if (string.IsNullOrEmpty(vm.teamProject))
                return null;

            vm.state = this.GetPayloadValue<string>(body, "resource.fields.['System.State']", token => token.ToString());
            if (string.IsNullOrEmpty(vm.state) || vm.state != "New")
                return null;

            vm.parentId = this.GetPayloadValue<int>(body, "resource.fields.['System.Parent']", token => Convert.ToInt32(token.ToString()));
            if (vm.parentId == 0)
                return null;

            logger.LogInformation(" Requested Payload eventType:" + vm.eventType);
            logger.LogInformation(" Requested Payload state:" + vm.state);
            logger.LogInformation(" Requested Payload workItemType:" + vm.workItemType);
            logger.LogInformation(" Requested Payload orgnization:" + vm.organization);

            String tempTeamProject = this.GetPayloadValue<string>(body, "resource.fields.['System.TeamProject']", token => token.ToString()) ?? "NOT FOUND";
            logger.LogInformation(" Requested Payload teamProject:" + tempTeamProject);
            logger.LogInformation(" Requested Payload parentId:" + vm.parentId);

            return vm;
        }

        private bool PayloadHasValue(JToken payload, string tokenQuery)
        {
            return payload.SelectToken(tokenQuery) != null;
        }

        private T GetPayloadValue<T>(JObject payload, string tokenQuery, Func<JToken, T> converter)
        {
            JToken token = payload.SelectToken(tokenQuery);

            if (token == null)
            {
                logger.LogError($" parameter {tokenQuery} not found");
                return default(T);
            }
            else
            {
                try
                {
                    return converter.Invoke(token);
                }
                catch
                {
                    logger.LogError($" cast failed for parameter {tokenQuery}");
                    return default(T);
                }
            }
        }

        private string GetOrganization(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return string.Empty;
            else
            {
                url = url.Replace("http://", string.Empty);
                url = url.Replace("https://", string.Empty);

                if (url.Contains(value: "visualstudio.com"))
                {
                    string[] split = url.Split('.');
                    return split[0].ToString();
                }
                else if (url.Contains("dev.azure.com"))
                {
                    url = url.Replace("dev.azure.com/", string.Empty);
                    string[] split = url.Split('/');
                    return split[0].ToString();
                }
                else
                    return string.Empty;
            }
        }
    }
}