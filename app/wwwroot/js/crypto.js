// Client-side cryptografie voor Unbreakable (opdracht 3).
// - Versleutelen: one-time pad modulo 37 met een lijn uit het codeboek.
// - Integriteit: HMAC-SHA256 via Web Crypto (crypto.subtle).
// De sleutels verlaten de browser nooit: enkel ciphertext, lijnnummer en MAC gaan naar de API.

export const ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_'; // 37 tekens, '_' = spatie
export const LINE_LENGTH = 40;                                  // lengte van één codeboeklijn
const MOD = ALPHABET.length;                                    // 37

// Web Crypto bestaat enkel in een "secure context" (HTTPS of localhost).
function subtle() {
    if (!window.isSecureContext || !window.crypto || !window.crypto.subtle) {
        throw new Error('Web Crypto is niet beschikbaar: open de app via HTTPS (of localhost).');
    }
    return window.crypto.subtle;
}

// Klein -> hoofdletters, accenten weg (é -> E), spatie -> _.
// Andere tekens (leestekens, cijfers met accent, emoji, nieuwe lijn ...) weigeren we
// met een duidelijke fout, zodat de ontvanger nooit een stil gewijzigd bericht krijgt.
export function normalize(text) {
    const upper = String(text)
        .normalize('NFD').replace(/[̀-ͯ]/g, '')
        .toUpperCase()
        .replace(/ /g, '_');
    const invalid = [...new Set([...upper].filter(c => !ALPHABET.includes(c)))];
    if (invalid.length > 0) {
        throw new Error('Niet toegelaten teken(s): ' + invalid.map(c => JSON.stringify(c)).join(' ') +
            '. Gebruik enkel letters, cijfers en spaties.');
    }
    return upper;
}

// Controleert een sleutel (één lijn uit het codeboek, zonder het lijnnummer).
export function checkKey(key) {
    const k = String(key).trim().toUpperCase();
    if (k.length !== LINE_LENGTH || [...k].some(c => !ALPHABET.includes(c))) {
        throw new Error(`De sleutel moet exact ${LINE_LENGTH} tekens uit A-Z, 0-9 en _ zijn (plak enkel de lijn, niet het nummer).`);
    }
    return k;
}

// C[i] = (P[i] + K[i]) mod 37
// Het bericht wordt aangevuld tot 40 tekens met '_' zodat de lengte niet lekt.
export function encrypt(plaintext, key) {
    const k = checkKey(key);
    const p = normalize(plaintext);
    if (p.length === 0) {
        throw new Error('Het bericht is leeg.');
    }
    if (p.length > LINE_LENGTH) {
        throw new Error(`Het bericht is ${p.length} tekens, maximum is ${LINE_LENGTH} (de lengte van de sleutel).`);
    }
    const padded = p.padEnd(LINE_LENGTH, '_');
    return [...padded]
        .map((c, i) => ALPHABET[(ALPHABET.indexOf(c) + ALPHABET.indexOf(k[i])) % MOD])
        .join('');
}

// P[i] = (C[i] - K[i] + 37) mod 37
export function decrypt(ciphertext, key) {
    const k = checkKey(key);
    const c = String(ciphertext);
    if (c.length !== LINE_LENGTH || [...c].some(ch => !ALPHABET.includes(ch))) {
        throw new Error('Ongeldige ciphertext.');
    }
    const padded = [...c]
        .map((ch, i) => ALPHABET[(ALPHABET.indexOf(ch) - ALPHABET.indexOf(k[i]) + MOD) % MOD])
        .join('');
    return padded.replace(/_+$/, '').replace(/_/g, ' ');
}

// ------------------------------------------------------------------ HMAC-SHA256

function hexToBytes(hex, expectedBytes, label) {
    const h = String(hex).trim().toLowerCase();
    if (!new RegExp(`^[0-9a-f]{${expectedBytes * 2}}$`).test(h)) {
        throw new Error(`${label} moet exact ${expectedBytes * 2} hexadecimale tekens zijn.`);
    }
    return Uint8Array.from(h.match(/../g), b => parseInt(b, 16));
}

function bytesToHex(bytes) {
    return [...new Uint8Array(bytes)].map(b => b.toString(16).padStart(2, '0')).join('');
}

function importHmacKey(hex) {
    return subtle().importKey(
        'raw',
        hexToBytes(hex, 32, 'De HMAC-sleutel'),
        { name: 'HMAC', hash: 'SHA-256' },
        false,                 // niet exporteerbaar
        ['sign', 'verify']);
}

// De MAC dekt de ciphertext én de velden die de server zou kunnen verwisselen:
// afzender, ontvanger en lijnnummer. JSON.stringify maakt de invoer eenduidig.
function macInput({ sender, recipient, keyLine, body }) {
    return new TextEncoder().encode(
        JSON.stringify(['UNBREAKABLE-v1', sender, recipient, Number(keyLine), body]));
}

export async function sign(hmacKeyHex, fields) {
    const key = await importHmacKey(hmacKeyHex);
    const mac = await subtle().sign('HMAC', key, macInput(fields));
    return bytesToHex(mac);
}

// verify() vergelijkt in constante tijd; zelf strings vergelijken (===) kan timing lekken.
export async function verify(hmacKeyHex, fields, macHex) {
    const key = await importHmacKey(hmacKeyHex);
    let mac;
    try {
        mac = hexToBytes(macHex, 32, 'De MAC');
    } catch {
        return false;
    }
    return subtle().verify('HMAC', key, mac, macInput(fields));
}
