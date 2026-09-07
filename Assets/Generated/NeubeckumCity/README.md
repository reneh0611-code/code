# Neubeckum – separate Stadt-Erweiterung

18 originale Gebäudemodelle in Metern, mit Fassaden, Fenstern, Balkonen, Dächern und Eingängen. Darunter Bungalows, Bäckerei, Bistro, Restaurant, Polizeistation, Autohandel, weiße Villa, sandfarbenes Stadthaus, schiefergraues Wohnhaus und ein separater Autohof mit befestigtem Hof und markierten Stellplätzen. Bestehende Betontextur und vorhandene Bank-/Fahrradständer-Prefabs werden wiederverwendet. Keine externen Downloads, keine Blender-Laufzeitabhängigkeit.

## Plastische Beschriftung und Gartenanlagen

FASSADEN UND GARTEN enthält massive, rund 6 cm tiefe Buchstaben-Meshes, Gartenmauern und Zäune. Die frühere flache Beschriftung wird ausgeblendet. Die Buchstaben sind eine eigens konstruierte Vektorschrift ohne externe Font-Datei. Alle Details bewegen sich mit der jeweiligen Hausgruppe. Die Eingänge bleiben offen, die Seitenzäune besitzen Collider.

Der Autohof ist ein eigener großer Bautyp und wird nicht über bereits belegte Grundstücke oder Straßen gelegt. Sein Hof hat einen separaten flachen Collider; nur das Gebäude hat einen hohen Collider. Er enthält keine Tankstellen-, Raststätten- oder Geschäftslogik. Falls automatisch kein ausreichend großes Grundstück gefunden wird, steht er als einzelnes Prefab zur manuellen Platzierung bereit.

## Einzelne Gebäude und Giebel-Update

Assets > Refresh importiert die geschlossenen Giebel auch bei bestehenden Modellinstanzen. UpgradeBuildings.request startet einmalig den Export einzelner Prefabs und ergänzt neue Varianten nur auf freien Bauplätzen. Bestehende Häuser werden nicht umpositioniert. Manuell: Day Ones > Stadt > Gebaeude aktualisieren und Varianten hinzufuegen.

Day Ones > Stadt > Einzelne Gebaeude oeffnen zeigt den Ordner EINZELNE GEBAEUDE. Dort liegen 18 Typen; BEREITS PLATZIERT enthält zusätzlich Kopien der bestehenden Häuser inklusive Außenbereichen. Jedes Gebäude hat einen eigenen Auswahlpunkt (CityBuilding/SelectionBase). In der Hierarchie eine Hausgruppe auswählen, mit W bewegen und mit Cmd+D duplizieren. Die Gebäudetypen sind Außenkulissen, keine betriebsfertigen Restaurants, Polizeisysteme oder Fahrzeuggeschäfte.

## Aufbau

Unity importieren lassen und die Szene zzz außerhalb des Play-Modus öffnen. BuildOnce.request startet einmalig den Aufbau. Alternativ: Day Ones > Stadt > Stadt-Erweiterung bauen.

Es werden maximal 96 Häuser auf sicher erkannten, freien und ebenen Flächen neben bodennahen Straßen platziert. Straßen, Terrain, existierende Gebäude und Parkplätze werden nicht verändert. Rund um den bekannten Supermarkteingang bleiben 72 Meter frei. Bei unklarer Erkennung werden Bauplätze ausgelassen, nicht erzwungen.

Alle neuen Objekte liegen unter NEUBECKUM - STADT ERWEITERUNG. Das Kontrollkästchen am Gruppenobjekt blendet alles aus. Day Ones > Stadt > Stadt-Erweiterung entfernen entfernt ausschließlich diese Gruppe (Undo möglich). Die Gruppe wird nach dem Löschen nicht automatisch neu erzeugt.

Die Erweiterung wird als Neubeckum - Stadt.prefab gespeichert. Die bestehende Szene wird ausdrücklich nicht automatisch gespeichert. Nach Sichtprüfung mit Cmd+S speichern, wenn der Aufbau gefällt.

Die aktuelle Ausführung schreibt ihren Status und die platzierten Positionen nach Library/NeubeckumCityResult.json. Ohne diese Rückmeldung ist der Aufbau nicht verifiziert. Modellvorschau und Editor-Code wurden außerhalb des laufenden Editors geprüft; Anordnung, Importmaterialien und Kollisionen benötigen noch Sichtprüfung in Unity.

Gebäude sind zunächst Außenkulissen mit Kollidern, keine begehbaren Innenräume. Vorhandene Spiel-/NPC-/Fahrzeugsysteme werden nicht umgestellt.
