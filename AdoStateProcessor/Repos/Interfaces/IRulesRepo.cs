using AdoStateProcessor.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AdoStateProcessor.Repos.Interfaces
{
    public interface IRulesRepo
    {
        Task<RulesModel> ListRules(string wit, string wiDirectory, string processType);
    }
}