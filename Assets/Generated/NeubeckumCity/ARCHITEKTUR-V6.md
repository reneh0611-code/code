# Architektur V6 – Wohnen und Gewerbe

Öffnen: Day Ones > Stadt > Architektur V6 oeffnen.
Ordner: Assets/Generated/NeubeckumCity/ARCHITEKTUR V6 - WOHNEN UND GEWERBE.
Erstellen: Day Ones > Stadt > Architektur V6 erstellen, außerhalb Play-Modus.

49 einzelne Prefabs: die 29 bisherigen Typen plus 20 neue Modelle.
42 begehbare Typen werden mit Root-Skalierung (1, 0.8, 1) gespeichert.
Die 7 nicht begehbaren Wohnvarianten haben Skalierung (1, 1, 1).
Die Höhenskalierung ist bereits enthalten; nicht zusätzlich über einen
skalierten Elternknoten anwenden. Alte Kataloge und Szeneninstanzen bleiben erhalten.

Neue begehbare Modelle:
- 48 Mehrfamilienhaus: 3 Etagen, 6 Wohnungen mit Küche/Wohnraum, Schlafzimmer,
  Bad; reales Treppenhaus mit Deckenöffnungen, Zwischen-/Endpodesten und Geländern.
  Etagenabstand nach Skalierung: 3,2 m. Stufen: etwa 13,3 cm hoch, 30 cm tief,
  Treppenbreite 2 m. Kein begehbarer Dachboden.
- 49 Autohaus: befahrbarer Showroom, freie 5,6 m breite Einfahrt, Empfang und
  vier leere Platzierungsanker für Ausstellungsautos. Keine Fahrzeug-Spawns.
- 50 Werkstatt: drei dauerhaft offene Garagenbuchten, 5,6 m breite und nach
  Skalierung 3,76 m hohe Öffnungen, Werkbänke und dekorative Hebebühnensäulen.
- 51 Skateboardshop mit Deck-Ausstellung und Regalen.
- 52 Nachtclub mit Tanzfläche, DJ-Pult und Lounge.
- 53 Mafia-Villa mit begehbarem Erdgeschoss und Besprechungsraum.
- 54 Premium-Fashionhaus und 55 Streetwear-Shop mit Kleiderständern.
- 56 Immobilienbüro mit Exposé-Tafeln und Beratungstischen.
- 57 Busbahnhof mit vier markierten Haltebuchten, überdachten Wartezonen,
  H-Schildern und begehbarem Wartegebäude; kein Busfahrplan-/Verkehrssystem.
- 58 Trattoria, 59 Asia-Restaurant, 60 Grillhaus: Gastraum und erreichbare Küche.

Neue nicht begehbare Wohnvarianten:
61 Klinkerblock ohne Garten; 62 schmales Reihenhaus ohne Garten;
63 Doppelhaus mit zwei Eingängen und 18 m Gartentiefe;
64 Villa mit 22 m Gartentiefe; 65 Bungalow mit 7 m Gartentiefe;
66 Eckwohnhaus ohne Garten; 67 Stadtvilla mit kompaktem 5-m-Hof.
Wohnpalette, Gesimse, Fensterbänke und Balkone orientieren sich an den vorhandenen
Außenmodellen. Die Grundstücke müssen beim Platzieren vollständig frei bleiben.

Prüfungen vor jedem Speichern:
- Hauptzugänge nach Skalierung auf blockierende BoxCollider.
- Beim Mehrfamilienhaus alle Etagen, beide Wohnungen, Treppen, Podeste und
  Verbindungen in die rückwärtigen Zimmer: Prüfhülle 70 cm breit, 2 m hoch.
- Werkstatt/Autohaus: 2,8 m breite, 2,4 m hohe Durchfahrtskorridore.
- Freier Weg durch die Wartehalle und ausgewählte Innenraumrouten.
Dies sind statische Kollisions-Stichproben, kein CharacterController-/Fahrtest.
Treppensteigen und Fahrverhalten sind zusätzlich im Play-Modus zu testen.

Vorschauen: Library/CivicPreviewsV6.
Kein automatisches Platzieren, kein Umbau von Terrain/Straßen, keine neue Berufs-,
Verkaufs-, Fahrzeug-, Tür- oder Multiplayer-Logik. Die Einrichtung ist einfach und
statisch. Frühere Typen behalten gegebenenfalls ihre nur dekorativen Obergeschosse;
vollständig erreichbare drei Wohn-Etagen sind speziell im neuen Modell 48 umgesetzt.
