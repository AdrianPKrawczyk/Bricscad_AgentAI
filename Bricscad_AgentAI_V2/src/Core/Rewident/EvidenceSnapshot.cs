using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Teigha.DatabaseServices;

namespace Bricscad_AgentAI_V2.Core.Rewident
{
    /// <summary>
    /// Para snapshot-ow wlasciwosci obiektu CAD wykonanych PRZED i PO mutacji.
    /// Uzywane przez Agenta Rewidenta do Chain of Evidence - porownania stanu obiektu
    /// przed wywolaniem narzedzia mutujacego i po nim, bez polegania na deklaracjach LLMa.
    /// Przeniesiony do src/Core/Rewident/ w v2.35.0.
    /// </summary>
    public class EvidenceSnapshot
    {
        public string Handle { get; set; }
        public string ObjectType { get; set; }
        public long TimestampUtcTicks { get; set; }
        public Dictionary<string, string> Properties { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Fix v2.35.1: dla narzedzi typu CreateObject obiekt przed mutacja nie istnial.
        // Przechowujemy syntetyczny snapshot 'before' z pustymi properties i ta flaga,
        // aby Rewident wiedzial ze para before/after jest spojna (nie "brak pary").
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public bool? ObjectExistedBefore { get; set; }

        [JsonIgnore]
        public bool IsEmpty => Properties == null || Properties.Count == 0;

        /// <summary>
        /// Tworzy syntetyczny snapshot 'before' dla obiektu, ktory nie istnial w DWG
        /// przed mutacja (np. swiezo utworzony przez CreateObject). Properties sa puste,
        /// ObjectExistedBefore=false.
        /// </summary>
        public static EvidenceSnapshot CreateNotExistedBefore(ObjectId id, string objectType)
        {
            return new EvidenceSnapshot
            {
                Handle = id.IsNull ? null : id.Handle.ToString(),
                ObjectType = objectType ?? "(unknown)",
                TimestampUtcTicks = DateTime.UtcNow.Ticks,
                Properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                ObjectExistedBefore = false
            };
        }

        /// <summary>
        /// Sprawdza czy snapshot reprezentuje "obiekt nie istnial przed" (create-scenario).
        /// Uzywane przez Rewidenta do rozroznienia CreateObject (legalne) od MissingPair (bug).
        /// </summary>
        [JsonIgnore]
        public bool IsNotExistedBefore => ObjectExistedBefore == false;

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None
            });
        }

        public static EvidenceSnapshot FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonConvert.DeserializeObject<EvidenceSnapshot>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Zwraca klucz Blackboard dla tego Handle w danym trybie (before/after).
        /// Format: @evidence_{mode}_{handle_hex} - wstepnie "@" umozliwia odczyt przez VariableStore.
        /// </summary>
        public static string BlackboardKey(string mode, string handleHex)
        {
            return "@evidence_" + mode + "_" + handleHex;
        }

        public static string BlackboardKey(string mode, ObjectId id)
        {
            if (id.IsNull) return null;
            return BlackboardKey(mode, id.Handle.ToString());
        }

        /// <summary>
        /// Zwraca liste kluczy Blackboard, ktore nalezy odczytac aby porownac pare before/after.
        /// </summary>
        public static (string beforeKey, string afterKey) BlackboardPair(string handleHex)
        {
            return (BlackboardKey("before", handleHex), BlackboardKey("after", handleHex));
        }
    }
}
