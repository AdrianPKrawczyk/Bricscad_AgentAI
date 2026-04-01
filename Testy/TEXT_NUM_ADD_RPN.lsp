(defun c:TestPoligon ( / make-mtext make-circle make-line make-pline make-hatch)
  (setvar "CMDECHO" 0)

  ;; Generator Zielonego MTextu
  (defun make-mtext (lay txt x y)
    (entmake (list '(0 . "MTEXT") '(100 . "AcDbEntity") (cons 8 lay) '(100 . "AcDbMText")
                   (cons 10 (list x y 0)) '(40 . 1.5) '(41 . 25.0) '(71 . 1)
                   (cons 1 (strcat "{\\C3;" txt "}"))))
  )

  (defun make-circle (lay r x y)
    (entmake (list '(0 . "CIRCLE") (cons 8 lay) (cons 10 (list x y 0)) (cons 40 r)))
  )

  (defun make-line (lay x1 y1 x2 y2)
    (entmake (list '(0 . "LINE") (cons 8 lay) (cons 10 (list x1 y1 0)) (cons 11 (list x2 y2 0))))
  )

  (defun make-pline (lay x1 y1 x2 y2)
    (entmake (list '(0 . "LWPOLYLINE") '(100 . "AcDbEntity") (cons 8 lay) '(100 . "AcDbPolyline") '(90 . 2) '(70 . 0)
                   (cons 10 (list x1 y1)) (cons 10 (list x2 y2))))
  )

  (defun make-hatch (lay x y w h / bnd)
    (command "_.rectang" "_non" (list x y) "_non" (list (+ x w) (+ y h)))
    (setq bnd (entlast))
    (command "_.-LAYER" "_M" lay "")
    (command "_.-HATCH" "_P" "SOLID" "_S" bnd "" "")
    (entdel bnd)
  )

  ;; Scenariusz 1: Zbiorniki (Okręgi -> Objętość)
  (command "_.-LAYER" "_M" "ZB_ZBIORNIKI" "_M" "ZB_OPISY" "")
  (make-circle "ZB_ZBIORNIKI" 2.0 0 100)
  (make-mtext "ZB_OPISY" "V = 0 m3" 0 95)

  ;; Scenariusz 2: Straty Ciepła (Linie -> Moc)
  (command "_.-LAYER" "_M" "CO_RURY" "_M" "CO_OPISY" "")
  (make-line "CO_RURY" 15 100 25 100)
  (make-mtext "CO_OPISY" "Q = 0 W" 15 95)

  ;; Scenariusz 3: Chłód (Polilinie kwadratowe -> kW)
  (command "_.-LAYER" "_M" "KL_POKOJE" "_M" "KL_OPISY" "")
  (command "_.rectang" "_non" '(40 100) "_non" '(45 105))
  (command "_.chprop" (entlast) "" "_LA" "KL_POKOJE" "")
  (make-mtext "KL_OPISY" "P = 0 kW" 40 95)

  ;; Scenariusz 4: Stal (Hatch -> kg)
  (command "_.-LAYER" "_M" "STAL_BLACHY" "_M" "ST_OPISY" "")
  (make-hatch "STAL_BLACHY" 60 100 2 2)
  (make-mtext "ST_OPISY" "m = 0 kg" 60 95)

  ;; Scenariusz 5: Przepływy (Okręgi Promień -> m3/h)
  (command "_.-LAYER" "_M" "WENT_KANALY" "_M" "WENT_OPISY" "")
  (make-circle "WENT_KANALY" 0.1 80 100)
  (make-mtext "WENT_OPISY" "Flow = 0 m3/h" 80 95)

  ;; Scenariusz 6: Ciśnienie (Polilinia Długość -> Pa)
  (command "_.-LAYER" "_M" "WENT_TRASY" "_M" "WENT_OPISY2" "")
  (make-pline "WENT_TRASY" 100 100 110 100)
  (make-mtext "WENT_OPISY2" "dP = 0 Pa" 100 95)

  ;; Scenariusz 7: Grunt (Hatch Pole -> kPa)
  (command "_.-LAYER" "_M" "FUN_STOPY" "_M" "FUN_OPISY" "")
  (make-hatch "FUN_STOPY" 120 100 2 2)
  (make-mtext "FUN_OPISY" "p = 0 kPa" 120 95)

  ;; Scenariusz 8: Kable (Linia Długość -> V)
  (command "_.-LAYER" "_M" "EL_KABLE" "_M" "EL_OPISY" "")
  (make-line "EL_KABLE" 140 100 155 100)
  (make-mtext "EL_OPISY" "dU = 0 V" 140 95)

  ;; Scenariusz 9: Kanały Pow. (Polilinia Długość -> m2)
  (command "_.-LAYER" "_M" "WENT_FLEX" "_M" "FLEX_OPISY" "")
  (make-pline "WENT_FLEX" 170 100 175 100)
  (make-mtext "FLEX_OPISY" "A = 0 m2" 170 95)

  ;; Scenariusz 10: Znaki Parcie (Okręgi Area -> kN)
  (command "_.-LAYER" "_M" "ZNAKI" "_M" "ZNAKI_OPISY" "")
  (make-circle "ZNAKI" 1.0 190 100)
  (make-mtext "ZNAKI_OPISY" "F = 0 kN" 190 95)

  (command "_.zoom" "_extents")
  (princ "\nGotowe! Mega Poligon wygenerowany! Posiada 10 stanowisk z geometrią i zielonymi tekstami RTF.")
  (princ)
)