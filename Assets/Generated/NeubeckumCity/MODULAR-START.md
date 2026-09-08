# Modulare Gebäudeteile

Öffnen: Day Ones > Stadt > Modulare Gebaeude oeffnen.
Erzeugen: Day Ones > Stadt > Modulare Einzelteile erstellen.

87 modulare Fassungen der aktuellen Gebäude: 49 Lokale/Wohnmodelle, 10 Außenvillen (V_), 16 begehbare Wohnvarianten (H_), 12 Außenquartiermodelle (Q_).

Keine Zusammenfassung unterschiedlicher Objekte nach Material mehr. MeshRenderer und MeshFilter bleiben direkt am ursprünglichen Bauteil. Materialien werden weiterhin geteilt; deshalb verändert eine Materialänderung weiterhin mehrere Objekte, das Verschieben jedoch nicht. Für eine unabhängige Farbe zuerst das Material duplizieren.

In der Hierarchy das Haus aufklappen und das Bauteil wählen. Bänke: SITZBANK - komplett verschiebbar. Geldautomaten: GELDAUTOMAT - komplett verschiebbar. Das Gruppenobjekt bewegt Geometrie und Kollision zusammen. W verschiebt, E dreht; Pivot verwendet den Objektursprung. Die Hausauswahl per Klick in der Scene kann weiterhin das Gesamtgebäude auswählen; die direkte Auswahl in der Hierarchy umgeht das.

Bestehende Farbgruppen sind NICHT automatisch getrennt worden. Frühere Prefabs und bereits eingerichtete Szenen bleiben unverändert, um Anpassungen und hinzugefügte Möbel nicht zu verlieren. Neue Fassungen liegen separat unter MODULAR - GEBAEUDE MIT EINZELTEILEN. Ein bestehendes Gebäude muss bewusst durch seine modulare Fassung ersetzt werden, wobei eigene Einrichtung vorher gesichert/übernommen werden muss. Nicht blind alte Szenenobjekte löschen.

Mehr einzelne Renderer erhöhen potenziell den Renderaufwand. Es wird keine erneute dauerhafte Farbzusammenfassung angewendet; spätere Laufzeitoptimierung muss die Bearbeitbarkeit im Editor erhalten. Ergebnisbericht: Library/ModularBuildingsResult.txt.
