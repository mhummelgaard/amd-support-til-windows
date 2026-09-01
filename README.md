# Skærmlup — AMD-support til Windows

Et forstørrelsesglas til skærmen for mennesker med **AMD** (aldersrelateret makuladegeneration)
og anden nedsat syn. Det forstørrer et område af skærmen — tekst, grafik, billeder, alt hvad
der står der — og kan tændes med **ét tryk på en museknap**, uden at brugeren først skal finde
noget lille og ulæseligt på skærmen.

Lokalt Windows-program. .NET 10 / WPF. Ingen konto, ingen internetforbindelse, ingen data
der forlader maskinen.

![Luppen med omvendte farver over almindelig tekst](docs/billeder/luppen.png)

Luppen med omvendte farver, 4x forstørrelse. Krydset viser hvor musen står — musepilen kommer
ikke med i en skærmkopi, så luppen tegner den selv, med en åbning i midten så teksten lige
under markøren ikke dækkes.

![Vejledningsbjælken](docs/billeder/vejledningsbjaelke.png)

Vejledningsbjælken står altid fremme og fortæller hvad luppen laver lige nu, og hvilken knap
der ændrer det.

## Sådan bruger man det

| Handling | Hvad der sker |
|---|---|
| **Midterste museknap** | Tænder og slukker luppen. Kan ændres til sideknapperne eller venstre+højre samtidig. |
| **Ctrl + rul på hjulet** | Mere eller mindre forstørrelse (1,5x til 20x). |
| **Skift + rul på hjulet** | Større eller mindre lup. |
| **Ctrl + Alt + L** | Tænd og sluk fra tastaturet. |
| **Ctrl + Alt + plus / minus** | Forstørrelse fra tastaturet. |
| **Ctrl + Alt + Page Up / Down** | Lupstørrelse fra tastaturet. |
| **Ctrl + Alt + K** | Næste farvetilstand. |
| **Ctrl + Alt + B** | Skift mellem lup og læsebånd. |
| **Ctrl + Alt + H** | Vis eller skjul vejledningsbjælken. |
| **Ctrl + Alt + S** | Åbn kontrolpanelet. |
| **Ctrl + Alt + piletaster** | Flyt luppen uden mus. |

Alle genveje står ét sted i koden — [`Shortcuts.cs`](src/Skaermlup.Core/Text/Shortcuts.cs) — og
både registreringen i Windows og alle tekster på skærmen læser derfra. Vejledningen kan altså
ikke komme til at love noget andet end programmet gør.

## De valg der er truffet for brugerens skyld

**Instruktionerne står altid fremme.** En bjælke langs skærmkanten viser med meget stor skrift
om luppen er tændt, hvad den forstørrer, og hvilken knap der ændrer det — også når luppen er
slukket, for ellers ville brugeren ikke kunne se hvordan man tænder den. Bjælken lægger sig i
skærmens arbejdsområde, så den ikke dækker proceslinjen, og kan gøres op til 250 % større.

**Udløseren er en museknap, ikke et ikon.** Knappen sidder allerede under fingeren.
Udløserklikket holdes som standard tilbage, så browseren ikke også åbner en ny fane når luppen
tændes. Knappen kan sættes til at virke som et rigtigt forstørrelsesglas: tændt så længe den
holdes nede.

**Kanterne genrejses ved høj forstørrelse.** Et skærmbillede er pixels, og detaljer der aldrig
blev optaget, kan ingen regnemetode finde frem. Men måden der forstørres på afgør om teksten
kan læses. Almindelig udglatning gør bogstaverne slørede, og firkantforstørrelse laver
trappetrin på alle kurver. Fra tre gange og opefter findes kantens placering ved udglatning —
den er nøjagtig langt ned under en hel pixel — og dens stejlhed genrejses bagefter, så
overgangen bliver lige så smal som på den oprindelige skærm.

![Luppen ved ti gange forstørrelse](docs/billeder/luppen-10x.png)

**Farvekanterne omkring bogstaver fjernes.** Windows tegner tekst med ClearType, hvor hver
skærmpixels tre farvestriber tændes forskelligt for at placere en bogstavkant en tredjedel
pixel ad gangen. På skærmens egen opløsning smelter striberne sammen for øjet. Forstørret ti
gange bliver de i stedet til brede røde og blå bræmmer — ren støj oven i det man prøver at
læse, og ekstra generende når farvesynet i forvejen er svækket. Farven jævnes derfor ud på
tværs af striberne, mens lysstyrken bliver rørt, så kanten bliver liggende præcis hvor den var.
En rigtig rød knap beholder sin farve, for dens farve er den samme over mange pixels i træk.

**Musen virker som den plejer.** Luppen ligger oven på skærmen, men klik går igennem den:
markøren kan sættes i teksten, filer kan åbnes, knapper kan trykkes. Det sker gennem et lille
hul i vinduet netop dér hvor musen står — se afsnittet om hvordan det er bygget.

