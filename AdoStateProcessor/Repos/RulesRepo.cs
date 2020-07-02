using AdoStateProcessor.Misc;
using AdoStateProcessor.Models;
using AdoStateProcessor.Repos.Interfaces;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace AdoStateProcessor.Repos
{
    public class RulesRepo : IRulesRepo, IDisposable
    {
        private const string RULES_DIR = "rules/";
        private IHelper _helper;

         public RulesRepo(IHelper helper, HttpClient httpClient)
        {
            _helper = helper;
        } 

        public async Task<RulesModel> ListRules(string wit, string wiDirectory, string processType)
        {
            string srcPathRules = RULES_DIR + processType;
            // read file into a string and deserialize JSON to a type
            System.Console.WriteLine( DateTime.Now + " wiDirectory: " + wiDirectory);
            string ruleFile = Path.Combine(wiDirectory, srcPathRules, $"rules.{wit.ToLower()}.json");
            System.Console.WriteLine( DateTime.Now + " ruleFile: " + ruleFile);
            RulesModel rules = JsonConvert.DeserializeObject<RulesModel>(File.ReadAllText(ruleFile));

            return rules;
            
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~RulesRepo()
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
