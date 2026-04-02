(defun c:GenerujPoligonTestowy ( / blk)
  ;; 1. Tworzenie warstw testowych
  (command "_-LAYER" "_Make" "Osie" "_Color" "8" "" "")
  (command "_-LAYER" "_Make" "Urzadzenia" "_Color" "3" "" "")
  (command "_-LAYER" "_Make" "TEMP_1" "_Color" "1" "" "")
  (command "_-LAYER" "_Make" "TEMP_2" "_Color" "2" "" "")
  (command "_-LAYER" "_Make" "Opisy" "_Color" "7" "" "")
  (command "_-LAYER" "_Set" "0" "")

  ;; 2. Tworzenie podstawowej geometrii
  (command "_-LAYER" "_Set" "Osie" "")
  (command "_LINE" "0,0" "100,100" "")
  (command "_LINE" "100,0" "0,100" "")
  
  (command "_-LAYER" "_Set" "0" "")
  (command "_CIRCLE" "50,50" "25")
  (command "_CIRCLE" "150,50" "25")

  ;; 3. Zamknięte polilinie do testów powierzchni (Area)
  (command "_-LAYER" "_Set" "TEMP_1" "")
  (command "_RECTANG" "200,0" "300,100")
  (command "_-LAYER" "_Set" "TEMP_2" "")
  (command "_RECTANG" "350,0" "400,50")

  ;; 4. Teksty do podmiany i formatowania
  (command "_-LAYER" "_Set" "Opisy" "")
  (command "_-TEXT" "10,-20" "10" "0" "Tutaj byla Awaria pompy.")
  (command "_-MTEXT" "10,-40" "_Height" "10" "_Rotation" "0" "_Width" "0" "Zglaszam ze wystapila Awaria zaworu." "")

  ;; 5. Definicja i wstawienie bloku "Silnik" z atrybutem (symulacja)
  ;; Z racji czystego lisp'a tworzymy blok z tekstem symulujacym atrybut do podmiany przez ACTION:EDIT_BLOCK
  (entmake '((0 . "BLOCK") (2 . "Silnik") (70 . 0) (10 0.0 0.0 0.0)))
  (entmake '((0 . "CIRCLE") (8 . "0") (10 0.0 0.0 0.0) (40 . 15.0) (62 . 0))) ; Kolor ByBlock (0)
  (entmake '((0 . "TEXT") (8 . "0") (10 -5.0 -3.0 0.0) (40 . 6.0) (1 . "V1") (62 . 0)))
  (entmake '((0 . "ENDBLK")))
  
  (command "_-LAYER" "_Set" "Urzadzenia" "")
  (command "_-INSERT" "Silnik" "50,150" "1" "1" "0")
  (command "_-INSERT" "Silnik" "150,150" "1" "1" "0")
  (command "_-INSERT" "Silnik" "250,150" "1" "1" "0")

  (command "_-LAYER" "_Set" "0" "")
  (command "_ZOOM" "_Extents")
  (princ "\n[SUKCES] Poligon testowy wygenerowany! Posiadasz teraz linie na 'Osie', warstwy 'TEMP', bloki 'Silnik' oraz okregi i zamkniete polilinie.")
  (princ)
)
;; Automatyczne uruchomienie funkcji po wklejeniu
(c:GenerujPoligonTestowy)