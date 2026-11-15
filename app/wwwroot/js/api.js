// Praat met de API. Alles wat de SPA weet, komt van hier.
//
// Twee modi (plan 8.4), gekozen bij het opstarten via GET /api/config:
// - legacy: het oude gedrag met X-User. Tijdelijk, verdwijnt in de opruim-PR.
// - oidc:   Authorization: Bearer <access token> via auth.js.
// De server beslist zelf welke modus geldt. Deze keuze bepaalt enkel wat de SPA toont en stuurt.

const USER_KEY = 'unbreakable_user';   // enkel legacy

let mode = 'legacy';
let auth = null;                       // de module auth.js, enkel in oidc-modus

// Vraagt de server in welke modus hij draait. Bij een fout blijft het legacy.
export async function loadMode() {
    let next = 'legacy';
    try {
        const response = await fetch('/api/config', { cache: 'no-store' });
        if (response.ok) {
            const config = await response.json();
            next = config.authMode === 'oidc' ? 'oidc' : 'legacy';
        }
    } catch {
        next = 'legacy';
    }
    mode = next;
    return mode;
}

export function authMode() {
    return mode;
}

// Aanroepen nadat auth.js geladen is en initAuth() klaar is.
export function useOidc(authModule) {
    mode = 'oidc';
    auth = authModule;
}

export function logout() {
    return mode === 'oidc' ? auth.logout() : Promise.resolve();
}

// In oidc-modus komt de naam uit het token (alleen voor weergave en de MAC-invoer).
export function currentUser() {
    return mode === 'oidc' ? auth.getUsername() : localStorage.getItem(USER_KEY);
}

// Enkel legacy. In oidc-modus kies je geen naam meer.
export function setUser(name) {
    localStorage.setItem(USER_KEY, name);
}

async function call(method, path, body) {
    const headers = {};

    if (mode === 'oidc') {
        const token = await auth.getToken();
        if (!token) {
            // getToken() stuurt je naar de login als het token niet te vernieuwen is.
            throw new Error('Niet aangemeld.');
        }
        headers['Authorization'] = 'Bearer ' + token;
    } else {
        headers['X-User'] = currentUser() || '';
    }

    const options = { method, headers, cache: 'no-store' };

    if (body !== undefined) {
        headers['Content-Type'] = 'application/json';
        options.body = JSON.stringify(body);
    }

    const response = await fetch('/api' + path, options);

    if (response.status === 401 && mode === 'oidc') {
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

// In oidc-modus gaan enkel deze vier velden mee. De server zet afzender, onderwerp en datum zelf.
function sendBody(message) {
    if (mode !== 'oidc') {
        return message;
    }
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