**Markøren tegnes selv, og den må ikke være i vejen.** Musepilen kommer ikke med i en
skærmkopi, så luppen tegner selv en markering. Den kan vælges efter hvad man er i gang med:

- **Kryds med hul i midten.** Streger hele vejen ud til kanten, så markøren er let at finde
  igen. Hullet holder teksten fri, og det bliver større mens musen bevæger sig — under en
  flytning er der hverken noget at læse eller nogen markør at sætte, men når musen står
  stille, skal krydset pege så præcist som muligt.
- **Streg under linjen.** Den vandrette streg lægger sig under tekstlinjen i stedet for hen
  over den. Beregnet til at skrive med.
- **Kun en ring**, eller ingen markering.

![Streg under linjen](docs/billeder/markering-understregning.png)

Markeringen er sort på lys baggrund og hvid på mørk, og hver streg har en kant i den modsatte
farve. En fast farve virker ikke: gult ses tydeligt på mørk bund og forsvinder næsten på en
hvid side.

**Farver kan vendes.** Syv tilstande: normale farver, kraftig kontrast, omvendte farver, gul på
sort, sort på gul, hvid på sort og gråtoner, plus kontrast og lysstyrke. Omvendte farver er for
mange med AMD den største enkeltforskel.

**Luppen følger skrivemarkøren.** Skriver man i et tekstfelt, flytter luppen med — indtil musen
røres, for musen har altid forkørselsret. Piletasterne kan flytte luppen helt uden mus.

**Kontrolpanelet er selv til at læse.** Brødtekst på 20 punkter, knapper på mindst 56 pixels,
afkrydsningsfelter tegnet forfra i en størrelse der kan ses, og plus/minus-knapper i stedet for
skydeknapper, som er svære både at ramme og at aflæse.

![Kontrolpanelet](docs/billeder/kontrolpanel.png)

## Hvad en lup ikke kan

Luppen forstørrer et billede af skærmen. Ved ti gange forstørrelse er der kun én hundrededel
så mange rigtige pixels bag hver tomme af det man ser, og de detaljer der aldrig blev optaget,
kan ingen regnemetode finde frem. Kanterne kan gøres skarpe igen — og det gør programmet — men
tegner et program sin tekst med små bogstaver, bliver den forstørrede tekst aldrig lige så fin
som den samme tekst tegnet stor fra begyndelsen.

Derfor er det bedste råd til den der har brug for meget forstørrelse at gøre begge dele:

1. Sæt Windows til at tegne alting større: **Indstillinger → System → Skærm → Skalering**,
   fx 150 % eller 175 %. Nu tegnes bogstaverne stort fra begyndelsen, med rigtige skrifttyper
   og fuld skarphed.
2. I browseren og andre programmer: **Ctrl og plus** gør siden større på samme måde.
3. Brug så luppen til resten. Fire eller fem gange oven i en skærm der allerede er skaleret op,
   giver et markant skarpere billede end femten gange på en uskaleret skærm.

## Sådan er det bygget

```
Skaermlup.Desktop  ──►  Skaermlup.Core
   (WPF + Win32)          (ren logik, ingen Windows-afhængigheder)
```

| Projekt | Ansvar |
|---|---|
| `src/Skaermlup.Core` | Geometri, forstørrelsestrin, farvefiltre, museudløser, indstillinger, alle tekster. Ingen afhængigheder — derfor testbart. |
| `src/Skaermlup.Desktop` | WPF-vinduer, skærmkopiering, globale muse- og tastaturlyttere. |
| `tests/Skaermlup.Tests` | 108 enhedstests af kernen. |

### Sådan vises billedet

1. `BitBlt` fra skærmens DC ned i en genbrugt DIB-buffer (ingen allokering pr. billede).
2. `ColorPipeline` farvebehandler udsnittet med færdigberegnede opslagstabeller.
3. Under tre gange forstørrelse sendes det lille udsnit direkte til grafikkortet, som skalerer
   det op via `Image.Stretch="Fill"`. Det er billigst, og udglatningen kan ikke ses endnu.
4. Fra tre gange og opefter fjernes ClearType-farvekanterne først, og `EdgeUpscaler` regner
   billedet ud i fuld størrelse med kantstyret forstørrelse. Så er der ingen opskalering
   tilbage til grafikkortet, og kanterne står skarpe.

`EdgeUpscaler` er skrevet for at kunne følge med tres gange i sekundet: yderpunkterne i hver
pixels omegn findes én gang pr. billede, kolonneopslaget regnes én gang pr. billede i stedet
for en halv million gange, ensfarvede flader kopieres uden at blive blandet, og arbejdet deles
i nogle få brede bånd — ikke én opgave pr. billedrække, for da koster planlægningen mere end
regnearbejdet. Målt: 1,6 ms pr. billede ved 921x432 pixels.

