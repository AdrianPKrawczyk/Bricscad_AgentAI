(defun c:TESTY_AI ( / )
  (setvar "CMDECHO" 0)
  
  ;; 1. Tworzenie warstw
  (command "_.LAYER" "_M" "Nawiew" "_C" 4 "" "")
  (command "_.LAYER" "_M" "Archiwum" "_C" 8 "" "")
  (command "_.LAYER" "_S" "0" "")

  ;; 2. Okręgi do testów zmiany właściwości
  (command "_.CIRCLE" '(10 10) 15)
  (command "_.CIRCLE" '(40 10) 15)
  (command "_.CIRCLE" '(70 10) 15)

  ;; 3. Linie (testowanie punktu początkowego)
  (command "_.LINE" '(0 0 0) '(100 100 0) "")
  (command "_.LINE" '(50 50 0) '(150 100 0) "")

  ;; 4. Teksty (z różną wysokością i słowem PVC)
  (command "_.TEXT" '(10 50) 10 0 "Rura PVC DN50")
  (command "_.TEXT" '(10 70) 25 0 "Zawór PVC")

  ;; 5. Zamknięte Polilinie (do testów FOREACH i RPN na polach powierzchni)
  ;; Prostokąt 1 (Pole: 2500, Środek: 125,25)
  (command "_.PLINE" '(100 0) '(150 0) '(150 50) '(100 50) "_C")
  ;; Prostokąt 2 (Pole: 900, Środek: 185,15)
  (command "_.PLINE" '(170 0) '(200 0) '(200 30) '(170 30) "_C")

  ;; 6. Definicja bloku "Silnik" (tworzy blok w pamięci rysunku)
  (if (not (tblsearch "BLOCK" "Silnik"))
    (progn
      (command "_.CIRCLE" '(0 0) 10)
      (command "_.LINE" '(-10 0) '(10 0) "")
      (command "_.LINE" '(0 -10) '(0 10) "")
      (command "_.BLOCK" "Silnik" '(0 0) (entlast) (entprevious) (entprevious) "")
    )
  )
  
  (princ "\n--- Srodowisko testowe wygenerowane! Wpisz polecenie, aby sprawdzic Agenta. ---")
  (princ)
)