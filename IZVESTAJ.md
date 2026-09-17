# Moja Srbija VR - izveštaj o projektu

**Predmet:** Virtuelna stvarnost (doktorske akademske studije)
**Autor:** Valerijan Matvejev
**Platforma:** Meta Quest 2 (standalone), Unity 6, OpenXR

---

## 1. Cilj projekta

Cilj projekta je razvoj VR sistema za **imerzivnu vizuelizaciju i istraživanje ličnih
sportskih aktivnosti** (biciklizam, trčanje) na trodimenzionalnom terenu Srbije.
Sistem pripada oblasti *immersive analytics* - korišćenju virtuelne stvarnosti kao
medija za analizu ličnih podataka (engl. *personal informatics*), gde prostorni prikaz,
prirodne interakcije rukama i osećaj razmere omogućavaju uvide koje klasičan
2D prikaz ne pruža.

Konkretni ciljevi:

1. **Automatska akvizicija podataka** - povezivanje sa Strava platformom (OAuth 2.0)
   i preuzimanje kompletne arhive aktivnosti korisnika (GPS putanje, statistika,
   fotografije).
2. **Verna maketa Srbije** - 3D model terena države sa realnim reljefom (digitalni
   model visina) i satelitskim snimcima, isečen po državnoj granici, optimizovan za
   samostalno (standalone) izvršavanje na mobilnom VR uređaju bez pristupa internetu.
3. **Prirodne VR interakcije** - selekcija ruta laserskim pokazivačem i direktnim
   dodirom, manipulacija mapom (pomeranje, zumiranje),
   pregled statistike i fotografija u prostoru.
