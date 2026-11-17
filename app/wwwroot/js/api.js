// Praat met de API. Alles wat de SPA weet, komt van hier.
//
// Authenticatie: Authorization: Bearer <access token> via auth.js (Keycloak, Authorization Code + PKCE).
// De server controleert het token en vraagt OpenFGA wie wat mag; de SPA bepaalt niets over toegang.

let auth = null;                       // de module auth.js, gezet door start() in app.js

// Aanroepen nadat auth.js geladen is en initAuth() klaar is.
export function useOidc(authModule) {
    auth = authModule;
}

export function logout() {
    return auth.logout();
}

// De naam komt uit het token (alleen voor weergave en de MAC-invoer).
export function currentUser() {
    return auth ? auth.getUsername() : '';
}

async function call(method, path, body) {
    const headers = {};

    const token = await auth.getToken();
    if (!token) {
        // getToken() stuurt je naar de login als het token niet te vernieuwen is.
        throw new Error('Niet aangemeld.');
    }
    headers['Authorization'] = 'Bearer ' + token;

    const options = { method, headers, cache: 'no-store' };

    if (body !== undefined) {
        headers['Content-Type'] = 'application/json';
        options.body = JSON.stringify(body);
    }

    const response = await fetch('/api' + path, options);

    if (response.status === 401) {
        await auth.login();
        throw new Error('Je sessie is verlopen. Je wordt opnieuw aangemeld.');
    }
    if (response.status === 503) {
        throw new Error(`${method} ${path} gaf 503: de autorisatieservice is niet bereikbaar. Probeer het later opnieuw.`);
    }
    if (!response.ok) {
        const detail = (await response.text().catch(() => '')).slice(0, 200);
        throw new Error(`${method} ${path} gaf ${response.status}${detail ? ': ' + detail : ''}`);
    }

    const type = response.headers.get('Content-Type') || '';
    return type.includes('application/json') ? response.json() : response.text();
}

// Enkel deze vier velden gaan mee. De server zet afzender, onderwerp en datum zelf.
function sendBody(message) {
    return {
        recipient: message.recipient,
        keyLine: message.keyLine,
        body: message.body,
        mac: message.mac,
    };
}

export const api = {
    users: () => call('GET', '/users'),
    inbox: (from) => call('GET', '/messages' + (from ? '?from=' + encodeURIComponent(from) : '')),
    search: (q) => call('GET', '/messages/search?q=' + encodeURIComponent(q)),
    message: (id) => call('GET', '/messages/' + id),
    send: (message) => call('POST', '/messages', sendBody(message)),
    remove: (id) => call('DELETE', '/messages/' + id),
    exportJson: () => call('GET', '/messages/export'),
};