### Klik gennem luppen

Windows sender kun klik videre til programmet nedenunder, hvis lupvinduet enten er lagdelt
eller har et hul netop dér hvor musen står. Lagdelte vinduer tegnes uden om grafikkortet, og
på en stor lup giver det både flimmer og tre gange så meget processorarbejde — det er målt.
Derfor bruges hullet: syv pixels i radius, altid præcis under musen, og skjult af musepilen
som Windows selv tegner ovenpå.

At det skal være enten-eller er ikke til at gætte sig til. `WS_EX_TRANSPARENT` alene er ikke
nok; klikket lander stadig på luppen. `WM_NCHITTEST` besvaret med `HTTRANSPARENT` hjælper
heller ikke — det svar sender kun videre til vinduer i samme tråd, ikke til andre programmer.
Og `WS_EX_LAYERED` kan ikke sættes udefra på et WPF-vindue: WPF ejer den typografi og fjerner
den igen med det samme.

Luppen lægges desuden sådan at den forstørrede udgave af pixlen under musen havner nøjagtigt
under musepilen. Uden det peger pilen ved siden af det den ligger på, og fejlen vokser med
forstørrelsen — ved ti gange er en halv kildepixel allerede fem pixels på skærmen.

Lupvinduet er sat til `WDA_EXCLUDEFROMCAPTURE`, så det ikke kan se sig selv — ellers ville
luppen forstørre sit eget billede i en uendelig tunnel. Kan Windows ikke det (ældre udgaver),
siger kontrolpanelet det og anbefaler læsebåndet, hvor kilden pr. konstruktion ligger uden for
båndet.

Vinduerne placeres med `SetWindowPos` i rigtige skærmpixels frem for i WPF's egne enheder. Det
er med vilje: med per-skærm-DPI ville en omregning give et halvt uskarpt billede på en skærm
med skalering.

### Belastning

Målt på en Windows 11-maskine med Release-byggeriet, ved 60 billeder i sekundet: cirka **6 %
af én processorkerne** ved fire gange forstørrelse og **5 %** ved ti gange. Med musen i ro
falder det yderligere, fordi luppen selv skruer ned til 20 billeder i sekundet.

"Rolig tilstand" i kontrolpanelet halverer det yderligere.

## Byg og kør

```bash
dotnet run --project src/Skaermlup.Desktop
```

```bash
dotnet test Skaermlup.slnx
```

Installer (kræver [Inno Setup](https://jrsoftware.org/isinfo.php)):

```bash
pwsh installer/build-installer.ps1
```

Resultatet er `installer/out/Skaermlup-Setup-<version>.exe` — self-contained, så .NET ikke
behøver være installeret på maskinen.

## Filer og fejlsøgning

Indstillinger og statusfil ligger under `%LocalAppData%\Skaermlup\`:

- `settings.json` — alle valg. En ødelagt fil lægges til side som `settings.json.ugyldig`,
  og programmet starter på standardindstillingerne.
- `diagnostik.txt` — skrives ved hver opstart: om museknappen og skærmkopieringen fik lov af
  Windows, hvilke genveje der var optaget af andre programmer, og hvor lang tid ét billede tog.

Luppen kan ikke fotograferes udefra, fordi den er skjult for al skærmkopiering. Til support
kan den tegne sig selv:

```bash
Skaermlup.exe --proevebillede=C:\Temp\lup
```

Kører luppen ikke i forvejen, starter den, gemmer billederne og lukker igen. Kører den
allerede — hvilket den gør hos enhver der faktisk bruger programmet — sendes beskeden videre
til den kopi der har luppen, som gemmer billederne og bliver kørende. Gemt bliver et billede
af luppen, vejledningsbjælken, kontrolpanelet og vejledningen. Det er sådan billederne
øverst i denne fil er lavet.

`Skaermlup.exe --start` starter med luppen tændt og uden at åbne kontrolpanelet. Det er den
genvej "Start sammen med Windows" bruger.

## Krav

Windows 10 (version 2004 eller nyere) eller Windows 11, 64 bit. Bygning kræver .NET 10 SDK.

## Licens

[MIT](LICENSE) — kort fortalt: enhver må frit bruge, ændre, dele og videresælge programmet,
også kommercielt, så længe copyright-teksten følger med. Der gives ingen garanti.

Det er valgt med vilje: skal en synskonsulent, en patientforening eller en kommune kunne
installere Skærmlup hos dem der har brug for det — eller bygge videre på det — skal der ikke
først forhandles om lov.

Selve licensteksten står på engelsk. Det er den originale ordlyd, og den bør ikke oversættes:
en oversættelse ville ikke være den samme juridiske tekst.
