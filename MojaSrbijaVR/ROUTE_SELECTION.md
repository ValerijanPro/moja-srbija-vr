# Mapa i ponovna voznja

Postojeci RoutePicker pri Play-u dodaje MapExperience. Sacuvanu scenu nije potrebno
ponovo graditi setup menijima. Izmene su u runtime skriptama.

## Mapa

- Mapa je vertikalna ispred korisnika. Slobodno pomeranje XR riga, snap-turn,
  rotiranje makete i stari automatski nagib su iskljuceni. Pracenje stvarnih
  pokreta glave ostaje ukljuceno.
- Oba kontrolera imaju svoj zrak. Trigger bilo koje ruke bira oznaku regiona,
  stavku u panelu ili rutu. Dugacki laso i preklopljeni nazivi su uklonjeni.
- Jedan grip pomera mapu u njenoj ravni; dva gripa menjaju zum rastojanjem ruku.
  Leva palica gore/dole takodje zumira, oko mesta gde pokazujes na mapu.
- Drzan trigger + palica iste ruke bira susednu rutu u jednom od cetiri smera
  pogleda. Palicu vrati u sredinu pre sledeceg koraka.
- A/B lista pojedinacne aktivnosti iz izabrane grupe slicnih putanja.
- Y ponovo postavlja pregled ispred tebe. X pokrece izabranu rutu.

U pregledu postoje dve razlicite vrste grupisanja:

1. **Slicne putanje:** isti sport, dovoljno slicna duzina, 90% uzoraka unutar
   150 m u oba smera, bez velikog obilaska. Svaki clan mora biti slican svim
   clanovima grupe. Smer prolaska i pocetna tacka petlje nisu bitni. Ovo je
   trenutnih 109 aktivnosti / 95 grupa; xN u naslovu znaci N aktivnosti.
2. **Regioni na ekranu:** prostorno bliske grupe dobijaju zajednicku oznaku
   [ N ] ruta. To ne znaci da su putanje iste. Klik otvara spisak svih grupa
   u regionu, sa stranicama, tako da nije obavezno duboko zumiranje.

Istovremeno se crtaju samo izabrana putanja i eventualno putanja pod pokazivacem.
Sve grupe su dostupne kroz spisak, cak i kada je njihova oznaka van vidnog polja.
Fotografije se prikazuju u panelu aktivnosti, bez oblaka foto-pinova preko mape.

Parametri slicnosti ostaju na RoutePicker komponenti i primenjuju se pri Play-u.
Izvorni podaci se ne brisu niti prepisuju.

## Voznja

Izaberi aktivnost pa pritisni X ili dugme **Pokreni voznju**. Posle zatamnjenja
teren postaje horizontalan i prikazuje se u stvarnoj horizontalnoj razmeri,
uz uklonjeno petostruko preuvelicanje reljefa. Pocetno stanje je pauzirano.

- A ili X: vozi/pauza.
- B ili Y: povratak na mapu, uz vracanje prethodnog zuma i polozaja.
- Leva palica gore/dole: 0.25x do 4x brzine.
- Po zavrsetku ruta staje. A/X ponovo pokrece od pocetka.

Osnovna brzina dolazi iz proseka izabrane aktivnosti. Ovo nije rekonstrukcija
svakog ubrzanja i zaustavljanja, jer replay trenutno koristi liniju rute i
prosecnu brzinu, a ne vremenske streamove aktivnosti.

Kamera je na visini vozaca nad uzorkovanim terenom. Pravac prati putanju sa
horizontalnim horizontom; korisnik i dalje moze slobodno da gleda oko sebe.
Maketa se pomera oko stabilnog koordinatnog pocetka radi preciznosti u VR-u.
Muzika se utisava tokom voznje.

Voznja koristi postojece satelitske teksture i mesh terena. Nema modela zgrada,
kolovoza, saobracaja ili Street View panorame. Ako putanja izlazi van dostupnog
terena, prikazuje se poruka i ostaje pregled mape.

## Provera

Tests/RouteGeometryChecks.cs se kompajlira zajedno sa RouteGeometry.cs i
RouteReplayMath.cs van Assets foldera. Proverava slicnost, sektore selekcije,
regione, interpolaciju, pauzu/kraj replay-a i ocuvanje svih stvarnih aktivnosti.

Kompilacijom proveriti runtime i Editor assembly. U headsetu dodatno proveriti:

1. Oba zraka rade na panelu i oznakama; istovremeni klikovi imaju prednost desne ruke.
2. Grip/palice ne rotiraju mapu i ne ukljucuju slobodno hodanje.
3. Region sa mnogo ruta omogucava pristup svakoj kroz paginaciju.
4. A/B menja statistiku i slike konkretnog clana grupe.
5. Ulazak, pauza, kraj i izlazak iz voznje vracaju mapu i XR rig ispravno.
6. APK ucitava rute, metapodatke, muziku i avatar preko RuntimeData.

APK postupak je u APK_SETUP.md. Uredjaj, performanse i komfor voznje zahtevaju
probu na stvarnom headsetu; C# provere to ne zamenjuju.
