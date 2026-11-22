# Sikkerhed

Skærmlup bruges af mennesker med nedsat syn. En sårbarhed her rammer nogen, der har svært ved
selv at opdage, at noget er galt. Derfor tages henvendelser alvorligt, også de små.

## Sådan melder du en sårbarhed

**Skriv til michael@hummelgaard.eu.** Skriv gerne "Skærmlup — sikkerhed" i emnefeltet.

Lad være med at oprette et almindeligt issue for en sårbarhed. Et issue er offentligt fra det
øjeblik det oprettes, og så er hullet kendt før der findes en rettelse.

Har du mulighed for det, så tag følgende med:

- Hvad du fandt, og hvad en angriber kan bruge det til
- Hvordan det gøres om — trin for trin er bedre end en beskrivelse
- Hvilken version af Skærmlup og hvilken Windows-version
- Om du har fortalt andre om det

## Hvad du kan forvente

| | |
|---|---|
| Kvittering for din henvendelse | Inden for 7 dage |
| Vurdering: er det en sårbarhed, og hvor alvorlig | Inden for 30 dage |
| Rettelse i en udgivelse | Efter alvorlighed. Alvorlige huller går forud for alt andet |

Det her er et fritidsprojekt drevet af én person. Fristerne ovenfor er dem, der kan holdes — og
hellere det end fine løfter, der ikke bliver til noget. Hører du ingenting inden for fristen, så
rykk gerne.

Du får kredit i udgivelsesnoterne, hvis du ønsker det. Sig til, hvis du hellere vil være anonym.

Jeg beder om, at du venter med at offentliggøre, til der er en rettelse ude, eller til der er
gået 90 dage — hvad der end kommer først.

## Hvilke versioner rettes der i

Kun den nyeste udgivelse. Der er ingen ældre grene at vedligeholde. Se
[udgivelserne](https://github.com/mhummelgaard/amd-support-til-windows/releases).

## Hvad der er i spil, og hvad der ikke er

**I spil:** selve programmet, installeren, byggescripterne i `installer/`, og alt hvad der
gør, at et af dem kan komme til at gøre noget, brugeren ikke har bedt om.

**Ikke i spil — det er sådan programmet er bygget, og det står beskrevet:**

- **Programmet læser hele skærmen.** Det er hele meningen med en lup. Se
  [dataflow-og-compliance.md](docs/dataflow-og-compliance.md) for hvor de billedpunkter går hen
  (ingen steder — de bliver i hukommelsen).
- **Programmet lytter på musen på hele systemet.** Ellers kunne luppen ikke tændes med en
  museknap uden først at finde noget på skærmen. Der er ingen tastaturaflytning.
- **Prøvebilleder kan indeholde alt, hvad der stod på skærmen.** Det er meningen med et
  prøvebillede. Programmet advarer om det, når billedet er taget, og IT kan slå prøvebilleder
  helt fra for hele pc'en med gruppepolitik — se [README](README.md#for-it-afdelingen).
- **SmartScreen advarer ved installation.** Programmet er endnu ikke kodesigneret. Det er et
  kendt hul — se G1 i [dataflow-og-compliance.md](docs/dataflow-og-compliance.md).
- Sårbarheder i Windows selv eller i andre programmer.

## Hvad der allerede er gjort

Sikkerhedsegenskaberne er sporet i koden og efterprøvet, ikke bare påstået:

- [Dataflow og compliance](docs/dataflow-og-compliance.md) — hver datastrøm i programmet
- [Trusselsmodel](docs/trusselsmodel.md) — hvem kunne finde på at angribe det, og hvordan
- [Stykliste](docs/sbom/) — hvad der ligger i leverancen
- `pwsh -File docs/efterproev-dataflow.ps1` — fjorten kontroller, der fejler hvis en påstand falder
