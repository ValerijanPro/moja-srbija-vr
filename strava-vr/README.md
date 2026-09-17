# Strava Srbija 3D — VR projekat (faze 1–2)

Vizuelizacija sopstvenih Strava ruta na 3D terenu Srbije.
Ovaj repo za sada sadrzi fazu 1 (podaci) i fazu 2 (desktop 3D prototip);
faza 3+ je Unity/Quest aplikacija koja koristi iste podatke.

## Brzi start (demo podaci vec generisani)

    cd viewer
    python3 -m http.server 8791
    # otvori http://localhost:8791

Viewer: 3D teren (AWS/Mapzen terrarium DEM) + satelitski snimci (Esri),
rute kao svetlece linije (Run = crveno-narandzasto, Ride = zuto-narandzasto).
- klik na rutu ili na stavku u listi -> statistika + zoom
- "Preleti rutu" -> kamera leti duz rute kroz teren (Esc prekida)
- slajder gore desno -> preuvelicanje reljefa

## Prava Strava (faza 1)

1. https://www.strava.com/settings/api -> napravi aplikaciju
   (Authorization Callback Domain: `localhost`)
2. `cp .env.example .env` i upisi CLIENT_ID/SECRET
3. `python3 strava_fetch.py` — prati uputstva (OAuth copy/paste flow)
4. Rezultat prepisuje `viewer/data/routes.geojson`; osvezi browser.

Opciono `--streams 30` povlaci detaljne streamove (visina/brzina/puls)
za fly-through sa realnom dinamikom (koristice ih i Unity faza).

## Struktura

    strava_fetch.py     Strava OAuth + sve aktivnosti -> GeoJSON (cist stdlib)
    make_demo_data.py   demo rute preko OSRM-a (dok nema pravih podataka)
    viewer/index.html   MapLibre GL 3D prototip (isti izgled cilja Unity scene)
    viewer/data/        routes.geojson (+ streams/<id>.json)

## Sledece faze (Unity/Quest 3S)

- Unity 6 LTS + OpenXR + XR Interaction Toolkit + XR Hands
- Cesium for Unity za isti teren; `viewer/data/*.json` ide u StreamingAssets
- tabletop mapa (grab/rotate/zoom rukama) -> selekcija rute -> 1:1 fly-through
