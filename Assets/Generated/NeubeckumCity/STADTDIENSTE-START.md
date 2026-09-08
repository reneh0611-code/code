# Stadtdienste und Industrie – modular

## Individuelle Architekturüberarbeitung

Weitere Ausarbeitung: Die Kirche besitzt jetzt einen runden, ungefähr 25 m hohen Turm (inklusive Kreuz nach Y=0.8), eine offene Glockengalerie mit Glocke, gestufte Kupferhaube und einen tatsächlich zugänglichen halbrunden Chor hinter dem Altar. Der Turm selbst ist weiterhin nicht als Innenraum ausgebaut. Das Kirchgrundstück benötigt dadurch hinter dem bisherigen Schiff weitere 7 m Platz und seitlich Platz für den Rundturm.

Stein- und Putzfassaden verwenden vorhandene Texturen aus Assets/GeeZyyGames/SuperMarket/Textures. UVs sind maßstäblich an den einzelnen Wandteilen ausgerichtet. Quelltexturen werden nicht verändert und Bauteile nicht farbweise zusammengefasst. Zusätzliche Nutzungsdetails umfassen die Paketannahme-Überdachung, Pfandhaus-Dachgauben und Holzfront, Hallentragwerk, markierte Storage-Tore, Schrottplatzüberdachung, Hofpylon, Trafo-Auffangwannen, Brückengeländer am Wasserwerk, Tankleiterholme und Feuerwehr-Torpakete.

Post: gelbe Dachscheibe, verglaste Ladenfront, asymmetrisches Vordach und Signalturm. Pfandhaus: Mansarddach, historische Holzfront, Sockel und Drei-Kugel-Ausleger. Lagerhalle: drei Shed-Dachabschnitte mit Lichtbändern und Fassadenrippen. Self-Storage: einzelne farbige Torabschlüsse und hochgezogene Rolltorkulissen. Schrottplatz: Kranlaufbahn, Haken und Wellblechdetails. Abschlepphof: langer Fahrzeugunterstand. Stadtwerke: Sammelschienenportale. Wasserwerk: kupferfarbener Dachabschluss, Säulenportal und Rohrleitungen. Tanklager: überhöhte Rohrbrücke. Kirche: Steinportal mit mehreren Bögen, Rosenfenster und Schalllamellen am Turm. Fitnessstudio: Glasfront, diagonale Außenstreben und gelbes Fassadenband. Elektroladen: dunkler Portalrahmen mit cyanfarbenen Lichtkanten. Feuerwehr: verglaster Mitteltrakt, verbindende rote Dachkante, Torrahmen und Turmverkleidung. Duplex: unterschiedlich gestaltete Eingangsüberdachungen.

Alle Ergänzungen bleiben separate Bauteile. Es werden keine zusätzlichen begehbaren Dachgeschosse vorgetäuscht. Der Feuerwehr-Mitteltrakt ist Empfang/Zugang, die seitlichen Buchten sind Fahrzeughallen.

Unity-Menü: Day Ones > Stadt > Stadtdienste und Industrie oeffnen.
Erstellen: gleiches Menü mit „erstellen“, außerhalb des Play-Modus.

14 eigenständige Anlagen/Prefabs im Ordner MODULAR - STADTDIENSTE UND INDUSTRIE. Keine Farb-Mesh-Zusammenfassung; Einrichtungsobjekte und technische Anlagen besitzen eigene Gruppen. In der Hierarchy Gruppe auswählen und verschieben, statt das gemeinsame Material zu bearbeiten. Y-Skalierung 0.8 bereits enthalten.

- Post/Paketshop: offene Zugänge, Theke, Paketregal und Paketstation.
- Pfandhaus: Theke, Regal und Aushänge.
- Duplex/Reihenhaus: zwei getrennte, zweigeschossige Wohneinheiten; Wohngeschosse über Treppen erreichbar.
- Lagerhalle: breite Tore, Hochregale, Dachbinder.
- Self-Storage: Büro, Hof und acht einzeln zugängliche Lagerboxen.
- Schrottplatz: Betriebsbüro, Container mit Metallschrott.
- Abschlepphof: Büro und acht markierte Sicherstellplätze, ohne Fahrzeuge.
- Stadtwerke/Umspannwerk: zugängliches Betriebsgebäude und Transformatorgruppen.
- Wasserwerk: Betriebsgebäude, vier trockene Becken mit Räumerbrücken, kein simuliertes Wasser.
- Tanklager: Betriebsgebäude und sechs geschlossene Lagertanks.
- Kirche: zugängliches Kirchenschiff, Kirchenbänke, Altar, Strebepfeiler, Glockenturm als nicht ausgebautes Architekturteil.
- Fitnessstudio: Trainingshalle mit acht Geräten.
- Elektro-/Handyladen: Verkaufstheke und Vitrinen.
- Feuerwehr: drei offene Gebäudebereiche inklusive Fahrzeugbuchten, Spinden, Vorfeld und technischem Schlauchturm.

Begehbarkeit betrifft Wohn-/Nutzräume und Hofwege. Tanks, Maschinen, Transformatoren, Dachräume und technische Türme sind nicht begehbare Kulissen. Keine Handels-, Lager-, Feuerwehr-, Strom- oder Wassersimulation, keine animierten Tore; Türen/Tore bleiben offen. Keine Pflanzen oder Fake-Wasserflächen.

Industriehöfe benötigen etwa 44 x 58 Meter ebenen Platz. Der Kirchbau und Feuerwehrstandort sind größer als die kleinen Läden. Auf freien Bauplätzen platzieren; Straßen und Terrain werden nicht automatisch geändert. Gebäude sind noch nicht in die Szene gesetzt.

Prüfungen: zentraler 2 m hoher Fußgängerkorridor, Bodenunterstützung, Lagerbox-Zugänge sowie separate Wohnhaus-Treppenprüfung. Prüfung im Play-Modus mit dem tatsächlichen Player und Fahrzeugen bleibt erforderlich. Ergebnis: Library/ServicesCollectionResult.txt. Vorschauen: Library/CivicPreviewsV6/SERVICE_*.png.
