# Byggemiljø til Skaermlup.Core og dens tests.
#
# Bemærk hvad der IKKE kan bygges her: Skaermlup.Desktop er net10.0-windows med WPF og
# direkte Win32-kald — skærmkopiering, vindueslag, museknapper. Det findes ikke på Linux og
# kan hverken bygges eller køres i en container. Det hører til på en Windows-maskine.
#
# Til gengæld er Skaermlup.Core rent net10.0 uden en eneste Windows-afhængighed. Det var et
# bevidst valg fra begyndelsen, og det er præcis derfor geometrien, farvefiltrene,
# museudløseren og opskaleringen kan efterprøves her — i det samme miljø hver gang.
FROM mcr.microsoft.com/dotnet/sdk:10.0

# Basisbilledet har allerede en almindelig bruger (ubuntu, uid 1000) og git. Den genbruges
# frem for at oprette en ny: uid 1000 er det samme som på de fleste Linux-værter, og en ny
# bruger ville kollidere med den der er i forvejen.
#
# less er det eneste der mangler for at kunne bruge git interaktivt i containeren.
RUN apt-get update \
    && apt-get install -y --no-install-recommends less \
    && rm -rf /var/lib/apt/lists/*

# Både pakkearkiv og byggeoutput lægges uden for det monterede projekt, og det er
# ikke kosmetik. Monteres et volumen inde i /home/ubuntu/.nuget, opretter Docker
# forældermappen som root, og brugeren kan ikke skrive sin egen NuGet-opsætning.
# Og skriver containeren i projektets egen bin/ og obj/, støder den ind i de filer
# Windows-byggeriet har lagt der: tidsstempler på dem kan ikke sættes hen over
# bind-monteringen fra NTFS, og byggeriet falder fra med MSB3374.
RUN mkdir -p /nuget /artifacts && chown ubuntu:ubuntu /nuget /artifacts
ENV NUGET_PACKAGES=/nuget \
    ArtifactsPath=/artifacts

USER ubuntu
WORKDIR /workspace

# Ingen telemetri og ingen velkomsthilsen ved hvert kald.
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
