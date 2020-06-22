using ADOStateProcessor.Misc;
using ADOStateProcessor.Models;
using ADOStateProcessor.Repos.Interfaces;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ADOStateProcessor.Repos
{
    public class RulesRepo : IRulesRepo, IDisposable
    {
        private const string RULES_DIR = "rules/";
        private IHelper _helper;
        private HttpClient _httpClient;

        public RulesRepo(IHelper helper, HttpClient httpClient)
        {
            _helper = helper;
            _httpClient = httpClient;
        }

        public async Task<RulesModel> ListRules(string wit, string wiDirectory, string processType)
        {
            string srcPathRules = RULES_DIR + processType;

            // set baseUrl to current context if SourceForRule is a relative path, i.e, not starting with http
  /*           if (!src.ToLower().StartsWith("http"))
            {
                src = $"{_httpContextAccessor.HttpContext.Request.Scheme}://{_httpContextAccessor.HttpContext.Request.Host}/{src}";
            } */

            
            //var json = await _httpClient.GetStringAsync($"{src}/rules.{wit.ToLower()}.json");

            //JObject o1 = JObject.Parse(File.ReadAllText($"{src}/rules.{wit.ToLower()}.json"));
            // read file into a string and deserialize JSON to a type
            System.Console.WriteLine( DateTime.Now + " wiDirectory: " + wiDirectory);
            string ruleFile = Path.Combine(wiDirectory, srcPathRules, $"rules.{wit.ToLower()}.json");
            //RulesModel rules = JsonConvert.DeserializeObject<RulesModel>(File.ReadAllText($"{srcPathRules}/rules.{wit.ToLower()}.json"));
            System.Console.WriteLine( DateTime.Now + " ruleFile: " + ruleFile);
            RulesModel rules = JsonConvert.DeserializeObject<RulesModel>(File.ReadAllText(ruleFile));
            //RulesModel rules = JsonConvert.DeserializeObject<RulesModel>(json);

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
