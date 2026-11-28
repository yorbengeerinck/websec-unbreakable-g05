# Y3 / Y5: discovery via de interne poort (backchannel)

Datum: 2026-11-11, uitgevoerd op de server (g05) na deploy van PR #14.
Instelling: `KC_HOSTNAME_BACKCHANNEL_DYNAMIC: "true"` in `docker-compose.yml`.

Commando:

    curl -s http://127.0.0.1:8081/auth/realms/master/.well-known/openid-configuration | grep -E '"issuer"|"jwks_uri"'

Relevante velden uit het resultaat (realm `master`):

| Veld | Waarde |
|---|---|
| `issuer` | `http://localhost:8081/auth/realms/master` |
| `jwks_uri` | `http://127.0.0.1:8081/auth/realms/master/protocol/openid-connect/certs` |
| `token_endpoint` | `http://127.0.0.1:8081/auth/realms/master/protocol/openid-connect/token` |
| `introspection_endpoint` | `http://127.0.0.1:8081/auth/realms/master/protocol/openid-connect/token/introspect` |

Conclusie:
- Backchannel-endpoints (`jwks_uri`, `token_endpoint`, `introspection_endpoint`) volgen het adres van het verzoek. De API haalt sleutels dus intern op en niet via de WAF.
- De issuer van `master` is `localhost:8081` omdat enkel die realm het attribuut `frontendUrl` kreeg (Y3b, nodig voor de admin-console via de tunnel).
- Voor realm `unbreakable` is geen `frontendUrl` ingesteld. Daar moet de issuer `https://g05.ipv6.thelastgym.be/auth/realms/unbreakable` zijn (zie `Oidc__Issuer`). Die controle hoort bij J1, nadat de realm bestaat.
