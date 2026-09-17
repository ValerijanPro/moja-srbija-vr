# Zvuk i preporucene ture

## Zvuk: predlog dizajna

- Mapa: tiha akusticna gitara bez vokala ili mekan ambijent. Melodija ne treba
  stalno da privlaci paznju dok se cita panel.
- Interakcije: kratak mekan klik za izbor, diskretna potvrda za promenu rezima.
  Bez zvuka pri svakom prolasku lasera preko linije.
- Voznja: muzika tisa, a kotrljanje, blag vetar i ambijent mesta u prvom planu.
  Zvuci okoline mogu biti prostorni; pozadinska muzika ostaje nedirekciona.
- Dati korisniku nezavisne kontrole muzike i efekata u narednoj iteraciji.

Sada je dodato samo postepeno utisavanje postojece muzike u voznji. Vetar,
kotrljanje i lokacijski ambijenti jos nisu dodati.

## Preporuke koje su sada u aplikaciji

Panel **Preporucene rute** prikazuje do sest stvarnih putanja iz korisnikove
lokalne arhive. Rangiranje trenutno koristi broj ponavljanja slicne putanje,
blizinu uobicajenoj duzini voznje i prepoznate nazive Ada/Avala/Fruska gora.
Uz svaki predlog prikazan je razlog preporuke. Klik otvara istu proverenu trasu,
statistiku, postojece fotografije i pregled trase. GPS podaci se ne salju servisu.

Ovo su preporuke za ponovnu voznju, ne novokreirane staze. Tako korisnik odmah
moze da vidi i proveri ceo tok bez izmisljanja putanje ili stanja puta.

## Sledeca faza: nove ture

Poceti malom kolekcijom stvarnih, proverljivih kandidata (npr. 10-20 GPX/GeoJSON
tura), sa opisom izvora, fotografijama za koje postoji pravo koriscenja i datumom
poslednje provere. Preporuke su zasebne od arhive licnih aktivnosti.

Prvo filtrirati kandidate po sportu, odabranoj podlozi/tipu bicikla, dozvoljenoj
duzini/usponu i pristupu. Nedostajuce podatke o podlozi ili pristupacnosti
prikazati kao nepoznate; ne izvoditi ih samo iz satelitske slike.

Zatim rangirati po:

1. Slicnosti duzine i uspona sa omiljenim/prethodnim aktivnostima korisnika.
2. Udaljenosti pocetka ture od odabrane polazne tacke.
3. Udelu novih deonica koje korisnik jos nije prosao.
4. Izabranim interesovanjima (reka, suma, vidikovac, mirnija voznja).

Svaka preporuka treba da objasni zasto je prikazana. Primer buduce kartice
(ilustracija, ne tvrdnja o stvarnoj turi): "18 km, 90 m uspona, pocetak 3 km od
tebe; slicno tvojim kracim voznjama, vecina trase ti je nova".

Kartica treba da sadrzi naziv, duzinu, procenjeno trajanje sa oznakom procene,
uspon, podlogu ako je poznata, 2-3 stvarne fotografije, kratak opis, razlog
preporuke i dugmad za pregled putanje / cuvanje. Fotografije i opis imaju
naveden izvor i datum; AI ne treba da izmislja stanje staze ili izgled lokacije.

Ovo je predlog naredne faze. U aplikaciju jos nije dodat katalog preporuka,
preuzimanje novih fotografija niti model za rangiranje.