4. **Analitičke funkcije** - automatsko grupisanje sličnih putanja, pregled
   aktivnosti kroz vreme, ponovno proživljavanje rute („replay" vožnje kroz teren)
   i preporuke narednih ruta.

## 2. Realizacija

### 2.1 Arhitektura sistema

Sistem čine tri celine, povezane jednosmernim tokom podataka:

```
Strava API ──► Python pipeline ──► ispečeni podaci ──► Unity VR aplikacija (Quest 2)
 (OAuth 2.0)    (akvizicija,          (mesh, teksture,      (interakcija, prikaz,
                 obrada, granica)      GeoJSON/TSV)           replay, preporuke)
```

**Python sloj** (`strava-vr/`): OAuth 2.0 autorizacija sa trajnim osvežavanjem tokena;
preuzimanje svih aktivnosti (Strava API `/athlete/activities`, dekodiranje *encoded
polyline* formata); preuzimanje metapodataka fotografija sa GPS lokacijama;
konstrukcija granice Srbije iz OpenStreetMap podataka (unija administrativnih
relacija, Douglas–Peucker simplifikacija); veb-prototip vizuelizacije (MapLibre GL)
korišćen kao dizajn-specifikacija VR scene.

**Sloj pripreme („pekara", Unity editor skripte):** jednokratno preuzimanje visinskih
podataka (AWS Terrarium DEM, zoom 9 i 12) i satelitskih snimaka (Esri World Imagery,
zoom 11 za celu državu + zoom 13 za region sa najviše aktivnosti); reprojekcija
Web-Mercator → ekvirektangularno; generisanje mesh-a terena (~400×440 grid,
isečen *point-in-polygon* testom po granici, sa bočnim zidovima i dnom - izgled
fizičke makete); preuveličanje reljefa 5× radi čitljivosti u minijaturi.
Rezultat je potpuno **offline** sadržaj.

**Unity VR aplikacija** (`MojaSrbijaVR/`): Unity 6 (URP), OpenXR, XR Interaction
Toolkit 3; IL2CPP/ARM64 build za Quest 2.

### 2.2 Ključne funkcionalnosti

- **Selekcija ruta:** kombinovani model - laserski pokazivač na daljinu, projekcija
  vrha kontrolera pri bliskom radu („dodir"), magnetno lepljenje kursora za najbližu
  putanju, i listanje preklopljenih kandidata (A/B dugmad) sa prikazom naziva.
- **Grupisanje sličnih putanja:** aktivnosti istog sporta čije putanje se poklapaju
  (≥90% tačaka unutar 150 m, uz uslov sličnih dužina) prikazuju se kao jedna
  reprezentativna linija; pojedinačne aktivnosti iz grupe se listaju.
  Geometrijski kriterijumi su izdvojeni u čistu C# biblioteku bez Unity zavisnosti.
- **Replay vožnje:** izabrana ruta se može „provozati" - teren se uvećava, a sistem
  pomera svet ispod posmatrača (*floating origin*) duž putanje brzinom iz stvarnog
  zapisa aktivnosti, sa kontrolom tempa i pauze.
- **Fotografije u prostoru:** slike sa aktivnosti lebde kao pinovi iznad mesta
  snimanja (GPS iz Strava zapisa, projektovan na putanju aktivnosti).
- **Interfejs u prostoru:** ekran dobrodošlice sa ukupnom statistikom arhive,
  prostorni meni sa listom ruta i preporukama, panel izabrane rute sa statistikom
  (dužina, uspon, tempo, puls) i fotografijama.
- **Ambijent:** proceduralno generisani zvuci interakcija - hover preko rute,
  zvonce bicikla pri izboru vožnje, koraci trčanja pri izboru trčanja; muzička
  podloga; legenda boja sportova u prostoru; mini-avatar (biciklista/trkač)
  koji se kreće po izabranoj ruti.

### 2.3 Tehnički izazovi i rešenja

| Izazov | Rešenje |
|---|---|
| Streaming globusa (Cesium) neprikladan za maketu: rupe u učitavanju, zakrivljenost, nestabilna razmera | Zamena strimovanog terena **jednokratno ispečenim mesh-om** - deterministički, offline, savršeno poravnat sa rutama (ista projekcija) |
| Razvoj bez podržanog PC-VR hardvera (Intel GPU) | Iterativni razvoj kroz **Virtual Desktop** streaming (Unity Play mode uživo u headsetu) |
| Z-fighting linija ruta pri velikom zumu | Namenski shader sa *depth bias* pomakom |
| Tanke linije kao mete u VR | Magnetna selekcija sa vidljivim radijusom hvatanja i listanjem kandidata |
| StreamingAssets nedostupni kao fajlovi na Androidu | Raspakivanje podataka u *persistent storage* pri prvom pokretanju (build manifest) |

## 3. Ostvareni rezultati

- **Funkcionalna standalone VR aplikacija** na Meta Quest 2 uređaju (bez računara),
  sa kompletnim tokom: dobrodošlica → pregled mape → selekcija → detalji → replay.
- Vizuelizovano **109 aktivnosti (~2.400 km, 13000 m uspona)** iz lične
  četvorogodišnje arhive (2022–2026), automatski grupisano u **95 grupa** putanja.
- Maketa Srbije: mesh od hiljada temena isečen po državnoj granici,
  satelitska tekstura 8K + region visoke rezolucije, potpuno offline.
- Kompletan pipeline je **reproducibilan za bilo kog Strava korisnika** - unosom
  sopstvenih API kredencijala sistem preuzima i vizuelizuje tuđu arhivu bez izmena koda.
- Performanse: stabilnih 120 fps na Quest 2 uređaju.

### Ograničenja i budući rad

- Rezolucija satelitskih snimaka ograničava vizuelni kvalitet replay režima -
  planirano: ispečeni visokorezolucijski „koridori" duž najčešćih ruta ili
  stilizovan prikaz terena u vožnji.
- Preporuke ruta su trenutno heuristične (učestalost, dužina, destinacija);
  planirana je integracija personalizovanog modela tempa (brzina u funkciji nagiba,
  fitovana iz arhive korisnika) za procenu trajanja neistraženih ruta.
- „Magla rata" vizuelizacija pokrivenosti teritorije (istraženo/neistraženo)
  je u pripremi.
- Hand-tracking je podržan od strane sistema, ali je interakcija optimizovana
  za kontrolere; puna optimizacija za gole ruke je budući rad.

## 4. Tehnologije

Unity 6 (URP, OpenXR, XR Interaction Toolkit 3, IL2CPP/ARM64) · Python 3
(stdlib, bez zavisnosti) · Strava API v3 (OAuth 2.0) · AWS Terrarium DEM ·
Esri World Imagery · OpenStreetMap (granica) · MapLibre GL (web prototip) ·
Meta Quest 2 · Virtual Desktop (razvojni streaming)
