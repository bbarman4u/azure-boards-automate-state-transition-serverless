using ADOStateProcessor.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ADOStateProcessor.Repos.Interfaces
{
    public interface IRulesRepo
    {
        Task<RulesModel> ListRules(string wit, string wiDirectory, string processType);
    }
}