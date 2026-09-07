# Stadtgebäude V3 – weniger Glas, zwölf zusätzliche Shops

Nach Assets > Refresh erfolgt der einmalige Aufbau automatisch.
Manuell: Day Ones > Stadt > Shops und Gebaeude V3 erstellen.
Zum Finden: Day Ones > Stadt > Shops und Gebaeude V3 oeffnen.

Ordner: Assets/Generated/NeubeckumCity/STADTGEBAEUDE V3 - SHOPS UND LOKALE

29 einzelne Prefabs: 17 bisherige Gebäudetypen mit reduzierten Fensterflächen und
12 neue Shops. Alte V1/V2-Prefabs und platzierte Instanzen werden nicht ersetzt.
Neue Varianten selbst aus dem Project-Fenster auf freie Grundstücke ziehen.

Fensterkonzept:
- Kleine Shops: ein Schaufenster neben dem freien Mitteleingang, geschlossene
  Seiten und Rückwand. Größere Märkte: zwei begrenzte Schaufenster.
- Casino: geschlossene Fassade statt normaler Fensterreihen.
- Standardgebäude: höchstens zwei kleine Frontfenster, ein seitliches Fenster.
- Obergeschosse: zwei schmale Frontfenster, ein rückwärtiges, geschlossene Stirnwände.
- Hallen: drei kurze Oberlichter je Längsseite statt durchgehendem Glasband.
- Bank: bewusst erhaltene repräsentative Glasfront, reduzierte restliche Fassaden.

Neue Typen 36–47:
36 Bäckerei mit Wohnhaus, Ziegeldach, gestreifter Markise und Kamin.
37 Metzgerei mit roter Markise, Keramikdetails und Kühltheke.
38 Apotheke mit grünem Leuchtkreuz und zurückgesetztem Obergeschoss.
39 Kleiner Kiosk mit Schutzdach und Zeitungsständer.
40 Blumenladen mit Holzpergola und Pflanzkübeln.
41 Friseur mit Barbierzeichen, Stühlen und dekorativen Spiegeln.
42 Modeboutique mit Ateliergeschoss, Steinlamellen und schwarzer Markise.
43 Breiter Elektronikmarkt mit Lichtkanten und Bildschirmattrappen.
44 Backstein-Fahrradladen mit Werkstattdach und Fahrradständern.
45 Tierbedarf mit grüner Dachblende und Warenregalen.
46 Großer Baumarkt mit Hallendach, Blechfassade und orangefarbenem Portal.
47 Schmales Buchhaus mit Obergeschoss, steilem Dach und Holzdetails.

Weiterhin nur einfache Erdgeschoss-Einrichtung, keine Verkaufs-/Berufslogik.
Obergeschosse sind Außenkulissen ohne Treppen. Spiegel sind dekorative Flächen,
keine Echtzeitspiegel. Polizeitor weiterhin lokal, nicht netzwerksynchronisiert.
Die Eingänge werden beim Erzeugen auf blockierende BoxCollider geprüft.
C# kompiliert; Sichtprüfung und Begehung der V3-Modelle nach Unity-Import erforderlich.
