using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.VisualStudio.Services.WebApi;

using AdoStateProcessor.Misc;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using System.Threading.Tasks;
using AdoStateProcessor.Repos.Interfaces;

namespace AdoStateProcessor.Repos
{
    public class WorkItemRepo : IWorkItemRepo, IDisposable
    {
        private IHelper _helper;

        public WorkItemRepo(IHelper helper)
        {
            _helper = helper;
        }

        public async Task<WorkItem> GetWorkItem(VssConnection connection, int id)
        {
            var client = connection.GetClient<WorkItemTrackingHttpClient>();
            try
            {
                return await client.GetWorkItemAsync(id, null, null, WorkItemExpand.Relations);

            }
            catch (Exception e)
            {
                System.Console.WriteLine("Exception e:"+ e.GetBaseException());
                return null;
            }
        }

        public async Task<List<WorkItem>> ListChildWorkItemsForParent(VssConnection connection, WorkItem parentWorkItem)
        {
            var client = connection.GetClient<WorkItemTrackingHttpClient>();

            // get all the related child work item links
            IEnumerable<WorkItemRelation> children = parentWorkItem.Relations.Where<WorkItemRelation>(x => x.Rel.Equals("System.LinkTypes.Hierarchy-Forward"));
            IList<int> Ids = new List<int>();

            // loop through children and extract the id's the from the url
            foreach (var child in children)
            {
                Ids.Add(_helper.GetWorkItemIdFromUrl(child.Url));
            }

            // in this case we only care about the state of the child work items
            string[] fields = new string[] { "System.State" };

            // go get the full list of child work items with the desired fields
            return await client.GetWorkItemsAsync(Ids, fields);
        }

        public async Task<WorkItem> UpdateWorkItemState(VssConnection connection, WorkItem workItem, string state)
        {
            JsonPatchDocument patchDocument = new JsonPatchDocument();
            patchDocument.Add(
                new JsonPatchOperation()
                {
                    Operation = Operation.Test,
                    Path = "/rev",
                    Value = workItem.Rev.ToString()
                }
            );

            patchDocument.Add(
                new JsonPatchOperation()
                {
                    Operation = Operation.Add,
                    Path = "/fields/System.State",
                    Value = state
                }
            );

            WorkItem result = null;

            var client = connection.GetClient<WorkItemTrackingHttpClient>();
            try
            {
                result = await client.UpdateWorkItemAsync(patchDocument, Convert.ToInt32(workItem.Id));
            }
            catch (Exception)
            {
                result = null;
            }
            return result;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~WorkItemRepo()
        {
            // Finalizer calls Dispose(false)
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _helper = null;
            }
        }
    }


}
