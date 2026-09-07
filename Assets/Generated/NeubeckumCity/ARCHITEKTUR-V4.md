# Architektur V4 – plastische Fassaden

Katalog: Assets/Generated/NeubeckumCity/ARCHITEKTUR V4 - DETAILLIERT.
Menüs: Day Ones > Stadt > Architektur V4 erstellen / Architektur V4 oeffnen.
Einmaliger automatischer Aufbau nach Assets > Refresh, außerhalb des Play-Modus.

29 separate Gebäude; V1–V3 und alle platzierten Instanzen bleiben unverändert.
Keine automatische Platzierung und keine Änderungen an Straße, Terrain oder Szene.

Rathaus: profilierte Geschoss- und Traufgesimse, Pilaster, Fensterverdachungen,
segmentierter Rundbogen, runde Säulen, Balkon mit Steinbalustern, Stadtsiegel,
Zahnschnitt und historische kupferfarbene Turmhaube mit Laternenabschluss.

Casino: hoher zentraler Baukörper, gestaffelte Messingpfeiler, größere extrudierte
Schrift, Strahlenornament, geschwungene Leuchtmarkise, bordeauxfarbene Schmuckfelder.

Übrige Architektur: maßstäbliche Mauerwerks-/Plattenfugen auf tatsächlichen Wand-
segmenten, Dachdeckung, Gesimse, Fensterlaibungen an historischen Typen, Sockel,
Wandleuchten sowie technische Dachaufbauten und Regenrinnen bei Gewerbebauten.
Kein erneutes Hinzufügen großflächiger Fensterreihen. Öffnungen bleiben frei.

Reliefs und Dachdeckung sind eigene Geometrie ohne zusätzliche Physik pro Stein.
Statische Details werden nach Material zusammengefasst. Zusätzliche Echtzeit-
Schattenlichter werden nicht angelegt; Leuchten arbeiten mit emissiven Materialien.
Die erzeugten Vorschauen liegen in Library/CivicPreviewsV4.

Begehbare Erdgeschosse behalten ihre einfache Ausstattung. Obergeschosse und
Fassadenbalkone bleiben Kulissen ohne Treppen. Keine neuen Jobs, Ladenfunktionen,
Polizei-/Casino-Spielmechaniken oder Multiplayer-Funktionen.

Eingangskorridore werden vor dem Prefab-Speichern auf BoxCollider geprüft.
Kompilierung geprüft. Sichtprüfung nach dem Import, Spieltest separat erforderlich.
