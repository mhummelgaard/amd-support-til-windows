# Trusselsmodel

Hvem kunne finde på at angribe Skærmlup, hvad ville de være ude efter, og hvad står i vejen.

**Skrevet:** 2026-09-09 · Følgedokument til [Dataflow og compliance](dataflow-og-compliance.md),
som beskriver hvad programmet gør. Her står, hvad der kunne gå galt.

---

## Den ubehagelige udgangsstilling

Skærmlup er, teknisk set, opbygget som spionprogrammel. Det er ikke en anklage — det er hvad
opgaven kræver:

| Evne | Hvorfor den er der | Hvad den ville være værd for en angriber |
|---|---|---|
| Læser hele skærmens indhold mange gange i sekundet | Ellers er det ikke en lup | Alt hvad brugeren ser: netbank, breve, journaler |
| Lavniveaukrog på musen for hele systemet | Ellers kan luppen ikke tændes med en knap | Hvor brugeren klikker, og hvornår |
| Kan starte med Windows | Så luppen er der, når brugeren har brug for den | Fodfæste der overlever genstart |
| Skjuler sit eget vindue for skærmkopiering | Så luppen ikke fylder i en Teams-deling | Et vindue der ikke ses i en fjernsupport-session |

Programmet gør intet af det ondsindet — det er sporet og efterprøvet i
[del A af dataflowdokumentet](dataflow-og-compliance.md). **Men netop derfor er selve
leverancekæden det, der skal beskyttes.** Et program med de evner, byttet ud undervejs, er et
færdigbygget spionprogram, som brugeren selv har installeret og givet lov at starte med Windows.

## Hvem vi taler om

| Angriber | Motiv | Hvor tæt kan de komme |
|---|---|---|
| **A1 — svindleren med telefonen** | Penge. Målgruppen er ældre med nedsat syn, den mest opsøgte gruppe overhovedet | Ringer, taler brugeren til rette, får dem til at gøre noget |
| **A2 — den der lægger en falsk installer ud** | Fodfæste på mange maskiner | Et domæne, en søgeannonce, en vedhæftet fil |
| **A3 — en anden bruger på samme maskine** | Nysgerrighed, kontrol | Fysisk adgang, egen Windows-konto |
| **A4 — programmel der allerede kører** | Eskalering, vedholdenhed | Allerede inde, kører som brugeren |
| **A5 — forsyningskæden** | Bredt fodfæste | En NuGet-pakke, en byggemaskine |

Bemærk, at A1 og A2 ikke angriber koden. De angriber mennesket. Det er hovedtruslen mod dette
program, og det er ikke en, man koder sig ud af.

---

## Truslerne, tungest først

### T1 — Falsk Skærmlup · **Høj** · ikke afværget

**A2.** Nogen bygger en kopi med skærmoptagelse i, kalder den Skærmlup, og lægger den et sted,
hvor den bliver fundet. Brugeren installerer den.

Installeren er **ikke kodesigneret**. Der er derfor ingen måde, hverken brugeren eller Windows
kan skelne den ægte fra en falsk. SmartScreen advarer om begge to på samme måde — og det er det
værste ved det: **den ægte vare oplærer brugeren i at ignorere præcis den advarsel, der skulle
have reddet dem.**

*Det står i vejen i dag:* SHA-256-summen af den udgivne fil står i [styklisten](sbom/), og
udgivelser bærer commit-id i `ProductVersion`. Det hjælper en IT-afdeling. Det hjælper ikke en
78-årig med makuladegeneration.

*Det der skal til:* kodesignering (G1). Den vigtigste sikkerhedsopgave i projektet, og den
eneste der koster penge frem for arbejde.

*Hvad det hjemmelavede certifikat ikke gør:* `installer/opret-selvsigneret-certifikat.ps1` laver
et signeringscertifikat efter samme metode som ESDH PDS — en rod, der stempler ét certifikat og
derefter mister sin nøgle. Det giver uiAccess på de maskiner, hvor roden er installeret. **Det
gør intet ved T1.** Andre maskiner kender ikke roden, og at bede fremmede installere en
hjemmelavet rod er præcis den slags, en svindler ville bede om. Den må aldrig sendes ud til
brugere.

### T2 — Kør lige den her kommando og send mig billederne · **Høj** · delvist afværget

**A1.** Svindleren ringer, udgiver sig for support, og får brugeren til at køre
`--proevebillede` og sende PNG-filerne. Billederne viser skærmen — og brugeren kan i sagens
natur ikke selv læse, hvad der kom med.

*Det står i vejen:* når billederne er gemt, kommer der en advarsel på skærmen i programmets egen
skriftstørrelse: de viser hvad der stod på skærmen, se dem igennem før de sendes, spørg en du
stoler på. Teksten er låst fast med tests, så den ikke kan skrives om til noget venligt og vagt.

*Restrisiko:* en advarsel stopper ikke en, der allerede har talt brugeren rundt. Den gør
risikoen synlig for den, der løber den. Mere kan et program ikke gøre her.

