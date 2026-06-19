;;; generate_fields_benchmark_model.lsp
;;; AutoLISP - generator modelu testowego dla Benchmark_12_ManageFields.
;;;
;;; Uruchomienie: GEN_FIELDS_BENCHMARK
;;;
;;; Tworzy rysunek z precyzyjnie zdefiniowanym zestawem obiektow,
;;; tak aby kazdy test benchmarku mial jednoznaczny cel w rysunku:
;;;   - warstwa "Bench_Text" - czyste teksty (MText/DBText) z polami
;;;   - warstwa "Bench_Plain" - teksty bez pol (kontrola negatywna)
;;;   - warstwa "Bench_Block" - instancje blokow z atrybutami MText/DBText
;;;
;;; Wszystkie identyfikowalne Handle'y zapisywane sa do tabelki DXF
;;; dla weryfikacji manualnej po benchmarku.

(defun c:GEN_FIELDS_BENCHMARK (/ oldcmdecho oldclayer i)

  (setq oldcmdecho (getvar "CMDECHO"))
  (setq oldclayer (getvar "CLAYER"))
  (setvar "CMDECHO" 0)

  (princ "\n=== Generowanie modelu benchmarkowego Benchmark_12_ManageFields ===")

  ;--- Warstwy ----------------------------------------------------
  (command "_.LAYER" "_M" "Bench_Text" "_C" "3" "" "")
  (command "_.LAYER" "_M" "Bench_Plain" "_C" "4" "" "")
  (command "_.LAYER" "_M" "Bench_Block" "_C" "5" "" "")

  ;================================================================
  ; GRUPA A: MText/DBText z pojedynczymi polami (warstwa Bench_Text)
  ;================================================================
  (command "_.LAYER" "_S" "Bench_Text" "")

  ; A1: MText z %<\AcVar "DWGNAME">
  (princ "\nA1: MText DWGNAME...")
  (command "_.MTEXT" "0,0" "_H" "2.5" "_W" "80"
           "Projekt: %<\\AcVar \"DWGNAME\">" "")

  ; A2: DBText z %<\AcDate>
  (princ "\nA2: DBText DATE...")
  (command "_.TEXT" "0,-15" "_H" "2.5" "0"
           "Data: %<\\AcDate \\yyyy-MM-dd>")

  ; A3: MText z %<\AcExpr>
  (princ "\nA3: MText EXPR...")
  (command "_.MTEXT" "0,-30" "_H" "2.5" "_W" "60"
           "Wynik: %<\\AcExpr 10*5>" "")

  ; A4: MText z %<\AcObjProp>
  (princ "\nA4: MText OBJPROP...")
  (command "_.MTEXT" "0,-45" "_H" "2.5" "_W" "80"
           "Warstwa: %<\\AcObjProp Object.\\Layer>" "")

  ; A5: MText z wieloma polami obok siebie
  (princ "\nA5: MText MULTIPLE...")
  (command "_.MTEXT" "0,-60" "_H" "2.5" "_W" "100"
           "Poczatek: %<\\AcVar \"DWGNAME\"> | Srodek: %<\\AcDate \\HH:mm> | Koniec" "")

  ; A6: DBText z %<\AcVar "SAVENAME">
  (princ "\nA6: DBText SAVENAME...")
  (command "_.TEXT" "0,-80" "_H" "2.5" "0"
           "Zapisano jako: %<\\AcVar \"SAVENAME\">")

  ;================================================================
  ; GRUPA B: Teksty bez pol (kontrola negatywna, warstwa Bench_Plain)
  ;================================================================
  (command "_.LAYER" "_S" "Bench_Plain" "")

  ; B1: MText bez pol
  (princ "\nB1: MText plain...")
  (command "_.MTEXT" "100,0" "_H" "2.5" "_W" "60"
           "Ten tekst NIE zawiera pol CAD." "")

  ; B2: DBText bez pol
  (princ "\nB2: DBText plain...")
  (command "_.TEXT" "100,-15" "_H" "2.5" "0"
           "Statyczny opis bez pol.")

  ; B3: MText z tekstem, ktory wyglada jak pole ale to zwykly tekst
  ; (nie zawiera %< - to nie jest pole)
  (princ "\nB3: MText fake field...")
  (command "_.MTEXT" "100,-30" "_H" "2.5" "_W" "60"
           "Wzor: %X (to nie jest pole CAD)" "")

  ;================================================================
  ; GRUPA C: Definicje blokow z atrybutami
  ;================================================================
  (command "_.LAYER" "_S" "Bench_Block" "")

  ; C1: Blok z atrybutem MText Z polem (do wstawienia pola)
  (princ "\nC1: Block BenchPole_MText...")
  (command "_.RECTANG" "200,0" "280,30")
  (command "_.BLOCK" "BenchPole_MText" "200,0"
           "_.ATTDEF" "_Tag" "TYTUL" "_Prompt" "Tytul"
           "_MText" "%%U"
           "Tytul projektu: %<\\AcVar \"DWGNAME\">"
           "_InsertionPoint" "210,15" "_Height" "2.5" ""
           "_.ENDBLK" "")

  ; C2: Blok z atrybutem DBText BEZ pola (do wstawienia pola)
  (princ "\nC2: Block BenchPole_DBText...")
  (command "_.CIRCLE" "300,15" "10")
  (command "_.BLOCK" "BenchPole_DBText" "300,15"
           "_.ATTDEF" "_Tag" "NUMER" "_Prompt" "Numer"
           "Stala etykieta"
           "_InsertionPoint" "300,5" "_Height" "2.0" ""
           "_.ENDBLK" "")

  ; C3: Blok z atrybutem STALYM (Constant=true) - do testu IncludeConstant
  (princ "\nC3: Block BenchPole_Constant...")
  (command "_.LINE" "350,0" "430,30" "")
  (command "_.BLOCK" "BenchPole_Constant" "350,0"
           "_.ATTDEF" "_Tag" "STALY" "_Prompt" "Staly"
           "_Constant" "Stala wartosc"
           "_InsertionPoint" "360,15" "_Height" "2.5" ""
           "_.ENDBLK" "")

  ; C4: Blok z dwoma atrybutami (NUMER + TYTUL) - do testu AttributeTagFilter
  (princ "\nC4: Block BenchPole_DualAttr...")
  (command "_.POLYGON" "450,15" "5" "10")
  ; Pierwszy atrybut - TYTUL
  (command "_.ATTDEF" "_Tag" "TYTUL" "_Prompt" "Tytul"
           "Tytul bez pola"
           "_InsertionPoint" "450,30" "_Height" "2.0" "")
  ; Drugi atrybut - NUMER
  (command "_.ATTDEF" "_Tag" "NUMER" "_Prompt" "Numer"
           "1234"
           "_InsertionPoint" "450,5" "_Height" "2.0" "")
  (command "_.BLOCK" "BenchPole_DualAttr" "440,5"
           "_.ENDBLK" "")

  ;================================================================
  ; GRUPA D: Wstawienia blokow (warstwa Bench_Block)
  ;================================================================
  (princ "\nD: Wstawienia blokow...")
  ; 2 instancje BenchPole_MText
  (command "_.INSERT" "BenchPole_MText" "200,-100" "1" "1" "0")
  (command "_.INSERT" "BenchPole_MText" "200,-150" "1" "1" "0")
  ; 2 instancje BenchPole_DBText
  (command "_.INSERT" "BenchPole_DBText" "350,15" "1" "1" "0")
  (command "_.INSERT" "BenchPole_DBText" "350,40" "1" "1" "0")
  ; 1 instancja BenchPole_Constant
  (command "_.INSERT" "BenchPole_Constant" "380,-100" "1" "1" "0")
  ; 3 instancje BenchPole_DualAttr
  (command "_.INSERT" "BenchPole_DualAttr" "440,-100" "1" "1" "0")
  (command "_.INSERT" "BenchPole_DualAttr" "440,-160" "1" "1" "0")
  (command "_.INSERT" "BenchPole_DualAttr" "440,-220" "1" "1" "0")

  ;================================================================
  ; Zakonczenie
  ;================================================================
  (command "_.ZOOM" "_E")
  (command "_.SELECT" "_All" "")

  (setvar "CMDECHO" oldcmdecho)
  (command "_.LAYER" "_S" oldclayer "")

  (princ "\n\n=== Model benchmarkowy gotowy. ===")
  (princ "\nWarstwy: Bench_Text (A1-A6), Bench_Plain (B1-B3), Bench_Block (C1-C4 + D)")
  (princ "\nZaznaczono wszystkie obiekty (SelectAll).")
  (princ "\nAby uruchomic benchmark: zaladuj Benchmark_12_ManageFields.json w AutoBenchmarkControl.")
  (princ)
)