using System;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Core.DynamicSystems
{
    public interface IDatasetProvider
    {
        JToken GetExactMatch(string datasetName, string searchColumn, string searchValue);
        JToken GetNearestGreater(string datasetName, string searchColumn, double targetValue);
        JToken GetNearestLower(string datasetName, string searchColumn, double targetValue);
    }
}
