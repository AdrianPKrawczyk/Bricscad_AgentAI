(defun c:TestNumAdd ( / make-dbtext make-mtext)
  (setvar "CMDECHO" 0)

  ;; Funkcja pomocnicza: Tworzy DBText
  (defun make-dbtext (lay txt x y)
    (entmake (list '(0 . "TEXT") (cons 8 lay) (cons 10 (list x y 0)) (cons 40 2.5) (cons 1 txt)))
  )

  ;; Funkcja pomocnicza: Tworzy MText WRAZ z wymuszonym czerwonym formatowaniem
  (defun make-mtext (lay txt x y)
    (entmake (list '(0 . "MTEXT") '(100 . "AcDbEntity") (cons 8 lay) '(100 . "AcDbMText")
                   (cons 10 (list x y 0)) '(40 . 2.5) '(41 . 50.0) '(71 . 1)
                   (cons 1 (strcat "{\\C1;" txt "}"))))
  )

  ;; 1. KONSTRUKCJA (2x DBText)
  (command "-layer" "m" "KONSTRUKCJA" "")
  (make-dbtext "KONSTRUKCJA" "IPE 200" 0 100)
  (make-dbtext "KONSTRUKCJA" "IPE 240" 0 90)

  ;; 2. KOTY (2x DBText)
  (command "-layer" "m" "KOTY" "")
  (make-dbtext "KOTY" "+ 15.50" 50 100)
  (make-dbtext "KOTY" "+ 12.00" 50 90)

  ;; 3. ZELBET (2x MText)
  (command "-layer" "m" "ZELBET" "")
  (make-mtext "ZELBET" "C20/25" 100 100)
  (make-mtext "ZELBET" "C25/30" 100 90)

  ;; 4. STAL (2x MText)
  (command "-layer" "m" "STAL" "")
  (make-mtext "STAL" "Profil HEB 300" 150 100)
  (make-mtext "STAL" "HEB 200" 150 90)

  ;; 5. PPOZ (2x MText)
  (command "-layer" "m" "PPOZ" "")
  (make-mtext "PPOZ" "REI 60" 200 100)
  (make-mtext "PPOZ" "Sciana REI 90" 200 90)

  ;; 6. TABELKA (2x MText)
  (command "-layer" "m" "TABELKA" "")
  (make-mtext "TABELKA" "Rev 1.0" 250 100)
  (make-mtext "TABELKA" "Rysunek Rev 2.0" 250 90)

  ;; 7. RURY (4x Mixed)
  (command "-layer" "m" "RURY" "")
  (make-dbtext "RURY" "DN 50" 0 50)
  (make-mtext "RURY" "DN 100" 0 40)
  (make-dbtext "RURY" "DN 150" 0 30)
  (make-mtext "RURY" "DN 200" 0 20)

  ;; 8. OZNACZENIA (4x Mixed)
  (command "-layer" "m" "OZNACZENIA" "")
  (make-dbtext "OZNACZENIA" "Poz. 100" 50 50)
  (make-mtext "OZNACZENIA" "Poz. 110" 50 40)
  (make-dbtext "OZNACZENIA" "Poz. 120" 50 30)
  (make-mtext "OZNACZENIA" "Poz. 130" 50 20)

  ;; 9. IZOLACJE (4x Mixed)
  (command "-layer" "m" "IZOLACJE" "")
  (make-dbtext "IZOLACJE" "Grubosc 10 cm" 100 50)
  (make-mtext "IZOLACJE" "Grubosc 15 cm" 100 40)
  (make-dbtext "IZOLACJE" "20 cm" 100 30)
  (make-mtext "IZOLACJE" "Grubosc 25 cm" 100 20)

  ;; 10. ELEKTRYKA (4x Mixed)
  (command "-layer" "m" "ELEKTRYKA" "")
  (make-dbtext "ELEKTRYKA" "Moc 10 kW" 150 50)
  (make-mtext "ELEKTRYKA" "Moc 15 kW" 150 40)
  (make-dbtext "ELEKTRYKA" "Zasilanie 20 kW" 150 30)
  (make-mtext "ELEKTRYKA" "25 kW" 150 20)

  (command "zoom" "extents")
  (princ "\nGotowe! Wpisano polecenie TestNumAdd. Wygenerowano teksty i mteksty w odpowiednich warstwach.")
  (princ)
)