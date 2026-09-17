# APK na pozajmljenom Quest-u

## Pravljenje APK-a

1. Zaustavi Play i sacuvaj otvorenu scenu.
2. Pokreni **MojaSrbija > 10 - Build APK**.
3. Sacekaj poruku `APK gotov` u Console.
4. Rezultat je `Builds/MojaSrbijaVR.apk` unutar Unity projekta.

Opcija proverava Android podrsku, cuva otvorene scene, podesava Android/OpenXR,
pravi APK (ne AAB) i koristi ukljucene scene iz Build Profiles / Scene List.
Unity Hub instalacija mora imati Android Build Support, Android SDK/NDK i OpenJDK.
Novi pre-build korak pravi manifest fajlova za ucitavanje podataka unutar APK-a.

## Instaliranje bez USB kabla: privatni ALPHA kanal

Ovo je odgovarajuci postupak kada USB/ADB uopste nije dostupan. APK se otpremi
na Metin privatni kanal preko interneta, a headset ga zatim preuzme preko Wi-Fi-ja.
Aplikacija se ne objavljuje javno u prodavnici.

1. Prijavi se na [Meta Horizon Developer Dashboard](https://developers.meta.com/horizon/manage/)
   svojim Meta nalogom i napravi developerski tim/organization ako ga jos nemas.
2. Klikni **Create a new app**, upisi npr. `Moja Srbija VR` i izaberi
   **Meta Horizon Store**. To je Quest/Android platforma; izbor ne objavljuje
   aplikaciju javno. `Link PC VR` nije odgovarajuci jer APK radi na headsetu.
3. Ako Meta trazi verifikaciju naloga ili organization admina, zavrsi je u
   istom Dashboard-u.
4. Instaliraj Meta Quest Developer Hub (MQDH) na racunar i prijavi se istim
   developerskim nalogom. Headset ne mora biti povezan sa racunarom.
5. U MQDH otvori **App Distribution**, izaberi aplikaciju i **Upload**.
6. Izaberi `Builds/MojaSrbijaVR.apk` i kanal **ALPHA**, pa sacekaj da obrada
   build-a bude zavrsena. Ako prijavi packaging gresku, ne menjaj scenu; sacuvaj
   tekst greske jer ona govori koje Android podesavanje treba ispraviti.
7. U Dashboard-u otvori aplikaciju > **Distribution > Release Channels > ALPHA**
   i dodaj email Meta naloga koji je trenutno prijavljen na pozajmljenom headsetu.
8. Vlasnik prihvati poziv za testiranje tim Meta nalogom.
9. Na headsetu otvori Store/Library, pronadji **My Preview Apps** (nekad se pojavi
   kao primljeni preview), izaberi aplikaciju i **Install**. Sve ide preko Wi-Fi-ja.

ALPHA kanal je na pocetku prazan, pa i nalog autora mora biti dodat kao tester
ako treba da instalira build. Za pozajmljeni uredjaj ne moras da menjas vlasnikov
nalog: pozovi upravo nalog koji je vec aktivan na headsetu.

[Meta release channels](https://developers.meta.com/horizon/resources/publish-release-channels/),
[MQDH App Distribution](https://developers.meta.com/horizon/documentation/unity/ts-mqdh-deploy-build/)

## Sta Virtual Desktop radi

Virtual Desktop ostaje koristan za razvoj i PCVR probu: igra tada radi na PC-ju.
On ne prenosi APK. ALPHA instalacija je zasebna Android verzija koja se izvrsava
na samom headsetu i ne zahteva da racunar bude ukljucen.
[Virtual Desktop](https://www.vrdesktop.net/)

Ove izmene su proverene kompajlerom i testovima geometrije; APK ovde nije
izgradjen niti instaliran na fizicki headset.
