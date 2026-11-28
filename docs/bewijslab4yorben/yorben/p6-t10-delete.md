# P6: T10 en T5 live, tuples volgen het verwijderen van een bericht

Datum: 17 november 2026, server g05. Geen wachtwoorden of tokens in dit bestand.

## Opzet

Bericht 18 is aangemaakt door `yorben` (sender) voor `jalil` (recipient) via de app. Een sender kan zijn eigen bericht niet lezen (`can_read: recipient`), dus de UI biedt geen verwijderknop aan hem. Het verwijderen is daarom uitgevoerd met een DELETE-aanvraag vanuit de browserconsole van de ingelogde `yorben`, met zijn eigen toegangstoken (het token werd niet getoond of opgeslagen):

    const a = await import('/js/auth.js'); const t = await a.getToken();
    (await fetch('/api/messages/18', {method:'DELETE', headers:{Authorization:'Bearer '+t}})).status

## Resultaat

| Stap | Uitkomst |
|---|---|
| T5: tuples na de POST (`fga tuple read --object message:18`, 15:19:48Z) | `sender` = `user:e9a8fbf9-...` en `recipient` = `user:d496df03-...`, beide aanwezig |
| T10: DELETE als sender | **204** (console) |
| Tuples na de DELETE | `"tuples": []`, beide tuples weg |

Screenshot: `p6-t10-delete-204-en-tuples.png` (console met 204 en de lege tuple-lijst in de terminal).

## Wat dit aantoont

- De afzender mag verwijderen (can_delete: sender), en de rij én de tuples verdwijnen samen (T10).
- Er blijven geen wees-tuples over in OpenFGA, dus een later bericht met hetzelfde id kan geen oude rechten erven.

## Niet gedaan

- Verwijderen als recipient (verwacht 404 volgens plan T9). Jalil kon bevestigen dat hij geen verwijdering kon uitvoeren; de statuscode is niet apart vastgelegd.
