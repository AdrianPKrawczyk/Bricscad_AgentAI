using System;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Core.DynamicSystems
{
    public interface IDatasetProvider
    {
        JToken GetExactMatch(string datasetName, string searchColumn, string searchValue, System.Collections.Generic.Dictionary<string, string> filters = null);
        JToken GetNearestGreater(string datasetName, string searchColumn, double targetValue, System.Collections.Generic.Dictionary<string, string> filters = null);
        JToken GetNearestLower(string datasetName, string searchColumn, double targetValue, System.Collections.Generic.Dictionary<string, string> filters = null);
    }
}
