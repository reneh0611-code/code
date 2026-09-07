# Begehbare Lokale und öffentliche Gebäude

17 neue Prefabs: Bank, Jobcenter, Waschsalon, zwei Lager-/Logistikhallen, Fast-Food-Restaurant, Waschstraße, Bahnhof, Rathaus, Casino, Finanzamt, Baustelle, Bar, Motel, Café, Eisdiele und Polizeiwache mit Sicherheitshof.

## Start

Unity außerhalb des Play-Modus importieren lassen. CivicBuildings.request erstellt den Katalog einmalig. Manuell: **Day Ones > Stadt > Begehbare Lokale und Polizei erstellen**.

**Day Ones > Stadt > Begehbare Gebaeude oeffnen** zeigt den Ordner **BEGEHBARE GEBAEUDE**. Alle Prefabs sind einzeln verschiebbar, duplizierbar und löschbar. Sicher platzierbare Typen werden unter der vorhandenen Stadtgruppe in **LOKALE UND OEFFENTLICHE GEBAEUDE** ergänzt. Typen ohne ausreichend große freie Fläche bleiben im Katalog zur manuellen Platzierung. Bestehende Kulissen, Straßen und Grundstücke werden nicht entfernt oder ersetzt. Die Szene wird nicht automatisch gespeichert.

## Räume und Fenster

Die Gebäude besitzen ausgesparte Türöffnungen, offene Türflügel, Innenböden, Innenbeleuchtung und einfache Einrichtung. Fenster verwenden ein transparentes Material und bleiben physisch geschlossen. Die Eingänge werden beim Erstellen durch Stichproben eines 80 cm breiten, 1,8 m hohen Durchgangs auf blockierende BoxCollider geprüft. Die Böden liegen 4 cm über der Grundstückshöhe, um Flackern mit dem Terrain zu vermeiden. Bei manueller Platzierung auf Hanglagen muss die Höhe angepasst werden.

Die Waschstraße hat eine freie Durchfahrt; Bürsten und Portale sind zunächst Kulisse. Der Bahnhof besitzt Empfangs-/Wartebereich und Vordach, keine angeschlossene Bahnstrecke. Das Motel hat offene Zimmerflügel im Erdgeschoss. Die Baustelle ist ein offener Rohbau. Das Casino nutzt massive 3D-Schrift, Goldakzente und emissive Leuchtflächen, ohne Glücksspielsystem.

## Polizei und Hoftor

Die neue Polizeiwache hat transparente Fenster, seitliche und rückwärtige Fenstergitter, einen rückwärtigen Gebäudeausgang und einen 30 x 22 Meter großen eingezäunten Hof. Das Tor hat 6 Meter lichte Breite und wird über die vorhandene **E-Interaktion** am Tor/Bedienpfosten bedient. Ein kinematisches Torblatt fährt seitlich am Zaun entlang. Die Sicherheitszone umfasst Durchfahrt und seitlichen Fahrweg; bei einem erkannten Spieler oder dynamischen Fahrzeug öffnet das Tor wieder. Es verwendet die bestehende lokale/Host-Interaktion; eine Synchronisierung zwischen getrennten Multiplayer-Clients wurde nicht implementiert.

## Spätere Logik

CityBuilding kennzeichnet Typ, Namen und Begehbarkeit. EINGANG - FREI und LOGIK ANKER - SPAETER sind benannte Anschlusspunkte. Berufs-, Polizei-, Wirtschafts-, Kasino-, Wasch- und Bahnbetriebssysteme sind nicht enthalten.

## Prüfung

Library/CivicBuildingsResult.txt protokolliert Katalog und tatsächliche Platzierungen. Library/CivicPreviews enthält automatisch erstellte Modellvorschauen, sofern der Editor rendern kann. Das Tor und die Durchgänge müssen zusätzlich in Play Mode mit dem realen Player und einem Fahrzeug geprüft werden. Kompilierung und geometrische Eingangsstichproben ersetzen diesen Spieltest nicht.
