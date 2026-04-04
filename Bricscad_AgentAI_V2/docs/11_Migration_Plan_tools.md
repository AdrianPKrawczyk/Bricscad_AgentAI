#### ETAP 1: Oczy Agenta (Narzędzia analityczne) - *NAJWYŻSZY PRIORYTET*
W V2 LLM musi wiedzieć, z czym pracuje, żeby wygenerować poprawne argumenty JSON dla edytorów.
* [ ] **Migracja `GetPropertiesTool` / `GetPropertiesToolLite`**: Odzyskanie możliwości zrzutu właściwości zaznaczonych obiektów (Refleksja C# na `IToolV2`).
* [ ] **Migracja `ReadPropertyTool`**: Odczytywanie pojedynczej cechy (np. Length, Area) z użyciem uniwersalnych parametrów wirtualnych.
* [ ] **Migracja `AnalyzeSelectionTool` & `ListUniqueTool`**: Agregator zliczający typy w `ActiveSelection`.
* [ ] **Migracja `ReadTextSampleTool`**: Pobieranie próbek tekstów chronione przed przepełnieniem okna kontekstowego (integracja z nowym limitem `TrimHistory`).

#### ETAP 2: Fundamenty Ochronne API
* [ ] **Nowy Walidator Właściwości (Zastępstwo dla `TagValidator.cs`)**: Moduł, który w V2 nie będzie już badał składni tagów (bo robi to parser JSON), ale będzie sprawdzał obiekty `JObject` wstrzykiwane do `ModifyPropertiesTool`. Musi sprawdzać z bazą `BricsCAD_API_V22.txt`, czy dany obiekt rzeczywiście posiada modyfikowaną właściwość.

#### ETAP 3: Zaawansowana Geometria i Tekst
* [ ] **Migracja `MTextFormatTool` & `MTextEditTool`**: Wstrzykiwanie formatowania RTF (kolory, pogrubienia).
* [ ] **Migracja `TextEditTool` (DBText)**.
* [ ] **Moduł Skal Opisowych (Annotative)**: Migracja `AddAnnoScaleTool`, `ReadAnnoScalesTool`, `RemoveAnnoScaleTool`. Manipulacja w `ObjectContextManager`.

#### ETAP 4: Ewolucja Bloków (Block Management)
* [ ] **Migracja `EditBlockTool`**: Edycja wnętrza bloków bez ich rozbijania.
* [ ] **Migracja `ListBlocksTool`**.
* [ ] **Migracja `CreateBlockTool` & `InsertBlockTool`**.

#### ETAP 5: Mechanizmy Interakcji i Kontroli Przepływu
* [ ] **Migracja `UserInputTool` & `UserChoiceTool`**: Generowanie promptów dla człowieka, pauzujących pętlę ReAct.
* [ ] **Narzędzie Pętli (`ForeachTool`)**: Konwersja starego systemu iteracji po listach pamięci na mechanikę zrozumiałą dla modeli funkcyjnych.

#### ETAP 6: Ekosystem Testowy
* [ ] **Wdrożenie `AutoBenchmark.cs` do V2**: Przepisanie silnika weryfikacji na obiekty JObject (zamiast szukania stringów).
* [ ] **Wdrożenie `AgentTesterControl.cs` & `DatasetManagerControl`**.
