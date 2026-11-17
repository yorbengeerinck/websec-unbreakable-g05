// OIDC-login via Keycloak: authorization code flow met PKCE (S256).
// Het access token blijft in het geheugen van keycloak-js. Het komt nooit in
// localStorage, sessionStorage, het DOM of een URL die wij zelf bouwen.

import Keycloak from '../vendor/keycloak-js/keycloak.js';

const ROOT = window.location.origin + '/';

const keycloak = new Keycloak({
    url: 'https://g05.ipv6.thelastgym.be/auth',
    realm: 'unbreakable',
    clientId: 'unbreakable-spa'
});

let ready = null;

async function start() {
    const authenticated = await keycloak.init({
        onLoad: 'login-required',   // geen data tonen vóór er ingelogd is
        pkceMethod: 'S256',
        flow: 'standard',
        checkLoginIframe: false,    // geen iframe (third-party cookies)
        redirectUri: ROOT           // zonder #-route, de SPA landt daarna op de inbox
    });

    if (!authenticated) {
        await login();
        return false;
    }

    // Token bijna of helemaal verlopen: vernieuwen, lukt dat niet dan opnieuw inloggen.
    keycloak.onTokenExpired = () => {
        keycloak.updateToken(30).catch(() => login());
    };
    keycloak.onAuthRefreshError = () => {
        login();
    };
    return true;
}

// Eén keer starten, ook als meerdere plekken initAuth() aanroepen.
export function initAuth() {
    if (!ready) {
        ready = start();
    }
    return ready;
}

// Geeft altijd een vers token (vernieuwt als het binnen 30 s verloopt).
// Let op: async. Gebruik `await getToken()` in api.js.
export async function getToken() {
    try {
        await keycloak.updateToken(30);
    } catch {
        await login();
        return '';
    }
    return keycloak.token || '';
}

export function getSub() {
    return keycloak.tokenParsed?.sub || '';
}

// Alleen voor weergave en voor de MAC-invoer. Nooit voor autorisatie (dat is de sub).
export function getUsername() {
    return keycloak.tokenParsed?.preferred_username || '';
}

export function login() {
    return keycloak.login({ redirectUri: ROOT });
}

export function logout() {
    return keycloak.logout({ redirectUri: ROOT });
}
