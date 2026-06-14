# Zestaw Testowy: Metric Vision AI

Niniejszy dokument zawiera scenariusze i prompty testowe służące do weryfikacji zdolności wizyjnych Agenta (szczególnie `SupervisorProfile` i `CadProfile`) w środowisku BricsCAD Agent AI V2.

## Przygotowanie Środowiska
1. W BricsCAD załaduj plik testowy LISP: `(load "tests/generate_vision_tests.lsp")`
2. Wykonaj komendę: `GEN_VISION_TESTS`
3. Oddal widok tak, żeby na ekranie znajdowało się wszystko (Zoom Extents), albo zresetuj widok. Możesz też zostawić dowolny widok, bo funkcja pobiera dane metryczne z modelu.

---

## Scenariusz 1: Omijanie śmieci (Algorytm IQR)
**Cel:** Sprawdzenie, czy Agent potrafi użyć narzędzia `ScanMetricVisionDrawing` dla `Scope: Model` z zachowaniem włączonego odrzucania śmieci (`FilterOutliers = true`). Dzięki temu nie wyrenderuje on wielkiego pustego obszaru obejmującego punkt `(50000, 50000)`.

**Prompt Użytkownika:**
> "Zrób mi zrzut całego modelu, ale pamiętaj o zignorowaniu oddalonych, zabłąkanych elementów. Powiedz mi co widzisz na wyrenderowanym skanie."

**Oczekiwane zachowanie Agenta:**
1. Wywołanie `ScanMetricVisionDrawing` z argumentami: `Scope: "Model"`, `FilterOutliers: true` (lub pominięcie, gdyż domyślnie jest true).
2. Wygenerowany JSON ze statusem sukcesu powinien wskazywać `cad_bounds` w przybliżeniu na zakres `[0,0]` do `[1200, 500]` (zależnie od marginesu), skutecznie ucinając okrąg na X=50000.
3. Supervisor powinien odczytać wyrenderowane płytki wizyjne (jeśli podłączono model VLM) i opisać siatkę prostokątów oraz napis.

---

## Scenariusz 2: Margines Zabezpieczający (MarginPercent)
**Cel:** Weryfikacja instrukcji `MarginPercent`. LLM powinien dodać margines podczas skanowania bardzo konkretnej warstwy lub małej selekcji.

**Prompt Użytkownika:**
> "Znajdź na rysunku wszystkie elementy na warstwie 'Wizja_Detal', zeskanuj je z bezpiecznym marginesem 30% i opisz mi co tam się znajduje."

**Oczekiwane zachowanie Agenta:**
1. Wywołanie `ScanMetricVisionDrawing` z: `Scope: "Layer"`, `LayerNames: ["Wizja_Detal"]`, `MarginPercent: 30`.
2. Agent otrzymuje skan ze wskaźnikiem na detal na osi `X=1000..1200`, powiększony o 30% buforu z każdej strony, widząc tym samym czysto wycentrowany obiekt z otoczeniem.

---

## Scenariusz 3: Manualne Okno Tnące (Scope = Window)
**Cel:** Sprawdzenie, czy sub-agent / supervisor potrafi logicznie wyliczyć obwiednie tnące dla obiektu i poprawnie przekazać je w `WindowBounds`.

**Prompt Użytkownika:**
> "Chcę żebyś wylistował wszystkie okręgi na rysunku, znalazł ten który znajduje się w punkcie (120,120) i zrobił mu skan z marginesem 20%. Użyj manualnego okna tnącego dla tego okręgu."

**Oczekiwane zachowanie Agenta:**
1. Wywołanie narzędzia czytającego z bazy (lub CAD Execute) aby pobrać bounding box okręgu w `120,120` (promień 30, więc box to od 90,90 do 150,150).
2. Wywołanie `ScanMetricVisionDrawing` z:
   - `Scope: "Window"`
   - `MarginPercent: 20`
   - `WindowBounds`: `{"MinX": 90.0, "MinY": 90.0, "MaxX": 150.0, "MaxY": 150.0}`
3. Zwrócenie udanego skanu.

---

## Scenariusz 4: Izolacja warstwy i przezroczystość tła (FadeOtherLayers)
**Cel:** Sprawdzenie skuteczności opcji `FadeOtherLayers`, która wygasza warstwy otoczenia uwydatniając warstwę docelową bez permanentnego usuwania tła.

**Prompt Użytkownika:**
> "Znajdź obiekty na warstwie 'Wizja_Wazne' i zrób im skan, używając opcji wygaszenia pozostałych warstw (FadeOtherLayers). Powiedz, co tam widzisz i co jest wokół."

**Oczekiwane zachowanie Agenta:**
1. Wywołanie `ScanMetricVisionDrawing` (lub `CaptureMetricVisionArea`) ze `Scope: "Layer"`, `LayerNames: ["Wizja_Wazne"]` oraz `FadeOtherLayers: true`.
2. Oczekiwany obraz zawiera kółko i napis "WAZNY CEL" na pełnym kontraście (warstwa `Wizja_Wazne`), podczas gdy gęsta siatka linii (z `Wizja_Tlo`) jest wyraźnie wyblakła (70% przezroczystości).

---

## Scenariusz 5: Izolacja warstwy przez zmianę koloru na szary (GrayOtherLayers)
**Cel:** Weryfikacja działania nakładania koloru szarego (ColorIndex 8) na warstwy wyłączone z selekcji.

**Prompt Użytkownika:**
> "Teraz wykonaj ten sam zrzut dla 'Wizja_Wazne', ale zamiast wygaszać resztę, użyj opcji przyszarzenia tła (GrayOtherLayers)."

**Oczekiwane zachowanie Agenta:**
1. Wywołanie skanowania z `LayerNames: ["Wizja_Wazne"]` oraz `GrayOtherLayers: true`.
2. Obraz wyjściowy ma tło w pełni widoczne (bez przezroczystości), ale wymuszone szarym kolorem (ColorIndex 8). Obiekty główne zachowują oryginalne barwy.

---

## Podsumowanie i Troubleshooting
Wszystkie błędy związane z "atlas byłby zbyt duży" oznaczają, że Agent wyłączył filtr `FilterOutliers` lub błędnie ustawił `TileCadSize` przy dużych odległościach pomiędzy obiektami. Prompt inżynieryjny wprowadzony w v2.28.36 powinien temu zapobiegać.
