# Individuelle Architektur V2

Unity: Assets > Refresh, danach Day Ones > Stadt > Individuelle Gebaeude V2 erstellen.
Der einmalige Bauauftrag erledigt den ersten Aufbau nach dem Import automatisch.
Ordner: Assets/Generated/NeubeckumCity/INDIVIDUELLE GEBAEUDE V2.

Separater Katalog: keine automatische Platzierung, kein Austausch der bestehenden
Häuser, keine Veränderung an Straßen, Terrain oder Szenen-Transforms. Die alten
Prefabs in BEGEHBARE GEBAEUDE bleiben erhalten. Größere neue Grundrisse müssen vor
dem Platzieren auf freie Flächen geprüft werden.

Neue Silhouetten:
- Bank: 24 x 16 m, bodentiefe Glasfront, versetzter Oberbau und bronzene Lamellen.
- Rathaus: 30 x 17 m, Beletage, Schieferdach, Eingangsgiebel, Uhrenturm.
- Lagerhalle: 26 x 32 m, 7,5 m Traufe, geschlossenes Satteldach, Oberlichter,
  6 m breites Hauptportal und seitliche geschlossene Sektionaltore.
- Logistikhalle: 38 x 28 m, 9 m Traufe, vier seitliche Torstationen.
- Jobcenter: zweigeschossig; Finanzamt: vier Geschosse mit zurückgesetztem Abschluss.
- Bahnhof: Langdach mit erhöhtem Mittelbau.
- Casino: goldene Art-Deco-Pylone, gestaffeltes Dach, Leuchtmarquise.
- Motel: langgestreckter zweigeschossiger Bau mit Laubengang.
- Polizei: zurückgesetzte Einsatzleitung, blauer Treppenhausturm, Funkmast und
  weiterhin gesicherter Hof mit E-Schiebetor.
- Bar: schmales Backsteinhaus mit steilem Dach, Schornstein und Auslegerschild.
- Café: Ziegeldach und Gauben; Eisdiele: niedriger Glas-Pavillon mit Mintdach.
- Fast Food: breiter flacher Glasbau mit auskragendem roten Dach.
- Waschstraße: schmaler langer Tunnel; Waschsalon: niedriger kleiner Laden mit Abluft.
- Baustelle: weiterhin offener Rohbau statt fertigem Gebäude.

Erdgeschosse behalten die freien Eingänge und einfache Einrichtung. Obergeschosse,
Laubengang und Türme sind Architekturkulissen ohne Treppen oder spielbare Räume.
Sektionaltore sind dekorativ geschlossen; kein Lade- oder Waschspiel implementiert.
Das Polizeitor bleibt eine lokale Interaktion, ohne Multiplayer-Replikation.

Der Aufbau prüft die Eingangskorridore vor dem Speichern. C#-Kompilierung wurde
geprüft; Sichtkontrolle und Begehung in Unity sind separat erforderlich.
