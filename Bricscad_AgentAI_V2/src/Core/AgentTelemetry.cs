using System;
using System.Collections.Generic;
using Bricscad_AgentAI_V2.Models;

namespace Bricscad_AgentAI_V2.Core
{
    public class DatasetSessionRecordArgs : EventArgs
    {
        public string Title { get; set; }
        public List<ChatMessage> HistorySnapshot { get; set; }
        public List<ToolDefinition> ToolsSnapshot { get; set; }
        public LLMStats Stats { get; set; }
    }

    public static class AgentTelemetry
    {
        public static event Action<string> OnStatusUpdated;
        public static event Action<string> OnToolLogged;
        public static event Action<LLMStats> OnStatsUpdated;
        public static event EventHandler<DatasetSessionRecordArgs> OnDatasetRecordAdded;

        public static void ReportStatus(string status)
        {
            OnStatusUpdated?.Invoke(status);
        }

        public static void ReportToolLog(string log)
        {
            OnToolLogged?.Invoke(log);
        }

        public static void ReportStats(LLMStats stats)
        {
            OnStatsUpdated?.Invoke(stats);
        }

        public static void ReportDatasetRecord(string title, List<ChatMessage> history, List<ToolDefinition> tools, LLMStats stats)
        {
            OnDatasetRecordAdded?.Invoke(null, new DatasetSessionRecordArgs
            {
                Title = title,
                HistorySnapshot = history,
                ToolsSnapshot = tools,
                Stats = stats
            });
        }
    }
}
