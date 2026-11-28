# Bijdrage Yorben: koppeling API-PDP, pipeline en live-validatie

Concept voor het verslag (P8). Hoofdstukken volgen de indeling in het plan; Pros bepaalt de definitieve plaatsing en het woordbudget.

**Architectuur en beheer (hoofdstuk 1).** Keycloak en OpenFGA draaien achter BunkerWeb. Enkel BunkerWeb is publiek (poorten 80 en 443). De beheerconsole van Keycloak is alleen bereikbaar via een SSH-tunnel naar `127.0.0.1:8081`; `/auth/admin/` geeft publiek een 403 [screenshot]. OpenFGA wordt beschermd met een preshared key en staat op het interne netwerk.

**Koppeling met de PDP (hoofdstuk 6).** Elke autorisatievraag gaat via `IMessageAuthorizer`. Bij het verzenden schrijft de API de `sender`- en `recipient`-tuples in een transactie. Bij het verwijderen door de afzender verwijdert de API die tuples. Als OpenFGA niet bereikbaar is of de model-ID of sleutel niet klopt, antwoordt de API met 503 (fail closed) en schrijft ze een auditregel met `decision=error`. Het ontwerp geeft bij weigering 404 in plaats van 403, zodat het bestaan van een bericht niet verraden wordt.

**Migratie (cutover).** De overgang van de oude `X-User`-modus naar OIDC gebeurde op 17 november, na een backup. De tijdelijke schakelaar `AUTH_MODE` bestond van PR #14 tot PR #20 en is daarna verwijderd. Terugdraaien kan door PR #20 terug te zetten.

**Testen en bewijs (hoofdstuk 7).** Live bewezen: T5 (tuples na POST), T10 (verwijderen door de afzender geeft 204 en verwijdert de tuples), T21 (OpenFGA niet bereikbaar: 503), T22 (fout model-ID: 503) en T23 (fout preshared key: 503). Na de cutover gaf de restore van de database dezelfde aantallen als voor de cutover.

**Pipeline.** Zes verplichte checks (backend-tests, Semgrep, dotnet-SCA, gitleaks, modeltests en compose-validatie) en een deploy met een smoke-test. De smoke-test controleert het ontworpen gedrag: de app-realm geeft 200 en de master-realm geeft 403.

**Bevindingen en restrisico's (hoofdstuk 8).** Drie punten. Ten eerste maskeerde Docker's userland proxy het IPv4-bronadres als `172.20.0.1`, waardoor één IP-ban alle bezoekers zou treffen. Opgelost met `userland-proxy: false`; het domein heeft enkel IPv6, dus dat is de enige route. Ten tweede draaien de integratietests tegen een stand-in van OpenFGA en niet tegen een echte container; het live-gedrag is met de tests hierboven bewezen. Ten derde zijn de statuscodes T7 tot T9 enkel door geautomatiseerde tests gedekt.

[AI-verantwoording: nog in te vullen door Yorben, telt niet mee in de woordtelling]
