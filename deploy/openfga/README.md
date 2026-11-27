# OpenFGA – model, tuples en herstel
 
Deze map bevat de autorisatieregels van Unbreakable. De API (PEP) beslist zelf niets: ze vraagt OpenFGA (PDP) bij elk verzoek op een bericht. Dit document beschrijft welke informatie OpenFGA daarvoor heeft, wie die informatie schrijft, en wat je doet als de app-databank en OpenFGA niet meer overeenkomen.
 
## 1. Het model
 
```
type user
 
type message
  relations
    define sender: [user]
    define recipient: [user]
    define can_read: recipient
    define can_delete: sender
```
 
- `sender` en `recipient` zijn de enige relaties die opgeslagen worden.
- `can_read` en `can_delete` worden afgeleid: lezen mag wie `recipient` is, verwijderen mag wie `sender` is.
- Het model-ID ligt vast in `/etc/unbreakable/.env` (`OPENFGA_MODEL_ID`). Een nieuw model verandert de beslissingen dus pas als iemand dat ID bewust aanpast (procedure: zie Y4 in het plan).
## 2. Waar haalt de PDP haar informatie?
 
OpenFGA kent enkel **tuples**: wie welke relatie heeft met welk bericht. Ze ziet nooit de inhoud van een bericht, geen gebruikersnamen en geen andere attributen.
 
| Deel van de vraag | Komt van |
|---|---|
| Gebruiker (`user:<sub>`) | De `sub`-claim uit het access token, gevalideerd door de API |
| Object (`message:<id>`) | Het id uit de route of uit de databank |
| Relatie (`can_read` / `can_delete`) | Het endpoint (GET = lezen, DELETE = verwijderen) |
| Het antwoord | De tuples in de OpenFGA-databank |
 
## 3. Tuple-matrix
 
| Actie | Tuple | Geschreven door | Wanneer | Als het mislukt |
|---|---|---|---|---|
| POST bericht | `user:<senderSub> sender message:<id>` | API | Na de commit van de rij in de app-DB | API verwijdert de rij weer en geeft 503 |
| POST bericht | `user:<recipientSub> recipient message:<id>` | API | In dezelfde write (alles of niets) | Idem |
| DELETE bericht | Beide tuples weg | API | Na het verwijderen van de rij | Tuples blijven staan (wees-tuple), onschadelijk |
 
Niemand anders schrijft tuples. OpenFGA is enkel bereikbaar op de interne Docker-netwerken `app` en `authz` en vraagt een preshared key.
 
**Volgorde bij POST: eerst de rij, dan de tuples.** Was het omgekeerd, dan kon er even een tuple bestaan voor een bericht dat (nog) niet in de databank staat.
 
**Volgorde bij DELETE: eerst de rij, dan de tuples.** De API haalt altijd eerst de rij op. Bestaat die niet, dan is het antwoord 404, wat de tuples ook zeggen.
 
## 4. Object-ID-conventie
 
- Object = `message:<Id>`, met `Id` de primaire sleutel van `Int messages`.
- PostgreSQL geeft nooit twee keer hetzelfde id, zolang de databank niet los van OpenFGA hersteld wordt (zie §6).
- Gebruiker = `user:<sub>`, met `sub` het Keycloak-ID (onveranderlijk). Nooit de gebruikersnaam: die kan wijzigen en hergebruikt worden.
## 5. Wat kan er misgaan
 
De app-DB en OpenFGA zijn twee aparte databanken. Er is geen transactie over beide heen, dus ze kunnen uit elkaar lopen.
 
| Situatie | Gevolg | Gevaarlijk? |
|---|---|---|
| **Weesrij**: rij bestaat, tuples niet (crash of fout na de INSERT) | Niemand ziet het bericht | Nee, fail closed. Enkel beschikbaarheid. |
| **Wees-tuple**: tuples bestaan, rij niet (crash of fout na de DELETE) | Niets: GET geeft 404, de inbox vindt geen rij | Nee, zolang id's niet hergebruikt worden |
| **Id-hergebruik**: enkel de app-DB hersteld uit een oudere backup | Een nieuw bericht krijgt het id van een oud bericht en erft de oude tuples. Een verkeerde persoon kan het lezen of verwijderen. | **Ja.** Zie §6. |
 
## 6. Runbook
 
### 6.1 Rijen en tuples vergelijken
 
Op de server, in de map van de repo.
 
**Tuples lezen** (de sleutel komt uit `.env` en wordt niet getoond):
 
```bash
set -a; . /etc/unbreakable/.env; set +a
sudo -E docker run --rm --network websec-unbreakable-g05_authz \
  -e FGA_API_URL=http://openfga:8080 -e FGA_API_TOKEN="$OPENFGA_API_TOKEN" \
  openfga/cli tuple read --store-id "$OPENFGA_STORE_ID" --output-format=simple-json
```
 
**Rijen lezen** (psql in de app-DB):
 
```sql
SELECT "Id", "SenderSub" IS NOT NULL AS heeft_subs FROM "Messages" ORDER BY "Id";
```
 
**Vergelijken:**
 
- Id in `Messages` zonder tuples → **weesrij**. Oplossen: de twee tuples opnieuw schrijven met de subs uit de rij (`tuple write`), of de rij verwijderen als het bericht niet meer nodig is. Oude lab 3-rijen zonder subs blijven bewust onzichtbaar.
- `message:<id>` in de tuples zonder rij → **wees-tuple**. Opruimen:
```bash
... openfga/cli tuple delete --store-id "$OPENFGA_STORE_ID" user:<sub> sender message:<id>
```
 
### 6.2 Na een restore
 
1. Herstel **altijd de app-DB en de OpenFGA-DB samen**, uit backups van hetzelfde moment (zie `y9-backup-restore.md`).
2. Is toch enkel de app-DB hersteld: zorg dat het volgende id hoger is dan elk id in de tuples, **voor** de app weer berichten aanneemt.
```sql
SELECT max("Id") FROM "Messages";
-- als het hoogste message-id in de tuples groter is:
SELECT setval(pg_get_serial_sequence('"Messages"', 'Id'), <hoogste tuple-id>);
```
 
3. Doe daarna de vergelijking uit §6.1 en enkele tests uit de testmatrix (T6–T9).

