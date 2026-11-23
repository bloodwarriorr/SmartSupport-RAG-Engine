export interface SessionUser {
  name: string;
  email: string;
  picture?: string;
}

function readPayload(token: string | null): any | null {
  if (!token) return null;
  try {
    const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    const json = decodeURIComponent(
      atob(base64)
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    return JSON.parse(json);
  } catch {
    return null;
  }
}

/** True when a Google ID token is stored and has not expired. */
export function hasValidSession(): boolean {
  const payload = readPayload(localStorage.getItem('idToken'));
  return !!payload?.exp && Date.now() < payload.exp * 1000;
}

/** The signed-in user's name, email and picture, read from the stored Google ID token. */
export function getSessionUser(): SessionUser | null {
  const payload = readPayload(localStorage.getItem('idToken'));
  if (!payload) return null;
  return {
    name: payload.name || payload.email || 'User',
    email: payload.email || '',
    picture: payload.picture
  };
}

export function clearSession(): void {
  localStorage.removeItem('idToken');
  localStorage.removeItem('isLoggedIn');
}
