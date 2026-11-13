export function setMarker(name, value) {
    document.cookie = name + "=" + encodeURIComponent(value) + "; path=/; samesite=lax";
}

export function getMarker(name) {
    const prefix = name + "=";
    const cookie = document.cookie.split("; ").find(c => c.startsWith(prefix));
    return cookie ? decodeURIComponent(cookie.slice(prefix.length)) : null;
}

export function clearMarker(name) {
    document.cookie = name + "=; path=/; samesite=lax; expires=Thu, 01 Jan 1970 00:00:00 GMT";
}