### T3 — Autostart peget et andet sted hen · **Middel** · afhængig af Windows

**A3, A4.** `HKCU\...\Run\Skaermlup` peger på programfilen. Kan nogen skrive i brugerens egen
registreringsgren, kan de pege den på deres eget program, som så starter under et navn,
brugeren stoler på.

*Det står i vejen:* selve programfilen ligger under Programmer og kræver administrator at ændre.
Angriberen kan ikke ændre Skærmlup — kun snyde med, hvad der starter.

*Vurdering:* kan nogen allerede skrive i HKCU, har de fodfæste i forvejen, og der er værre ting
at lave end at pille ved en genvej. Ikke noget programmet kan løse.

### T4 — Musepilen bliver væk · **Middel** · afværget

**Ingen angriber — en fejl.** Luppen skjuler Windows' egen musepil, mens den tegner sin egen.
Går programmet ned i det øjeblik, står brugeren tilbage uden nogen musepil overhovedet.

For denne målgruppe er det ikke en skønhedsfejl. En bruger med AMD, der mister musepilen, kan
reelt ikke betjene sin computer — og kan heller ikke se at læse, hvad der står på skærmen om det.

*Det står i vejen:* der lægges en vagtfil, mens pilen er skjult. Ved næste opstart ser programmet
efter den og sætter markørerne på plads igen. Der ryddes op ad tre veje: normal nedlukning,
`ProcessExit`, og en fejlfanger der holder programmet i live frem for at rive brugerens arbejde
med sig.

### T5 — Forgiftet afhængighed · **Lav** · lille angrebsflade

**A5.** `CommunityToolkit.Mvvm` eller en .NET-runtimepakke kompromitteres opstrøms.

*Det står i vejen:* angrebsfladen er usædvanligt lille — én tredjepartspakke i hele det udgivne
program, og 56 native kald mod udelukkende `user32`, `gdi32` og `kernel32`. Der er
[stykliste](sbom/) over leverancen, og `efterproev-dataflow.ps1` siger fra, hvis der pludselig
kommer en afhængighed mere.

*Restrisiko:* afhængigheder gennemgås ikke systematisk for kendte sårbarheder. Med én pakke er
arbejdet lille, men det sker ikke automatisk.

### T6 — Skærmindholdet lækker ud af programmet · **Lav** · afværget og målt

**A4.** Programmet læser hele skærmen. Sendte det bare en brøkdel videre, ville det være alvorligt.

*Det står i vejen:* billederne bliver i hukommelsen, overskrives ved næste billede og skrives
aldrig til disk. Målt på den installerede udgave: `ws2_32.dll` er aldrig indlæst i processen, og
der var nul netværksendepunkter. Uden Winsock kan der ikke have eksisteret en forbindelse. Se A.5
i [compliancedokumentet](dataflow-og-compliance.md).

### T7 — Luppen bruges til at skjule noget · **Lav** · accepteret

**A3, A4.** Lupvinduet udelukker sig selv fra skærmkopiering. I princippet kan indhold vises på
skærmen, uden at en optagelse eller fjernsupport kan se det.

*Vurdering:* accepteret. Egenskaben findes, fordi luppen ellers ville forstørre sig selv i det
uendelige og fylde hele billedet i en skærmdeling. Nytten er langt større end
misbrugsmuligheden, og den der vil skjule noget for en optagelse har nemmere måder.

### T8 — Indstillingsfilen manipuleres · **Meget lav** · begrænset skade

**A3, A4.** `settings.json` ligger under brugerens profil og kan skrives af enhver, der er
logget ind som brugeren.

*Det står i vejen:* filen indeholder kun præferencer. `Normalize()` retter alt uden for det
tilladte, og en ødelagt fil lægges til side, hvorefter der startes på standarder. Værste udfald
er en lup der ser forkert ud, indtil den stilles tilbage.

---

## Hvad modellen fører til

1. **Kodesignering er den vigtigste opgave i projektet.** T1 er den eneste høje trussel, der
   ikke er afværget, den rammer den mest udsatte gruppe, og den løses med penge frem for arbejde.
2. **Advarslen ved prøvebilleder er gjort** (T2). Mere kan koden ikke gøre ved social
   manipulation.
3. **Angrebsfladen skal holdes lille.** Den er projektets bedste sikkerhedsegenskab, og den er
   nem at miste ved et uheld. Derfor fejler `efterproev-dataflow.ps1`, hvis der kommer en
   afhængighed, et netværkskald eller en fremmed DLL med.
4. **Afhængigheder bør gennemgås ved hver udgivelse.** Med én pakke tager det minutter.

## Hvad modellen ikke dækker

- Angreb der kræver administrator i forvejen. Så er slaget tabt et andet sted.
- Fysisk adgang til en ulåst maskine.
- Windows' egne sårbarheder.
- Andres programmer der læser skærmen. Skærmlup gør ikke skærmen mere læsbar for dem, end den
  var i forvejen.

Modellen gennemgås, når programmet får en ny evne — særligt hvis den rører disk, netværk, andre
processer eller brugerens rettigheder.
