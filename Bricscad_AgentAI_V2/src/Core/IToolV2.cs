using Bricscad_AgentAI_V2.Models;
using Bricscad.ApplicationServices;
using Newtonsoft.Json.Linq;

namespace Bricscad_AgentAI_V2.Core
{
    /// <summary>
    /// Interfejs narzucający strukturę dla każdego narzędzia Agent AI V2 operującego na Tool Calling.
    /// </summary>
    public interface IToolV2
    {
        /// <summary>
        /// Zwraca listę tagów (kategorii) do których należy to narzędzie (np. #core, #bloki, #tekst).
        /// Używane przez Semantic Tool Routing do filtrowania zestawu narzędzi wysyłanego do LLM.
        /// </summary>
        string[] ToolTags { get; }

        /// <summary>
        /// Zwraca schemat funkcji w formacie zgodnym z wymogami Tool Calling modeli LLM (np. OpenAI).
        /// Określa parametry wejściowe, typy i listę wymaganych atrybutów.
        /// </summary>
        ToolDefinition GetToolSchema();

        /// <summary>
        /// Wykonuje logikę narzędzia (zazwyczaj ingerencję w BricsCAD API).
        /// </summary>
        /// <param name="doc">Aktywny dokument BricsCAD w którym pracuje narzędzie.</param>
        /// <param name="args">Zdeserializowane parametry funkcji wywołanej przez Agenta.</param>
        /// <returns>Odpowiedź informująca o sukcesie lub opisie napotkanego błędu, zwracana potem do LLM.</returns>
        string Execute(Document doc, JObject args);
    }
}
