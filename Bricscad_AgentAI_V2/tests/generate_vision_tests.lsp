;;; AutoLISP do generowania środowiska testowego dla Metric Vision (Bielik AI V2 GOLD)
;;; Uruchomienie komendą: GEN_VISION_TESTS

(defun C:GEN_VISION_TESTS ( / old_cmdecho )
  (setq old_cmdecho (getvar "CMDECHO"))
  (setvar "CMDECHO" 0)

  (princ "\nGenerowanie poligonu testowego Metric Vision...")

  ;; 1. UTWORZENIE WARSTW TESTOWYCH
  (command "_-LAYER" "_M" "Wizja_Glowna" "_C" "3" "" 
                     "_M" "Wizja_Smieci" "_C" "1" "" 
                     "_M" "Wizja_Detal" "_C" "6" "" 
                     "_M" "Wizja_Tlo" "_C" "2" "" 
                     "_M" "Wizja_Wazne" "_C" "4" "" "")

  ;; 2. GŁÓWNY KLASTER OBIEKTÓW (Od 0,0 do 500,500)
  (princ "\n- Rysowanie właściwego modelu (Główny Klaster)...")
  (command "_-LAYER" "_S" "Wizja_Glowna" "")
  
  ;; Siatka pomieszczeń / linii
  (command "_RECTANG" "0,0" "500,500")
  (command "_RECTANG" "20,20" "220,220")
  (command "_RECTANG" "240,20" "480,220")
  (command "_RECTANG" "20,240" "480,480")

  ;; Kółka reprezentujące np. słupy/drzewa
  (command "_CIRCLE" "120,120" "30")
  (command "_CIRCLE" "360,120" "30")
  (command "_CIRCLE" "250,360" "50")

  ;; Tekst dla rozpoznawania przez OCR (jeśli dojdzie VLM)
  (command "_MTEXT" "50,450" "_H" "15.0" "_W" "300" "OBSZAR GLOWNY METRIC VISION" "")

  ;; 3. OBIEKTY ODDALONE - OUTLIERY (Sztuczne zabrudzenia rysunku)
  ;; Mają na celu sprawdzenie działania filtru IQR (FilterOutliers = true)
  (princ "\n- Rysowanie outlierów (sprawdzian algorytmu IQR)...")
  (command "_-LAYER" "_S" "Wizja_Smieci" "")
  
  ;; Bardzo oddalony mały okrąg i linia
  (command "_CIRCLE" "50000,50000" "5")
  (command "_PLINE" "-20000,-15000" "-20000,-14000" "")
  (command "_MTEXT" "50005,50000" "_H" "20.0" "_W" "100" "BLAD GEOMETRII" "")

  ;; 4. RAMKA DETALU - Do testu Scope = Window
  (princ "\n- Rysowanie detalu okiennego (sprawdzian Scope=Window)...")
  (command "_-LAYER" "_S" "Wizja_Detal" "")
  
  ;; Rysujemy detal pomiędzy 1000,0 a 1200,200 (jest poza głównym klastrem, ale bliżej niż outliery)
  (command "_RECTANG" "1000,0" "1200,200")
  (command "_LINE" "1000,0" "1200,200" "")
  (command "_LINE" "1000,200" "1200,0" "")
  (command "_CIRCLE" "1100,100" "20")
  (command "_MTEXT" "1020,180" "_H" "10.0" "_W" "150" "DETAL DO SKANOWANIA" "")

  ;; 5. TEST WYGASZANIA WARSTW (FadeOtherLayers / GrayOtherLayers)
  (princ "\n- Rysowanie obiektów do testu przezroczystości warstw...")
  (command "_-LAYER" "_S" "Wizja_Tlo" "")
  
  ;; Gęsta siatka tła (szum)
  (command "_LINE" "2000,0" "2200,200" "")
  (command "_LINE" "2000,20" "2200,220" "")
  (command "_LINE" "2000,40" "2200,240" "")
  (command "_LINE" "2000,60" "2200,260" "")
  (command "_LINE" "2000,80" "2200,280" "")
  (command "_LINE" "2000,100" "2200,300" "")
  (command "_LINE" "2000,200" "2200,0" "")
  (command "_LINE" "2000,220" "2200,20" "")
  (command "_LINE" "2000,240" "2200,40" "")
  (command "_LINE" "2000,260" "2200,60" "")

  (command "_-LAYER" "_S" "Wizja_Wazne" "")
  ;; Ważne elementy schowane w szumie
  (command "_CIRCLE" "2100,100" "15")
  (command "_RECTANG" "2090,90" "2110,110")
  (command "_MTEXT" "2080,130" "_H" "12.0" "_W" "100" "WAZNY CEL" "")

  (command "_-LAYER" "_S" "0" "")
  (setvar "CMDECHO" old_cmdecho)
  (princ "\n[SUKCES] Poligon testowy Vision wygenerowany!")
  (princ "\nDo przetestowania agenta użyj zestawu z pliku vision_test_suite.md")
  (princ)
)

(princ "\nZaladowano generator testow wizyjnych. Wpisz komende: GEN_VISION_TESTS")
(princ)
